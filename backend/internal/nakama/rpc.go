package nakama

import (
	"github.com/heroiclabs/nakama-common/runtime"
	"golang.org/x/oauth2"

	"github.com/delta/terrabound/backend/internal/oauth"
)

type RPCHandlers struct {
	oauthConf *oauth2.Config
	oauthRepo oauth.OAuthRepository
}

// NewRPCHandlers constructs handlers with the minimal dependencies required.
func NewRPCHandlers(repo oauth.OAuthRepository, conf *oauth2.Config) *RPCHandlers {
	return &RPCHandlers{
		oauthRepo: repo,
		oauthConf: conf,
	}
}

const (
	rpcRegisterOAuth  = "oauth_register"
	rpcExchangeTokens = "oauth_exchange"
	rpcGetOAuthToken  = "oauth_get_token"
)

func (h *RPCHandlers) RegisterOAuthRPCs(initializer runtime.Initializer, logger runtime.Logger) error {
	if err := initializer.RegisterRpc(rpcRegisterOAuth, h.AuthorizationURLRPC); err != nil {
		logger.Error("register %s failed: %v", rpcRegisterOAuth, err)
		return err
	}

	if err := initializer.RegisterRpc(rpcExchangeTokens, h.ExchangeCodeForTokensRPC); err != nil {
		logger.Error("register %s failed: %v", rpcExchangeTokens, err)
		return err
	}
	if err := initializer.RegisterRpc(rpcGetOAuthToken, h.GetOAuthTokenRPC); err != nil {
		logger.Error("register %s failed: %v", rpcGetOAuthToken, err)
		return err
	}
	return nil
}

// // ValidateOrderRPC demonstrates parsing payloads, calling domain logic, and responding with JSON.
// func (h *RPCHandlers) ValidateOrderRPC(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
// 	var req ValidateOrderRequest
// 	if payload == "" {
// 		req = ValidateOrderRequest{}
// 	} else if err := json.Unmarshal([]byte(payload), &req); err != nil {
// 		return "", runtime.NewError("invalid json payload", 3)
// 	}

// 	if req.MatchID == "" {
// 		req.MatchID = "debug-match"
// 	}
// 	if req.PlayerID == "" {
// 		if userID, ok := ctx.Value(runtime.RUNTIME_CTX_USER_ID).(string); ok {
// 			req.PlayerID = userID
// 		}
// 	}

// 	// Use package-level game logic functions (service layer removed).
// 	state := game.SeedState(game.MatchID(req.MatchID), []game.PlayerID{game.PlayerID(req.PlayerID)}, nil)
// 	order := game.Order{
// 		Player: game.PlayerID(req.PlayerID),
// 		Action: req.Action,
// 		Target: req.Target,
// 		Units:  req.Units,
// 	}

// 	eval, err := game.ValidateOrder(state, order, nil)
// 	if err != nil {
// 		return "", runtime.NewError(err.Error(), 13)
// 	}

// 	resp := ValidateOrderResponse{
// 		Valid:    eval.Valid,
// 		Reason:   eval.Reason,
// 		NextTurn: eval.NextTurn,
// 	}
// 	if eval.Projection != nil {
// 		resp.Projection = eval.Projection
// 	}

// 	bytes, err := json.Marshal(resp)
// 	if err != nil {
// 		return "", runtime.NewError("failed to marshal response", 13)
// 	}

// 	return string(bytes), nil
// }

// // ValidateOrderRequest is the payload from Unity or Nakama Console.
// type ValidateOrderRequest struct {
// 	MatchID  string `json:"matchId"`
// 	PlayerID string `json:"playerId"`
// 	Action   string `json:"action"`
// 	Target   string `json:"target"`
// 	Units    int    `json:"units"`
// }

// // ValidateOrderResponse echoes the domain evaluation.
// type ValidateOrderResponse struct {
// 	Valid      bool             `json:"valid"`
// 	Reason     string           `json:"reason,omitempty"`
// 	NextTurn   uint32           `json:"nextTurn"`
// 	Projection *game.MatchState `json:"projection,omitempty"`
// }
