const CURRENT_CONTRACT_VERSION = "2026-05-30";
const GEMINI_DEFAULT_MODEL = "gemini-2.5-flash";
const MAX_GEMINI_ATTEMPTS = 3;
const SESSION_TTL_SECONDS = 60 * 60 * 24 * 7;

declare const Deno: {
  serve: (handler: (request: Request) => Response | Promise<Response>) => void;
  env: {
    get: (name: string) => string | undefined;
  };
};

const REQUIRED_SESSION_ACTIONS = new Set([
  "change-password",
  "logout",
  "create-account",
  "issue-temporary-password",
  "deactivate-account",
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
  "roll-cosmetic-gacha",
  "equip-cosmetic",
  "battle-action",
  "start-battle",
  "reset-battle",
  "set-boss-hp",
  "set-boss-config",
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

const TEXT_ENCODER = new TextEncoder();
const USER_ROLES = ["member", "mentor"] as const;
const SESSION_STATUSES = ["in_progress", "pending", "approved", "rejected", "incomplete", "needs_review", "ai_pending"] as const;
const AI_RANKS = ["S", "A+", "A", "B", "C", "D"] as const;
const BATTLE_ROLES = ["attacker", "healer", "defender", "supporter"] as const;
const WEAPON_KINDS = ["blade", "rifle", "cannon", "shield", "debug_tool", "release_gear", "contest_gear"] as const;
const ACHIEVEMENT_TYPES = ["contest_submission", "release", "update", "award", "continuous_dev"] as const;
const ACHIEVEMENT_STATUSES = ["pending", "approved", "rejected"] as const;
const BATTLE_ACTION_TYPES = ["normal", "strong", "full_power", "support", "guard"] as const;
const BATTLE_STATUSES = ["scheduled", "active", "completed"] as const;
const BATTLE_PHASES = ["turn_start", "action_select", "resolving", "result", "completed"] as const;
const MENTOR_NAMES = ["らっしーね", "かみむー", "えーえす", "だーす", "いのべえ", "ゆっけ", "たーとる", "まさぴー"];
const LEGACY_DEMO_MEMBERS = new Map([
  ["member1", "ハッカーくん"],
  ["member2", "ネットランナー"],
  ["member3", "プログラミング部"],
  ["member4", "デザイナーさん"],
  ["member5", "サウンド係"],
  ["member6", "メカニックちゃん"],
]);

type GameApiRequest = {
  ContractVersion?: string;
  Action?: string;
  SessionToken?: string;
  LoginId?: string;
  Password?: string;
  NewPassword?: string;
  Nickname?: string;
  TeamId?: string;
  RankingVisible?: boolean;
  UserId?: string;
  SessionId?: string;
  AchievementId?: string;
  ProductId?: string;
  Goal?: string;
  AchievementRate?: number;
  AchievementType?: number;
  Title?: string;
  Url?: string;
  Description?: string;
  Reflection?: string;
  NextTask?: string;
  Comment?: string;
  Role?: number;
  Weapon?: number;
  ActionType?: number;
  Multiplier?: number;
  BossName?: string;
  BossType?: string;
  StoryTeaser?: string;
  ItemId?: string;
  Equipped?: boolean;
};

type GameApiResponse = {
  Ok: boolean;
  ContractVersion: string;
  ErrorCode?: string;
  Error?: string;
  AuthExpired?: boolean;
  RetryAfterSeconds?: number;
  SessionToken?: string;
  User?: Record<string, unknown> | null;
  TemporaryPassword?: string;
  Snapshot?: Record<string, unknown>;
  Session?: Record<string, unknown> | null;
  Product?: Record<string, unknown> | null;
  Achievement?: Record<string, unknown> | null;
  ActionResult?: Record<string, unknown> | null;
  GachaResult?: Record<string, unknown> | null;
};

const COSMETIC_REWARDS = [
  { id: "cosmetic:hat_wizard", label: "魔法帽" },
  { id: "cosmetic:sword", label: "ローポリソード" },
  { id: "cosmetic:bow", label: "ローポリボウ" },
  { id: "cosmetic:shield", label: "ローポリシールド" },
  { id: "cosmetic:cyan_aura", label: "シアンオーラ" },
];

type SupabaseDbConfig = {
  url: string;
  serviceKey: string;
};

type DbTeam = {
  id: string;
  code?: string | null;
  name?: string | null;
  mentor_user_id?: string | null;
};

type DbUser = {
  id: string;
  login_id: string;
  password_hash?: string;
  nickname: string;
  role: string;
  team_id?: string | null;
  ranking_visible?: boolean | null;
  initial_password_changed?: boolean | null;
  is_active?: boolean | null;
};

type DbCharacterStats = {
  user_id: string;
  level?: number | null;
  exp?: number | null;
  hp?: number | null;
  atk?: number | null;
  def?: number | null;
  mp?: number | null;
  unlocked_weapons?: unknown;
  titles?: unknown;
  skills?: unknown;
};

type DbDevSession = {
  id: string;
  user_id: string;
  started_at: string;
  ended_at?: string | null;
  duration_minutes?: number | null;
  goal: string;
  achievement_rate?: number | null;
  reflection?: string | null;
  next_task?: string | null;
  status: string;
  suspicious_flags?: unknown;
  mentor_comment?: string | null;
  approved_by?: string | null;
  approved_at?: string | null;
  ai_evaluation_failure_reason?: string | null;
};

type DbAiEvaluation = {
  dev_session_id: string;
  total_score: number;
  rank: string;
  axis_scores?: unknown;
  feedback: string;
  exp_multiplier?: number | string | null;
  model_name?: string | null;
};

type DbProduct = {
  id: string;
  user_id: string;
  title: string;
  url: string;
  description?: string | null;
  is_public?: boolean | null;
  hidden_by?: string | null;
  created_at: string;
};

type DbAchievement = {
  id: string;
  user_id: string;
  type: string;
  title: string;
  description?: string | null;
  status: string;
  approved_by?: string | null;
  approved_at?: string | null;
  created_at: string;
};

type DbAuditLog = {
  id: string;
  actor_user_id?: string | null;
  action_type: string;
  target_type: string;
  target_id?: string | null;
  before_state?: string | null;
  after_state?: string | null;
  created_at: string;
};

type DbBossBattle = {
  id: string;
  week_start_date: string;
  boss_id?: string | null;
  boss_name: string;
  boss_type: string;
  boss_def?: number | null;
  story_teaser?: string | null;
  base_hp: number;
  hp_multiplier?: number | string | null;
  current_hp: number;
  max_hp: number;
  turn_count: number;
  turn_number?: number | null;
  phase?: string | null;
  status: string;
  result?: string | null;
  total_damage?: number | null;
  highlight_user_id?: string | null;
  created_by: string;
  created_at: string;
  started_at?: string | null;
  completed_at?: string | null;
};

type DbBattleParticipant = {
  battle_id: string;
  user_id: string;
  nickname: string;
  role: string;
  weapon_kind: string;
  current_hp: number;
  current_mp: number;
  total_damage: number;
  total_heal: number;
  support_count: number;
};

type DbBattleAction = {
  battle_id: string;
  user_id: string;
  turn_number: number;
  role: string;
  weapon_kind?: string | null;
  action_type: string;
  mp_cost?: number | null;
  damage?: number | null;
  heal?: number | null;
  support_effect?: unknown;
  message?: string | null;
  created_at?: string | null;
};

type WeaponDefinition = {
  kind: number;
  dbKind: string;
  displayName: string;
  description: string;
  mpEfficiencyBonus: number;
  damageMultiplier: number;
  preferredRole: number;
  isSpecial?: boolean;
};

type BattleState = {
  battle: DbBossBattle;
  participants: BattleParticipantState[];
  actions: DbBattleAction[];
};

type BattleParticipantState = DbBattleParticipant & {
  stats: DbCharacterStats;
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

class ApiError extends Error {
  readonly code: string;
  readonly status: number;
  readonly retryAfterSeconds: number;

  constructor(code: string, message: string, status = 400, retryAfterSeconds = 0) {
    super(message);
    this.code = code;
    this.status = status;
    this.retryAfterSeconds = retryAfterSeconds;
  }
}

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

  try {
    const payload = await readPayload(request);
    const action = validateRequestVersionAndAction(request, payload);
    if (REQUIRED_SESSION_ACTIONS.has(action) && !payload.SessionToken?.trim()) {
      throw new ApiError("invalid_session", "session token is required", 401);
    }

    return await handleAction(action, payload);
  } catch (error) {
    if (error instanceof ApiError) {
      return json({
        ...errorResponse(error.code, error.message),
        RetryAfterSeconds: error.retryAfterSeconds,
      }, error.status);
    }

    return json(errorResponse(
      "server_error",
      error instanceof Error ? error.message : "unexpected server error",
    ), 500);
  }
});

async function readPayload(request: Request): Promise<GameApiRequest> {
  try {
    return await request.json();
  } catch (_error) {
    throw new ApiError("server_error", "invalid JSON request", 400);
  }
}

function validateRequestVersionAndAction(request: Request, payload: GameApiRequest): string {
  const headerVersion = request.headers.get("x-aor-contract-version")?.trim();
  const requestVersion = payload.ContractVersion?.trim();
  if (headerVersion !== CURRENT_CONTRACT_VERSION || requestVersion !== CURRENT_CONTRACT_VERSION) {
    throw new ApiError("contract_mismatch", "Supabase game-api contract mismatch", 409);
  }

  const action = payload.Action?.trim() ?? "";
  if (!SUPPORTED_ACTIONS.has(action)) {
    throw new ApiError("server_error", `unsupported action: ${action || "(blank)"}`, 400);
  }

  return action;
}

async function handleAction(action: string, payload: GameApiRequest): Promise<Response> {
  switch (action) {
    case "login":
      return handleLogin(payload);
    case "change-password":
      return handleChangePassword(payload);
    case "logout":
      return handleLogout(payload);
    case "create-account":
      return handleCreateAccount(payload);
    case "issue-temporary-password":
      return handleIssueTemporaryPassword(payload);
    case "deactivate-account":
      return handleDeactivateAccount(payload);
    case "snapshot":
      return json(okResponse({ Snapshot: await buildSnapshot() }));
    case "front-display-snapshot":
      return json(okResponse({ Snapshot: await buildSnapshot() }));
    case "start-session":
      return handleStartSession(payload);
    case "complete-session":
      return handleCompleteSession(payload);
    case "approve-session":
      return handleReviewSession(payload, true);
    case "reject-session":
      return handleReviewSession(payload, false);
    case "submit-achievement":
      return handleSubmitAchievement(payload);
    case "approve-achievement":
      return handleReviewAchievement(payload, true);
    case "reject-achievement":
      return handleReviewAchievement(payload, false);
    case "register-product":
      return handleRegisterProduct(payload);
    case "hide-product":
      return handleHideProduct(payload);
    case "roll-cosmetic-gacha":
      return handleRollCosmeticGacha(payload);
    case "equip-cosmetic":
      return handleEquipCosmetic(payload);
    case "start-battle":
      return handleStartBattle(payload);
    case "reset-battle":
      return handleResetBattle(payload);
    case "set-boss-hp":
      return handleSetBossHp(payload);
    case "set-boss-config":
      return handleSetBossConfig(payload);
    case "battle-action":
      return handleBattleAction(payload);
    default:
      throw new ApiError("server_error", `unsupported action: ${action}`, 400);
  }
}

async function handleLogin(payload: GameApiRequest): Promise<Response> {
  const loginId = normalizeLoginId(payload.LoginId);
  const password = normalizeRequired(payload.Password, "パスワードを入力してください。");
  const user = await loadUserByLoginId(loginId, true);
  if (!user || user.is_active === false) {
    throw new ApiError("auth_rejected", "IDまたはパスワードが違います", 401);
  }

  const expected = user.password_hash ?? "";
  if (expected !== await hashPassword(password)) {
    throw new ApiError("auth_rejected", "IDまたはパスワードが違います", 401);
  }

  const sessionToken = await createSessionToken(user.id);
  return json(okResponse({
    SessionToken: sessionToken,
    User: await userDto(user),
    Snapshot: await buildSnapshot(),
  }));
}

async function handleChangePassword(payload: GameApiRequest): Promise<Response> {
  const user = await requireUser(payload);
  const currentPassword = normalizeRequired(payload.Password, "現在のパスワードを入力してください。");
  const newPassword = normalizePassword(payload.NewPassword);
  const loaded = await loadUserById(user.id, true);
  if (!loaded || loaded.password_hash !== await hashPassword(currentPassword)) {
    throw new ApiError("auth_rejected", "現在のパスワードが違います。", 403);
  }

  const before = describeUser(loaded);
  const updated = await patchUser(user.id, {
    password_hash: await hashPassword(newPassword),
    initial_password_changed: true,
  });
  await recordAudit(user.id, "account.initial_password_change", "user", user.id, before, describeUser(updated));
  return json(okResponse({
    User: await userDto(updated),
    Snapshot: await buildSnapshot(),
  }));
}

async function handleLogout(payload: GameApiRequest): Promise<Response> {
  const token = payload.SessionToken?.trim() ?? "";
  if (token) {
    await rest<unknown>(`app_sessions?${qs({ session_token: `eq.${token}` })}`, {
      method: "PATCH",
      body: {
        revoked_at: new Date().toISOString(),
      },
    }).catch(() => null);
  }

  return json(okResponse({}));
}

async function handleCreateAccount(payload: GameApiRequest): Promise<Response> {
  const mentor = await requireMentor(payload, "メンター権限が必要です。");
  const role = enumString(USER_ROLES, payload.Role ?? 0);
  const loginId = normalizeLoginId(payload.LoginId);
  const nickname = normalizeRequired(payload.Nickname, "表示名を入力してください。");
  const existing = await loadUserByLoginId(loginId, true);
  if (existing) {
    throw new ApiError("forbidden", "同じログインIDのユーザーがいます。", 409);
  }

  const temporaryPassword = generateTemporaryPassword();
  const teamId = await ensureTeamId(payload.TeamId, role);
  const inserted = await rest<DbUser[]>("users?select=" + userSelect(), {
    method: "POST",
    prefer: "return=representation",
    body: {
      login_id: loginId,
      password_hash: await hashPassword(temporaryPassword),
      nickname,
      role,
      team_id: teamId,
      ranking_visible: payload.RankingVisible !== false,
      initial_password_changed: false,
      is_active: true,
    },
  });
  const user = firstOrThrow(inserted, "アカウントを作成できませんでした。");

  if (role === "member") {
    await upsertStats(user.id, ensureGrowthUnlocks(defaultStats()));
  }

  await recordAudit(mentor.id, "account.create", "user", user.id, "", describeUser(user));
  await recordAudit(mentor.id, "account.temporary_password_issue", "user", user.id, "", `${describeUser(user)};temporaryPasswordIssued=true`);
  return json(okResponse({
    User: await userDto(user),
    TemporaryPassword: temporaryPassword,
    Snapshot: await buildSnapshot(),
  }));
}

async function handleIssueTemporaryPassword(payload: GameApiRequest): Promise<Response> {
  const mentor = await requireMentor(payload, "メンター権限が必要です。");
  const userId = normalizeRequired(payload.UserId, "ユーザーを選択してください。");
  const user = await loadUserById(userId, true);
  if (!user || user.is_active === false) {
    throw new ApiError("forbidden", "有効なアカウントが見つかりません。", 404);
  }

  const before = describeUser(user);
  const temporaryPassword = generateTemporaryPassword();
  const updated = await patchUser(user.id, {
    password_hash: await hashPassword(temporaryPassword),
    initial_password_changed: false,
  });
  await recordAudit(mentor.id, "account.temporary_password_issue", "user", user.id, before, `${describeUser(updated)};temporaryPasswordIssued=true`);
  return json(okResponse({
    User: await userDto(updated),
    TemporaryPassword: temporaryPassword,
    Snapshot: await buildSnapshot(),
  }));
}

async function handleDeactivateAccount(payload: GameApiRequest): Promise<Response> {
  const mentor = await requireMentor(payload, "メンター権限が必要です。");
  const userId = normalizeRequired(payload.UserId, "ユーザーを選択してください。");
  const user = await loadUserById(userId, true);
  if (!user) {
    throw new ApiError("not_found", "アカウントが見つかりません。", 404);
  }
  if (user.role !== "member") {
    throw new ApiError("forbidden", "メンバーだけ無効化できます。", 403);
  }

  const before = describeUser(user);
  const updated = user.is_active === false
    ? user
    : await patchUser(user.id, {
      is_active: false,
      ranking_visible: false,
    });

  await rest<unknown>(`battle_participants?${qs({ user_id: `eq.${user.id}` })}`, { method: "DELETE" }).catch(() => null);
  await recordAudit(mentor.id, "account.deactivate", "user", user.id, before, describeUser(updated));
  return json(okResponse({
    User: await userDto(updated),
    Snapshot: await buildSnapshot(),
  }));
}

async function handleStartSession(payload: GameApiRequest): Promise<Response> {
  const user = await requireMember(payload, "メンバーだけが開発ログを作成できます。");
  const active = await loadActiveSessionForUser(user.id);
  if (active) {
    throw new ApiError("forbidden", "進行中のセッションがあります。", 409);
  }

  const goal = payload.Goal?.trim() || "新しいガジェットを実装";
  const inserted = await rest<DbDevSession[]>("dev_sessions?select=" + devSessionSelect(), {
    method: "POST",
    prefer: "return=representation",
    body: {
      user_id: user.id,
      started_at: new Date().toISOString(),
      goal,
      status: "in_progress",
      suspicious_flags: [],
    },
  });
  const session = firstOrThrow(inserted, "開発セッションを開始できませんでした。");
  return json(okResponse({
    Session: sessionDto(session, null),
    Snapshot: await buildSnapshot(),
  }));
}

async function handleCompleteSession(payload: GameApiRequest): Promise<Response> {
  const user = await requireMember(payload, "開発ログを編集する権限が必要です。");
  const sessionId = normalizeRequired(payload.SessionId, "セッションが見つかりません。");
  const session = await loadSessionById(sessionId);
  if (!session || session.user_id !== user.id) {
    throw new ApiError("forbidden", "セッションが見つかりません。", 404);
  }

  if (!["in_progress", "incomplete"].includes(session.status)) {
    throw new ApiError("forbidden", "完了できるセッションではありません。", 409);
  }

  const endedAt = new Date().toISOString();
  const duration = Math.max(25, calculateDurationMinutes(session.started_at, endedAt));
  const completionPayload = {
    ...payload,
    Goal: payload.Goal?.trim() || session.goal,
  };
  const evaluation = await evaluateDevLogWithGemini(completionPayload);
  const status = "value" in evaluation ? "pending" : "ai_pending";
  const aiEvaluationFailureReason = "reason" in evaluation ? normalizeFailureReason(evaluation.reason) : null;
  const updated = await updateSession(session.id, {
    ended_at: endedAt,
    duration_minutes: duration,
    achievement_rate: clampScore(payload.AchievementRate ?? 0),
    reflection: payload.Reflection?.trim() || "実装の進め方と詰まりどころを整理した。",
    next_task: payload.NextTask?.trim() || "動作確認とUIフィードバックを改善する。",
    status,
    suspicious_flags: [],
    ai_evaluation_failure_reason: aiEvaluationFailureReason,
  });

  let evalRecord: DbAiEvaluation | null = null;
  if ("value" in evaluation) {
    evalRecord = await upsertAiEvaluation(session.id, evaluation.value);
  }

  return json(okResponse({
    Session: sessionDto(updated, evalRecord),
    Snapshot: await buildSnapshot(),
  }));
}

async function handleReviewSession(payload: GameApiRequest, approve: boolean): Promise<Response> {
  const mentor = await requireMentor(payload, "メンター権限が必要です。");
  const sessionId = normalizeRequired(payload.SessionId, "承認対象を選択してください。");
  const session = await loadSessionById(sessionId);
  if (!session) {
    throw new ApiError("forbidden", "承認対象が見つかりません。", 404);
  }

  if (!["pending", "needs_review", "ai_pending", "incomplete"].includes(session.status)) {
    throw new ApiError("forbidden", "承認待ちの開発ログではありません。", 409);
  }

  const before = describeSession(session);
  let evalRecord = await loadEvaluation(session.id);
  if (approve && !evalRecord) {
    evalRecord = await upsertAiEvaluation(session.id, fallbackEvaluation(session));
  }

  const updated = await updateSession(session.id, {
    status: approve ? "approved" : "rejected",
    approved_by: mentor.id,
    approved_at: new Date().toISOString(),
    mentor_comment: payload.Comment?.trim() || (approve ? "確認しました。正式EXPへ反映します。" : "却下しました。内容を見直してください。"),
    ai_evaluation_failure_reason: approve ? null : session.ai_evaluation_failure_reason,
  });

  if (approve) {
    await applyApprovedSessionGrowth(updated, evalRecord);
    await ensureScheduledBattle(mentor.id);
  }

  await recordAudit(mentor.id, approve ? "session.approve" : "session.reject", "session", session.id, before, describeSession(updated));
  return json(okResponse({
    Session: sessionDto(updated, evalRecord),
    Snapshot: await buildSnapshot(),
  }));
}

async function handleSubmitAchievement(payload: GameApiRequest): Promise<Response> {
  const user = await requireMember(payload, "メンバーだけが実績を申請できます。");
  const inserted = await rest<DbAchievement[]>("achievements?select=" + achievementSelect(), {
    method: "POST",
    prefer: "return=representation",
    body: {
      user_id: user.id,
      type: enumString(ACHIEVEMENT_TYPES, payload.AchievementType ?? 0),
      title: normalizeRequired(payload.Title, "実績名を入力してください。"),
      description: payload.Description?.trim() || "",
      status: "pending",
    },
  });
  const achievement = firstOrThrow(inserted, "実績を保存できませんでした。");
  return json(okResponse({
    Achievement: await achievementDto(achievement),
    Snapshot: await buildSnapshot(),
  }));
}

async function handleReviewAchievement(payload: GameApiRequest, approve: boolean): Promise<Response> {
  const mentor = await requireMentor(payload, "メンターだけが実績を承認できます。");
  const achievementId = normalizeRequired(payload.AchievementId, "実績を選択してください。");
  const achievement = await loadAchievement(achievementId);
  if (!achievement) {
    throw new ApiError("forbidden", "実績が見つかりません。", 404);
  }

  if (achievement.status !== "pending") {
    return json(okResponse({
      Achievement: await achievementDto(achievement),
      Snapshot: await buildSnapshot(),
    }));
  }

  const before = describeAchievement(achievement);
  const updated = firstOrThrow(await rest<DbAchievement[]>(
    `achievements?${qs({ id: `eq.${achievement.id}`, select: achievementSelect() })}`,
    {
      method: "PATCH",
      prefer: "return=representation",
      body: {
        status: approve ? "approved" : "rejected",
        approved_by: mentor.id,
        approved_at: new Date().toISOString(),
      },
    },
  ), "実績を更新できませんでした。");

  if (approve) {
    await applyAchievementReward(updated);
  }

  await recordAudit(mentor.id, approve ? "achievement.approve" : "achievement.reject", "achievement", updated.id, before, describeAchievement(updated));
  return json(okResponse({
    Achievement: await achievementDto(updated),
    Snapshot: await buildSnapshot(),
  }));
}

async function handleRegisterProduct(payload: GameApiRequest): Promise<Response> {
  const user = await requireMember(payload, "メンバーだけがプロダクトURLを登録できます。");
  const inserted = await rest<DbProduct[]>("products?select=" + productSelect(), {
    method: "POST",
    prefer: "return=representation",
    body: {
      user_id: user.id,
      title: normalizeRequired(payload.Title, "プロダクト名を入力してください。"),
      url: normalizeUrl(payload.Url),
      description: payload.Description?.trim() || "",
      is_public: true,
    },
  });
  const product = firstOrThrow(inserted, "プロダクトを登録できませんでした。");
  return json(okResponse({
    Product: productDto(product),
    Snapshot: await buildSnapshot(),
  }));
}

async function handleHideProduct(payload: GameApiRequest): Promise<Response> {
  const mentor = await requireMentor(payload, "メンターだけがプロダクトURLを非表示にできます。");
  const productId = normalizeRequired(payload.ProductId, "プロダクトを選択してください。");
  const product = await loadProduct(productId);
  if (!product) {
    throw new ApiError("forbidden", "プロダクトが見つかりません。", 404);
  }

  const before = describeProduct(product);
  const updated = firstOrThrow(await rest<DbProduct[]>(
    `products?${qs({ id: `eq.${product.id}`, select: productSelect() })}`,
    {
      method: "PATCH",
      prefer: "return=representation",
      body: {
        is_public: false,
        hidden_by: mentor.id,
      },
    },
  ), "プロダクトを更新できませんでした。");

  await recordAudit(mentor.id, "product.hide", "product", updated.id, before, describeProduct(updated));
  return json(okResponse({
    Product: productDto(updated),
    Snapshot: await buildSnapshot(),
  }));
}

async function handleRollCosmeticGacha(payload: GameApiRequest): Promise<Response> {
  const user = await requireMember(payload, "メンバーだけがガチャを引けます。");
  const stats = await loadStats(user.id);
  const approvedMinutes = await approvedDevMinutesForUser(user.id);
  const earnedRolls = Math.floor(approvedMinutes / 60);
  const titles = normalizeStringArray(stats.titles);
  const ownedCosmetics = titles.filter((title) => title.startsWith("cosmetic:"));
  const remainingRolls = earnedRolls - ownedCosmetics.length;
  if (remainingRolls <= 0) {
    throw new ApiError("forbidden", "ガチャ回数が足りません。承認済み開発1時間で1回引けます。", 403);
  }

  const candidates = COSMETIC_REWARDS.filter((reward) => !ownedCosmetics.includes(reward.id));
  if (candidates.length === 0) {
    throw new ApiError("forbidden", "獲得できるコスメはすべて入手済みです。", 409);
  }

  const random = new Uint32Array(1);
  crypto.getRandomValues(random);
  const reward = candidates[random[0] % candidates.length];
  titles.push(reward.id);
  addUnique(titles, `equipped:${reward.id}`);
  stats.titles = titles;
  await upsertStats(user.id, stats);
  await recordAudit(user.id, "cosmetic.gacha_roll", "user", user.id, `remainingRolls=${remainingRolls}`, `item=${reward.id}`);
  return json(okResponse({
    GachaResult: {
      ItemId: reward.id,
      Label: reward.label,
      RemainingRolls: Math.max(0, remainingRolls - 1),
    },
    Snapshot: await buildSnapshot(),
  }));
}

async function handleEquipCosmetic(payload: GameApiRequest): Promise<Response> {
  const user = await requireMember(payload, "メンバーだけがコスメを変更できます。");
  const itemId = normalizeRequired(payload.ItemId, "コスメを選択してください。");
  if (!COSMETIC_REWARDS.some((reward) => reward.id === itemId)) {
    throw new ApiError("forbidden", "存在しないコスメです。", 404);
  }

  const stats = await loadStats(user.id);
  const titles = normalizeStringArray(stats.titles);
  if (!titles.includes(itemId)) {
    throw new ApiError("forbidden", "まだ入手していないコスメです。", 403);
  }

  const equippedTag = `equipped:${itemId}`;
  const nextTitles = titles.filter((title) => title !== equippedTag);
  if (payload.Equipped !== false) {
    nextTitles.push(equippedTag);
  }

  stats.titles = nextTitles;
  await upsertStats(user.id, stats);
  await recordAudit(user.id, "cosmetic.equip", "user", user.id, `item=${itemId}`, `equipped=${payload.Equipped !== false}`);
  return json(okResponse({ Snapshot: await buildSnapshot() }));
}

async function handleStartBattle(payload: GameApiRequest): Promise<Response> {
  const mentor = await requireMentor(payload, "メンター権限が必要です。");
  let state = await loadLatestBattleState();
  if (!state || state.battle.status === "completed") {
    state = await createBattle("scheduled", mentor.id);
  }

  const updates = {
    created_by: mentor.id,
    started_at: new Date().toISOString(),
    completed_at: null,
    status: "active",
    phase: "turn_start",
    result: null,
    turn_number: 1,
    total_damage: 0,
    highlight_user_id: null,
    current_hp: state.battle.max_hp,
  };

  const battle = await patchBattle(state.battle.id, updates);
  await rest<unknown>(`battle_actions?${qs({ battle_id: `eq.${battle.id}` })}`, { method: "DELETE" });
  await rest<unknown>(`battle_participants?${qs({ battle_id: `eq.${battle.id}` })}`, { method: "DELETE" });
  await upsertBattleParticipants(await buildBattleParticipants(battle.id));

  return json(okResponse({ Snapshot: await buildSnapshot() }));
}

async function handleResetBattle(payload: GameApiRequest): Promise<Response> {
  const mentor = await requireMentor(payload, "メンター権限が必要です。");
  const state = await loadLatestBattleState();
  if (!state) {
    await createBattle("scheduled", mentor.id);
    return json(okResponse({ Snapshot: await buildSnapshot() }));
  }

  const battle = await patchBattle(state.battle.id, {
    created_by: mentor.id,
    started_at: null,
    completed_at: null,
    status: "scheduled",
    phase: "turn_start",
    result: null,
    turn_number: 1,
    total_damage: 0,
    highlight_user_id: null,
    current_hp: state.battle.max_hp,
  });
  await rest<unknown>(`battle_actions?${qs({ battle_id: `eq.${battle.id}` })}`, { method: "DELETE" });
  await rest<unknown>(`battle_participants?${qs({ battle_id: `eq.${battle.id}` })}`, { method: "DELETE" });
  await upsertBattleParticipants(await buildBattleParticipants(battle.id));
  return json(okResponse({ Snapshot: await buildSnapshot() }));
}

async function handleSetBossHp(payload: GameApiRequest): Promise<Response> {
  await requireMentor(payload, "メンターだけがボスHPを調整できます。");
  const state = await ensureLatestBattleState(payload);
  const multiplier = Math.max(0.1, Number.isFinite(payload.Multiplier) ? payload.Multiplier ?? 1 : 1);
  const baseHp = Math.max(1, state.battle.base_hp || state.battle.max_hp);
  const maxHp = Math.max(2500, Math.round(baseHp * multiplier));
  await patchBattle(state.battle.id, {
    hp_multiplier: multiplier,
    max_hp: maxHp,
    current_hp: state.battle.status === "scheduled" ? maxHp : Math.min(state.battle.current_hp, maxHp),
  });
  return json(okResponse({ Snapshot: await buildSnapshot() }));
}

async function handleSetBossConfig(payload: GameApiRequest): Promise<Response> {
  const mentor = await requireMentor(payload, "メンターだけがボス設定を変更できます。");
  const state = await ensureLatestBattleState(payload);
  const bossName = normalizeRequired(payload.BossName, "ボス名を入力してください。").slice(0, 32);
  const bossType = (payload.BossType?.trim() || state.battle.boss_type || "コードマスター").slice(0, 20);
  const storyTeaser = (payload.StoryTeaser?.trim() || state.battle.story_teaser || "").slice(0, 120);
  const before = `bossName=${state.battle.boss_name};bossType=${state.battle.boss_type};storyTeaser=${state.battle.story_teaser ?? ""}`;
  const updated = await patchBattle(state.battle.id, {
    boss_name: bossName,
    boss_type: bossType,
    story_teaser: storyTeaser,
    created_by: mentor.id,
  });
  await recordAudit(mentor.id, "battle.boss_config", "boss_battle", state.battle.id, before, `bossName=${updated.boss_name};bossType=${updated.boss_type};storyTeaser=${updated.story_teaser ?? ""}`);
  return json(okResponse({ Snapshot: await buildSnapshot() }));
}

async function handleBattleAction(payload: GameApiRequest): Promise<Response> {
  const user = await requireMember(payload, "ボス戦参加にはログインが必要です。");
  const state = await ensureLatestBattleState(payload);
  const actionTypeRequested = enumString(BATTLE_ACTION_TYPES, payload.ActionType ?? 0);
  if (state.battle.status !== "active") {
    return json(okResponse({
      ActionResult: battleActionResultDto({
        user_id: user.id,
        nickname: user.nickname,
        role: enumString(BATTLE_ROLES, payload.Role ?? 0),
        weapon_kind: enumString(WEAPON_KINDS, payload.Weapon ?? 0),
        action_type: actionTypeRequested,
        turn_number: state.battle.turn_number ?? 1,
        mp_cost: 0,
        damage: 0,
        heal: 0,
        support_effect: "メンターがゲーム開始するまで待機中です。",
        message: "メンターがゲーム開始するまで待機中です。",
        battle_id: state.battle.id,
      }),
      Snapshot: await buildSnapshot(),
    }));
  }

  const participant = state.participants.find((item) => item.user_id === user.id);
  if (!participant) {
    throw new ApiError("forbidden", "参加者が見つかりません。", 404);
  }

  let role = enumString(BATTLE_ROLES, payload.Role ?? 0);
  let weaponKind = enumString(WEAPON_KINDS, payload.Weapon ?? 0);
  if (!isWeaponUnlocked(participant.stats, weaponKind)) {
    weaponKind = "blade";
  }

  const weapon = resolveWeapon(weaponKind);
  let actionType = actionTypeRequested;
  let mpCost = getMpCost(actionType, weapon);
  if (Math.max(0, participant.current_mp) < mpCost) {
    actionType = "normal";
    mpCost = 0;
  }

  participant.role = role;
  participant.weapon_kind = weaponKind;
  participant.current_mp = Math.max(0, participant.current_mp - mpCost);

  const damageBefore = state.battle.current_hp;
  const potentialDamage = calculateDamage(participant.stats, weapon, state.battle, role, actionType);
  let heal = 0;
  let support = "";
  if (actionType === "support") {
    const supportResult = applySupport(state, participant, role);
    heal = supportResult.heal;
    support = supportResult.support;
  }

  if (actionType === "guard") {
    participant.current_mp = Math.min(statsMp(participant.stats), participant.current_mp + 6);
    support = "次ターンに備えてMPを回復";
  }

  const damage = applyBossDamage(state, participant, potentialDamage);
  participant.total_heal += heal;
  if (support) {
    participant.support_count += 1;
  }

  state.battle.highlight_user_id = user.id;
  const teamFollowUpDamage = state.battle.current_hp > 0 ? applyTeamFollowUp(state, user.id) : 0;
  const action = {
    battle_id: state.battle.id,
    user_id: user.id,
    turn_number: Math.max(1, state.battle.turn_number ?? 1),
    role,
    weapon_kind: weaponKind,
    action_type: actionType,
    mp_cost: mpCost,
    damage,
    heal,
    support_effect: support || {},
    message: buildActionMessage(participant.nickname, actionType, damage, heal, support, teamFollowUpDamage),
  };

  advanceTurnIfNeeded(state);
  await patchBattle(state.battle.id, {
    current_hp: state.battle.current_hp,
    boss_def: state.battle.boss_def ?? 4,
    total_damage: state.battle.total_damage ?? (damageBefore - state.battle.current_hp),
    turn_number: state.battle.turn_number,
    phase: state.battle.phase,
    status: state.battle.status,
    result: state.battle.result,
    completed_at: state.battle.completed_at,
    highlight_user_id: state.battle.highlight_user_id,
  });
  await upsertBattleParticipants(state.participants.map((item) => participantUpsertBody(item)));
  await upsertBattleAction(action);

  return json(okResponse({
    ActionResult: battleActionResultDto(action),
    Snapshot: await buildSnapshot(),
  }));
}

async function requireUser(payload: GameApiRequest): Promise<DbUser> {
  const userId = await verifySessionToken(payload.SessionToken);
  const user = await loadUserById(userId, true);
  if (!user || user.is_active === false) {
    throw new ApiError("invalid_session", "session expired", 401);
  }

  return user;
}

async function requireMentor(payload: GameApiRequest, message: string): Promise<DbUser> {
  const user = await requireUser(payload);
  if (user.role !== "mentor") {
    throw new ApiError("forbidden", message, 403);
  }

  return user;
}

async function requireMember(payload: GameApiRequest, message: string): Promise<DbUser> {
  const user = await requireUser(payload);
  if (user.role !== "member") {
    throw new ApiError("forbidden", message, 403);
  }

  return user;
}

async function loadUserByLoginId(loginId: string, withPassword = false): Promise<DbUser | null> {
  const users = await rest<DbUser[]>(`users?${qs({
    login_id: `eq.${loginId}`,
    select: userSelect(withPassword),
    limit: "1",
  })}`, { method: "GET" });
  return users[0] ?? null;
}

async function loadUserById(userId: string, withPassword = false): Promise<DbUser | null> {
  const users = await rest<DbUser[]>(`users?${qs({
    id: `eq.${userId}`,
    select: userSelect(withPassword),
    limit: "1",
  })}`, { method: "GET" });
  return users[0] ?? null;
}

async function patchUser(userId: string, body: Record<string, unknown>): Promise<DbUser> {
  return firstOrThrow(await rest<DbUser[]>(`users?${qs({ id: `eq.${userId}`, select: userSelect() })}`, {
    method: "PATCH",
    prefer: "return=representation",
    body,
  }), "ユーザーを更新できませんでした。");
}

async function loadTeams(): Promise<Map<string, DbTeam>> {
  const teams = await rest<DbTeam[]>("teams?select=id,code,name,mentor_user_id", { method: "GET" });
  return new Map(teams.map((team) => [team.id, team]));
}

async function ensureTeamId(teamCodeInput: string | undefined, role: string): Promise<string | null> {
  const code = normalizeTeamCode(teamCodeInput, role);
  const existing = await rest<DbTeam[]>(`teams?${qs({ code: `eq.${code}`, select: "id,code,name,mentor_user_id", limit: "1" })}`, { method: "GET" });
  if (existing[0]) {
    return existing[0].id;
  }

  const inserted = await rest<DbTeam[]>("teams?select=id,code,name,mentor_user_id", {
    method: "POST",
    prefer: "return=representation",
    body: {
      code,
      name: teamDisplayName(code),
    },
  });
  return firstOrThrow(inserted, "チームを作成できませんでした。").id;
}

async function loadStats(userId: string): Promise<DbCharacterStats> {
  const stats = await rest<DbCharacterStats[]>(`character_stats?${qs({ user_id: `eq.${userId}`, select: statsSelect(), limit: "1" })}`, { method: "GET" });
  if (stats[0]) {
    return ensureGrowthUnlocks(stats[0]);
  }

  return await upsertStats(userId, ensureGrowthUnlocks(defaultStats(userId)));
}

async function upsertStats(userId: string, stats: DbCharacterStats): Promise<DbCharacterStats> {
  const body = {
    user_id: userId,
    level: Math.max(1, Math.round(stats.level ?? 1)),
    exp: Math.max(0, Math.round(stats.exp ?? 0)),
    unlocked_weapons: normalizeNumberArray(stats.unlocked_weapons, [0]),
    titles: normalizeStringArray(stats.titles),
    skills: normalizeStringArray(stats.skills, ["基礎攻撃"]),
  };
  const rows = await rest<DbCharacterStats[]>(`character_stats?${qs({ on_conflict: "user_id", select: statsSelect() })}`, {
    method: "POST",
    prefer: "resolution=merge-duplicates,return=representation",
    body,
  });
  return ensureGrowthUnlocks(firstOrThrow(rows, "ステータスを保存できませんでした。"));
}

async function loadActiveSessionForUser(userId: string): Promise<DbDevSession | null> {
  const sessions = await rest<DbDevSession[]>(`dev_sessions?${qs({
    user_id: `eq.${userId}`,
    status: "in.(in_progress,incomplete)",
    select: devSessionSelect(),
    order: "started_at.desc",
    limit: "1",
  })}`, { method: "GET" });
  return sessions[0] ?? null;
}

async function approvedDevMinutesForUser(userId: string): Promise<number> {
  const sessions = await rest<DbDevSession[]>(`dev_sessions?${qs({
    user_id: `eq.${userId}`,
    status: "eq.approved",
    select: "duration_minutes",
  })}`, { method: "GET" });
  return sessions.reduce((sum, session) => sum + Math.max(0, Math.round(session.duration_minutes ?? 0)), 0);
}

async function loadSessionById(sessionId: string): Promise<DbDevSession | null> {
  const sessions = await rest<DbDevSession[]>(`dev_sessions?${qs({ id: `eq.${sessionId}`, select: devSessionSelect(), limit: "1" })}`, { method: "GET" });
  return sessions[0] ?? null;
}

async function updateSession(sessionId: string, body: Record<string, unknown>): Promise<DbDevSession> {
  return firstOrThrow(await rest<DbDevSession[]>(`dev_sessions?${qs({ id: `eq.${sessionId}`, select: devSessionSelect() })}`, {
    method: "PATCH",
    prefer: "return=representation",
    body,
  }), "開発ログを更新できませんでした。");
}

async function loadEvaluation(sessionId: string): Promise<DbAiEvaluation | null> {
  const evaluations = await rest<DbAiEvaluation[]>(`ai_evaluations?${qs({
    dev_session_id: `eq.${sessionId}`,
    select: aiEvaluationSelect(),
    limit: "1",
  })}`, { method: "GET" });
  return evaluations[0] ?? null;
}

async function upsertAiEvaluation(sessionId: string, evaluation: ValidatedEvaluation): Promise<DbAiEvaluation> {
  const rows = await rest<DbAiEvaluation[]>(`ai_evaluations?${qs({
    on_conflict: "dev_session_id",
    select: aiEvaluationSelect(),
  })}`, {
    method: "POST",
    prefer: "resolution=merge-duplicates,return=representation",
    body: {
      dev_session_id: sessionId,
      total_score: evaluation.totalScore,
      rank: rankLabelFromScore(evaluation.totalScore),
      axis_scores: toAxisScoreRecord(evaluation),
      feedback: evaluation.feedback,
      exp_multiplier: multiplierFromRank(rankFromScore(evaluation.totalScore)),
      model_name: currentGeminiModel(),
    },
  });
  return firstOrThrow(rows, "AI評価を保存できませんでした。");
}

async function loadProduct(productId: string): Promise<DbProduct | null> {
  const products = await rest<DbProduct[]>(`products?${qs({ id: `eq.${productId}`, select: productSelect(), limit: "1" })}`, { method: "GET" });
  return products[0] ?? null;
}

async function loadAchievement(achievementId: string): Promise<DbAchievement | null> {
  const achievements = await rest<DbAchievement[]>(`achievements?${qs({ id: `eq.${achievementId}`, select: achievementSelect(), limit: "1" })}`, { method: "GET" });
  return achievements[0] ?? null;
}

async function buildSnapshot(): Promise<Record<string, unknown>> {
  const [teamsById, users, stats, sessions, evaluations, products, achievements, auditLogs, activeBattle] = await Promise.all([
    loadTeams(),
    rest<DbUser[]>(`users?${qs({ is_active: "eq.true", select: userSelect(), order: "created_at.asc" })}`, { method: "GET" }),
    rest<DbCharacterStats[]>(`character_stats?${qs({ select: statsSelect() })}`, { method: "GET" }),
    rest<DbDevSession[]>(`dev_sessions?${qs({ select: devSessionSelect(), order: "started_at.desc" })}`, { method: "GET" }),
    rest<DbAiEvaluation[]>(`ai_evaluations?${qs({ select: aiEvaluationSelect() })}`, { method: "GET" }),
    rest<DbProduct[]>(`products?${qs({ select: productSelect(), order: "created_at.desc" })}`, { method: "GET" }),
    rest<DbAchievement[]>(`achievements?${qs({ select: achievementSelect(), order: "created_at.desc" })}`, { method: "GET" }),
    rest<DbAuditLog[]>(`audit_logs?${qs({ select: auditLogSelect(), order: "created_at.desc", limit: "50" })}`, { method: "GET" }).catch(() => []),
    loadLatestBattleState(),
  ]);
  const evaluationsBySession = new Map(evaluations.map((evaluation) => [evaluation.dev_session_id, evaluation]));
  return {
    Users: await Promise.all(users.filter((user) => !isLegacyDemoMember(user)).map((user) => userDto(user, teamsById))),
    Stats: stats.map((item) => statsDto(ensureGrowthUnlocks(item))),
    Weapons: weapons().map(weaponDto),
    Sessions: sessions.map((session) => sessionDto(session, evaluationsBySession.get(session.id) ?? null)),
    Products: products.map(productDto),
    Achievements: await Promise.all(achievements.map((achievement) => achievementDto(achievement))),
    AuditLogs: auditLogs.map(auditLogDto),
    ActiveBattle: activeBattle ? battleDto(activeBattle) : null,
  };
}

async function ensureLatestBattleState(payload: GameApiRequest): Promise<BattleState> {
  const state = await loadLatestBattleState();
  if (state) {
    return state;
  }

  const mentor = await requireMentor(payload, "メンター権限が必要です。");
  return createBattle("scheduled", mentor.id);
}

async function ensureScheduledBattle(creatorId: string): Promise<BattleState | null> {
  const state = await loadLatestBattleState();
  if (state && state.battle.status !== "completed") {
    return state;
  }

  const mentor = await loadUserById(creatorId);
  if (!mentor || mentor.role !== "mentor") {
    return null;
  }

  return createBattle("scheduled", creatorId);
}

async function loadLatestBattleState(): Promise<BattleState | null> {
  const battles = await rest<DbBossBattle[]>(`boss_battles?${qs({
    select: battleSelect(),
    order: "created_at.desc",
    limit: "1",
  })}`, { method: "GET" });
  const battle = battles[0];
  if (!battle) {
    return null;
  }

  const [participants, actions] = await Promise.all([
    rest<DbBattleParticipant[]>(`battle_participants?${qs({
      battle_id: `eq.${battle.id}`,
      select: battleParticipantSelect(),
      order: "created_at.asc",
    })}`, { method: "GET" }),
    rest<DbBattleAction[]>(`battle_actions?${qs({
      battle_id: `eq.${battle.id}`,
      select: battleActionSelect(),
      order: "turn_number.asc,created_at.asc",
    })}`, { method: "GET" }),
  ]);
  const syncedParticipants = await syncBattleParticipantsWithActiveMembers(battle, participants);
  const participantStates = await Promise.all(syncedParticipants.map(async (participant) => ({
    ...participant,
    stats: await loadStats(participant.user_id),
  })));
  return { battle, participants: participantStates, actions };
}

async function createBattle(status: string, creatorId: string): Promise<BattleState> {
  const creator = await loadUserById(creatorId);
  if (!creator || creator.role !== "mentor") {
    throw new ApiError("forbidden", "メンター権限が必要です。", 403);
  }

  const [approvedSessions, existingBattles] = await Promise.all([
    rest<DbDevSession[]>(`dev_sessions?${qs({ status: "eq.approved", select: devSessionSelect() })}`, { method: "GET" }),
    rest<Array<{ id: string }>>(`boss_battles?${qs({ select: "id" })}`, { method: "GET" }).catch(() => []),
  ]);
  const approvedWeight = Math.max(1000, approvedSessions.reduce((sum, session) => sum + Math.max(0, session.duration_minutes ?? 0), 0));
  const maxHp = Math.max(2500, Math.round(approvedWeight * 2.5));
  const bossIndex = Math.max(0, existingBattles.length) % MENTOR_NAMES.length;
  const bossName = `メンター・${MENTOR_NAMES[bossIndex]}`;
  const createdAt = new Date().toISOString();
  const battle = firstOrThrow(await rest<DbBossBattle[]>(`boss_battles?select=${battleSelect()}`, {
    method: "POST",
    prefer: "return=representation",
    body: {
      week_start_date: weekStartDate(createdAt),
      boss_id: `boss-${bossIndex + 1}`,
      boss_name: bossName,
      boss_type: "コードマスター",
      boss_def: 4 + bossIndex,
      story_teaser: "なぜメンターが襲ってくるのか。次の勝利で記録断片が解放される。",
      base_hp: maxHp,
      hp_multiplier: 1,
      current_hp: maxHp,
      max_hp: maxHp,
      turn_count: 3,
      turn_number: 1,
      status,
      phase: "turn_start",
      total_damage: 0,
      created_by: creatorId,
      created_at: createdAt,
    },
  }), "ボス戦を作成できませんでした。");

  const participants = await buildBattleParticipants(battle.id);
  await upsertBattleParticipants(participants);
  return await loadLatestBattleState() ?? { battle, participants: [], actions: [] };
}

async function buildBattleParticipants(battleId: string): Promise<Record<string, unknown>[]> {
  return buildBattleParticipantsForMembers(battleId, await loadBattleEligibleMemberUsers());
}

async function loadActiveMemberUsers(): Promise<DbUser[]> {
  return await rest<DbUser[]>(
    `users?${qs({ role: "eq.member", is_active: "eq.true", select: userSelect(), order: "created_at.asc" })}`,
    { method: "GET" },
  );
}

async function loadBattleEligibleMemberUsers(): Promise<DbUser[]> {
  const users = await loadActiveMemberUsers();
  return users.filter((user) => !isLegacyDemoMember(user));
}

function isLegacyDemoMember(user: DbUser): boolean {
  return user.role === "member" && LEGACY_DEMO_MEMBERS.get(user.login_id) === user.nickname;
}

function isLegacyDemoNickname(nickname: string): boolean {
  return [...LEGACY_DEMO_MEMBERS.values()].includes(nickname);
}

async function buildBattleParticipantsForMembers(battleId: string, members: DbUser[]): Promise<Record<string, unknown>[]> {
  const roles = ["attacker", "healer", "defender", "supporter", "attacker", "supporter"];
  const weaponKinds = ["blade", "rifle", "shield", "debug_tool", "cannon", "rifle"];
  const participants: Record<string, unknown>[] = [];
  for (let index = 0; index < members.length; index++) {
    const member = members[index];
    const stats = await loadStats(member.id);
    participants.push({
      battle_id: battleId,
      user_id: member.id,
      nickname: member.nickname,
      role: roles[index % roles.length],
      weapon_kind: weaponKinds[index % weaponKinds.length],
      current_hp: statsHp(stats),
      current_mp: statsMp(stats),
      total_damage: 0,
      total_heal: 0,
      support_count: 0,
    });
  }

  return participants;
}

async function syncBattleParticipantsWithActiveMembers(
  battle: DbBossBattle,
  participants: DbBattleParticipant[],
): Promise<DbBattleParticipant[]> {
  if (battle.status === "completed") {
    return participants;
  }

  const members = await loadBattleEligibleMemberUsers();
  const activeMemberIds = new Set(members.map((member) => member.id));
  const existingParticipantIds = new Set(participants.map((participant) => participant.user_id));
  const expectedParticipants = await buildBattleParticipantsForMembers(battle.id, members);
  const missingParticipants = expectedParticipants.filter((participant) => {
    const userId = typeof participant.user_id === "string" ? participant.user_id : "";
    return userId !== "" && !existingParticipantIds.has(userId);
  });
  const obsoleteParticipantIds = participants
    .filter((participant) => !isLegacyDemoNickname(participant.nickname))
    .map((participant) => participant.user_id)
    .filter((userId) => userId && !activeMemberIds.has(userId));

  if (missingParticipants.length === 0 && obsoleteParticipantIds.length === 0) {
    return participants;
  }

  if (missingParticipants.length > 0) {
    await upsertBattleParticipants(missingParticipants);
  }

  if (obsoleteParticipantIds.length > 0) {
    await rest<unknown>(`battle_participants?${qs({
      battle_id: `eq.${battle.id}`,
      user_id: `in.(${obsoleteParticipantIds.join(",")})`,
    })}`, { method: "DELETE" });
  }

  return await rest<DbBattleParticipant[]>(`battle_participants?${qs({
    battle_id: `eq.${battle.id}`,
    select: battleParticipantSelect(),
    order: "created_at.asc",
  })}`, { method: "GET" });
}

async function patchBattle(battleId: string, body: Record<string, unknown>): Promise<DbBossBattle> {
  return firstOrThrow(await rest<DbBossBattle[]>(`boss_battles?${qs({ id: `eq.${battleId}`, select: battleSelect() })}`, {
    method: "PATCH",
    prefer: "return=representation",
    body,
  }), "ボス戦を更新できませんでした。");
}

async function upsertBattleParticipants(participants: Record<string, unknown>[]): Promise<void> {
  if (participants.length === 0) {
    return;
  }

  await rest<unknown>(`battle_participants?${qs({ on_conflict: "battle_id,user_id" })}`, {
    method: "POST",
    prefer: "resolution=merge-duplicates",
    body: participants,
  });
}

async function upsertBattleAction(action: Record<string, unknown>): Promise<void> {
  await rest<unknown>(`battle_actions?${qs({ on_conflict: "battle_id,user_id,turn_number" })}`, {
    method: "POST",
    prefer: "resolution=merge-duplicates",
    body: action,
  });
}

function participantUpsertBody(item: BattleParticipantState): Record<string, unknown> {
  return {
    battle_id: item.battle_id,
    user_id: item.user_id,
    nickname: item.nickname,
    role: item.role,
    weapon_kind: item.weapon_kind,
    current_hp: Math.max(0, item.current_hp),
    current_mp: Math.max(0, item.current_mp),
    total_damage: Math.max(0, item.total_damage),
    total_heal: Math.max(0, item.total_heal),
    support_count: Math.max(0, item.support_count),
  };
}

async function applyApprovedSessionGrowth(session: DbDevSession, evaluation: DbAiEvaluation | null): Promise<void> {
  const stats = await loadStats(session.user_id);
  const rank = rankFromLabel(evaluation?.rank ?? "C");
  const expGained = Math.round(Math.max(0, session.duration_minutes ?? 0) * multiplierFromRank(rank));
  addExp(stats, expGained);
  await upsertStats(session.user_id, ensureGrowthUnlocks(stats));
}

async function applyAchievementReward(achievement: DbAchievement): Promise<void> {
  const stats = await loadStats(achievement.user_id);
  const titles = normalizeStringArray(stats.titles);
  const skills = normalizeStringArray(stats.skills);
  const unlocked = normalizeNumberArray(stats.unlocked_weapons, [0]);
  const rewardWeapon = rewardWeaponForAchievement(achievement.type);
  if (rewardWeapon != null && !unlocked.includes(rewardWeapon)) {
    unlocked.push(rewardWeapon);
  }

  addUnique(titles, rewardTitle(achievement.type));
  addUnique(skills, rewardSkill(achievement.type));
  stats.unlocked_weapons = unlocked;
  stats.titles = titles;
  stats.skills = skills;
  await upsertStats(achievement.user_id, stats);
}

function applyBossDamage(state: BattleState, participant: BattleParticipantState, potentialDamage: number): number {
  const damage = Math.min(Math.max(0, Math.round(potentialDamage)), Math.max(0, state.battle.current_hp));
  state.battle.current_hp = Math.max(0, state.battle.current_hp - damage);
  state.battle.total_damage = Math.max(0, (state.battle.total_damage ?? 0) + damage);
  participant.total_damage += damage;
  return damage;
}

function applySupport(state: BattleState, actor: BattleParticipantState, role: string): { heal: number; support: string } {
  if (role === "healer") {
    const healPool = Math.max(state.participants.length, Math.round(statsMp(actor.stats) * 0.8));
    const healPerParticipant = Math.max(1, Math.ceil(healPool / Math.max(1, state.participants.length)));
    let heal = 0;
    for (const participant of state.participants) {
      const before = participant.current_hp;
      participant.current_hp = Math.min(statsHp(participant.stats), participant.current_hp + healPerParticipant);
      heal += participant.current_hp - before;
    }

    return { heal, support: heal > 0 ? `チームHPを${heal}回復` : "チームHPは満タン" };
  }

  if (role === "defender") {
    let restoredMp = 0;
    for (const participant of state.participants) {
      const before = participant.current_mp;
      participant.current_mp = Math.min(statsMp(participant.stats), participant.current_mp + 2);
      restoredMp += participant.current_mp - before;
    }

    return { heal: 0, support: restoredMp > 0 ? `味方を保護しMPを${restoredMp}補助` : "味方を保護" };
  }

  if (role === "supporter") {
    const beforeDef = state.battle.boss_def ?? 4;
    state.battle.boss_def = Math.max(0, beforeDef - 1);
    return { heal: 0, support: beforeDef > state.battle.boss_def ? `敵DEF ${beforeDef}->${state.battle.boss_def}` : "敵DEFは最低値" };
  }

  return { heal: 0, support: "" };
}

function applyTeamFollowUp(state: BattleState, userId: string): number {
  let totalDamage = 0;
  for (const participant of state.participants) {
    if (participant.user_id === userId || participant.current_hp <= 0 || state.battle.current_hp <= 0) {
      continue;
    }

    const roleMultiplier = participant.role === "attacker"
      ? 1.05
      : participant.role === "supporter"
      ? 0.72
      : participant.role === "defender"
      ? 0.62
      : participant.role === "healer"
      ? 0.48
      : 0.7;
    const weapon = resolveWeapon(participant.weapon_kind);
    const potentialDamage = Math.max(1, Math.round(statsAtk(participant.stats) * roleMultiplier * weapon.damageMultiplier - (state.battle.boss_def ?? 4)));
    totalDamage += applyBossDamage(state, participant, potentialDamage);
    if (participant.role === "healer") {
      participant.total_heal += 4;
      participant.support_count += 1;
    } else if (participant.role === "supporter") {
      participant.support_count += 1;
    }
  }

  return totalDamage;
}

function advanceTurnIfNeeded(state: BattleState): void {
  if (state.battle.current_hp <= 0) {
    completeBattle(state, "win");
    return;
  }

  state.battle.phase = "result";
  state.battle.turn_number = Math.max(1, state.battle.turn_number ?? 1) + 1;
  if (state.battle.turn_number > state.battle.turn_count) {
    completeBattle(state, "lose");
    return;
  }

  state.battle.phase = "turn_start";
}

function completeBattle(state: BattleState, result: string): void {
  state.battle.result = result;
  state.battle.phase = "completed";
  state.battle.status = "completed";
  state.battle.completed_at = state.battle.completed_at ?? new Date().toISOString();
}

async function buildPublicSessionSecret(): Promise<CryptoKey> {
  const secret = Deno.env.get("SUPABASE_GAME_SESSION_SECRET")?.trim() || supabaseDbConfig().serviceKey;
  return await crypto.subtle.importKey(
    "raw",
    TEXT_ENCODER.encode(secret),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign", "verify"],
  );
}

async function createSessionToken(userId: string): Promise<string> {
  const now = Math.floor(Date.now() / 1000);
  const payload = base64UrlEncode(JSON.stringify({
    sub: userId,
    iat: now,
    exp: now + SESSION_TTL_SECONDS,
  }));
  const key = await buildPublicSessionSecret();
  const signature = await crypto.subtle.sign("HMAC", key, TEXT_ENCODER.encode(payload));
  return `${payload}.${base64UrlEncodeBytes(new Uint8Array(signature))}`;
}

async function verifySessionToken(sessionToken?: string): Promise<string> {
  const token = sessionToken?.trim() ?? "";
  const [payload, signature] = token.split(".");
  if (!payload || !signature) {
    throw new ApiError("invalid_session", "session token is invalid", 401);
  }

  const key = await buildPublicSessionSecret();
  const expected = await crypto.subtle.sign("HMAC", key, TEXT_ENCODER.encode(payload));
  if (base64UrlEncodeBytes(new Uint8Array(expected)) !== signature) {
    throw new ApiError("invalid_session", "session token is invalid", 401);
  }

  const parsed = JSON.parse(base64UrlDecode(payload)) as { sub?: string; exp?: number };
  if (!parsed.sub || !parsed.exp || parsed.exp < Math.floor(Date.now() / 1000)) {
    throw new ApiError("auth_expired", "session expired", 401);
  }

  return parsed.sub;
}

async function rest<T>(
  pathAndQuery: string,
  init: { method: string; prefer?: string; body?: unknown },
): Promise<T> {
  const config = supabaseDbConfig();
  const headers: Record<string, string> = {
    apikey: config.serviceKey,
  };

  if (!config.serviceKey.startsWith("sb_secret_")) {
    headers.Authorization = `Bearer ${config.serviceKey}`;
  }

  if (init.body !== undefined) {
    headers["Content-Type"] = "application/json";
  }

  if (init.prefer) {
    headers.Prefer = init.prefer;
  }

  const response = await fetch(`${config.url}/rest/v1/${pathAndQuery}`, {
    method: init.method,
    headers,
    body: init.body !== undefined ? JSON.stringify(init.body) : undefined,
  });

  if (!response.ok) {
    const detail = await response.text();
    throw new ApiError("server_error", `Supabase ${response.status}: ${detail.slice(0, 240)}`, 500);
  }

  if (response.status === 204) {
    return null as T;
  }

  const text = await response.text();
  return (text ? JSON.parse(text) : null) as T;
}

function supabaseDbConfig(): SupabaseDbConfig {
  const url = Deno.env.get("SUPABASE_URL")?.trim().replace(/\/+$/, "");
  const serviceKey = readNamedSecret("SUPABASE_SECRET_KEYS", "default")
    || Deno.env.get("SUPABASE_SECRET_KEY")?.trim()
    || Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")?.trim();

  if (!url || !serviceKey) {
    throw new ApiError("server_error", "Supabase persistence is not configured", 500);
  }

  return { url, serviceKey };
}

function readNamedSecret(envName: string, keyName: string): string {
  const raw = Deno.env.get(envName)?.trim();
  if (!raw) {
    return "";
  }

  try {
    const parsed = JSON.parse(raw) as Record<string, unknown>;
    const value = parsed[keyName];
    return typeof value === "string" ? value.trim() : "";
  } catch (_error) {
    return "";
  }
}

async function evaluateDevLogWithGemini(
  payload: GameApiRequest,
): Promise<{ ok: true; value: ValidatedEvaluation } | { ok: false; reason: string }> {
  const apiKey = Deno.env.get("GEMINI_API_KEY")?.trim();
  if (!apiKey) {
    return { ok: false, reason: "GEMINI_API_KEY is not configured" };
  }

  const model = currentGeminiModel();
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

function json(body: GameApiResponse, status = body.Ok ? 200 : 500): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      ...CORS_HEADERS,
      "Content-Type": "application/json; charset=utf-8",
    },
  });
}

