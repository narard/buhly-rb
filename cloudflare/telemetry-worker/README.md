# Buhly Anonymous Daily Opens Worker (Cloudflare)

Минимальный open-source воркер для подсчёта **уникальных открытий программы в день** без сбора персональных данных, IP-адресов или ID устройств.

## Как работает
- Десктопная программа `Buhly.exe` (начиная с релиза, где подключён `https://buhly.by/api/version`) при первом запуске за календарный день (`UTC+3`) делает один анонимный `GET`-запрос проверки новой версии.
- Воркер возвращает актуальный `tag_name` из GitHub Releases и увеличивает целочисленный счётчик `opens:YYYY-MM-DD` в Cloudflare KV.
- Админка `Buhly_Dashboard.html` читает суммарную статистику через `GET https://buhly.by/api/stats`.

## Деплой в Cloudflare Dashboard (2 минуты)
1. В Cloudflare: **Workers & Pages → KV** создайте namespace **`BUHLY_STATS`**.
2. Создайте Worker **`buhly-stats`** и вставьте код из `worker.js`.
3. В настройках воркера **Settings → Bindings** добавьте KV namespace с именем переменной **`STATS_KV`** → `BUHLY_STATS`.
4. В **Settings → Domains & Routes → Add Route** добавьте маршрут: `buhly.by/api/*` (зона `buhly.by`).
5. Проверка: откройте `https://buhly.by/api/version` и `https://buhly.by/api/stats`.
