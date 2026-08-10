# Historical implementation — never deploy

`index.ts` is the retired pre-v2 backend preserved for source archaeology only.
It intentionally lives outside `supabase/functions`, so the Supabase CLI cannot
select or bundle it as an Edge Function.

The only deployable legacy function entrypoint is
`../../functions/game-api/index.ts`, which always returns HTTP 410. Production
traffic belongs to the canonical sibling project and its `game-api-v2` function.