function userSelect(withPassword = false): string {
  return [
    "id",
    "login_id",
    withPassword ? "password_hash" : "",
    "nickname",
    "role",
    "team_id",
    "ranking_visible",
    "initial_password_changed",
    "is_active",
  ].filter(Boolean).join(",");
}

function statsSelect(): string {
  return "user_id,level,exp,hp,atk,def,mp,unlocked_weapons,titles,skills";
}

function devSessionSelect(): string {
  return "id,user_id,started_at,ended_at,duration_minutes,goal,achievement_rate,reflection,next_task,status,suspicious_flags,mentor_comment,approved_by,approved_at,ai_evaluation_failure_reason";
}

function aiEvaluationSelect(): string {
  return "dev_session_id,total_score,rank,axis_scores,feedback,exp_multiplier,model_name";
}

function productSelect(): string {
  return "id,user_id,title,url,description,is_public,hidden_by,created_at";
}

function achievementSelect(): string {
  return "id,user_id,type,title,description,status,approved_by,approved_at,created_at";
}

function auditLogSelect(): string {
  return "id,actor_user_id,action_type,target_type,target_id,before_state,after_state,created_at";
}

function battleSelect(): string {
  return "id,week_start_date,boss_id,boss_name,boss_type,boss_def,story_teaser,base_hp,hp_multiplier,current_hp,max_hp,turn_count,turn_number,phase,status,result,total_damage,highlight_user_id,created_by,created_at,started_at,completed_at";
}

