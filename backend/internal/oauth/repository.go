package oauth

import (
	"context"
	"database/sql"
	"fmt"
	"time"
)

type OAuthRepository interface {
	SaveToken(ctx context.Context, tx *sql.Tx, token *OAuthToken) error
	GetToken(ctx context.Context, userID string) (*OAuthToken, error)
}

type SQLOAuthRepository struct {
	db *sql.DB
}

func NewSQLOAuthRepository(db *sql.DB) *SQLOAuthRepository {
	return &SQLOAuthRepository{db: db}
}

func (r *SQLOAuthRepository) SaveToken(ctx context.Context, tx *sql.Tx, token *OAuthToken) error {
	const query = `
        INSERT INTO user_oauth_tokens (user_id, access_token, refresh_token, expiry_time, created_at)
        VALUES ($1, $2, $3, $4, NOW())
        ON CONFLICT (user_id) DO UPDATE SET
            access_token  = EXCLUDED.access_token,
            refresh_token = EXCLUDED.refresh_token,
            expiry_time   = EXCLUDED.expiry_time,
            created_at    = NOW();
    `

	exec := r.db.ExecContext
	if tx != nil {
		exec = tx.ExecContext
	}

	_, err := exec(ctx, query,
		token.UserID,
		token.AccessToken,
		token.RefreshToken,
		token.ExpiryTime,
	)
	if err != nil {
		return fmt.Errorf("save oauth token for user %s: %w", token.UserID, err)
	}
	return nil
}

func (r *SQLOAuthRepository) GetToken(ctx context.Context, userID string) (*OAuthToken, error) {
	const query = `
		SELECT user_id, access_token, refresh_token, expiry_time
		FROM user_oauth_tokens
		WHERE user_id = $1
		LIMIT 1
	`

	row := r.db.QueryRowContext(ctx, query, userID)
	var t OAuthToken
	var refresh sql.NullString
	var expiry time.Time
	if err := row.Scan(&t.UserID, &t.AccessToken, &refresh, &expiry); err != nil {
		if err == sql.ErrNoRows {
			return nil, sql.ErrNoRows
		}
		return nil, fmt.Errorf("get oauth token for user %s: %w", userID, err)
	}
	t.RefreshToken = refresh
	t.ExpiryTime = expiry
	return &t, nil
}
