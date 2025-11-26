package game

import (
	"errors"
	"fmt"
	"time"
)

// Clock abstracts time for deterministic tests.
type Clock interface {
	Now() time.Time
}

type systemClock struct{}

func (systemClock) Now() time.Time { return time.Now().UTC() }

// SeedState creates a predictable default state for quick iterations.
// Accepts an optional clock; pass nil to use the real system clock.
func SeedState(matchID MatchID, playerIDs []PlayerID, clock Clock) *MatchState {
	if clock == nil {
		clock = systemClock{}
	}
	territories := map[string]Territory{}
	for idx, pid := range playerIDs {
		territories[fmt.Sprintf("capital-%d", idx+1)] = Territory{ID: fmt.Sprintf("capital-%d", idx+1), Owner: pid, Units: 20}
	}
	return &MatchState{
		ID:          matchID,
		Turn:        1,
		LastTick:    clock.Now(),
		Territories: territories,
	}
}

// ValidateOrder performs lightweight rules checking and simulates the next turn snapshot.
// Returns an OrderEvaluation or an error if input is invalid.
func ValidateOrder(state *MatchState, order Order, clock Clock) (*OrderEvaluation, error) {
	if state == nil {
		return nil, errors.New("missing match state")
	}
	if order.Player == "" {
		return nil, errors.New("missing player")
	}
	terr, ok := state.Territories[order.Target]
	if !ok {
		return &OrderEvaluation{Valid: false, Reason: "unknown territory", NextTurn: state.Turn}, nil
	}
	if terr.Owner != order.Player {
		return &OrderEvaluation{Valid: false, Reason: "player does not control target", NextTurn: state.Turn}, nil
	}
	if order.Units <= 0 || order.Units > terr.Units {
		return &OrderEvaluation{Valid: false, Reason: "invalid unit count", NextTurn: state.Turn}, nil
	}
	projection := state.Clone()
	projTerr := projection.Territories[order.Target]
	projTerr.Units -= order.Units
	if projTerr.Units == 0 {
		projTerr.Units = 1 // leave at least a garrison for example purposes
	}
	projection.Territories[order.Target] = projTerr
	projection.Turn = state.Turn + 1
	if clock == nil {
		clock = systemClock{}
	}
	projection.LastTick = clock.Now()
	return &OrderEvaluation{
		Valid:      true,
		NextTurn:   projection.Turn,
		Projection: projection,
	}, nil
}
