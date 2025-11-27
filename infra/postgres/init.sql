CREATE TABLE IF NOT EXISTS user_dauth_tokens (
    user_id VARCHAR(255) PRIMARY KEY,
    access_token TEXT NOT NULL,
    refresh_token TEXT,
    id_token TEXT,
    expiry_time TIMESTAMP NOT NULL,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_user_dauth_tokens_user_id ON user_dauth_tokens(user_id);
CREATE INDEX IF NOT EXISTS idx_user_dauth_tokens_expiry ON user_dauth_tokens(expiry_time);