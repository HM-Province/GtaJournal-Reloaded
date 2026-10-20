-- Ссылки на аватарки пользователей журнала (по нику без тега): в топе и меню аккаунтов
-- аватарок нет, берём последние известные с главной.
CREATE TABLE user_avatars (
    username   TEXT NOT NULL COLLATE NOCASE PRIMARY KEY,
    avatar_url TEXT NOT NULL,
    updated_at TEXT NOT NULL
);