function battleParticipantSelect(): string {
  return "battle_id,user_id,nickname,role,weapon_kind,current_hp,current_mp,total_damage,total_heal,support_count";
}

function battleActionSelect(): string {
  return "battle_id,user_id,turn_number,role,weapon_kind,action_type,mp_cost,damage,heal,support_effect,message,created_at";
}

async function userDto(user: DbUser, teamsById?: Map<string, DbTeam>): Promise<Record<string, unknown>> {
  const teams = teamsById ?? await loadTeams();
  const team = user.team_id ? teams.get(user.team_id) : null;
  return {
    Id: user.id,
    LoginId: user.login_id,
    Nickname: user.nickname,
    Role: enumIndex(USER_ROLES, user.role),
    TeamId: team?.code ?? normalizeTeamCode(team?.name, user.role),
    RankingVisible: user.ranking_visible !== false,
    InitialPasswordChanged: user.initial_password_changed !== false,
    IsActive: user.is_active !== false,
  };
}

function statsDto(stats: DbCharacterStats): Record<string, unknown> {
  const normalized = ensureGrowthUnlocks(stats);
  return {
    UserId: normalized.user_id,
    Level: Math.max(1, Math.round(normalized.level ?? 1)),
    Exp: Math.max(0, Math.round(normalized.exp ?? 0)),
    Hp: statsHp(normalized),
    Atk: statsAtk(normalized),
    Def: statsDef(normalized),
    Mp: statsMp(normalized),
    UnlockedWeapons: normalizeNumberArray(normalized.unlocked_weapons, [0]),
    Titles: normalizeStringArray(normalized.titles),
    Skills: normalizeStringArray(normalized.skills, ["基礎攻撃"]),
  };
}

