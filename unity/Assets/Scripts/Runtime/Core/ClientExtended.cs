// using Nakama;
// using Cysharp.Threading.Tasks;
// using UnityEngine;
// using Runtime.Utilities.Result;
// using Runtime.Config.Client;
// using System;
// using System.Collections.Generic;
// using UnityEditor.Rendering;

// namespace Runtime.Core
// {
//     public class ClientExtended : IDisposable
//     {
//         public string Tag { get; }
//         public IClient Client { get; private set; }
//         public SessionFactory SessionFactory { get; private set; }
//         private readonly ClientConfig _clientConfig;
//         public bool _initialized;

//         public ClientExtended(string tag, ClientConfig clientConfig)
//         {
//             Tag = tag ?? throw new ArgumentNullException(nameof(tag));
//             _clientConfig = clientConfig ?? throw new ArgumentNullException(nameof(clientConfig));
//             SessionFactory = new SessionFactory();
//         }
//         public async UniTask<Result<ClientExtended>> InitAsync(string handshakeRPC = null)
//         {
//             if (_initialized) return Result<ClientExtended>.Ok(this);

//             try
//             {
//                 Debug.Log($"Initializing ClientExtended for tag={Tag}");

//                 Client = new Client(
//                     _clientConfig.scheme,
//                     _clientConfig.host,
//                     _clientConfig.port,
//                     _clientConfig.serverKey,
//                     UnityWebRequestAdapter.Instance,
//                     _clientConfig.autoRefreshSession
//                 );

//                 if (!string.IsNullOrEmpty(handshakeRPC))
//                 {
//                     var resp = await Client.RpcAsync("defaulthttpkey", handshakeRPC)
//                                    .Timeout(TimeSpan.FromSeconds(5));
//                     Debug.Log($"Handshake response: {resp?.Payload}");
//                 }

//                 _initialized = true;
//                 return Result<ClientExtended>.Ok(this);
//             }
//             catch (Exception ex)
//             {
//                 Debug.LogWarning($"InitAsync failed: {ex}");
//                 return Result<ClientExtended>.Fail(ex.Message);
//             }
//         }

//         public void Shutdown()
//         {
//             Debug.Log($"Shutting down ClientExtended for tag={Tag}");
//             // e.g., SessionFactory.CloseAll() or similar if you added that
//             _initialized = false;
//         }

//         public void Dispose()
//         {
//             Shutdown();
//         }

//         public async UniTask<string> CallRpcAsync(string httpKey, string rpcName)
//         {
//             if (Client == null) throw new InvalidOperationException("Client not initialized. Call InitAsync first.");
//             if (string.IsNullOrEmpty(httpKey)) throw new ArgumentNullException(nameof(httpKey));

//             var response = await Client.RpcAsync(httpKey, rpcName);
//             return response?.Payload;
//         }
//     }
// }


// // using System;
// // using Cysharp.Threading.Tasks;
// // using Nakama;
// // using theHesam.NakamaExtension.Runtime.Factory;
// // using theHesam.NakamaExtension.Runtime.NakamaConfig.ClientConfig;
// // using UnityEngine;

// // namespace Runtime.Core
// // {
// //     public class ClientExtended : IDisposable
// //     {
// //         public string Tag { get; }
// //         public IClient Client { get; private set; }
// //         public SessionFactory SessionFactory { get; private set; }
// //         private readonly ServerClientConfigs _clientConfig;
// //         private bool _initialized;

// //         public ClientExtended(string tag, ServerClientConfigs clientConfig)
// //         {
// //             Tag = tag ?? throw new ArgumentNullException(nameof(tag));
// //             _clientConfig = clientConfig ?? throw new ArgumentNullException(nameof(clientConfig));
// //             SessionFactory = new SessionFactory();
// //         }

// //         // Initialize the Nakama client. Optional handshakeRpcName lets you validate network.
// //         public async UniTask<(bool success, string error)> InitAsync(string handshakeRpcName = null)
// //         {
// //             if (_initialized) return (true, null);

// //             try
// //             {
// //                 Debug.Log($"Initializing ClientExtended for tag={Tag}");

// //                 Client = new Client(
// //                     _clientConfig.scheme,
// //                     _clientConfig.host,
// //                     _clientConfig.port,
// //                     _clientConfig.serverKey,
// //                     UnityWebRequestAdapter.Instance,
// //                     _clientConfig.autoRefreshSession
// //                 );

// //                 // optional handshake RPC to verify connectivity/auth
// //                 if (!string.IsNullOrEmpty(handshakeRpcName))
// //                 {
// //                     var resp = await Client.RpcAsync("defaulthttpkey", handshakeRpcName)
// //                                            .Timeout(TimeSpan.FromSeconds(5));
// //                     Debug.Log($"Handshake response: {resp?.Payload}");
// //                 }

// //                 _initialized = true;
// //                 return (true, null);
// //             }
// //             catch (Exception ex)
// //             {
// //                 Debug.LogError($"ClientExtended.InitAsync failed: {ex.Message}");
// //                 return (false, ex.Message);
// //             }
// //         }

// //         public void Shutdown()
// //         {
// //             // If you have sockets or other disposable resources, dispose/close them here.
// //             Debug.Log($"Shutting down ClientExtended for tag={Tag}");
// //             // e.g., SessionFactory.CloseAll() or similar if you added that
// //             _initialized = false;
// //         }

// //         public void Dispose()
// //         {
// //             Shutdown();
// //             // additional dispose if required
// //         }

// //         public async UniTask<string> CallRpcAsync(string httpKey, string rpcName)
// //         {
// //             if (Client == null) throw new InvalidOperationException("Client not initialized. Call InitAsync first.");
// //             if (string.IsNullOrEmpty(httpKey)) throw new ArgumentNullException(nameof(httpKey));

// //             var response = await Client.RpcAsync(httpKey, rpcName);
// //             return response?.Payload;
// //         }
// //     }
// // }
