package nakama

import (
	"context"
	"database/sql"
	"net/http"

	"github.com/heroiclabs/nakama-common/runtime"
)

func InitModule(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, initializer runtime.Initializer) error {
	logger.Info("=== Nakama Go Backend Module Loading ===")

	if err := initializer.RegisterHttp("/api/hi", func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		w.Write([]byte(`{"message":"hi from nakama"}`))
	}, http.MethodGet); err != nil {
		return err
	}

	// Auth endpoints (no session/http_key required)
	if err := initializer.RegisterHttp("/auth/init", HTTPAuthInitHandler(ctx, logger, nk), http.MethodPost); err != nil {
		return err
	}
	if err := initializer.RegisterHttp("/auth/check", HTTPAuthCheckHandler(ctx, logger, nk), http.MethodPost); err != nil {
		return err
	}
	if err := initializer.RegisterHttp("/auth/callback", CreateAuthCallbackHandler(ctx, logger, nk), http.MethodGet); err != nil {
		return err
	}

	if err := initializer.RegisterMatch("movement_match", func(
		ctx context.Context,
		logger runtime.Logger,
		db *sql.DB,
		nk runtime.NakamaModule,
	) (runtime.Match, error) {
		return &MovementMatch{}, nil
	}); err != nil {
		logger.Error("Failed to register movement_match: %v", err)
		return err
	}

	if err := initializer.RegisterRpc("dynamic_match", func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
		return requestDynamicMatch(ctx, logger, db, nk, payload)
	}); err != nil {
		return err
	}
	
	logger.Info("=== Backend Ready - Waiting for Unity clients ===")

	return nil
}
