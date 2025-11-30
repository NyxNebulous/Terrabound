// // Runtime/Core/SessionExtended.cs
// using System;
// using Cysharp.Threading.Tasks;
// using Nakama;
// using Runtime.Core;              
// using Runtime.Config.SessionConfig;
// using Runtime.Core;
// using Runtime.Utilities.Result;
// using UnityEngine;

// namespace Runtime.Core
// {
//     public class SessionExtended : IDisposable
//     {
//         public string Tag { get; private set; }
//         public ISession Session { get; private set; }
//         public SocketFactory SocketFactory { get; private set; }
//         public SessionConfig SessionConfig { get; private set; }
//         public ClientExtended ClientExtended { get; private set; }
//         public SessionConnectionController SessionConnectionController { get; private set; }

//         private bool _initialized;
//         private bool _disposed;

//         public SessionExtended()
//         {
//             // SocketFactory = new SocketFactory();
//         }

//         public async UniTask<Result<SessionExtended>> CreateSessionAsync<T>(
//             string tag,
//             ClientExtended clientExtended,
//             T sessionConfig,
//             int timeoutMs = 10000) where T : SessionConfig
//         {
//             if (_disposed) return Result<SessionExtended>.Fail("SessionExtended is disposed.");
//             if (clientExtended == null) return Result<SessionExtended>.Fail("clientExtended is null.");
//             if (sessionConfig == null) return Result<SessionExtended>.Fail("sessionConfig is null.");
//             if (_initialized && Tag == tag) return Result<SessionExtended>.Ok(this);

//             try
//             {
//                 Tag = tag;
//                 ClientExtended = clientExtended;
//                 SessionConfig = sessionConfig;

//                 SocketFactory = new SocketFactory();

//                 if (sessionConfig is SessionConfigDevice deviceCfg)
//                 {
//                     Session = await clientExtended.Client
//                             .AuthenticateDeviceAsync(deviceCfg.UniqueIdentifier)
//                             .Timeout(TimeSpan.FromMilliseconds(timeoutMs));
//                 }
//                 else if (sessionConfig is SessionConfigEmail emailCfg)
//                 {
//                     Session = await clientExtended.Client
//                             .AuthenticateEmailAsync(emailCfg.username, emailCfg.password)
//                             .Timeout(TimeSpan.FromMilliseconds(timeoutMs));
//                 }
//                 else
//                 {
//                     return Result<SessionExtended>.Fail($"Unsupported session config type: {sessionConfig.GetType().Name}");
//                 }

//                 if (Session == null)
//                     return Result<SessionExtended>.Fail("Authentication returned null session.");

//                 _initialized = true;
//                 return Result<SessionExtended>.Ok(this);
//             }
//             catch (Exception ex)
//             {
//                 Debug.LogWarning($"CreateSessionAsync failed for tag={tag}: {ex.Message}");
//                 return Result<SessionExtended>.Fail(ex.Message);
//             }
//         }

//         public async UniTask<Result<SocketExtended>> InitSocketAsync(int connectTimeoutMs = 10000)
//         {
//             if (_disposed) return Result<SocketExtended>.Fail("SessionExtended is disposed.");
//             if (!_initialized) return Result<SocketExtended>.Fail("SessionExtended not initialized. Call CreateSessionAsync first.");
//             if (ClientExtended == null || ClientExtended.Client == null) return Result<SocketExtended>.Fail("Client not initialized.");

//             try
//             {
//                 var socketResult = await SocketFactory.CreateSocketAsync(ClientExtended.Client, Session);
//                 if (!socketResult.Success) return Result<SocketExtended>.Fail(socketResult.Error);

//                 var socket = socketResult.Value;

//                 await socket.ConnectAsync().Timeout(TimeSpan.FromMilliseconds(connectTimeoutMs));


//                 return Result<SocketExtended>.Ok(socket);
//             }
//             catch (Exception ex)
//             {
//                 UnityEngine.Debug.LogWarning($"InitSocketAsync failed for tag={Tag}: {ex.Message}");
//                 return Result<SocketExtended>.Fail(ex.Message);
//             }
//         }

//         public void SetSessionConnectionController(SessionConnectionController controller)
//         {
//             SessionConnectionController = controller;
//         }
//         public async UniTask<SessionExtended> CreateSessionAsyncOrThrow<T>(string tag, ClientExtended clientExtended, T sessionConfig, int timeoutMs = 10000)
//             where T : SessionConfig
//         {
//             var res = await CreateSessionAsync(tag, clientExtended, sessionConfig, timeoutMs);
//             if (!res.Success) throw new InvalidOperationException(res.Error ?? "CreateSessionAsync failed");
//             return res.Value;
//         }

//         public void Dispose()
//         {
//             if (_disposed) return;
//             _disposed = true;

//             try
//             {
//                 try
//                 {
//                     SessionConnectionController?.Dispose();
//                 }
//                 catch (Exception ex)
//                 {
//                     UnityEngine.Debug.LogWarning($"Exception while disposing SessionConnectionController: {ex.Message}");
//                 }

//                 try
//                 {
//                     SocketFactory?.Dispose();
//                 }
//                 catch (Exception ex)
//                 {
//                     UnityEngine.Debug.LogWarning($"Exception while disposing SocketFactory: {ex.Message}");
//                 }

//                 Session = null;
//                 ClientExtended = null;
//                 SessionConfig = null;
//                 Tag = null;
//             }
//             catch (Exception ex)
//             {
//                 UnityEngine.Debug.LogWarning($"SessionExtended.Dispose exception: {ex.Message}");
//             }
//         }
//     }
// }
