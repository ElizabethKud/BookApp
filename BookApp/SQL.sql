-- Создание таблицы пользователей
CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(100) NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
	email VARCHAR(100) NOT NULL UNIQUE,
    registration_date TIMESTAMP NOT NULL DEFAULT NOW(),
);

-- Создание таблицы книг
CREATE TABLE IF NOT EXISTS books (
    id SERIAL PRIMARY KEY,
    title VARCHAR(255) NOT NULL,
    publication_year INTEGER CHECK (publication_year > 0),
    pages_count INTEGER,
    language VARCHAR(50),
    file_path VARCHAR(255) NOT NULL, -- Для локальных книг
	is_default BOOLEAN NOT NULL DEFAULT FALSE
);

-- Создание таблицы авторов
CREATE TABLE IF NOT EXISTS authors (
    id SERIAL PRIMARY KEY,
    last_name VARCHAR(255),
    first_name VARCHAR(255),
    middle_name VARCHAR(255),
    birth_year INTEGER,
    country VARCHAR(100)
);

-- Создание таблицы оценок
CREATE TABLE IF NOT EXISTS ratings (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    book_id INTEGER NOT NULL REFERENCES books(id) ON DELETE CASCADE,
    rating INTEGER NOT NULL CHECK (rating BETWEEN 1 AND 10),
    rating_date TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE (user_id, book_id) -- чтобы один пользователь мог поставить оценку только один раз
);


-- Создание таблицы жанров
CREATE TABLE IF NOT EXISTS genres (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL UNIQUE
);

-- Создание таблицы закладок
CREATE TABLE IF NOT EXISTS bookmarks (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    book_id INTEGER NOT NULL REFERENCES books(id) ON DELETE CASCADE,
    page_number INTEGER NOT NULL,
    name VARCHAR(255),
    date_added TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Создание таблицы настроек оформления
CREATE TABLE IF NOT EXISTS display_settings (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    background_color VARCHAR(50), -- Например, #FFFFFF
    font_color VARCHAR(50),
    font_size INTEGER DEFAULT 16,
    font_family VARCHAR(100) DEFAULT 'Arial'
);

-- Создание таблицы избранных книг
CREATE TABLE IF NOT EXISTS favorite_books (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    book_id INTEGER NOT NULL REFERENCES books(id) ON DELETE CASCADE,
    date_added TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE (user_id, book_id) -- нельзя добавить одну и ту же книгу дважды
);

-- Создание таблицы истории чтения
CREATE TABLE IF NOT EXISTS reading_history (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    book_id INTEGER NOT NULL REFERENCES books(id) ON DELETE CASCADE,
    last_read_page INTEGER,
    last_read_date TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE (user_id, book_id) -- одна запись на книгу на пользователя
);

-- Создание таблицы связей книга-автор
CREATE TABLE IF NOT EXISTS book_author (
    id SERIAL PRIMARY KEY,
    book_id INTEGER NOT NULL REFERENCES books(id) ON DELETE CASCADE,
    author_id INTEGER NOT NULL REFERENCES authors(id) ON DELETE CASCADE
);

-- Создание таблицы связей книга-жанр
CREATE TABLE IF NOT EXISTS book_genre (
    id SERIAL PRIMARY KEY,
    book_id INTEGER NOT NULL REFERENCES books(id) ON DELETE CASCADE,
    genre_id INTEGER NOT NULL REFERENCES genres(id) ON DELETE CASCADE
);

SELECT * from books;