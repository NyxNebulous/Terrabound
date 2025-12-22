package dauth

import (
	"database/sql"
	"time"
)

type DAuthToken struct {
	UserID       string
	AccessToken  string
	RefreshToken sql.NullString
	IDToken      sql.NullString
	ExpiryTime   time.Time
}

type DAuthUser struct {
	ID      int64 `json:"id"`
	Name    string `json:"name"`
	Email   string `json:"email"`
	RollNo  string `json:"rollNo,omitempty"`
	Gender  string `json:"gender,omitempty"`
	Branch  string `json:"branch,omitempty"`
	Contact string `json:"contact,omitempty"`
}

type TokenResponse struct {
	AccessToken  string `json:"access_token"`
	TokenType    string `json:"token_type"`
	ExpiresIn    int64  `json:"expires_in,omitempty"`
	RefreshToken string `json:"refresh_token,omitempty"`
	IDToken      string `json:"id_token,omitempty"`
	State        string `json:"state,omitempty"`
}