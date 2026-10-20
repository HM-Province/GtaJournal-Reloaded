-- Опциональная система тегов подразделений: "[ОКБ-М] Nick_Name".
CREATE TABLE unit_tags (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    tag        TEXT    NOT NULL COLLATE NOCASE UNIQUE,  -- подстрока, которую ищем в теге ника
    title      TEXT    NOT NULL,                        -- подпись в статистике
    color      TEXT    NOT NULL,                        -- #RRGGBB
    sort_order INTEGER NOT NULL DEFAULT 0
);

INSERT INTO unit_tags (tag, title, color, sort_order) VALUES
    ('ОКБ-М', 'ОКБ',   '#3B82F6', 1),
    ('ЦГБ-П', 'ЦГБ-П', '#EF4444', 2),
    ('ЦГБ-Н', 'ЦГБ-Н', '#22C55E', 3);

INSERT OR IGNORE INTO settings (key, value) VALUES ('use_tags', '0');
