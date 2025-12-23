using System;
using System.Text;
using Cysharp.Threading.Tasks;
using Nakama;
using Terrabound.Runtime.Utilities;
using UnityEngine;
using UnityEngine.Networking;
// using Google;

namespace Terrabound.Runtime.Flow
{
    /// <summary>
    /// LAYER 2: Flow / Coordinator Layer - Authentication Flow
    /// 
    /// Handles authentication sequencing:
    /// 1. Create Nakama client
    /// 2. Authenticate (device or Google)
    /// 
    /// DO NOT expose Nakama concepts to UI.
    /// </summary>
    public class AuthFlow : IDisposable
    {
        private IClient _client;
        private ISession _session;
        private readonly string _scheme;
        private readonly string _host;
        private readonly int _port;
        private readonly string _serverKey;
        private bool _disposed;

        private const float POLL_INTERVAL_SECONDS = 1.2f;
        private const int MAX_POLLS = 120; // ~2.5 minutes

        private const string SESSION_TOKEN_PREF_KEY = "nakama.session";

        public AuthFlow(string scheme, string host, int port, string serverKey)
        {
            _scheme = scheme;
            _host = host;
            _port = port;
            _serverKey = serverKey;
            if (_client == null)
            {
                _client = new Client(
                    _scheme,
                    _host,
                    _port,
                    _serverKey,
                    UnityWebRequestAdapter.Instance
                );
                Debug.Log("[AuthFlow] Nakama client created.");
            }
        }

        [Serializable] private class AuthInitReq { public string provider; }
        [Serializable] private class AuthInitResp { public bool success; public string state; public string url; public string message; }
        [Serializable] private class AuthCheckReq { public string state; }
        [Serializable] private class AuthCheckResp { public bool success; public bool ready; public string customId; public string username; public string email; public string message; }

