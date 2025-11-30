package nakama

import (
	"context"
	"database/sql"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"
)

const (
	rpcValidateOrder = "tb_order_validate"
	matchName        = "tb_war_room"
)

func InitModule(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, initializer runtime.Initializer) error {
	start := time.Now()

	// dauthRepo := dauth.NewSQLDAuthRepository(db)
	// dauthConf := dauth.NewDAuthConfig()
	// dauthService := dauth.NewDAuthService(dauthConf)

	// rpcHandlers := NewRPCHandlers(dauthRepo, dauthConf, *dauthService)

	// if err := rpcHandlers.RegisterOAuthRPCs(initializer, logger); err != nil {
	// 	return err
	// }

	logger.Info("Terrabound backend initialized in %dms", time.Since(start).Milliseconds())
	return nil
}
