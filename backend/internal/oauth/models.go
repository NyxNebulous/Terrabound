package oauth

import (
	"database/sql"
	"time"
)

type OAuthToken struct {
	UserID       string
	AccessToken  string
	RefreshToken sql.NullString
	ExpiryTime   time.Time
}
