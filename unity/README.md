# Unity Client Notes

## Folder Layout
```
unity/
├── Assets/
│   └── Scripts/
│       ├── Networking/
│       │   └── GameClient.cs
│       ├── GameLogic/
│       └── UI/
└── ProjectSettings/
```

* **Networking/** — low-level Nakama connectivity: authentication, matchmaking, RPC helpers.
* **GameLogic/** — deterministic simulation layer that mirrors the Go domain types for prediction.
* **UI/** — presentation and input bindings (keep networking calls out of UI code).

## Quickstart
1. Install the [Nakama Unity/ .NET client](https://github.com/heroiclabs/nakama-dotnet) via the Unity Package Manager or by dropping the .unitypackage.
2. Add `GameClient` to a bootstrap scene and plug the inspector fields (server address, port).
3. Call `await GameClient.Instance.ConnectAndAuthenticate(deviceId)` during your scene load.
4. Use `GameClient.Instance.ValidateOrderAsync(...)` to talk to the sample RPC built into the backend.
5. Use `GameClient.Instance.JoinAuthoritativeMatchAsync()` to connect to the server-side match handler stub.

Refer to `GameClient.cs` for minimal sample code you can expand.
