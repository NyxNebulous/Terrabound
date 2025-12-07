package main

import (
	"context"
	"database/sql"

	"github.com/heroiclabs/nakama-common/runtime"

	"github.com/delta/terrabound/backend/internal/nakama"
)

func InitModule(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, initializer runtime.Initializer) error {
	return nakama.InitModule(ctx, logger, db, nk, initializer)
}
