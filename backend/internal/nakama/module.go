package nakama

import (
	"context"
	"database/sql"
	"net/http"

	"github.com/heroiclabs/nakama-common/runtime"
)

// InitModule is the plugin entrypoint for Nakama
func InitModule(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, initializer runtime.Initializer) error {
	logger.Info("Nakama REST API + Match module loaded")

	// Register simple HTTP endpoint
	if err := initializer.RegisterHttp("/api/hi", func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		w.Write([]byte(`{"message":"hi from nakama"}`))
	}, http.MethodGet); err != nil {
		return err
	}

	// Register the HiMatch handler for WebSockets
	if err := initializer.RegisterMatch("movement_match", func(
		ctx context.Context,
		logger runtime.Logger,
		db *sql.DB,
		nk runtime.NakamaModule,
	) (runtime.Match, error) {
		return &MovementMatch{}, nil
	}); err != nil {
		return err
	}

	return nil
}
