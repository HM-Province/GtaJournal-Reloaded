-- Аккаунты журнала. Пароли не храним: только cookie сессии и (по желанию) TOTP-секрет.
CREATE TABLE accounts (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    login            TEXT    NOT NULL COLLATE NOCASE UNIQUE,
    display_name     TEXT    NULL,
    avatar_url       TEXT    NULL,
    user_agent       TEXT    NOT NULL,
    client_hints     TEXT    NULL,     -- значение sec-ch-ua (только для Chromium-браузеров)
    totp_secret      BLOB    NULL,     -- секрет, зашифрованный DPAPI (CurrentUser)
    cookies          TEXT    NULL,     -- JSON с cookie сессии
    created_at       TEXT    NOT NULL,
    last_login_at    TEXT    NULL
);

-- Настройки приложения: ключ/значение.
CREATE TABLE settings (
    key   TEXT NOT NULL PRIMARY KEY,
    value TEXT NOT NULL
);
