const CURRENT_CONTRACT_VERSION = "2026-05-30";
const GEMINI_DEFAULT_MODEL = "gemini-2.5-flash";
const MAX_GEMINI_ATTEMPTS = 3;

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
  SessionId?: string;
  Goal?: string;
  AchievementRate?: number;
  Reflection?: string;
  NextTask?: string;
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

type GeminiAxisScores = {
  goal_achievement?: unknown;
  specificity?: unknown;
  learning?: unknown;
  next_action?: unknown;
  continuity?: unknown;
};

type GeminiEvaluationResponse = {
  total_score?: unknown;
  axis_scores?: GeminiAxisScores;
  feedback?: unknown;
};

type ValidatedEvaluation = {
  totalScore: number;
  goalScore: number;
  specificityScore: number;
  learningScore: number;
  nextActionScore: number;
  continuityScore: number;
  feedback: string;
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
    case "complete-session":
      return handleCompleteSession(_payload);
    case "front-display-snapshot":
      return json(okResponse({ Snapshot: emptySnapshot() }));
    default:
      return json(errorResponse(
        "server_error",
        `${action} is callable, but its persistent handler is not implemented yet.`,
      ), 501);
  }
}

async function handleCompleteSession(payload: GameApiRequest): Promise<Response> {
  const evaluation = await evaluateDevLogWithGemini(payload);
  if (evaluation.ok) {
    return json(okResponse({
      Session: buildSessionResponse(payload, evaluation.value, ""),
    }));
  }

  return json(okResponse({
    Session: buildSessionResponse(payload, null, evaluation.reason),
  }));
}

async function evaluateDevLogWithGemini(
  payload: GameApiRequest,
): Promise<{ ok: true; value: ValidatedEvaluation } | { ok: false; reason: string }> {
  const apiKey = Deno.env.get("GEMINI_API_KEY")?.trim();
  if (!apiKey) {
    return { ok: false, reason: "GEMINI_API_KEY is not configured" };
  }

  const model = Deno.env.get("GEMINI_MODEL")?.trim() || GEMINI_DEFAULT_MODEL;
  const endpoint = `https://generativelanguage.googleapis.com/v1beta/models/${
    encodeURIComponent(model)
  }:generateContent`;

  try {
    for (let attempt = 1; attempt <= MAX_GEMINI_ATTEMPTS; attempt++) {
      const response = await fetch(endpoint, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "x-goog-api-key": apiKey,
        },
        body: JSON.stringify({
          contents: [
            {
              role: "user",
              parts: [
                { text: buildEvaluationPrompt(payload) },
              ],
            },
          ],
          generationConfig: {
            responseMimeType: "application/json",
            responseJsonSchema: geminiEvaluationJsonSchema(),
          },
        }),
      });

      if (!response.ok) {
        if (isRetryableGeminiStatus(response.status) && attempt < MAX_GEMINI_ATTEMPTS) {
          await delay(150 * attempt);
          continue;
        }

        return { ok: false, reason: `Gemini HTTP ${response.status}` };
      }

      const body = await response.json();
      const text = extractGeminiText(body);
      const parsed = JSON.parse(stripJsonFence(text)) as GeminiEvaluationResponse;
      return { ok: true, value: validateGeminiEvaluation(parsed) };
    }

    return { ok: false, reason: "Gemini retry attempts exhausted" };
  } catch (error) {
    return {
      ok: false,
      reason: error instanceof Error ? error.message : "Gemini evaluation failed",
    };
  }
}

function isRetryableGeminiStatus(status: number): boolean {
  return status === 429 || status >= 500;
}

