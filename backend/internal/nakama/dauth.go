package nakama

import (
	"context"
	"database/sql"
	"encoding/json"
	"time"

	"github.com/google/uuid"
	"github.com/heroiclabs/nakama-common/runtime"

	"github.com/delta/terrabound/backend/internal/constants"
	"github.com/delta/terrabound/backend/internal/dauth"
)

// ---- Types ----

type DAuthURLResponse struct {
	AuthURL string `json:"auth_url"`
}

type DAuthExchangeCodeRequest struct {
	Code  string `json:"code"`
	State string `json:"state"`
}

type DAuthTokenInfoResponse struct {
	Found           bool   `json:"found"`
	ExpiresIn       string `json:"expires_in"`
	HasRefreshToken bool   `json:"has_refresh_token"`
	HasIDToken      bool   `json:"has_id_token"`
}

type DAuthUserResponse struct {
	Success bool             `json:"success"`
	User    *dauth.DAuthUser `json:"user,omitempty"`
}

// ---- RPC: Generate DAuth Authorization URL ----

func (h *RPCHandlers) DAuthAuthorizationURLRPC(
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

	// CSRF protection - generate state
	state := uuid.New().String()
	// Generate nonce for DAuth (required by DAuth)
	nonce := uuid.New().String()

	// Store both state and nonce
	stateData := map[string]string{
		"state": state,
		"nonce": nonce,
	}
	stateJSON, err := json.Marshal(stateData)
	if err != nil {
		logger.Error("failed to marshal dauth state data: %v", err)
		return "", constants.ErrMarshalResponse
	}

	stateStorageWrite := runtime.StorageWrite{
		Collection:      dauth.DAuthStateCollection,
		Key:             dauth.DAuthStateKey,
		UserID:          userID,
		Value:           string(stateJSON),
		PermissionRead:  0, // No public read
		PermissionWrite: 0, // Only server can update
	}

	if _, err := nk.StorageWrite(ctx, []*runtime.StorageWrite{&stateStorageWrite}); err != nil {
		logger.Error("failed to save dauth state for user %s: %v", userID, err)
		return "", constants.ErrStorageWriteFailed
	}


	authURL := h.dauthService.GetAuthorizationURL(state, nonce)

	resp := DAuthURLResponse{AuthURL: authURL}
	bytes, err := json.Marshal(resp)
	if err != nil {
		logger.Error("failed to marshal dauth authorization URL response: %v", err)
		return "", constants.ErrMarshalResponse
	}

	return string(bytes), nil
}

// ---- RPC: Exchange Code for DAuth Tokens ----

func (h *RPCHandlers) DAuthExchangeCodeForTokensRPC(
	ctx context.Context,
	logger runtime.Logger,
	db *sql.DB,
	nk runtime.NakamaModule,
	payload string,
) (string, error) {
	var req DAuthExchangeCodeRequest
	if err := json.Unmarshal([]byte(payload), &req); err != nil {
		logger.Error("failed to unmarshal dauth exchange code request: %v", err)
		return "", constants.ErrUnmarshalRequest
	}

	userID, ok := ctx.Value(runtime.RUNTIME_CTX_USER_ID).(string)
	if !ok || userID == "" {
		return "", constants.ErrUserMissing
	}

	// Read stored state and nonce
	readOp := runtime.StorageRead{
		Collection: dauth.DAuthStateCollection,
		Key:        dauth.DAuthStateKey,
		UserID:     userID,
	}
	records, err := nk.StorageRead(ctx, []*runtime.StorageRead{&readOp})
	if err != nil || len(records) == 0 {
		logger.Error("failed to read dauth state for user %s: %v", userID, err)
		return "", constants.ErrStorageReadFailed
	}

	var stateData map[string]string
	if err := json.Unmarshal([]byte(records[0].Value), &stateData); err != nil {
		logger.Error("failed to unmarshal dauth state data for user %s: %v", userID, err)
		return "", constants.ErrStorageReadFailed
	}

	storedState := stateData["state"]
	if storedState == "" {
		logger.Warn("empty DAuth state stored for user %s", userID)
		return "", constants.ErrStateMismatch
	}

	if storedState != req.State {
		logger.Warn("DAuth state mismatch for user %s. Received: %s, Stored: %s", userID, req.State, storedState)
		return "", constants.ErrStateMismatch
	}

	// Exchange code for tokens
	tokenEx, err := h.dauthService.ExchangeCode(ctx, req.Code)
	if err != nil {
		logger.Error("DAuth token exchange failed for user %s: %v", userID, err)
		return "", constants.ErrTokenExchangeFailed
	}

	expiryTime := tokenEx.Expiry
	if expiryTime.IsZero() {
		expiryTime = time.Now().Add(24 * time.Hour)
	}

	// Extract id_token from extra fields if present
	idToken := ""
	if idTokenRaw := tokenEx.Extra("id_token"); idTokenRaw != nil {
		if idTokenStr, ok := idTokenRaw.(string); ok {
			idToken = idTokenStr
		}
	}

	dauthToken := &dauth.DAuthToken{
		UserID:      userID,
		AccessToken: tokenEx.AccessToken,
		RefreshToken: sql.NullString{
			String: tokenEx.RefreshToken,
			Valid:  tokenEx.RefreshToken != "",
		},
		IDToken: sql.NullString{
			String: idToken,
			Valid:  idToken != "",
		},
		ExpiryTime: expiryTime,
	}

	tx, err := db.Begin()
	if err != nil {
		logger.Error("failed to begin transaction for user %s: %v", userID, err)
		return "", constants.ErrDBOperationFailed
	}
	defer tx.Rollback()

	if err := h.dauthRepo.SaveToken(ctx, tx, dauthToken); err != nil {
		logger.Error("failed to save dauth token for user %s: %v", userID, err)
		return "", constants.ErrDBOperationFailed
	}

	if err := tx.Commit(); err != nil {
		logger.Error("failed to commit transaction for user %s: %v", userID, err)
		return "", constants.ErrDBOperationFailed
	}

	// Clean up state storage
	deleteOp := runtime.StorageDelete{
		Collection: dauth.DAuthStateCollection,
		Key:        dauth.DAuthStateKey,
		UserID:     userID,
	}
	if err := nk.StorageDelete(ctx, []*runtime.StorageDelete{&deleteOp}); err != nil {
		logger.Warn("failed to clean up dauth state for user %s: %v", userID, err)
	}

	// Fetch user info from DAuth
	userInfo, err := h.dauthService.GetUserInfo(ctx, tokenEx.AccessToken)
	if err != nil {
		logger.Warn("failed to fetch user info from DAuth for user %s: %v", userID, err)
		// Still return success as token was saved
	}

	resp := map[string]any{
		"success": true,
		"user":    userInfo,
	}
	respBytes, err := json.Marshal(resp)
	if err != nil {
		logger.Error("failed to marshal dauth token response for user %s: %v", userID, err)
		return "", constants.ErrMarshalResponse
	}

	return string(respBytes), nil
}

