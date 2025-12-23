using UnityEngine;
using UnityEngine.InputSystem;
using Nakama;
using Nakama.TinyJson;
using Terrabound.Runtime.Protocol;
using System.Text;
using System.Collections.Generic;

namespace Terrabound.Runtime.Examples
{
    /// <summary>
    /// EXAMPLE: How to use the new 3-layer architecture.
    /// 
    /// This shows the complete workflow:
    /// 1. Attach TerraboundMultiplayer to a GameObject
    /// 2. Call authentication and match methods from UI buttons
    /// 3. Register opcodes and send messages
    /// </summary>
    public class GameplayExample : MonoBehaviour
    {
        [System.Serializable]
        private class MovementInputMessage
        {
            public float x;
            public float y;
        }

        [System.Serializable]
        private class MovementPlayerState
        {
            public string user_id;
            public float x;
            public float y;
        }

        [Header("References")]
        [SerializeField] private TerraboundMultiplayer multiplayer;

        [Header("Player Setup")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private float moveSpeed = 5f;

        private MatchProtocol _protocol;
        private GameObject _localPlayer;
        private Dictionary<string, GameObject> _remotePlayers = new Dictionary<string, GameObject>();
        private bool _opcodesRegistered;
        
        private float _sendTimer = 0f;
        private const float SEND_INTERVAL = 0.1f; // 10Hz position updates

        private void Start()
        {
            if (multiplayer == null)
            {
                multiplayer = TerraboundMultiplayer.Instance;
            }

            if (playerPrefab == null)
            {
                Debug.LogError("PlayerPrefab not assigned! Please assign it in the Inspector.");
            }

            if (multiplayer.IsInMatch())
            {
                SetupGameplay();
                SpawnLocalPlayer();
            }
        }

        private void Update()
        {
            if (_localPlayer != null && _protocol != null && multiplayer.IsInMatch())
            {
                _sendTimer += Time.deltaTime;
                if (_sendTimer >= SEND_INTERVAL)
                {
                    _sendTimer = 0f;
                    SendPlayerMove(_localPlayer.transform.position, _localPlayer.transform.rotation);
                }
            }
        }

        #region Gameplay Setup

        /// <summary>
        /// Setup gameplay after joining match.
        /// Register opcodes and start game loop.
        /// </summary>
        private void SetupGameplay()
        {
            if (multiplayer == null)
            {
                Debug.LogError("SetupGameplay() called but multiplayer is null.");
                return;
            }

            // Get protocol layer from MatchFlow
            var matchFlow = multiplayer.GetMatchFlow();
            _protocol = matchFlow?.GetProtocol();

            if (_protocol == null)
            {
                Debug.LogError("Protocol is null. Cannot setup gameplay (matchFlow or protocol not ready). ");
                return;
            }

            RegisterOpcodes();
        }

        /// <summary>
        /// Register all opcode handlers.
        /// This is where you define what happens when you receive messages.
        /// </summary>
        private void RegisterOpcodes()
        {
            if (_opcodesRegistered)
            {
                return;
            }

            // Register PlayerMove opcode
            _protocol.RegisterOpcodeExplicit(MatchOpcodes.PlayerMove,1, OnPlayerMove);

            // Register PlayerAction opcode
            // _protocol.RegisterOpcode(MatchOpcodes.PlayerAction, OnPlayerAction);

            // Register GameStateSync opcode
            // _protocol.RegisterOpcode(MatchOpcodes.GameStateSync, OnGameStateSync);

            // Register presence events
            _protocol.OnMatchPresence += OnMatchPresenceEvent;

            _opcodesRegistered = true;
        }

        #endregion

        #region Opcode Handlers (Receiving Messages)

            // Nakama backend broadcasts a full snapshot:
            // { "<userId>": {"user_id":"<userId>","x":0,"y":0}, ... }
            // So opcode=1 is a state-sync, not a wrapped PlayerMoveState.
        private void OnPlayerMove(IMatchState state)
        {
            if (state == null) return;

            string json;
            try
            {
                json = Encoding.UTF8.GetString(state.State);
            }
            catch
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(json) || json == "null")
            {
                return;
            }

            Dictionary<string, MovementPlayerState> snapshot;
            try
            {
                snapshot = json.FromJson<Dictionary<string, MovementPlayerState>>();
            }
            catch
            {
                return;
            }

            if (snapshot == null || snapshot.Count == 0)
            {
                return;
            }

            var session = multiplayer != null ? multiplayer.GetSession() : null;
            string localUserId = session != null ? session.UserId : null;

            foreach (var kvp in snapshot)
            {
                var userId = kvp.Key;
                var playerState = kvp.Value;
                if (playerState == null) continue;

                // Only update remotes here; local moves client-side.
                if (!string.IsNullOrEmpty(localUserId) && userId == localUserId)
                {
                    continue;
                }

                UpdateRemotePlayerPosition(userId, new Vector3(playerState.x, playerState.y, 0f));
            }

            return;
        }