function weaponDto(weapon: WeaponDefinition): Record<string, unknown> {
  return {
    Kind: weapon.kind,
    DisplayName: weapon.displayName,
    Description: weapon.description,
    MpEfficiencyBonus: weapon.mpEfficiencyBonus,
    DamageMultiplier: weapon.damageMultiplier,
    PreferredRole: weapon.preferredRole,
    IsSpecial: weapon.isSpecial === true,
  };
}

function sessionDto(session: DbDevSession, evaluation: DbAiEvaluation | null | undefined): Record<string, unknown> {
  return {
    Id: session.id,
    UserId: session.user_id,
    StartedAtUtc: session.started_at,
    EndedAtUtc: session.ended_at ?? "",
    DurationMinutes: Math.max(0, Math.round(session.duration_minutes ?? 0)),
    Goal: session.goal,
    AchievementRate: Math.max(0, Math.round(session.achievement_rate ?? 0)),
    Reflection: session.reflection ?? "",
    NextTask: session.next_task ?? "",
    Status: enumIndex(SESSION_STATUSES, session.status),
    SuspiciousFlags: normalizeStringArray(session.suspicious_flags),
    MentorComment: session.mentor_comment ?? "",
    ApprovedBy: session.approved_by ?? "",
    ApprovedAtUtc: session.approved_at ?? "",
    AiEvaluationFailureReason: session.ai_evaluation_failure_reason ?? "",
    Evaluation: evaluation ? aiEvaluationDto(evaluation) : null,
  };
}

