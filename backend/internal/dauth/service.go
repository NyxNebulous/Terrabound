package dauth

import (
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"time"

	"golang.org/x/oauth2"
)

type DAuthService struct {
	config *oauth2.Config
	client *http.Client
}

func NewDAuthService(config *oauth2.Config) *DAuthService {
	return &DAuthService{
		config: config,
		client: &http.Client{},
	}
}

func (s *DAuthService) GetAuthorizationURL(state, nonce string) string {
	return s.config.AuthCodeURL(state,
		oauth2.SetAuthURLParam("grant_type", "authorization_code"),
		oauth2.SetAuthURLParam("response_type", "code"),
		oauth2.SetAuthURLParam("nonce", nonce),
	)
}

func (s *DAuthService) ExchangeCode(ctx context.Context, code string) (*oauth2.Token, error) {
	token, err := s.config.Exchange(ctx, code,
		oauth2.SetAuthURLParam("grant_type", "authorization_code"),
	)
	if err != nil {
		return nil, fmt.Errorf("failed to exchange code: %w", err)
	}
	return token, nil
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