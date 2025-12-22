package nakama

import (
	"context"
	"crypto/rand"
	"database/sql"
	"encoding/base64"
	"encoding/json"
	"fmt"
	"net/http"
	"strings"
	"time"

	"github.com/delta/terrabound/backend/internal/dauth"
	"github.com/delta/terrabound/backend/internal/oauth"
	"github.com/heroiclabs/nakama-common/runtime"
)

type authInitRequest struct {
	Provider string `json:"provider"` // "dauth" | "google"
}

type authInitResponse struct {
	Success bool   `json:"success"`
	State   string `json:"state"`
	URL     string `json:"url"`
	Message string `json:"message,omitempty"`
}

type authCheckRequest struct {
	State string `json:"state"`
}

type authCheckResponse struct {
	Success  bool   `json:"success"`
	Ready    bool   `json:"ready"`
	CustomID string `json:"customId,omitempty"`
	Username string `json:"username,omitempty"`
	Email    string `json:"email,omitempty"`
	Message  string `json:"message,omitempty"`
}

// HTTPAuthInitHandler starts the provider auth flow and returns a browser URL.
// This is intentionally an HTTP endpoint (not an RPC) so the client does NOT need a Nakama session or http_key.
func HTTPAuthInitHandler(ctx context.Context, logger runtime.Logger, nk runtime.NakamaModule) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost {
			http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
			return
		}

		var req authInitRequest
		if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
			http.Error(w, "invalid json", http.StatusBadRequest)
			return
		}
		req.Provider = strings.TrimSpace(strings.ToLower(req.Provider))
		if req.Provider == "" {
			http.Error(w, "provider is required", http.StatusBadRequest)
			return
		}

		state := randomState()
		nonce := randomState()

		var authURL string
		switch req.Provider {
		case "dauth":
			svc := dauth.NewDAuthService(dauth.NewDAuthConfig())
			authURL = svc.GetAuthorizationURL(state, nonce)
		case "google":
			svc := oauth.NewGoogleOAuthService(oauth.NewGoogleOAuthConfig())
			authURL = svc.GetAuthorizationURL(state)
		default:
			http.Error(w, "unknown provider", http.StatusBadRequest)
			return
		}

		stateData := map[string]interface{}{
			"provider":   req.Provider,
			"nonce":      nonce,
			"created_at": time.Now().Unix(),
			"expires_at": time.Now().Add(10 * time.Minute).Unix(),
		}
		stateJSON, _ := json.Marshal(stateData)

		if _, err := nk.StorageWrite(ctx, []*runtime.StorageWrite{{
			Collection:      "auth_states",
			Key:             state,
			UserID:          "",
			Value:           string(stateJSON),
			PermissionRead:  0,
			PermissionWrite: 0,
		}}); err != nil {
			logger.Error("auth init: storage write failed: %v", err)
			http.Error(w, "failed to init auth", http.StatusInternalServerError)
			return
		}

		w.Header().Set("Content-Type", "application/json")
		_ = json.NewEncoder(w).Encode(authInitResponse{Success: true, State: state, URL: authURL})
	}
}

// HTTPAuthCheckHandler polls for completion of the provider flow.
func HTTPAuthCheckHandler(ctx context.Context, logger runtime.Logger, nk runtime.NakamaModule) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost {
			http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
			return
		}

		var req authCheckRequest
		if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
			http.Error(w, "invalid json", http.StatusBadRequest)
			return
		}
		req.State = strings.TrimSpace(req.State)
		if req.State == "" {
			http.Error(w, "state is required", http.StatusBadRequest)
			return
		}

		objs, err := nk.StorageRead(ctx, []*runtime.StorageRead{{Collection: "auth_sessions", Key: req.State, UserID: ""}})
		if err != nil || len(objs) == 0 {
			w.Header().Set("Content-Type", "application/json")
			_ = json.NewEncoder(w).Encode(authCheckResponse{Success: true, Ready: false})
			return
		}

		var session map[string]interface{}
		if err := json.Unmarshal([]byte(objs[0].Value), &session); err != nil {
			http.Error(w, "invalid session data", http.StatusInternalServerError)
			return
		}

		// one-time use
		_ = nk.StorageDelete(ctx, []*runtime.StorageDelete{{Collection: "auth_sessions", Key: req.State, UserID: ""}})

		resp := authCheckResponse{Success: true, Ready: true}
		if v, ok := session["custom_id"].(string); ok {
			resp.CustomID = v
		}
		if v, ok := session["username"].(string); ok {
			resp.Username = v
		}
		if v, ok := session["email"].(string); ok {
			resp.Email = v
		}

		w.Header().Set("Content-Type", "application/json")
		_ = json.NewEncoder(w).Encode(resp)
	}
}

