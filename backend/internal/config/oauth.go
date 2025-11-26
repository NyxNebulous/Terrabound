package config

import (
	"fmt"
	"os"
	"strings"

	"golang.org/x/oauth2"
)

const (
	OauthStateCollection = "oauth_state"
	OauthStateKey        = "state"
)
func mustEnv(key string) string {
	value := strings.TrimSpace(os.Getenv(key))
	if value == "" {
		panic(fmt.Sprintf("environment variable %s is required but not set", key))
	}
	return value
}

func NewOAuthConfig() *oauth2.Config {
	clientID := mustEnv("OAUTH_CLIENT_ID")
	clientSecret := mustEnv("OAUTH_CLIENT_SECRET")
	redirectURI := mustEnv("OAUTH_REDIRECT_URI")
	oauthScope := mustEnv("OAUTH_SCOPE")

	authURL := mustEnv("OAUTH_AUTH_URL")
	tokenURL := mustEnv("OAUTH_TOKEN_URL")
	endpoint := oauth2.Endpoint{
		AuthURL:  authURL,
		TokenURL: tokenURL,
	}

	return &oauth2.Config{
		ClientID:     clientID,
		ClientSecret: clientSecret,
		RedirectURL:  redirectURI,
		Scopes:       strings.Split(oauthScope, " "),
		Endpoint:     endpoint,
	}
}
