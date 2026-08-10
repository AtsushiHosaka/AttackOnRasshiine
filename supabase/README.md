# Deprecated backend — do not deploy

This directory is retained only as historical Unity-project source. Its
migrations, seed, and legacy `game-api` Edge Function are disabled in
`config.toml` and must not be linked or deployed. Defense in depth also keeps a
first-sorting blocker migration, a deploy script that always fails, and an HTTP
410-only function entrypoint. The retired implementation is isolated under
`history/legacy-game-api`, and the retired schema/seed under `history`, outside
the deployable `functions`, `migrations`, and seed paths.

The only canonical backend is the sibling project:

```text
../../AttackOnRasshiineSupabase
```

Use its guarded scripts for every backend operation:

```sh
cd ../../AttackOnRasshiineSupabase
scripts/deploy.sh --check
scripts/deploy.sh --dry-run
```

That project deploys only `game-api-v2`, owns the canonical contract at
`contracts/game-api.contract.json`, and contains the production security
migrations and verification suite. Do not run `supabase link`, `supabase db
push`, or `supabase functions deploy` from this deprecated directory.
