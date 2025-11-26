package main

import (
	"context"
	"database/sql"

	"github.com/heroiclabs/nakama-common/runtime"

	"github.com/delta/terrabound/backend/internal/nakama"
)

// InitModule is the plugin entrypoint loaded by Nakama. It delegates to our modular package for clarity.
func InitModule(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, initializer runtime.Initializer) error {
	return nakama.InitModule(ctx, logger, db, nk, initializer)
}
