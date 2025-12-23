using System;
using System.Collections.Generic;
using System.Text;
using Nakama;
using Nakama.TinyJson;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Terrabound.Runtime.Protocol
{
    /// <summary>
    /// LAYER 3: Network / Protocol Layer - Match Protocol
    /// 
    /// This handles:
    /// - Opcode registration and dispatch
    /// - Message serialization
    /// - Network event handling
    /// - Protocol correctness
    /// </summary>
    public class MatchProtocol : IDisposable
    {
        private readonly ISocket _socket;
        private readonly string _matchId;
        private readonly Dictionary<long, Action<IMatchState>> _opcodeHandlers = new();
        private readonly Dictionary<string, long> _opcodeKeyMap = new();
        private bool _isListening;
        private bool _disposed;

        public event Action<IMatchPresenceEvent> OnMatchPresence;

        public MatchProtocol(ISocket socket, string matchId)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _matchId = matchId ?? throw new ArgumentNullException(nameof(matchId));
        }

        public void StartListening()
        {
            if (_isListening) return;

            _socket.ReceivedMatchState += HandleMatchState;
            _socket.ReceivedMatchPresence += HandleMatchPresence;
            _isListening = true;

            Debug.Log("[MatchProtocol] Started listening to match events.");
        }

        public void StopListening()
        {
            if (!_isListening) return;

            _socket.ReceivedMatchState -= HandleMatchState;
            _socket.ReceivedMatchPresence -= HandleMatchPresence;
            _isListening = false;

            Debug.Log("[MatchProtocol] Stopped listening to match events.");
        }

        public long RegisterOpcode(string key, Action<IMatchState> handler)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Opcode key cannot be null or empty.", nameof(key));

            long opcode = unchecked((long)key.GetHashCode());

            if (_opcodeHandlers.ContainsKey(opcode))
            {
                Debug.LogWarning($"[MatchProtocol] Opcode {opcode} (key: {key}) already registered. Overwriting.");
            }

            _opcodeHandlers[opcode] = handler;
            _opcodeKeyMap[key] = opcode;

            Debug.Log($"[MatchProtocol] Registered opcode: {key} → {opcode}");
            return opcode;
        }

        public void RegisterOpcodeExplicit(string key, long opcode, Action<IMatchState> handler)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Opcode key cannot be null or empty.", nameof(key));

            if (_opcodeHandlers.ContainsKey(opcode))
            {
                Debug.LogWarning($"[MatchProtocol] Opcode {opcode} (key: {key}) already registered. Overwriting.");
            }

            _opcodeHandlers[opcode] = handler;
            _opcodeKeyMap[key] = opcode;

            Debug.Log($"[MatchProtocol] Registered explicit opcode: {key} → {opcode}");
        }

        public void UnregisterOpcode(string key)
        {
            if (_opcodeKeyMap.TryGetValue(key, out long opcode))
            {
                _opcodeHandlers.Remove(opcode);
                _opcodeKeyMap.Remove(key);
                Debug.Log($"[MatchProtocol] Unregistered opcode: {key}");
            }
        }

        public void ClearHandlers()
        {
            _opcodeHandlers.Clear();
            _opcodeKeyMap.Clear();
            Debug.Log("[MatchProtocol] Cleared all opcode handlers.");
        }

        public async UniTask<bool> SendMessage<T>(string opcodeKey, T message)
        {
            if (!_opcodeKeyMap.TryGetValue(opcodeKey, out long opcode))
            {
                Debug.LogError($"[MatchProtocol] Opcode key '{opcodeKey}' not registered. Call RegisterOpcode first.");
                return false;
            }

            return await SendMessageByOpcode(opcode, message);
        }

        public async UniTask<bool> SendMessageByOpcode<T>(long opcode, T message)
        {
            if (_disposed || _socket == null)
            {
                Debug.LogError("[MatchProtocol] Cannot send: Protocol is disposed or socket is null.");
                return false;
            }

            try
            {
                var json = message.ToJson();
                var data = Encoding.UTF8.GetBytes(json);

                await _socket.SendMatchStateAsync(_matchId, opcode, data);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MatchProtocol] SendMessageByOpcode failed: {ex.Message}");
                return false;
            }
        }

        public async UniTask<bool> SendRawBytes(long opcode, byte[] data)
        {
            if (_disposed || _socket == null)
            {
                Debug.LogError("[MatchProtocol] Cannot send: Protocol is disposed or socket is null.");
                return false;
            }

            try
            {
                await _socket.SendMatchStateAsync(_matchId, opcode, data);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MatchProtocol] SendRawBytes failed: {ex.Message}");
                return false;
            }
        }

        private void HandleMatchState(IMatchState state)
        {
            if (_opcodeHandlers.TryGetValue(state.OpCode, out var handler))
            {
                try
                {
                    handler?.Invoke(state);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MatchProtocol] Handler error for OpCode {state.OpCode}: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"[MatchProtocol] Received unhandled OpCode: {state.OpCode}");
            }
        }

        private void HandleMatchPresence(IMatchPresenceEvent presence)
        {
            OnMatchPresence?.Invoke(presence);
        }

        public long? GetOpcode(string key) => _opcodeKeyMap.TryGetValue(key, out long opcode) ? opcode : null;
        public bool IsOpcodeRegistered(string key) => _opcodeKeyMap.ContainsKey(key);
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            StopListening();
            ClearHandlers();
        }
    }
}