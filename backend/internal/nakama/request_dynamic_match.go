package nakama

import (
	"context"
	"database/sql"
	"encoding/json"
	"fmt"
	"math"
	"time"

	"github.com/heroiclabs/nakama-common/api"
	"github.com/heroiclabs/nakama-common/runtime"
)

const (
	matchesCollection = "dynamic_matches"
	maxReturnRecords  = 128
)

type matchMetadata struct {
	MatchId        string `json:"matchId"`
	MinElo         int32  `json:"minElo"`
	MaxElo         int32  `json:"maxElo"`
	CurrentPlayers int32  `json:"currentPlayers"`
	MaxPlayers     int32  `json:"maxPlayers"`
	CreatedAt      int64  `json:"createdAt"`
}

type rpcResponse struct {
	MatchId        string `json:"matchId"`
	MinElo         int32  `json:"minElo"`
	MaxElo         int32  `json:"maxElo"`
	CurrentPlayers int32  `json:"currentPlayers"`
	MaxPlayers     int32  `json:"maxPlayers"`
	ServerTime     int64  `json:"serverTime"`
}

// readPlayerElo obtains the player's ELO from account metadata, defaults to 1000 when missing.
func readPlayerElo(ctx context.Context, nk runtime.NakamaModule, userID string) (int32, error) {
	users, err := nk.UsersGetId(ctx, []string{userID}, nil)
	if err != nil || len(users) == 0 {
		return 0, fmt.Errorf("users get failed: %v", err)
	}
	var meta map[string]interface{}
	if err := json.Unmarshal([]byte(users[0].Metadata), &meta); err != nil {
		return 1000, nil
	}
	if val, ok := meta["elo"].(float64); ok {
		return int32(val), nil
	}
	return 1000, nil
}

// findCompatibleMatch scans the storage bucket for a compatible open match.
func findCompatibleMatch(ctx context.Context, nk runtime.NakamaModule, elo int32) (*api.StorageObject, *matchMetadata, error) {
	list, _, err := nk.StorageList(ctx, "", "", matchesCollection, maxReturnRecords, "")
	if err != nil {
		return nil, nil, err
	}
	var mostAppropriateMatch *matchMetadata
	var matchRecord *api.StorageObject
	for _, rec := range list {
		var meta matchMetadata
		if err := json.Unmarshal([]byte(rec.Value), &meta); err != nil {
			continue
		}
		if meta.CurrentPlayers >= meta.MaxPlayers {
			continue
		}
		if mostAppropriateMatch == nil {
			mostAppropriateMatch = &meta
			matchRecord = rec
			continue
		}
		currentMid := float64(meta.MinElo+meta.MaxElo) / 2.0
		bestMid := float64(mostAppropriateMatch.MinElo+mostAppropriateMatch.MaxElo) / 2.0

		if math.Abs(float64(elo)-currentMid) < math.Abs(float64(elo)-bestMid) {
			mostAppropriateMatch = &meta
			matchRecord = rec
		}
	}
	return matchRecord, mostAppropriateMatch, nil
}

// writeMatchRecord writes or updates a match record.
func writeMatchRecord(ctx context.Context, nk runtime.NakamaModule, record *matchMetadata, version string) error {
	value, _ := json.Marshal(record)
	writes := []*runtime.StorageWrite{
		{
			Collection:      matchesCollection,
			Key:             record.MatchId,
			UserID:          "", // public ownership
			Value:           string(value),
			PermissionRead:  2,
			PermissionWrite: 0,
			Version:         version,
		},
	}
	if _, err := nk.StorageWrite(ctx, writes); err != nil {
		return err
	}
	return nil
}

// requestDynamicMatch implements the backend-driven dynamic room creation/join selection.
// HAVE TO FIX RACE CONDITION IN THIS RPC.
func requestDynamicMatch(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	session := ctx.Value(runtime.RUNTIME_CTX_USER_ID).(string)
	if session == "" {
		return "", runtime.NewError("missing user session", 3)
	}

	elo, err := readPlayerElo(ctx, nk, session)
	if err != nil {
		logger.Warn("elo read failed: %v", err)
		elo = 1000
	}

	const maxPlayers = int32(8)
	const eloRange = int32(200)

	// 1) Find compatible open match
	rec, meta, err := findCompatibleMatch(ctx, nk, elo)
	if err != nil {
		logger.Error("list matches failed: %v", err)
	}

	if meta != nil && rec != nil {
		meta.CurrentPlayers = int32(math.Min(float64(meta.CurrentPlayers+1), float64(meta.MaxPlayers)))
		if err := writeMatchRecord(ctx, nk, meta, rec.Version); err != nil {
			return "", runtime.NewError(fmt.Sprintf("failed to update match: %v", err), 13)
		}
		resp := rpcResponse{
			MatchId:        meta.MatchId,
			MinElo:         meta.MinElo,
			MaxElo:         meta.MaxElo,
			CurrentPlayers: meta.CurrentPlayers,
			MaxPlayers:     meta.MaxPlayers,
			ServerTime:     time.Now().Unix(),
		}
		out, _ := json.Marshal(resp)
		return string(out), nil
	}

	// 2) Create new authoritative match and persist metadata
	matchId, err := nk.MatchCreate(ctx, "movement_match", map[string]interface{}{
		"minElo": elo - eloRange,
		"maxElo": elo + eloRange})

	if err != nil {
		return "", runtime.NewError(fmt.Sprintf("match create failed: %v", err), 13)
	}

	meta = &matchMetadata{
		MatchId:        matchId,
		MinElo:         elo - eloRange,
		MaxElo:         elo + eloRange,
		CurrentPlayers: 1,
		MaxPlayers:     maxPlayers,
		CreatedAt:      time.Now().Unix(),
	}

	if err := writeMatchRecord(ctx, nk, meta, ""); err != nil {
		return "", runtime.NewError(fmt.Sprintf("storage write failed: %v", err), 13)
	}

	resp := rpcResponse{
		MatchId:        meta.MatchId,
		MinElo:         meta.MinElo,
		MaxElo:         meta.MaxElo,
		CurrentPlayers: meta.CurrentPlayers,
		MaxPlayers:     meta.MaxPlayers,
		ServerTime:     time.Now().Unix(),
	}
	out, _ := json.Marshal(resp)
	return string(out), nil
}