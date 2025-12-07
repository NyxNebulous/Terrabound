using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using Nakama;
using UnityEngine;

public class MultiplayerManager : MonoBehaviour
{
    [Header("Nakama Settings")]
    public string Host = "127.0.0.1";
    public int Port = 7350;
    public string ServerKey = "defaultkey";
    
    [Header("Match Settings")]
    [Tooltip("Leave empty to CREATE a new match, or paste Match ID to JOIN existing match")]
    public string JoinMatchId = "";
    
    [Header("Player Setup")]
    public GameObject PlayerPrefab;
    
    private IClient _client;
    private ISocket _socket;
    private ISession _session;
    private IMatch _currentMatch;
    
    private GameObject _localPlayer;
    private Dictionary<string, GameObject> _remotePlayers = new Dictionary<string, GameObject>();
    private Queue<Action> _mainThreadQueue = new Queue<Action>();
    private object _queueLock = new object();
    
    private const long OPCODE_STATE_UPDATE = 1;
    private float _sendTimer = 0f;
    private const float SEND_INTERVAL = 0.1f; // 10Hz to match server tick rate

    async void Start()
    {
        if (PlayerPrefab == null)
        {
            Debug.LogError("PlayerPrefab not assigned!");
            return;
        }
        
        await ConnectAndJoinAsync();
    }

