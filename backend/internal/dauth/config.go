package dauth

import (
	"fmt"
	"os"
	"strings"
)

const (
	DAuthStateCollection = "dauth_state"
	DAuthStateKey        = "state"

	DAuthBaseURL     = "https://auth.delta.nitt.edu"
	DAuthAuthURL     = "https://auth.delta.nitt.edu/authorize"
	DAuthTokenURL    = "https://auth.delta.nitt.edu/api/oauth/token"
	DAuthUserInfoURL = "https://auth.delta.nitt.edu/api/resources/user"
	DAuthJWKSURL     = "https://auth.delta.nitt.edu/api/oauth/oidc/key"
)

func mustEnv(key string) string {
	value := strings.TrimSpace(os.Getenv(key))
	if value == "" {
		panic(fmt.Sprintf("environment variable %s is required but not set", key))
	}
	return value
}

type DAuthConfig struct {
	ClientID     string
	ClientSecret string
	RedirectURI  string
}

func NewDAuthConfig() *DAuthConfig {
	return &DAuthConfig{
		ClientID:     mustEnv("DAUTH_CLIENT_ID"),
		ClientSecret: mustEnv("DAUTH_CLIENT_SECRET"),
		RedirectURI:  mustEnv("DAUTH_REDIRECT_URI"),
	}
}
