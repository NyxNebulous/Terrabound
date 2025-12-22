package oauth

import (
	"fmt"
	"os"
	"strings"
)

const (
	GoogleUserInfoURL = "https://www.googleapis.com/oauth2/v2/userinfo"
)

func mustEnv(key string) string {
	value := strings.TrimSpace(os.Getenv(key))
	if value == "" {
		panic(fmt.Sprintf("environment variable %s is required but not set", key))
	}
	return value
}

type GoogleConfig struct {
	ClientID     string
	ClientSecret string
	RedirectURI  string
}

func NewGoogleOAuthConfig() *GoogleConfig {
	return &GoogleConfig{
		ClientID:     mustEnv("GOOGLE_CLIENT_ID"),
		ClientSecret: mustEnv("GOOGLE_CLIENT_SECRET"),
		RedirectURI:  mustEnv("GOOGLE_REDIRECT_URI"),
	}
}