        private void OnMatchPresenceEvent(IMatchPresenceEvent presence)
        {
            // Player joined
            foreach (var user in presence.Joins)
            {
                Debug.Log($"[Presence] Player joined: {user.Username} (ID: {user.UserId.Substring(0, 8)}...)");
                
                // Don't spawn ourselves
                var session = multiplayer.GetSession();
                if (session == null || user.UserId != session.UserId)
                {
                    SpawnRemotePlayer(user.UserId, user.Username);
                }
            }

            // Player left
            foreach (var user in presence.Leaves)
            {
                Debug.Log($"[Presence] Player left: {user.Username} (ID: {user.UserId.Substring(0, 8)}...)");
                DespawnRemotePlayer(user.UserId);
            }
        }

        #endregion

        #region Player Management

        /// <summary>
        /// Spawn the local player with movement controller.
        /// </summary>
        private void SpawnLocalPlayer()
        {
            if (_localPlayer != null)
            {
                return;
            }

            if (playerPrefab == null)
            {
                Debug.LogError("Cannot spawn local player: PlayerPrefab is null.");
                return;
            }

            if (multiplayer == null)
            {
                return;
            }

            _localPlayer = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            _localPlayer.name = "LocalPlayer";
            SetPlayerColor(_localPlayer, Color.green);
            
            // Add movement controller
            var controller = _localPlayer.AddComponent<SimplePlayerController>();
            controller.Speed = moveSpeed;
        }

        /// <summary>
        /// Spawn a remote player for a connected user.
        /// </summary>
        private void SpawnRemotePlayer(string userId, string username)
        {
            if (_remotePlayers.ContainsKey(userId))
            {
                return;
            }

            if (playerPrefab == null)
            {
                Debug.LogError("Cannot spawn remote player: PlayerPrefab is null.");
                return;
            }

            var player = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            player.name = $"RemotePlayer_{username}";
            SetPlayerColor(player, Color.red);
            
            _remotePlayers[userId] = player;
        }

        /// <summary>
        /// Remove a remote player when they disconnect.
        /// </summary>
        private void DespawnRemotePlayer(string userId)
        {
            if (_remotePlayers.TryGetValue(userId, out var player))
            {
                Destroy(player);
                _remotePlayers.Remove(userId);
            }
        }

        /// <summary>
        /// Update remote player position (server-authoritative).
        /// </summary>
        private void UpdateRemotePlayerPosition(string playerId, Vector3 position)
        {
            // Spawn player if not exists (late join case)
            if (!_remotePlayers.ContainsKey(playerId))
            {
                SpawnRemotePlayer(playerId, $"Player_{playerId.Substring(0, 4)}");
            }

            if (_remotePlayers.TryGetValue(playerId, out var player))
            {
                player.transform.position = position;
            }
        }

        /// <summary>
        /// Set player visual color (Green = local, Red = remote).
        /// </summary>
        private void SetPlayerColor(GameObject player, Color color)
        {
            var spriteRenderer = player.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = color;
                return;
            }

            var meshRenderer = player.GetComponentInChildren<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.material.color = color;
            }
        }

        /// <summary>
        /// Send player movement to server.
        /// Called automatically every SEND_INTERVAL.
        /// </summary>
        public async void SendPlayerMove(Vector3 position, Quaternion rotation)
        {
            var session = multiplayer.GetSession();
            if (_protocol == null || session == null)
            {
                return;
            }

            // Nakama movement match expects: {"x": float32, "y": float32}
            var input = new MovementInputMessage
            {
                x = position.x,
                y = position.y
            };

            await _protocol.SendMessage(MatchOpcodes.PlayerMove, input);
        }

        public void LeaveMatch()
        {
            if (_localPlayer != null)
            {
                Destroy(_localPlayer);
                _localPlayer = null;
            }

            foreach (var pl in _remotePlayers)
            {
                if (pl.Value != null)
                {
                    Destroy(pl.Value);
                }
            }
            _remotePlayers.Clear();

            multiplayer.LeaveMatch();
            _protocol = null;
            _opcodesRegistered = false;
        }

        #endregion


        private void OnDestroy()
        {
            LeaveMatch();
        }
    }


    public class SimplePlayerController : MonoBehaviour
    {
        public float Speed = 5f;

        private static Vector2 ReadMoveInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float x = 0f;
                float y = 0f;

                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;

                return new Vector2(x, y);
            }

            return Vector2.zero;
        }

        private void Update()
        {
            Vector2 move = ReadMoveInput();
            transform.Translate(move.normalized * Speed * Time.deltaTime);
        }
    }

}
