const CURRENT_CONTRACT_VERSION = "2026-05-30";

const REQUIRED_SESSION_ACTIONS = new Set([
  "change-password",
  "create-account",
  "issue-temporary-password",
  "snapshot",
  "start-session",
  "complete-session",
  "approve-session",
  "reject-session",
  "submit-achievement",
  "approve-achievement",
  "reject-achievement",
  "register-product",
  "hide-product",
  "battle-action",
  "start-battle",
  "reset-battle",
  "set-boss-hp",
]);

const PUBLIC_ACTIONS = new Set([
  "login",
  "front-display-snapshot",
]);

const SUPPORTED_ACTIONS = new Set([
  ...PUBLIC_ACTIONS,
  ...REQUIRED_SESSION_ACTIONS,
]);

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, apikey, content-type, x-aor-contract-version",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};

type GameApiRequest = {
  ContractVersion?: string;
  Action?: string;
  SessionToken?: string;
};

type GameApiResponse = {
  Ok: boolean;
  ContractVersion: string;
  ErrorCode?: string;
  Error?: string;
  AuthExpired?: boolean;
  RetryAfterSeconds?: number;
  SessionToken?: string;
  User?: Record<string, unknown>;
  TemporaryPassword?: string;
  Snapshot?: Record<string, unknown>;
  Session?: Record<string, unknown>;
  Product?: Record<string, unknown>;
  Achievement?: Record<string, unknown>;
  ActionResult?: Record<string, unknown>;
};

Deno.serve(async (request) => {
  if (request.method === "OPTIONS") {
    return new Response(null, {
      status: 204,
      headers: CORS_HEADERS,
    });
  }

  if (request.method !== "POST") {
    return json(errorResponse("server_error", "POST only"), 405);
  }

  let payload: GameApiRequest;
  try {
    payload = await request.json();
  } catch (_error) {
    return json(errorResponse("server_error", "invalid JSON request"), 400);
  }

  const headerVersion = request.headers.get("x-aor-contract-version")?.trim();
  const requestVersion = payload.ContractVersion?.trim();
  if (headerVersion !== CURRENT_CONTRACT_VERSION || requestVersion !== CURRENT_CONTRACT_VERSION) {
    return json(errorResponse("contract_mismatch", "Supabase game-api contract mismatch"), 409);
  }

  const action = payload.Action?.trim() ?? "";
  if (!SUPPORTED_ACTIONS.has(action)) {
    return json(errorResponse("server_error", `unsupported action: ${action || "(blank)"}`), 400);
  }

  if (REQUIRED_SESSION_ACTIONS.has(action) && !payload.SessionToken?.trim()) {
    return json({
      ...errorResponse("invalid_session", "session token is required"),
      AuthExpired: true,
    }, 401);
  }

  return handleAction(action, payload);
});

async function handleAction(action: string, _payload: GameApiRequest): Promise<Response> {
  switch (action) {
    case "front-display-snapshot":
      return json(okResponse({ Snapshot: emptySnapshot() }));
    default:
      return json(errorResponse(
        "server_error",
        `${action} is callable, but its persistent handler is not implemented yet.`,
      ), 501);
  }
}

function okResponse(body: Partial<GameApiResponse> = {}): GameApiResponse {
  return {
    Ok: true,
    ContractVersion: CURRENT_CONTRACT_VERSION,
    ...body,
  };
}

function errorResponse(errorCode: string, message: string): GameApiResponse {
  return {
    Ok: false,
    ContractVersion: CURRENT_CONTRACT_VERSION,
    ErrorCode: errorCode,
    Error: message,
    AuthExpired: errorCode === "auth_expired" || errorCode === "invalid_session",
    RetryAfterSeconds: 0,
  };
}

function emptySnapshot(): Record<string, unknown> {
  return {
    Users: [],
    Stats: [],
    Weapons: [],
    Sessions: [],
    Products: [],
    Achievements: [],
    AuditLogs: [],
    ActiveBattle: null,
  };
}

function json(body: GameApiResponse, status = body.Ok ? 200 : 500): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      ...CORS_HEADERS,
      "Content-Type": "application/json; charset=utf-8",
    },
  });
}
