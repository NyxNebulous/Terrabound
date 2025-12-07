using UnityEngine;
using Nakama;
using System.Collections.Generic;
using Newtonsoft.Json; // Make sure you install Newtonsoft.Json
using System.Text;

public class RemotePlayerController : MonoBehaviour
{
    public GameObject playerPrefab; // Assign your player prefab
    private Dictionary<string, GameObject> remotePlayers = new Dictionary<string, GameObject>();

    private string localUserId;

    private void Start()
    {
        if (NakamaConnection.Instance == null)
        {
            Debug.LogError("NakamaConnection instance not found!");
            return;
        }

        var socket = NakamaConnection.Instance.Socket;
        if (socket == null)
        {
            Debug.LogError("Nakama socket not connected yet!");
            return;
        }

        // Store your own Nakama user_id
        localUserId = NakamaConnection.Instance.GetSession().UserId;

        // Listen for match state updates
        socket.ReceivedMatchState += OnMatchState;
    }

    private void OnMatchState(IMatchState matchState)
    {
        if (matchState?.State == null) return;

        string json = Encoding.UTF8.GetString(matchState.State);

        Dictionary<string, PlayerState> playersDict = null;
        try
        {
            // Parse dictionary keyed by user_id
            playersDict = JsonConvert.DeserializeObject<Dictionary<string, PlayerState>>(json);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Failed to parse match state JSON: " + e.Message);
            return;
        }

        foreach (var kvp in playersDict)
        {
            string userId = kvp.Key;
            PlayerState pState = kvp.Value;

            // Skip your own player
            if (userId == localUserId) continue;

            // Spawn or update remote player
            if (!remotePlayers.ContainsKey(userId))
            {
                GameObject newPlayer = Instantiate(playerPrefab, new Vector3(pState.x, pState.y, 0f), Quaternion.identity);
                remotePlayers.Add(userId, newPlayer);
            }
            else
            {
                remotePlayers[userId].transform.position = new Vector3(pState.x, pState.y, 0f);
            }
        }

        // Remove players who left
        List<string> toRemove = new List<string>();
        foreach (var playerId in remotePlayers.Keys)
        {
            if (!playersDict.ContainsKey(playerId))
                toRemove.Add(playerId);
        }
        foreach (var id in toRemove)
        {
            Destroy(remotePlayers[id]);
            remotePlayers.Remove(id);
        }
    }
}

// Serializable class matching backend
[System.Serializable]

public class PlayerState
{
    public string user_id;
    public float x;
    public float y;
}
