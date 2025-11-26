//sample code

// using System;
// using System.Collections.Generic;
// using System.Threading.Tasks;
// using Nakama;
// using Nakama.TinyJson;
// using Nakama.Unity;
// using UnityEngine;

// namespace Terrabound.Client.Networking
// {
//     /// <summary>
//     /// Thin convenience wrapper around the Nakama C# client for gameplay scripts.
//     /// Keeps networking concerns outside of MonoBehaviours that contain UI/gameplay logic.
//     /// </summary>
//     public class GameClient : MonoBehaviour
//     {
//         [SerializeField] private string scheme = "http";
//         [SerializeField] private string host = "127.0.0.1";
//         [SerializeField] private int port = 7350;
//         [SerializeField] private string serverKey = "defaultkey";

//         public static GameClient Instance { get; private set; }

//         private IClient _client;
//         private ISession _session;
//         private ISocket _socket;

//         private void Awake()
//         {
//             if (Instance != null && Instance != this)
//             {
//                 Destroy(gameObject);
//                 return;
//             }
//             Instance = this;
//             DontDestroyOnLoad(gameObject);
//             _client = new Client(scheme, host, port, serverKey, UnityWebRequestAdapter.Instance);
//         }

//         public async Task ConnectAndAuthenticateAsync(string deviceId)
//         {
//             _session = await _client.AuthenticateDeviceAsync(deviceId, create: true, username: deviceId);
//             _socket = _client.NewSocket();
//             await _socket.ConnectAsync(_session, new Dictionary<string, string>());
//         }

//         public async Task<string> ValidateOrderAsync(string matchId, string target, int units)
//         {
//             var payload = new
//             {
//                 matchId,
//                 target,
//                 units,
//                 action = "attack"
//             };
//             var response = await _client.RpcAsync(_session, "tb_order_validate", payload.ToJson());
//             return response.Payload;
//         }

//         public async Task<IMatch> JoinAuthoritativeMatchAsync()
//         {
//             if (_socket == null)
//             {
//                 throw new InvalidOperationException("Call ConnectAndAuthenticateAsync first");
//             }
//             return await _socket.JoinMatchAsync("tb_war_room");
//         }
//     }
// }