function aiEvaluationDto(evaluation: DbAiEvaluation): Record<string, unknown> {
  const axis = typeof evaluation.axis_scores === "object" && evaluation.axis_scores !== null
    ? evaluation.axis_scores as Record<string, unknown>
    : {};
  return {
    TotalScore: Math.max(0, Math.round(evaluation.total_score ?? 0)),
    Rank: rankFromLabel(evaluation.rank),
    GoalScore: Math.max(0, Math.round(Number(axis.goal_achievement ?? 0))),
    SpecificityScore: Math.max(0, Math.round(Number(axis.specificity ?? 0))),
    LearningScore: Math.max(0, Math.round(Number(axis.learning ?? 0))),
    NextActionScore: Math.max(0, Math.round(Number(axis.next_action ?? 0))),
    ContinuityScore: Math.max(0, Math.round(Number(axis.continuity ?? 0))),
    ExpMultiplier: Number(evaluation.exp_multiplier ?? multiplierFromRank(rankFromLabel(evaluation.rank))),
    Feedback: evaluation.feedback ?? "",
    ModelName: evaluation.model_name ?? currentGeminiModel(),
  };
}

function productDto(product: DbProduct): Record<string, unknown> {
  return {
    Id: product.id,
    UserId: product.user_id,
    Title: product.title,
    Url: product.url,
    Description: product.description ?? "",
    IsPublic: product.is_public !== false,
    HiddenBy: product.hidden_by ?? "",
    CreatedAtUtc: product.created_at,
  };
}

