# Buhly Anonymous Telemetry & Version Worker (Cloudflare)

Минимальный open-source воркер для проверки обновлений и подсчёта **анонимных открытий программы в день** и **скачиваний с сайта** без сбора персональных данных, IP-адресов или ID устройств.

## Как работает
- **Проверка версии (`GET /api/version`):** десктопная программа `Buhly.exe` при первом запуске за календарный день (`UTC+3`) делает один анонимный `GET`-запрос. Воркер возвращает актуальный `tag_name` из GitHub Releases и увеличивает счётчики `opens:YYYY-MM-DD` и `opens:total` в Cloudflare KV. Запросы с другими методами (например, `HEAD`) счётчики не увеличивают.
- **Учёт скачиваний (`GET /api/download/zip` и `GET /api/download/exe`):**
  - В обычном режиме увеличивает счётчики `dl:YYYY-MM-DD` / `dl:total` (только для `GET`) и отдаёт `302` редирект на последний релиз в GitHub.
  - С параметром **`?ping=1`** (`/api/download/zip?ping=1`, `/api/download/exe?ping=1`) увеличивает те же счётчики (только для `GET`) и возвращает **`204 No Content`** с CORS-заголовками. Это позволяет лендингу `buhly.by` считать клики через `fetch(..., { keepalive: true })` / `sendBeacon`, оставляя прямую ссылку `href` на GitHub Releases без изменений.
- **Статистика (`GET /api/stats`):** админка `Buhly_Dashboard.html` и скрипт `collect_stats.py` читают агрегированные данные за 30 дней.

## Авторизация `/api/stats` (`STATS_TOKEN`)
- Для защиты `/api/stats` задайте секретную переменную **`STATS_TOKEN`** в настройках воркера (**Settings → Variables and Secrets → Add → Secret**).
- Токен передаётся **строго в HTTP-заголовке `X-Stats-Token`** (передача через URL `?token=` отключена, чтобы токен не попадал в логи и историю браузера). Проверка выполняется в constant-time режиме (`timingSafeEqual`).
- В `collect_stats.py` токен подхватывается из переменной окружения `BUHLY_STATS_TOKEN` (или `STATS_TOKEN`).

## Ограничения Cloudflare KV Free
- Бесплатный тариф Cloudflare KV ограничен **1 000 операциями записи (`put`) в сутки** и **100 000 чтений в сутки**.
- Каждое учитываемое `GET`-событие (`/api/version` или `/api/download/*`) выполняет **2 записи в KV** (`<prefix>:YYYY-MM-DD` с TTL 90 дней и `<prefix>:total`), то есть лимита KV Free хватает примерно на **500 событий в сутки** (плюс редкие обновления кэша релиза раз в 10 минут).
- Все ошибки записи/лимитов KV в `incrementCounter` перехватываются без падения ответа — при исчерпании дневного лимита KV проверка обновлений и скачивания продолжают работать штатно. При росте нагрузки выше ~450–500 запусков/день перейдите на Workers Paid или перейдите на Durable Objects / батчирование.

## Деплой в Cloudflare Dashboard
1. В Cloudflare: **Workers & Pages → KV** создайте namespace **`BUHLY_STATS`**.
2. Создайте Worker **`buhly-stats`** и вставьте код из `worker.js`.
3. В настройках воркера **Settings → Bindings** добавьте KV namespace с именем переменной **`STATS_KV`** → `BUHLY_STATS`.
4. В **Settings → Variables and Secrets** добавьте Secret **`STATS_TOKEN`** (произвольная длинная случайная строка).
5. В **Settings → Domains & Routes → Add** подключите маршрут:
   - основной вариант: `buhly.by/api/*` (зона `buhly.by`, если домен проксируется через Cloudflare);
   - альтернативный кастомный маршрут / поддомен: **`api.buhly.by`** (Custom Domain или Route `api.buhly.by/*`), если основной сайт `buhly.by` смотрит напрямую на GitHub Pages и вы хотите отделить API-поддомен.
6. Проверка:
   - `GET https://buhly.by/api/version` (или `https://api.buhly.by/api/version`)
   - `GET https://buhly.by/api/stats` с заголовком `X-Stats-Token: <ваш_токен>`
