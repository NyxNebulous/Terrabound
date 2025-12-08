# Terrabound

Multiplayer strategy game with authoritative server logic, built with **Go + Nakama**, **Unity + C#**, and **Docker + Postgres**.

---

## Quick Start

```bash
# Using VS Code Dev Container (recommended)
# 1. Open folder in VS Code → "Reopen in Container"
# 2. Run:
task stack:up
```

**Services:**
| Port | Service |
|------|---------|
| 7351 | Nakama Console (admin/password) |
| 7350 | Nakama HTTP API |
| 5432 | PostgreSQL |

---

## Project Structure

```
backend/               # Go plugin for Nakama
├── cmd/module/        # Entrypoint
└── internal/
    ├── game/          # Pure domain logic (testable)
    ├── nakama/        # RPCs, matches, hooks
    ├── config/        # Configuration helpers
    ├── constants/     # Shared errors/constants
    └── oauth/         # OAuth flows

unity/                 # Unity client
└── Assets/Scripts/    # Networking, GameLogic, UI

infra/                 # Docker Compose + configs
├── docker-compose.yml
├── config/local.yml
└── postgres/init.sql
```

---

## Commands

| Command | Description |
|---------|-------------|
| `task stack:up` | Build plugin & start services |
| `task stack:down` | Stop all services |
| `task stack:logs` | Tail Nakama logs |
| `task backend:watch` | Hot-reload on `.go` changes |
| `task backend:test` | Run unit tests |
| `task tidy` | Run `go mod tidy` |

> `stack:up` builds automatically—no need to run `backend:build` separately.

---

## Architecture

### Backend

```
backend/
├── cmd/module/main.go     # Nakama entrypoint
├── internal/game/         # Pure domain logic (no Nakama deps)
└── internal/nakama/       # Thin integration layer
```

- **`internal/game/`** — Deterministic rules, unit-testable with `go test`
- **`internal/nakama/`** — RPCs and match handlers calling game logic

### Unity

```
unity/Assets/Scripts/
├── Networking/    # Nakama client wrapper
├── GameLogic/     # Client-side prediction (mirrors backend)
└── UI/            # Presentation layer
```

Keep networking isolated from gameplay/UI. Use Nakama SDK compatible with server v3.34.1.

---

## Dev Container

Pre-configured environment with Go 1.25, Docker CLI, Task, and reflex.

1. Open in VS Code → **F1** → "Reopen in Container"
2. Run `task stack:up`

---

## Example RPC

```bash
curl -X POST "http://127.0.0.1:7350/v2/rpc/tb_order_validate?http_key=defaultkey" \
  -H "Content-Type: application/json" \
  -d '{"matchId":"debug","playerId":"user-1","action":"attack","target":"capital-1","units":5}'
```

---

## Prerequisites (Local Setup)

Only needed if **not** using the Dev Container:

| Tool | Version | Install |
|------|---------|---------|
| Docker | 24+ | [docker.com](https://docker.com) |
| Go | 1.25+ | [go.dev](https://go.dev) |
| Task | latest | `go install github.com/go-task/task/v3/cmd/task@latest` |
| reflex | latest | `go install github.com/cespare/reflex@latest` |
| Unity | 2021 LTS+ | [unity.com](https://unity.com) |