async function achievementDto(achievement: DbAchievement): Promise<Record<string, unknown>> {
  const rewardWeapon = rewardWeaponForAchievement(achievement.type);
  return {
    Id: achievement.id,
    UserId: achievement.user_id,
    Type: enumIndex(ACHIEVEMENT_TYPES, achievement.type),
    Title: achievement.title,
    Description: achievement.description ?? "",
    Status: enumIndex(ACHIEVEMENT_STATUSES, achievement.status),
    ApprovedBy: achievement.approved_by ?? "",
    ApprovedAtUtc: achievement.approved_at ?? "",
    CreatedAtUtc: achievement.created_at,
    HasRewardWeapon: achievement.status === "approved" && rewardWeapon != null,
    RewardWeapon: rewardWeapon ?? 0,
    RewardTitle: achievement.status === "approved" ? rewardTitle(achievement.type) : "",
    RewardSkill: achievement.status === "approved" ? rewardSkill(achievement.type) : "",
  };
}

function auditLogDto(log: DbAuditLog): Record<string, unknown> {
  return {
    Id: log.id,
    ActorUserId: log.actor_user_id ?? "",
    ActionType: log.action_type,
    TargetType: log.target_type,
    TargetId: log.target_id ?? "",
    Before: log.before_state ?? "",
    After: log.after_state ?? "",
    CreatedAtUtc: log.created_at,
  };
}

