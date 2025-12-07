# Terrabound

Multiplayer strategy game with authoritative server logic. Built with **Go + Nakama** (backend), **Unity + C#** (client), and **Docker + Postgres** (infrastructure).

## Quick Start

```bash
# 1. Open in VS Code Dev Container (recommended) or ensure Docker v24+ is installed
# 2. Start everything
task stack:up

# 3. Access services
# Nakama Console: http://localhost:7351 (admin/password)
# Nakama API:     http://localhost:7350
# PostgreSQL:     localhost:5432
```

## Project Structure

```
backend/           # Go plugin for Nakama
├── cmd/module/    # Entrypoint
└── internal/
    ├── game/      # Pure domain logic (testable, no Nakama deps)
    ├── nakama/    # RPCs, matches, hooks
    └── constants/ # Shared errors/constants

unity/             # Unity client
└── Assets/        # Scripts, scenes, prefabs

infra/             # Docker Compose + configs
├── docker-compose.yml
├── config/local.yml
└── postgres/init.sql
```

## Commands

| Command | Description |
|---------|-------------|
| `task stack:up` | Build plugin & start Nakama + Postgres |
| `task stack:down` | Stop all services |
| `task stack:logs` | Tail Nakama logs |
| `task backend:watch` | Hot-reload: rebuild + restart on `.go` changes |
| `task backend:test` | Run unit tests |

> `stack:up` automatically builds the plugin — no need to run `backend:build` separately.

## Architecture

**Backend** separates concerns:
- `internal/game/` — Deterministic game rules, unit-testable
- `internal/nakama/` — Thin integration layer calling game logic

**Unity** mirrors this pattern:
- Keep networking isolated from gameplay/UI
- Use Nakama .NET SDK compatible with server v3.34.1

## Devcontainer

The Dev Container provides a fully configured development environment with:
- ✅ Go 1.25
- ✅ Docker CLI (Docker-in-Docker)
- ✅ Task runner + reflex (hot-reload)
- ✅ VS Code extensions: Go, Docker, Task
- ✅ Port forwarding for all services

### Using the Dev Container

1. Install [Docker Desktop](https://www.docker.com/products/docker-desktop/) and [VS Code](https://code.visualstudio.com/)
2. Install the [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)
3. Open this repo in VS Code
4. Click "Reopen in Container" when prompted (or `F1` → "Dev Containers: Reopen in Container")
5. Wait for setup to complete (~2-3 minutes first time)
6. Run `task stack:up` in the integrated terminal

All `task ...` commands work inside the container without any additional setup on your host machine.

## Example RPC

```bash
curl -X POST "http://127.0.0.1:7350/v2/rpc/tb_order_validate?http_key=defaultkey" \
  -H "Content-Type: application/json" \
  -d '{"matchId":"debug","playerId":"user-1","action":"attack","target":"capital-1","units":5}'
```
