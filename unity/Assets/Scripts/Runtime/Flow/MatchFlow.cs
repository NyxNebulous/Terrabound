using System;
using Cysharp.Threading.Tasks;
using Nakama;
using Nakama.TinyJson;
using Terrabound.Runtime.Protocol;
using Terrabound.Runtime.Utilities;
using UnityEngine;

namespace Terrabound.Runtime.Flow
{
    /// <summary>
    /// LAYER 2: Flow / Coordinator Layer - Match Flow
    /// 
    /// Handles match sequencing:
    /// 1. Connect socket
    /// 2. Request match from backend (RPC)
    /// 3. Join match
    /// 4. Setup opcode handlers
    /// 5. Handle disconnection
    /// </summary>
    public class MatchFlow : IDisposable
    {
        private ISocket _socket;
        private IMatch _currentMatch;
        private string _currentMatchId;
        private MatchProtocol _protocol;
        private bool _disposed;

        public async UniTask<Result<MatchJoinResult>> JoinDynamicMatchAsync(
            IClient client,
            ISession session,
            string rpcName,
            string payloadJson = null)
        {
            if (_disposed) return Result<MatchJoinResult>.Fail("MatchFlow is disposed.");
            if (client == null) return Result<MatchJoinResult>.Fail("Client is null.");
            if (session == null) return Result<MatchJoinResult>.Fail("Session is null.");

            try
            {
                if (_socket == null)
                {
                    _socket = client.NewSocket(useMainThread: true);
                    Debug.Log("[MatchFlow] Socket created.");
                }

                if (!_socket.IsConnected)
                {
                    await _socket.ConnectAsync(session, appearOnline: true);
                    Debug.Log("[MatchFlow] Socket connected.");
                }

                var rpcPayload = string.IsNullOrEmpty(payloadJson) ? "{}" : payloadJson;
                var rpcResponse = await client.RpcAsync(session, rpcName, rpcPayload);

                if (string.IsNullOrEmpty(rpcResponse?.Payload))
                    return Result<MatchJoinResult>.Fail("RPC returned empty response.");

                var matchData = rpcResponse.Payload.FromJson<DynamicMatchResponse>();
                
                if (matchData == null || string.IsNullOrEmpty(matchData.matchId))
                    return Result<MatchJoinResult>.Fail("RPC did not return a matchId.");

                _currentMatchId = matchData.matchId;
                Debug.Log($"[MatchFlow] Backend assigned match: {_currentMatchId}");

                _currentMatch = await _socket.JoinMatchAsync(_currentMatchId);
                
                if (_currentMatch == null)
                    return Result<MatchJoinResult>.Fail("JoinMatchAsync returned null.");

                Debug.Log($"[MatchFlow] Joined match successfully.");

                _protocol = new MatchProtocol(_socket, _currentMatchId);
                _protocol.StartListening();

                var result = new MatchJoinResult
                {
                    matchId = _currentMatchId,
                    serverTime = matchData.serverTime,
                    minElo = matchData.minElo,
                    maxElo = matchData.maxElo,
                    currentPlayers = matchData.currentPlayers,
                    maxPlayers = matchData.maxPlayers
                };

                return Result<MatchJoinResult>.Ok(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MatchFlow] JoinDynamicMatchAsync failed: {ex.Message}");
                return Result<MatchJoinResult>.Fail(ex.Message);
            }
        }
        public async UniTask<Result<bool>> LeaveMatchAsync()
        {
            if (_disposed) return Result<bool>.Fail("MatchFlow is disposed.");
            if (_socket == null || string.IsNullOrEmpty(_currentMatchId))
                return Result<bool>.Fail("Not in a match.");

            try
            {
                await _socket.LeaveMatchAsync(_currentMatchId);
                
                _protocol?.StopListening();
                _protocol?.Dispose();
                _protocol = null;

                _currentMatch = null;
                _currentMatchId = null;

                Debug.Log("[MatchFlow] Left match.");
                return Result<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MatchFlow] LeaveMatchAsync failed: {ex.Message}");
                return Result<bool>.Fail(ex.Message);
            }
        }
        public MatchProtocol GetProtocol() => _protocol;
        public bool IsInMatch() => !string.IsNullOrEmpty(_currentMatchId);
        public string GetMatchId() => _currentMatchId;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _protocol?.Dispose();
            _protocol = null;
            _socket = null;
            _currentMatch = null;
            _currentMatchId = null;
        }
    }

}
