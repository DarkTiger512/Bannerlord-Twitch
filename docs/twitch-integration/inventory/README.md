# BLT Twitch Integration Inventory

Generated from tracked source and the default v4 YAML configuration. Re-run with `node tools/generate-inventory.mjs`.

## Coverage

| Area | Count |
|---|---:|
| Commands | 63 |
| Rewards | 15 |
| Settings | 1468 |
| Source components | 355 |

## Action categories

| Category | Commands |
|---|---:|
| Battle | 9 |
| Progression | 7 |
| Stream Control | 1 |
| Community | 2 |
| Hero | 10 |
| Kingdom | 9 |
| Equipment | 15 |
| Tournament | 3 |
| Retinue | 3 |
| General | 4 |

## Component map

| Kind | Files |
|---|---:|
| action-handler | 67 |
| persistence | 73 |
| twitch-service | 10 |
| configuration | 150 |
| test | 20 |
| support | 136 |
| harmony-patch | 16 |
| behavior | 41 |
| overlay-hub | 6 |

## Current data flow

Twitch chat/EventSub and channel-point redemptions are normalized into `ReplyContext`, resolved through `ActionManager`, and executed by registered handlers. Settings come from per-profile YAML and are edited by BLTConfigure. Self-hosted overlays use SignalR hubs. The experimental Extension code signs privileged JWTs in the mod and the local relay forwards raw command strings; both paths are replaced by the structured managed-service protocol.

## Machine-readable references

- `commands.json`: configured ordinary commands and handler settings.
- `rewards.json`: native channel-point reward definitions.
- `action-manifest.json`: initial Extension-facing action catalog.
- `settings.json`: public configurable properties and source locations.
- `components.json`: project files, symbols, and architectural roles.