        public async UniTask<Result<ISession>> AuthenticateWithDeviceAsync()
        {
            if (_disposed) return Result<ISession>.Fail("AuthFlow is disposed.");

            try
            {
                if (await TryRestoreSessionAsync())
                {
                    Debug.Log($"[AuthFlow] Restored session for: {_session.UserId}");
                    return Result<ISession>.Ok(_session);
                }

                var deviceId = PlayerPrefs.GetString("deviceId", string.Empty);

                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    deviceId = SystemInfo.deviceUniqueIdentifier;
                    if (string.IsNullOrWhiteSpace(deviceId) || deviceId == "unknown")
                        deviceId = Guid.NewGuid().ToString("N");

                    PlayerPrefs.SetString("deviceId", deviceId);
                    PlayerPrefs.Save();
                }

                _session = await _client.AuthenticateDeviceAsync(deviceId, create: true);

                if (_session == null)
                    return Result<ISession>.Fail("Authentication returned null session.");

                SaveSessionToken(_session);

                Debug.Log($"[AuthFlow] Authenticated as: {_session.UserId}");
                return Result<ISession>.Ok(_session);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AuthFlow] Device authentication failed: {ex.Message}");
                return Result<ISession>.Fail(ex.Message);
            }
        }

        public async UniTask<Result<ISession>> AuthenticateWithAuthAsync(string provider)
        {
            if (_disposed) return Result<ISession>.Fail("AuthFlow is disposed.");

            try
            {
                if (await TryRestoreSessionAsync())
                {
                    Debug.Log($"[AuthFlow] Restored session for: {_session.UserId}");
                    return Result<ISession>.Ok(_session);
                }

                var initUrl = $"{_scheme}://{_host}:{_port}/auth/init";
                var initReq = new AuthInitReq { provider = provider };
                var initResp = await PostJsonAsync<AuthInitResp>(initUrl, JsonUtility.ToJson(initReq));

                if (initResp == null || !initResp.success || string.IsNullOrEmpty(initResp.url) || string.IsNullOrEmpty(initResp.state))
                {
                    var errorMsg = initResp?.message ?? "No response from auth/init";
                    Debug.LogError($"[AuthFlow] OAuth init failed: {errorMsg}");
                    return Result<ISession>.Fail($"OAuth init failed: {errorMsg}");
                }

                Debug.Log($"[AuthFlow] Opening browser for {provider} sign-in...");
                Application.OpenURL(initResp.url);

                // Step 3: Poll auth/check until ready
                var checkUrl = $"{_scheme}://{_host}:{_port}/auth/check";
                AuthCheckResp checkResp = null;

                for (int i = 0; i < MAX_POLLS; i++)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(POLL_INTERVAL_SECONDS));

                    var checkReq = new AuthCheckReq { state = initResp.state };
                    checkResp = await PostJsonAsync<AuthCheckResp>(checkUrl, JsonUtility.ToJson(checkReq));

                    if (checkResp != null && checkResp.success && checkResp.ready && !string.IsNullOrEmpty(checkResp.customId))
                    {
                        Debug.Log($"[AuthFlow] OAuth authentication ready (poll {i + 1}/{MAX_POLLS})");
                        break;
                    }

                    // Log progress every 10 polls
                    if ((i + 1) % 10 == 0)
                    {
                        Debug.Log($"[AuthFlow] Waiting for OAuth completion... ({i + 1}/{MAX_POLLS})");
                    }
                }

                if (checkResp == null || !checkResp.ready || string.IsNullOrEmpty(checkResp.customId))
                {
                    Debug.LogError("[AuthFlow] OAuth polling timed out or failed.");
                    return Result<ISession>.Fail("OAuth authentication timed out. Please try again.");
                }

                // Step 4: Authenticate with Nakama using Custom ID
                _session = await _client.AuthenticateCustomAsync(checkResp.customId, create: true);

                if (_session == null)
                    return Result<ISession>.Fail("Nakama authentication returned null session.");

                SaveSessionToken(_session);

                Debug.Log($"[AuthFlow] Authenticated via {provider} as: {_session.UserId}");
                if (!string.IsNullOrEmpty(checkResp.email))
                {
                    Debug.Log($"[AuthFlow] Email: {checkResp.email}");
                }

                return Result<ISession>.Ok(_session);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AuthFlow] OAuth authentication failed: {ex.Message}");
                return Result<ISession>.Fail(ex.Message);
            }
        }

        public async UniTask<Result<ISession>> LinkWithGoogleAsync(ISession session, string token)
        {
            try
            {
                await _client.LinkGoogleAsync(session, token);
                return Result<ISession>.Ok(_session);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AuthFlow] Google link failed: {ex.Message}");
                return Result<ISession>.Fail(ex.Message);
            }
        }


        public IClient GetClient() => _client;
        public ISession GetSession() => _session;
        private static async UniTask<T> PostJsonAsync<T>(string url, string jsonBody) where T : class
        {
            using (var req = new UnityWebRequest(url, "POST"))
            {
                var bodyRaw = Encoding.UTF8.GetBytes(jsonBody ?? "{}");
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                await req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                    throw new Exception($"HTTP POST {url} failed: {req.responseCode} {req.error}\n{req.downloadHandler.text}");

                var text = req.downloadHandler.text;
                if (string.IsNullOrWhiteSpace(text)) return null;

                try
                {
                    return JsonUtility.FromJson<T>(text);
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed parsing JSON from {url}: {e}\n{text}");
                }
            }
        }

        private void SaveSessionToken(ISession session)
        {
            if (session == null || string.IsNullOrWhiteSpace(session.AuthToken))
            {
                Debug.LogWarning("[AuthFlow] Not saving session token: session/authToken missing.");
                return;
            }
            PlayerPrefs.SetString(SESSION_TOKEN_PREF_KEY, session.AuthToken);
            PlayerPrefs.Save();
            Debug.Log("[AuthFlow] Saved session auth token.");
        }

        private void ClearSavedSessionToken()
        {
            if (PlayerPrefs.HasKey(SESSION_TOKEN_PREF_KEY))
            {
                PlayerPrefs.DeleteKey(SESSION_TOKEN_PREF_KEY);
                PlayerPrefs.Save();
                Debug.Log("[AuthFlow] Cleared saved session auth token.");
            }
        }

        private async UniTask<bool> TryRestoreSessionAsync()
        {
            var authToken = PlayerPrefs.GetString(SESSION_TOKEN_PREF_KEY, string.Empty);
            if (string.IsNullOrWhiteSpace(authToken))
            {
                Debug.Log("[AuthFlow] No saved session token found.");
                return false;
            }

            Debug.Log("[AuthFlow] Found saved session token. Attempting restore...");

            ISession restored;
            try
            {
                restored = Session.Restore(authToken);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AuthFlow] Failed to restore session from token: {ex.Message}");
                ClearSavedSessionToken();
                return false;
            }

            if (restored == null || restored.IsExpired)
            {
                Debug.LogWarning("[AuthFlow] Restored session is null or expired. Reauth required.");
                ClearSavedSessionToken();
                return false;
            }

            try
            {
                await _client.GetAccountAsync(restored);
                _session = restored;
                Debug.Log("[AuthFlow] Restored session validated successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AuthFlow] Restored session validation failed. Reauth required. Reason: {ex.Message}");
                ClearSavedSessionToken();
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _client = null;
            _session = null;
        }
    }
}
