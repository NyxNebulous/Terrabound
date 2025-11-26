# Terrabound Dev Stack

**Terrabound** is a multiplayer strategy/war game inspired by Supremacy 1914, built with authoritative server logic for real-time or near-real-time interactions. This repository provides a complete development setup for the backend (Go + Nakama), client (Unity + C#), and infrastructure (Docker + Postgres).

## Project Overview

- **Backend**: Go runtime plugins for Nakama server, handling authoritative multiplayer matches, RPCs, and game state.
- **Client**: Unity project with C# scripts for networking, authentication, and gameplay.
- **Infrastructure**: Docker Compose for local Nakama + Postgres stack, plus tooling for fast iteration.
- **Goal**: Clean architecture separating domain logic from Nakama integration, with Unity client that can iterate quickly against the server.

## Repository Layout

```
.
├── backend/                 # Go module compiled into backend.so
│   ├── cmd/module/          # Nakama entrypoint delegating to internal packages
│   ├── internal/game/       # Pure domain logic (deterministic + unit-testable)
│   └── internal/nakama/     # Thin integration layer (RPCs, matches, hooks)
├── unity/                   # Unity scaffolding + networking sample script
│   ├── Assets/Scripts/      # Unity scripts (Networking, GameLogic, UI)
│   └── README.md            # Unity-specific notes
├── infra/                   # Docker Compose, Nakama config, Postgres init script
│   ├── config/local.yml     # Nakama server configuration
│   ├── docker-compose.yml   # Services: Postgres + Nakama
│   └── postgres/init.sql    # Database seed script
├── Taskfile.yml             # Single entry point for builds, watch mode, stack control
├── .devcontainer/           # VS Code Dev Container for consistent tooling
├── .gitignore               # Excludes build artifacts, IDE files, Unity output
└── README.md                # You're here
```

- **backend/**: Standalone Go module (go.mod) with pure game logic in `internal/game/` and Nakama glue in `internal/nakama/`. Built as a `.so` plugin.
- **unity/**: Unity project structure with scripts for connecting to Nakama.
- **infra/**: Local dev environment via Docker Compose. Nakama runs the Go plugin, Postgres stores data.
- **Taskfile.yml**: Task runner for all workflows (build, run, watch, logs, etc.).

## Tooling Philosophy

- **Docker + Compose**: Ensures Nakama + Postgres parity with production. No local installs needed.
- **heroiclabs/nakama-pluginbuilder**: Official builder image for compiling Go plugins with exact toolchain/CGO settings.
- **Taskfile**: Wraps all commands (build/watch/run) so you remember one tool: `task <target>`.
- **reflex**: Optional hot-reload watcher for Go files (rebuild + restart Nakama on changes).
- **Devcontainer**: VS Code setup with Go 1.21, Docker CLI, Task, and reflex preinstalled.

## Prerequisites

- **Docker** (24+) and **Docker Compose v2** (for `docker compose` commands).
- **Go 1.25+** (matches the module's `go 1.25.0` target; only needed if running tests outside the builder image).
- **Task** (`go install github.com/go-task/task/v3/cmd/task@latest`).
- **reflex** for live rebuilds (`go install github.com/cespare/reflex@latest`).
- **Unity 2021 LTS+** with the Nakama .NET client imported (for the `unity/` project).

## Fast Iteration Workflow

### 1. Build the Plugin
Compile the Go backend into a Nakama plugin:
```bash
task backend:build
```
- Uses `heroiclabs/nakama-pluginbuilder:3.34.1` to produce `backend/build/backend.so` (keeps parity with Nakama 3.34.1).
- Runs inside Docker for consistency.

### 2. Run the Stack
Start Nakama 3.34.1 + Postgres locally:
```bash
task stack:up-detached
```
- Launches services in the background.
- Nakama mounts your built `.so` and config from `infra/`.

### 3. Hot Reload While Editing
Watch Go files and auto-rebuild/restart:
```bash
task backend:watch
```
- Requires reflex.
- On `.go` file changes: rebuild plugin → `docker compose restart nakama` (~1s loop).

### 4. Monitor and Stop
```bash
task stack:logs      # Tail Nakama logs
task stack:down      # Stop and remove containers
task stack:psql      # Open psql shell into Postgres
```

### 5. Test Pure Logic
Run Go unit tests (domain logic only):
```bash
task backend:test
```

### 6. Tidy Modules
```bash
task tidy
```
- Runs `go mod tidy` in backend module.

## Backend Architecture

The backend separates concerns for scalability and testability:

```
backend/
├── cmd/module/main.go     # Minimal Nakama entrypoint
├── internal/game/         # Pure domain logic (deterministic + unit-testable)
└── internal/nakama/       # Thin integration layer (RPCs, matches, hooks)
```

- **`internal/game/`**: Core rules (e.g., `Service.ValidateOrder`, `MatchState`, `Order`). No Nakama dependencies—testable with `go test`.
- **`internal/nakama/`**: Bridges Nakama contexts to game logic. `RPCHandlers` parse JSON payloads and call `game.Service`. `RegisterMatch` exposes authoritative matches.
- **Sample RPC**: `tb_order_validate` validates player orders and returns projections.
- **Sample Match**: `tb_war_room` seeds state and updates timestamps—extend for real simulation/broadcasting.

### Testing the Sample RPC
With stack running, call via HTTP:
```bash
curl -X POST "http://127.0.0.1:7350/v2/rpc/tb_order_validate?http_key=defaultkey" \
  -H "Content-Type: application/json" \
  -d '{"matchId":"debug","playerId":"user-1","action":"attack","target":"capital-1","units":5}'
```
Response: JSON with validation result and projected state.

## Unity Integration Outline

Unity connects to Nakama for auth, RPCs, and matches. Keep networking out of UI/gameplay scripts. The backend builds against `heroiclabs/nakama:3.34.1` and `github.com/heroiclabs/nakama-common v1.43.1`, so keep the Unity client on a compatible Nakama SDK release.

### Folder Structure
```
unity/Assets/Scripts/
├── Networking/
│   └── GameClient.cs    # Nakama client wrapper
├── GameLogic/           # Client-side prediction/state (mirror backend types)
└── UI/                  # Views + presenters (depend on interfaces, not raw Nakama)
```

- **Networking/**: Low-level Nakama connectivity (auth, RPC helpers, match joins).
- **GameLogic/**: Deterministic simulation mirroring `backend/internal/game` for prediction.
- **UI/**: Presentation; call into logic layer for actions.

### Quickstart
1. Import Nakama .NET SDK into Unity.
2. Add `GameClient` to a bootstrap scene (marked `DontDestroyOnLoad`).
3. Authenticate: `await GameClient.Instance.ConnectAndAuthenticateAsync(deviceId)`.
4. Call RPCs: `await GameClient.Instance.ValidateOrderAsync(matchId, target, units)`.
5. Join matches: `await GameClient.Instance.JoinAuthoritativeMatchAsync()`.

## Devcontainer

Open the folder in VS Code and select "Reopen in Container". The container has:
- Go 1.21
- Docker CLI
- Task + reflex
- Extensions: Go, Makefile Tools, Docker

Run all `task ...` commands inside it without touching your host.

## Git Hygiene

**Tracked**:
- `backend/**/*.go`, `Taskfile.yml`, `infra/**`, `.devcontainer/**`, Unity scripts you author.

**Ignored** (`.gitignore`):
- `backend/build/` (compiled plugin)
- IDE cruft (`.vscode/`, `.idea/`, `*.swp`)
- Unity output (`Library/`, `Temp/`, `Obj/`, `Builds/`, `Logs/`, `*.csproj`, etc.)
- Docker volumes

## Extending the Stack

- **Add RPCs**: Extend `internal/nakama/rpc.go` and wire in `Register`.
- **Expand Game Logic**: Add rules to `internal/game/`, test with `task backend:test`.
- **Matches**: Flesh out `warMatchHandler` for simulation and broadcasting.
- **Unity**: Mirror domain types for prediction; use Nakama socket for real-time updates.
- **CI**: Build `backend.so` in pipelines, push to artifact store.

Happy building! 🚀