function battleDto(state: BattleState): Record<string, unknown> {
  const participants = state.participants.filter((participant) => !isLegacyDemoNickname(participant.nickname));
  const participantIds = new Set(participants.map((participant) => participant.user_id));
  return {
    Id: state.battle.id,
    Boss: {
      Id: state.battle.boss_id ?? "remote-boss",
      Name: state.battle.boss_name,
      BossType: state.battle.boss_type,
      MaxHp: state.battle.max_hp,
      CurrentHp: state.battle.current_hp,
      Def: state.battle.boss_def ?? 4,
      StoryTeaser: state.battle.story_teaser ?? "",
    },
    Participants: participants.map((participant) => ({
      UserId: participant.user_id,
      Nickname: participant.nickname,
      Stats: statsDto(participant.stats),
      Role: enumIndex(BATTLE_ROLES, participant.role),
      Weapon: enumIndex(WEAPON_KINDS, participant.weapon_kind),
      CurrentHp: participant.current_hp,
      CurrentMp: participant.current_mp,
      TotalDamage: participant.total_damage,
      TotalHeal: participant.total_heal,
      SupportCount: participant.support_count,
    })),
    WeekStartDate: state.battle.week_start_date,
    BaseHp: state.battle.base_hp,
    HpMultiplier: Number(state.battle.hp_multiplier ?? 1),
    Status: enumIndex(BATTLE_STATUSES, state.battle.status),
    TurnNumber: Math.max(1, state.battle.turn_number ?? 1),
    TurnCount: Math.max(1, state.battle.turn_count),
    Phase: enumIndex(BATTLE_PHASES, state.battle.phase ?? "turn_start"),
    Outcome: state.battle.result === "win" ? 1 : state.battle.result === "lose" ? 2 : 0,
    Result: state.battle.result ?? "",
    TotalDamage: participants.reduce((sum, participant) => sum + Math.max(0, Math.round(participant.total_damage ?? 0)), 0),
    HighlightUserId: state.battle.highlight_user_id ?? "",
    CreatedBy: state.battle.created_by,
    CreatedAtUtc: state.battle.created_at,
    StartedAtUtc: state.battle.started_at ?? "",
    CompletedAtUtc: state.battle.completed_at ?? "",
    Actions: state.actions.filter((action) => participantIds.has(action.user_id)).map(battleActionResultDto),
  };
}

function battleActionResultDto(action: Partial<DbBattleAction> & { nickname?: string }): Record<string, unknown> {
  return {
    UserId: action.user_id ?? "",
    Nickname: action.nickname ?? "",
    Role: enumIndex(BATTLE_ROLES, action.role ?? "attacker"),
    Weapon: enumIndex(WEAPON_KINDS, action.weapon_kind ?? "blade"),
    ActionType: enumIndex(BATTLE_ACTION_TYPES, action.action_type ?? "normal"),
    TurnNumber: Math.max(1, Math.round(action.turn_number ?? 1)),
    MpCost: Math.max(0, Math.round(action.mp_cost ?? 0)),
    Damage: Math.max(0, Math.round(action.damage ?? 0)),
    Heal: Math.max(0, Math.round(action.heal ?? 0)),
    SupportEffect: typeof action.support_effect === "string" ? action.support_effect : "",
    Message: action.message ?? "",
  };
}

function qs(values: Record<string, string>): string {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(values)) {
    params.set(key, value);
  }

  return params.toString();
}

function firstOrThrow<T>(items: T[] | null | undefined, message: string): T {
  const item = items?.[0];
  if (!item) {
    throw new ApiError("server_error", message, 500);
  }

  return item;
}

function normalizeRequired(value: string | undefined, errorMessage: string): string {
  const normalized = value?.trim();
  if (!normalized) {
    throw new ApiError("forbidden", errorMessage, 400);
  }

  return normalized;
}

function normalizeLoginId(value: string | undefined): string {
  const normalized = normalizeRequired(value, "ログインIDを入力してください。").toLowerCase();
  if (normalized.length < 3 || !/^[a-z0-9._-]+$/.test(normalized)) {
    throw new ApiError("forbidden", "ログインIDは半角英数字、ハイフン、アンダースコア、ドットで入力してください。", 400);
  }

  return normalized;
}

function normalizePassword(value: string | undefined): string {
  const normalized = normalizeRequired(value, "新しいパスワードを入力してください。");
  if (normalized.length < 8) {
    throw new ApiError("forbidden", "パスワードは8文字以上で入力してください。", 400);
  }

  return normalized;
}

function normalizeTeamCode(value: string | undefined | null, role = "member"): string {
  const normalized = value?.trim().toLowerCase();
  if (!normalized) {
    return role === "mentor" ? "mentor" : "blue";
  }

  return normalized.replace(/[^a-z0-9._-]/g, "_");
}

function normalizeUrl(value: string | undefined): string {
  const normalized = normalizeRequired(value, "URLを入力してください。");
  try {
    const url = new URL(normalized);
    if (url.protocol !== "http:" && url.protocol !== "https:") {
      throw new Error("unsupported protocol");
    }

    return url.toString();
  } catch (_error) {
    throw new ApiError("forbidden", "httpまたはhttpsのURLを入力してください。", 400);
  }
}

function teamDisplayName(code: string): string {
  if (code === "blue") return "ブルー班";
  if (code === "magenta") return "マゼンタ班";
  if (code === "mentor") return "メンター";
  return `${code}班`;
}

async function hashPassword(value: string): Promise<string> {
  const digest = await crypto.subtle.digest("SHA-256", TEXT_ENCODER.encode(normalizeRequired(value, "パスワードを入力してください。")));
  return `sha256:${base64EncodeBytes(new Uint8Array(digest))}`;
}

function generateTemporaryPassword(): string {
  return `AOR-${crypto.randomUUID().replaceAll("-", "").slice(0, 8)}`;
}

function base64EncodeBytes(bytes: Uint8Array): string {
  let binary = "";
  for (const byte of bytes) {
    binary += String.fromCharCode(byte);
  }

  return btoa(binary);
}

function base64UrlEncode(value: string): string {
  return base64UrlEncodeBytes(TEXT_ENCODER.encode(value));
}

function base64UrlEncodeBytes(bytes: Uint8Array): string {
  return base64EncodeBytes(bytes).replaceAll("+", "-").replaceAll("/", "_").replace(/=+$/, "");
}

function base64UrlDecode(value: string): string {
  const padded = value.replaceAll("-", "+").replaceAll("_", "/").padEnd(Math.ceil(value.length / 4) * 4, "=");
  const binary = atob(padded);
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index++) {
    bytes[index] = binary.charCodeAt(index);
  }

  return new TextDecoder().decode(bytes);
}

function enumIndex(values: readonly string[], value: string): number {
  const index = values.indexOf(value);
  return index < 0 ? 0 : index;
}

function enumString(values: readonly string[], value: number): string {
  const index = Math.max(0, Math.min(values.length - 1, Math.round(value)));
  return values[index];
}

function normalizeNumberArray(value: unknown, fallback: number[] = []): number[] {
  const source = Array.isArray(value) ? value : fallback;
  return [...new Set(source.map((item) => Number(item)).filter((item) => Number.isFinite(item)).map((item) => Math.max(0, Math.round(item))))];
}

function normalizeStringArray(value: unknown, fallback: string[] = []): string[] {
  const source = Array.isArray(value) ? value : fallback;
  return [...new Set(source.filter((item): item is string => typeof item === "string").map((item) => item.trim()).filter(Boolean))];
}

function defaultStats(userId = ""): DbCharacterStats {
  return {
    user_id: userId,
    level: 1,
    exp: 0,
    hp: 100,
    atk: 10,
    def: 5,
    mp: 30,
    unlocked_weapons: [0],
    titles: [],
    skills: ["基礎攻撃"],
  };
}

function ensureGrowthUnlocks(stats: DbCharacterStats): DbCharacterStats {
  const level = Math.max(1, Math.round(stats.level ?? 1));
  stats.level = level;
  stats.exp = Math.max(0, Math.round(stats.exp ?? 0));
  stats.hp = 100 + (level - 1) * 10;
  stats.atk = 10 + (level - 1) * 2;
  stats.def = 5 + (level - 1);
  stats.mp = 30 + (level - 1) * 2;
  const unlocked = normalizeNumberArray(stats.unlocked_weapons, [0]);
  const skills = normalizeStringArray(stats.skills, ["基礎攻撃"]);
  addGrowthUnlock(unlocked, skills, level, 0, "基礎攻撃");
  addGrowthUnlock(unlocked, skills, level, 1, "省MP射撃");
  addGrowthUnlock(unlocked, skills, level, 2, "ガード支援");
  addGrowthUnlock(unlocked, skills, level, 3, "チャージ砲撃");
  addGrowthUnlock(unlocked, skills, level, 4, "デバッグ支援");
  if (level >= 7) addUnique(skills, "レイド指揮");
  stats.unlocked_weapons = unlocked;
  stats.titles = normalizeStringArray(stats.titles);
  stats.skills = skills;
  return stats;
}

