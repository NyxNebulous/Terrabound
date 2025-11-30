package game


//JUST SOME RANDOM FILE TO UNDERSTAND FLOW

// import (
// 	"encoding/json"
// 	"time"
// )

// // MatchID represents a Nakama authoritative match identifier.
// type MatchID string

// // PlayerID represents a Nakama user identifier.
// type PlayerID string

// // Territory describes ownership and stationed units for a map tile.
// type Territory struct {
// 	ID    string   `json:"id"`
// 	Owner PlayerID `json:"owner"`
// 	Units int      `json:"units"`
// }

// // MatchState captures the minimal strategic snapshot we can expose to Unity.
// type MatchState struct {
// 	ID          MatchID              `json:"matchId"`
// 	Turn        uint32               `json:"turn"`
// 	LastTick    time.Time            `json:"lastTick"`
// 	Territories map[string]Territory `json:"territories"`
// }

// // Order models an action a commander wants to execute in the simulation.
// type Order struct {
// 	Player PlayerID `json:"player"`
// 	Action string   `json:"action"`
// 	Target string   `json:"target"`
// 	Units  int      `json:"units"`
// }

// // OrderEvaluation returns whether the order is accepted plus any projected state.
// type OrderEvaluation struct {
// 	Valid      bool        `json:"valid"`
// 	Reason     string      `json:"reason,omitempty"`
// 	NextTurn   uint32      `json:"nextTurn"`
// 	Projection *MatchState `json:"projection,omitempty"`
// }

// // Clone makes a deep copy so pure game logic never mutates shared references.
// func (ms *MatchState) Clone() *MatchState {
// 	if ms == nil {
// 		return nil
// 	}
// 	territories := make(map[string]Territory, len(ms.Territories))
// 	for k, v := range ms.Territories {
// 		territories[k] = v
// 	}
// 	return &MatchState{
// 		ID:          ms.ID,
// 		Turn:        ms.Turn,
// 		LastTick:    ms.LastTick,
// 		Territories: territories,
// 	}
// }

// // MarshalJSON ensures timestamps are emitted in RFC3339 for easy Unity parsing.
// func (ms *MatchState) MarshalJSON() ([]byte, error) {
// 	type alias MatchState
// 	return json.Marshal(&struct {
// 		LastTick string `json:"lastTick"`
// 		*alias
// 	}{
// 		LastTick: ms.LastTick.UTC().Format(time.RFC3339Nano),
// 		alias:    (*alias)(ms),
// 	})
// }
