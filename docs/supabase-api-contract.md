# Supabase Game API Contract

This document defines the Unity client contract for the Supabase Edge Function at `/functions/v1/game-api`.

## Version

- Current contract version: `2026-05-30`
- Unity sends the version in both `ContractVersion` request JSON and the `X-AOR-Contract-Version` header.
- The Edge Function should return HTTP `409` with `ErrorCode: "contract_mismatch"` when the deployed backend cannot satisfy this contract.

## Runtime Config

Unity reads `Assets/StreamingAssets/supabase-config.json`.

```json
{
  "Enabled": true,
  "SupabaseUrl": "https://project.supabase.co",
  "SupabasePublishableKey": "sb_publishable_...",
  "UseDemoRepositoryFallback": false,
  "ApiContractVersion": "2026-05-30"
}
```

`UseDemoRepositoryFallback` must be `false` for production scenes. Local demo/test scenes may set it to `true`; otherwise Unity must show the API/configuration error instead of silently using `LocalGameRepository`.

## Request Envelope

All actions are sent as JSON by POST.

Required common fields:

- `ContractVersion`: `2026-05-30`
- `Action`: one of the actions below
- `SessionToken`: required for all actions except `login`

Actions:

- `login`: `LoginId`, `Password`
- `snapshot`
- `start-session`: `Goal`
- `complete-session`: `SessionId`, `AchievementRate`, `Reflection`, `NextTask`
- `approve-session`: `SessionId`, `Comment`
- `reject-session`: `SessionId`, `Comment`
- `submit-achievement`: `AchievementType`, `Title`, `Description`
- `approve-achievement`: `AchievementId`
- `reject-achievement`: `AchievementId`
- `battle-action`: `Role`, `Weapon`, `ActionType`
- `start-battle`
- `reset-battle`
- `set-boss-hp`: `Multiplier`

## Response Envelope

Successful responses:

```json
{
  "Ok": true,
  "ContractVersion": "2026-05-30",
  "SessionToken": "opaque-session-token",
  "User": {},
  "Snapshot": {},
  "Session": {},
  "ActionResult": {}
}
```

Error responses:

```json
{
  "Ok": false,
  "ContractVersion": "2026-05-30",
  "ErrorCode": "auth_expired",
  "Error": "session expired",
  "AuthExpired": true,
  "RetryAfterSeconds": 0
}
```

Required error codes:

- `auth_expired` or `invalid_session`: clear Unity session token and force login.
- `auth_rejected` or `forbidden`: do not retry automatically.
- `contract_mismatch`: show production API incompatibility; do not use demo fallback.
- `rate_limited`: set `RetryAfterSeconds`.
- `server_error`: user may retry.

## Snapshot DTO

`Snapshot` is the source of truth for production Unity runtime state. The DTO mapper converts:

- `Users` to `UserProfile`
- `Stats` to `CharacterStatsRecord`
- `Weapons` to `WeaponDefinition`
- `Sessions` to `DevSession`
- `Products` to `ProductEntry`
- `Achievements` to `AchievementEntry`
- `AuditLogs` to `AuditLogEntry`
- `ActiveBattle` to `BossBattleState`

DTO enums are encoded as integer values matching Unity enums. Unity clamps unknown enum values to the nearest supported value to keep older clients from crashing, but contract mismatches should still be rejected by the backend when shape or semantics are incompatible.