    private async UniTask ConnectAndJoinAsync()
    {
        try
        {
            // 1. Connect to Nakama
            _client = new Client("http", Host, Port, ServerKey, UnityWebRequestAdapter.Instance);
            Debug.Log($"Connecting to Nakama at {Host}:{Port}...");

            // 2. Authenticate with unique device ID
            var deviceId = Guid.NewGuid().ToString();
            _session = await _client.AuthenticateDeviceAsync(deviceId);
            Debug.Log($"✓ Authenticated as {_session.Username} (ID: {_session.UserId.Substring(0, 8)}...)");

            // 3. Create socket connection
            _socket = _client.NewSocket();
            RegisterSocketHandlers();
            await _socket.ConnectAsync(_session, true);
            Debug.Log("✓ Socket connected");

            // 4. Create or Join match
            if (string.IsNullOrEmpty(JoinMatchId))
            {
                // CREATE new match
                _currentMatch = await _socket.CreateMatchAsync("movement_match");
                Debug.Log($"✓ Created server-authoritative match: {_currentMatch.Id}");
                Debug.Log($"<color=yellow>📋 Share this Match ID with other players: {_currentMatch.Id}</color>");
            }
            else
            {
                // JOIN existing match
                _currentMatch = await _socket.JoinMatchAsync(JoinMatchId.Trim());
                Debug.Log($"✓ Joined existing match: {_currentMatch.Id}");
            }

            // 5. Spawn local player
            SpawnLocalPlayer();
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Connection failed: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void RegisterSocketHandlers()
    {
        // Handle players joining/leaving
        _socket.ReceivedMatchPresence += ev =>
        {
            Debug.Log($"Match presence update - Joins: {ev.Joins.Count()}, Leaves: {ev.Leaves.Count()}");
            
            foreach (var presence in ev.Joins)
            {
                if (presence.UserId != _session.UserId)
                {
                    Debug.Log($"Player joined: {presence.UserId.Substring(0, 8)}...");
                    Enqueue(() => SpawnRemotePlayer(presence.UserId));
                }
            }

            foreach (var presence in ev.Leaves)
            {
                Debug.Log($"Player left: {presence.UserId.Substring(0, 8)}...");
                Enqueue(() => DespawnRemotePlayer(presence.UserId));
            }
        };

        // Handle state updates from server
        _socket.ReceivedMatchState += state =>
        {
            if (state.OpCode != OPCODE_STATE_UPDATE)
                return;

            try
            {
                var json = Encoding.UTF8.GetString(state.State);
                Debug.Log($"[RECEIVED] Raw JSON from server: {json}");

                // *** 1) Try to get sender userId from presence (relay case)
                string senderUserId = null;
                if (state.UserPresence != null)
                {
                    senderUserId = state.UserPresence.UserId;
                }

                // *** 2) Try parse as full ServerPlayerState (authoritative case)
                ServerPlayerState playerState = null;
                bool parsedFull = false;
                try
                {
                    playerState = JsonUtility.FromJson<ServerPlayerState>(json);
                    if (playerState != null && (playerState.user_id != null || senderUserId != null))
                    {
                        // If user_id missing but we know sender, fill it
                        if (string.IsNullOrEmpty(playerState.user_id) && !string.IsNullOrEmpty(senderUserId))
                        {
                            playerState.user_id = senderUserId;
                        }
                        parsedFull = true;
                    }
                }
                catch
                {
                    parsedFull = false;
                }

                if (!parsedFull)
                {
                    // *** 3) Fallback: treat as simple {x,y} from relay match
                    PositionOnly pos = null;
                    try
                    {
                        pos = JsonUtility.FromJson<PositionOnly>(json);
                    }
                    catch { }

                    if (pos != null && !string.IsNullOrEmpty(senderUserId))
                    {
                        playerState = new ServerPlayerState
                        {
                            user_id = senderUserId,
                            x = pos.x,
                            y = pos.y
                        };
                        parsedFull = true;
                    }
                }

                if (parsedFull && playerState != null && !string.IsNullOrEmpty(playerState.user_id))
                {
                    Debug.Log($"[PARSED] Player: {playerState.user_id.Substring(0, 8)}... at ({playerState.x:F2}, {playerState.y:F2})");
                    
                    // Only update if it's not our own player
                    if (playerState.user_id != _session.UserId)
                    {
                        Enqueue(() => UpdateRemotePlayer(playerState));
                    }
                    else
                    {
                        Debug.Log("[SKIP] Ignoring own state (local player)");
                    }
                }
                else
                {
                    Debug.LogWarning($"[PARSE ERROR] Could not parse player state from: {json}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to parse server state: {ex.Message}\n{ex.StackTrace}");
            }
        };
    }

    void Update()
    {
        ProcessMainThreadQueue();

        // Send position updates to server
        if (_localPlayer != null && _currentMatch != null)
        {
            _sendTimer += Time.deltaTime;
            if (_sendTimer >= SEND_INTERVAL)
            {
                _sendTimer = 0f;
                SendPositionToServer().Forget();
            }
        }
    }

    private void SpawnLocalPlayer()
    {
        if (_localPlayer != null) return;

        _localPlayer = Instantiate(PlayerPrefab, Vector3.zero, Quaternion.identity);
        _localPlayer.name = "LocalPlayer";
        SetPlayerColor(_localPlayer, Color.green);
        _localPlayer.AddComponent<SimplePlayerController>();
        
        Debug.Log("✓ Local player spawned (Green)");
    }

    private void SpawnRemotePlayer(string userId)
    {
        if (_remotePlayers.ContainsKey(userId)) return;

        var player = Instantiate(PlayerPrefab, Vector3.zero, Quaternion.identity);
        player.name = $"RemotePlayer_{userId.Substring(0, 8)}";
        SetPlayerColor(player, Color.red);
        
        _remotePlayers[userId] = player;
        Debug.Log($"✓ Remote player spawned (Red): {userId.Substring(0, 8)}...");
    }

    private void DespawnRemotePlayer(string userId)
    {
        if (_remotePlayers.TryGetValue(userId, out var player))
        {
            Destroy(player);
            _remotePlayers.Remove(userId);
            Debug.Log($"Remote player removed: {userId.Substring(0, 8)}...");
        }
    }

    private void UpdateRemotePlayer(ServerPlayerState playerState)
    {
        Debug.Log($"[UPDATE REMOTE] UserId: {playerState.user_id.Substring(0, 8)}... to position ({playerState.x:F2}, {playerState.y:F2})");
        
        if (!_remotePlayers.ContainsKey(playerState.user_id))
        {
            Debug.Log("[SPAWN NEEDED] Remote player not found, spawning...");
            SpawnRemotePlayer(playerState.user_id);
        }

        if (_remotePlayers.TryGetValue(playerState.user_id, out var player))
        {
            // Direct position update (server is authoritative)
            var targetPos = new Vector3(playerState.x, playerState.y, 0f);
            player.transform.position = targetPos;
            Debug.Log($"[POSITION SET] Remote player {playerState.user_id.Substring(0, 8)}... moved to {targetPos}");
        }
        else
        {
            Debug.LogError($"[ERROR] Failed to find remote player {playerState.user_id.Substring(0, 8)}... in dictionary!");
        }
    }

    private async UniTask SendPositionToServer()
    {
        if (_socket == null || _currentMatch == null || _localPlayer == null) return;

        var pos = _localPlayer.transform.position;
        
        // Send position in format server expects: {x, y}
        var inputMsg = new InputMessage
        {
            x = pos.x,
            y = pos.y
        };

        var json = JsonUtility.ToJson(inputMsg);
        var bytes = Encoding.UTF8.GetBytes(json);

        try
        {
            await _socket.SendMatchStateAsync(_currentMatch.Id, OPCODE_STATE_UPDATE, bytes);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Send failed: {ex.Message}");
        }
    }

    private void SetPlayerColor(GameObject player, Color color)
    {
        var renderer = player.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = color;
        }
        else
        {
            var meshRenderer = player.GetComponentInChildren<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.material.color = color;
            }
        }
    }

    private void Enqueue(Action action)
    {
        lock (_queueLock)
        {
            _mainThreadQueue.Enqueue(action);
        }
    }

    private void ProcessMainThreadQueue()
    {
        lock (_queueLock)
        {
            while (_mainThreadQueue.Count > 0)
            {
                var action = _mainThreadQueue.Dequeue();
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Main thread action error: {ex.Message}");
                }
            }
        }
    }

    private async void OnDestroy()
    {
        try
        {
            if (_socket != null)
            {
                await _socket.CloseAsync();
                Debug.Log("Socket closed");
            }
        }
        catch { }
    }

    // Data structures matching Go backend
    [Serializable]
    private class InputMessage
    {
        public float x;
        public float y;
    }

    // *** Full state (authoritative) – may come from your Go match handler
    [Serializable]
    private class ServerPlayerState
    {
        public string user_id;
        public float x;
        public float y;
    }

    // *** Simple {x,y} only – for relayed matches / old backend
    [Serializable]
    private class PositionOnly
    {
        public float x;
        public float y;
    }
}

public class SimplePlayerController : MonoBehaviour
{
    public float Speed = 5f;

    void Update()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return;

        Vector2 move = Vector2.zero;

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;

        transform.Translate(move.normalized * Speed * Time.deltaTime);
    }
}