function addGrowthUnlock(unlocked: number[], skills: string[], level: number, weapon: number, skill: string): void {
  if (level >= weapon + 1) {
    addUnique(unlocked, weapon);
    addUnique(skills, skill);
  }
}

function addExp(stats: DbCharacterStats, amount: number): void {
  stats.exp = Math.max(0, Math.round(stats.exp ?? 0)) + Math.max(0, amount);
  stats.level = Math.max(1, Math.round(stats.level ?? 1));
  while ((stats.exp ?? 0) >= expToNextLevel(stats.level)) {
    stats.exp = (stats.exp ?? 0) - expToNextLevel(stats.level);
    stats.level += 1;
  }

  ensureGrowthUnlocks(stats);
}

function expToNextLevel(level: number): number {
  return 50 + Math.max(1, level) * 25;
}

function statsHp(stats: DbCharacterStats): number {
  return Math.max(1, Math.round(stats.hp ?? (100 + (Math.max(1, stats.level ?? 1) - 1) * 10)));
}

function statsAtk(stats: DbCharacterStats): number {
  return Math.max(0, Math.round(stats.atk ?? (10 + (Math.max(1, stats.level ?? 1) - 1) * 2)));
}

function statsDef(stats: DbCharacterStats): number {
  return Math.max(0, Math.round(stats.def ?? (5 + (Math.max(1, stats.level ?? 1) - 1))));
}

function statsMp(stats: DbCharacterStats): number {
  return Math.max(0, Math.round(stats.mp ?? (30 + (Math.max(1, stats.level ?? 1) - 1) * 2)));
}

function isWeaponUnlocked(stats: DbCharacterStats, weaponKind: string): boolean {
  const weaponIndex = enumIndex(WEAPON_KINDS, weaponKind);
  return normalizeNumberArray(ensureGrowthUnlocks(stats).unlocked_weapons, [0]).includes(weaponIndex);
}

function weapons(): WeaponDefinition[] {
  return [
    { kind: 0, dbKind: "blade", displayName: "ブレード", description: "安定した通常攻撃。軽量でWebGL向け演出にも向く。", mpEfficiencyBonus: 0, damageMultiplier: 1.0, preferredRole: 0 },
    { kind: 1, dbKind: "rifle", displayName: "ライフル", description: "MP効率がよい遠距離攻撃。", mpEfficiencyBonus: 3, damageMultiplier: 0.95, preferredRole: 3 },
    { kind: 2, dbKind: "cannon", displayName: "キャノン", description: "高火力・高MP消費の一撃。", mpEfficiencyBonus: 0, damageMultiplier: 1.25, preferredRole: 0 },
    { kind: 3, dbKind: "shield", displayName: "シールド", description: "防御と味方保護に向く。", mpEfficiencyBonus: 0, damageMultiplier: 0.75, preferredRole: 2 },
    { kind: 4, dbKind: "debug_tool", displayName: "デバッグツール", description: "敵DEF低下と支援に向く。", mpEfficiencyBonus: 0, damageMultiplier: 0.8, preferredRole: 3 },
    { kind: 5, dbKind: "release_gear", displayName: "リリース兵装", description: "プロダクトリリース実績で解放される特殊武器。", mpEfficiencyBonus: 0, damageMultiplier: 1.45, preferredRole: 0, isSpecial: true },
    { kind: 6, dbKind: "contest_gear", displayName: "コンテスト兵装", description: "大会提出・受賞で解放される特殊武器。", mpEfficiencyBonus: 0, damageMultiplier: 1.6, preferredRole: 0, isSpecial: true },
  ];
}

function resolveWeapon(dbKind: string): WeaponDefinition {
  return weapons().find((weapon) => weapon.dbKind === dbKind) ?? weapons()[0];
}

function getMpCost(actionType: string, weapon: WeaponDefinition): number {
  const baseCost = actionType === "strong" ? 10 : actionType === "full_power" ? 20 : actionType === "support" ? 10 : 0;
  return Math.max(0, baseCost - weapon.mpEfficiencyBonus);
}

function calculateDamage(stats: DbCharacterStats, weapon: WeaponDefinition, battle: DbBossBattle, role: string, actionType: string): number {
  const raw = statsAtk(stats) * roleMultiplier(role) * actionMultiplier(actionType) * weapon.damageMultiplier - Math.max(0, battle.boss_def ?? 4);
  return Math.max(0, Math.round(raw));
}

function roleMultiplier(role: string): number {
  return role === "attacker" ? 1.25 : role === "supporter" ? 0.85 : role === "healer" || role === "defender" ? 0.75 : 1;
}

function actionMultiplier(actionType: string): number {
  return actionType === "strong" ? 1.8 : actionType === "full_power" ? 3 : actionType === "support" ? 0.4 : actionType === "guard" ? 0.2 : 1;
}

function buildActionMessage(nickname: string, actionType: string, damage: number, heal: number, support: string, teamFollowUpDamage: number): string {
  const actionName = actionType === "strong" ? "強攻撃" : actionType === "full_power" ? "全力攻撃" : actionType === "support" ? "支援行動" : actionType === "guard" ? "ガード" : "通常攻撃";
  if (heal > 0) {
    return `${nickname}の${actionName}: ${support} / チーム追撃 ${teamFollowUpDamage}`;
  }

  if (support) {
    return `${nickname}の${actionName}: ${damage}ダメージ / ${support} / チーム追撃 ${teamFollowUpDamage}`;
  }

  return `${nickname}の${actionName}: ${damage}ダメージ / チーム追撃 ${teamFollowUpDamage}`;
}

function rewardWeaponForAchievement(type: string): number | null {
  return type === "contest_submission" || type === "award"
    ? 6
    : type === "release" || type === "update"
    ? 5
    : null;
}

function rewardTitle(type: string): string {
  return type === "contest_submission" ? "大会挑戦者" : type === "release" ? "リリース職人" : type === "update" ? "改善職人" : type === "award" ? "受賞者" : type === "continuous_dev" ? "継続開発者" : "実績達成者";
}

function rewardSkill(type: string): string {
  return type === "contest_submission" ? "コンテストブースト" : type === "release" ? "リリースブースト" : type === "update" ? "アップデートブースト" : type === "award" ? "アワードブースト" : type === "continuous_dev" ? "継続力" : "成長補正";
}

function addUnique<T>(values: T[], value: T): void {
  if (!values.includes(value)) {
    values.push(value);
  }
}

async function recordAudit(actorUserId: string, actionType: string, targetType: string, targetId: string, before: string, after: string): Promise<void> {
  await rest<unknown>("audit_logs", {
    method: "POST",
    body: {
      actor_user_id: actorUserId,
      action_type: actionType,
      target_type: targetType,
      target_id: targetId,
      before_state: before,
      after_state: after,
    },
  }).catch(() => null);
}

function describeUser(user: DbUser): string {
  return `loginId=${user.login_id};nickname=${user.nickname};role=${user.role};teamId=${user.team_id ?? ""};initialPasswordChanged=${user.initial_password_changed !== false};isActive=${user.is_active !== false}`;
}

function describeSession(session: DbDevSession): string {
  return `status=${session.status};durationMinutes=${session.duration_minutes ?? 0};achievementRate=${session.achievement_rate ?? 0};mentorComment=${session.mentor_comment ?? ""};approvedBy=${session.approved_by ?? ""}`;
}

function describeAchievement(achievement: DbAchievement): string {
  return `status=${achievement.status};type=${achievement.type};title=${achievement.title}`;
}

function describeProduct(product: DbProduct): string {
  return `isPublic=${product.is_public !== false};hiddenBy=${product.hidden_by ?? ""};title=${product.title};url=${product.url}`;
}

function calculateDurationMinutes(startedAtUtc: string, endedAtUtc: string): number {
  const startedAt = Date.parse(startedAtUtc);
  const endedAt = Date.parse(endedAtUtc);
  if (!Number.isFinite(startedAt) || !Number.isFinite(endedAt)) {
    return 0;
  }

  return Math.max(0, Math.round((endedAt - startedAt) / 60000));
}

function weekStartDate(iso: string): string {
  const date = new Date(iso);
  const utc = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate()));
  const offset = (utc.getUTCDay() + 6) % 7;
  utc.setUTCDate(utc.getUTCDate() - offset);
  return utc.toISOString().slice(0, 10);
}

function fallbackEvaluation(session: DbDevSession): ValidatedEvaluation {
  const total = Math.max(40, Math.min(85, 60 + Math.round((session.achievement_rate ?? 0) / 4)));
  return {
    totalScore: total,
    goalScore: total,
    specificityScore: total,
    learningScore: total,
    nextActionScore: total,
    continuityScore: total,
    feedback: "AI評価失敗のため暫定評価です。記録は保存されました。",
  };
}

function isRetryableGeminiStatus(status: number): boolean {
  return status === 429 || status >= 500;
}

function delay(milliseconds: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
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
  const candidate = (body as { candidates?: Array<{ content?: { parts?: Array<{ text?: string }> } }> }).candidates?.[0];
  const text = candidate?.content?.parts?.map((part) => part.text ?? "").join("").trim();
  if (!text) {
    throw new Error("Gemini response did not include text");
  }

  return text;
}

function stripJsonFence(text: string): string {
  return text.replace(/^```(?:json)?/i, "").replace(/```$/i, "").trim();
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
  const score = typeof value === "number" ? value : NaN;
  if (!Number.isInteger(score) || score < 0 || score > 100) {
    throw new Error(`Gemini score is invalid: ${name}`);
  }

  return score;
}

function toAxisScoreRecord(evaluation: ValidatedEvaluation): Record<string, number> {
  return {
    goal_achievement: evaluation.goalScore,
    specificity: evaluation.specificityScore,
    learning: evaluation.learningScore,
    next_action: evaluation.nextActionScore,
    continuity: evaluation.continuityScore,
  };
}

function currentGeminiModel(): string {
  return Deno.env.get("GEMINI_MODEL")?.trim() || GEMINI_DEFAULT_MODEL;
}

function rankLabelFromScore(score: number): string {
  switch (rankFromScore(score)) {
    case 0:
      return "S";
    case 1:
      return "A+";
    case 2:
      return "A";
    case 3:
      return "B";
    case 4:
      return "C";
    default:
      return "D";
  }
}

function rankFromScore(score: number): number {
  if (score >= 95) return 0;
  if (score >= 85) return 1;
  if (score >= 75) return 2;
  if (score >= 60) return 3;
  if (score >= 40) return 4;
  return 5;
}

function rankFromLabel(rank: string): number {
  return rank === "S" ? 0 : rank === "A+" ? 1 : rank === "A" ? 2 : rank === "B" ? 3 : rank === "C" ? 4 : 5;
}

function multiplierFromRank(rank: number): number {
  return rank === 0 ? 2 : rank === 1 ? 1.8 : rank === 2 ? 1.6 : rank === 3 ? 1.3 : rank === 4 ? 1 : 0.8;
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
        required: ["goal_achievement", "specificity", "learning", "next_action", "continuity"],
        additionalProperties: false,
      },
      feedback: {
        type: "string",
      },
    },
    required: ["total_score", "axis_scores", "feedback"],
    additionalProperties: false,
  };
}
