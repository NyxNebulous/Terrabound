using UnityEngine;
using Cysharp.Threading.Tasks;
using Terrabound.Runtime.Flow;
using UnityEngine.SceneManagement;

namespace Terrabound.Runtime
{
    /// <summary>
    /// LAYER 1: Unity Entry / UI Layer
    /// 
    /// This is main entry point for multiplayer.
    /// Attach this to a GameObject and call methods.
    /// </summary>
    public class TerraboundMultiplayer : MonoBehaviour
    {
        public static TerraboundMultiplayer Instance { get; private set; }

        [Header("Server Configuration")]
        [SerializeField, Tooltip("Nakama server scheme (http/https)")]
        private string serverScheme = "http";

        [SerializeField, Tooltip("Nakama server host")]
        private string serverHost = "localhost";

        [SerializeField, Tooltip("Nakama server port")]
        private int serverPort = 7350;

        [SerializeField, Tooltip("Nakama server key")]
        private string serverKey = "defaultkey";

        [Header("Match Configuration")]
        [SerializeField, Tooltip("RPC function name to request dynamic match")]
        private string matchRpcName = "dynamic_match";

        [Header("Runtime State (Read Only)")]
        [SerializeField, Tooltip("Is currently authenticated?")]
        private bool isAuthenticated;

        [SerializeField, Tooltip("Is currently in a match?")]
        private bool isInMatch;

        private AuthFlow _authFlow;
        private MatchFlow _matchFlow;

        private string _currentMatchId;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _authFlow = new AuthFlow(serverScheme, serverHost, serverPort, serverKey);
            _matchFlow = new MatchFlow();
        }

        public async void AuthenticateWithDevice()
        {
            await AuthenticateWithDeviceAsync();
        }
        public async void AuthenticateWithGoogle()
        {
            await AuthenticateWithAuthAsync("google");
        }
        public async void AuthenticateWithDAuth()
        {
            await AuthenticateWithAuthAsync("dauth");
        }
        public async void JoinMatch()
        {
            await JoinMatchAsync();
        }
        public async void LeaveMatch()
        {
            await LeaveMatchAsync();
        }

        private async UniTask AuthenticateWithDeviceAsync()
        {
            if (isAuthenticated)
            {
                Debug.LogWarning("[TerraboundMultiplayer] Already authenticated.");
                return;
            }

            Debug.Log("[TerraboundMultiplayer] Authenticating with device...");

            var result = await _authFlow.AuthenticateWithDeviceAsync();

            if (result.Success)
            {
                isAuthenticated = true;
                Debug.Log("[TerraboundMultiplayer] ✓ Device authentication successful!");
            }
            else
            {
                Debug.LogError($"[TerraboundMultiplayer] ✗ Device authentication failed: {result.Error}");
            }
        }

        private async UniTask AuthenticateWithAuthAsync(string provider)
        {
            if (isAuthenticated)
            {
                Debug.LogWarning("[TerraboundMultiplayer] Already authenticated.");
                return;
            }

            Debug.Log($"[TerraboundMultiplayer] Authenticating with {provider}...");
            Debug.Log("[TerraboundMultiplayer] Please complete sign-in in your browser.");

            var result = await _authFlow.AuthenticateWithAuthAsync(provider);

            if (result.Success)
            {
                isAuthenticated = true;
                Debug.Log($"[TerraboundMultiplayer] ✓ {provider} authentication successful!");
            }
            else
            {
                Debug.LogError($"[TerraboundMultiplayer] ✗ {provider} authentication failed: {result.Error}");
            }
        }
        private async UniTask JoinMatchAsync()
        {
            if (!isAuthenticated)
            {
                Debug.LogError("[TerraboundMultiplayer] Cannot join match: Not authenticated. Call AuthenticateWithDevice() or AuthenticateWithGoogle() first.");
                return;
            }

            if (isInMatch)
            {
                Debug.LogWarning("[TerraboundMultiplayer] Already in a match.");
                return;
            }

            Debug.Log("[TerraboundMultiplayer] Joining match...");

            var result = await _matchFlow.JoinDynamicMatchAsync(
                _authFlow.GetClient(),
                _authFlow.GetSession(),
                matchRpcName
            );

            if (result.Success)
            {
                isInMatch = true;
                _currentMatchId = result.Value.matchId;
                Debug.Log($"[TerraboundMultiplayer] ✓ Joined match: {_currentMatchId}");

                await SceneManager.LoadSceneAsync("Scene2", LoadSceneMode.Single).ToUniTask();
            }
            else
            {
                Debug.LogError($"[TerraboundMultiplayer] ✗ Failed to join match: {result.Error}");
            }
        }

        private async UniTask LeaveMatchAsync()
        {
            if (!isInMatch)
            {
                Debug.LogWarning("[TerraboundMultiplayer] Not in a match.");
                return;
            }

            Debug.Log("[TerraboundMultiplayer] Leaving match...");

            var result = await _matchFlow.LeaveMatchAsync();

            if (result.Success)
            {
                isInMatch = false;
                _currentMatchId = null;
                Debug.Log("[TerraboundMultiplayer] ✓ Left match successfully.");
            }
            else
            {
                Debug.LogError($"[TerraboundMultiplayer] ✗ Failed to leave match: {result.Error}");
            }
        }

        public MatchFlow GetMatchFlow() => _matchFlow;
        public Nakama.ISession GetSession() => _authFlow.GetSession();
        public bool IsAuthenticated() => isAuthenticated;
        public bool IsInMatch() => isInMatch;
        public string GetCurrentMatchId() => _currentMatchId;

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            _authFlow?.Dispose();
            _matchFlow?.Dispose();
        }
    }
}
