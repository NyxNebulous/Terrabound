package dauth

import (
	"fmt"
	"os"
	"strings"

	"golang.org/x/oauth2"
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

func NewDAuthConfig() *oauth2.Config {
	clientID := mustEnv("DAUTH_CLIENT_ID")
	clientSecret := mustEnv("DAUTH_CLIENT_SECRET")
	redirectURI := mustEnv("DAUTH_REDIRECT_URI")
	
	scopes := []string{"openid", "email", "profile", "user"}

	return &oauth2.Config{
		ClientID:     clientID,
		ClientSecret: clientSecret,
		RedirectURL:  redirectURI,
		Scopes:       scopes,
		Endpoint: oauth2.Endpoint{
			AuthURL:  DAuthAuthURL,
			TokenURL: DAuthTokenURL,
		},
	}
}