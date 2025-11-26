CREATE TABLE
    IF NOT EXISTS user_oauth_tokens (
        user_id VARCHAR(128) PRIMARY KEY,
        access_token TEXT NOT NULL,
        refresh_token TEXT,
        expiry_time TIMESTAMPTZ NOT NULL,
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW ()
    );

