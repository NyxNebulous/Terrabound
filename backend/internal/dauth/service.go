package dauth

import (
	"context"
	"database/sql"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"strings"
	"time"
)

type DAuthService struct {
	config *DAuthConfig
	client *http.Client
}

func NewDAuthService(config *DAuthConfig) *DAuthService {
	return &DAuthService{
		config: config,
		client: &http.Client{},
	}
}

func (s *DAuthService) GetAuthorizationURL(state, nonce string) string {
	q := url.Values{}
	q.Set("client_id", s.config.ClientID)
	q.Set("redirect_uri", s.config.RedirectURI)
	q.Set("response_type", "code")
	q.Set("scope", "openid email profile user")
	q.Set("state", state)
	q.Set("nonce", nonce)
	return DAuthAuthURL + "?" + q.Encode()
}

func (s *DAuthService) ExchangeCode(ctx context.Context, code string) (*DAuthToken, error) {
	form := url.Values{}
	form.Set("grant_type", "authorization_code")
	form.Set("code", code)
	form.Set("redirect_uri", s.config.RedirectURI)
	form.Set("client_id", s.config.ClientID)
	form.Set("client_secret", s.config.ClientSecret)

	req, err := http.NewRequestWithContext(ctx, http.MethodPost, DAuthTokenURL, strings.NewReader(form.Encode()))
	if err != nil {
		return nil, fmt.Errorf("failed to create token request: %w", err)
	}
	req.Header.Set("Content-Type", "application/x-www-form-urlencoded")

	resp, err := s.client.Do(req)
	if err != nil {
		return nil, fmt.Errorf("failed to exchange code: %w", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		body, _ := io.ReadAll(resp.Body)
		return nil, fmt.Errorf("token request failed with status %d: %s", resp.StatusCode, string(body))
	}

	var tokenResp TokenResponse
	if err := json.NewDecoder(resp.Body).Decode(&tokenResp); err != nil {
		return nil, fmt.Errorf("failed to decode token response: %w", err)
	}

	expiresIn := tokenResp.ExpiresIn
	if expiresIn == 0 {
		expiresIn = 3600
	}

	return &DAuthToken{
		AccessToken:  tokenResp.AccessToken,
		RefreshToken: sqlNullString(tokenResp.RefreshToken),
		IDToken:      sqlNullString(tokenResp.IDToken),
		ExpiryTime:   time.Now().Add(time.Duration(expiresIn) * time.Second),
	}, nil
}

func (s *DAuthService) GetUserInfo(ctx context.Context, accessToken string) (*DAuthUser, error) {
	req, err := http.NewRequestWithContext(ctx, "POST", DAuthUserInfoURL, nil)
	if err != nil {
		return nil, fmt.Errorf("failed to create user info request: %w", err)
	}

	req.Header.Set("Authorization", "Bearer "+accessToken)
	req.Header.Set("Content-Type", "application/json")

	resp, err := s.client.Do(req)
	if err != nil {
		return nil, fmt.Errorf("failed to fetch user info: %w", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		body, _ := io.ReadAll(resp.Body)
		return nil, fmt.Errorf("user info request failed with status %d: %s", resp.StatusCode, string(body))
	}

	var user DAuthUser
	if err := json.NewDecoder(resp.Body).Decode(&user); err != nil {
		return nil, fmt.Errorf("failed to decode user info: %w", err)
	}

	return &user, nil
}

func (s *DAuthService) ValidateToken(token *DAuthToken) bool {
	return token != nil && token.ExpiryTime.After(time.Now())
}

func sqlNullString(val string) sql.NullString {
	if val == "" {
		return sql.NullString{}
	}
	return sql.NullString{String: val, Valid: true}
}
