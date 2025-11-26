package nakama

import (
	"context"
	"database/sql"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"

	"github.com/delta/terrabound/backend/internal/oauth"
)

const (
	rpcValidateOrder = "tb_order_validate"
	matchName        = "tb_war_room"
)

func InitModule(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, initializer runtime.Initializer) error {
	start := time.Now()

	// Build OAuth repository and config
	oauthRepo := oauth.NewSQLOAuthRepository(db)
	oauthConf := oauth.NewOAuthConfig()

	rpcHandlers := NewRPCHandlers(oauthRepo, oauthConf)

	if err := rpcHandlers.RegisterOAuthRPCs(initializer, logger); err != nil {
		return err
	}

	logger.Info("Terrabound backend initialized in %dms", time.Since(start).Milliseconds())
	return nil
}
