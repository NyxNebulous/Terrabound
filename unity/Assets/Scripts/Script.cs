using System;
using System.Collections.Generic;
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
    public string JoinMatchId = "";

    [Header("Player Setup")]
    public GameObject PlayerPrefab;
    private IClient _client;
    private ISocket _socket;
    private ISession _session;
    private string _matchId;

    private GameObject _localPlayer;
    private Dictionary<string, GameObject> _remotePlayers = new Dictionary<string, GameObject>();
    private float _sendTimer = 0f;
    private const float SEND_INTERVAL = 0.05f; // 20Hz  
    private Queue<Action> _mainThreadQueue = new Queue<Action>();
    private object _queueLock = new object();
    private const long OPCODE_POSITION = 1;
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
            _client = new Client("http", Host, Port, ServerKey, UnityWebRequestAdapter.Instance);

            var deviceId = GetOrCreateDeviceId();
            _session = await _client.AuthenticateDeviceAsync(deviceId);
            Debug.Log($"Authenticated as {_session.UserId} and {_session.Username}");

            _socket = _client.NewSocket();
            RegisterSocketHandlers();
            await _socket.ConnectAsync(_session, true);
            Debug.Log("Socket connected");

            if (!string.IsNullOrEmpty(JoinMatchId))
            {
                var match = await _socket.JoinMatchAsync(JoinMatchId);
                _matchId = match.Id;
                Debug.Log($"Joined match: {_matchId}");
            }
            else
            {
                var match = await _socket.CreateMatchAsync();
                _matchId = match.Id;
                Debug.Log($"Created match: {_matchId}\nCopy this ID to JoinMatchId in other clients");
            }

            SpawnLocalPlayer();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Connection failed: {ex.Message}");
        }
    }
    private void RegisterSocketHandlers()
    {
        _socket.ReceivedMatchPresence += ev =>
        {
            foreach (var presence in ev.Joins)
            {
                if (presence.UserId != _session.UserId)
                {
                    Enqueue(() => SpawnRemotePlayer(presence.UserId));
                }
            }

            foreach (var presence in ev.Leaves)
            {
                Enqueue(() => DespawnRemotePlayer(presence.UserId));
            }
        };
        _socket.ReceivedMatchState += state =>
        {
            if (state.OpCode == OPCODE_POSITION && state.UserPresence.UserId != _session.UserId)
            {
                try
                {
                    var json = Encoding.UTF8.GetString(state.State);
                    var msg = JsonUtility.FromJson<PosMsg>(json);
                    Enqueue(() => UpdateRemotePlayerPosition(msg.uid, msg.x, msg.y));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to parse position: {ex.Message}");
                }
            }
        };
    }

    void Update()
    {
        ProcessMainThreadQueue();

        if (_localPlayer != null && _matchId != null)
        {
            // _sendTimer += Time.deltaTime;  
            // if (_sendTimer >= SEND_INTERVAL)  
            // {  
            //     _sendTimer = 0f;  
            SendLocalPosition().Forget();
            // }  
        }
    }

    private void SpawnLocalPlayer()
    {
        if (_localPlayer != null) return;

        _localPlayer = Instantiate(PlayerPrefab, new Vector3(0, 0, 0), Quaternion.identity);
        _localPlayer.name = "LocalPlayer";

        SetPlayerColor(_localPlayer, Color.green);

        var controller = _localPlayer.AddComponent<SimplePlayerController>();

        Debug.Log("Local player spawned");
    }

    private void SpawnRemotePlayer(string userId)
    {
        if (_remotePlayers.ContainsKey(userId)) return;

        var player = Instantiate(PlayerPrefab, Vector3.zero, Quaternion.identity);
        player.name = $"RemotePlayer_{userId.Substring(0, 8)}";

        SetPlayerColor(player, Color.red);

        _remotePlayers[userId] = player;
        Debug.Log($"Remote player spawned: {userId}");
    }

    private void DespawnRemotePlayer(string userId)
    {
        if (_remotePlayers.TryGetValue(userId, out var player))
        {
            Destroy(player);
            _remotePlayers.Remove(userId);
            Debug.Log($"Remote player removed: {userId}");
        }
    }

    private void UpdateRemotePlayerPosition(string userId, float x, float y)
    {
        if (!_remotePlayers.ContainsKey(userId))
        {
            SpawnRemotePlayer(userId);
        }

        if (_remotePlayers.TryGetValue(userId, out var player))
        {
            player.transform.position = new Vector3(x, y, 0f);
        }
    }

    private async UniTask SendLocalPosition()
    {
        if (_socket == null || _matchId == null || _localPlayer == null) return;

        var pos = _localPlayer.transform.position;
        var msg = new PosMsg
        {
            uid = _session.UserId,
            x = pos.x,
            y = pos.y
        };

        var json = JsonUtility.ToJson(msg);
        var bytes = Encoding.UTF8.GetBytes(json);

        try
        {
            await _socket.SendMatchStateAsync(_matchId, OPCODE_POSITION, bytes);
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

    // Main-thread queue helpers  
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

    private string GetOrCreateDeviceId()
    {
        const string key = "nakama_device_id";

        // FOR TESTING: Always generate unique ID
        var testId = Guid.NewGuid().ToString();
        Debug.Log($"  → Generated TEST device ID: {testId.Substring(0, 8)}...");
        return testId;

        // (uncomment for real use):
        // if (PlayerPrefs.HasKey(key))
        // {
        //     var id = PlayerPrefs.GetString(key);
        //     Debug.Log($"  → Using existing device ID: {id.Substring(0, 8)}...");
        //     return id;
        // }

        // var newId = Guid.NewGuid().ToString();
        // PlayerPrefs.SetString(key, newId);
        // PlayerPrefs.Save();
        // Debug.Log($"  → Generated new device ID: {newId.Substring(0, 8)}...");
        // return newId;
    }

    private async void OnDestroy()
    {
        try
        {
            if (_socket != null) await _socket.CloseAsync();
        }
        catch { }
    }

    [Serializable]
    private class PosMsg
    {
        public string uid;
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

        Vector2 move = new Vector2(0, 0);

        // WASD  
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;

        transform.Translate(move.normalized * Speed * Time.deltaTime);
    }
}