// CreateAuthCallbackHandler handles redirects from both providers.
func CreateAuthCallbackHandler(ctx context.Context, logger runtime.Logger, nk runtime.NakamaModule) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		code := r.URL.Query().Get("code")
		state := r.URL.Query().Get("state")

		if code == "" || state == "" {
			http.Error(w, "Missing code or state", http.StatusBadRequest)
			return
		}

		objs, err := nk.StorageRead(ctx, []*runtime.StorageRead{{Collection: "auth_states", Key: state, UserID: ""}})
		if err != nil || len(objs) == 0 {
			http.Error(w, "Invalid or expired state", http.StatusBadRequest)
			return
		}

		var stateData map[string]interface{}
		if err := json.Unmarshal([]byte(objs[0].Value), &stateData); err != nil {
			http.Error(w, "Invalid state data", http.StatusInternalServerError)
			return
		}

		expiresAt, _ := stateData["expires_at"].(float64)
		if time.Now().Unix() > int64(expiresAt) {
			http.Error(w, "State expired", http.StatusBadRequest)
			return
		}

		provider, _ := stateData["provider"].(string)
		provider = strings.ToLower(strings.TrimSpace(provider))

		var customID, username, email string
		switch provider {
		case "dauth":
			customID, username, email, err = handleDAuthCallback(ctx, code)
		case "google":
			customID, username, email, err = handleGoogleCallback(ctx, code)
		default:
			http.Error(w, "Unknown provider", http.StatusBadRequest)
			return
		}

		if err != nil {
			logger.Error("auth callback failed (%s): %v", provider, err)
			http.Error(w, "Authentication failed", http.StatusInternalServerError)
			return
		}

		sessionData := map[string]interface{}{
			"custom_id": customID,
			"username":  username,
			"email":     email,
			"provider":  provider,
		}
		sessionJSON, _ := json.Marshal(sessionData)

		if _, err := nk.StorageWrite(ctx, []*runtime.StorageWrite{{
			Collection:      "auth_sessions",
			Key:             state,
			UserID:          "",
			Value:           string(sessionJSON),
			PermissionRead:  0,
			PermissionWrite: 0,
		}}); err != nil {
			logger.Error("failed to store auth session: %v", err)
			http.Error(w, "Failed to complete authentication", http.StatusInternalServerError)
			return
		}

		_ = nk.StorageDelete(ctx, []*runtime.StorageDelete{{Collection: "auth_states", Key: state, UserID: ""}})

		w.Header().Set("Content-Type", "text/html")
		w.WriteHeader(http.StatusOK)
		w.Write([]byte(successHTML))
	}
}

func handleDAuthCallback(ctx context.Context, code string) (customID, username, email string, err error) {
	svc := dauth.NewDAuthService(dauth.NewDAuthConfig())
	tok, err := svc.ExchangeCode(ctx, code)
	if err != nil {
		return "", "", "", fmt.Errorf("dauth token exchange failed: %w", err)
	}

	user, err := svc.GetUserInfo(ctx, tok.AccessToken)
	if err != nil {
		return "", "", "", fmt.Errorf("dauth userinfo failed: %w", err)
	}

	customID = fmt.Sprintf("dauth:%d", user.ID)
	username = user.Name
	if username == "" {
		username = user.Email
	}
	email = user.Email
	return
}

func handleGoogleCallback(ctx context.Context, code string) (customID, username, email string, err error) {
	svc := oauth.NewGoogleOAuthService(oauth.NewGoogleOAuthConfig())
	tok, err := svc.ExchangeCode(ctx, code)
	if err != nil {
		return "", "", "", fmt.Errorf("google token exchange failed: %w", err)
	}

	user, err := svc.GetUserInfo(ctx, tok.AccessToken)
	if err != nil {
		return "", "", "", fmt.Errorf("google userinfo failed: %w", err)
	}

	customID = fmt.Sprintf("google:%s", user.ID)
	username = user.Name
	if username == "" {
		username = user.Email
	}
	email = user.Email
	return
}

func randomState() string {
	b := make([]byte, 32)
	_, _ = rand.Read(b)
	return base64.RawURLEncoding.EncodeToString(b)
}

const successHTML = `<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8" />
  <title>Auth OK</title>
</head>
<body style="font-family: Arial, sans-serif; text-align:center; padding: 40px;">
  <h2>Authentication successful</h2>
  <p>You can close this window and return to the game.</p>
</body>
</html>`

// Dummy signature keepers (RPC signature is required by initializer.RegisterRpc).
// We expose init/check as HTTP endpoints, but keeping RPC wrappers is useful for debugging.
func AuthInitRPC(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	// Optional: You can wire this later if you want RPC instead of HTTP.
	return "", runtime.NewError("auth_init RPC disabled; use /auth/init", 3)
}

func AuthCheckRPC(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	return "", runtime.NewError("auth_check RPC disabled; use /auth/check", 3)
}
