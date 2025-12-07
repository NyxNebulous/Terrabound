package nakama

import (
	"context"
	"database/sql"
	"encoding/json"

	"github.com/heroiclabs/nakama-common/runtime"
)

type InputMessage struct {
	X float32 `json:"x"`
	Y float32 `json:"y"`
}

type PlayerState struct {
	UserID string  `json:"user_id"`
	X      float32 `json:"x"`
	Y      float32 `json:"y"`
}

type MatchState struct {
	Players map[string]*PlayerState
}

type MovementMatch struct{}

func (m *MovementMatch) MatchInit(
	ctx context.Context,
	logger runtime.Logger,
	db *sql.DB,
	nk runtime.NakamaModule,
	params map[string]interface{},
) (interface{}, int, string) {

	state := &MatchState{
		Players: make(map[string]*PlayerState),
	}

	tickRate := 10 // 10 ticks/sec
	label := "movement_match"

	logger.Info("Movement match initialized.")
	return state, tickRate, label
}

func (m *MovementMatch) MatchJoinAttempt(
	ctx context.Context,
	logger runtime.Logger,
	db *sql.DB,
	nk runtime.NakamaModule,
	dispatcher runtime.MatchDispatcher,
	tick int64,
	state interface{},
	presence runtime.Presence,
	metadata map[string]string,
) (interface{}, bool, string) {
	return state, true, ""
}

func (m *MovementMatch) MatchJoin(
	ctx context.Context,
	logger runtime.Logger,
	db *sql.DB,
	nk runtime.NakamaModule,
	dispatcher runtime.MatchDispatcher,
	tick int64,
	state interface{},
	joins []runtime.Presence,
) interface{} {

	s := state.(*MatchState)

	for _, p := range joins {
		s.Players[p.GetUserId()] = &PlayerState{
			UserID: p.GetUserId(),
			X:      0,
			Y:      0,
		}
		logger.Info("Player joined: %s", p.GetUserId())
	}

	return s
}

func (m *MovementMatch) MatchLeave(
	ctx context.Context,
	logger runtime.Logger,
	db *sql.DB,
	nk runtime.NakamaModule,
	dispatcher runtime.MatchDispatcher,
	tick int64,
	state interface{},
	leaves []runtime.Presence,
) interface{} {

	s := state.(*MatchState)

	for _, p := range leaves {
		delete(s.Players, p.GetUserId())
		logger.Info("Player left: %s", p.GetUserId())
	}

	return s
}

func (m *MovementMatch) MatchLoop(
	ctx context.Context,
	logger runtime.Logger,
	db *sql.DB,
	nk runtime.NakamaModule,
	dispatcher runtime.MatchDispatcher,
	tick int64,
	state interface{},
	messages []runtime.MatchData,
) interface{} {

	s := state.(*MatchState)

	// 1. Process input messages from clients (absolute positions)
	for _, msg := range messages {
		var input InputMessage
		if err := json.Unmarshal(msg.GetData(), &input); err != nil {
			logger.Warn("Failed to parse input from %s: %v", msg.GetUserId(), err)
			continue
		}

		if player, ok := s.Players[msg.GetUserId()]; ok {
			player.X = input.X
			player.Y = input.Y
		}
	}

	// 2. Broadcast updated state
	stateJson, err := json.Marshal(s.Players)
	if err != nil {
		logger.Error("Failed to marshal match state: %v", err)
		return s
	}

	dispatcher.BroadcastMessage(1, stateJson, nil, nil, true)

	return s
}

func (m *MovementMatch) MatchSignal(
	ctx context.Context,
	logger runtime.Logger,
	db *sql.DB,
	nk runtime.NakamaModule,
	dispatcher runtime.MatchDispatcher,
	tick int64,
	state interface{},
	data string,
) (interface{}, string) {
	return state, ""
}

func (m *MovementMatch) MatchTerminate(
	ctx context.Context,
	logger runtime.Logger,
	db *sql.DB,
	nk runtime.NakamaModule,
	dispatcher runtime.MatchDispatcher,
	tick int64,
	state interface{},
	graceSeconds int,
) interface{} {
	return state
}
