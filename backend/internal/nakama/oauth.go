package nakama

import (
	"context"
	"database/sql"
	"encoding/json"
	"time"

	"github.com/google/uuid"
	"github.com/heroiclabs/nakama-common/runtime"
	"golang.org/x/oauth2"

	"github.com/delta/terrabound/backend/internal/constants"
	"github.com/delta/terrabound/backend/internal/oauth"
)

// ---- Types ----

type RegisterURLResponse struct {
	AuthURL string `json:"auth_url"`
}

type ExchangeCodeRequest struct {
	Code  string `json:"code"`
	State string `json:"state"`
}

type TokenResponse struct {
	AccessToken  string `json:"access_token"`
	TokenType    string `json:"token_type"`
	ExpiresIn    int64  `json:"expires_in"`
	RefreshToken string `json:"refresh_token"`
}

type GetTokenResponse struct {
	Found           bool  `json:"found"`
	ExpiresIn       string `json:"expires_in"`
	HasRefreshToken bool  `json:"has_refresh_token"`
}

// ---- RPC: Generate Authorization URL ----

func (h *RPCHandlers) AuthorizationURLRPC(
	ctx context.Context,
	logger runtime.Logger,
	db *sql.DB,
	nk runtime.NakamaModule,
	payload string,
) (string, error) {
	userID, ok := ctx.Value(runtime.RUNTIME_CTX_USER_ID).(string)
	if !ok || userID == "" {
		return "", constants.ErrUserMissing
	}

	// CSRF protection
	state := uuid.New().String()

	stateStorageWrite := runtime.StorageWrite{
		Collection:      oauth.OauthStateCollection,
		Key:             oauth.OauthStateKey,
		UserID:          userID,
		Value:           state,
		PermissionRead:  0, // No public read
		PermissionWrite: 0, // Only server can update
	}

	if _, err := nk.StorageWrite(ctx, []*runtime.StorageWrite{&stateStorageWrite}); err != nil {
		logger.Error("failed to save oauth state for user %s: %v", userID, err)
		return "", constants.ErrStorageWriteFailed
	}

	authURL := h.oauthConf.AuthCodeURL(state, oauth2.AccessTypeOffline)

	resp := RegisterURLResponse{AuthURL: authURL}
	bytes, err := json.Marshal(resp)
	if err != nil {
		logger.Error("failed to marshal authorization URL response: %v", err)
		return "", constants.ErrMarshalResponse
	}

	return string(bytes), nil
}

// ---- RPC: Exchange Code for Tokens ----

func (h *RPCHandlers) ExchangeCodeForTokensRPC(
	ctx context.Context,
	logger runtime.Logger,
	db *sql.DB,
	nk runtime.NakamaModule,
	payload string,
) (string, error) {
	var req ExchangeCodeRequest
	if err := json.Unmarshal([]byte(payload), &req); err != nil {
		logger.Error("failed to unmarshal exchange code request: %v", err)
		return "", constants.ErrUnmarshalRequest
	}

	userID, ok := ctx.Value(runtime.RUNTIME_CTX_USER_ID).(string)
	if !ok || userID == "" {
		return "", constants.ErrUserMissing
	}

	readOp := runtime.StorageRead{
		Collection: oauth.OauthStateCollection,
		Key:        oauth.OauthStateKey,
		UserID:     userID,
	}
	records, err := nk.StorageRead(ctx, []*runtime.StorageRead{&readOp})
	if err != nil || len(records) == 0 {
		logger.Error("failed to read oauth state for user %s: %v", userID, err)
		return "", constants.ErrStorageReadFailed
	}

	storedState := records[0].Value
	if storedState == "" {
		logger.Warn("empty OAuth state stored for user %s", userID)
		return "", constants.ErrStateMismatch
	}

	if storedState != req.State {
		logger.Warn("OAuth state mismatch for user %s. Received: %s, Stored: %s", userID, req.State, storedState)
		return "", constants.ErrStateMismatch
	}

	tokenEx, err := h.oauthConf.Exchange(ctx, req.Code)
	if err != nil {
		logger.Error("OAuth token exchange failed for user %s: %v", userID, err)
		return "", constants.ErrTokenExchangeFailed
	}

	expiryTime := tokenEx.Expiry
	if expiryTime.IsZero() {
		expiryTime = time.Now().Add(24 * time.Hour)
	}

	oauthToken := &oauth.OAuthToken{
		UserID:      userID,
		AccessToken: tokenEx.AccessToken,
		RefreshToken: sql.NullString{
			String: tokenEx.RefreshToken,
			Valid:  tokenEx.RefreshToken != "",
		},
		ExpiryTime: expiryTime,
	}
	tx, _ := db.Begin()
	if err := h.oauthRepo.SaveToken(ctx, tx, oauthToken); err != nil {
		logger.Error("failed to save oauth token for user %s: %v", userID, err)
		return "", constants.ErrDBOperationFailed
	}

	deleteOp := runtime.StorageDelete{
		Collection: oauth.OauthStateCollection,
		Key:        oauth.OauthStateKey,
		UserID:     userID,
	}
	if err := nk.StorageDelete(ctx, []*runtime.StorageDelete{&deleteOp}); err != nil {
		logger.Warn("failed to clean up oauth state for user %s: %v", userID, err)
	}

	resp := map[string]any{
		"success": true,
	}
	respBytes, err := json.Marshal(resp)
	if err != nil {
		logger.Error("failed to marshal token response for user %s: %v", userID, err)
		return "", constants.ErrMarshalResponse
	}

	return string(respBytes), nil
}

func (h *RPCHandlers) GetOAuthTokenRPC(
	ctx context.Context,
	logger runtime.Logger,
	db *sql.DB,
	nk runtime.NakamaModule,
	payload string,
) (string, error) {
	userID, ok := ctx.Value(runtime.RUNTIME_CTX_USER_ID).(string)
	if !ok || userID == "" {
		return "", constants.ErrUserMissing
	}

	token, err := h.oauthRepo.GetToken(ctx, userID)
	if err != nil {
		if err == sql.ErrNoRows {
			resp := map[string]any{"found": false}
			b, _ := json.Marshal(resp)
			return string(b), nil
		}
		logger.Error("failed to read oauth token for user %s: %v", userID, err)
		return "", constants.ErrDBOperationFailed
	}

	resp := GetTokenResponse{
		Found:       true,
		ExpiresIn:      token.ExpiryTime.UTC().Format(time.RFC3339Nano),
		HasRefreshToken: token.RefreshToken.Valid,
	}
	b, err := json.Marshal(resp)
	if err != nil {
		logger.Error("failed to marshal token info for user %s: %v", userID, err)
		return "", constants.ErrMarshalResponse
	}
	return string(b), nil
}
