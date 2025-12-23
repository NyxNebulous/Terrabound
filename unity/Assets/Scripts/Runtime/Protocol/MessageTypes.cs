using System;

namespace Terrabound.Runtime.Protocol
{
    /// <summary>
    /// LAYER 3: Network / Protocol Layer - Message Types
    /// 
    /// Define your opcode message structures here.
    /// </summary>
    [Serializable]
    public class MultiPlayerMessage<T>
    {
        public string uuid;
        public string needLastStateUserId;
        public T message;

        public MultiPlayerMessage() { }

        public MultiPlayerMessage(T msg, string userId = null)
        {
            uuid = Guid.NewGuid().ToString();
            needLastStateUserId = userId;
            message = msg;
        }
    }

    // Use these consistent strings across all clients.
    public static class MatchOpcodes
    {
        public const string PlayerMove = "player_move";
        public const string PlayerAction = "player_action";
        public const string GameStateSync = "game_state_sync";
        public const string PlayerJoin = "player_join";
        public const string PlayerLeave = "player_leave";

        // Add more opcodes as needed
    }

    // Result data returned from joining a match.
    public class MatchJoinResult
    {
        public string matchId;
        public long serverTime;
        public int minElo;
        public int maxElo;
        public int currentPlayers;
        public int maxPlayers;
    }

    // Response from backend RPC for dynamic match.
    public class DynamicMatchResponse
    {
        public string matchId { get; set; }
        public long serverTime { get; set; }
        public int minElo { get; set; }
        public int maxElo { get; set; }
        public int currentPlayers { get; set; }
        public int maxPlayers { get; set; }
    }
}
