using UnityEngine;
using Nakama;
using System;
using System.Text;
using System.Threading.Tasks;

public class MatchClient : MonoBehaviour
{
    public static MatchClient Instance;

    private ISocket socket;
    private IMatch match;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public async Task JoinMatch(string matchId = "movement_match")
    {
        // Make sure NakamaConnection is initialized
        socket = NakamaConnection.Instance.GetSocket();

        if (socket == null)
        {
            Debug.LogError("Socket is null! Make sure NakamaConnection is ready.");
            return;
        }

        // Join the server-authoritative match
        match = await socket.JoinMatchAsync(matchId);
        Debug.Log($"Joined match: {match.Id}");

        // Listen for messages from the match
        socket.ReceivedMatchState += OnMatchState;
    }

    private void OnMatchState(IMatchState state)
    {
        string json = Encoding.UTF8.GetString(state.State);
        Debug.Log("[Match Message] " + json);

        // Here you can parse JSON and update other players' positions
        // Example: {"playerName":"Alice","x":1.2,"y":3.4}
    }

    public async void SendPlayerMove(string playerName, Vector3 position)
    {
        if (match == null)
        {
            Debug.LogWarning("Not in a match yet!");
            return;
        }

        // Create a simple JSON payload
        string json = JsonUtility.ToJson(new PlayerMoveData
        {
            playerName = playerName,
            x = position.x,
            y = position.y
        });

        byte[] data = Encoding.UTF8.GetBytes(json);

        // Send to match with OpCode 1 (you can define different codes for different actions)
        await socket.SendMatchStateAsync(match.Id, 1, data);
    }
}

// Helper class for JSON serialization
[Serializable]
public class PlayerMoveData
{
    public string playerName;
    public float x;
    public float y;
}
