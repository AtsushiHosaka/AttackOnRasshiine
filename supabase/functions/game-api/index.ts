// Fail closed even if this deprecated function is manually selected for deploy.
// The historical implementation is kept outside `supabase/functions` under
// `supabase/history/legacy-game-api`; it must never regain a deployable entrypoint.
import { deprecatedGameApiResponse } from "./handler.ts";

Deno.serve(deprecatedGameApiResponse);
