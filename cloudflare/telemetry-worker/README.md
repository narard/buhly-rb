# Buhly Anonymous Telemetry & Version Worker (Cloudflare)

Минимальный open-source воркер для проверки обновлений и подсчёта **анонимных открытий программы в день** и **скачиваний с сайта** без сбора персональных данных, IP-адресов или ID устройств.

## Как работает
- **Проверка версии (`GET /api/version`):** десктопная программа `Buhly.exe` при первом запуске за календарный день (`UTC+3`) делает один анонимный `GET`-запрос на `https://api.buhly.by/api/version`. Воркер возвращает актуальный `tag_name` из GitHub Releases и делает **1 запись в KV** — увеличивает счётчик `opens:YYYY-MM-DD` (TTL 365 дней). Запросы с другими методами (например, `HEAD`) счётчики не увеличивают.
- **Учёт скачиваний (`GET /api/download/zip` и `GET /api/download/exe`):**
  - В обычном режиме делает **1 запись в KV** (`dl:YYYY-MM-DD`, TTL 365 дней, только для `GET`) и отдаёт `302` редирект на последний релиз в GitHub.
  - С параметром **`?ping=1`** (`/api/download/zip?ping=1`, `/api/download/exe?ping=1`) увеличивает тот же дневной счётчик (только для `GET`) и возвращает **`204 No Content`** с CORS-заголовками. Это позволяет лендингу `buhly.by` считать клики через `fetch(..., { keepalive: true })` / `sendBeacon`, оставляя прямую ссылку `href` на GitHub Releases без изменений.
- **Статистика (`GET /api/stats`):** админка `Buhly_Dashboard.html` и скрипт `collect_stats.py` читают агрегированные данные за 30 дней. Итоги `opens_total` и `site_downloads_total` вычисляются как `Math.max(legacyTotal, sumAllDaily)` через `kv.list` с пагинацией по `cursor` (игнорируя legacy-ключи `opens:total` / `dl:total`), поэтому ранее накопленная история сохраняется без потерь.

## Авторизация `/api/stats` (`STATS_TOKEN`)
- Для защиты `/api/stats` задайте секретную переменную **`STATS_TOKEN`** в настройках воркера (**Settings → Variables and Secrets → Add → Secret**).
- Токен передаётся **строго в HTTP-заголовке `X-Stats-Token`** (передача через URL `?token=` отключена, чтобы токен не попадал в логи и историю браузера). Проверка выполняется в constant-time режиме (`timingSafeEqual`).
- В `collect_stats.py` токен подхватывается из переменной окружения `BUHLY_STATS_TOKEN` (или `STATS_TOKEN`).

## Оптимизация под Cloudflare KV Free (1 write/req)
- Бесплатный тариф Cloudflare KV ограничен **1 000 операциями записи (`put`) в сутки** и **100 000 чтений в сутки**.
- Каждое учитываемое `GET`-событие (`/api/version` или `/api/download/*`) выполняет **ровно 1 запись в KV** (`<prefix>:YYYY-MM-DD` с TTL 365 дней; отдельные записи в `<prefix>:total` убраны). Это удваивает эффективную квоту KV Free до **~1 000 событий в сутки** (за вычетом редких обновлений кэша релиза раз в 10 минут).
- Все ошибки записи/лимитов KV в `incrementCounter` перехватываются без падения ответа — при исчерпании дневного лимита KV проверка обновлений и скачивания продолжают работать штатно. При росте нагрузки выше ~900–1 000 запусков/день перейдите на Workers Paid или на Durable Objects / батчирование.

## Деплой в Cloudflare Dashboard и Custom Domain `api.buhly.by`
1. В Cloudflare: **Workers & Pages → KV** создайте namespace **`BUHLY_STATS`**.
2. Создайте Worker **`buhly-stats`** и вставьте код из `worker.js`.
3. В настройках воркера **Settings → Bindings** добавьте KV namespace с именем переменной **`STATS_KV`** → `BUHLY_STATS`.
4. В **Settings → Variables and Secrets** добавьте Secret **`STATS_TOKEN`** (произвольная длинная случайная строка).
5. В **Settings → Domains & Routes → Add → Custom Domain** добавьте основной домен API: **`api.buhly.by`** (Cloudflare автоматически создаст DNS-запись и выпустит TLS-сертификат; основной сайт `buhly.by` при этом может продолжать смотреть напрямую на GitHub Pages).
   - Fallback-адрес воркера `https://buhly-stats.repinm911.workers.dev` остаётся активным на время переключения DNS и поддерживается в `collect_stats.py` автоматически.
6. Проверка:
   - `GET https://api.buhly.by/api/version`
   - `GET https://api.buhly.by/api/stats` с заголовком `X-Stats-Token: <ваш_токен>`
