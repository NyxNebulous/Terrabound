package dauth

import (
	"context"
	"database/sql"
	"fmt"
	"time"
)

type DAuthRepository interface {
	SaveToken(ctx context.Context, tx *sql.Tx, token *DAuthToken) error
	GetToken(ctx context.Context, userID string) (*DAuthToken, error)
	DeleteToken(ctx context.Context, tx *sql.Tx, userID string) error
}

type SQLDAuthRepository struct {
	db *sql.DB
}

func NewSQLDAuthRepository(db *sql.DB) *SQLDAuthRepository {
	return &SQLDAuthRepository{db: db}
}

func (r *SQLDAuthRepository) SaveToken(ctx context.Context, tx *sql.Tx, token *DAuthToken) error {
	const query = `
        INSERT INTO user_dauth_tokens (user_id, access_token, refresh_token, id_token, expiry_time, created_at)
        VALUES ($1, $2, $3, $4, $5, NOW())
        ON CONFLICT (user_id) DO UPDATE SET
            access_token  = EXCLUDED.access_token,
            refresh_token = EXCLUDED.refresh_token,
            id_token      = EXCLUDED.id_token,
            expiry_time   = EXCLUDED.expiry_time,
            updated_at    = NOW();
    `

	exec := r.db.ExecContext
	if tx != nil {
		exec = tx.ExecContext
	}

	_, err := exec(ctx, query,
		token.UserID,
		token.AccessToken,
		token.RefreshToken,
		token.IDToken,
		token.ExpiryTime,
	)
	if err != nil {
		return fmt.Errorf("save dauth token for user %s: %w", token.UserID, err)
	}
	return nil
}

func (r *SQLDAuthRepository) GetToken(ctx context.Context, userID string) (*DAuthToken, error) {
	const query = `
		SELECT user_id, access_token, refresh_token, id_token, expiry_time
		FROM user_dauth_tokens
		WHERE user_id = $1
		LIMIT 1
	`

	row := r.db.QueryRowContext(ctx, query, userID)
	var t DAuthToken
	var refresh sql.NullString
	var idToken sql.NullString
	var expiry time.Time
	
	if err := row.Scan(&t.UserID, &t.AccessToken, &refresh, &idToken, &expiry); err != nil {
		if err == sql.ErrNoRows {
			return nil, sql.ErrNoRows
		}
		return nil, fmt.Errorf("get dauth token for user %s: %w", userID, err)
	}
	
	t.RefreshToken = refresh
	t.IDToken = idToken
	t.ExpiryTime = expiry
	return &t, nil
}

func (r *SQLDAuthRepository) DeleteToken(ctx context.Context, tx *sql.Tx, userID string) error {
	const query = `DELETE FROM user_dauth_tokens WHERE user_id = $1`

	exec := r.db.ExecContext
	if tx != nil {
		exec = tx.ExecContext
	}

	_, err := exec(ctx, query, userID)
	if err != nil {
		return fmt.Errorf("delete dauth token for user %s: %w", userID, err)
	}
	return nil
}