function delay(milliseconds: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
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

function buildSessionResponse(
  payload: GameApiRequest,
  evaluation: ValidatedEvaluation | null,
  failureReason: string,
): Record<string, unknown> {
  const now = new Date().toISOString();
  return {
    Id: payload.SessionId ?? "",
    UserId: "",
    StartedAtUtc: now,
    EndedAtUtc: now,
    DurationMinutes: 0,
    Goal: payload.Goal ?? "",
    AchievementRate: clampScore(payload.AchievementRate ?? 0),
    Reflection: payload.Reflection ?? "",
    NextTask: payload.NextTask ?? "",
    Status: evaluation ? 1 : 6,
    SuspiciousFlags: [],
    MentorComment: "",
    ApprovedBy: "",
    ApprovedAtUtc: "",
    AiEvaluationFailureReason: evaluation ? "" : normalizeFailureReason(failureReason),
    Evaluation: evaluation ? toUnityEvaluation(evaluation) : null,
  };
}

function toUnityEvaluation(evaluation: ValidatedEvaluation): Record<string, unknown> {
  const rank = rankFromScore(evaluation.totalScore);
  return {
    TotalScore: evaluation.totalScore,
    Rank: rank,
    GoalScore: evaluation.goalScore,
    SpecificityScore: evaluation.specificityScore,
    LearningScore: evaluation.learningScore,
    NextActionScore: evaluation.nextActionScore,
    ContinuityScore: evaluation.continuityScore,
    ExpMultiplier: multiplierFromRank(rank),
    Feedback: evaluation.feedback,
    ModelName: Deno.env.get("GEMINI_MODEL")?.trim() || GEMINI_DEFAULT_MODEL,
  };
}

function geminiEvaluationJsonSchema(): Record<string, unknown> {
  const scoreSchema = {
    type: "integer",
    minimum: 0,
    maximum: 100,
  };

  return {
    type: "object",
    properties: {
      total_score: scoreSchema,
      axis_scores: {
        type: "object",
        properties: {
          goal_achievement: scoreSchema,
          specificity: scoreSchema,
          learning: scoreSchema,
          next_action: scoreSchema,
          continuity: scoreSchema,
        },
        required: [
          "goal_achievement",
          "specificity",
          "learning",
          "next_action",
          "continuity",
        ],
        additionalProperties: false,
      },
      feedback: {
        type: "string",
      },
    },
    required: [
      "total_score",
      "axis_scores",
      "feedback",
    ],
    additionalProperties: false,
  };
}

function buildEvaluationPrompt(payload: GameApiRequest): string {
  return [
    "あなたはプログラミングスクールの開発ログを評価するメンター補助AIです。",
    "AI評価だけで承認確定せず、メンター承認前提の参考評価として扱います。",
    "開発時間が長いだけで高評価にせず、具体的な学びや次回行動を重視してください。",
    "JSONのみを返してください。Markdown、コードフェンス、説明文は含めないでください。",
    "",
    "出力JSON:",
    "{\"total_score\":0,\"axis_scores\":{\"goal_achievement\":0,\"specificity\":0,\"learning\":0,\"next_action\":0,\"continuity\":0},\"feedback\":\"120文字以内の日本語の短評\"}",
    "",
    "開発目標:",
    payload.Goal || "(DB永続化後にセッションから補完)",
    "",
    "達成度:",
    `${clampScore(payload.AchievementRate ?? 0)}%`,
    "",
    "振り返り:",
    payload.Reflection || "",
    "",
    "次のタスク:",
    payload.NextTask || "",
  ].join("\n");
}

function extractGeminiText(body: unknown): string {
  const candidate = (body as { candidates?: Array<{ content?: { parts?: Array<{ text?: string }> } }> })
    .candidates?.[0];
  const text = candidate?.content?.parts
    ?.map((part) => part.text ?? "")
    .join("")
    .trim();

  if (!text) {
    throw new Error("Gemini response did not include text");
  }

  return text;
}

function stripJsonFence(text: string): string {
  return text
    .replace(/^```(?:json)?/i, "")
    .replace(/```$/i, "")
    .trim();
}

function validateGeminiEvaluation(parsed: GeminiEvaluationResponse): ValidatedEvaluation {
  if (!parsed || typeof parsed !== "object") {
    throw new Error("Gemini JSON is not an object");
  }

  const axis = parsed.axis_scores;
  if (!axis || typeof axis !== "object") {
    throw new Error("Gemini JSON missing axis_scores");
  }

  const feedback = typeof parsed.feedback === "string" ? parsed.feedback.trim() : "";
  if (!feedback) {
    throw new Error("Gemini JSON missing feedback");
  }

  return {
    totalScore: readScore(parsed.total_score, "total_score"),
    goalScore: readScore(axis.goal_achievement, "goal_achievement"),
    specificityScore: readScore(axis.specificity, "specificity"),
    learningScore: readScore(axis.learning, "learning"),
    nextActionScore: readScore(axis.next_action, "next_action"),
    continuityScore: readScore(axis.continuity, "continuity"),
    feedback: feedback.slice(0, 120),
  };
}

function readScore(value: unknown, name: string): number {
  if (!Number.isInteger(value) || value < 0 || value > 100) {
    throw new Error(`Gemini score is invalid: ${name}`);
  }

  return value;
}

function rankFromScore(score: number): number {
  if (score >= 95) return 0;
  if (score >= 85) return 1;
  if (score >= 75) return 2;
  if (score >= 60) return 3;
  if (score >= 40) return 4;
  return 5;
}

function multiplierFromRank(rank: number): number {
  switch (rank) {
    case 0:
      return 2.0;
    case 1:
      return 1.8;
    case 2:
      return 1.6;
    case 3:
      return 1.3;
    case 4:
      return 1.0;
    default:
      return 0.8;
  }
}

function clampScore(value: number): number {
  if (!Number.isFinite(value)) {
    return 0;
  }

  return Math.max(0, Math.min(100, Math.round(value)));
}

function normalizeFailureReason(reason: string): string {
  return reason?.trim() || "AI評価失敗のため暫定評価です。記録は保存されました。";
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