// ---- RPC: Get DAuth Token Info ----

func (h *RPCHandlers) GetDAuthTokenRPC(
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

	token, err := h.dauthRepo.GetToken(ctx, userID)
	if err != nil {
		if err == sql.ErrNoRows {
			resp := map[string]any{"found": false}
			b, _ := json.Marshal(resp)
			return string(b), nil
		}
		logger.Error("failed to read dauth token for user %s: %v", userID, err)
		return "", constants.ErrDBOperationFailed
	}

	resp := DAuthTokenInfoResponse{
		Found:           true,
		ExpiresIn:       token.ExpiryTime.UTC().Format(time.RFC3339),
		HasRefreshToken: token.RefreshToken.Valid,
		HasIDToken:      token.IDToken.Valid,
	}
	b, err := json.Marshal(resp)
	if err != nil {
		logger.Error("failed to marshal dauth token info for user %s: %v", userID, err)
		return "", constants.ErrMarshalResponse
	}
	return string(b), nil
}

// ---- RPC: Get DAuth User Info ----

func (h *RPCHandlers) GetDAuthUserInfoRPC(
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

	token, err := h.dauthRepo.GetToken(ctx, userID)
	if err != nil {
		if err == sql.ErrNoRows {
			resp := DAuthUserResponse{Success: false}
			b, _ := json.Marshal(resp)
			return string(b), nil
		}
		logger.Error("failed to read dauth token for user %s: %v", userID, err)
		return "", constants.ErrDBOperationFailed
	}

	// Check if token is still valid
	if !h.dauthService.ValidateToken(token) {
		logger.Warn("dauth token expired for user %s", userID)
		resp := DAuthUserResponse{Success: false}
		b, _ := json.Marshal(resp)
		return string(b), nil
	}

	// Fetch user info from DAuth
	userInfo, err := h.dauthService.GetUserInfo(ctx, token.AccessToken)
	if err != nil {
		logger.Error("failed to fetch user info from DAuth for user %s: %v", userID, err)
		return "", constants.ErrDBOperationFailed
	}

	resp := DAuthUserResponse{
		Success: true,
		User:    userInfo,
	}
	b, err := json.Marshal(resp)
	if err != nil {
		logger.Error("failed to marshal dauth user info for user %s: %v", userID, err)
		return "", constants.ErrMarshalResponse
	}
	return string(b), nil
}

// ---- RPC: Revoke DAuth Token ----

func (h *RPCHandlers) RevokeDAuthTokenRPC(
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

	tx, err := db.Begin()
	if err != nil {
		logger.Error("failed to begin transaction for user %s: %v", userID, err)
		return "", constants.ErrDBOperationFailed
	}
	defer tx.Rollback()

	if err := h.dauthRepo.DeleteToken(ctx, tx, userID); err != nil {
		logger.Error("failed to delete dauth token for user %s: %v", userID, err)
		return "", constants.ErrDBOperationFailed
	}

	if err := tx.Commit(); err != nil {
		logger.Error("failed to commit transaction for user %s: %v", userID, err)
		return "", constants.ErrDBOperationFailed
	}

	resp := map[string]any{"success": true}
	b, _ := json.Marshal(resp)
	return string(b), nil
}
