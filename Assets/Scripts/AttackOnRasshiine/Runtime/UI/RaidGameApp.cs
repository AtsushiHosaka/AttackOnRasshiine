using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AttackOnRasshiine.Runtime.Battle;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Scene;
using AttackOnRasshiine.Runtime.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if UNITY_EDITOR || (UNITY_WEBGL && DEVELOPMENT_BUILD)
using AttackOnRasshiine.Runtime.QA;
#endif

namespace AttackOnRasshiine.Runtime.UI
{
    public sealed class RaidGameApp : MonoBehaviour
    {
        private const float BattleStatePollIntervalSeconds = 5f;
        private const float InitialBattleStatePollDelaySeconds = 0.5f;
        private const int MemberPreviewTextureSize = 512;
        private const float DefaultUiTextScale = 0.9f;
        private const float ReferenceUiWidth = 1440f;
        private const float ReferenceUiHeight = 1024f;
        private const int LoginStatusDisplayCharacterLimit = 24;
        private const int MentorAccountPageSize = 12;
        private const int CompactCollectionPageSize = 1;
        private const int PortraitCollectionPageSize = 2;
        private const int AiEvaluationSnapshotPollAttemptCount = 3;
        private const string DefaultBackLabel = "戻る";
        private const string SettingsLabel = "設定";
        private static readonly Color LoginFrameColor = new(0.843f, 0.737f, 0.447f, 0.90f);
        private static readonly Color LoginPanelSurfaceColor = new(0.018f, 0.080f, 0.190f, 0.94f);
        private static readonly Color LoginStatusSurfaceColor = new(0.012f, 0.052f, 0.130f, 0.97f);
        private static readonly Color LoginBrandGold = new(1.0f, 0.88f, 0.62f, 1f);
#if UNITY_EDITOR
        private const string EditorPreviewEnabledKey = "AttackOnRasshiine.EditorPreview.Enabled";
        private const string EditorPreviewLoginIdKey = "AttackOnRasshiine.EditorPreview.LoginId";
        private const string EditorPreviewBattleSetupKey = "AttackOnRasshiine.EditorPreview.BattleSetup";
        private const string EditorPreviewScreenKey = "AttackOnRasshiine.EditorPreview.Screen";
#endif

        [SerializeField] private RasshiineTheme theme;
        [SerializeField] private RaidBattleController battleController;
        [SerializeField] private AnimatedSkybox animatedSkybox;
        [SerializeField] private NeonCityBackdrop neonCityBackdrop;

        private LocalGameRepository repository;
        private LocalGameRepository frontDisplayRepository;
        private SupabaseGameClient supabase;
        private RasshiineSceneRouter sceneRouter;
        private DevLogPresenter devLogPresenter;
        private NeonUiFactory ui;
#if UNITY_EDITOR
        private string editorPreviewScreen = string.Empty;
        private bool editorPreviewActive;
#endif
#if UNITY_EDITOR || (UNITY_WEBGL && DEVELOPMENT_BUILD)
        private WebGlVisualQaIntent webGlVisualQaIntent;
        private bool webGlVisualQaActive;
#endif
        private RectTransform root;
        private UserProfile currentUser;
        private BattleRole selectedRole = BattleRole.Attacker;
        private WeaponKind selectedWeapon = WeaponKind.Blade;
        private AchievementType selectedAchievementType = AchievementType.Release;
        private DevSessionReviewFilter selectedReviewFilter = DevSessionReviewFilter.All;
        private RankingPeriod selectedRankingPeriod = RankingPeriod.Weekly;
        private RankingView selectedRankingView = RankingView.Overall;
        private RankingKind selectedRankingKind = RankingKind.DevelopmentTime;
        private UserRole selectedAccountRole = UserRole.Member;
        private bool selectedAccountRankingVisible = true;
        private int mentorAccountPage;
        private int productPage;
        private int achievementPage;
        private int teamPage;
        private int wardrobePage;
        private int historyPage;
        private int mentorReviewPage;
        private int mentorTeamPage;
        private bool memberProductFormVisible;
        private bool memberAchievementFormVisible;
        private bool mentorReviewCorrectionVisible;
        private string mentorReviewCorrectionSessionId = string.Empty;
        private bool battleStateExpanded;
        private bool battleCommandDeckExpanded;
        private string lastBattleMessage = "メンターの開始待ち";
        private FeedbackTone lastBattleTone = FeedbackTone.Waiting;
        private string lastSessionMessage = string.Empty;
        private FeedbackTone lastSessionTone = FeedbackTone.Info;
        private string lastProductMessage = string.Empty;
        private FeedbackTone lastProductTone = FeedbackTone.Info;
        private string lastAchievementMessage = string.Empty;
        private FeedbackTone lastAchievementTone = FeedbackTone.Info;
        private string lastMentorMessage = string.Empty;
        private FeedbackTone lastMentorTone = FeedbackTone.Info;
        private string loginErrorMessage = string.Empty;
        private string initialPasswordChangeMessage = string.Empty;
        private string pendingInitialPassword = string.Empty;
        private bool isNetworkBusy;
        private Coroutine battleStatePollingRoutine;
        private int battleStatePollGeneration;
        private Coroutine developerNavigationFrontRoutine;
        private Coroutine aiEvaluationSnapshotPollingRoutine;
        private int aiEvaluationSnapshotPollGeneration;
        private bool pollingFrontDisplaySnapshot;
        private bool remoteSnapshotReady;
        private RasshiineProductionScene activeProductionScene = RasshiineProductionScene.Login;
        private RasshiineProductionScene settingsReturnScene = RasshiineProductionScene.Login;
        private GameObject memberPreviewRig;
        private RenderTexture memberPreviewTexture;
        private Sprite battleCommandHexSprite;
        private CosmeticInventoryDto cosmeticInventory;
        private CosmeticGachaResultDto lastCosmeticGachaResult;
        private string selectedCosmeticSlot = "weapon";
        private string cosmeticFeedbackMessage = string.Empty;
        private FeedbackTone cosmeticFeedbackTone = FeedbackTone.Info;
        private bool cosmeticInventoryLoadAttempted;
        private bool cosmeticRequestInFlight;
        private string pendingTemporaryPasswordReveal = string.Empty;
        private string pendingTemporaryPasswordNickname = string.Empty;
        private bool temporaryPasswordRevealVisible;

        private InputField loginIdInput;
        private InputField passwordInput;
        private Text loginStatusText;
        private Button loginButton;
        private Text loginButtonText;
        private InputField lastLoginFocusTarget;
        private string retainedLoginId = string.Empty;
        private string retainedGoalDraft = string.Empty;
        private string retainedReflectionDraft = string.Empty;
        private string retainedNextTaskDraft = string.Empty;
        private float retainedAchievementRate = 75f;
        private string retainedProductTitleDraft = string.Empty;
        private string retainedProductUrlDraft = string.Empty;
        private string retainedProductDescriptionDraft = string.Empty;
        private string retainedAchievementTitleDraft = string.Empty;
        private string retainedAchievementDescriptionDraft = string.Empty;
        private string retainedMemberLoginIdDraft = string.Empty;
        private string retainedMemberNicknameDraft = string.Empty;
        private string retainedMemberTeamIdDraft;
        private InputField initialNewPasswordInput;
        private InputField initialConfirmPasswordInput;
        private InputField goalInput;
        private InputField reflectionInput;
        private InputField nextTaskInput;
        private InputField productTitleInput;
        private InputField productUrlInput;
        private InputField productDescriptionInput;
        private InputField achievementTitleInput;
        private InputField achievementDescriptionInput;
        private InputField memberLoginIdInput;
        private InputField memberNicknameInput;
        private InputField memberTeamIdInput;
        private Slider achievementSlider;

        private enum FeedbackTone
        {
            Info,
            Success,
            Waiting,
            Warning,
            Danger,
            Battle
        }

        private void Awake()
        {
            if (theme == null)
            {
                theme = FindAnyObjectByType<RasshiineTheme>();
            }

            if (battleController == null)
            {
                battleController = FindAnyObjectByType<RaidBattleController>();
            }

            if (animatedSkybox == null)
            {
                animatedSkybox = FindAnyObjectByType<AnimatedSkybox>();
            }

            if (neonCityBackdrop == null)
            {
                neonCityBackdrop = FindAnyObjectByType<NeonCityBackdrop>();
            }

            // Start from an empty authoritative cache. Editor and loopback visual
            // fixtures replace this only behind their explicit compile/runtime gates.
            repository = LocalGameRepository.CreateAuthoritativeCache();
            supabase = new SupabaseGameClient();
            devLogPresenter = new DevLogPresenter();

            // Public front-display scenes must never inherit an authenticated route's
            // bearer or private snapshot. Web routing clears this before the load; this
            // second boundary also protects direct scene entry and scene reloads.
            if (IsActiveProductionScene(RasshiineProductionScene.FrontDisplay))
            {
                RasshiineRuntimeSession.Clear();
            }

            supabase.RestoreSessionToken(RasshiineRuntimeSession.SessionToken);
            if (RasshiineRuntimeSession.Snapshot != null)
            {
                repository.ApplySnapshot(RasshiineRuntimeSession.Snapshot);
            }

            currentUser = RasshiineRuntimeSession.CurrentUser;
#if UNITY_WEBGL && DEVELOPMENT_BUILD && !UNITY_EDITOR
            TryApplyWebGlVisualQaIntent();
#endif
#if UNITY_EDITOR
            TryApplyEditorPreviewUser();
#endif
            sceneRouter = RasshiineSceneRouter.Ensure();
            ui = new NeonUiFactory(theme);
            EnsureEventSystem();
            CreateRoot();
        }

        private void Start()
        {
            if (theme != null && theme.SkyboxMaterial != null)
            {
                if (animatedSkybox != null)
                {
                    animatedSkybox.Configure(theme.SkyboxMaterial);
                }
                else
                {
                    RenderSettings.skybox = theme.SkyboxMaterial;
                }
            }

#if UNITY_WEBGL && DEVELOPMENT_BUILD && !UNITY_EDITOR
            if (webGlVisualQaActive)
            {
                battleController?.LoadBattle(repository.ActiveBattle);
                SyncBattleControlledParticipant();
                ShowWebGlVisualQaScreen();
                return;
            }
#endif
#if UNITY_EDITOR
            if (editorPreviewActive)
            {
                battleController?.LoadBattle(repository.ActiveBattle);
                SyncBattleControlledParticipant();
                ShowStartupScene();
                ShowEditorPreviewScreen();
                StartCoroutine(LoadSupabaseConfigOnly());
                return;
            }
#endif
            battleController?.ClearBattleStage();
            battleController?.SetControlledParticipant(null);
            ShowSupabaseSyncingScene();
            StartCoroutine(LoadSupabaseConfig());
        }

        private void OnDestroy()
        {
            StopBattleStatePolling();
            CancelAiEvaluationSnapshotPolling();
            ClearMemberPreview();
            if (battleCommandHexSprite != null)
            {
                DestroyRuntimeObject(battleCommandHexSprite);
                battleCommandHexSprite = null;
            }
        }

        private IEnumerator LoadSupabaseConfig()
        {
            yield return supabase.LoadConfig();
            if (supabase is not { IsConfigured: true })
            {
                ShowSupabaseConnectionGate(
                    "冒険者の記録を読み込めません",
                    RemoteErrorMessage("しばらく待ってから、もう一度お試しください。"));
                yield break;
            }

            // The unauthenticated boot/login flow does not depend on shared game data. Requiring
            // a public snapshot here made a transient API outage hide the login screen entirely.
            // Only authenticated restores and the public front display need an initial snapshot.
            if (!RequiresStartupSnapshot(supabase.HasSession, IsActiveProductionScene(RasshiineProductionScene.FrontDisplay)))
            {
                ShowStartupScene();
                yield break;
            }

            SupabaseGameApiResponseDto response = null;
            yield return supabase.HasSession
                ? supabase.GetSnapshot(result => response = result)
                : supabase.GetFrontDisplaySnapshot(result => response = result);
            if (ReturnToLoginIfRemoteSessionExpired(response))
            {
                yield break;
            }

            if (response?.Ok == true && HasRequiredAuthoritativeSnapshot(response.Snapshot))
            {
                ApplyRemoteSnapshot(response);
                ShowStartupScene();
                yield break;
            }

            ShowSupabaseConnectionGate(
                "冒険者の記録を読み込めません",
                RemoteErrorMessage("記録を取得できませんでした。通信環境を確認して、もう一度お試しください。"));
        }

        private static bool RequiresStartupSnapshot(bool hasSession, bool isFrontDisplayScene)
        {
            return hasSession || isFrontDisplayScene;
        }

        private static bool HasRequiredAuthoritativeSnapshot(GameSnapshotDto snapshot)
        {
            // The release repository intentionally has no local battle fixture.
            // Never paper over an incomplete server response with demo state.
            return snapshot?.ActiveBattle?.Boss != null;
        }

        private IEnumerator LoadSupabaseConfigOnly()
        {
            yield return supabase.LoadConfig();
        }

#if UNITY_WEBGL && DEVELOPMENT_BUILD && !UNITY_EDITOR
        private void TryApplyWebGlVisualQaIntent()
        {
            if (!WebGlVisualQaContext.TryGet(out var intent))
            {
                return;
            }

            webGlVisualQaIntent = intent;
            webGlVisualQaActive = true;
            repository = LocalGameRepository.CreateVisualQaFixture();
            RasshiineRuntimeSession.Clear();
            supabase.ClearSession();
            PrepareWebGlVisualQaFixture(intent);
        }
#endif

#if UNITY_EDITOR || (UNITY_WEBGL && DEVELOPMENT_BUILD)
        private void PrepareWebGlVisualQaFixture(WebGlVisualQaIntent intent)
        {
            if (intent == null)
            {
                throw new ArgumentNullException(nameof(intent));
            }

            var member = repository.Members.FirstOrDefault(item => item.IsActive);
            var mentor = repository.Mentors.FirstOrDefault(item => item.IsActive);
            if (member == null || mentor == null || repository.ActiveBattle == null)
            {
                throw new InvalidOperationException("Visual-QA fixture could not create its closed local roster.");
            }

            SeedWebGlVisualQaRichContent(member, mentor, intent.Screen);
            currentUser = intent.Role switch
            {
                UserRole.Member => member,
                UserRole.Mentor => mentor,
                _ => null
            };

            foreach (var user in repository.Users)
            {
                user.InitialPasswordChanged = true;
            }

            if (intent.Screen == WebGlVisualQaScreen.InitialPassword && currentUser != null)
            {
                currentUser.InitialPasswordChanged = false;
            }

            activeProductionScene = intent.Scene;
            settingsReturnScene = intent.Scene;
            remoteSnapshotReady = true;
            cosmeticInventory = CreateEditorCosmeticPreview();
            cosmeticInventoryLoadAttempted = true;
            cosmeticRequestInFlight = false;

            if (intent.Screen == WebGlVisualQaScreen.WeaponWishResult)
            {
                lastCosmeticGachaResult = new CosmeticGachaResultDto
                {
                    drawId = "visual-qa-draw",
                    itemId = "preview-weapon-sun",
                    code = "tinyhero_suncrest_blade",
                    label = "日輪の剣",
                    slot = "weapon",
                    rarity = "legendary",
                    unityAssetKey = "tinyhero/weapon/suncrest_blade",
                    quantity = 1,
                    remainingCredits = 2,
                    wasDuplicate = false,
                    replayed = false
                };
            }

            RasshiineRuntimeSession.SetUser(currentUser);
            RasshiineRuntimeSession.SetSnapshot(repository.CreateSnapshot());
        }

        private void SeedWebGlVisualQaRichContent(
            UserProfile member,
            UserProfile mentor,
            WebGlVisualQaScreen screen)
        {
            repository.RegisterProduct(
                member.Id,
                "星渡りのチームポータル",
                "https://example.com/attack-on-rasshiine/stellar-team-portal",
                "仲間の開発記録とレイドへの準備を、ひとつの画面で確認できる作品です。");
            repository.RegisterProduct(
                member.Id,
                "Tiny Hero 装備シミュレーター",
                "https://example.com/attack-on-rasshiine/tiny-hero-loadout",
                "軽量なHeroアセットの装備と配色をブラウザ上で試せます。");

            var approvedAchievement = repository.SubmitAchievement(
                member.Id,
                AchievementType.Release,
                "チームポータルを公開",
                "企画からWebGL公開までを仲間と協力して完成させました。");
            repository.ApproveAchievement(approvedAchievement.Id, mentor.Id);
            repository.SubmitAchievement(
                member.Id,
                AchievementType.ContestSubmission,
                "校内ゲームコンテストへ応募",
                "遊びやすさと軽量化を両立したボス戦を提出しました。");

            repository.StartSession(member.Id, "ボス戦のコマンドUIと光の演出を磨く");
            repository.CompleteSession(
                member.Id,
                88,
                "役割と武器の選択を整理し、初めて遊ぶ仲間にも伝わる画面にできた。",
                "スマートフォン幅で文字とタップ領域を最終確認する。");

            if (screen == WebGlVisualQaScreen.DevLogActive)
            {
                repository.StartSession(member.Id, "スマートフォンのレイアウトを仕上げる");
            }

            if (screen is WebGlVisualQaScreen.MentorAccountList or WebGlVisualQaScreen.MentorAccountListPageTwo)
            {
                var index = 0;
                while (repository.Users.Count < 53)
                {
                    repository.CreateUserAccount(
                        mentor.Id,
                        $"visual-qa-{index:00}",
                        $"星巡りメンバー {index + 1}",
                        UserRole.Member,
                        index % 2 == 0 ? "blue" : "magenta",
                        true);
                    index += 1;
                }

                mentorAccountPage = screen == WebGlVisualQaScreen.MentorAccountListPageTwo ? 1 : 0;
            }

            var battle = repository.ActiveBattle;
            var requiresActiveBattle = screen is WebGlVisualQaScreen.BattleActive
                or WebGlVisualQaScreen.BattleExpanded
                or WebGlVisualQaScreen.BattleCoopTurn
                or WebGlVisualQaScreen.BattleResult
                or WebGlVisualQaScreen.FrontActive
                or WebGlVisualQaScreen.FrontResult;
            if (requiresActiveBattle)
            {
                repository.StartBattle(mentor.Id);
                battle = repository.ActiveBattle;
                if (screen == WebGlVisualQaScreen.BattleActive)
                {
                    // Reference-only composition data. Keep the production battle
                    // contract at three turns, but make the deterministic browser QA
                    // frame read exactly like the approved TURN 3/12 encounter.
                    battle.TurnCount = 12;
                    battle.TurnNumber = 3;
                    battle.Boss.CurrentHp = Mathf.RoundToInt(battle.Boss.MaxHp * 0.80f);
                }
            }

            if (screen is WebGlVisualQaScreen.BattleCoopTurn or WebGlVisualQaScreen.BattleResult or WebGlVisualQaScreen.FrontActive or WebGlVisualQaScreen.FrontResult)
            {
                repository.SubmitBattleAction(
                    member.Id,
                    BattleRole.Attacker,
                    WeaponKind.Blade,
                    BattleActionType.Strong);
            }

            battleCommandDeckExpanded = screen == WebGlVisualQaScreen.BattleExpanded;
            battleStateExpanded = screen is WebGlVisualQaScreen.BattleCoopTurn or WebGlVisualQaScreen.BattleResult;
            if (screen is WebGlVisualQaScreen.BattleResult or WebGlVisualQaScreen.FrontResult)
            {
                battle.Status = BattleStatus.Completed;
                battle.Phase = BattlePhase.Completed;
                battle.Outcome = BattleOutcome.Victory;
                battle.Boss.CurrentHp = 0;
                battle.CompletedAtUtc = DateTime.UtcNow;
                lastBattleMessage = "レイドクリア！ 仲間の連携で星喰らいの巨像を退けました。";
                lastBattleTone = FeedbackTone.Success;
            }
            else if (requiresActiveBattle)
            {
                battle.Phase = BattlePhase.ActionSelect;
                lastBattleMessage = "役割・武器・行動を選び、仲間と同じターンに力を合わせよう。";
                lastBattleTone = FeedbackTone.Battle;
            }
        }

        private void ShowWebGlVisualQaScreen()
        {
            if (!webGlVisualQaActive || webGlVisualQaIntent == null)
            {
                return;
            }

            switch (webGlVisualQaIntent.Screen)
            {
                case WebGlVisualQaScreen.Loading:
                    ShowSupabaseSyncingScene();
                    break;
                case WebGlVisualQaScreen.ConnectionGate:
                    ShowSupabaseConnectionGate("冒険者の記録へ接続できません", "通信状況を確認して、もう一度お試しください。");
                    break;
                case WebGlVisualQaScreen.Login:
                    ShowLogin();
                    break;
                case WebGlVisualQaScreen.LoginError:
                    loginErrorMessage = "IDまたはパスワードが違います";
                    ShowLogin();
                    break;
                case WebGlVisualQaScreen.InitialPassword:
                    ShowInitialPasswordChange();
                    break;
                case WebGlVisualQaScreen.MemberHome:
                    ShowMemberHome();
                    break;
                case WebGlVisualQaScreen.DevLogIdle:
                case WebGlVisualQaScreen.DevLogActive:
                    ShowDevLog();
                    break;
                case WebGlVisualQaScreen.MemberProducts:
                case WebGlVisualQaScreen.MentorProducts:
                    ShowProducts();
                    break;
                case WebGlVisualQaScreen.MemberAchievements:
                case WebGlVisualQaScreen.MentorAchievements:
                    ShowAchievements();
                    break;
                case WebGlVisualQaScreen.MemberTeam:
                    ShowMemberTeam();
                    break;
                case WebGlVisualQaScreen.Wardrobe:
                    ShowCosmeticWardrobe();
                    break;
                case WebGlVisualQaScreen.WeaponWish:
                case WebGlVisualQaScreen.WeaponWishResult:
                    ShowWeaponWish();
                    break;
                case WebGlVisualQaScreen.MemberHistory:
                    ShowMemberHistory();
                    break;
                case WebGlVisualQaScreen.MemberSettings:
                case WebGlVisualQaScreen.MentorSettings:
                    ShowSettings();
                    break;
                case WebGlVisualQaScreen.Ranking:
                    ShowRanking();
                    break;
                case WebGlVisualQaScreen.MentorDashboard:
                    ShowMentorDashboard();
                    break;
                case WebGlVisualQaScreen.MentorOperations:
                    ShowMentorOperations();
                    break;
                case WebGlVisualQaScreen.MentorReview:
                    ShowMentorReviewQueue();
                    break;
                case WebGlVisualQaScreen.MentorTeam:
                    ShowMentorTeamStatus();
                    break;
                case WebGlVisualQaScreen.MentorAccounts:
                    ShowMentorAccounts();
                    break;
                case WebGlVisualQaScreen.MentorAccountList:
                case WebGlVisualQaScreen.MentorAccountListPageTwo:
                    ShowMentorAccountList();
                    break;
                case WebGlVisualQaScreen.BattleMemberScheduled:
                case WebGlVisualQaScreen.BattleMentorScheduled:
                case WebGlVisualQaScreen.BattleActive:
                case WebGlVisualQaScreen.BattleExpanded:
                case WebGlVisualQaScreen.BattleCoopTurn:
                case WebGlVisualQaScreen.BattleResult:
                    ShowBattle();
                    break;
                case WebGlVisualQaScreen.FrontScheduled:
                case WebGlVisualQaScreen.FrontActive:
                case WebGlVisualQaScreen.FrontResult:
                    ShowFrontScreen();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Canvas.ForceUpdateCanvases();
            Debug.Log(WebGlVisualQaContext.ReadyLogPrefix + webGlVisualQaIntent.Slug);
        }
#endif

#if UNITY_EDITOR
        private void TryApplyEditorPreviewUser()
        {
            if (!Application.isEditor)
            {
                return;
            }

            var previewEnabled = EditorPrefs.GetBool(EditorPreviewEnabledKey, false);
            var battleSetup = EditorPrefs.GetString(EditorPreviewBattleSetupKey, string.Empty);
            if (!previewEnabled && string.IsNullOrWhiteSpace(battleSetup))
            {
                return;
            }

            editorPreviewActive = true;
            EditorPrefs.SetBool(EditorPreviewEnabledKey, false);
            EditorPrefs.SetString(EditorPreviewBattleSetupKey, string.Empty);
            editorPreviewScreen = EditorPrefs.GetString(EditorPreviewScreenKey, string.Empty);
            EditorPrefs.SetString(EditorPreviewScreenKey, string.Empty);
            currentUser = null;
            repository = new LocalGameRepository();
            RasshiineRuntimeSession.Clear();
            supabase?.ClearSession();
            if (previewEnabled)
            {
                var previewLoginId = EditorPrefs.GetString(EditorPreviewLoginIdKey, "mentor1");
                var previewUser = repository.Users.FirstOrDefault(user => string.Equals(user.LoginId, previewLoginId, StringComparison.OrdinalIgnoreCase));
                if (previewUser == null)
                {
                    Debug.LogWarning($"AttackOnRasshiine editor preview user was not found: {previewLoginId}");
                    return;
                }

                currentUser = previewUser;
                RasshiineRuntimeSession.SetUser(previewUser);
            }

            ApplyEditorBattlePreviewSetup(battleSetup);
            RasshiineRuntimeSession.SetSnapshot(repository.CreateSnapshot());
        }

        private void ShowEditorPreviewScreen()
        {
            if (string.IsNullOrWhiteSpace(editorPreviewScreen))
            {
                return;
            }

            var screen = editorPreviewScreen;
            editorPreviewScreen = string.Empty;
            switch (screen)
            {
                case "products":
                    ShowProducts();
                    return;
                case "achievements":
                    ShowAchievements();
                    return;
                case "ranking":
                    ShowRanking();
                    return;
                case "settings":
                    ShowSettings();
                    return;
                case "mentor-operations":
                    ShowMentorOperations();
                    return;
                case "mentor-review":
                    ShowMentorReviewQueue();
                    return;
                case "mentor-team":
                    ShowMentorTeamStatus();
                    return;
                case "mentor-accounts":
                    ShowMentorAccounts();
                    return;
            }
        }

        private void ApplyEditorBattlePreviewSetup(string setup)
        {
            if (string.IsNullOrWhiteSpace(setup) || string.Equals(setup, "scheduled", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var mentorId = repository.Mentors.FirstOrDefault()?.Id;
            repository.StartBattle(mentorId);
            selectedRole = BattleRole.Attacker;
            selectedWeapon = WeaponKind.Blade;
            if (string.Equals(setup, "active", StringComparison.OrdinalIgnoreCase))
            {
                SetBattleFeedback("検証: レイド開始。役割・武器・行動を選べます。", FeedbackTone.Battle);
                return;
            }

            var participants = repository.ActiveBattle.Participants.ToList();
            if (participants.Count == 0)
            {
                SetBattleFeedback("検証: 参加者データがありません。", FeedbackTone.Warning);
                return;
            }

            var first = participants[0];
            var previewActionType = string.Equals(setup, "coop-turn", StringComparison.OrdinalIgnoreCase)
                ? BattleActionType.FullPower
                : BattleActionType.Strong;
            var result = repository.SubmitBattleAction(first.UserId, BattleRole.Attacker, WeaponKind.Blade, previewActionType);
            selectedRole = first.Role;
            selectedWeapon = first.Weapon;
            SetBattleFeedback(result.Message, BattleFeedbackTone(result));
            if (string.Equals(setup, "coop-turn", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(setup, "result", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var participant in participants.Skip(1).Take(2))
                {
                    if (repository.ActiveBattle.IsCompleted)
                    {
                        break;
                    }

                    repository.SubmitBattleAction(participant.UserId, participant.Role, participant.Weapon, BattleActionType.Normal);
                }

                SetBattleFeedback("検証: 3ターン分の協力行動を解決しました。", FeedbackTone.Battle);
            }
        }
#endif

        private bool IsActiveProductionScene(RasshiineProductionScene expected)
        {
            return RasshiineSceneCatalog.TryGetSceneByName(SceneManager.GetActiveScene().name, out var scene)
                && scene == expected;
        }

        private void ShowStartupScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!RasshiineSceneCatalog.TryGetSceneByName(activeScene.name, out var scene))
            {
                ShowLogin();
                return;
            }

            if (scene == RasshiineProductionScene.FrontDisplay)
            {
                currentUser = null;
                loginErrorMessage = string.Empty;
                battleController?.SetControlledParticipant(null);
                ShowFrontScreen();
                return;
            }

            if (scene == RasshiineProductionScene.Boot || scene == RasshiineProductionScene.Login)
            {
                ShowLogin();
                return;
            }

            if (RasshiineSceneCatalog.IsDisplayOnlyScene(scene))
            {
                ShowFrontScreen();
                return;
            }

            if (currentUser == null)
            {
                ShowLogin();
                return;
            }

            if (scene == RasshiineProductionScene.MentorDashboard)
            {
                if (currentUser.Role != UserRole.Mentor)
                {
                    ShowMemberHome();
                    return;
                }

                ShowMentorDashboard();
                return;
            }

            if (scene == RasshiineProductionScene.DevLog)
            {
                ShowDevLog();
                return;
            }

            if (scene == RasshiineProductionScene.Battle)
            {
                ShowBattle();
                return;
            }

            ShowMemberHome();
        }

        private void ShowSupabaseSyncingScene()
        {
            StopBattleStatePolling();
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Login);
            ClearRoot();

            var constrained = UsesConstrainedLayout();
            var panel = ui.CreatePanel(
                root,
                "JourneyLoadingPanel",
                theme.RaidPanel,
                constrained ? new Vector2(0.08f, 0.34f) : new Vector2(0.27f, 0.30f),
                constrained ? new Vector2(0.92f, 0.66f) : new Vector2(0.73f, 0.67f),
                Vector2.zero,
                Vector2.zero);
            AddVertical(panel, constrained ? 18 : 28, constrained ? 10 : 18, TextAnchor.MiddleCenter);
            AddDisplayText(panel, "旅支度を整えています", constrained ? 28 : 34, theme.Text, constrained ? 64 : 48, TextAnchor.MiddleCenter);
            AddText(panel, "冒険者の記録を安全に読み込んでいます。", constrained ? 16 : 18, FontStyle.Bold, theme.MutedText, constrained ? 54 : 34, TextAnchor.MiddleCenter);
            AddReadableProgress(panel, 0.68f, false, constrained ? 34f : 48f, "記録を確認中");
        }

        private void ShowSupabaseConnectionGate(string title, string detail)
        {
            StopBattleStatePolling();
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Login);
            ClearRoot();

            var constrained = UsesConstrainedLayout();
            var panel = ui.CreatePanel(
                root,
                "JourneyConnectionGate",
                theme.RaidPanel,
                constrained ? new Vector2(0.07f, 0.22f) : new Vector2(0.24f, 0.24f),
                constrained ? new Vector2(0.93f, 0.78f) : new Vector2(0.76f, 0.72f),
                Vector2.zero,
                Vector2.zero);
            AddVertical(panel, constrained ? 18 : 26, constrained ? 9 : 14, TextAnchor.MiddleCenter);
            if (constrained)
            {
                AddText(panel, "CONNECTION", 13, FontStyle.Bold, theme.Cyan, 26, TextAnchor.MiddleCenter);
            }

            var visibleTitle = constrained ? FormatConnectionGateTitle(title) : title;
            var visibleDetail = constrained
                ? Shorten(detail, UsesPortraitLayout() ? 44 : 68)
                : detail;
            AddDisplayText(panel, visibleTitle, constrained ? 27 : 30, theme.Text, constrained ? 94 : 44, TextAnchor.MiddleCenter);
            AddText(panel, visibleDetail, constrained ? 16 : 17, FontStyle.Bold, theme.MutedText, constrained ? 74 : 66, TextAnchor.MiddleCenter);
            AddButton(panel, "もう一度試す", theme.PrimaryButton, () =>
            {
                ShowSupabaseSyncingScene();
                StartCoroutine(LoadSupabaseConfig());
            });
        }

        private static string FormatConnectionGateTitle(string title)
        {
            var normalized = string.IsNullOrWhiteSpace(title) ? "接続できません" : title.Trim();
            if (normalized.Length <= 10 || normalized.Contains("\n", StringComparison.Ordinal))
            {
                return normalized;
            }

            var breakAt = normalized.IndexOf('へ');
            if (breakAt < 3 || breakAt >= normalized.Length - 2)
            {
                breakAt = normalized.Length / 2 - 1;
            }

            return $"{normalized.Substring(0, breakAt + 1)}\n{normalized.Substring(breakAt + 1)}";
        }

        private bool IsEditorPreviewActive()
        {
#if UNITY_EDITOR
            return editorPreviewActive;
#else
            return false;
#endif
        }

        private bool IsVisualPreviewActive()
        {
#if UNITY_EDITOR || (UNITY_WEBGL && DEVELOPMENT_BUILD)
            return IsEditorPreviewActive() || webGlVisualQaActive;
#else
            return false;
#endif
        }

        private bool HandleUnavailableAuthoritativeBackend()
        {
#if UNITY_EDITOR
            // Editor-only fixtures remain available for deterministic EditMode and
            // visual-preview tests. Player builds must never mutate the local cache.
            return false;
#elif UNITY_WEBGL && DEVELOPMENT_BUILD
            if (webGlVisualQaActive)
            {
                return false;
            }
#endif

            ShowSupabaseConnectionGate(
                "冒険者の記録へ接続できません",
                "本番APIとの接続を確認して、もう一度お試しください。");
            return true;
        }

        private bool EnsureRemoteSnapshotReadyForNavigation()
        {
            if (remoteSnapshotReady || IsVisualPreviewActive())
            {
                return true;
            }

            ShowSupabaseSyncingScene();
            StartCoroutine(LoadSupabaseConfig());
            return false;
        }

        private void SyncBattleControlledParticipant()
        {
            if (battleController == null || currentUser == null || repository == null)
            {
                battleController?.SetControlledParticipant(null);
                return;
            }

            var battle = repository.ActiveBattle;
            var canControlParticipant = currentUser.Role == UserRole.Member
                && battle != null
                && battle.IsActive
                && repository.GetParticipant(currentUser.Id) != null;
            battleController.SetControlledParticipant(canControlParticipant ? currentUser.Id : null);
        }

        private void ApplyRemoteSnapshot(SupabaseGameApiResponseDto response, bool battleOnly = false)
        {
            if (response?.Snapshot == null)
            {
                return;
            }

            var snapshot = response.Snapshot.ToSnapshot();
            if (battleOnly)
            {
                repository.ApplyBattleSnapshot(snapshot.ActiveBattle);
            }
            else
            {
                repository.ApplySnapshot(snapshot);
            }

            RasshiineRuntimeSession.SetSnapshot(repository.CreateSnapshot());
            remoteSnapshotReady = true;
            if (activeProductionScene == RasshiineProductionScene.Battle)
            {
                battleController?.LoadBattle(repository.ActiveBattle);
                SyncBattleControlledParticipant();
            }
            else if (activeProductionScene == RasshiineProductionScene.FrontDisplay)
            {
                var publicBattle = IsActiveProductionScene(RasshiineProductionScene.FrontDisplay)
                    ? repository.ActiveBattle
                    : frontDisplayRepository?.ActiveBattle;
                battleController?.LoadBattle(SelectBattleStageState(
                    true,
                    repository.ActiveBattle,
                    publicBattle));
                battleController?.SetControlledParticipant(null);
            }
            else
            {
                battleController?.ClearBattleStage();
                battleController?.SetControlledParticipant(null);
            }
        }

        private void ApplyRemoteFrontDisplayProjection(SupabaseGameApiResponseDto response)
        {
            if (response?.Snapshot == null)
            {
                return;
            }

            frontDisplayRepository ??= LocalGameRepository.CreateAuthoritativeCache();
            frontDisplayRepository.ApplySnapshot(response.Snapshot.ToSnapshot());
            if (activeProductionScene == RasshiineProductionScene.FrontDisplay)
            {
                battleController?.LoadBattle(frontDisplayRepository.ActiveBattle);
                battleController?.SetControlledParticipant(null);
            }
        }

        private void ApplyRemoteBattleDelta(SupabaseGameApiResponseDto response)
        {
            if (response?.BattleDelta == null)
            {
                return;
            }

            repository.ApplyBattleDelta(response.BattleDelta);
            PersistRuntimeSnapshot();
            remoteSnapshotReady = true;
            if (activeProductionScene == RasshiineProductionScene.Battle)
            {
                battleController?.LoadBattle(repository.ActiveBattle);
                SyncBattleControlledParticipant();
            }
        }

        private void ApplyRemoteBattleState(SupabaseGameApiResponseDto response)
        {
            if (response?.NotModified == true || response?.ActiveBattle == null)
            {
                return;
            }

            repository.ApplyBattleSnapshot(SupabaseDtoMapper.ToDomain(response.ActiveBattle));
            PersistRuntimeSnapshot();
            remoteSnapshotReady = true;
            if (activeProductionScene == RasshiineProductionScene.Battle)
            {
                battleController?.LoadBattle(repository.ActiveBattle);
                SyncBattleControlledParticipant();
            }
        }

        private static BossBattleState SelectBattleStageState(
            bool frontDisplay,
            BossBattleState authenticatedBattle,
            BossBattleState publicFrontBattle)
        {
            return frontDisplay ? publicFrontBattle : authenticatedBattle;
        }

        private void ApplyRemoteSnapshotPreservingModerationRecords(SupabaseGameApiResponseDto response)
        {
            var products = repository.Products.ToList();
            var achievements = repository.Achievements.ToList();
            ApplyRemoteSnapshot(response);
            foreach (var product in products)
            {
                repository.ApplyProductUpdate(product);
            }

            foreach (var achievement in achievements)
            {
                repository.ApplyAchievementUpdate(achievement);
            }

            repository.ApplyProductUpdate(response?.Product?.ToDomain());
            repository.ApplyAchievementUpdate(response?.Achievement?.ToDomain());
            PersistRuntimeSnapshot();
        }

        private void PersistRuntimeSnapshot()
        {
            RasshiineRuntimeSession.SetSnapshot(repository.CreateSnapshot());
        }

        private void CreateRoot()
        {
            if (ui == null)
            {
                ui = new NeonUiFactory(theme);
            }

            if (root != null)
            {
                return;
            }

            var canvas = ui.CreateCanvas("Rasshiine Game UI");
            root = new GameObject("ScreenRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(canvas.transform, false);
            ui.Stretch(root, 0, 0, 0, 0);
        }

        private RectTransform EnsureRoot()
        {
            if (root == null)
            {
                CreateRoot();
            }

            return root;
        }

        private void ClearRoot()
        {
            ClearMemberPreview();
            ui.Clear(EnsureRoot());
        }

        private void ClearMemberPreview()
        {
            if (memberPreviewRig != null)
            {
                DestroyRuntimeObject(memberPreviewRig);
                memberPreviewRig = null;
            }

            if (memberPreviewTexture != null)
            {
                memberPreviewTexture.Release();
                DestroyRuntimeObject(memberPreviewTexture);
                memberPreviewTexture = null;
            }
        }

        private Texture EnsureMemberPreviewTexture()
        {
            if (memberPreviewTexture == null)
            {
                memberPreviewTexture = new RenderTexture(MemberPreviewTextureSize, MemberPreviewTextureSize, 16, RenderTextureFormat.ARGB32)
                {
                    name = "RasshiineMemberTinyHeroPreview",
                    antiAliasing = 4,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                if (CanRenderMemberPreview())
                {
                    memberPreviewTexture.Create();
                }
            }

            if (memberPreviewRig == null)
            {
                CreateMemberPreviewRig();
            }

            return memberPreviewTexture;
        }

        private void CreateMemberPreviewRig()
        {
            memberPreviewRig = new GameObject("MemberTinyHeroPreviewRig");
            memberPreviewRig.transform.position = new Vector3(0f, -1000f, 0f);

            var modelRoot = new GameObject("ModelRoot");
            modelRoot.transform.SetParent(memberPreviewRig.transform, false);
            modelRoot.transform.localPosition = Vector3.zero;
            modelRoot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var model = CreateMemberPreviewModel(modelRoot.transform);
            FitPreviewModel(model);

            var lightObject = new GameObject("PreviewKeyLight", typeof(Light));
            lightObject.transform.SetParent(memberPreviewRig.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(38f, -28f, 0f);
            var key = lightObject.GetComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.96f, 0.84f, 1f);
            key.intensity = 1.65f;

            var fillObject = new GameObject("PreviewFillLight", typeof(Light));
            fillObject.transform.SetParent(memberPreviewRig.transform, false);
            fillObject.transform.localPosition = new Vector3(-1.5f, 1.2f, -1.8f);
            var fill = fillObject.GetComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(0.62f, 0.78f, 1f, 1f);
            fill.intensity = 0.85f;
            fill.range = 5f;

            var cameraObject = new GameObject("PreviewCamera", typeof(Camera));
            cameraObject.transform.SetParent(memberPreviewRig.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0.86f, -3.2f);
            cameraObject.transform.LookAt(memberPreviewRig.transform.position + new Vector3(0f, 0.74f, 0f), Vector3.up);
            var previewCamera = cameraObject.GetComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = 1.22f;
            previewCamera.nearClipPlane = 0.01f;
            previewCamera.farClipPlane = 8f;
            previewCamera.targetTexture = memberPreviewTexture;
            if (CanRenderMemberPreview())
            {
                previewCamera.Render();
            }
        }

        private static bool CanRenderMemberPreview()
        {
            return !Application.isBatchMode &&
                   SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
        }

        private GameObject CreateMemberPreviewModel(Transform parent)
        {
            if (theme != null && theme.MemberPlaceholderPrefab != null)
            {
                var instance = Instantiate(theme.MemberPlaceholderPrefab, parent, false);
                instance.name = "TinyHeroPreviewModel";
                TinyHeroCosmeticApplicator.Apply(instance, cosmeticInventory);
                foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
                {
                    if (!animator.isActiveAndEnabled || !animator.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    animator.updateMode = AnimatorUpdateMode.Normal;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.applyRootMotion = false;
                    animator.Update(0f);
                    animator.enabled = false;
                }

                return instance;
            }

            var fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            fallback.name = "TinyHeroPreviewFallback";
            fallback.transform.SetParent(parent, false);
            fallback.transform.localScale = new Vector3(0.62f, 0.92f, 0.62f);
            return fallback;
        }

        private static void FitPreviewModel(GameObject model)
        {
            if (model == null)
            {
                return;
            }

            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                model.transform.localPosition = Vector3.zero;
                model.transform.localScale = Vector3.one;
                return;
            }

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            var modelHeight = Mathf.Max(0.001f, bounds.size.y);
            var scale = 1.86f / modelHeight;
            model.transform.localScale *= scale;

            bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            var origin = model.transform.parent != null ? model.transform.parent.position : Vector3.zero;
            model.transform.position += new Vector3(
                origin.x - bounds.center.x,
                origin.y - 0.82f - bounds.min.y,
                origin.z - bounds.center.z);
        }

        private void SetBackdrop(NeonCityBackdrop.BackdropPreset preset)
        {
            neonCityBackdrop?.SetPreset(preset);
        }

        private void MarkScene(RasshiineProductionScene scene)
        {
            var leavingBattleStage = (activeProductionScene == RasshiineProductionScene.Battle ||
                                      activeProductionScene == RasshiineProductionScene.FrontDisplay) &&
                                     scene != RasshiineProductionScene.Battle &&
                                     scene != RasshiineProductionScene.FrontDisplay;
            activeProductionScene = scene;
            if (scene != RasshiineProductionScene.DevLog)
            {
                CancelAiEvaluationSnapshotPolling();
            }
            if (leavingBattleStage)
            {
                battleController?.ClearBattleStage();
                battleController?.SetControlledParticipant(null);
            }

            sceneRouter?.SetCurrentScene(scene);
            ConfigureBattleStatePolling(scene);
        }

        private void ConfigureBattleStatePolling(RasshiineProductionScene scene)
        {
#if UNITY_EDITOR || (UNITY_WEBGL && DEVELOPMENT_BUILD)
            if (webGlVisualQaActive)
            {
                StopBattleStatePolling();
                return;
            }
#endif
            var shouldPoll = scene == RasshiineProductionScene.Battle || scene == RasshiineProductionScene.FrontDisplay;
            if (!shouldPoll)
            {
                StopBattleStatePolling();
                return;
            }

            var useFrontDisplaySnapshot = scene == RasshiineProductionScene.FrontDisplay;
            if (battleStatePollingRoutine != null && pollingFrontDisplaySnapshot == useFrontDisplaySnapshot)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                return;
            }

            StopBattleStatePolling();
            pollingFrontDisplaySnapshot = useFrontDisplaySnapshot;
            var generation = battleStatePollGeneration;
            battleStatePollingRoutine = StartCoroutine(PollBattleState(useFrontDisplaySnapshot, generation));
        }

        private void StopBattleStatePolling()
        {
            // Never interrupt a nested UnityWebRequest: doing so skips the busy-state
            // cleanup in both layers. Invalidate the generation and let any in-flight
            // request finish before its stale poll exits.
            battleStatePollGeneration++;
            battleStatePollingRoutine = null;
        }

        private IEnumerator PollBattleState(bool useFrontDisplaySnapshot, int generation)
        {
            yield return new WaitForSeconds(InitialBattleStatePollDelaySeconds);
            while (generation == battleStatePollGeneration)
            {
                if (supabase is { IsConfigured: true } && !isNetworkBusy)
                {
                    if (useFrontDisplaySnapshot)
                    {
                        yield return RefreshRemoteSnapshot(null, true);
                    }
                    else
                    {
                        yield return RefreshRemoteBattleState();
                    }

                    if (generation != battleStatePollGeneration)
                    {
                        yield break;
                    }

                    if (useFrontDisplaySnapshot && activeProductionScene == RasshiineProductionScene.FrontDisplay)
                    {
                        ShowFrontScreen();
                    }
                    else if (!useFrontDisplaySnapshot && activeProductionScene == RasshiineProductionScene.Battle && currentUser != null)
                    {
                        ShowBattle();
                    }
                }

                yield return new WaitForSeconds(BattleStatePollIntervalSeconds);
            }
        }

        private IEnumerator RefreshRemoteBattleState()
        {
            isNetworkBusy = true;
            SupabaseGameApiResponseDto response = null;
            yield return supabase.GetBattleState(result => response = result);
            isNetworkBusy = false;
            if (ReturnToLoginIfRemoteSessionExpired(response))
            {
                yield break;
            }

            if (response?.Ok == true)
            {
                ApplyRemoteBattleState(response);
            }
        }

        private void RequestLogout()
        {
            if (isNetworkBusy)
            {
                // Shared-device safety wins over waiting for a background refresh. The
                // login boundary cancels all owned coroutines, clears the validated
                // bearer, and discards authenticated UI state immediately. A detached
                // client retains only the captured bearer long enough to revoke it on
                // the server across the login-scene transition.
                SessionRevocationDispatcher.Enqueue(supabase?.CreateSessionRevocationClient());
                ShowLogin();
                return;
            }

            if (!Application.isPlaying || supabase is not { IsConfigured: true, HasSession: true })
            {
                ShowLogin();
                return;
            }

            isNetworkBusy = true;
            StartCoroutine(LogoutRemoteThenShowLogin());
        }

        private IEnumerator LogoutRemoteThenShowLogin()
        {
            SupabaseGameApiResponseDto response = null;
            yield return supabase.Logout(result => response = result);
            isNetworkBusy = false;

            // Local credentials must always be discarded even when the network is unavailable.
            // When the request succeeds, SupabaseGameClient has already revoked and cleared the
            // server session before this reset runs.
            if (response?.Ok != true)
            {
                Debug.LogWarning("Server logout could not be confirmed; retrying revocation after clearing the local session.");
                SessionRevocationDispatcher.Enqueue(supabase?.CreateSessionRevocationClient());
            }

            ShowLogin();
        }

        private void ShowLogin()
        {
            // Unity does not guarantee iterator finally blocks when a MonoBehaviour
            // coroutine is stopped. Detach the possibly busy client first and keep only
            // a clean configured anonymous clone for the next login attempt.
            var anonymousLoginClient = supabase?.CreateAnonymousClient();
            if (Application.isPlaying)
            {
                StopAllCoroutines();
            }
            if (anonymousLoginClient != null)
            {
                supabase = anonymousLoginClient;
            }
            isNetworkBusy = false;

            if (loginIdInput != null && !string.IsNullOrWhiteSpace(loginIdInput.text))
            {
                retainedLoginId = loginIdInput.text.Trim();
            }

            MarkScene(RasshiineProductionScene.Login);
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Login);
            currentUser = null;
            frontDisplayRepository = null;
            ResetCosmeticState();
            ClearAuthenticatedUiState();
            pendingInitialPassword = string.Empty;
            initialPasswordChangeMessage = string.Empty;
            RasshiineRuntimeSession.Clear();
            supabase?.ClearSession();
            battleController?.SetControlledParticipant(null);
            if (ShouldLoadLoginScene())
            {
                sceneRouter?.ReturnToLogin();
                return;
            }

            ClearRoot();
            var portraitLogin = UsesPortraitLoginLayout();
            ResolveLoginComposition(
                portraitLogin,
                out var brandAnchorMin,
                out var brandAnchorMax,
                out var panelAnchorMin,
                out var panelAnchorMax);
            CreateLoginSunRays(portraitLogin);
            CreateLoginBrand(brandAnchorMin, brandAnchorMax, portraitLogin);

            var panel = CreateLoginFramedSurface(
                root,
                "LoginPanel",
                panelAnchorMin,
                panelAnchorMax,
                LoginFrameColor,
                LoginPanelSurfaceColor,
                10f,
                "LoginPanelFill",
                "LoginPanelDedicatedFrameSlot",
                theme.LoginPanelFrame);

            var contentLayer = CreateLoginContentLayer(panel);
            PrewarmLoginFontGlyphs();
            CreateLoginDivider(contentLayer);
            var loginLabelColor = new Color(theme.Text.r, theme.Text.g, theme.Text.b, 0.96f);
            if (Application.isPlaying)
            {
                StartCoroutine(CreateDeferredLoginLabels(contentLayer, loginLabelColor));
            }
            else
            {
                CreateLoginLabels(contentLayer, loginLabelColor);
            }

            loginIdInput = ui.CreateInput(contentLayer, "LoginIdInput", string.Empty);
            ConfigureLoginInputTypography(loginIdInput, 16);
            loginIdInput.text = retainedLoginId;
            SetAnchored(loginIdInput.GetComponent<RectTransform>(), new Vector2(0.08f, 0.625f), new Vector2(0.92f, 0.765f));

            passwordInput = ui.CreateInput(contentLayer, "PasswordInput", string.Empty);
            ConfigureLoginInputTypography(passwordInput, 16);
            passwordInput.contentType = InputField.ContentType.Password;
            passwordInput.ForceLabelUpdate();
            SetAnchored(passwordInput.GetComponent<RectTransform>(), new Vector2(0.08f, 0.405f), new Vector2(0.92f, 0.545f));

            var statusArea = CreateLoginFramedSurface(
                contentLayer,
                "LoginStatusArea",
                new Vector2(0.08f, 0.24f),
                new Vector2(0.92f, 0.39f),
                new Color(theme.Gold.r, theme.Gold.g, theme.Gold.b, 0.62f),
                LoginStatusSurfaceColor,
                7f,
                "LoginStatusAreaFill",
                "LoginStatusDedicatedFrameSlot",
                theme.LoginStatusFrame);
            statusArea.gameObject.AddComponent<RectMask2D>();

            var hasLoginError = !string.IsNullOrWhiteSpace(loginErrorMessage);
            var statusMessage = hasLoginError ? loginErrorMessage : "ID・パスワードを入力";
            loginStatusText = ui.CreateText(
                statusArea,
                "LoginStatusMessage",
                TruncateLoginStatus(statusMessage),
                ResolveLoginFontSize(14),
                FontStyle.Bold,
                hasLoginError ? theme.Danger : theme.MutedText,
                TextAnchor.MiddleCenter);
            DisableTextFitGuard(loginStatusText);
            loginStatusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            loginStatusText.verticalOverflow = VerticalWrapMode.Truncate;
            loginStatusText.resizeTextForBestFit = false;
            loginStatusText.lineSpacing = 0.9f;
            SetAnchored(loginStatusText.rectTransform, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));

            loginButton = CreateLoginLiveButton(contentLayer, "Login", "ログイン", SubmitLogin);
            SetAnchored(loginButton.GetComponent<RectTransform>(), new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.21f));
            ConfigureLoginKeyboardNavigation(loginIdInput, passwordInput, loginButton);
            ConfigureLoginSubmitHandlers(loginIdInput, passwordInput);
            UpdateLoginStatus(statusMessage, hasLoginError);
            SetLoginBusyState(isNetworkBusy);
            AddDeveloperNavigation();
            StartCoroutine(RefreshLoginTextGeometry());
        }

        private void CreateLoginSunRays(bool portraitLogin)
        {
            if (theme?.LoginSunRays == null)
            {
                return;
            }

            var raysObject = new GameObject("LoginSunRays", typeof(RectTransform), typeof(Image));
            raysObject.transform.SetParent(root, false);
            var rays = raysObject.GetComponent<Image>();
            rays.sprite = theme.LoginSunRays;
            rays.type = Image.Type.Simple;
            rays.preserveAspect = false;
            rays.color = new Color(1f, 0.96f, 0.84f, 0.58f);
            rays.raycastTarget = false;
            SetAnchored(
                rays.rectTransform,
                portraitLogin ? new Vector2(0f, 0.45f) : new Vector2(0f, 0.25f),
                portraitLogin ? new Vector2(1f, 1f) : new Vector2(0.58f, 1f));
            raysObject.transform.SetAsFirstSibling();
        }

        private IEnumerator CreateDeferredLoginLabels(RectTransform contentLayer, Color loginLabelColor)
        {
            yield return null;
            if (contentLayer == null || !contentLayer.gameObject.activeInHierarchy)
            {
                yield break;
            }

            CreateLoginLabels(contentLayer, loginLabelColor);
            Canvas.ForceUpdateCanvases();
        }

        private void CreateLoginLabels(RectTransform contentLayer, Color loginLabelColor)
        {
            if (contentLayer == null || contentLayer.Find("LoginTitleTextSurface") != null)
            {
                return;
            }

            var compactLandscape = !UsesPortraitLoginLayout() && ResolveLoginViewportSize().x < 1000f;
            AddLoginAnchoredDisplayText(
                contentLayer,
                "LoginTitleText",
                "ログイン",
                20,
                theme.Text,
                compactLandscape ? new Vector2(0.08f, 0.865f) : new Vector2(0.08f, 0.875f),
                compactLandscape ? new Vector2(0.92f, 0.955f) : new Vector2(0.92f, 0.94f),
                TextAnchor.MiddleCenter);
            AddLoginAnchoredText(contentLayer, "LoginIdLabel", "ログインID", 14, FontStyle.Bold, loginLabelColor, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.83f), TextAnchor.MiddleLeft);
            AddLoginAnchoredText(contentLayer, "PasswordLabel", "パスワード", 14, FontStyle.Bold, loginLabelColor, new Vector2(0.08f, 0.56f), new Vector2(0.92f, 0.61f), TextAnchor.MiddleLeft);
        }

        private void PrewarmLoginFontGlyphs()
        {
            if (theme?.UiDisplayFont != null && theme.UiDisplayFont.dynamic)
            {
                theme.UiDisplayFont.RequestCharactersInTexture("ログイン", ResolveLoginFontSize(20), FontStyle.Normal);
            }

            if (theme?.UiTitleFont != null && theme.UiTitleFont.dynamic)
            {
                theme.UiTitleFont.RequestCharactersInTexture(
                    "ログインIDパスワード・を入力してください接続中",
                    ResolveLoginFontSize(14),
                    FontStyle.Normal);
            }
        }

        private IEnumerator RefreshLoginTextGeometry()
        {
            yield return null;
            var texts = root != null ? root.GetComponentsInChildren<Text>(true) : Array.Empty<Text>();
            foreach (var text in texts)
            {
                if (text == null || string.IsNullOrEmpty(text.text) || text.font == null)
                {
                    continue;
                }

                if (text.font.dynamic)
                {
                    text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
                }
            }

            yield return null;
            foreach (var text in texts)
            {
                if (text == null)
                {
                    continue;
                }

                text.cachedTextGenerator.Invalidate();
                text.cachedTextGeneratorForLayout.Invalidate();
                text.SetAllDirty();
            }

            Canvas.ForceUpdateCanvases();
        }

        private RectTransform CreateLoginContentLayer(RectTransform panel)
        {
            var layerObject = new GameObject("LoginContentLayer", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            layerObject.transform.SetParent(panel, false);
            var layer = layerObject.GetComponent<RectTransform>();
            ui.Stretch(layer, 0f, 0f, 0f, 0f);
            var canvas = layerObject.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1;
            return layer;
        }

        private void ResolveLoginComposition(
            bool portraitLogin,
            out Vector2 brandAnchorMin,
            out Vector2 brandAnchorMax,
            out Vector2 panelAnchorMin,
            out Vector2 panelAnchorMax)
        {
            if (portraitLogin)
            {
                brandAnchorMin = new Vector2(0.07f, 0.75f);
                brandAnchorMax = new Vector2(0.93f, 0.96f);
                panelAnchorMin = new Vector2(0.07f, 0.165f);
                panelAnchorMax = new Vector2(0.93f, 0.72f);
                return;
            }

            var compactLandscape = ResolveLoginViewportSize().x < 1000f;
            if (compactLandscape)
            {
                brandAnchorMin = new Vector2(0.40f, 0.71f);
                brandAnchorMax = new Vector2(0.72f, 0.955f);
                panelAnchorMin = new Vector2(0.055f, 0.12f);
                panelAnchorMax = new Vector2(0.365f, 0.94f);
                return;
            }

            brandAnchorMin = new Vector2(0.055f, 0.70f);
            brandAnchorMax = new Vector2(0.36f, 0.95f);
            panelAnchorMin = new Vector2(0.08f, 0.11f);
            panelAnchorMax = new Vector2(0.35f, 0.66f);
        }

        private void CreateLoginBrand(Vector2 anchorMin, Vector2 anchorMax, bool portraitLogin)
        {
            var brand = new GameObject("LoginBrand", typeof(RectTransform)).GetComponent<RectTransform>();
            brand.SetParent(root, false);
            SetAnchored(brand, anchorMin, anchorMax);

            // Dedicated generated ornament hook. The selected option uses the existing crest now;
            // a transparent frame-only sprite can be assigned to this slot without changing layout.
            CreateLoginAssetSlot(brand, "LoginBrandDedicatedDecorationSlot", null);

            var crestObject = new GameObject("LoginCrest", typeof(RectTransform), typeof(Image));
            crestObject.transform.SetParent(brand, false);
            var crest = crestObject.GetComponent<Image>();
            crest.sprite = theme.WaypointCompass;
            crest.preserveAspect = true;
            crest.color = theme.WaypointCompass != null ? Color.white : Color.clear;
            crest.raycastTarget = false;
            var crestRect = crest.rectTransform;
            crestRect.anchorMin = new Vector2(0.5f, 0.67f);
            crestRect.anchorMax = crestRect.anchorMin;
            crestRect.pivot = new Vector2(0.5f, 0.5f);
            var viewport = ResolveLoginViewportSize();
            var crestPixels = portraitLogin ? 82f : viewport.x < 1000f ? 88f : 150f;
            var crestSize = ResolveLoginUiLength(crestPixels);
            crestRect.sizeDelta = new Vector2(crestSize, crestSize);
            crestRect.anchoredPosition = Vector2.zero;

            var brandPixels = portraitLogin ? 22 : viewport.x < 1000f ? 18 : 30;
            var title = ui.CreateDisplayText(
                brand,
                "LoginBrandTitle",
                "Attack On Rasshiine",
                ResolveLoginFontSize(brandPixels),
                LoginBrandGold,
                TextAnchor.MiddleCenter);
            DisableTextFitGuard(title);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = ResolveLoginFontSize(16);
            title.resizeTextMaxSize = ResolveLoginFontSize(brandPixels);
            SetAnchored(title.rectTransform, new Vector2(0.02f, 0.03f), new Vector2(0.98f, 0.32f));
            var shadow = title.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.01f, 0.025f, 0.06f, 0.78f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            shadow.useGraphicAlpha = true;
        }

        private RectTransform CreateLoginFramedSurface(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color frameColor,
            Color fillColor,
            float inset,
            string fillName,
            string dedicatedFrameSlotName,
            Sprite dedicatedFrame)
        {
            var surfaceObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            surfaceObject.transform.SetParent(parent, false);
            var surface = surfaceObject.GetComponent<RectTransform>();
            SetAnchored(surface, anchorMin, anchorMax);
            var frame = surfaceObject.GetComponent<Image>();
            var usesIntegratedPanelArt = dedicatedFrame != null && name == "LoginPanel";
            frame.sprite = usesIntegratedPanelArt ? dedicatedFrame : null;
            frame.type = usesIntegratedPanelArt ? Image.Type.Sliced : Image.Type.Simple;
            frame.color = usesIntegratedPanelArt ? Color.white : dedicatedFrame != null ? Color.clear : frameColor;
            frame.raycastTarget = false;
            if (usesIntegratedPanelArt)
            {
                var shadow = surfaceObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0.005f, 0.018f, 0.045f, 0.66f);
                shadow.effectDistance = new Vector2(5f, -7f);
                shadow.useGraphicAlpha = true;
            }

            var fillObject = new GameObject(fillName, typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(surface, false);
            var fill = fillObject.GetComponent<Image>();
            fill.sprite = null;
            fill.type = Image.Type.Simple;
            fill.color = usesIntegratedPanelArt ? Color.clear : fillColor;
            fill.raycastTarget = false;
            ui.Stretch(fill.rectTransform, inset, inset, -inset, -inset);

            // Generated login-only 9-slice frames are connected here by the theme pass.
            CreateLoginAssetSlot(surface, dedicatedFrameSlotName, usesIntegratedPanelArt ? null : dedicatedFrame);
            return surface;
        }

        private void CreateLoginAssetSlot(Transform parent, string name, Sprite sprite)
        {
            var slotObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            slotObject.transform.SetParent(parent, false);
            var slot = slotObject.GetComponent<Image>();
            slot.sprite = sprite;
            slot.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            slot.color = sprite != null ? Color.white : Color.clear;
            slot.raycastTarget = false;
            ui.Stretch(slot.rectTransform, 0f, 0f, 0f, 0f);
        }

        private void CreateLoginDivider(RectTransform panel)
        {
            var dividerObject = new GameObject("LoginHeadingDivider", typeof(RectTransform), typeof(Image));
            dividerObject.transform.SetParent(panel, false);
            var divider = dividerObject.GetComponent<Image>();
            divider.sprite = theme.LoginDivider;
            divider.type = Image.Type.Simple;
            divider.preserveAspect = true;
            divider.color = theme.LoginDivider != null ? Color.white : new Color(theme.Gold.r, theme.Gold.g, theme.Gold.b, 0.72f);
            divider.raycastTarget = false;
            var compactLandscape = !UsesPortraitLoginLayout() && ResolveLoginViewportSize().x < 1000f;
            SetAnchored(
                divider.rectTransform,
                compactLandscape ? new Vector2(0.14f, 0.82f) : new Vector2(0.14f, 0.835f),
                compactLandscape ? new Vector2(0.86f, 0.85f) : new Vector2(0.86f, 0.87f));
        }

        private Text AddLoginAnchoredText(
            RectTransform parent,
            string name,
            string value,
            int targetPixels,
            FontStyle style,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            TextAnchor alignment)
        {
            var labelSurface = CreateLoginLabelSurface(parent, $"{name}Surface", anchorMin, anchorMax);
            var text = ui.CreateText(labelSurface, name, value, ResolveLoginFontSize(targetPixels), style, color, alignment);
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            ui.Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
            AddLoginTextShadow(text);
            return text;
        }

        private Text AddLoginAnchoredDisplayText(
            RectTransform parent,
            string name,
            string value,
            int targetPixels,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            TextAnchor alignment)
        {
            var labelSurface = CreateLoginLabelSurface(parent, $"{name}Surface", anchorMin, anchorMax);
            var text = ui.CreateDisplayText(labelSurface, name, value, ResolveLoginFontSize(targetPixels), color, alignment);
            DisableTextFitGuard(text);
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            ui.Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
            AddLoginTextShadow(text);
            return text;
        }

        private RectTransform CreateLoginLabelSurface(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var surfaceObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            surfaceObject.transform.SetParent(parent, false);
            var surface = surfaceObject.GetComponent<RectTransform>();
            SetAnchored(surface, anchorMin, anchorMax);
            var image = surfaceObject.GetComponent<Image>();
            image.sprite = null;
            image.color = new Color(0.006f, 0.028f, 0.074f, 0.18f);
            image.raycastTarget = false;
            return surface;
        }

        private static void AddLoginTextShadow(Text text)
        {
            if (text == null || text.GetComponent<Shadow>() != null)
            {
                return;
            }

            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.005f, 0.015f, 0.035f, 0.88f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            shadow.useGraphicAlpha = true;
        }

        private void ConfigureLoginInputTypography(InputField input, int targetPixels)
        {
            if (input == null)
            {
                return;
            }

            var fontSize = ResolveLoginFontSize(targetPixels);
            var heatVisual = input.transform.Find("HeatInputFieldPrefabVisual");
            if (heatVisual != null)
            {
                heatVisual.gameObject.SetActive(false);
            }

            var inputImage = input.GetComponent<Image>();
            if (inputImage != null)
            {
                inputImage.sprite = theme.LoginInputFrame;
                inputImage.type = theme.LoginInputFrame != null ? Image.Type.Sliced : Image.Type.Simple;
                inputImage.color = theme.LoginInputFrame != null ? Color.white : new Color(theme.Gold.r, theme.Gold.g, theme.Gold.b, 0.72f);
            }

            var fillObject = new GameObject("LoginInputFill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(input.transform, false);
            var fill = fillObject.GetComponent<Image>();
            fill.sprite = null;
            fill.type = Image.Type.Simple;
            fill.color = LoginStatusSurfaceColor;
            fill.raycastTarget = false;
            ui.Stretch(fill.rectTransform, 6f, 6f, -6f, -6f);
            fillObject.transform.SetAsFirstSibling();

            if (input.textComponent != null)
            {
                input.textComponent.fontSize = fontSize;
                input.textComponent.resizeTextForBestFit = false;
                input.textComponent.transform.SetAsLastSibling();
            }

            if (input.placeholder is Text placeholder)
            {
                placeholder.fontSize = fontSize;
                placeholder.resizeTextForBestFit = false;
                placeholder.transform.SetAsLastSibling();
            }
        }

        private Button CreateLoginLiveButton(
            Transform parent,
            string name,
            string label,
            UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.GetComponent<Image>();
            var loginButtonSprite = theme.LoginCtaButton != null ? theme.LoginCtaButton : theme.PrimaryButton;
            image.sprite = loginButtonSprite;
            image.type = loginButtonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = loginButtonSprite != null ? Color.white : new Color(0.03f, 0.12f, 0.28f, 1f);
            if (loginButtonSprite != null)
            {
                image.pixelsPerUnitMultiplier = 1.5f;
            }
            image.raycastTarget = true;

            // Dedicated generated CTA sprite hook: replace this Image.sprite through the theme.
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.90f, 0.96f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.76f, 0.86f, 0.98f, 1f);
            colors.disabledColor = new Color(0.38f, 0.44f, 0.54f, 0.72f);
            button.colors = colors;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            loginButtonText = ui.CreateDisplayText(
                buttonObject.transform,
                "LoginButtonLabel",
                label,
                ResolveLoginFontSize(18),
                theme.Text,
                TextAnchor.MiddleCenter);
            DisableTextFitGuard(loginButtonText);
            loginButtonText.horizontalOverflow = HorizontalWrapMode.Wrap;
            loginButtonText.verticalOverflow = VerticalWrapMode.Truncate;
            loginButtonText.resizeTextForBestFit = true;
            loginButtonText.resizeTextMinSize = ResolveLoginFontSize(16);
            loginButtonText.resizeTextMaxSize = ResolveLoginFontSize(18);
            ui.Stretch(loginButtonText.rectTransform, 12f, 6f, -12f, -6f);
            return button;
        }

        private void ConfigureLoginSubmitHandlers(InputField loginId, InputField password)
        {
            AddLoginSubmitHandler(loginId, () => FocusLoginInput(password));
            AddLoginSubmitHandler(password, SubmitLogin);
        }

        private static void AddLoginSubmitHandler(InputField input, UnityEngine.Events.UnityAction onSubmit)
        {
            if (input == null || onSubmit == null)
            {
                return;
            }

            var trigger = input.GetComponent<EventTrigger>() ?? input.gameObject.AddComponent<EventTrigger>();
            trigger.triggers ??= new List<EventTrigger.Entry>();
            var submit = new EventTrigger.Entry { eventID = EventTriggerType.Submit };
            submit.callback.AddListener(_ => onSubmit.Invoke());
            trigger.triggers.Add(submit);
        }

        private void SubmitLogin()
        {
            if (isNetworkBusy)
            {
                return;
            }

            var loginId = loginIdInput != null ? loginIdInput.text.Trim() : string.Empty;
            var password = passwordInput != null ? passwordInput.text : string.Empty;
            retainedLoginId = loginId;
            if (string.IsNullOrWhiteSpace(loginId) || string.IsNullOrWhiteSpace(password))
            {
                loginErrorMessage = "ログインIDとパスワードを入力してください";
                UpdateLoginStatus(loginErrorMessage, true);
                FocusLoginInput(string.IsNullOrWhiteSpace(loginId) ? loginIdInput : passwordInput);
                return;
            }

            loginErrorMessage = string.Empty;
            TryLogin(loginId, password);
        }

        private void SetLoginBusyState(bool busy)
        {
            if (loginButton != null)
            {
                loginButton.interactable = !busy;
            }

            if (loginButtonText != null)
            {
                loginButtonText.text = busy ? "接続中…" : "ログイン";
                loginButtonText.color = busy ? theme.MutedText : theme.Text;
            }

            if (loginIdInput != null)
            {
                loginIdInput.interactable = !busy;
            }

            if (passwordInput != null)
            {
                passwordInput.interactable = !busy;
            }
        }

        private void UpdateLoginStatus(string message, bool isError)
        {
            if (loginStatusText == null)
            {
                return;
            }

            loginStatusText.text = TruncateLoginStatus(message);
            loginStatusText.color = isError ? theme.Danger : isNetworkBusy ? theme.Gold : theme.MutedText;
        }

        private void HandleLoginFailure(string message)
        {
            isNetworkBusy = false;
            if (loginIdInput != null && !string.IsNullOrWhiteSpace(loginIdInput.text))
            {
                retainedLoginId = loginIdInput.text.Trim();
            }

            loginErrorMessage = message;
            if (passwordInput != null)
            {
                passwordInput.text = string.Empty;
                passwordInput.ForceLabelUpdate();
            }

            SetLoginBusyState(false);
            UpdateLoginStatus(loginErrorMessage, true);
            FocusLoginInput(passwordInput);
        }

        private void FocusLoginInput(InputField input)
        {
            if (input == null || !input.interactable)
            {
                return;
            }

            lastLoginFocusTarget = input;
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(input.gameObject);
            }

            input.Select();
            input.ActivateInputField();
        }

        private static string TruncateLoginStatus(string message)
        {
            var normalized = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
            if (normalized.Length <= LoginStatusDisplayCharacterLimit)
            {
                return normalized;
            }

            var visibleLength = LoginStatusDisplayCharacterLimit - 1;
            if (visibleLength > 0 && char.IsHighSurrogate(normalized[visibleLength - 1]))
            {
                visibleLength -= 1;
            }

            return $"{normalized.Substring(0, visibleLength).TrimEnd()}…";
        }

        private Vector2 ResolveLoginViewportSize()
        {
            if (Application.isPlaying && Screen.width > 0 && Screen.height > 0)
            {
                return new Vector2(Screen.width, Screen.height);
            }

            if (root == null)
            {
                return new Vector2(ReferenceUiWidth, ReferenceUiHeight);
            }

            var canvas = root.GetComponentInParent<Canvas>();
            var scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            return root.rect.size * scale;
        }

        private float ResolveLoginUiScale()
        {
            // When a canvas scaler is disabled (the layout-QA harness and a few
            // embedded-host scenarios), Canvas.scaleFactor is the authoritative
            // rendered-pixel conversion. Reconstructing it from a synthetic
            // viewport can drift enough to turn a nominal 48 px target into a
            // 36 px target on compact screens.
            if (root != null)
            {
                var canvas = root.GetComponentInParent<Canvas>();
                var scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
                if (canvas != null && (scaler == null || !scaler.enabled) && canvas.scaleFactor > 0f)
                {
                    return canvas.scaleFactor;
                }
            }

            var viewport = ResolveLoginViewportSize();
            var widthScale = Mathf.Max(0.01f, viewport.x / ReferenceUiWidth);
            var heightScale = Mathf.Max(0.01f, viewport.y / ReferenceUiHeight);
            return Mathf.Sqrt(widthScale * heightScale);
        }

        private int ResolveLoginFontSize(int targetPixels)
        {
            var scale = ResolveLoginUiScale();
            return Mathf.Clamp(Mathf.CeilToInt(targetPixels / scale), targetPixels, targetPixels * 4);
        }

        private float ResolveLoginUiLength(float targetPixels)
        {
            return targetPixels / ResolveLoginUiScale();
        }

        private float ResolveMinimumUiLength(float designUnits, float minimumRenderedPixels)
        {
            return Mathf.Max(designUnits, minimumRenderedPixels / ResolveLoginUiScale());
        }

        private bool UsesPortraitLoginLayout()
        {
            if (UsesPortraitLayout())
            {
                return true;
            }

            if (root == null)
            {
                return false;
            }

            var size = root.rect.size;
            return size.x > 0f && size.y > size.x * 1.25f;
        }

        private static void ConfigureLoginKeyboardNavigation(InputField loginId, InputField password, Button loginButton)
        {
            if (loginId == null || password == null || loginButton == null)
            {
                return;
            }

            loginId.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnDown = password
            };
            password.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = loginId,
                selectOnDown = loginButton
            };
            loginButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = password
            };
        }

        private void TryLogin(string loginId, string password)
        {
            retainedLoginId = string.IsNullOrWhiteSpace(loginId) ? retainedLoginId : loginId.Trim();
            if (supabase is { IsConfigured: true })
            {
                if (!isNetworkBusy)
                {
                    StartCoroutine(TrySupabaseLogin(loginId, password));
                }
                return;
            }

            if (supabase is { CanUseDemoRepositoryFallback: true })
            {
                TryLocalLogin(loginId, password);
                return;
            }

            HandleLoginFailure(RemoteErrorMessage("本番APIに接続できません。設定と通信状態を確認してください。"));
        }

        private void TryLocalLogin(string loginId, string password)
        {
            var user = repository.Authenticate(loginId, password);
            if (user == null)
            {
                HandleLoginFailure("IDまたはパスワードが違います");
                return;
            }

            CompleteLogin(user, password);
        }

        private IEnumerator TrySupabaseLogin(string loginId, string password)
        {
            isNetworkBusy = true;
            SetLoginBusyState(true);
            UpdateLoginStatus("冒険者の記録を確認しています…", false);
            SupabaseGameApiResponseDto response = null;
            yield return supabase.Login(loginId, password, result => response = result);

            var validUser = response?.User != null
                && !string.IsNullOrWhiteSpace(response.User.Id)
                && response.User.IsActive
                && Enum.IsDefined(typeof(UserRole), response.User.Role);
            var hasRequiredSnapshot = response?.User?.InitialPasswordChanged != true
                || HasRequiredAuthoritativeSnapshot(response.Snapshot);
            if (response?.Ok != true || !supabase.HasSession || !validUser || !hasRequiredSnapshot)
            {
                supabase.ClearSession();
                HandleLoginFailure(RemoteErrorMessage("IDまたはパスワードが違います"));
                yield break;
            }

            isNetworkBusy = false;
            SetLoginBusyState(false);
            ApplyRemoteSnapshot(response);
            CompleteLogin(response.User.ToDomain(), password);
        }

        private void CompleteLogin(UserProfile user, string password)
        {
            currentUser = user;
            ResetCosmeticState();
            RasshiineRuntimeSession.SetUser(user);
            RasshiineRuntimeSession.SetSessionToken(supabase.SessionToken);
            PersistRuntimeSnapshot();
            loginErrorMessage = string.Empty;
            initialPasswordChangeMessage = string.Empty;
            SyncBattleControlledParticipant();
            if (repository.RequiresInitialPasswordChange(currentUser))
            {
                pendingInitialPassword = password;
                ShowInitialPasswordChange();
                return;
            }

            pendingInitialPassword = string.Empty;
            ShowPostLoginHome();
        }

        private void ShowPostLoginHome()
        {
            if (TryApplyPendingWebRouteAfterLogin())
            {
                return;
            }

            if (TryLoadHomeScene())
            {
                return;
            }

            if (currentUser.Role == UserRole.Mentor)
            {
                ShowMentorDashboard();
            }
            else
            {
                ShowMemberHome();
            }
        }

        private bool TryApplyPendingWebRouteAfterLogin()
        {
            if (currentUser == null || !RasshiineWebRouteIntent.TryPeek(out var requestedScene))
            {
                return false;
            }

            if (requestedScene != RasshiineProductionScene.Battle
                && requestedScene != RasshiineProductionScene.FrontDisplay)
            {
                return false;
            }

            if (!RasshiineSceneCatalog.CanRoleAccessScene(currentUser.Role, requestedScene))
            {
                RasshiineWebRouteIntent.Consume(requestedScene);
                return false;
            }

            RasshiineWebRouteIntent.Consume(requestedScene);
            if (IsActiveProductionScene(requestedScene))
            {
                if (requestedScene == RasshiineProductionScene.Battle)
                {
                    ShowBattle();
                }
                else
                {
                    ShowFrontScreen();
                }

                return true;
            }

            sceneRouter.LoadScene(requestedScene);
            return true;
        }

        private void ShowInitialPasswordChange()
        {
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Login);
            ClearRoot();
            var compactLandscape = UsesCompactLandscapeLayout();
            var portrait = UsesPortraitLayout();
            var panel = ui.CreatePanel(
                root,
                "InitialPasswordPanel",
                theme.RaidPanel,
                compactLandscape ? new Vector2(0.10f, 0.06f) : portrait ? new Vector2(0.07f, 0.13f) : new Vector2(0.22f, 0.16f),
                compactLandscape ? new Vector2(0.90f, 0.94f) : portrait ? new Vector2(0.93f, 0.86f) : new Vector2(0.78f, 0.84f),
                Vector2.zero,
                Vector2.zero);

            if (compactLandscape)
            {
                BuildCompactInitialPasswordChange(panel);
                return;
            }

            AddVertical(panel, portrait ? 18 : 24, portrait ? 12 : 18, TextAnchor.UpperCenter);
            var title = AddDisplayText(panel, "初回パスワード変更", portrait ? 31 : 42, theme.Text, portrait ? 50 : 62, TextAnchor.MiddleCenter);
            PreserveFullSingleLineLabel(title, portrait ? 22 : 28);
            AddText(panel, $"{Shorten(currentUser.Nickname, portrait ? 13 : 20)} / {UserRoleLabel(currentUser.Role)}", portrait ? 19 : 24, FontStyle.Bold, theme.Cyan, portrait ? 34 : 40, TextAnchor.MiddleCenter);
            AddText(panel, "安全のため、初期パスワードを変更してから開始します。", portrait ? 17 : 21, FontStyle.Bold, theme.Warning, portrait ? 54 : 42, TextAnchor.MiddleCenter);
            AddInitialPasswordFieldsAndActions(panel, portrait ? 58 : 66, portrait ? 58 : 72);
        }

        private void BuildCompactInitialPasswordChange(RectTransform panel)
        {
            AddHorizontal(panel, 14, 12);
            var introduction = ui.CreatePanel(panel, "InitialPasswordIntroduction", theme.LogPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(introduction.gameObject, 0.40f, -1);
            AddVertical(introduction, 12, 7, TextAnchor.MiddleCenter);
            AddText(introduction, "FIRST LOGIN", 12, FontStyle.Bold, theme.Gold, 20, TextAnchor.MiddleCenter);
            var title = AddDisplayText(introduction, "初回パスワード変更", 27, theme.Text, 52, TextAnchor.MiddleCenter);
            PreserveFullSingleLineLabel(title, 19);
            AddText(introduction, $"{Shorten(currentUser.Nickname, 10)} / {UserRoleLabel(currentUser.Role)}", 16, FontStyle.Bold, theme.Cyan, 30, TextAnchor.MiddleCenter);
            AddText(introduction, "初期パスワードを変更して\n安全に利用を開始します", 15, FontStyle.Bold, theme.Warning, 62, TextAnchor.MiddleCenter);

            var form = ui.CreatePanel(panel, "InitialPasswordForm", theme.RaidPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(form.gameObject, 0.60f, -1);
            AddVertical(form, 10, 8, TextAnchor.MiddleCenter);
            AddInitialPasswordFieldsAndActions(form, 54, 52);
        }

        private void AddInitialPasswordFieldsAndActions(Transform parent, float fieldHeight, float buttonHeight)
        {
            initialNewPasswordInput = ui.CreateInput(parent, "InitialNewPasswordInput", "新しいパスワード");
            initialNewPasswordInput.contentType = InputField.ContentType.Password;
            AddLayout(initialNewPasswordInput.gameObject, -1, fieldHeight);
            initialConfirmPasswordInput = ui.CreateInput(parent, "InitialConfirmPasswordInput", "新しいパスワードを再入力");
            initialConfirmPasswordInput.contentType = InputField.ContentType.Password;
            AddLayout(initialConfirmPasswordInput.gameObject, -1, fieldHeight);

            if (!string.IsNullOrWhiteSpace(initialPasswordChangeMessage))
            {
                AddText(parent, Shorten(initialPasswordChangeMessage, 34), 16, FontStyle.Bold, theme.Danger, 38, TextAnchor.MiddleCenter);
            }

            var start = ui.CreateButton(parent, "CompleteInitialPassword", "変更して開始", theme.PrimaryButton, TryCompleteInitialPasswordChange, Color.white);
            AddLayout(start.gameObject, -1, buttonHeight);
            var logout = ui.CreateButton(parent, "InitialPasswordLogout", "ログアウト", theme.SecondaryButton, RequestLogout, theme.Text);
            AddLayout(logout.gameObject, -1, buttonHeight);
        }

        private void TryCompleteInitialPasswordChange()
        {
            var newPassword = initialNewPasswordInput.text;
            var confirmPassword = initialConfirmPasswordInput.text;
            if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            {
                initialPasswordChangeMessage = "確認用パスワードが一致しません。";
                ShowInitialPasswordChange();
                return;
            }

            if (supabase is { IsConfigured: true })
            {
                if (!supabase.HasSession)
                {
                    initialPasswordChangeMessage = "セッション期限切れです。再ログインしてください。";
                    ShowLogin();
                    return;
                }

                if (isNetworkBusy)
                {
                    initialPasswordChangeMessage = "通信中です。少し待ってから操作してください。";
                    ShowInitialPasswordChange();
                    return;
                }

                StartCoroutine(ChangeRemotePassword(pendingInitialPassword, newPassword));
                return;
            }

            if (HandleUnavailableAuthoritativeBackend())
            {
                return;
            }

            try
            {
                CompleteInitialPasswordChange(repository.ChangePassword(currentUser.Id, pendingInitialPassword, newPassword));
            }
            catch (Exception exception)
            {
                initialPasswordChangeMessage = exception.Message;
                ShowInitialPasswordChange();
            }
        }

        private IEnumerator ChangeRemotePassword(string currentPassword, string newPassword)
        {
            isNetworkBusy = true;
            SupabaseGameApiResponseDto response = null;
            yield return supabase.ChangePassword(currentPassword, newPassword, result => response = result);
            isNetworkBusy = false;
            if (ReturnToLoginIfRemoteSessionExpired(response))
            {
                yield break;
            }

            if (response?.Ok == true)
            {
                ApplyRemoteSnapshot(response);
                CompleteInitialPasswordChange(response.User?.ToDomain() ?? currentUser);
                yield break;
            }

            initialPasswordChangeMessage = RemoteErrorMessage("パスワードを変更できませんでした。入力内容と通信状態を確認してください。");
            ShowInitialPasswordChange();
        }

        private void CompleteInitialPasswordChange(UserProfile user)
        {
            currentUser = user;
            currentUser.InitialPasswordChanged = true;
            pendingInitialPassword = string.Empty;
            initialPasswordChangeMessage = string.Empty;
            RasshiineRuntimeSession.SetUser(currentUser);
            // The password RPC rotates the bearer and revokes the previous token. Carry
            // the client's validated fresh token into the authenticated home scene.
            RasshiineRuntimeSession.SetSessionToken(supabase.SessionToken);
            PersistRuntimeSnapshot();
            ShowPostLoginHome();
        }

        private bool TryLoadHomeScene()
        {
            if (!RasshiineSceneCatalog.TryGetSceneByName(SceneManager.GetActiveScene().name, out var scene))
            {
                return false;
            }

            if (scene == RasshiineProductionScene.DevLog && currentUser.Role == UserRole.Member)
            {
                sceneRouter.LoadScene(RasshiineProductionScene.DevLog);
                return true;
            }

            sceneRouter.LoadScene(RasshiineSceneCatalog.GetAuthenticatedHomeScene(currentUser.Role));
            return true;
        }

        private bool ShouldLoadLoginScene()
        {
            if (!RasshiineSceneCatalog.TryGetSceneByName(SceneManager.GetActiveScene().name, out var scene))
            {
                return false;
            }

            return RasshiineSceneCatalog.RequiresAuthenticatedUser(scene)
                && scene != RasshiineProductionScene.DevLog;
        }

        private void ShowMemberHome()
        {
            if (currentUser == null)
            {
                ShowLogin();
                return;
            }

            if (!RasshiineSceneCatalog.CanRoleAccessScene(currentUser.Role, RasshiineProductionScene.MemberHome))
            {
                ShowMentorDashboard();
                return;
            }

            MarkScene(RasshiineProductionScene.MemberHome);
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Home);
            ApplyCosmeticsToHomeHero();
            ClearRoot();
            var stats = repository.GetStats(currentUser.Id);
            var hud = new GameObject("MemberHomeHud", typeof(RectTransform)).GetComponent<RectTransform>();
            hud.SetParent(EnsureRoot(), false);
            ui.Stretch(hud, 0f, 0f, 0f, 0f);

            AddHomeNavigationArc(hud);
            AddHomePlayerStatus(hud, stats);
            AddHomeWorldLabel(hud);
            AddHomeDevelopmentCompass(hud);
            AddHomeNextRaidRibbon(hud);
            AddHomeWelcomeToast(hud);
            ApplyPortraitHomeLayout(hud);

            if (Application.isPlaying && cosmeticInventory == null && !cosmeticInventoryLoadAttempted &&
                !cosmeticRequestInFlight && !isNetworkBusy)
            {
                StartCoroutine(RefreshCosmeticInventory());
            }
        }

        private void ShowDevLog()
        {
            if (currentUser == null)
            {
                ShowLogin();
                return;
            }

            if (!RasshiineSceneCatalog.CanRoleAccessScene(currentUser.Role, RasshiineProductionScene.DevLog))
            {
                ShowMentorDashboard();
                return;
            }

            MarkScene(RasshiineProductionScene.DevLog);
            ClearRoot();
            AddHeader("開発ログ", string.Empty, ShowMemberHome);
            var useFullWidth = UsesPortraitLayout() || UsesCompactLandscapeLayout();
            var scroll = CreateScrollPanel(
                root,
                "DevLogScroll",
                new Vector2(useFullWidth ? 0.06f : 0.43f, 0.055f),
                new Vector2(useFullWidth ? 0.94f : 0.96f, 0.815f));
            var devLogState = devLogPresenter.Build(repository, currentUser, supabase is { IsConfigured: true }, isNetworkBusy);
            var active = devLogState.ActiveSession;

            var current = CreateColumn(scroll, "CurrentSession", theme.RaidPanel, 1f);
            AddText(current, active == null ? "今日の開発目標" : "現在の開発セッション", 32, FontStyle.Bold, theme.Text, 44, TextAnchor.MiddleCenter);
            var statusRow = CreateHudRow(current, "DevLogStatus", 72);
            AddFrontDisplayMetric(statusRow, "承認待ち", $"{devLogState.PendingCount}件", devLogState.PendingCount > 0 ? theme.Cyan : theme.MutedText, 24);
            AddFrontDisplayMetric(statusRow, "AI評価待ち", $"{devLogState.AiPendingCount}件", devLogState.AiPendingCount > 0 ? theme.Cyan : theme.MutedText, 24);
            AddFrontDisplayMetric(statusRow, "要確認", $"{devLogState.NeedsReviewCount}件", devLogState.NeedsReviewCount > 0 ? theme.Magenta : theme.MutedText, 24);
            if (devLogState.AiPendingCount > 0 && supabase is { IsConfigured: true })
            {
                var refreshAi = ui.CreateButton(current, "AiEvaluationRefresh", isNetworkBusy ? "AI評価を確認中…" : "AI評価を再確認", theme.SecondaryButton, () =>
                {
                    if (!TryRefreshRemoteSnapshot(ShowDevLog))
                    {
                        ShowDevLog();
                    }
                }, theme.Text);
                AddLayout(refreshAi.gameObject, -1, 54f);
                refreshAi.interactable = !isNetworkBusy;
            }
            if (active == null)
            {
                AddText(current, "今から取り組むことを1つだけ書いてください。", 19, FontStyle.Bold, theme.MutedText, 30, TextAnchor.MiddleCenter);
                goalInput = ui.CreateInput(current, "GoalInput", "例: ボス戦UIのコマンド表示を見やすくする");
                goalInput.text = retainedGoalDraft;
                goalInput.onValueChanged.AddListener(value => retainedGoalDraft = value);
                AddLayout(goalInput.gameObject, -1, 96);
                AddButton(current, "この目標で開始", theme.PrimaryButton, () =>
                {
                    var validation = devLogPresenter.ValidateStart(goalInput.text);
                    if (!validation.IsValid)
                    {
                        SetSessionFeedback(validation.Message, FeedbackTone.Warning);
                        ShowDevLog();
                        return;
                    }

                    if (TryStartRemoteSession(goalInput.text))
                    {
                        return;
                    }

                    repository.StartSession(currentUser.Id, goalInput.text);
                    retainedGoalDraft = string.Empty;
                    PersistRuntimeSnapshot();
                    SetSessionFeedback("開始しました。今日の目標に集中できます。", FeedbackTone.Success);
                    ShowDevLog();
                });
            }
            else
            {
                var activeView = devLogPresenter.ToView(active);
                var elapsed = DateTime.UtcNow - active.StartedAtUtc;
                var activeRow = CreateHudRow(current, "ActiveDevLogStatus", 78);
                AddFrontDisplayMetric(activeRow, "経過時間", $"{elapsed.Hours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}", theme.Cyan, 26);
                AddFrontDisplayMetric(activeRow, "状態", activeView.ReviewStateLabel, theme.Magenta, 20);
                AddText(current, $"目標  {Shorten(active.Goal, 58)}", 22, FontStyle.Bold, theme.Text, 38, TextAnchor.MiddleCenter);
                AddText(current, "達成度", 22, FontStyle.Bold, theme.Text, 30);
                achievementSlider = ui.CreateSlider(current, "AchievementSlider");
                achievementSlider.value = retainedAchievementRate;
                achievementSlider.onValueChanged.AddListener(value => retainedAchievementRate = value);
                AddLayout(achievementSlider.gameObject, -1, 58);
                AddText(current, "ふりかえり", 22, FontStyle.Bold, theme.Text, 30);
                reflectionInput = ui.CreateInput(current, "ReflectionInput", "今日進んだこと、詰まったこと、学んだこと", true);
                reflectionInput.text = retainedReflectionDraft;
                reflectionInput.onValueChanged.AddListener(value => retainedReflectionDraft = value);
                AddLayout(reflectionInput.gameObject, -1, 112);
                AddText(current, "次にやること", 22, FontStyle.Bold, theme.Text, 30);
                nextTaskInput = ui.CreateInput(current, "NextTaskInput", "次回すぐ着手できる具体的な作業", true);
                nextTaskInput.text = retainedNextTaskDraft;
                nextTaskInput.onValueChanged.AddListener(value => retainedNextTaskDraft = value);
                AddLayout(nextTaskInput.gameObject, -1, 86);
                AddButton(current, "開発を終了して記録する", theme.PrimaryButton, () =>
                {
                    var achievementRate = Mathf.RoundToInt(achievementSlider.value);
                    var validation = devLogPresenter.ValidateCompletion(achievementRate, reflectionInput.text, nextTaskInput.text);
                    if (!validation.IsValid)
                    {
                        SetSessionFeedback(validation.Message, FeedbackTone.Warning);
                        ShowDevLog();
                        return;
                    }

                    if (TryCompleteRemoteSession(active.Id, achievementRate, reflectionInput.text, nextTaskInput.text))
                    {
                        return;
                    }

                    var saved = repository.CompleteSession(currentUser.Id, achievementRate, reflectionInput.text, nextTaskInput.text);
                    ClearRetainedDevLogDrafts();
                    PersistRuntimeSnapshot();
                    SetSessionFeedback($"AI評価 {RankLabel(saved.Evaluation.Rank)} / 仮EXP +{saved.PreviewExp} / {StatusLabel(saved.Status)}", FeedbackTone.Success);
                    ShowDevLog();
                });
            }

            if (!string.IsNullOrWhiteSpace(lastSessionMessage))
            {
                AddFeedbackBanner(current, lastSessionMessage, lastSessionTone, 74);
            }

            var historyButton = ui.CreateButton(scroll, "OpenDevelopmentHistory", $"最近の記録を見る（{devLogState.History.Count}件）", theme.SecondaryButton, ShowMemberHistory);
            AddLayout(historyButton.gameObject, -1, 54);
        }

        private void ShowProducts()
        {
            ClearTemporaryPasswordReveal();
            ClearRoot();
            var isMentor = currentUser.Role == UserRole.Mentor;
            UnityEngine.Events.UnityAction backAction = isMentor
                ? (UnityEngine.Events.UnityAction)ShowMentorDashboard
                : ShowMemberHome;
            AddHeader("プロダクト", string.Empty, backAction);
            var useFullWidth = UsesPortraitLayout() || UsesCompactLandscapeLayout();
            var scroll = CreateScrollPanel(
                root,
                "ProductsScroll",
                isMentor || useFullWidth ? new Vector2(0.08f, 0.06f) : new Vector2(0.43f, 0.06f),
                isMentor || useFullWidth ? new Vector2(0.92f, 0.82f) : new Vector2(0.96f, 0.82f));

            if (UsesConstrainedLayout())
            {
                ShowResponsiveProducts(scroll, isMentor);
                return;
            }

            if (!isMentor)
            {
                var form = CreateDashboardSection(scroll, "ProductForm", 260, theme.RaidPanel);
                AddText(form, "作品URLを登録", 26, FontStyle.Bold, theme.Text, 34);
                var inputRow = new GameObject("ProductInputs", typeof(RectTransform));
                inputRow.transform.SetParent(form, false);
                AddLayout(inputRow, -1, 48);
                AddHorizontal(inputRow.GetComponent<RectTransform>(), 0, 10);
                productTitleInput = ui.CreateInput(inputRow.transform, "ProductTitleInput", "プロダクト名");
                productTitleInput.text = retainedProductTitleDraft;
                productTitleInput.onValueChanged.AddListener(value => retainedProductTitleDraft = value);
                AddLayout(productTitleInput.gameObject, 0.42f, -1);
                productUrlInput = ui.CreateInput(inputRow.transform, "ProductUrlInput", "https://example.com");
                productUrlInput.text = retainedProductUrlDraft;
                productUrlInput.onValueChanged.AddListener(value => retainedProductUrlDraft = value);
                AddLayout(productUrlInput.gameObject, 0.58f, -1);
                productDescriptionInput = ui.CreateInput(form, "ProductDescriptionInput", "紹介コメント", true);
                productDescriptionInput.text = retainedProductDescriptionDraft;
                productDescriptionInput.onValueChanged.AddListener(value => retainedProductDescriptionDraft = value);
                AddLayout(productDescriptionInput.gameObject, -1, 64);
                var registerButton = ui.CreateButton(form, "RegisterProductUrl", "公開URLを登録", theme.PrimaryButton, () =>
                {
                    if (TryRegisterRemoteProduct(productTitleInput.text, productUrlInput.text, productDescriptionInput.text))
                    {
                        return;
                    }

                    try
                    {
                        repository.RegisterProduct(currentUser.Id, productTitleInput.text, productUrlInput.text, productDescriptionInput.text);
                        ClearRetainedProductDrafts();
                        SetProductFeedback("登録しました。全員に公開されます。", FeedbackTone.Success);
                    }
                    catch (Exception exception)
                    {
                        SetProductFeedback(exception.Message, FeedbackTone.Danger);
                    }

                    ShowProducts();
                });
                AddLayout(registerButton.gameObject, -1, 48);
            }

            if (!string.IsNullOrWhiteSpace(lastProductMessage))
            {
                AddFeedbackBanner(scroll, lastProductMessage, lastProductTone, 74);
            }

            var list = CreateColumn(scroll, "ProductList", theme.LogPanel, 1f);
            AddText(list, isMentor ? "登録済みプロダクト" : "みんなのプロダクト", 34, FontStyle.Bold, theme.Text, 48);
            var productsToShow = isMentor ? repository.GetProductsForMentor() : repository.GetProductsForUser(currentUser.Id);
            if (productsToShow.Count == 0)
            {
                AddText(list, "まだ登録されたURLはありません。", 24, FontStyle.Bold, theme.MutedText, 44);
                return;
            }

            foreach (var product in productsToShow.Take(12))
            {
                AddProductSummary(list, product, isMentor);
            }
        }

        private void ShowAchievements()
        {
            ClearTemporaryPasswordReveal();
            ClearRoot();
            var isMentor = currentUser.Role == UserRole.Mentor;
            UnityEngine.Events.UnityAction backAction = isMentor
                ? (UnityEngine.Events.UnityAction)ShowMentorDashboard
                : ShowMemberHome;
            AddHeader("実績", string.Empty, backAction);
            var useFullWidth = UsesPortraitLayout() || UsesCompactLandscapeLayout();
            var scroll = CreateScrollPanel(
                root,
                "AchievementsScroll",
                isMentor || useFullWidth ? new Vector2(0.08f, 0.06f) : new Vector2(0.43f, 0.06f),
                isMentor || useFullWidth ? new Vector2(0.92f, 0.82f) : new Vector2(0.96f, 0.82f));

            if (UsesConstrainedLayout())
            {
                ShowResponsiveAchievements(scroll, isMentor);
                return;
            }

            if (!isMentor)
            {
                var form = CreateDashboardSection(scroll, "AchievementForm", 306, theme.RaidPanel);
                AddText(form, "実績を申請", 26, FontStyle.Bold, theme.Text, 34);
                AddSelectorRowCompact(form, Enum.GetValues(typeof(AchievementType)).Cast<AchievementType>(), selectedAchievementType, value =>
                {
                    selectedAchievementType = value;
                    ShowAchievements();
                }, AchievementTypeLabel);
                achievementTitleInput = ui.CreateInput(form, "AchievementTitleInput", "実績名");
                achievementTitleInput.text = retainedAchievementTitleDraft;
                achievementTitleInput.onValueChanged.AddListener(value => retainedAchievementTitleDraft = value);
                AddLayout(achievementTitleInput.gameObject, -1, 48);
                achievementDescriptionInput = ui.CreateInput(form, "AchievementDescriptionInput", "説明・URL・補足", true);
                achievementDescriptionInput.text = retainedAchievementDescriptionDraft;
                achievementDescriptionInput.onValueChanged.AddListener(value => retainedAchievementDescriptionDraft = value);
                AddLayout(achievementDescriptionInput.gameObject, -1, 58);
                var submitButton = ui.CreateButton(form, "SubmitAchievement", "申請する", theme.PrimaryButton, () =>
                {
                    if (TrySubmitRemoteAchievement(selectedAchievementType, achievementTitleInput.text, achievementDescriptionInput.text))
                    {
                        return;
                    }

                    try
                    {
                        repository.SubmitAchievement(currentUser.Id, selectedAchievementType, achievementTitleInput.text, achievementDescriptionInput.text);
                        ClearRetainedAchievementDrafts();
                        SetAchievementFeedback("申請しました。メンター承認後に報酬が反映されます。", FeedbackTone.Success);
                    }
                    catch (Exception exception)
                    {
                        SetAchievementFeedback(exception.Message, FeedbackTone.Danger);
                    }

                    ShowAchievements();
                });
                AddLayout(submitButton.gameObject, -1, 48);
            }

            if (!string.IsNullOrWhiteSpace(lastAchievementMessage))
            {
                AddFeedbackBanner(scroll, lastAchievementMessage, lastAchievementTone, 74);
            }

            var list = CreateColumn(scroll, "AchievementList", theme.LogPanel, 1f);
            AddText(list, isMentor ? "実績申請一覧" : "自分の実績申請", 34, FontStyle.Bold, theme.Text, 48);
            if (isMentor)
            {
                var pendingAchievements = repository.GetPendingAchievements();
                if (pendingAchievements.Count == 0)
                {
                    AddText(list, "承認待ちの実績申請はありません。", 24, FontStyle.Bold, theme.Cyan, 44);
                }
                else
                {
                    foreach (var achievement in pendingAchievements)
                    {
                        AddAchievementSummary(list, achievement, true);
                    }
                }

                AddText(list, "最近の実績履歴", 28, FontStyle.Bold, theme.Text, 42);
                foreach (var achievement in repository.GetRecentAchievements().Where(item => item.Status != AchievementStatus.Pending).Take(12))
                {
                    AddAchievementSummary(list, achievement, true);
                }
                return;
            }

            var achievementsToShow = repository.GetAchievementsForUser(currentUser.Id);
            if (achievementsToShow.Count == 0)
            {
                AddText(list, "実績申請はまだありません。", 24, FontStyle.Bold, theme.MutedText, 44);
                return;
            }

            foreach (var achievement in achievementsToShow.Take(12))
            {
                AddAchievementSummary(list, achievement, isMentor);
            }
        }

        private void ShowResponsiveProducts(Transform parent, bool isMentor)
        {
            if (!isMentor)
            {
                AddResponsivePaneTabs(
                    parent,
                    "Product",
                    "みんなの作品",
                    "作品を登録",
                    !memberProductFormVisible,
                    () =>
                    {
                        memberProductFormVisible = false;
                        ShowProducts();
                    },
                    () =>
                    {
                        memberProductFormVisible = true;
                        ShowProducts();
                    });

                if (memberProductFormVisible)
                {
                    AddResponsiveProductForm(parent);
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(lastProductMessage))
            {
                AddFeedbackBanner(parent, lastProductMessage, lastProductTone, 68);
            }

            var products = isMentor
                ? repository.GetProductsForMentor().ToList()
                : repository.GetProductsForUser(currentUser.Id).ToList();
            var list = CreateDashboardSection(parent, "ResponsiveProductList", -1f, theme.LogPanel);
            AddText(list, isMentor ? "登録済みプロダクト" : "みんなのプロダクト", 28, FontStyle.Bold, theme.Text, 40);
            if (products.Count == 0)
            {
                AddText(list, "まだ登録された作品はありません。", 18, FontStyle.Bold, theme.MutedText, 52, TextAnchor.MiddleCenter);
                return;
            }

            var pageSize = ResponsiveCollectionPageSize(2, 3, 12);
            productPage = ClampPage(productPage, products.Count, pageSize);
            foreach (var product in products.Skip(productPage * pageSize).Take(pageSize))
            {
                AddProductSummary(list, product, isMentor);
            }

            AddResponsivePagination(list, "Product", products.Count, pageSize, productPage, page =>
            {
                productPage = page;
                ShowProducts();
            });
        }

        private void AddResponsiveProductForm(Transform parent)
        {
            var form = CreateDashboardSection(parent, "ProductForm", -1f, theme.RaidPanel);
            AddText(form, "公開する作品", 24, FontStyle.Bold, theme.Text, 34);
            var identityRow = CreateHudRow(form, "ProductIdentityInputs", 54);
            productTitleInput = ui.CreateInput(identityRow, "ProductTitleInput", "プロダクト名");
            productTitleInput.text = retainedProductTitleDraft;
            productTitleInput.onValueChanged.AddListener(value => retainedProductTitleDraft = value);
            AddLayout(productTitleInput.gameObject, 0.42f, -1);
            productUrlInput = ui.CreateInput(identityRow, "ProductUrlInput", "https://example.com");
            productUrlInput.text = retainedProductUrlDraft;
            productUrlInput.onValueChanged.AddListener(value => retainedProductUrlDraft = value);
            AddLayout(productUrlInput.gameObject, 0.58f, -1);
            productDescriptionInput = ui.CreateInput(form, "ProductDescriptionInput", "ひとこと紹介", true);
            productDescriptionInput.text = retainedProductDescriptionDraft;
            productDescriptionInput.onValueChanged.AddListener(value => retainedProductDescriptionDraft = value);
            AddLayout(productDescriptionInput.gameObject, -1, 54);
            var register = ui.CreateButton(form, "RegisterProductUrl", "作品URLを公開する", theme.PrimaryButton, () =>
            {
                if (TryRegisterRemoteProduct(productTitleInput.text, productUrlInput.text, productDescriptionInput.text))
                {
                    return;
                }

                try
                {
                    repository.RegisterProduct(currentUser.Id, productTitleInput.text, productUrlInput.text, productDescriptionInput.text);
                    ClearRetainedProductDrafts();
                    memberProductFormVisible = false;
                    SetProductFeedback("登録しました。全員に公開されます。", FeedbackTone.Success);
                }
                catch (Exception exception)
                {
                    SetProductFeedback(exception.Message, FeedbackTone.Danger);
                }

                ShowProducts();
            });
            AddLayout(register.gameObject, -1, 54);
        }

        private void ShowResponsiveAchievements(Transform parent, bool isMentor)
        {
            if (!isMentor)
            {
                AddResponsivePaneTabs(
                    parent,
                    "Achievement",
                    "申請履歴",
                    "実績を申請",
                    !memberAchievementFormVisible,
                    () =>
                    {
                        memberAchievementFormVisible = false;
                        ShowAchievements();
                    },
                    () =>
                    {
                        memberAchievementFormVisible = true;
                        ShowAchievements();
                    });

                if (memberAchievementFormVisible)
                {
                    AddResponsiveAchievementForm(parent);
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(lastAchievementMessage))
            {
                AddFeedbackBanner(parent, lastAchievementMessage, lastAchievementTone, 68);
            }

            var achievements = isMentor
                ? repository.GetPendingAchievements()
                    .Concat(repository.GetRecentAchievements().Where(item => item.Status != AchievementStatus.Pending))
                    .Distinct()
                    .ToList()
                : repository.GetAchievementsForUser(currentUser.Id).ToList();
            var list = CreateDashboardSection(parent, "ResponsiveAchievementList", -1f, theme.LogPanel);
            AddText(list, isMentor ? "実績申請一覧" : "自分の実績申請", 28, FontStyle.Bold, theme.Text, 40);
            if (achievements.Count == 0)
            {
                AddText(list, "表示できる実績申請はありません。", 18, FontStyle.Bold, theme.MutedText, 52, TextAnchor.MiddleCenter);
                return;
            }

            var pageSize = ResponsiveCollectionPageSize(2, 3, 12);
            achievementPage = ClampPage(achievementPage, achievements.Count, pageSize);
            foreach (var achievement in achievements.Skip(achievementPage * pageSize).Take(pageSize))
            {
                AddAchievementSummary(list, achievement, isMentor);
            }

            AddResponsivePagination(list, "Achievement", achievements.Count, pageSize, achievementPage, page =>
            {
                achievementPage = page;
                ShowAchievements();
            });
        }

        private void AddResponsiveAchievementForm(Transform parent)
        {
            var form = CreateDashboardSection(parent, "AchievementForm", -1f, theme.RaidPanel);
            AddBattleSelectorGrid(form, Enum.GetValues(typeof(AchievementType)).Cast<AchievementType>(), selectedAchievementType, value =>
            {
                selectedAchievementType = value;
                ShowAchievements();
            }, AchievementTypeLabel, UsesPortraitLayout() ? 2 : 4);
            achievementTitleInput = ui.CreateInput(form, "AchievementTitleInput", "実績名");
            achievementTitleInput.text = retainedAchievementTitleDraft;
            achievementTitleInput.onValueChanged.AddListener(value => retainedAchievementTitleDraft = value);
            AddLayout(achievementTitleInput.gameObject, -1, 52);
            achievementDescriptionInput = ui.CreateInput(form, "AchievementDescriptionInput", "説明・URL・補足", true);
            achievementDescriptionInput.text = retainedAchievementDescriptionDraft;
            achievementDescriptionInput.onValueChanged.AddListener(value => retainedAchievementDescriptionDraft = value);
            AddLayout(achievementDescriptionInput.gameObject, -1, 52);
            var submit = ui.CreateButton(form, "SubmitAchievement", "この内容で申請する", theme.PrimaryButton, () =>
            {
                if (TrySubmitRemoteAchievement(selectedAchievementType, achievementTitleInput.text, achievementDescriptionInput.text))
                {
                    return;
                }

                try
                {
                    repository.SubmitAchievement(currentUser.Id, selectedAchievementType, achievementTitleInput.text, achievementDescriptionInput.text);
                    ClearRetainedAchievementDrafts();
                    memberAchievementFormVisible = false;
                    SetAchievementFeedback("申請しました。メンター承認後に報酬が反映されます。", FeedbackTone.Success);
                }
                catch (Exception exception)
                {
                    SetAchievementFeedback(exception.Message, FeedbackTone.Danger);
                }

                ShowAchievements();
            });
            AddLayout(submit.gameObject, -1, 54);
        }

        private bool TryStartRemoteSession(string goal)
        {
            if (supabase is not { IsConfigured: true })
            {
                return HandleUnavailableAuthoritativeBackend();
            }

            if (!supabase.HasSession)
            {
                SetSessionFeedback("セッション期限切れです。再ログインしてください。", FeedbackTone.Warning);
                ShowLogin();
                return true;
            }

            if (isNetworkBusy)
            {
                SetSessionFeedback("通信中です。少し待ってから操作してください。", FeedbackTone.Waiting);
                ShowDevLog();
                return true;
            }

            StartCoroutine(StartRemoteSession(goal));
            return true;
        }

        private bool TrySubmitRemoteAchievement(AchievementType type, string title, string description)
        {
            if (supabase is not { IsConfigured: true })
            {
                return HandleUnavailableAuthoritativeBackend();
            }

            if (!supabase.HasSession)
            {
                SetAchievementFeedback("セッション期限切れです。再ログインしてください。", FeedbackTone.Warning);
                ShowLogin();
                return true;
            }

            if (isNetworkBusy)
            {
                SetAchievementFeedback("通信中です。少し待ってから操作してください。", FeedbackTone.Waiting);
                ShowAchievements();
                return true;
            }

            StartCoroutine(SubmitRemoteAchievement(type, title, description));
            return true;
        }

        private bool TryRegisterRemoteProduct(string title, string url, string description)
        {
            if (supabase is not { IsConfigured: true })
            {
                return HandleUnavailableAuthoritativeBackend();
            }

            if (!RuntimeUrlSecurity.TryNormalizeExternalHttpsUrl(url, out var normalizedUrl))
            {
                SetProductFeedback("公開HTTPSのURLを入力してください。", FeedbackTone.Warning);
                ShowProducts();
                return true;
            }

            if (!supabase.HasSession)
            {
                SetProductFeedback("セッション期限切れです。再ログインしてください。", FeedbackTone.Warning);
                ShowLogin();
                return true;
            }

            if (isNetworkBusy)
            {
                SetProductFeedback("通信中です。少し待ってから操作してください。", FeedbackTone.Waiting);
                ShowProducts();
                return true;
            }

            StartCoroutine(RegisterRemoteProduct(title, normalizedUrl, description));
            return true;
        }

        private IEnumerator RegisterRemoteProduct(string title, string url, string description)
        {
            isNetworkBusy = true;
            SupabaseGameApiResponseDto response = null;
            yield return supabase.RegisterProduct(title, url, description, result => response = result);
            isNetworkBusy = false;
            if (ReturnToLoginIfRemoteSessionExpired(response))
            {
                yield break;
            }

            if (response?.Ok == true)
            {
                ApplyRemoteSnapshotPreservingModerationRecords(response);
                ClearRetainedProductDrafts();
                SetProductFeedback("登録しました。全員に公開されます。", FeedbackTone.Success);
            }
            else
            {
                SetProductFeedback(RemoteErrorMessage("登録できませんでした。入力内容と通信状態を確認してください。"), FeedbackTone.Danger);
            }

            ShowProducts();
        }

        private bool TryHideRemoteProduct(string productId, string title)
        {
            if (supabase is not { IsConfigured: true })
            {
                return HandleUnavailableAuthoritativeBackend();
            }

            if (!supabase.HasSession)
            {
                SetProductFeedback("セッション期限切れです。再ログインしてください。", FeedbackTone.Warning);
                ShowLogin();
                return true;
            }

            if (isNetworkBusy)
            {
                SetProductFeedback("通信中です。少し待ってから操作してください。", FeedbackTone.Waiting);
                ShowProducts();
                return true;
            }

            StartCoroutine(HideRemoteProduct(productId, title));
            return true;
        }

        private IEnumerator HideRemoteProduct(string productId, string title)
        {
            isNetworkBusy = true;
            SupabaseGameApiResponseDto response = null;
            yield return supabase.HideProduct(productId, result => response = result);
            isNetworkBusy = false;
            if (ReturnToLoginIfRemoteSessionExpired(response))
            {
                yield break;
            }

            if (response?.Ok == true)
            {
                ApplyRemoteSnapshotPreservingModerationRecords(response);
                SetProductFeedback($"{title} を非表示にしました。", FeedbackTone.Warning);
            }
            else
            {
                SetProductFeedback(RemoteErrorMessage("非表示にできませんでした。権限と通信状態を確認してください。"), FeedbackTone.Danger);
            }

            ShowProducts();
        }

        private IEnumerator SubmitRemoteAchievement(AchievementType type, string title, string description)
        {
            isNetworkBusy = true;
            SupabaseGameApiResponseDto response = null;
            yield return supabase.SubmitAchievement(type, title, description, result => response = result);
            isNetworkBusy = false;
            if (ReturnToLoginIfRemoteSessionExpired(response))
            {
                yield break;
            }

            if (response?.Ok == true)
            {
                ApplyRemoteSnapshotPreservingModerationRecords(response);
                ClearRetainedAchievementDrafts();
                SetAchievementFeedback("申請しました。メンター承認後に報酬が反映されます。", FeedbackTone.Success);
            }
            else
            {
                SetAchievementFeedback(RemoteErrorMessage("申請できませんでした。入力内容と通信状態を確認してください。"), FeedbackTone.Danger);
            }

            ShowAchievements();
        }

        private bool TryReviewRemoteAchievement(string achievementId, bool approve, string title)
        {
            if (supabase is not { IsConfigured: true })
            {
                return HandleUnavailableAuthoritativeBackend();
            }

            if (!supabase.HasSession)
            {
                SetAchievementFeedback("セッション期限切れです。再ログインしてください。", FeedbackTone.Warning);
                ShowLogin();
                return true;
            }

            if (isNetworkBusy)
            {
                SetAchievementFeedback("通信中です。少し待ってから操作してください。", FeedbackTone.Waiting);
                ShowAchievements();
                return true;
            }

            StartCoroutine(ReviewRemoteAchievement(achievementId, approve, title));
            return true;
        }

        private IEnumerator ReviewRemoteAchievement(string achievementId, bool approve, string title)
        {
            isNetworkBusy = true;
            SupabaseGameApiResponseDto response = null;
            if (approve)
            {
                yield return supabase.ApproveAchievement(achievementId, result => response = result);
            }
            else
            {
                yield return supabase.RejectAchievement(achievementId, result => response = result);
            }

            isNetworkBusy = false;
            if (ReturnToLoginIfRemoteSessionExpired(response))
            {
                yield break;
            }

            if (response?.Ok == true)
            {
                ApplyRemoteSnapshotPreservingModerationRecords(response);
                var message = approve
                    ? $"{title} を承認し、報酬を付与しました。"
                    : $"{title} を却下しました。";
                SetAchievementFeedback(message, approve ? FeedbackTone.Success : FeedbackTone.Warning);
            }
            else
            {
                SetAchievementFeedback(RemoteErrorMessage("更新できませんでした。通信状態を確認してください。"), FeedbackTone.Danger);
            }

            ShowAchievements();
        }

        private IEnumerator StartRemoteSession(string goal)
        {
            isNetworkBusy = true;
            SupabaseGameApiResponseDto response = null;
            yield return supabase.StartSession(goal, result => response = result);
            isNetworkBusy = false;
            if (ReturnToLoginIfRemoteSessionExpired(response))
            {
                yield break;
            }

            if (response?.Ok == true)
            {
                ApplyRemoteSnapshot(response);
                retainedGoalDraft = string.Empty;
                SetSessionFeedback("開始しました。今日の目標に集中できます。", FeedbackTone.Success);
            }
            else
            {
                SetSessionFeedback(RemoteErrorMessage("保存できませんでした。通信状態を確認してください。"), FeedbackTone.Danger);
            }

            ShowDevLog();
        }

        private bool TryCompleteRemoteSession(string sessionId, int achievementRate, string reflection, string nextTask)
        {
            if (supabase is not { IsConfigured: true })
            {
                return HandleUnavailableAuthoritativeBackend();
            }

            if (!supabase.HasSession)
            {
                SetSessionFeedback("セッション期限切れです。再ログインしてください。", FeedbackTone.Warning);
                ShowLogin();
                return true;
            }

            if (isNetworkBusy)
            {
                SetSessionFeedback("通信中です。AI評価または保存処理の完了を待ってください。", FeedbackTone.Waiting);
                ShowDevLog();
                return true;
            }

            StartCoroutine(CompleteRemoteSession(sessionId, achievementRate, reflection, nextTask));
            return true;
        }

        private IEnumerator CompleteRemoteSession(string sessionId, int achievementRate, string reflection, string nextTask)
        {
            isNetworkBusy = true;
            SupabaseGameApiResponseDto response = null;
            yield return supabase.CompleteSession(sessionId, achievementRate, reflection, nextTask, result => response = result);
            isNetworkBusy = false;
            if (ReturnToLoginIfRemoteSessionExpired(response))
            {
                yield break;
            }

            var shouldPollAiEvaluation = false;
            if (response?.Ok == true)
            {
                ApplyRemoteSnapshot(response);
                ClearRetainedDevLogDrafts();
                var saved = repository.GetSessionsForUser(currentUser.Id).FirstOrDefault(item => item.Id == sessionId);
                if (saved?.Evaluation != null)
                {
                    SetSessionFeedback($"AI評価 {RankLabel(saved.Evaluation.Rank)} / 仮EXP +{saved.PreviewExp} / {StatusLabel(saved.Status)}", FeedbackTone.Success);
                }
                else
                {
                    SetSessionFeedback("AI評価待ちです。完了後に承認待ちへ反映されます。", FeedbackTone.Waiting);
                    shouldPollAiEvaluation = ShouldPollAiEvaluationSnapshot(saved);
                }
            }
            else
            {
                SetSessionFeedback(RemoteErrorMessage("保存できませんでした。通信状態を確認してください。"), FeedbackTone.Danger);
            }

            ShowDevLog();
            if (shouldPollAiEvaluation)
            {
                BeginAiEvaluationSnapshotPolling(sessionId);
            }
        }

        private void BeginAiEvaluationSnapshotPolling(string sessionId)
        {
            CancelAiEvaluationSnapshotPolling();
            if (!Application.isPlaying || string.IsNullOrWhiteSpace(sessionId) || currentUser == null)
            {
                return;
            }

            var generation = aiEvaluationSnapshotPollGeneration;
            aiEvaluationSnapshotPollingRoutine = StartCoroutine(PollAiEvaluationSnapshot(
                sessionId,
                currentUser.Id,
                generation));
        }

        private void CancelAiEvaluationSnapshotPolling()
        {
            aiEvaluationSnapshotPollGeneration++;
            // Let an in-flight request reach both busy-state cleanup paths. The existing
            // generation checks prevent its response from updating a departed screen.
            aiEvaluationSnapshotPollingRoutine = null;
        }

        private IEnumerator PollAiEvaluationSnapshot(string sessionId, string userId, int generation)
        {
            for (var attempt = 0; attempt < AiEvaluationSnapshotPollAttemptCount; attempt++)
            {
                yield return new WaitForSecondsRealtime(AiEvaluationSnapshotPollDelaySeconds(attempt));
                if (!CanContinueAiEvaluationSnapshotPolling(sessionId, userId, generation))
                {
                    ReleaseAiEvaluationSnapshotPolling(generation);
                    yield break;
                }

                if (isNetworkBusy)
                {
                    continue;
                }

                isNetworkBusy = true;
                SupabaseGameApiResponseDto response = null;
                yield return supabase.GetSnapshot(result => response = result);
                isNetworkBusy = false;

                if (ReturnToLoginIfRemoteSessionExpired(response))
                {
                    ReleaseAiEvaluationSnapshotPolling(generation);
                    yield break;
                }

                if (!CanContinueAiEvaluationSnapshotPolling(sessionId, userId, generation))
                {
                    ReleaseAiEvaluationSnapshotPolling(generation);
                    yield break;
                }

                if (response?.Ok == true)
                {
                    ApplyRemoteSnapshot(response);
                    var saved = repository.GetSessionsForUser(userId).FirstOrDefault(item => item.Id == sessionId);
                    if (!ShouldPollAiEvaluationSnapshot(saved))
                    {
                        if (saved?.Evaluation != null)
                        {
                            SetSessionFeedback($"AI評価 {RankLabel(saved.Evaluation.Rank)} / 仮EXP +{saved.PreviewExp} / {StatusLabel(saved.Status)}", FeedbackTone.Success);
                        }
                        else
                        {
                            SetSessionFeedback("AI評価の処理状態が更新されました。最新の記録を確認してください。", FeedbackTone.Success);
                        }

                        aiEvaluationSnapshotPollingRoutine = null;
                        ShowDevLog();
                        yield break;
                    }
                }
            }

            if (CanContinueAiEvaluationSnapshotPolling(sessionId, userId, generation))
            {
                SetSessionFeedback("AI評価はサーバーで処理中です。少し待ってから「AI評価を再確認」を押してください。", FeedbackTone.Waiting);
                aiEvaluationSnapshotPollingRoutine = null;
                ShowDevLog();
            }
        }

        private void ReleaseAiEvaluationSnapshotPolling(int generation)
        {
            if (generation == aiEvaluationSnapshotPollGeneration)
            {
                aiEvaluationSnapshotPollingRoutine = null;
            }
        }

        private bool CanContinueAiEvaluationSnapshotPolling(string sessionId, string userId, int generation)
        {
            if (generation != aiEvaluationSnapshotPollGeneration ||
                activeProductionScene != RasshiineProductionScene.DevLog ||
                currentUser?.Id != userId ||
                supabase is not { IsConfigured: true, HasSession: true })
            {
                return false;
            }

            var session = repository?.GetSessionsForUser(userId).FirstOrDefault(item => item.Id == sessionId);
            return ShouldPollAiEvaluationSnapshot(session);
        }

        private static bool ShouldPollAiEvaluationSnapshot(DevSession session)
        {
            return session is { Status: DevSessionStatus.AiPending, Evaluation: null };
        }

        private static float AiEvaluationSnapshotPollDelaySeconds(int attempt)
        {
            return Mathf.Pow(2f, Mathf.Clamp(attempt, 0, AiEvaluationSnapshotPollAttemptCount - 1) + 1);
        }

        private void ShowBattle()
        {
            MarkScene(RasshiineProductionScene.Battle);
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Battle);
            ClearRoot();
            UnityEngine.Events.UnityAction backAction = ShowMemberHome;
            if (currentUser.Role == UserRole.Mentor)
            {
                backAction = ShowMentorDashboard;
            }
            var battle = repository.ActiveBattle;
            battleController?.LoadBattle(battle);
            SyncBattleControlledParticipant();
            // The approved bright battle reference has one quiet, centred scene title.
            // Turn/state information already lives in the action band (or the scheduled /
            // result card), so repeating it below the title only competes with the boss HUD.
            AddBattleHeader("ボス戦", backAction);
            AddBattleBossHpHud(battle);
            AddBattleTurnOrderStrip(battle);
            AddBattleStateHud(battle);

            if (battle.Status == BattleStatus.Scheduled)
            {
                AddScheduledBattleMenu(battle);
                return;
            }

            if (battle.IsCompleted)
            {
                AddBattleResultMenu();
                return;
            }

            AddBattleCommandMenu();
        }

        private void AddBattleStateHud(BossBattleState battle)
        {
            var partyStatus = repository.GetBattlePartyStatus();
            if (battle.IsCompleted && battleCommandDeckExpanded)
            {
                // The focused result sheet owns the lower half of the screen. Keep
                // the summary HUD out of that state so the result never becomes a
                // stack of two competing panels.
                return;
            }
            // During an active turn the state summary and command entry share one deliberate
            // bottom action band. Keeping a second compact panel here made the screen read as
            // two unrelated HUDs and reduced the clear arena view at WebGL sizes.
            if (!battle.IsActive && !battleStateExpanded && !(battle.IsCompleted && battleCommandDeckExpanded))
            {
                var portrait = UsesPortraitLoginLayout();
                var compactPanel = ui.CreatePanel(
                    root,
                    "BattleCompactStateHud",
                    theme.RaidPanel,
                    portrait ? new Vector2(0.04f, 0.205f) : new Vector2(0.045f, 0.04f),
                    portrait ? new Vector2(0.96f, 0.315f) : new Vector2(0.44f, 0.205f),
                    Vector2.zero,
                    Vector2.zero);
                AddBattleHudBacking(compactPanel);
                AddHorizontal(compactPanel, 8, 8);
                AddBattleHudMetric(compactPanel, "STATE", BattleStatusLabel(battle.Status), theme.Cyan);
                AddBattleHudMetric(compactPanel, "TEAM", $"{partyStatus.ParticipantCount}人", theme.Text);
                AddBattleHudMetric(compactPanel, "DMG", $"{battle.TotalDamage:N0}", theme.Cyan);
            }

            if (!battleStateExpanded)
            {
                return;
            }

            var statePortrait = UsesPortraitLoginLayout();
            var stateCompactLandscape = !statePortrait && ResolveLoginViewportSize().y < 520f;
            var statePanel = ui.CreatePanel(
                root,
                "BattleStateHud",
                theme.RaidPanel,
                statePortrait
                    ? new Vector2(0.04f, 0.30f)
                    : stateCompactLandscape ? new Vector2(0.04f, 0.22f) : new Vector2(0.045f, 0.25f),
                statePortrait
                    ? new Vector2(0.96f, 0.70f)
                    : stateCompactLandscape ? new Vector2(0.56f, 0.78f) : new Vector2(0.46f, 0.55f),
                Vector2.zero,
                Vector2.zero);
            AddBattleHudBacking(statePanel);
            AddVertical(statePanel, 12, 5);
            AddText(statePanel, battle.IsCompleted ? "RESULT" : battle.IsActive ? "LIVE RAID" : "RAID STANDBY", 14, FontStyle.Bold, theme.Cyan, 20, TextAnchor.MiddleCenter);
            var bossMetrics = CreateHudRow(statePanel, "BossMetrics", ResolveMinimumUiLength(58f, 50f));
            AddBattleHudMetric(bossMetrics, battle.IsActive ? "TURN" : "STATUS", battle.IsActive ? $"{Mathf.Min(battle.TurnNumber, battle.TurnCount)} / {battle.TurnCount}" : BattleStatusLabel(battle.Status), theme.Cyan);
            AddBattleHudMetric(bossMetrics, "参加", $"{battle.Participants.Count}人", theme.Text);
            AddBattleHudMetric(bossMetrics, "TEAM DMG", $"{battle.TotalDamage:N0}", theme.Cyan);
            var partyMetrics = CreateHudRow(statePanel, "PartyMetrics", ResolveMinimumUiLength(58f, 50f));
            AddBattleHudMetric(partyMetrics, "PARTY HP", $"{partyStatus.CurrentHp:N0} / {partyStatus.MaxHp:N0}", theme.Cyan);
            AddBattleHudMetric(partyMetrics, "PARTY MP", $"{partyStatus.CurrentMp:N0} / {partyStatus.MaxMp:N0}", theme.Cyan);
            AddBattleHudMetric(partyMetrics, "ALIVE", $"{partyStatus.AliveCount} / {partyStatus.ParticipantCount}", theme.Text);
            if (battle.IsCompleted)
            {
                var summary = repository.GetBattleResultSummary();
                AddText(statePanel, summary.ResultMessage, 16, FontStyle.Bold, summary.IsVictory ? theme.Cyan : theme.Text, 28, TextAnchor.MiddleCenter);
            }
            else
            {
                AddBattleActivityStrip(statePanel, battle);
            }

            AddBattleSignalLine(statePanel, lastBattleMessage, lastBattleTone);
            var closeRow = CreateHudRow(statePanel, "CloseBattleStateRow", ResolveMinimumUiLength(54f, 46f));
            if (battle.IsCompleted)
            {
                AddBattleButtonCell(closeRow, "OpenBattleResult", "結果詳細", theme.PrimaryButton, () =>
                {
                    battleStateExpanded = false;
                    battleCommandDeckExpanded = true;
                    ShowBattle();
                }, theme.Text, 12);
                AddBattleButtonCell(closeRow, "CloseBattleState", "閉じる", theme.SecondaryButton, () =>
                {
                    battleStateExpanded = false;
                    ShowBattle();
                }, theme.Text, 12);
            }
            else
            {
                AddBattleButtonCell(closeRow, "CloseBattleState", "閉じる", theme.SecondaryButton, () =>
                {
                    battleStateExpanded = false;
                    ShowBattle();
                }, theme.Text, 12);
            }
        }

        private void AddScheduledBattleMenu(BossBattleState battle)
        {
            var portrait = UsesPortraitLoginLayout();
            var waitingPanel = ui.CreatePanel(
                root,
                "BattleCompactCommandHud",
                theme.RaidPanel,
                portrait ? new Vector2(0.04f, 0.04f) : new Vector2(0.58f, 0.04f),
                portrait ? new Vector2(0.96f, 0.19f) : new Vector2(0.955f, 0.205f),
                Vector2.zero,
                Vector2.zero);
            AddBattleHudBacking(waitingPanel);
            AddHorizontal(waitingPanel, 8, 8);
            var summary = new GameObject("BattleWaitSummary", typeof(RectTransform), typeof(VerticalLayoutGroup));
            summary.transform.SetParent(waitingPanel, false);
            AddLayout(summary, 1, -1);
            AddVertical(summary.GetComponent<RectTransform>(), 0, 0, TextAnchor.MiddleLeft);
            AddText(summary.transform, "RAID", 12, FontStyle.Bold, theme.Cyan, 18);
            AddText(summary.transform, $"{BattleStatusLabel(battle.Status)} / {battle.Participants.Count}人", 18, FontStyle.Bold, theme.Text, 29);

            if (currentUser.Role == UserRole.Mentor)
            {
                AddBattleButtonCell(waitingPanel, "StartBattle", "ゲーム開始", theme.PrimaryButton, () =>
                {
                    battleStateExpanded = false;
                    battleCommandDeckExpanded = false;
                    if (TryStartRemoteBattle(ShowBattle))
                    {
                        return;
                    }

                    repository.StartBattle(currentUser.Id);
                    battleController.LoadBattle(repository.ActiveBattle);
                    battleController.SetControlledParticipant(null);
                    SetBattleFeedback("ボス戦開始", FeedbackTone.Battle);
                    ShowBattle();
                }, theme.Text, 13, 0.95f);
            }
            else
            {
                AddBattleButtonCell(waitingPanel, "BattleRefresh", "更新", theme.SecondaryButton, () =>
                {
                    if (!TryRefreshRemoteSnapshot(ShowBattle))
                    {
                        ShowBattle();
                    }
                }, theme.Text, 13, 0.65f);
                AddBattleButtonCell(waitingPanel, "BattleDevLog", "ログ", theme.PrimaryButton, ShowDevLog, theme.Text, 13, 0.65f);
            }
        }

        private void AddBattleResultMenu()
        {
            if (battleStateExpanded && !battleCommandDeckExpanded)
            {
                // The expanded summary already contains a direct Result details
                // action. A second floating result rail made the completed state
                // look unfinished and forced the eye to choose between two CTAs.
                return;
            }

            if (!battleCommandDeckExpanded)
            {
                var portrait = UsesPortraitLoginLayout();
                var collapsed = ui.CreatePanel(
                    root,
                    "BattleCompactCommandHud",
                    theme.RaidPanel,
                    portrait ? new Vector2(0.04f, 0.04f) : new Vector2(0.58f, 0.04f),
                    portrait ? new Vector2(0.96f, 0.19f) : new Vector2(0.955f, 0.205f),
                    Vector2.zero,
                    Vector2.zero);
                AddBattleHudBacking(collapsed);
                AddHorizontal(collapsed, 8, 8);
                var summaryBox = new GameObject("BattleResultSummary", typeof(RectTransform), typeof(VerticalLayoutGroup));
                summaryBox.transform.SetParent(collapsed, false);
                AddLayout(summaryBox, 1, -1);
                AddVertical(summaryBox.GetComponent<RectTransform>(), 0, 0, TextAnchor.MiddleLeft);
                AddText(summaryBox.transform, "RESULT", 12, FontStyle.Bold, theme.Cyan, 18);
                AddText(summaryBox.transform, "結果を確認", 18, FontStyle.Bold, theme.Text, 29);
                AddBattleButtonCell(collapsed, "OpenBattleResult", "結果", theme.PrimaryButton, () =>
                {
                    battleStateExpanded = false;
                    battleCommandDeckExpanded = true;
                    ShowBattle();
                }, theme.Text, 13, 0.75f);
                return;
            }

            var resultPortrait = UsesPortraitLoginLayout();
            var resultPanel = ui.CreatePanel(
                root,
                "BattleResultHud",
                theme.RaidPanel,
                resultPortrait ? new Vector2(0.04f, 0.02f) : new Vector2(0.60f, 0.06f),
                resultPortrait ? new Vector2(0.96f, 0.72f) : new Vector2(0.965f, 0.70f),
                Vector2.zero,
                Vector2.zero);
            AddBattleHudBacking(resultPanel);
            AddVertical(resultPanel, 12, 5);
            var resultHeaderHeight = ResolveMinimumUiLength(52f, 44f);
            var header = CreateHudRow(resultPanel, "BattleResultHeader", resultHeaderHeight);
            // CreateHudRow normally permits non-interactive metric rows to shrink.
            // This row owns the close action, so preserve the physical 44 px floor.
            header.GetComponent<LayoutElement>().minHeight = resultHeaderHeight;
            var title = ui.CreateText(header, "BattleResultTitle", "RESULT", ResolveUiFontSize(16), FontStyle.Bold, theme.Cyan, TextAnchor.MiddleLeft);
            AddLayout(title.gameObject, 1, -1);
            AddBattleButtonCell(header, "CloseBattleResult", "閉じる", theme.SecondaryButton, () =>
            {
                battleCommandDeckExpanded = false;
                ShowBattle();
            }, theme.Text, 12, 0.65f);

            // Result details can exceed the compact-landscape height once personal and
            // top-contributor rows are present. Keep the close action fixed at a real
            // touch-target size and scroll only the detail body instead of allowing the
            // layout group to crush every action into unusable slivers.
            var resultScrollHost = new GameObject(
                "BattleResultScroll",
                typeof(RectTransform),
                typeof(LayoutElement));
            resultScrollHost.transform.SetParent(resultPanel, false);
            var resultScrollLayout = resultScrollHost.GetComponent<LayoutElement>();
            resultScrollLayout.flexibleWidth = 1f;
            resultScrollLayout.flexibleHeight = 1f;
            resultScrollLayout.minHeight = 0f;
            resultScrollLayout.preferredHeight = 0f;
            var resultContent = CreateEmbeddedScrollContent(
                resultScrollHost.GetComponent<RectTransform>(),
                "BattleResultDetails",
                2,
                5);
            AddBattleResultPanel(resultContent, repository.GetBattleResultSummary(), currentUser.Id);
            if (currentUser.Role == UserRole.Mentor)
            {
                var nextButton = ui.CreateButton(resultPanel, "ResetBattle", "次週の準備", theme.SecondaryButton, () =>
                {
                    battleStateExpanded = false;
                    battleCommandDeckExpanded = false;
                    if (TryResetRemoteBattle(ShowBattle))
                    {
                        return;
                    }

                    repository.ResetBattle(currentUser.Id);
                    battleController.LoadBattle(repository.ActiveBattle);
                    SetBattleFeedback("次週の準備完了", FeedbackTone.Success);
                    ShowBattle();
                });
                AddLayout(nextButton.gameObject, -1, ResolveMinimumUiLength(52f, 44f));
            }
        }

        private void AddBattleCommandMenu()
        {
            var participant = repository.GetParticipant(currentUser.Id);
            if (participant == null)
            {
                AddBattleObserverMenu();
                return;
            }

            var availableWeapons = repository.GetAvailableBattleWeapons(participant.UserId).ToList();
            if (availableWeapons.Count == 0)
            {
                availableWeapons.Add(WeaponKind.Blade);
            }

            if (!availableWeapons.Contains(selectedWeapon))
            {
                selectedWeapon = availableWeapons[0];
            }

            if (!battleCommandDeckExpanded)
            {
                AddBattleCompactCommandHud(participant);
                return;
            }

            var portrait = UsesPortraitLoginLayout();
            var compactLandscape = !portrait && ResolveLoginViewportSize().y < 520f;
            var actionPanel = ui.CreatePanel(
                root,
                "BattleCommandHud",
                theme.RaidPanel,
                portrait ? new Vector2(0.04f, 0.02f) : new Vector2(0.06f, 0.025f),
                portrait
                    ? new Vector2(0.96f, 0.68f)
                    : compactLandscape ? new Vector2(0.94f, 0.72f) : new Vector2(0.94f, 0.50f),
                Vector2.zero,
                Vector2.zero);
            AddBattleHudBacking(actionPanel);
            var header = CreateBattleAnchoredRow(actionPanel, "CommandDeckHeader", new Vector2(0.03f, 0.795f), new Vector2(0.97f, 0.97f), 8);
            var headerInfo = new GameObject("CommandDeckHeaderInfo", typeof(RectTransform), typeof(VerticalLayoutGroup));
            headerInfo.transform.SetParent(header, false);
            AddLayout(headerInfo, 1f, -1f);
            AddVertical(headerInfo.GetComponent<RectTransform>(), 0, 1, TextAnchor.MiddleLeft);
            var title = ui.CreateText(headerInfo.transform, "CommandDeckTitle", $"COMMAND / {Shorten(participant.Nickname, 9)}", ResolveUiFontSize(16), FontStyle.Bold, theme.Cyan, TextAnchor.MiddleLeft);
            ConfigureSingleLineLabel(title, title.fontSize);
            AddLayout(title.gameObject, -1, ResolveMinimumUiLength(24f, 18f));
            var vitals = ui.CreateText(headerInfo.transform, "CommandDeckVitals", $"HP {participant.CurrentHp}/{participant.Stats.Hp}   MP {participant.CurrentMp}/{participant.Stats.Mp}", ResolveUiFontSize(13), FontStyle.Bold, theme.Text, TextAnchor.MiddleLeft);
            ConfigureSingleLineLabel(vitals, vitals.fontSize);
            AddLayout(vitals.gameObject, -1, ResolveMinimumUiLength(20f, 16f));
            AddBattleButtonCell(header, "CloseCommandDeck", "閉じる", theme.SecondaryButton, () =>
            {
                battleCommandDeckExpanded = false;
                ShowBattle();
            }, theme.Text, 12, 0.50f);

            var selectionColumn = CreateBattleAnchoredColumn(actionPanel, "BattleCommandSelection", new Vector2(0.03f, 0.05f), new Vector2(0.52f, 0.77f), 5);
            AddBattleSectionLabel(selectionColumn, "ROLE");
            AddBattleSelectorGrid(selectionColumn, Enum.GetValues(typeof(BattleRole)).Cast<BattleRole>(), selectedRole, value =>
            {
                selectedRole = value;
                ShowBattle();
            }, RoleShortLabel, portrait ? 2 : 4);

            AddBattleSectionLabel(selectionColumn, "WEAPON");
            AddBattleSelectorGrid(selectionColumn, availableWeapons, selectedWeapon, value =>
            {
                selectedWeapon = value;
                ShowBattle();
            }, WeaponLabel, portrait ? 2 : 4);

            var actionColumn = CreateBattleAnchoredColumn(actionPanel, "BattleCommandActions", new Vector2(0.54f, 0.05f), new Vector2(0.97f, 0.77f), 5);
            AddBattleSectionLabel(actionColumn, "ACTION");
            AddActionGrid(actionColumn, repository.GetBattleActionOptions(currentUser.Id, selectedWeapon).ToList(), 2);
        }

        private void AddBattleCompactCommandHud(BattleParticipant participant)
        {
            var battle = repository.ActiveBattle;
            var portrait = UsesPortraitLayout();
            var compactLandscape = UsesCompactLandscapeLayout();
            var viewport = ResolveLoginViewportSize();
            var desktopBattleHud = !portrait && !compactLandscape && viewport.x >= 1000f && viewport.y >= 600f;
            var desktopBandMaxY = battleStateExpanded ? 0.235f : 0.305f;
            var commandPanelObject = new GameObject("BattleActionBand", typeof(RectTransform));
            commandPanelObject.transform.SetParent(root, false);
            var commandPanel = commandPanelObject.GetComponent<RectTransform>();
            ui.SetRect(
                commandPanel,
                portrait ? new Vector2(0.025f, 0.018f) : desktopBattleHud ? new Vector2(0.025f, 0.018f) : new Vector2(0.035f, 0.025f),
                portrait
                    ? new Vector2(0.975f, 0.285f)
                    : desktopBattleHud ? new Vector2(0.982f, desktopBandMaxY) : new Vector2(0.965f, 0.205f),
                Vector2.zero,
                Vector2.zero);

            var partyHost = new GameObject("BattlePartyCards", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            partyHost.transform.SetParent(commandPanel, false);
            SetRelativeRect(
                partyHost.GetComponent<RectTransform>(),
                portrait ? new Vector2(0.01f, 0.56f) : desktopBattleHud ? new Vector2(0.00f, 0.00f) : new Vector2(0.01f, 0.04f),
                portrait
                    ? new Vector2(0.99f, 0.99f)
                    : desktopBattleHud ? new Vector2(0.496f, 0.59f) : new Vector2(0.53f, 0.96f));
            var partyLayout = partyHost.GetComponent<HorizontalLayoutGroup>();
            partyLayout.spacing = portrait || compactLandscape ? 5f : 4f;
            partyLayout.padding = new RectOffset(0, 0, 0, 0);
            partyLayout.childControlWidth = true;
            partyLayout.childControlHeight = true;
            partyLayout.childForceExpandWidth = true;
            partyLayout.childForceExpandHeight = true;
            var visibleParticipants = battle.Participants
                .OrderBy(item => item.UserId == participant.UserId ? 0 : 1)
                .ThenBy(item => item.Nickname, StringComparer.Ordinal)
                .Take(3)
                .ToList();
            foreach (var partyMember in visibleParticipants)
            {
                AddBrightBattlePartyCard(partyHost.transform, partyMember, !desktopBattleHud);
            }

            var commandHost = new GameObject("BattleQuickCommands", typeof(RectTransform));
            commandHost.transform.SetParent(commandPanel, false);
            SetRelativeRect(
                commandHost.GetComponent<RectTransform>(),
                portrait ? new Vector2(0.01f, 0.01f) : desktopBattleHud ? new Vector2(0.650f, 0.00f) : new Vector2(0.55f, 0.01f),
                portrait ? new Vector2(0.99f, 0.55f) : new Vector2(0.995f, 0.99f));

            var turnBadge = new GameObject("BattleTurnBadge", typeof(RectTransform), typeof(Image), typeof(Shadow));
            turnBadge.transform.SetParent(commandHost.transform, false);
            var turnBadgeImage = turnBadge.GetComponent<Image>();
            turnBadgeImage.sprite = theme.LoginCtaButton ?? theme.PrimaryButton;
            turnBadgeImage.type = turnBadgeImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            turnBadgeImage.color = Color.white;
            turnBadgeImage.raycastTarget = false;
            var turnBadgeShadow = turnBadge.GetComponent<Shadow>();
            turnBadgeShadow.effectColor = new Color(0.04f, 0.09f, 0.15f, 0.26f);
            turnBadgeShadow.effectDistance = new Vector2(0f, -3f);
            SetRelativeRect(
                turnBadge.GetComponent<RectTransform>(),
                desktopBattleHud ? new Vector2(0.04f, 0.81f) : new Vector2(0.01f, 0.58f),
                desktopBattleHud ? new Vector2(0.31f, 0.97f) : new Vector2(0.28f, 0.96f));
            var turnValue = AddAnchoredText(
                turnBadge.GetComponent<RectTransform>(),
                "BattleBandMetric_TURN_Value",
                $"TURN {Mathf.Min(battle.TurnNumber, battle.TurnCount)} / {battle.TurnCount}",
                16,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.08f, 0.08f),
                new Vector2(0.92f, 0.92f),
                TextAnchor.MiddleCenter);
            PreserveFullSingleLineLabel(turnValue, 14);
            if (!desktopBattleHud)
            {
                var commandSummary = AddAnchoredText(
                    commandHost.GetComponent<RectTransform>(),
                    "BattleCommandSummary",
                    BuildCompactCommandLabel(),
                    15,
                    FontStyle.Bold,
                    new Color(0.11f, 0.17f, 0.25f, 1f),
                    desktopBattleHud ? new Vector2(0.43f, 0.90f) : new Vector2(0.01f, 0.08f),
                    desktopBattleHud ? new Vector2(0.98f, 0.995f) : new Vector2(0.28f, 0.54f),
                    TextAnchor.MiddleCenter);
                PreserveFullSingleLineLabel(commandSummary, 13);
                AddBattleTextShadow(commandSummary, new Color(1f, 1f, 1f, 0.72f), new Vector2(1f, -1f));
            }

            var options = repository.GetBattleActionOptions(currentUser.Id, selectedWeapon).ToList();
            var normal = options.FirstOrDefault(option => option.ActionType == BattleActionType.Normal);
            var guard = options.FirstOrDefault(option => option.ActionType == BattleActionType.Guard);
            AddBrightBattleActionButton(
                commandHost.transform,
                "QuickAttack",
                "攻撃",
                theme.BattleIcon,
                desktopBattleHud ? new Vector2(0.00f, 0.00f) : new Vector2(0.31f, 0.02f),
                desktopBattleHud ? new Vector2(0.30f, 0.80f) : new Vector2(0.51f, 0.98f),
                Color.white,
                new Color(0.96f, 0.34f, 0.20f, 1f),
                new Color(0.12f, 0.17f, 0.25f, 1f),
                () => ExecuteBattleAction(BattleActionType.Normal),
                normal?.IsAvailable != false);
            AddBrightBattleActionButton(
                commandHost.transform,
                "OpenCommandDeck",
                "スキル",
                theme.AchievementIcon ?? theme.GoalIcon,
                desktopBattleHud ? new Vector2(0.315f, -0.004f) : new Vector2(0.53f, 0.00f),
                desktopBattleHud ? new Vector2(0.705f, 0.98f) : new Vector2(0.75f, 1.00f),
                new Color(0.53f, 0.29f, 0.91f, 1f),
                Color.white,
                Color.white,
                () =>
                {
                    battleStateExpanded = false;
                    battleCommandDeckExpanded = true;
                    ShowBattle();
                },
                true);
            AddBrightBattleActionButton(
                commandHost.transform,
                "QuickGuard",
                "防御",
                theme.BattleShieldIcon ?? theme.CheckIcon,
                desktopBattleHud ? new Vector2(0.72f, 0.00f) : new Vector2(0.77f, 0.02f),
                desktopBattleHud ? new Vector2(1.00f, 0.80f) : new Vector2(0.99f, 0.98f),
                Color.white,
                new Color(0.12f, 0.56f, 0.92f, 1f),
                new Color(0.12f, 0.17f, 0.25f, 1f),
                () => ExecuteBattleAction(BattleActionType.Guard),
                guard?.IsAvailable != false);
        }

        private void AddBrightBattlePartyCard(Transform parent, BattleParticipant participant, bool compact)
        {
            var cardObject = new GameObject($"BattlePartyCard_{participant.UserId}", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(Shadow));
            cardObject.transform.SetParent(parent, false);
            var cardImage = cardObject.GetComponent<Image>();
            cardImage.sprite = theme.StatCard ?? theme.SecondaryButton;
            cardImage.type = cardImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            cardImage.color = Color.white;
            cardImage.raycastTarget = false;
            var layout = cardObject.GetComponent<LayoutElement>();
            layout.flexibleWidth = 1f;
            layout.flexibleHeight = 1f;
            layout.minWidth = 0f;
            layout.minHeight = 0f;
            var shadow = cardObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0.04f, 0.09f, 0.15f, 0.24f);
            shadow.effectDistance = new Vector2(0f, -3f);

            var roleColor = participant.Role switch
            {
                BattleRole.Attacker => new Color(0.96f, 0.34f, 0.20f, 1f),
                BattleRole.Healer => new Color(0.42f, 0.75f, 0.20f, 1f),
                BattleRole.Defender => new Color(0.12f, 0.56f, 0.92f, 1f),
                _ => new Color(0.53f, 0.29f, 0.91f, 1f)
            };
            var roleDot = new GameObject("Role", typeof(RectTransform), typeof(Image));
            roleDot.transform.SetParent(cardObject.transform, false);
            var roleDotImage = roleDot.GetComponent<Image>();
            roleDotImage.sprite = theme.BattleCircleButton;
            roleDotImage.color = roleColor;
            roleDotImage.preserveAspect = true;
            roleDotImage.raycastTarget = false;
            SetRelativeRect(
                roleDotImage.rectTransform,
                compact ? new Vector2(0.045f, 0.61f) : new Vector2(0.045f, 0.58f),
                compact ? new Vector2(0.18f, 0.91f) : new Vector2(0.12f, 0.84f));

            var nickname = AddAnchoredText(
                cardObject.GetComponent<RectTransform>(),
                "Nickname",
                Shorten(participant.Nickname, compact ? 7 : 10),
                compact ? 12 : 21,
                FontStyle.Bold,
                new Color(0.12f, 0.17f, 0.25f, 1f),
                new Vector2(0.18f, 0.62f),
                new Vector2(0.94f, 0.92f),
                TextAnchor.MiddleLeft);
            ConfigureSingleLineLabel(nickname, compact ? 11 : 19);
            // The approved battle reference treats these as compact combat gauges,
            // not profile cards. Hide the nickname on wide screens so the two vital
            // rows and their large values own the full white card.
            nickname.gameObject.SetActive(compact);
            AddBattlePartyVitalBar(cardObject.transform, "HP", participant.CurrentHp, participant.Stats?.Hp ?? 0, new Color(0.39f, 0.74f, 0.20f, 1f), compact ? 0.37f : 0.49f, compact ? 0.59f : 0.78f, compact);
            AddBattlePartyVitalBar(cardObject.transform, "MP", participant.CurrentMp, participant.Stats?.Mp ?? 0, new Color(0.16f, 0.60f, 0.91f, 1f), compact ? 0.10f : 0.15f, compact ? 0.32f : 0.44f, compact);
        }

        private void AddBattlePartyVitalBar(
            Transform card,
            string label,
            int current,
            int maximum,
            Color fillColor,
            float minY,
            float maxY,
            bool compact)
        {
            var cardRect = card as RectTransform;
            // Build the track first and put both labels above it in draw order. In the
            // WebGL reference-size capture the track used to win the batching order on
            // some GPUs, making the right-aligned HP/MP numbers appear to be missing.
            var track = new GameObject($"{label}Track", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(card, false);
            var trackImage = track.GetComponent<Image>();
            trackImage.color = new Color(0.18f, 0.22f, 0.28f, 0.18f);
            trackImage.raycastTarget = false;
            var trackPadding = compact ? 0.055f : 0.085f;
            SetRelativeRect(trackImage.rectTransform, new Vector2(0.29f, minY + trackPadding), new Vector2(0.70f, maxY - trackPadding));
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(track.transform, false);
            var fillImage = fill.GetComponent<Image>();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.fillAmount = maximum <= 0 ? 0f : Mathf.Clamp01(current / (float)maximum);
            fillImage.color = fillColor;
            fillImage.raycastTarget = false;
            ui.Stretch(fillImage.rectTransform, 0f, 0f, 0f, 0f);

            var labelText = AddAnchoredText(
                cardRect,
                $"{label}Label",
                label,
                compact ? 11 : 20,
                FontStyle.Bold,
                new Color(0.16f, 0.20f, 0.27f, 1f),
                new Vector2(!compact && label == "HP" ? 0.13f : 0.06f, minY),
                new Vector2(0.28f, maxY),
                TextAnchor.MiddleLeft);
            PreserveFullSingleLineLabel(labelText, compact ? 10 : 18);
            var value = AddAnchoredText(
                cardRect,
                $"{label}Value",
                $"{Mathf.Max(0, current):N0}",
                compact ? 12 : 25,
                FontStyle.Bold,
                new Color(0.10f, 0.15f, 0.23f, 1f),
                new Vector2(0.715f, minY),
                new Vector2(0.965f, maxY),
                TextAnchor.MiddleRight);
            PreserveFullSingleLineLabel(value, compact ? 11 : 22);
            AddBattleTextShadow(value, new Color(1f, 1f, 1f, 0.92f), new Vector2(1f, -1f));
        }

        private Button AddBrightBattleActionButton(
            Transform parent,
            string name,
            string label,
            Sprite icon,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color frameColor,
            Color iconColor,
            Color labelColor,
            UnityEngine.Events.UnityAction onClick,
            bool interactable)
        {
            var viewport = ResolveLoginViewportSize();
            var constrainedBattleHud = UsesPortraitLayout() ||
                                       UsesCompactLandscapeLayout() ||
                                       viewport.x < 1000f ||
                                       viewport.y < 600f;
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            SetRelativeRect(buttonObject.GetComponent<RectTransform>(), anchorMin, anchorMax);
            var hitSurface = buttonObject.GetComponent<Image>();
            hitSurface.color = Color.clear;
            hitSurface.raycastTarget = true;

            // Keep non-overlapping, reliable hit targets while allowing the authored
            // hex artwork to occupy the same bold visual footprint as the reference.
            // ResolveBattleCommandHexSprite crops only the transparent source margins;
            // the authored hex itself remains undistorted and uses the original texture.
            var frameObject = new GameObject($"{name}_Frame", typeof(RectTransform), typeof(Image), typeof(Shadow));
            frameObject.transform.SetParent(buttonObject.transform, false);
            var frame = frameObject.GetComponent<Image>();
            frame.sprite = ResolveBattleCommandHexSprite() ?? theme.PrimaryButton;
            frame.type = Image.Type.Simple;
            frame.preserveAspect = true;
            frame.color = frameColor;
            frame.raycastTarget = false;
            SetRelativeRect(frame.rectTransform, Vector2.zero, Vector2.one);
            var shadow = frameObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0.04f, 0.09f, 0.15f, 0.28f);
            shadow.effectDistance = new Vector2(0f, -4f);

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = frame;
            button.onClick.AddListener(onClick);
            button.interactable = interactable && !isNetworkBusy;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(0.95f, 0.98f, 1f, 1f),
                pressedColor = new Color(0.80f, 0.88f, 0.96f, 1f),
                selectedColor = new Color(0.95f, 0.98f, 1f, 1f),
                disabledColor = new Color(0.58f, 0.60f, 0.64f, 0.50f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };

            if (name == "QuickAttack")
            {
                AddBattleSwordMark(buttonObject.GetComponent<RectTransform>(), iconColor, constrainedBattleHud);
            }
            else if (icon != null)
            {
                var iconObject = new GameObject($"{name}_Icon", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(buttonObject.transform, false);
                var iconImage = iconObject.GetComponent<Image>();
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
                iconImage.color = iconColor;
                iconImage.raycastTarget = false;
                SetRelativeRect(
                    iconImage.rectTransform,
                    constrainedBattleHud ? new Vector2(0.27f, 0.39f) : new Vector2(0.24f, 0.38f),
                    constrainedBattleHud ? new Vector2(0.73f, 0.84f) : new Vector2(0.76f, 0.87f));
            }

            var actionLabelFontSize = ResolveUiFontSize(constrainedBattleHud ? 18 : 28);
            var labelText = ui.CreateText(buttonObject.transform, $"{name}_Label", label, actionLabelFontSize, FontStyle.Bold, labelColor, TextAnchor.MiddleCenter);
            SetRelativeRect(labelText.rectTransform, new Vector2(0.08f, 0.06f), new Vector2(0.92f, constrainedBattleHud ? 0.39f : 0.36f));
            ConfigureSingleLineLabel(labelText, ResolveUiFontSize(constrainedBattleHud ? 14 : 22));
            if (!constrainedBattleHud && labelColor != Color.white)
            {
                AddBattleTextShadow(labelText, new Color(1f, 1f, 1f, 0.62f), new Vector2(1f, -1f));
            }
            labelText.raycastTarget = false;
            return button;
        }

        private Sprite ResolveBattleCommandHexSprite()
        {
            var source = theme != null ? theme.HexBadge : null;
            if (source == null || source.texture == null)
            {
                return source;
            }

            if (battleCommandHexSprite != null && battleCommandHexSprite.texture == source.texture)
            {
                return battleCommandHexSprite;
            }

            if (battleCommandHexSprite != null)
            {
                DestroyRuntimeObject(battleCommandHexSprite);
            }

            var rect = source.rect;
            var cropped = new Rect(
                rect.x + rect.width * 0.12f,
                rect.y + rect.height * 0.06f,
                rect.width * 0.76f,
                rect.height * 0.88f);
            battleCommandHexSprite = Sprite.Create(
                source.texture,
                cropped,
                new Vector2(0.5f, 0.5f),
                Mathf.Max(1f, source.pixelsPerUnit),
                0,
                SpriteMeshType.FullRect);
            battleCommandHexSprite.name = "BattleCommandHex_CroppedTransparentMargin";
            return battleCommandHexSprite;
        }

        private static void AddBattleSwordMark(RectTransform parent, Color color, bool compact)
        {
            // Asset-free Unity UI geometry keeps the command readable without falling
            // back to an emoji or a web/CSS approximation. The three rectangles form a
            // single sword silhouette and add no texture memory to the WebGL build.
            var holder = new GameObject("QuickAttack_SwordMark", typeof(RectTransform));
            holder.transform.SetParent(parent, false);
            SetRelativeRect(
                holder.GetComponent<RectTransform>(),
                compact ? new Vector2(0.29f, 0.40f) : new Vector2(0.25f, 0.38f),
                compact ? new Vector2(0.71f, 0.84f) : new Vector2(0.75f, 0.88f));
            holder.transform.localRotation = Quaternion.Euler(0f, 0f, -42f);

            AddBattleSwordPiece(holder.transform, "Blade", color, new Vector2(0.43f, 0.26f), new Vector2(0.57f, 0.94f));
            AddBattleSwordPiece(holder.transform, "Guard", color, new Vector2(0.22f, 0.22f), new Vector2(0.78f, 0.34f));
            AddBattleSwordPiece(holder.transform, "Grip", color, new Vector2(0.44f, 0.04f), new Vector2(0.56f, 0.24f));
        }

        private static void AddBattleSwordPiece(Transform parent, string name, Color color, Vector2 min, Vector2 max)
        {
            var piece = new GameObject(name, typeof(RectTransform), typeof(Image));
            piece.transform.SetParent(parent, false);
            var image = piece.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            SetRelativeRect(image.rectTransform, min, max);
        }

        private string BuildCompactCommandLabel()
        {
            return $"{RoleShortLabel(selectedRole)} / {WeaponLabel(selectedWeapon)}";
        }

        private void AddBattleObserverMenu()
        {
            var portrait = UsesPortraitLoginLayout();
            var observerPanel = ui.CreatePanel(
                root,
                "BattleCompactCommandHud",
                theme.RaidPanel,
                portrait ? new Vector2(0.04f, 0.04f) : new Vector2(0.58f, 0.04f),
                portrait ? new Vector2(0.96f, 0.19f) : new Vector2(0.955f, 0.205f),
                Vector2.zero,
                Vector2.zero);
            AddBattleHudBacking(observerPanel);
            AddHorizontal(observerPanel, 8, 8);

            var summary = new GameObject("BattleObserverSummary", typeof(RectTransform), typeof(VerticalLayoutGroup));
            summary.transform.SetParent(observerPanel, false);
            AddLayout(summary, 1, -1);
            AddVertical(summary.GetComponent<RectTransform>(), 0, 0, TextAnchor.MiddleLeft);
            AddText(summary.transform, "VIEW", 12, FontStyle.Bold, theme.Cyan, 18);
            AddText(summary.transform, "観戦モード", 18, FontStyle.Bold, theme.Text, 29);
            AddBattleButtonCell(observerPanel, "FrontDisplay", "前面表示", theme.PrimaryButton, ShowFrontScreen, theme.Text, 13, 0.85f);
        }

        private void AddBattleBossHpHud(BossBattleState battle)
        {
            if (battle?.Boss == null)
            {
                return;
            }

            var boss = battle.Boss;
            var bossDisplayName = boss.Name;
#if UNITY_EDITOR || (UNITY_WEBGL && DEVELOPMENT_BUILD)
            if (webGlVisualQaActive && webGlVisualQaIntent?.Screen == WebGlVisualQaScreen.BattleActive)
            {
                bossDisplayName = "コーラルビートル";
            }
#endif
            var bossHpRatio = boss.MaxHp <= 0 ? 0f : Mathf.Clamp01(boss.CurrentHp / (float)boss.MaxHp);
            var compactLandscape = UsesCompactLandscapeLayout();
            var constrained = compactLandscape || UsesPortraitLayout();
            var viewport = ResolveLoginViewportSize();
            var compactBossHud = constrained || viewport.x < 1000f || viewport.y < 600f;
            var hpPanelObject = new GameObject("BattleBossHpHud", typeof(RectTransform), typeof(Image), typeof(Shadow));
            hpPanelObject.transform.SetParent(root, false);
            var hpPanel = hpPanelObject.GetComponent<RectTransform>();
            ui.SetRect(
                hpPanel,
                compactLandscape ? new Vector2(0.10f, 0.790f) : compactBossHud ? new Vector2(0.08f, 0.810f) : new Vector2(0.114f, 0.828f),
                compactLandscape ? new Vector2(0.80f, 0.919f) : compactBossHud ? new Vector2(0.92f, 0.905f) : new Vector2(0.424f, 0.910f),
                Vector2.zero,
                Vector2.zero);
            var hpFrame = hpPanelObject.GetComponent<Image>();
            // The reference uses a light, low-profile enemy pill. Reusing the login
            // status frame removes the heavy double chrome without adding a texture.
            hpFrame.sprite = theme.LoginStatusFrame ?? theme.LoginCtaButton ?? theme.PrimaryButton;
            hpFrame.type = hpFrame.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            hpFrame.color = Color.white;

            // login_status_frame.png intentionally contains only the ivory outline.
            // Give that outline the charcoal fill visible in the approved battle HUD;
            // otherwise the cyan sky shows through and the enemy bar reads like a flat
            // turquoise banner rather than a game UI pill.
            var hpBackingObject = new GameObject("BattleBossHpBacking", typeof(RectTransform), typeof(Image));
            hpBackingObject.transform.SetParent(hpPanel, false);
            var hpBacking = hpBackingObject.GetComponent<Image>();
            hpBacking.sprite = null;
            hpBacking.color = new Color(0.065f, 0.085f, 0.135f, 0.995f);
            hpBacking.raycastTarget = false;
            SetRelativeRect(hpBacking.rectTransform, new Vector2(0.018f, 0.10f), new Vector2(0.982f, 0.90f));
            hpBackingObject.transform.SetAsFirstSibling();
            var hpShadow = hpPanelObject.GetComponent<Shadow>();
            hpShadow.effectColor = new Color(0.04f, 0.09f, 0.15f, 0.34f);
            hpShadow.effectDistance = new Vector2(0f, -4f);
            hpShadow.useGraphicAlpha = true;

            var bossBadge = new GameObject("BattleBossPortrait", typeof(RectTransform), typeof(Image));
            bossBadge.transform.SetParent(hpPanel, false);
            var bossBadgeImage = bossBadge.GetComponent<Image>();
            bossBadgeImage.sprite = theme.BattleCircleButton ?? theme.HexBadge;
            bossBadgeImage.preserveAspect = true;
            bossBadgeImage.color = theme.BattlePortraitBossCoralBeetle != null
                ? Color.white
                : new Color(0.67f, 0.39f, 0.91f, 1f);
            bossBadgeImage.raycastTarget = false;
            SetRelativeRect(
                bossBadgeImage.rectTransform,
                compactBossHud ? new Vector2(-0.045f, 0.04f) : new Vector2(-0.006f, 0.035f),
                compactBossHud ? new Vector2(0.155f, 0.98f) : new Vector2(0.158f, 0.975f));

            AddBattleBossCrystalMark(bossBadgeImage.rectTransform);
            if (theme.BattlePortraitBossCoralBeetle != null)
            {
                AddBattleTurnPortraitImage(
                    bossBadgeImage.rectTransform,
                    "AuthoredBossHud",
                    theme.BattlePortraitBossCoralBeetle,
                    Color.white,
                    new Vector2(0.06f, 0.06f),
                    new Vector2(0.94f, 0.94f));
            }

            var bossName = AddAnchoredText(
                hpPanel,
                "BattleBossName",
                bossDisplayName,
                compactBossHud ? 18 : 23,
                FontStyle.Bold,
                Color.white,
                compactBossHud ? new Vector2(0.16f, 0.42f) : new Vector2(0.19f, 0.42f),
                compactBossHud ? new Vector2(0.70f, 0.94f) : new Vector2(0.96f, 0.94f),
                TextAnchor.MiddleLeft);
            PreserveFullSingleLineLabel(bossName, 10);
            bossName.horizontalOverflow = HorizontalWrapMode.Overflow;
            var hpValue = AddAnchoredText(
                hpPanel,
                "BattleBossHpValue",
                $"{boss.CurrentHp:N0} / {boss.MaxHp:N0}",
                compactBossHud ? 14 : 18,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.70f, 0.42f),
                new Vector2(0.96f, 0.94f),
                TextAnchor.MiddleRight);
            PreserveFullSingleLineLabel(hpValue, compactBossHud ? 13 : 17);
            hpValue.gameObject.SetActive(compactBossHud);

            var progressTrack = new GameObject("BattleBossHpProgress", typeof(RectTransform), typeof(Image));
            progressTrack.transform.SetParent(hpPanel, false);
            var progressTrackImage = progressTrack.GetComponent<Image>();
            progressTrackImage.color = new Color(0.035f, 0.065f, 0.115f, 0.92f);
            progressTrackImage.raycastTarget = false;
            SetRelativeRect(
                progressTrackImage.rectTransform,
                compactBossHud ? new Vector2(0.16f, 0.08f) : new Vector2(0.19f, 0.08f),
                new Vector2(0.96f, 0.34f));

            var progressFill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            progressFill.transform.SetParent(progressTrack.transform, false);
            var progressFillImage = progressFill.GetComponent<Image>();
            progressFillImage.type = Image.Type.Filled;
            progressFillImage.fillMethod = Image.FillMethod.Horizontal;
            progressFillImage.fillOrigin = 0;
            progressFillImage.fillAmount = bossHpRatio;
            progressFillImage.color = new Color(1f, 0.27f, 0.38f, 1f);
            progressFillImage.raycastTarget = false;
            ui.Stretch(progressFillImage.rectTransform, 2f, 2f, 2f, 2f);
        }

        private static void AddBattleBossCrystalMark(RectTransform parent)
        {
            var dark = new Color(0.20f, 0.10f, 0.29f, 1f);
            var cyan = new Color(0.31f, 0.90f, 0.96f, 1f);
            AddBattleBossCrystalPiece(parent, "Core", dark, new Vector2(0.34f, 0.27f), new Vector2(0.66f, 0.67f), 45f);
            AddBattleBossCrystalPiece(parent, "HornLeft", dark, new Vector2(0.18f, 0.56f), new Vector2(0.42f, 0.84f), 34f);
            AddBattleBossCrystalPiece(parent, "HornRight", dark, new Vector2(0.58f, 0.56f), new Vector2(0.82f, 0.84f), 56f);
            AddBattleBossCrystalPiece(parent, "EyeLeft", cyan, new Vector2(0.28f, 0.43f), new Vector2(0.46f, 0.51f), -8f);
            AddBattleBossCrystalPiece(parent, "EyeRight", cyan, new Vector2(0.54f, 0.43f), new Vector2(0.72f, 0.51f), 8f);
        }

        private static void AddBattleBossCrystalPiece(
            RectTransform parent,
            string name,
            Color color,
            Vector2 min,
            Vector2 max,
            float rotation)
        {
            var piece = new GameObject($"BattleBossMark_{name}", typeof(RectTransform), typeof(Image));
            piece.transform.SetParent(parent, false);
            var image = piece.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            SetRelativeRect(image.rectTransform, min, max);
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private void AddBattleTurnOrderStrip(BossBattleState battle)
        {
            var viewport = ResolveLoginViewportSize();
            if (battle == null ||
                !battle.IsActive ||
                UsesConstrainedLayout() ||
                viewport.x < 1000f ||
                viewport.y < 600f)
            {
                return;
            }

            var stripObject = new GameObject("BattleTurnOrderStrip", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            stripObject.transform.SetParent(EnsureRoot(), false);
            var strip = stripObject.GetComponent<RectTransform>();
            var narrowLandscape = viewport.x / Mathf.Max(1f, viewport.y) < 1.55f;
            SetAnchored(strip, new Vector2(narrowLandscape ? 0.605f : 0.650f, 0.888f), new Vector2(0.900f, 0.976f));
            var layout = stripObject.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var participants = battle.Participants.Take(3).ToList();
            var arrowIndex = 0;
            for (var index = 0; index < participants.Count; index++)
            {
                if (index > 0)
                {
                    AddBattleTurnOrderArrow(strip, arrowIndex++);
                }

                // Match the readable reference rhythm: two hero portraits, the boss,
                // then the third hero portrait. Arrows make chronology explicit.
                if (index == 2)
                {
                    AddBattleTurnOrderItem(
                        strip,
                        "BattleTurnOrderBoss",
                        null,
                        new Color(0.53f, 0.29f, 0.91f, 1f),
                        true);
                    AddBattleTurnOrderArrow(strip, arrowIndex++);
                }

                var participant = participants[index];
                var icon = participant.Role switch
                {
                    BattleRole.Attacker => theme.BattleIcon,
                    BattleRole.Healer => theme.AchievementIcon ?? theme.GoalIcon,
                    BattleRole.Defender => theme.BattleShieldIcon ?? theme.CheckIcon,
                    _ => theme.TeamIcon
                };
                // The blue / coral / green portrait rhythm follows the approved
                // reference while the small chest insignia still communicates role.
                var color = index switch
                {
                    0 => new Color(0.12f, 0.45f, 0.88f, 1f),
                    1 => new Color(0.94f, 0.29f, 0.22f, 1f),
                    _ => new Color(0.42f, 0.75f, 0.20f, 1f)
                };
                AddBattleTurnOrderItem(strip, $"BattleTurnOrderParticipant_{index}", icon, color, false, index);
            }

            if (participants.Count < 3)
            {
                if (participants.Count > 0)
                {
                    AddBattleTurnOrderArrow(strip, arrowIndex);
                }
                AddBattleTurnOrderItem(
                    strip,
                    "BattleTurnOrderBoss",
                    null,
                    new Color(0.53f, 0.29f, 0.91f, 1f),
                    true);
            }
        }

        private void AddBattleTurnOrderItem(RectTransform parent, string name, Sprite icon, Color accent, bool boss, int portraitStyle = 0)
        {
            var itemObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(Shadow));
            itemObject.transform.SetParent(parent, false);
            var itemImage = itemObject.GetComponent<Image>();
            itemImage.sprite = theme.BattleCircleButton ?? theme.HexBadge;
            itemImage.preserveAspect = true;
            itemImage.color = Color.white;
            itemImage.raycastTarget = false;
            var itemLayout = itemObject.GetComponent<LayoutElement>();
            itemLayout.flexibleWidth = 0f;
            itemLayout.flexibleHeight = 0f;
            itemLayout.preferredWidth = 72f;
            itemLayout.preferredHeight = 72f;
            itemLayout.minWidth = 60f;
            itemLayout.minHeight = 60f;
            var shadow = itemObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0.04f, 0.09f, 0.15f, 0.30f);
            shadow.effectDistance = new Vector2(0f, -3f);
            shadow.useGraphicAlpha = true;

            if (boss)
            {
                AddBattleBossCrystalMark(itemImage.rectTransform);
                if (theme.BattlePortraitBossCoralBeetle != null)
                {
                    AddBattleTurnPortraitImage(
                        itemImage.rectTransform,
                        "AuthoredBoss",
                        theme.BattlePortraitBossCoralBeetle,
                        Color.white,
                        new Vector2(0.05f, 0.05f),
                        new Vector2(0.95f, 0.95f));
                }
                return;
            }

            AddBattleTurnOrderHeroPortrait(itemImage.rectTransform, icon, accent, portraitStyle);
            var authoredHeroPortrait = portraitStyle switch
            {
                1 => theme.BattlePortraitHeroRed,
                2 => theme.BattlePortraitHeroGreen,
                _ => theme.BattlePortraitHeroBlue
            };
            if (authoredHeroPortrait != null)
            {
                AddBattleTurnPortraitImage(
                    itemImage.rectTransform,
                    "AuthoredHero",
                    authoredHeroPortrait,
                    Color.white,
                    new Vector2(0.05f, 0.05f),
                    new Vector2(0.95f, 0.95f));
            }
        }

        private void AddBattleTurnOrderArrow(RectTransform parent, int index)
        {
            var arrowObject = new GameObject($"BattleTurnOrderArrow_{index}", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(Shadow));
            arrowObject.transform.SetParent(parent, false);
            var arrowImage = arrowObject.GetComponent<Image>();
            arrowImage.sprite = theme.BackIcon;
            arrowImage.preserveAspect = true;
            arrowImage.color = Color.white;
            arrowImage.raycastTarget = false;
            arrowImage.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            var arrowLayout = arrowObject.GetComponent<LayoutElement>();
            arrowLayout.flexibleWidth = 0f;
            arrowLayout.flexibleHeight = 0f;
            arrowLayout.preferredWidth = 17f;
            arrowLayout.preferredHeight = 24f;
            arrowLayout.minWidth = 13f;
            arrowLayout.minHeight = 18f;
            var shadow = arrowObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0.05f, 0.12f, 0.20f, 0.48f);
            shadow.effectDistance = new Vector2(1.5f, -2f);
            shadow.useGraphicAlpha = true;
        }

        private void AddBattleTurnOrderHeroPortrait(RectTransform parent, Sprite roleIcon, Color accent, int portraitStyle)
        {
            var darkAccent = Color.Lerp(accent, new Color(0.05f, 0.09f, 0.16f, 1f), 0.34f);
            var skin = portraitStyle switch
            {
                1 => new Color(0.98f, 0.67f, 0.49f, 1f),
                2 => new Color(0.91f, 0.67f, 0.48f, 1f),
                _ => new Color(0.95f, 0.72f, 0.55f, 1f)
            };

            AddBattleTurnPortraitImage(parent, "Backdrop", theme.BattleCircleButton, accent, new Vector2(0.10f, 0.10f), new Vector2(0.90f, 0.90f));
            AddBattleTurnPortraitImage(parent, "Shoulders", theme.HexBadge, darkAccent, new Vector2(0.17f, 0.04f), new Vector2(0.83f, 0.43f));
            AddBattleTurnPortraitImage(parent, "Face", theme.BattleCircleButton, skin, new Vector2(0.29f, 0.25f), new Vector2(0.71f, 0.70f));
            AddBattleTurnPortraitImage(parent, "Hair", theme.HexBadge, darkAccent, new Vector2(0.20f, 0.48f), new Vector2(0.80f, 0.88f));
            AddBattleTurnPortraitPiece(parent, "EyeLeft", new Color(0.07f, 0.11f, 0.17f, 1f), new Vector2(0.37f, 0.43f), new Vector2(0.43f, 0.49f));
            AddBattleTurnPortraitPiece(parent, "EyeRight", new Color(0.07f, 0.11f, 0.17f, 1f), new Vector2(0.57f, 0.43f), new Vector2(0.63f, 0.49f));
            if (roleIcon != null)
            {
                AddBattleTurnPortraitImage(parent, "Role", roleIcon, Color.white, new Vector2(0.42f, 0.08f), new Vector2(0.58f, 0.24f));
            }
        }

        private static void AddBattleTurnPortraitImage(RectTransform parent, string name, Sprite sprite, Color color, Vector2 min, Vector2 max)
        {
            var portraitPart = new GameObject($"BattleTurnPortrait_{name}", typeof(RectTransform), typeof(Image));
            portraitPart.transform.SetParent(parent, false);
            var image = portraitPart.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = color;
            image.raycastTarget = false;
            SetRelativeRect(image.rectTransform, min, max);
        }

        private static void AddBattleTurnPortraitPiece(RectTransform parent, string name, Color color, Vector2 min, Vector2 max)
        {
            AddBattleTurnPortraitImage(parent, name, null, color, min, max);
        }

        private void AddBattleActivityStrip(Transform parent, BossBattleState battle)
        {
            var latest = battle.Actions?.LastOrDefault();
            if (latest == null)
            {
                return;
            }

            AddText(parent, $"LAST ACTION / {latest.Nickname} {BattleActionShortLabel(latest.ActionType)}  DMG {latest.Damage:N0}", 15, FontStyle.Bold, theme.Cyan, 25, TextAnchor.MiddleCenter);
        }

        private void AddBattleResultPanel(Transform panel, BattleResultSummary summary, string userId)
        {
            AddText(panel, summary.ResultTitle, 26, FontStyle.Bold, summary.IsVictory ? theme.Cyan : theme.Danger, ResolveMinimumUiLength(42f, 32f), TextAnchor.MiddleCenter);
            var resultMetrics = CreateHudRow(panel, "BattleResultMetrics", ResolveMinimumUiLength(58f, 50f));
            AddBattleHudMetric(resultMetrics, "BOSS HP", $"{summary.BossCurrentHp:N0}/{summary.BossMaxHp:N0}", theme.Text);
            AddBattleHudMetric(resultMetrics, "TEAM DMG", $"{summary.TeamDamage:N0}", theme.Magenta);
            AddBattleHudMetric(resultMetrics, "参加", $"{summary.ParticipantCount}人", theme.Cyan);
            AddText(panel, summary.RewardSummary, 16, FontStyle.Bold, theme.Magenta, ResolveMinimumUiLength(30f, 24f), TextAnchor.MiddleCenter);

            var personal = summary.Contributors.FirstOrDefault(entry => entry.UserId == userId);
            if (personal != null)
            {
                AddText(panel, "YOUR CONTRIBUTION", 14, FontStyle.Bold, theme.MutedText, ResolveMinimumUiLength(22f, 18f));
                AddText(panel, $"{personal.HighlightContext} / Score {personal.ContributionScore:N0} / +{personal.RewardExp}EXP", 16, FontStyle.Bold, theme.Cyan, ResolveMinimumUiLength(30f, 23f));
            }

            AddText(panel, "TOP CONTRIBUTORS", 14, FontStyle.Bold, theme.MutedText, ResolveMinimumUiLength(22f, 18f));
            var rank = 1;
            foreach (var entry in summary.Contributors.Take(3))
            {
                var mvp = entry.IsMvp ? "MVP " : string.Empty;
                AddText(panel, $"{rank}. {mvp}{entry.Nickname}  DMG {entry.Damage:N0}  +{entry.RewardExp}EXP", 16, FontStyle.Bold, entry.IsMvp ? theme.Magenta : theme.Text, ResolveMinimumUiLength(28f, 22f));
                rank += 1;
            }

            var front = ui.CreateButton(panel, "FrontDisplay", "前に映す画面", theme.PrimaryButton, ShowFrontScreen);
            AddLayout(front.gameObject, -1, ResolveMinimumUiLength(48f, 44f));
        }

        private void ShowFrontScreen()
        {
            ClearTemporaryPasswordReveal();
            var standaloneFrontDisplay = IsActiveProductionScene(RasshiineProductionScene.FrontDisplay);
            var readOnlyDisplay = currentUser == null || standaloneFrontDisplay;
            MarkScene(RasshiineProductionScene.FrontDisplay);
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Home);
            ClearRoot();
            UnityEngine.Events.UnityAction backAction = null;
            var backLabel = DefaultBackLabel;
            if (!readOnlyDisplay && currentUser != null)
            {
                backAction = currentUser.Role == UserRole.Mentor
                    ? (UnityEngine.Events.UnityAction)ShowMentorDashboard
                    : ShowMemberHome;
                backLabel = "戻る";
            }

            AddHeader("開発状況", string.Empty, backAction, backLabel, currentUser != null);
            var panel = ui.CreatePanel(root, "FrontPanel", theme.RaidPanel, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.80f), Vector2.zero, Vector2.zero);

            var usesSceneRepository = standaloneFrontDisplay || currentUser == null;
            if (!usesSceneRepository)
            {
                frontDisplayRepository ??= LocalGameRepository.CreateAuthoritativeCache();
            }
            var displayRepository = usesSceneRepository ? repository : frontDisplayRepository;
            var summary = displayRepository.GetFrontDisplaySummary();
            battleController?.LoadBattle(SelectBattleStageState(
                true,
                repository.ActiveBattle,
                displayRepository.ActiveBattle));
            battleController?.SetControlledParticipant(null);
            UnityEngine.Events.UnityAction openDevLog = currentUser == null
                ? (UnityEngine.Events.UnityAction)ShowLogin
                : currentUser.Role == UserRole.Mentor
                    ? ShowMentorDashboard
                    : ShowDevLog;
            UnityEngine.Events.UnityAction openBattle = currentUser == null
                ? (UnityEngine.Events.UnityAction)ShowLogin
                : ShowBattle;

            if (!usesSceneRepository && displayRepository.ActiveBattle == null &&
                Application.isPlaying && !isNetworkBusy &&
                supabase is { IsConfigured: true, HasSession: true })
            {
                TryRefreshRemoteSnapshot(ShowFrontScreen, true);
            }

            if (UsesPortraitLayout())
            {
                BuildPortraitFrontDisplay(panel, summary, openDevLog, openBattle);
                return;
            }

            if (UsesCompactLandscapeLayout())
            {
                BuildCompactFrontDisplay(panel, summary, openDevLog, openBattle);
                return;
            }

            var left = ui.CreatePanel(panel, "FrontLeft", theme.LogPanel, new Vector2(0.025f, 0.06f), new Vector2(0.675f, 0.94f), Vector2.zero, Vector2.zero);
            AddVertical(left, 18, 10);
            AddText(left, "今週の開発", 34, FontStyle.Bold, theme.Text, 44, TextAnchor.MiddleCenter);
            AddText(left, FormatMinutes(summary.WeeklyApprovedMinutes), 58, FontStyle.Bold, theme.Cyan, 70, TextAnchor.MiddleCenter);
            var frontMetrics = CreateHudRow(left, "FrontMetrics", 90);
            AddFrontDisplayMetric(frontMetrics, "参加メンバー", $"{summary.ParticipantCount} / {summary.MemberCount}", theme.Text, 28);
            AddFrontDisplayMetric(frontMetrics, "進行状況", summary.IsCompleted ? summary.ResultTitle : summary.PhaseLabel, summary.IsCompleted ? theme.Magenta : theme.Cyan, 24);
            AddFrontDisplayMetric(frontMetrics, "最近の記録", $"{summary.Highlights.Count}件", theme.Text, 28);
            if (summary.IsCompleted)
            {
                AddText(left, summary.RewardSummary, 24, FontStyle.Bold, theme.Magenta, 42, TextAnchor.MiddleCenter);
            }

            var actionRow = CreateHudRow(left, "FrontActions", 84);
            var devActionLabel = currentUser == null
                ? "ログインして記録"
                : currentUser.Role == UserRole.Mentor
                    ? "承認状況を確認"
                    : "開発を記録";
            AddDashboardAction(actionRow, devActionLabel, theme.PrimaryButton, openDevLog).name = devActionLabel;
            AddDashboardAction(actionRow, "ボス戦に参加", theme.SecondaryButton, openBattle).name = "ボス戦に参加";

            var right = ui.CreatePanel(panel, "FrontRight", theme.RaidPanel, new Vector2(0.705f, 0.06f), new Vector2(0.975f, 0.94f), Vector2.zero, Vector2.zero);
            AddVertical(right, 16, 8);
            AddText(right, "今週の注目メンバー", 28, FontStyle.Bold, theme.Text, 38, TextAnchor.MiddleCenter);
            if (summary.TopHighlight != null)
            {
                var highlight = summary.TopHighlight;
                AddText(right, Shorten(highlight.Nickname, 16), 38, FontStyle.Bold, theme.Cyan, 48, TextAnchor.MiddleCenter);
                AddText(right, $"開発時間 {FormatMinutes(highlight.ApprovedMinutes)}", 22, FontStyle.Bold, theme.Text, 32, TextAnchor.MiddleCenter);
            }

            AddText(right, "最近のハイライト", 22, FontStyle.Bold, theme.Text, 30, TextAnchor.MiddleCenter);
            foreach (var highlight in summary.Highlights.Skip(1).Take(3))
            {
                AddText(right, $"{Shorten(highlight.Nickname, 12)}  {FormatMinutes(highlight.ApprovedMinutes)}", 18, FontStyle.Bold, theme.MutedText, 26, TextAnchor.MiddleCenter);
            }
        }

        private void BuildPortraitFrontDisplay(
            RectTransform panel,
            FrontDisplaySummary summary,
            UnityEngine.Events.UnityAction openDevLog,
            UnityEngine.Events.UnityAction openBattle)
        {
            AddVertical(panel, 12, 7, TextAnchor.MiddleCenter);
            AddText(panel, "PUBLIC RAID BOARD", 12, FontStyle.Bold, theme.Gold, 20, TextAnchor.MiddleCenter);
            var headline = CreateHudRow(panel, "FrontPortraitHeadline", 58);
            var headlineLabel = AddDisplayText(headline, "今週の開発", 25, theme.Text, 58, TextAnchor.MiddleLeft);
            AddLayout(headlineLabel.gameObject, 1f, -1);
            PreserveFullSingleLineLabel(headlineLabel, 20);
            var headlineValue = AddDisplayText(headline, FormatMinutes(summary.WeeklyApprovedMinutes), 33, theme.Cyan, 58, TextAnchor.MiddleRight);
            AddLayout(headlineValue.gameObject, 1f, -1);
            PreserveFullSingleLineLabel(headlineValue, 24);
            AddFrontPhaseBanner(panel, summary, 58);

            var metrics = CreateHudRow(panel, "FrontPortraitMetrics", 82);
            AddFrontDisplayMetric(metrics, "参加", $"{summary.ParticipantCount}/{summary.MemberCount}", theme.Text, 21);
            AddFrontDisplayMetric(metrics, "状態", FrontPhaseValue(summary), FrontPhaseColor(summary), 18);
            AddFrontDisplayMetric(metrics, "記録", $"{summary.Highlights.Count}件", theme.Text, 21);

            AddFrontHighlightCard(panel, summary, true);
            if (summary.IsCompleted && !string.IsNullOrWhiteSpace(summary.RewardSummary))
            {
                AddText(panel, Shorten(summary.RewardSummary, 42), 18, FontStyle.Bold, theme.Gold, 42, TextAnchor.MiddleCenter);
            }

            AddFrontActionRow(panel, openDevLog, openBattle, 68);
        }

        private void BuildCompactFrontDisplay(
            RectTransform panel,
            FrontDisplaySummary summary,
            UnityEngine.Events.UnityAction openDevLog,
            UnityEngine.Events.UnityAction openBattle)
        {
            AddHorizontal(panel, 10, 8);
            var main = ui.CreatePanel(panel, "FrontCompactMain", theme.LogPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(main.gameObject, 0.70f, -1);
            AddVertical(main, 8, 5, TextAnchor.MiddleCenter);

            var headline = CreateHudRow(main, "FrontCompactHeadline", 48);
            var headlineLabel = AddText(headline, "今週の開発", 20, FontStyle.Bold, theme.Text, 48, TextAnchor.MiddleLeft);
            AddLayout(headlineLabel.gameObject, 1f, -1);
            PreserveFullSingleLineLabel(headlineLabel, 17);
            var headlineValue = AddDisplayText(headline, FormatMinutes(summary.WeeklyApprovedMinutes), 27, theme.Cyan, 48, TextAnchor.MiddleRight);
            AddLayout(headlineValue.gameObject, 1f, -1);
            PreserveFullSingleLineLabel(headlineValue, 20);
            AddFrontPhaseBanner(main, summary, 42);

            var metrics = CreateHudRow(main, "FrontCompactMetrics", 72);
            AddFrontDisplayMetric(metrics, "参加", $"{summary.ParticipantCount}/{summary.MemberCount}", theme.Text, 18);
            AddFrontDisplayMetric(metrics, "状態", FrontPhaseValue(summary), FrontPhaseColor(summary), 16);
            AddFrontDisplayMetric(metrics, "記録", $"{summary.Highlights.Count}件", theme.Text, 18);
            AddFrontActionRow(main, openDevLog, openBattle, 60);

            var side = ui.CreatePanel(panel, "FrontCompactHighlight", theme.RaidPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(side.gameObject, 0.30f, -1);
            AddVertical(side, 8, 5, TextAnchor.MiddleCenter);
            AddFrontHighlightCard(side, summary, false);
            if (summary.IsCompleted && !string.IsNullOrWhiteSpace(summary.RewardSummary))
            {
                AddText(side, Shorten(summary.RewardSummary, 28), 15, FontStyle.Bold, theme.Gold, 54, TextAnchor.MiddleCenter);
            }
        }

        private void AddFrontPhaseBanner(Transform parent, FrontDisplaySummary summary, float height)
        {
            var banner = ui.CreatePanel(parent, "FrontPhaseBanner", theme.NotificationPanel ?? theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(banner.gameObject, -1, height);
            AddHorizontal(banner, 12, 10);
            var accent = new GameObject("PhaseAccent", typeof(RectTransform), typeof(Image));
            accent.transform.SetParent(banner, false);
            AddLayout(accent, 8, -1);
            accent.GetComponent<Image>().color = FrontPhaseColor(summary);
            var lead = AddText(banner, FrontPhaseLead(summary), 18, FontStyle.Bold, FrontPhaseColor(summary), height, TextAnchor.MiddleLeft);
            AddLayout(lead.gameObject, 1.25f, -1);
            var detail = AddText(banner, FrontPhaseDetail(summary), 15, FontStyle.Bold, theme.MutedText, height, TextAnchor.MiddleRight);
            AddLayout(detail.gameObject, 0.75f, -1);
        }

        private void AddFrontHighlightCard(Transform parent, FrontDisplaySummary summary, bool includeRecent)
        {
            var card = ui.CreatePanel(parent, "FrontHighlightCard", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(card.gameObject, -1, includeRecent ? 148f : 146f);
            AddVertical(card, 8, 3, TextAnchor.MiddleCenter);
            AddText(card, "今週の注目", 14, FontStyle.Bold, theme.Gold, 20, TextAnchor.MiddleCenter);
            if (summary.TopHighlight == null)
            {
                AddText(card, "次の承認ログを待っています", 15, FontStyle.Bold, theme.MutedText, 48, TextAnchor.MiddleCenter);
                return;
            }

            // Keep the player name at the intended rendered size. At portrait and
            // 844x390 WebGL scales a 34-unit line box is shorter than the font's
            // actual ascent/descent, so best-fit cannot preserve even short names.
            var highlightName = AddDisplayText(card, Shorten(summary.TopHighlight.Nickname, includeRecent ? 13 : 9), includeRecent ? 25 : 20, theme.Cyan, 44, TextAnchor.MiddleCenter);
            PreserveFullSingleLineLabel(highlightName, includeRecent ? 18 : 15);
            AddText(card, $"開発 {FormatMinutes(summary.TopHighlight.ApprovedMinutes)}", 15, FontStyle.Bold, theme.Text, 23, TextAnchor.MiddleCenter);
            if (includeRecent)
            {
                var recent = string.Join("  ・  ", summary.Highlights.Skip(1).Take(2).Select(item => Shorten(item.Nickname, 7)));
                AddText(card, string.IsNullOrWhiteSpace(recent) ? "チームの記録を集計中" : recent, 13, FontStyle.Bold, theme.MutedText, 20, TextAnchor.MiddleCenter);
            }
        }

        private void AddFrontActionRow(
            Transform parent,
            UnityEngine.Events.UnityAction openDevLog,
            UnityEngine.Events.UnityAction openBattle,
            float height)
        {
            var actions = CreateHudRow(parent, "FrontResponsiveActions", height);
            var devLabel = currentUser == null
                ? "ログインして記録"
                : currentUser.Role == UserRole.Mentor
                    ? "承認状況を見る"
                    : "開発を記録";
            var dev = ui.CreateButton(actions, devLabel, devLabel, theme.PrimaryButton, openDevLog, Color.white);
            AddLayout(dev.gameObject, 1f, -1);
            var battle = ui.CreateButton(actions, "ボス戦を見る", "ボス戦を見る", theme.SecondaryButton, openBattle, theme.Text);
            AddLayout(battle.gameObject, 1f, -1);
        }

        private static string FrontPhaseValue(FrontDisplaySummary summary)
        {
            return summary.IsCompleted ? summary.ResultTitle : summary.PhaseLabel;
        }

        private string FrontPhaseLead(FrontDisplaySummary summary)
        {
            if (summary.IsCompleted)
            {
                return summary.IsVictory ? "勝利 — RAID COMPLETE" : "RAID RESULT";
            }

            return summary.IsScheduled ? "次のレイドを準備中" : "LIVE — 仲間が戦闘中";
        }

        private static string FrontPhaseDetail(FrontDisplaySummary summary)
        {
            if (summary.IsCompleted)
            {
                return "報酬を集計しました";
            }

            return summary.IsScheduled ? "開始を待っています" : "現在の貢献を反映中";
        }

        private Color FrontPhaseColor(FrontDisplaySummary summary)
        {
            return summary.IsCompleted ? theme.Gold : summary.IsScheduled ? theme.Warning : theme.Cyan;
        }

        private void ShowSettings()
        {
            if (currentUser == null)
            {
                ShowLogin();
                return;
            }

            ClearTemporaryPasswordReveal();
            settingsReturnScene = activeProductionScene;
            StopBattleStatePolling();
            SetBackdrop(activeProductionScene == RasshiineProductionScene.Battle || activeProductionScene == RasshiineProductionScene.FrontDisplay
                ? NeonCityBackdrop.BackdropPreset.Battle
                : NeonCityBackdrop.BackdropPreset.Home);
            ClearRoot();
            AddHeader("設定", string.Empty, ReturnFromSettings, DefaultBackLabel, false);

            var useFullWidth = UsesPortraitLayout() || UsesCompactLandscapeLayout();
            var settingsFrame = ui.CreatePanel(
                EnsureRoot(),
                "SettingsContent",
                theme.LogPanel,
                new Vector2(useFullWidth ? 0.06f : 0.43f, 0.12f),
                new Vector2(useFullWidth ? 0.94f : 0.96f, 0.80f),
                Vector2.zero,
                Vector2.zero);

            if (UsesConstrainedLayout())
            {
                BuildResponsiveSettings(settingsFrame);
                return;
            }

            var content = CreateEmbeddedScrollContent(settingsFrame, "SettingsScroll", 28, 18);

            AddText(content, "アカウント", 38, FontStyle.Bold, theme.Text, 56, TextAnchor.MiddleCenter);
            AddText(content, $"ログインID {currentUser.LoginId}  /  {UserRoleLabel(currentUser.Role)}", 24, FontStyle.Bold, theme.Cyan, 42, TextAnchor.MiddleCenter);
            AddText(content, "操作中の内容は自動で保存されます。", 18, FontStyle.Bold, theme.MutedText, 38, TextAnchor.MiddleCenter);

            AddButton(content, "前に映す画面", theme.SecondaryButton, ShowFrontScreen);
            AddButton(content, "戻る", theme.PrimaryButton, ReturnFromSettings);
            AddButton(content, "ログアウト", theme.DangerButton, RequestLogout);
        }

        private void BuildResponsiveSettings(RectTransform frame)
        {
            AddVertical(frame, 18, 10, TextAnchor.MiddleCenter);
            AddDisplayText(frame, "アカウント", 30, theme.Text, 48, TextAnchor.MiddleCenter);
            AddText(frame, $"{Shorten(currentUser.LoginId, 22)}  /  {UserRoleLabel(currentUser.Role)}", 20, FontStyle.Bold, theme.Cyan, 40, TextAnchor.MiddleCenter);
            AddText(frame, "入力中の内容は端末内に残さず、安全に同期します。", 15, FontStyle.Bold, theme.MutedText, 44, TextAnchor.MiddleCenter);

            var primaryActions = CreateHudRow(frame, "SettingsPrimaryActions", 64);
            var front = ui.CreateButton(primaryActions, "SettingsFrontDisplay", "前面表示", theme.SecondaryButton, ShowFrontScreen, theme.Text);
            AddLayout(front.gameObject, 1f, -1);
            var back = ui.CreateButton(primaryActions, "SettingsReturn", "元の画面へ戻る", theme.PrimaryButton, ReturnFromSettings, Color.white);
            AddLayout(back.gameObject, 1f, -1);

            var logout = ui.CreateButton(frame, "SettingsLogout", "この端末からログアウト", theme.DangerButton, RequestLogout, Color.white);
            AddLayout(logout.gameObject, -1, 64);
            AddText(frame, "共有端末では、終了時に必ずログアウトしてください。", 14, FontStyle.Bold, theme.Warning, 34, TextAnchor.MiddleCenter);
        }

        private void ReturnFromSettings()
        {
            if (currentUser == null)
            {
                ShowLogin();
                return;
            }

            switch (settingsReturnScene)
            {
                case RasshiineProductionScene.Battle:
                    ShowBattle();
                    return;
                case RasshiineProductionScene.FrontDisplay:
                    ShowFrontScreen();
                    return;
                case RasshiineProductionScene.DevLog:
                    ShowDevLog();
                    return;
                case RasshiineProductionScene.MentorDashboard when currentUser.Role == UserRole.Mentor:
                    ShowMentorDashboard();
                    return;
                case RasshiineProductionScene.MemberHome:
                    ShowMemberHome();
                    return;
                default:
                    if (RasshiineSceneCatalog.GetAuthenticatedHomeScene(currentUser.Role) == RasshiineProductionScene.MentorDashboard)
                    {
                        ShowMentorDashboard();
                    }
                    else
                    {
                        ShowMemberHome();
                    }
                    return;
            }
        }

        private void ShowMentorDashboard()
        {
            if (currentUser == null)
            {
                ShowLogin();
                return;
            }

            if (currentUser.Role != UserRole.Mentor)
            {
                SetSessionFeedback("メンター権限が必要な画面です。", FeedbackTone.Warning);
                ShowMemberHome();
                return;
            }

            ClearTemporaryPasswordReveal();
            MarkScene(RasshiineProductionScene.MentorDashboard);
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Home);
            ClearRoot();
            var pendingSessionCount = repository.GetPendingSessions().Count;
            var needsReviewCount = repository.GetPendingSessions(DevSessionReviewFilter.NeedsReview).Count;
            var pendingAchievementCount = repository.GetPendingAchievements().Count;
            var battle = repository.ActiveBattle;
            var reviewLabel = needsReviewCount > 0
                ? $"要確認 {needsReviewCount}件"
                : pendingSessionCount > 0
                    ? $"承認待ち {pendingSessionCount}件"
                    : "通常運用";

            if (UsesConstrainedLayout())
            {
                BuildResponsiveMentorDashboard(
                    battle,
                    pendingSessionCount,
                    needsReviewCount,
                    pendingAchievementCount,
                    reviewLabel);
                return;
            }

            var topBar = ui.CreatePanel(root, "MentorTopBar", theme.RaidPanel, new Vector2(0.04f, 0.855f), new Vector2(0.96f, 0.955f), Vector2.zero, Vector2.zero);
            AddHorizontal(topBar, 4, 16);
            topBar.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(18, 18, 4, 4);
            var refreshButton = ui.CreateButton(topBar, "MentorRefresh", "更新", theme.SecondaryButton, () =>
            {
                if (!TryRefreshRemoteSnapshot(ShowMentorDashboard))
                {
                    ShowMentorDashboard();
                }
            });
            AddLayout(refreshButton.gameObject, 190, -1);
            AddButtonOverlayLabel(refreshButton, "更新", 22);
            var titleBox = new GameObject("MentorTopTitle", typeof(RectTransform), typeof(VerticalLayoutGroup));
            titleBox.transform.SetParent(topBar, false);
            AddLayout(titleBox, 1, -1);
            AddVertical(titleBox.GetComponent<RectTransform>(), 0, 0, TextAnchor.MiddleCenter);
            AddText(titleBox.transform, "メンター画面", 30, FontStyle.Bold, theme.Text, 42, TextAnchor.MiddleCenter);
            var settingsButton = ui.CreateButton(topBar, "MentorSettings", "設定", theme.SecondaryButton, ShowSettings);
            AddLayout(settingsButton.gameObject, 190, -1);
            AddButtonOverlayLabel(settingsButton, "設定", 22);

            var shell = ui.CreatePanel(root, "MentorDashboardShell", theme.RaidPanel, new Vector2(0.055f, 0.115f), new Vector2(0.945f, 0.825f), Vector2.zero, Vector2.zero);
            AddHorizontal(shell, 22, 20);

            var statusFrame = ui.CreatePanel(shell, "MentorStatusPanel", theme.LogPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(statusFrame.gameObject, 0.64f, -1);
            var statusPanel = CreateEmbeddedScrollContent(statusFrame, "MentorStatusScroll", 18, 12);
            AddText(statusPanel, "らっしーね", 39, FontStyle.Bold, theme.Magenta, 56, TextAnchor.MiddleCenter);
            AddText(statusPanel, BattleStatusLabel(battle.Status), 24, FontStyle.Bold, theme.Cyan, 38, TextAnchor.MiddleCenter);
            AddText(statusPanel, $"今週 {FormatMinutes(repository.GetTotalApprovedMinutes())}", 25, FontStyle.Bold, theme.Cyan, 40, TextAnchor.MiddleCenter);
            AddText(statusPanel, $"BOSS HP {battle.Boss.CurrentHp:N0}", 27, FontStyle.Bold, theme.Text, 38, TextAnchor.MiddleCenter);
            AddProgress(statusPanel, battle.Boss.CurrentHp / (float)Mathf.Max(battle.Boss.MaxHp, 1), true, 76);
            AddText(statusPanel, $"承認 {pendingSessionCount} / 確認 {needsReviewCount} / 実績 {pendingAchievementCount}", 18, FontStyle.Bold, theme.MutedText, 32, TextAnchor.MiddleCenter);
            if (!string.IsNullOrWhiteSpace(lastMentorMessage))
            {
                AddFeedbackBanner(statusPanel, lastMentorMessage, lastMentorTone, 58);
            }

            var actionFrame = ui.CreatePanel(shell, "MentorRaidPanel", theme.RaidPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(actionFrame.gameObject, 0.36f, -1);
            var actionPanel = CreateEmbeddedScrollContent(actionFrame, "MentorRaidScroll", 18, 12);
            AddFrontDisplayMetric(actionPanel, "参加予定", $"{battle.Participants.Count} / {repository.Members.Count}", theme.Cyan, 40);
            var battleButton = ui.CreateButton(actionPanel, "MentorBattleButton", battle.Status == BattleStatus.Scheduled ? "ゲーム開始" : "ボス戦へ", theme.PrimaryButton, () =>
            {
                if (battle.Status != BattleStatus.Scheduled)
                {
                    ShowBattle();
                    return;
                }

                if (TryStartRemoteBattle(ShowBattle))
                {
                    return;
                }

                repository.StartBattle(currentUser.Id);
                battleController.LoadBattle(repository.ActiveBattle);
                battleController.SetControlledParticipant(null);
                SetBattleFeedback("ボス戦開始", FeedbackTone.Battle);
                ShowBattle();
            });
            AddLayout(battleButton.gameObject, -1, 72);
            var frontButton = ui.CreateButton(actionPanel, "MentorFrontButton", "全体画面", theme.SecondaryButton, ShowFrontScreen);
            AddLayout(frontButton.gameObject, -1, 64);
            if (pendingSessionCount > 0 || needsReviewCount > 0)
            {
                var reviewButton = ui.CreateButton(actionPanel, "MentorReviewButton", reviewLabel, needsReviewCount > 0 ? theme.DangerButton : theme.SecondaryButton, ShowMentorReviewQueue, needsReviewCount > 0 ? theme.Magenta : theme.Text);
                AddLayout(reviewButton.gameObject, -1, 64);
            }

            var menuButton = ui.CreateButton(actionPanel, "MentorOperationsButton", "管理メニュー", theme.SecondaryButton, ShowMentorOperations);
            AddLayout(menuButton.gameObject, -1, 64);
        }

        private void BuildResponsiveMentorDashboard(
            BossBattleState battle,
            int pendingSessionCount,
            int needsReviewCount,
            int pendingAchievementCount,
            string reviewLabel)
        {
            AddHeader("メンター画面", reviewLabel, null);
            var shell = ui.CreatePanel(root, "MentorDashboardShell", theme.RaidPanel, new Vector2(0.04f, 0.055f), new Vector2(0.96f, 0.82f), Vector2.zero, Vector2.zero);
            if (UsesPortraitLayout())
            {
                AddVertical(shell, 16, 12);
            }
            else
            {
                AddHorizontal(shell, 14, 12);
            }

            var status = ui.CreatePanel(shell, "MentorStatusPanel", theme.LogPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(status.gameObject, UsesPortraitLayout() ? 1f : 0.60f, -1);
            AddVertical(status, 8, 4, TextAnchor.MiddleCenter);
            var titleRow = CreateHudRow(status, "MentorResponsiveTitle", 46);
            var bossTitle = AddDisplayText(titleRow, "らっしーね", 25, theme.Gold, 46, TextAnchor.MiddleLeft);
            AddLayout(bossTitle.gameObject, 1.15f, -1);
            PreserveFullSingleLineLabel(bossTitle, 20);
            var battleStatus = AddText(titleRow, BattleStatusLabel(battle.Status), 16, FontStyle.Bold, theme.Cyan, 46, TextAnchor.MiddleRight);
            AddLayout(battleStatus.gameObject, 0.85f, -1);
            PreserveFullSingleLineLabel(battleStatus, 14);
            var metrics = CreateHudRow(status, "MentorResponsiveMetrics", 72);
            AddFrontDisplayMetric(metrics, "今週", FormatMinutes(repository.GetTotalApprovedMinutes()), theme.Cyan, 18);
            AddFrontDisplayMetric(metrics, "BOSS", $"{battle.Boss.CurrentHp:N0}", theme.Text, 18);
            AddFrontDisplayMetric(metrics, "参加", $"{battle.Participants.Count}/{repository.Members.Count}", theme.Cyan, 18);
            AddReadableProgress(status, battle.Boss.CurrentHp / (float)Mathf.Max(battle.Boss.MaxHp, 1), true, 36, "BOSS HP");
            AddText(status, $"承認 {pendingSessionCount}  ・  要確認 {needsReviewCount}  ・  実績 {pendingAchievementCount}", 14, FontStyle.Bold, needsReviewCount > 0 ? theme.Warning : theme.MutedText, 28, TextAnchor.MiddleCenter);
            if (!string.IsNullOrWhiteSpace(lastMentorMessage))
            {
                AddFeedbackBanner(status, lastMentorMessage, lastMentorTone, 62);
            }

            var actions = ui.CreatePanel(shell, "MentorRaidPanel", theme.RaidPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(actions.gameObject, UsesPortraitLayout() ? 1f : 0.40f, -1);
            AddVertical(actions, 8, 6, TextAnchor.MiddleCenter);
            var refresh = ui.CreateButton(actions, "MentorRefresh", "最新状態に更新", theme.SecondaryButton, () =>
            {
                if (!TryRefreshRemoteSnapshot(ShowMentorDashboard))
                {
                    ShowMentorDashboard();
                }
            }, theme.Text);
            AddLayout(refresh.gameObject, -1, 48);
            var firstRow = CreateHudRow(actions, "MentorResponsiveActionsA", 56);
            var battleButton = ui.CreateButton(firstRow, "MentorBattleButton", battle.Status == BattleStatus.Scheduled ? "ゲーム開始" : "ボス戦へ", theme.PrimaryButton, () => OpenOrStartMentorBattle(battle), Color.white);
            AddLayout(battleButton.gameObject, 1f, -1);
            var frontButton = ui.CreateButton(firstRow, "MentorFrontButton", "前面表示", theme.SecondaryButton, ShowFrontScreen, theme.Text);
            AddLayout(frontButton.gameObject, 1f, -1);
            var secondRow = CreateHudRow(actions, "MentorResponsiveActionsB", 56);
            var review = ui.CreateButton(secondRow, "MentorReviewButton", needsReviewCount > 0 ? $"要確認 {needsReviewCount}" : $"レビュー {pendingSessionCount}", needsReviewCount > 0 ? theme.DangerButton : theme.SecondaryButton, ShowMentorReviewQueue, needsReviewCount > 0 ? Color.white : theme.Text);
            AddLayout(review.gameObject, 1f, -1);
            var menu = ui.CreateButton(secondRow, "MentorOperationsButton", "管理メニュー", theme.SecondaryButton, ShowMentorOperations, theme.Text);
            AddLayout(menu.gameObject, 1f, -1);
        }

        private void OpenOrStartMentorBattle(BossBattleState battle)
        {
            if (battle.Status != BattleStatus.Scheduled)
            {
                ShowBattle();
                return;
            }

            if (TryStartRemoteBattle(ShowBattle))
            {
                return;
            }

            repository.StartBattle(currentUser.Id);
            battleController.LoadBattle(repository.ActiveBattle);
            battleController.SetControlledParticipant(null);
            SetBattleFeedback("ボス戦開始", FeedbackTone.Battle);
            ShowBattle();
        }

        private void ShowMentorOperations()
        {
            if (currentUser == null)
            {
                ShowLogin();
                return;
            }

            if (currentUser.Role != UserRole.Mentor)
            {
                SetSessionFeedback("メンター権限が必要な画面です。", FeedbackTone.Warning);
                ShowMemberHome();
                return;
            }

            ClearTemporaryPasswordReveal();
            MarkScene(RasshiineProductionScene.MentorDashboard);
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Home);
            ClearRoot();
            AddHeader("運用メニュー", string.Empty, ShowMentorDashboard);
            if (UsesConstrainedLayout())
            {
                BuildResponsiveMentorOperations();
                return;
            }

            var scroll = CreateDashboardScrollPanel(root, "MentorOperationsScroll", new Vector2(0.1f, 0.06f), new Vector2(0.9f, 0.82f));
            var battle = repository.ActiveBattle;
            var operationsHeight = DashboardSectionHeight(46f, 76f, 84f, 58f, 72f, battle.IsCompleted ? 72f : 0f);
            var operations = CreateDashboardSection(scroll, "MentorOperations", operationsHeight, theme.RaidPanel);
            AddText(operations, "運用ショートカット", 32, FontStyle.Bold, theme.Text, 46);
            var actionRow = CreateHudRow(operations, "MentorPrimaryActions", 76);
            AddDashboardAction(actionRow, "前面表示", theme.PrimaryButton, ShowFrontScreen);
            AddDashboardAction(actionRow, "チーム状況", theme.SecondaryButton, ShowMentorTeamStatus);
            AddDashboardAction(actionRow, "作品管理", theme.SecondaryButton, ShowProducts);
            AddDashboardAction(actionRow, "実績承認", theme.SecondaryButton, ShowAchievements);
            AddDashboardAction(actionRow, "アカウント管理", theme.SecondaryButton, ShowMentorAccounts);
            var battleMetrics = CreateHudRow(operations, "MentorBattleMetrics", 84);
            AddHudMetric(battleMetrics, "STATUS", BattleStatusLabel(battle.Status), theme.Magenta);
            AddHudMetric(battleMetrics, "BOSS HP", $"{battle.Boss.CurrentHp:N0}/{battle.Boss.MaxHp:N0}", theme.Text);
            AddHudMetric(battleMetrics, "参加", $"{battle.Participants.Count}人", theme.Cyan);
            AddProgress(operations, battle.Boss.CurrentHp / (float)Mathf.Max(battle.Boss.MaxHp, 1), true, 58);
            if (battle.Status == BattleStatus.Scheduled)
            {
                AddButton(operations, "ゲーム開始", theme.PrimaryButton, () =>
                {
                    if (TryStartRemoteBattle(ShowBattle))
                    {
                        return;
                    }

                    repository.StartBattle(currentUser.Id);
                    battleController.LoadBattle(repository.ActiveBattle);
                    battleController.SetControlledParticipant(null);
                    SetBattleFeedback("ボス戦開始", FeedbackTone.Battle);
                    ShowBattle();
                });
            }
            else
            {
                AddButton(operations, "ボス戦を確認", theme.SecondaryButton, ShowBattle);
            }

            if (battle.IsCompleted)
            {
                AddButton(operations, "次週の準備", theme.DangerButton, () =>
                {
                    if (TryResetRemoteBattle(ShowMentorDashboard))
                    {
                        return;
                    }

                    repository.ResetBattle(currentUser.Id);
                    battleController.LoadBattle(repository.ActiveBattle);
                    SetBattleFeedback("次週の準備完了", FeedbackTone.Success);
                    ShowMentorDashboard();
                });
            }
        }

        private void BuildResponsiveMentorOperations()
        {
            var battle = repository.ActiveBattle;
            var panel = ui.CreatePanel(root, "MentorOperationsScroll", theme.RaidPanel, new Vector2(0.04f, 0.055f), new Vector2(0.96f, 0.82f), Vector2.zero, Vector2.zero);
            AddVertical(panel, 14, 8, TextAnchor.MiddleCenter);
            AddText(panel, "運用ショートカット", 25, FontStyle.Bold, theme.Text, 38);

            if (UsesPortraitLayout())
            {
                var rowA = CreateHudRow(panel, "MentorPrimaryActionsA", 58);
                AddDashboardAction(rowA, "前面表示", theme.PrimaryButton, ShowFrontScreen);
                AddDashboardAction(rowA, "チーム状況", theme.SecondaryButton, ShowMentorTeamStatus);
                AddDashboardAction(rowA, "作品管理", theme.SecondaryButton, ShowProducts);
                var rowB = CreateHudRow(panel, "MentorPrimaryActionsB", 58);
                AddDashboardAction(rowB, "実績承認", theme.SecondaryButton, ShowAchievements);
                AddDashboardAction(rowB, "アカウント管理", theme.SecondaryButton, ShowMentorAccounts);
            }
            else
            {
                var row = CreateHudRow(panel, "MentorPrimaryActions", 68);
                AddDashboardAction(row, "前面表示", theme.PrimaryButton, ShowFrontScreen);
                AddDashboardAction(row, "チーム状況", theme.SecondaryButton, ShowMentorTeamStatus);
                AddDashboardAction(row, "作品管理", theme.SecondaryButton, ShowProducts);
                AddDashboardAction(row, "実績承認", theme.SecondaryButton, ShowAchievements);
                AddDashboardAction(row, "アカウント管理", theme.SecondaryButton, ShowMentorAccounts);
            }

            var metrics = CreateHudRow(panel, "MentorBattleMetrics", 78);
            AddFrontDisplayMetric(metrics, "STATUS", BattleStatusLabel(battle.Status), theme.Gold, 19);
            AddFrontDisplayMetric(metrics, "BOSS HP", $"{battle.Boss.CurrentHp:N0}/{battle.Boss.MaxHp:N0}", theme.Text, 19);
            AddFrontDisplayMetric(metrics, "参加", $"{battle.Participants.Count}人", theme.Cyan, 21);
            AddReadableProgress(panel, battle.Boss.CurrentHp / (float)Mathf.Max(battle.Boss.MaxHp, 1), true, 36, "レイド進行");

            var battleAction = ui.CreateButton(panel, "MentorOperationsBattleAction", battle.Status == BattleStatus.Scheduled ? "ゲームを開始する" : "ボス戦を確認する", battle.Status == BattleStatus.Scheduled ? theme.PrimaryButton : theme.SecondaryButton, () => OpenOrStartMentorBattle(battle), battle.Status == BattleStatus.Scheduled ? Color.white : theme.Text);
            AddLayout(battleAction.gameObject, -1, 60);
            if (battle.IsCompleted)
            {
                var reset = ui.CreateButton(panel, "MentorOperationsReset", "次週の準備", theme.DangerButton, () =>
                {
                    if (TryResetRemoteBattle(ShowMentorDashboard))
                    {
                        return;
                    }

                    repository.ResetBattle(currentUser.Id);
                    battleController.LoadBattle(repository.ActiveBattle);
                    SetBattleFeedback("次週の準備完了", FeedbackTone.Success);
                    ShowMentorDashboard();
                }, Color.white);
                AddLayout(reset.gameObject, -1, 58);
            }
        }

        private void ShowMentorReviewQueue()
        {
            if (currentUser == null)
            {
                ShowLogin();
                return;
            }

            if (currentUser.Role != UserRole.Mentor)
            {
                SetSessionFeedback("メンター権限が必要な画面です。", FeedbackTone.Warning);
                ShowMemberHome();
                return;
            }

            ClearTemporaryPasswordReveal();
            MarkScene(RasshiineProductionScene.MentorDashboard);
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Home);
            ClearRoot();
            AddHeader("承認レビュー", $"{ReviewFilterLabel(selectedReviewFilter)} / {repository.GetPendingSessions(selectedReviewFilter).Count}件", ShowMentorDashboard);
            if (UsesConstrainedLayout())
            {
                BuildResponsiveMentorReviewQueue();
                return;
            }

            var scroll = CreateScrollPanel(root, "MentorReviewScroll", new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.82f));
            var reviewFeedbackHeight = !string.IsNullOrWhiteSpace(lastMentorMessage) ? 68f : 0f;
            var filters = CreateDashboardSection(scroll, "MentorReviewFilters", DashboardSectionHeight(42f, 70f, reviewFeedbackHeight), theme.RaidPanel);
            AddText(filters, "レビュー対象", 30, FontStyle.Bold, theme.Text, 42);
            AddSelectorRow(filters, Enum.GetValues(typeof(DevSessionReviewFilter)).Cast<DevSessionReviewFilter>(), selectedReviewFilter, value =>
            {
                selectedReviewFilter = value;
                ShowMentorReviewQueue();
            }, ReviewFilterLabel);
            if (!string.IsNullOrWhiteSpace(lastMentorMessage))
            {
                AddFeedbackBanner(filters, lastMentorMessage, lastMentorTone, 68);
            }

            var items = repository.GetPendingSessions(selectedReviewFilter).ToList();
            if (items.Count == 0)
            {
                var empty = CreateDashboardSection(scroll, "MentorReviewEmpty", 180f, theme.RaidPanel);
                AddText(empty, $"{ReviewFilterLabel(selectedReviewFilter)}の対象はありません。", 26, FontStyle.Bold, theme.Cyan, 54, TextAnchor.MiddleCenter);
                return;
            }

            foreach (var session in items)
            {
                AddSessionSummary(scroll, session, true, ShowMentorReviewQueue);
            }
        }

        private void BuildResponsiveMentorReviewQueue()
        {
            var content = CreateScrollPanel(root, "MentorReviewScroll", new Vector2(0.04f, 0.035f), new Vector2(0.96f, 0.82f));
            AddSelectorRowCompact(content, Enum.GetValues(typeof(DevSessionReviewFilter)).Cast<DevSessionReviewFilter>(), selectedReviewFilter, value =>
            {
                selectedReviewFilter = value;
                mentorReviewPage = 0;
                mentorReviewCorrectionVisible = false;
                mentorReviewCorrectionSessionId = string.Empty;
                ShowMentorReviewQueue();
            }, ReviewFilterLabel);

            if (!string.IsNullOrWhiteSpace(lastMentorMessage))
            {
                AddFeedbackBanner(content, lastMentorMessage, lastMentorTone, 64);
            }

            var items = repository.GetPendingSessions(selectedReviewFilter).ToList();
            if (items.Count == 0)
            {
                var empty = CreateDashboardSection(content, "MentorReviewEmpty", 150f, theme.RaidPanel);
                AddDisplayText(empty, "確認は完了しています", 25, theme.Cyan, 54, TextAnchor.MiddleCenter);
                AddText(empty, $"{ReviewFilterLabel(selectedReviewFilter)}の対象はありません。", 17, FontStyle.Bold, theme.MutedText, 42, TextAnchor.MiddleCenter);
                return;
            }

            mentorReviewPage = ClampPage(mentorReviewPage, items.Count, 1);
            AddResponsivePagination(content, "MentorReview", items.Count, 1, mentorReviewPage, page =>
            {
                mentorReviewPage = page;
                mentorReviewCorrectionVisible = false;
                mentorReviewCorrectionSessionId = string.Empty;
                ShowMentorReviewQueue();
            });
            var session = items[mentorReviewPage];
            if (mentorReviewCorrectionVisible && string.Equals(mentorReviewCorrectionSessionId, session.Id, StringComparison.Ordinal))
            {
                AddResponsiveCorrectionReview(content, session);
            }
            else
            {
                AddResponsiveQuickReview(content, session);
            }
        }

        private void AddResponsiveQuickReview(Transform parent, DevSession session)
        {
            var user = repository.Users.First(item => item.Id == session.UserId);
            var card = CreateDashboardSection(parent, $"ResponsiveSession_{session.Id}", -1f, theme.StatCard);
            AddText(card, $"{StatusLabel(session.Status)} / {Shorten(user.Nickname, 10)} / {FormatMinutes(session.DurationMinutes)}", 23, FontStyle.Bold, StatusColor(session.Status), 38);
            AddText(card, $"達成度 {session.AchievementRate}%  ・  {Shorten(session.Goal, 30)}", 17, FontStyle.Bold, theme.Cyan, 32);
            if (session.Evaluation != null)
            {
                AddText(card, $"AI評価 {RankLabel(session.Evaluation.Rank)}  /  仮EXP +{session.PreviewExp}", 16, FontStyle.Bold, theme.MutedText, 28);
            }

            var actions = CreateHudRow(card, "ApprovalActions", 60);
            var approve = ui.CreateButton(actions, "Approve", "承認", theme.PrimaryButton, () => ApproveResponsiveSession(session), Color.white);
            AddLayout(approve.gameObject, 1f, -1);
            var correct = ui.CreateButton(actions, "OpenCorrection", "時間を修正", theme.SecondaryButton, () =>
            {
                mentorReviewCorrectionVisible = true;
                mentorReviewCorrectionSessionId = session.Id;
                ShowMentorReviewQueue();
            }, theme.Text);
            AddLayout(correct.gameObject, 1f, -1);
            var reject = ui.CreateButton(actions, "Reject", "却下", theme.DangerButton, () => RejectResponsiveSession(session), Color.white);
            AddLayout(reject.gameObject, 1f, -1);
        }

        private void AddResponsiveCorrectionReview(Transform parent, DevSession session)
        {
            var user = repository.Users.First(item => item.Id == session.UserId);
            var card = CreateDashboardSection(parent, $"ResponsiveCorrection_{session.Id}", -1f, theme.StatCard);
            AddText(card, $"{Shorten(user.Nickname, 12)} の開発時間を修正", 22, FontStyle.Bold, theme.Gold, 38);
            AddText(card, "達成度とAI評価は保持されます。修正理由を監査ログに残してください。", 15, FontStyle.Bold, theme.MutedText, 38);
            var durationInput = ui.CreateInput(card, "CorrectedDuration", $"開発時間（現在 {session.DurationMinutes}分）");
            durationInput.contentType = InputField.ContentType.IntegerNumber;
            durationInput.text = session.DurationMinutes.ToString();
            AddLayout(durationInput.gameObject, -1, 52);
            var commentInput = ui.CreateInput(card, "MentorCommentInput", "修正理由", true);
            AddLayout(commentInput.gameObject, -1, 58);
            var actions = CreateHudRow(card, "CorrectionActions", 60);
            var apply = ui.CreateButton(actions, "ApproveWithCorrections", "修正して承認", theme.PrimaryButton, () =>
            {
                var correctedDuration = ReadReviewInt(durationInput, session.DurationMinutes, 1, 24 * 60);
                var comment = ReadReviewComment(commentInput, $"開発時間を {correctedDuration}分へ修正して承認");
                if (TryReviewRemoteSession(session, true, comment, ShowMentorReviewQueue, correctedDuration))
                {
                    return;
                }

                var approved = repository.ApproveSessionWithCorrections(
                    session.Id,
                    currentUser.Id,
                    session.AchievementRate,
                    correctedDuration,
                    session.Reflection,
                    session.NextTask,
                    comment);
                mentorReviewCorrectionVisible = false;
                mentorReviewCorrectionSessionId = string.Empty;
                SetMentorFeedback(BuildGrowthFeedbackMessage(user.Nickname, approved), FeedbackTone.Success);
                ShowMentorReviewQueue();
            }, Color.white);
            AddLayout(apply.gameObject, 1.4f, -1);
            var cancel = ui.CreateButton(actions, "CancelCorrection", "戻る", theme.SecondaryButton, () =>
            {
                mentorReviewCorrectionVisible = false;
                mentorReviewCorrectionSessionId = string.Empty;
                ShowMentorReviewQueue();
            }, theme.Text);
            AddLayout(cancel.gameObject, 0.8f, -1);
        }

        private void ApproveResponsiveSession(DevSession session)
        {
            const string comment = "確認しました。正式EXPへ反映します。";
            if (TryReviewRemoteSession(session, true, comment, ShowMentorReviewQueue))
            {
                return;
            }

            var user = repository.Users.First(item => item.Id == session.UserId);
            var approved = repository.ApproveSession(session.Id, currentUser.Id, comment);
            SetMentorFeedback(BuildGrowthFeedbackMessage(user.Nickname, approved), FeedbackTone.Success);
            ShowMentorReviewQueue();
        }

        private void RejectResponsiveSession(DevSession session)
        {
            const string comment = "今回は内容を再確認してください。";
            if (TryReviewRemoteSession(session, false, comment, ShowMentorReviewQueue))
            {
                return;
            }

            var user = repository.Users.First(item => item.Id == session.UserId);
            repository.RejectSession(session.Id, currentUser.Id, comment);
            SetMentorFeedback($"{user.Nickname} のログを却下しました。履歴に理由が残ります。", FeedbackTone.Warning);
            ShowMentorReviewQueue();
        }

        private void ShowMentorTeamStatus()
        {
            if (currentUser == null)
            {
                ShowLogin();
                return;
            }

            if (currentUser.Role != UserRole.Mentor)
            {
                SetSessionFeedback("メンター権限が必要な画面です。", FeedbackTone.Warning);
                ShowMemberHome();
                return;
            }

            ClearTemporaryPasswordReveal();
            MarkScene(RasshiineProductionScene.MentorDashboard);
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Home);
            ClearRoot();
            AddHeader("チーム状況", $"メンバー {repository.Members.Count}人 / メンター {repository.Mentors.Count}人", ShowMentorDashboard);
            if (UsesConstrainedLayout())
            {
                BuildResponsiveMentorTeamStatus();
                return;
            }

            var scroll = CreateScrollPanel(root, "MentorTeamScroll", new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.82f));
            var topContributor = repository.GetHighlightedContributor();
            var teamHeight = DashboardSectionHeight(46f, 92f, topContributor != null ? 36f : 34f, topContributor != null ? 32f : 0f);
            var team = CreateDashboardSection(scroll, "MentorTeamOverview", teamHeight, theme.RaidPanel);
            AddText(team, "今週のチーム", 32, FontStyle.Bold, theme.Text, 46);
            var metrics = CreateHudRow(team, "MentorTeamMetrics", 92);
            AddHudMetric(metrics, "TEAM DEV", FormatMinutes(repository.GetTotalApprovedMinutes()), theme.Cyan);
            AddHudMetric(metrics, "MEMBERS", $"{repository.Members.Count}人", theme.Text);
            AddHudMetric(metrics, "MENTORS", $"{repository.Mentors.Count}人", theme.Text);
            AddHudMetric(metrics, "実績承認", $"{repository.GetPendingAchievements().Count}件", repository.GetPendingAchievements().Count > 0 ? theme.Magenta : theme.Cyan);
            if (topContributor != null)
            {
                AddText(team, $"今週の貢献TOP: {topContributor.Nickname}  Score {topContributor.ContributionScore:N0}", 23, FontStyle.Bold, theme.Magenta, 36);
                AddText(team, $"{topContributor.TeamName} / {topContributor.HighlightContext}", 19, FontStyle.Bold, theme.Cyan, 32);
            }
            else
            {
                AddText(team, "承認済みログまたはボス戦貢献がまだありません。", 20, FontStyle.Bold, theme.MutedText, 34);
            }

            var rosterRows = repository.Members.Take(5).Count();
            var roster = CreateDashboardSection(scroll, "MentorRosterPanel", Mathf.Max(220f, DashboardSectionHeight(42f) + rosterRows * 128f), theme.RaidPanel);
            AddMentorMemberRosterSection(roster);
            var auditRows = Mathf.Max(1, repository.GetRecentAuditLogs(6).Count);
            var audit = CreateDashboardSection(scroll, "MentorAuditPanel", DashboardSectionHeight(42f) + auditRows * 48f, theme.RaidPanel);
            AddRecentAuditLogSection(audit);
        }

        private void BuildResponsiveMentorTeamStatus()
        {
            var content = CreateScrollPanel(root, "MentorTeamScroll", new Vector2(0.04f, 0.035f), new Vector2(0.96f, 0.82f));
            var overview = ui.CreatePanel(content, "MentorTeamOverview", theme.RaidPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(overview.gameObject, -1, 92);
            AddHorizontal(overview, 10, 8);
            AddFrontDisplayMetric(overview, "TEAM DEV", FormatMinutes(repository.GetTotalApprovedMinutes()), theme.Cyan, 20);
            AddFrontDisplayMetric(overview, "MEMBERS", $"{repository.Members.Count}人", theme.Text, 20);
            AddFrontDisplayMetric(overview, "MENTORS", $"{repository.Mentors.Count}人", theme.Text, 20);
            AddFrontDisplayMetric(overview, "承認待ち", $"{repository.GetPendingAchievements().Count}件", theme.Gold, 20);

            var members = repository.Members
                .OrderByDescending(member => repository.GetApprovedMinutesThisWeek(member.Id))
                .ThenBy(member => member.LoginId)
                .ToList();
            var pageSize = ResponsiveCollectionPageSize(2, 4, 5);
            mentorTeamPage = ClampPage(mentorTeamPage, members.Count, pageSize);
            var roster = CreateDashboardSection(content, "MentorRosterPanel", -1f, theme.LogPanel);
            AddText(roster, "メンバー一覧", 24, FontStyle.Bold, theme.Text, 34);
            foreach (var member in members.Skip(mentorTeamPage * pageSize).Take(pageSize))
            {
                var stats = repository.GetStats(member.Id);
                var row = ui.CreatePanel(roster, $"MentorRoster_{member.Id}", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                AddLayout(row.gameObject, -1, 88);
                AddHorizontal(row, 12, 10);
                var identity = new GameObject("Identity", typeof(RectTransform));
                identity.transform.SetParent(row, false);
                AddLayout(identity, 1f, -1);
                AddVertical(identity.GetComponent<RectTransform>(), 0, 2);
                AddText(identity.transform, $"{Shorten(member.Nickname, 12)} / {member.TeamId}", 19, FontStyle.Bold, member.IsActive ? theme.Text : theme.MutedText, 30);
                AddText(identity.transform, $"Lv.{stats.Level}  ・  今週 {FormatMinutes(repository.GetApprovedMinutesThisWeek(member.Id))}", 16, FontStyle.Bold, theme.Cyan, 26);
                var pending = repository.GetSessionsForUser(member.Id).Count(session => session.Status is DevSessionStatus.Pending or DevSessionStatus.NeedsReview or DevSessionStatus.AiPending);
                var pendingLabel = AddText(row, $"確認 {pending}件", 17, FontStyle.Bold, pending > 0 ? theme.Gold : theme.MutedText, 88, TextAnchor.MiddleRight);
                AddLayout(pendingLabel.gameObject, 118f, -1);
            }

            AddResponsivePagination(roster, "MentorTeam", members.Count, pageSize, mentorTeamPage, page =>
            {
                mentorTeamPage = page;
                ShowMentorTeamStatus();
            });

            var latestAudit = repository.GetRecentAuditLogs(1).FirstOrDefault();
            AddText(content, latestAudit == null ? "監査ログはまだありません。" : $"最新の監査  {Shorten(BuildAuditLogLine(latestAudit), 52)}", 15, FontStyle.Bold, theme.MutedText, 34, TextAnchor.MiddleCenter);
        }

        private void ShowMentorAccounts()
        {
            if (currentUser == null)
            {
                ShowLogin();
                return;
            }

            if (currentUser.Role != UserRole.Mentor)
            {
                SetSessionFeedback("メンター権限が必要な画面です。", FeedbackTone.Warning);
                ShowMemberHome();
                return;
            }

            MarkScene(RasshiineProductionScene.MentorDashboard);
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Home);
            ClearRoot();
            AddHeader("アカウント管理", string.Empty, ShowMentorDashboard);
            if (UsesConstrainedLayout())
            {
                BuildResponsiveMentorAccounts();
                return;
            }

            var shell = ui.CreatePanel(root, "MentorAccountShell", theme.RaidPanel, new Vector2(0.12f, 0.055f), new Vector2(0.88f, 0.82f), Vector2.zero, Vector2.zero);
            AddHorizontal(shell, 22, 18);

            var guide = CreateColumn(shell, "MentorAccountGuide", theme.LogPanel, 0.42f);
            AddText(guide, "アカウント作成", 30, FontStyle.Bold, theme.Text, 56, TextAnchor.MiddleCenter);
            var badge = new GameObject("AccountBadge", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(guide, false);
            AddLayout(badge, -1, 150);
            var badgeImage = badge.GetComponent<Image>();
            badgeImage.sprite = theme.AchievementIcon != null ? theme.AchievementIcon : theme.HexBadge;
            badgeImage.preserveAspect = true;
            badgeImage.color = theme.Gold;
            badgeImage.raycastTarget = false;
            AddText(guide, "新しいメンバーを追加", 19, FontStyle.Bold, theme.Cyan, 32, TextAnchor.MiddleCenter);
            AddText(guide, "発行済みは別画面で確認", 17, FontStyle.Bold, theme.MutedText, 28, TextAnchor.MiddleCenter);

            var accountFrame = ui.CreatePanel(shell, "MentorAccountForm", theme.RaidPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(accountFrame.gameObject, 0.58f, -1);
            var accountPanel = CreateEmbeddedScrollContent(accountFrame, "MentorAccountFormScroll", 14, 10);
            if (!string.IsNullOrWhiteSpace(lastMentorMessage))
            {
                AddFeedbackBanner(accountPanel, lastMentorMessage, lastMentorTone, 58);
            }

            AddTemporaryPasswordReveal(accountPanel);
            AddMentorAccountSection(accountPanel, ShowMentorAccounts);
        }

        private void BuildResponsiveMentorAccounts()
        {
            var shell = ui.CreatePanel(root, "MentorAccountShell", theme.RaidPanel, new Vector2(0.04f, 0.035f), new Vector2(0.96f, 0.82f), Vector2.zero, Vector2.zero);
            AddVertical(shell, 14, 8);
            // This row is not itself selectable, so it does not receive AddLayout's
            // automatic interactive-size floor. Reserve enough design height for a
            // comfortable 48 px tab target on the 844x390 WebGL fallback.
            var nav = CreateHudRow(shell, "MentorAccountNavigation", 70);
            var createTab = ui.CreateButton(nav, "MentorAccountCreateTab", "アカウント作成", theme.PrimaryButton, ShowMentorAccounts, Color.white);
            AddLayout(createTab.gameObject, 1f, -1);
            var listTab = ui.CreateButton(nav, "MentorAccountListTab", "発行済みを見る", theme.SecondaryButton, ShowMentorAccountList, theme.Text);
            AddLayout(listTab.gameObject, 1f, -1);

            if (!string.IsNullOrWhiteSpace(lastMentorMessage))
            {
                AddFeedbackBanner(shell, lastMentorMessage, lastMentorTone, 58);
            }

            AddTemporaryPasswordReveal(shell);

            var body = new GameObject("ResponsiveAccountForm", typeof(RectTransform));
            body.transform.SetParent(shell, false);
            AddLayout(body, -1, UsesPortraitLayout() ? 720f : 360f);
            if (UsesPortraitLayout())
            {
                AddVertical(body.GetComponent<RectTransform>(), 0, 10);
            }
            else
            {
                AddHorizontal(body.GetComponent<RectTransform>(), 0, 12);
            }

            var fields = CreateDashboardSection(body.transform, "ResponsiveAccountFields", -1f, theme.LogPanel);
            AddLayout(fields.gameObject, 1f, -1);
            AddText(fields, "ログイン情報", 21, FontStyle.Bold, theme.Text, 32);
            memberLoginIdInput = ui.CreateInput(fields, "MemberLoginIdInput", "login-id");
            memberLoginIdInput.text = retainedMemberLoginIdDraft;
            memberLoginIdInput.onValueChanged.AddListener(value => retainedMemberLoginIdDraft = value);
            AddLayout(memberLoginIdInput.gameObject, -1, 52);
            memberNicknameInput = ui.CreateInput(fields, "MemberNicknameInput", "表示名");
            memberNicknameInput.text = retainedMemberNicknameDraft;
            memberNicknameInput.onValueChanged.AddListener(value => retainedMemberNicknameDraft = value);
            AddLayout(memberNicknameInput.gameObject, -1, 52);
            memberTeamIdInput = ui.CreateInput(fields, "MemberTeamIdInput", "team: blue / magenta / mentor");
            retainedMemberTeamIdDraft ??= selectedAccountRole == UserRole.Mentor ? "mentor" : "blue";
            memberTeamIdInput.text = retainedMemberTeamIdDraft;
            memberTeamIdInput.onValueChanged.AddListener(value => retainedMemberTeamIdDraft = value);
            AddLayout(memberTeamIdInput.gameObject, -1, 52);

            var controls = CreateDashboardSection(body.transform, "ResponsiveAccountControls", -1f, theme.LogPanel);
            AddLayout(controls.gameObject, 1f, -1);
            AddText(controls, "権限と公開範囲", 21, FontStyle.Bold, theme.Text, 32);
            AddSelectorRowCompact(controls, new[] { UserRole.Member, UserRole.Mentor }, selectedAccountRole, value =>
            {
                selectedAccountRole = value;
                ShowMentorAccounts();
            }, UserRoleLabel);
            AddSelectorRowCompact(controls, new[] { true, false }, selectedAccountRankingVisible, value =>
            {
                selectedAccountRankingVisible = value;
                ShowMentorAccounts();
            }, RankingVisibilityLabel);
            var issue = ui.CreateButton(controls, "アカウントを発行", "安全にアカウントを発行", theme.PrimaryButton, IssueAccountFromResponsiveForm, Color.white);
            AddLayout(issue.gameObject, -1, 58);
        }

        private void IssueAccountFromResponsiveForm()
        {
            if (TryCreateRemoteAccount(memberLoginIdInput.text, memberNicknameInput.text, selectedAccountRole, memberTeamIdInput.text, selectedAccountRankingVisible, ShowMentorAccounts))
            {
                return;
            }

            try
            {
                var result = repository.CreateUserAccount(currentUser.Id, memberLoginIdInput.text, memberNicknameInput.text, selectedAccountRole, memberTeamIdInput.text, selectedAccountRankingVisible);
                PersistRuntimeSnapshot();
                ClearRetainedMentorAccountDrafts();
                SetPendingTemporaryPasswordReveal(result.User.Nickname, result.TemporaryPassword);
                SetMentorFeedback($"{result.User.Nickname} を発行しました。初回パスワードは明示操作で一度だけ確認してください。", FeedbackTone.Success);
            }
            catch (Exception exception)
            {
                SetMentorFeedback(exception.Message, FeedbackTone.Danger);
            }

            ShowMentorAccounts();
        }

        private void AddTemporaryPasswordReveal(Transform parent)
        {
            if (string.IsNullOrWhiteSpace(pendingTemporaryPasswordReveal))
            {
                return;
            }

            var reveal = ui.CreatePanel(parent, "TemporaryPasswordReveal", theme.NotificationPanel ?? theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(reveal.gameObject, -1, temporaryPasswordRevealVisible ? 126f : 72f);
            AddHorizontal(reveal, 12, 10);
            var label = temporaryPasswordRevealVisible
                ? $"{Shorten(pendingTemporaryPasswordNickname, 12)}  初回PW  {pendingTemporaryPasswordReveal}"
                : $"{Shorten(pendingTemporaryPasswordNickname, 12)} の初回パスワードを発行しました";
            var revealLabel = AddText(reveal, label, temporaryPasswordRevealVisible ? 19 : 17, FontStyle.Bold, temporaryPasswordRevealVisible ? theme.Gold : theme.Cyan, temporaryPasswordRevealVisible ? 126f : 72f, TextAnchor.MiddleLeft);
            AddLayout(revealLabel.gameObject, 1f, -1);
            var action = ui.CreateButton(reveal, "TemporaryPasswordRevealAction", temporaryPasswordRevealVisible ? "表示を閉じる" : "初回PWを表示", temporaryPasswordRevealVisible ? theme.DangerButton : theme.SecondaryButton, () =>
            {
                if (temporaryPasswordRevealVisible)
                {
                    // Destroy() is deferred until the end of the Unity frame in players.
                    // Blank and hide the live secret first so it cannot survive for one more render.
                    revealLabel.text = string.Empty;
                    reveal.gameObject.SetActive(false);
                    ClearTemporaryPasswordReveal();
                }
                else
                {
                    temporaryPasswordRevealVisible = true;
                }

                ShowMentorAccounts();
            }, temporaryPasswordRevealVisible ? Color.white : theme.Text);
            AddLayout(action.gameObject, 190f, -1);
        }

        private void SetPendingTemporaryPasswordReveal(string nickname, string temporaryPassword)
        {
            pendingTemporaryPasswordNickname = nickname?.Trim() ?? string.Empty;
            pendingTemporaryPasswordReveal = temporaryPassword?.Trim() ?? string.Empty;
            temporaryPasswordRevealVisible = false;
        }

        private void ClearTemporaryPasswordReveal()
        {
            var visibleReveal = root?.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(rect => string.Equals(rect.name, "TemporaryPasswordReveal", StringComparison.Ordinal));
            if (visibleReveal != null)
            {
                foreach (var text in visibleReveal.GetComponentsInChildren<Text>(true))
                {
                    text.text = string.Empty;
                }

                visibleReveal.gameObject.SetActive(false);
            }

            pendingTemporaryPasswordNickname = string.Empty;
            pendingTemporaryPasswordReveal = string.Empty;
            temporaryPasswordRevealVisible = false;
        }

        private void ShowMentorAccountList()
        {
            if (currentUser == null)
            {
                ShowLogin();
                return;
            }

            if (currentUser.Role != UserRole.Mentor)
            {
                SetSessionFeedback("メンター権限が必要な画面です。", FeedbackTone.Warning);
                ShowMemberHome();
                return;
            }

            MarkScene(RasshiineProductionScene.MentorDashboard);
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Home);
            ClearTemporaryPasswordReveal();
            ClearRoot();
            AddHeader("発行済み", string.Empty, ShowMentorAccounts);
            var scroll = CreateScrollPanel(
                root,
                "MentorAccountListScroll",
                UsesConstrainedLayout() ? new Vector2(0.04f, 0.035f) : new Vector2(0.16f, 0.06f),
                UsesConstrainedLayout() ? new Vector2(0.96f, 0.82f) : new Vector2(0.84f, 0.82f));
            if (!string.IsNullOrWhiteSpace(lastMentorMessage))
            {
                AddFeedbackBanner(scroll, lastMentorMessage, lastMentorTone, 74);
            }

            var users = repository.Users.OrderBy(user => user.LoginId).ToList();
            var pageSize = ResponsiveCollectionPageSize(CompactCollectionPageSize, PortraitCollectionPageSize, MentorAccountPageSize);
            var pageCount = Mathf.Max(1, Mathf.CeilToInt(users.Count / (float)pageSize));
            mentorAccountPage = Mathf.Clamp(mentorAccountPage, 0, pageCount - 1);
            AddMentorAccountPagination(scroll, users.Count, pageCount, pageSize);

            foreach (var member in users
                         .Skip(mentorAccountPage * pageSize)
                         .Take(pageSize))
            {
                AddMemberAccountSummary(scroll, member, ShowMentorAccountList);
            }
        }

        private void AddMentorAccountPagination(Transform parent, int userCount, int pageCount, int pageSize)
        {
            var pagination = ui.CreatePanel(parent, "MentorAccountPagination", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(pagination.gameObject, -1, 72);
            AddHorizontal(pagination, 12, 10);

            var previous = ui.CreateButton(pagination, "MentorAccountPreviousPage", "前へ", theme.SecondaryButton, () =>
            {
                mentorAccountPage = Mathf.Max(0, mentorAccountPage - 1);
                ShowMentorAccountList();
            });
            AddLayout(previous.gameObject, 0.8f, -1);
            previous.interactable = mentorAccountPage > 0;

            var firstVisible = userCount == 0 ? 0 : mentorAccountPage * pageSize + 1;
            var lastVisible = Mathf.Min(userCount, (mentorAccountPage + 1) * pageSize);
            var indicatorValue = UsesPortraitLayout()
                ? $"{mentorAccountPage + 1} / {pageCount}\n{firstVisible}–{lastVisible} / {userCount}件"
                : $"{mentorAccountPage + 1} / {pageCount} ページ  ・  {firstVisible}–{lastVisible} / {userCount}件";
            var indicator = ui.CreateText(
                pagination,
                "MentorAccountPageIndicator",
                indicatorValue,
                ResolveUiFontSize(18),
                FontStyle.Bold,
                theme.Text,
                TextAnchor.MiddleCenter);
            AddLayout(indicator.gameObject, 1.6f, -1);
            indicator.GetComponent<LayoutElement>().preferredWidth = 0f;

            var next = ui.CreateButton(pagination, "MentorAccountNextPage", "次へ", theme.SecondaryButton, () =>
            {
                mentorAccountPage = Mathf.Min(pageCount - 1, mentorAccountPage + 1);
                ShowMentorAccountList();
            });
            AddLayout(next.gameObject, 0.8f, -1);
            next.interactable = mentorAccountPage < pageCount - 1;
        }

        private void AddMentorMemberRosterSection(Transform parent)
        {
            AddText(parent, "メンバー一覧", 28, FontStyle.Bold, theme.Text, 42);
            foreach (var member in repository.Members
                         .OrderByDescending(member => repository.GetApprovedMinutesThisWeek(member.Id))
                         .ThenBy(member => member.LoginId)
                         .Take(5))
            {
                var stats = repository.GetStats(member.Id);
                var row = CreateColumn(parent, $"MentorRoster_{member.Id}", theme.StatCard, 1f);
                AddText(row, $"{member.Nickname} / {member.TeamId}", 21, FontStyle.Bold, member.IsActive ? theme.Text : theme.MutedText, 32);
                AddText(row, $"Lv.{stats.Level}  今週 {FormatMinutes(repository.GetApprovedMinutesThisWeek(member.Id))}  承認待ち {repository.GetSessionsForUser(member.Id).Count(session => session.Status is DevSessionStatus.Pending or DevSessionStatus.NeedsReview or DevSessionStatus.AiPending)}件", 18, FontStyle.Bold, theme.Cyan, 32);
            }
        }

        private void AddMentorAccountSection(Transform parent, Action refreshAction = null)
        {
            refreshAction ??= ShowMentorDashboard;
            AddText(parent, "作成内容", 28, FontStyle.Bold, theme.Text, 38);
            memberLoginIdInput = ui.CreateInput(parent, "MemberLoginIdInput", "login-id");
            memberLoginIdInput.text = retainedMemberLoginIdDraft;
            memberLoginIdInput.onValueChanged.AddListener(value => retainedMemberLoginIdDraft = value);
            AddLayout(memberLoginIdInput.gameObject, -1, 52);
            memberNicknameInput = ui.CreateInput(parent, "MemberNicknameInput", "表示名");
            memberNicknameInput.text = retainedMemberNicknameDraft;
            memberNicknameInput.onValueChanged.AddListener(value => retainedMemberNicknameDraft = value);
            AddLayout(memberNicknameInput.gameObject, -1, 52);
            memberTeamIdInput = ui.CreateInput(parent, "MemberTeamIdInput", "team: blue / magenta / mentor");
            retainedMemberTeamIdDraft ??= selectedAccountRole == UserRole.Mentor ? "mentor" : "blue";
            memberTeamIdInput.text = retainedMemberTeamIdDraft;
            memberTeamIdInput.onValueChanged.AddListener(value => retainedMemberTeamIdDraft = value);
            AddLayout(memberTeamIdInput.gameObject, -1, 52);
            AddSelectorRow(parent, new[] { UserRole.Member, UserRole.Mentor }, selectedAccountRole, value =>
            {
                selectedAccountRole = value;
                refreshAction();
            }, UserRoleLabel);
            AddSelectorRow(parent, new[] { true, false }, selectedAccountRankingVisible, value =>
            {
                selectedAccountRankingVisible = value;
                refreshAction();
            }, RankingVisibilityLabel);
            var createButton = ui.CreateButton(parent, "アカウントを発行", "アカウントを発行", theme.PrimaryButton, () =>
            {
                if (TryCreateRemoteAccount(memberLoginIdInput.text, memberNicknameInput.text, selectedAccountRole, memberTeamIdInput.text, selectedAccountRankingVisible, refreshAction))
                {
                    return;
                }

                try
                {
                    var result = repository.CreateUserAccount(currentUser.Id, memberLoginIdInput.text, memberNicknameInput.text, selectedAccountRole, memberTeamIdInput.text, selectedAccountRankingVisible);
                    PersistRuntimeSnapshot();
                    ClearRetainedMentorAccountDrafts();
                    SetPendingTemporaryPasswordReveal(result.User.Nickname, result.TemporaryPassword);
                    SetMentorFeedback($"{result.User.Nickname} を発行しました。初回パスワードは明示操作で確認してください。", FeedbackTone.Success);
                }
                catch (Exception exception)
                {
                    SetMentorFeedback(exception.Message, FeedbackTone.Danger);
                }

                refreshAction();
            });
            AddLayout(createButton.gameObject, -1, 58);
            var listButton = ui.CreateButton(parent, "発行済みを見る", "発行済みを見る", theme.SecondaryButton, ShowMentorAccountList);
            AddLayout(listButton.gameObject, -1, 48);
        }

        private void AddMemberAccountSummary(Transform parent, UserProfile member, Action refreshAction)
        {
            var summary = CreateColumn(parent, $"MemberAccount_{member.Id}", theme.StatCard, 1f);
            AddText(summary, $"{member.Nickname} / {member.LoginId}", 20, FontStyle.Bold, member.IsActive ? theme.Text : theme.MutedText, 32);
            AddText(summary, $"{UserRoleLabel(member.Role)} / {member.TeamId} / {RankingVisibilityLabel(member.RankingVisible)} / {InitialPasswordStateLabel(member)}", 18, FontStyle.Bold, member.InitialPasswordChanged ? theme.Cyan : theme.Magenta, 32);
            AddButton(summary, "一時PW再発行", theme.SecondaryButton, () =>
            {
                if (TryIssueRemoteTemporaryPassword(member.Id, member.Nickname, ShowMentorAccounts))
                {
                    return;
                }

                try
                {
                    var result = repository.IssueTemporaryPassword(currentUser.Id, member.Id);
                    PersistRuntimeSnapshot();
                    SetPendingTemporaryPasswordReveal(result.User.Nickname, result.TemporaryPassword);
                    SetMentorFeedback($"{result.User.Nickname} の初回パスワードを再発行しました。明示操作で確認してください。", FeedbackTone.Success);
                }
                catch (Exception exception)
                {
                    SetMentorFeedback(exception.Message, FeedbackTone.Danger);
                }

                ShowMentorAccounts();
            });
        }

        private bool TryCreateRemoteAccount(string loginId, string nickname, UserRole role, string teamId, bool rankingVisible, Action refreshAction = null)
        {
            refreshAction ??= ShowMentorDashboard;
            if (supabase is not { IsConfigured: true })
            {
                return HandleUnavailableAuthoritativeBackend();
            }

            if (!supabase.HasSession)
            {
                SetMentorFeedback("セッション期限切れです。再ログインしてください。", FeedbackTone.Warning);
                ShowLogin();
                return true;
            }

            if (isNetworkBusy)
            {
                SetMentorFeedback("通信中です。アカウント発行の完了を待ってください。", FeedbackTone.Waiting);
                refreshAction();
                return true;
            }

            StartCoroutine(CreateRemoteAccount(loginId, nickname, role, teamId, rankingVisible, refreshAction));
            return true;
        }

        private IEnumerator CreateRemoteAccount(string loginId, string nickname, UserRole role, string teamId, bool rankingVisible, Action refreshAction)
        {
            isNetworkBusy = true;
            SupabaseGameApiResponseDto response = null;
            yield return supabase.CreateAccount(loginId, nickname, role, teamId, rankingVisible, result => response = result);
            isNetworkBusy = false;
            if (ReturnToLoginIfRemoteSessionExpired(response))
            {
                yield break;
            }

            if (response?.Ok == true)
            {
                ApplyRemoteSnapshot(response);
                var user = response.User?.ToDomain();
                ClearRetainedMentorAccountDrafts();
                if (string.IsNullOrWhiteSpace(response.TemporaryPassword))
                {
                    ClearTemporaryPasswordReveal();
                    SetMentorFeedback($"{user?.Nickname ?? "アカウント"} は発行済みですが、初回パスワードを取得できませんでした。発行済み一覧から再発行してください。", FeedbackTone.Warning);
                }
                else
                {
                    SetPendingTemporaryPasswordReveal(user?.Nickname ?? "アカウント", response.TemporaryPassword);
                    SetMentorFeedback($"{user?.Nickname ?? "アカウント"} を発行しました。初回パスワードは明示操作で確認してください。", FeedbackTone.Success);
                }
            }
            else
            {
                SetMentorFeedback(RemoteErrorMessage("発行できませんでした。入力内容と通信状態を確認してください。"), FeedbackTone.Danger);
            }

            refreshAction();
        }

        private bool TryIssueRemoteTemporaryPassword(string userId, string nickname, Action refreshAction = null)
        {
            refreshAction ??= ShowMentorDashboard;
            if (supabase is not { IsConfigured: true })
            {
                return HandleUnavailableAuthoritativeBackend();
            }

            if (!supabase.HasSession)
            {
                SetMentorFeedback("セッション期限切れです。再ログインしてください。", FeedbackTone.Warning);
                ShowLogin();
                return true;
            }

            if (isNetworkBusy)
            {
                SetMentorFeedback("通信中です。パスワード再発行の完了を待ってください。", FeedbackTone.Waiting);
                refreshAction();
                return true;
            }

            StartCoroutine(IssueRemoteTemporaryPassword(userId, nickname, refreshAction));
            return true;
        }

        private IEnumerator IssueRemoteTemporaryPassword(string userId, string nickname, Action refreshAction)
        {
            isNetworkBusy = true;
            SupabaseGameApiResponseDto response = null;
            yield return supabase.IssueTemporaryPassword(userId, result => response = result);
            isNetworkBusy = false;
            if (ReturnToLoginIfRemoteSessionExpired(response))
            {
                yield break;
            }

            if (response?.Ok == true)
            {
                ApplyRemoteSnapshot(response);
                var user = response.User?.ToDomain();
                if (string.IsNullOrWhiteSpace(response.TemporaryPassword))
                {
                    ClearTemporaryPasswordReveal();
                    SetMentorFeedback($"{user?.Nickname ?? nickname} の初回パスワード応答を確認できませんでした。再発行をやり直してください。", FeedbackTone.Danger);
                }
                else
                {
                    SetPendingTemporaryPasswordReveal(user?.Nickname ?? nickname, response.TemporaryPassword);
                    SetMentorFeedback($"{user?.Nickname ?? nickname} の初回パスワードを再発行しました。明示操作で確認してください。", FeedbackTone.Success);
                }
            }
            else
            {
                SetMentorFeedback(RemoteErrorMessage("再発行できませんでした。権限と通信状態を確認してください。"), FeedbackTone.Danger);
            }

            refreshAction();
        }

        private void AddRecentAuditLogSection(Transform parent)
        {
            AddText(parent, "監査ログ", 28, FontStyle.Bold, theme.Text, 42);
            var logs = repository.GetRecentAuditLogs(6);
            if (logs.Count == 0)
            {
                AddText(parent, "まだ監査ログはありません。", 22, FontStyle.Bold, theme.MutedText, 36);
                return;
            }

            foreach (var log in logs)
            {
                AddText(parent, BuildAuditLogLine(log), 18, FontStyle.Normal, theme.MutedText, 34);
            }
        }

        private void AddAchievementSummary(Transform parent, AchievementEntry achievement, bool mentorControls)
        {
            if (UsesPortraitLayout() && mentorControls && achievement.Status == AchievementStatus.Pending)
            {
                AddPortraitPendingAchievementSummary(parent, achievement);
                return;
            }

            var summaryHeight = achievement.Status == AchievementStatus.Approved
                ? 150f
                : mentorControls && achievement.Status == AchievementStatus.Pending
                    ? 132f
                    : 116f;
            var summary = ui.CreatePanel(parent, $"Achievement_{achievement.Id}", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(summary.gameObject, -1, summaryHeight);
            AddHorizontal(summary, 14, 12);
            AddCatalogIcon(summary, $"AchievementIcon_{achievement.Id}", theme.AchievementIcon ?? theme.HexBadge, theme.Cyan);

            var body = new GameObject("AchievementBody", typeof(RectTransform));
            body.transform.SetParent(summary, false);
            AddLayout(body, 1, -1);
            AddVertical(body.GetComponent<RectTransform>(), 0, 4);

            var user = repository.Users.FirstOrDefault(item => item.Id == achievement.UserId);
            var statusColor = achievement.Status == AchievementStatus.Approved
                ? theme.Cyan
                : achievement.Status == AchievementStatus.Rejected
                    ? theme.Danger
                    : theme.Warning;
            AddText(body.transform, $"{user?.Nickname ?? "不明"} / {AchievementTypeLabel(achievement.Type)} / {AchievementStatusLabel(achievement.Status)}", 17, FontStyle.Bold, statusColor, 24);
            AddText(body.transform, achievement.Title, 22, FontStyle.Bold, theme.Text, 30);
            if (!string.IsNullOrWhiteSpace(achievement.Description))
            {
                AddText(body.transform, Shorten(achievement.Description, 24), 17, FontStyle.Normal, theme.MutedText, 24);
            }

            if (achievement.Status == AchievementStatus.Approved)
            {
                var rewardWeapon = achievement.HasRewardWeapon ? $" / {WeaponLabel(achievement.RewardWeapon)}" : string.Empty;
                AddText(body.transform, Shorten($"報酬: {achievement.RewardTitle} / {achievement.RewardSkill}{rewardWeapon}", 58), 17, FontStyle.Bold, theme.Cyan, 24);
            }

            if (!mentorControls || achievement.Status != AchievementStatus.Pending)
            {
                return;
            }

            var actions = new GameObject("AchievementActions", typeof(RectTransform));
            actions.transform.SetParent(summary, false);
            AddLayout(actions, 128, -1);
            AddVertical(actions.GetComponent<RectTransform>(), 0, 8, TextAnchor.MiddleCenter);
            var approve = ui.CreateButton(actions.transform, "ApproveAchievement", "承認", theme.PrimaryButton, () =>
            {
                if (TryReviewRemoteAchievement(achievement.Id, true, achievement.Title))
                {
                    return;
                }

                repository.ApproveAchievement(achievement.Id, currentUser.Id);
                SetAchievementFeedback($"{achievement.Title} を承認し、報酬を付与しました。", FeedbackTone.Success);
                ShowAchievements();
            });
            AddLayout(approve.gameObject, -1, 46);
            var reject = ui.CreateButton(actions.transform, "RejectAchievement", "却下", theme.DangerButton, () =>
            {
                if (TryReviewRemoteAchievement(achievement.Id, false, achievement.Title))
                {
                    return;
                }

                repository.RejectAchievement(achievement.Id, currentUser.Id);
                SetAchievementFeedback($"{achievement.Title} を却下しました。", FeedbackTone.Warning);
                ShowAchievements();
            });
            AddLayout(reject.gameObject, -1, 46);
        }

        private void AddPortraitPendingAchievementSummary(Transform parent, AchievementEntry achievement)
        {
            var summary = ui.CreatePanel(parent, $"Achievement_{achievement.Id}", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(summary.gameObject, -1, 180f);
            AddVertical(summary, 12, 8);

            var infoRow = new GameObject("AchievementInfo", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            infoRow.transform.SetParent(summary, false);
            AddLayout(infoRow, -1, 102f);
            AddHorizontal(infoRow.GetComponent<RectTransform>(), 0, 10);
            AddCatalogIcon(infoRow.transform, $"AchievementIcon_{achievement.Id}", theme.AchievementIcon ?? theme.HexBadge, theme.Cyan);

            var body = new GameObject("AchievementBody", typeof(RectTransform));
            body.transform.SetParent(infoRow.transform, false);
            AddLayout(body, 1f, -1f);
            AddVertical(body.GetComponent<RectTransform>(), 0, 4);

            var user = repository.Users.FirstOrDefault(item => item.Id == achievement.UserId);
            AddText(
                body.transform,
                $"{Shorten(user?.Nickname ?? "不明", 7)} / {AchievementTypeLabel(achievement.Type)} / {AchievementStatusLabel(achievement.Status)}",
                15,
                FontStyle.Bold,
                theme.Warning,
                22f);
            AddText(body.transform, Shorten(achievement.Title, 12), 20, FontStyle.Bold, theme.Text, 30f);
            if (!string.IsNullOrWhiteSpace(achievement.Description))
            {
                AddText(body.transform, Shorten(achievement.Description, 16), 15, FontStyle.Normal, theme.MutedText, 24f);
            }

            // Put the decision pair on its own full-width rail.  The previous fixed
            // side column became narrower than a finger target on 390 px portrait.
            var actions = CreateHudRow(summary, "AchievementActions", 56f);
            var approve = ui.CreateButton(actions, "ApproveAchievement", "承認", theme.PrimaryButton, () =>
            {
                if (TryReviewRemoteAchievement(achievement.Id, true, achievement.Title))
                {
                    return;
                }

                repository.ApproveAchievement(achievement.Id, currentUser.Id);
                SetAchievementFeedback($"{achievement.Title} を承認し、報酬を付与しました。", FeedbackTone.Success);
                ShowAchievements();
            });
            AddLayout(approve.gameObject, 1f, -1f);

            var reject = ui.CreateButton(actions, "RejectAchievement", "却下", theme.DangerButton, () =>
            {
                if (TryReviewRemoteAchievement(achievement.Id, false, achievement.Title))
                {
                    return;
                }

                repository.RejectAchievement(achievement.Id, currentUser.Id);
                SetAchievementFeedback($"{achievement.Title} を却下しました。", FeedbackTone.Warning);
                ShowAchievements();
            });
            AddLayout(reject.gameObject, 1f, -1f);
        }

        private void AddProductSummary(Transform parent, ProductEntry product, bool mentorControls)
        {
            if (UsesPortraitLayout())
            {
                AddPortraitProductSummary(parent, product, mentorControls);
                return;
            }

            var summary = ui.CreatePanel(parent, $"Product_{product.Id}", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(summary.gameObject, -1, string.IsNullOrWhiteSpace(product.Description) ? 124f : 150f);
            AddHorizontal(summary, 14, 12);
            AddCatalogIcon(summary, $"ProductIcon_{product.Id}", theme.ProductIcon ?? theme.HexBadge, product.IsPublic ? theme.Cyan : theme.MutedText);

            var body = new GameObject("ProductBody", typeof(RectTransform));
            body.transform.SetParent(summary, false);
            AddLayout(body, 1, -1);
            AddVertical(body.GetComponent<RectTransform>(), 0, 4);

            var owner = repository.Users.FirstOrDefault(user => user.Id == product.UserId);
            var postedAt = product.CreatedAtUtc == default ? string.Empty : $" / {product.CreatedAtUtc.ToLocalTime():M/d HH:mm}";
            var status = product.IsPublic ? "公開中" : "非公開";
            AddText(body.transform, Shorten(product.Title, 20), 22, FontStyle.Bold, product.IsPublic ? theme.Text : theme.MutedText, 30);
            AddText(body.transform, $"{Shorten(owner?.Nickname ?? "不明", 8)} / {status}{postedAt}", 17, FontStyle.Bold, product.IsPublic ? theme.Cyan : theme.MutedText, 24);
            AddText(body.transform, Shorten(product.Url, 64), 17, FontStyle.Normal, theme.Cyan, 24);
            if (!string.IsNullOrWhiteSpace(product.Description))
            {
                AddText(body.transform, Shorten(product.Description, 26), 16, FontStyle.Normal, theme.MutedText, 24);
            }

            var normalizedProductUrl = string.Empty;
            var canOpenProduct = product.IsPublic && TryNormalizeExternalUrl(product.Url, out normalizedProductUrl);
            if (!canOpenProduct && !mentorControls)
            {
                return;
            }

            var actions = new GameObject("ProductActions", typeof(RectTransform));
            actions.transform.SetParent(summary, false);
            AddLayout(actions, ResolveMinimumUiLength(156f, 112f), -1);
            AddVertical(actions.GetComponent<RectTransform>(), 0, 8, TextAnchor.MiddleCenter);

            if (canOpenProduct)
            {
                var openButton = ui.CreateButton(actions.transform, "OpenProduct", "作品を開く", theme.SecondaryButton, () => ExternalUrlLauncher.TryOpen(normalizedProductUrl));
                AddLayout(openButton.gameObject, -1, 46f);
            }

            if (!mentorControls)
            {
                return;
            }

            if (product.IsPublic)
            {
                var hideButton = ui.CreateButton(actions.transform, "HideProduct", "非表示", theme.DangerButton, () =>
                {
                    if (TryHideRemoteProduct(product.Id, product.Title))
                    {
                        return;
                    }

                    repository.HideProduct(product.Id, currentUser.Id);
                    SetProductFeedback($"{product.Title} を非表示にしました。", FeedbackTone.Warning);
                    ShowProducts();
                });
                AddLayout(hideButton.gameObject, -1, 46f);
                return;
            }

            var hiddenBy = repository.Users.FirstOrDefault(user => user.Id == product.HiddenBy);
            var hiddenText = ui.CreateText(actions.transform, "HiddenByMentor", $"非表示\n{hiddenBy?.Nickname ?? "不明"}", 16, FontStyle.Bold, theme.Magenta, TextAnchor.MiddleCenter);
            AddLayout(hiddenText.gameObject, -1, 58f);
        }

        private void AddPortraitProductSummary(Transform parent, ProductEntry product, bool mentorControls)
        {
            var hasDescription = !string.IsNullOrWhiteSpace(product.Description);
            var normalizedProductUrl = string.Empty;
            var canOpenProduct = product.IsPublic && TryNormalizeExternalUrl(product.Url, out normalizedProductUrl);
            var hasActions = canOpenProduct || mentorControls;
            var summaryHeight = hasActions ? (hasDescription ? 248f : 224f) : (hasDescription ? 190f : 166f);
            var summary = ui.CreatePanel(parent, $"Product_{product.Id}", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(summary.gameObject, -1, summaryHeight);
            AddVertical(summary, 14, 10);

            var info = new GameObject("ProductInfo", typeof(RectTransform));
            info.transform.SetParent(summary, false);
            AddLayout(info, -1, hasDescription ? 154f : 130f);
            AddHorizontal(info.GetComponent<RectTransform>(), 0, 10);
            AddCatalogIcon(info.transform, $"ProductIcon_{product.Id}", theme.ProductIcon ?? theme.HexBadge, product.IsPublic ? theme.Cyan : theme.MutedText);

            var body = new GameObject("ProductBody", typeof(RectTransform));
            body.transform.SetParent(info.transform, false);
            AddLayout(body, 1f, -1);
            AddVertical(body.GetComponent<RectTransform>(), 0, 4);

            var owner = repository.Users.FirstOrDefault(user => user.Id == product.UserId);
            var postedAt = product.CreatedAtUtc == default ? string.Empty : $" / {product.CreatedAtUtc.ToLocalTime():M/d HH:mm}";
            var status = product.IsPublic ? "公開中" : "非公開";
            AddText(body.transform, Shorten(product.Title, 20), 22, FontStyle.Bold, product.IsPublic ? theme.Text : theme.MutedText, 34);
            AddText(body.transform, $"{Shorten(owner?.Nickname ?? "不明", 8)} / {status}{postedAt}", 17, FontStyle.Bold, product.IsPublic ? theme.Cyan : theme.MutedText, 28);
            AddText(body.transform, Shorten(product.Url, 42), 16, FontStyle.Normal, theme.Cyan, 28);
            if (hasDescription)
            {
                AddText(body.transform, Shorten(product.Description, 26), 16, FontStyle.Normal, theme.MutedText, 30);
            }

            if (!hasActions)
            {
                return;
            }

            var actions = new GameObject("ProductActions", typeof(RectTransform));
            actions.transform.SetParent(summary, false);
            AddLayout(actions, -1, 52f);
            AddHorizontal(actions.GetComponent<RectTransform>(), 0, 10);

            if (canOpenProduct)
            {
                var openButton = ui.CreateButton(actions.transform, "OpenProduct", "作品を開く", theme.SecondaryButton, () => ExternalUrlLauncher.TryOpen(normalizedProductUrl));
                AddLayout(openButton.gameObject, 1f, -1);
            }

            if (!mentorControls)
            {
                return;
            }

            if (product.IsPublic)
            {
                var hideButton = ui.CreateButton(actions.transform, "HideProduct", "非表示", theme.DangerButton, () =>
                {
                    if (TryHideRemoteProduct(product.Id, product.Title))
                    {
                        return;
                    }

                    repository.HideProduct(product.Id, currentUser.Id);
                    SetProductFeedback($"{product.Title} を非表示にしました。", FeedbackTone.Warning);
                    ShowProducts();
                });
                AddLayout(hideButton.gameObject, 1f, -1);
                return;
            }

            var hiddenBy = repository.Users.FirstOrDefault(user => user.Id == product.HiddenBy);
            var hiddenText = ui.CreateText(actions.transform, "HiddenByMentor", $"非表示: {hiddenBy?.Nickname ?? "不明"}", 16, FontStyle.Bold, theme.Magenta, TextAnchor.MiddleCenter);
            AddLayout(hiddenText.gameObject, 1f, -1);
        }

        private static bool TryNormalizeExternalUrl(string value, out string normalized)
        {
            return RuntimeUrlSecurity.TryNormalizeExternalHttpsUrl(value, out normalized);
        }

        private void ShowRanking()
        {
            ClearRoot();
            AddHeader("ランキング", string.Empty, ShowMemberHome);
            var useFullWidth = UsesPortraitLayout() || UsesCompactLandscapeLayout();
            var scroll = CreateScrollPanel(
                root,
                "RankingScroll",
                new Vector2(useFullWidth ? 0.06f : 0.43f, 0.06f),
                new Vector2(useFullWidth ? 0.94f : 0.96f, 0.82f));
            var panel = CreateDashboardSection(scroll, "RankingPanel", RankingPanelHeight(), theme.RaidPanel);
            AddVertical(panel, 22, 14);
            var rankingTitle = AddDisplayText(panel, $"{RankingKindLabel(selectedRankingKind)}ランキング", UsesConstrainedLayout() ? 30 : 38, theme.Text, UsesConstrainedLayout() ? 50 : 60, TextAnchor.MiddleCenter);
            PreserveFullSingleLineLabel(rankingTitle, UsesConstrainedLayout() ? 23 : 29);
            AddSelectorRow(panel, Enum.GetValues(typeof(RankingKind)).Cast<RankingKind>(), selectedRankingKind, value =>
            {
                selectedRankingKind = value;
                ShowRanking();
            }, RankingKindLabel);
            if (selectedRankingKind == RankingKind.DevelopmentTime)
            {
                AddSelectorRow(panel, Enum.GetValues(typeof(RankingView)).Cast<RankingView>(), selectedRankingView, value =>
                {
                    selectedRankingView = value;
                    ShowRanking();
                }, RankingViewLabel);
            }

            AddSelectorRow(panel, Enum.GetValues(typeof(RankingPeriod)).Cast<RankingPeriod>(), selectedRankingPeriod, value =>
            {
                selectedRankingPeriod = value;
                ShowRanking();
            }, RankingPeriodLabel);

            if (selectedRankingKind == RankingKind.BattleDamage)
            {
                AddBattleDamageRanking(panel);
                return;
            }

            AddDevelopmentTimeRanking(panel);
        }

        private float RankingPanelHeight()
        {
            var selectorRows = selectedRankingKind == RankingKind.DevelopmentTime ? 3 : 2;
            var entryRows = Mathf.Max(1, RankingEntryCount());
            var childSpacingCount = selectorRows + entryRows + 1;
            return Mathf.Max(
                520f,
                44f + 60f + selectorRows * 70f + 42f + entryRows * 46f + childSpacingCount * 14f);
        }

        private int RankingEntryCount()
        {
            if (selectedRankingKind == RankingKind.BattleDamage)
            {
                return repository.GetBattleDamageRanking(selectedRankingPeriod).Take(12).Count();
            }

            if (selectedRankingView == RankingView.Team)
            {
                return repository.GetTeamDevelopmentTimeRanking(selectedRankingPeriod).Take(12).Count();
            }

            var entries = selectedRankingView == RankingView.TeamMember
                ? repository.GetTeamMemberDevelopmentTimeRanking(currentUser?.TeamId, selectedRankingPeriod)
                : repository.GetDevelopmentTimeRanking(selectedRankingPeriod);
            return entries.Take(12).Count();
        }

        private void AddDevelopmentTimeRanking(Transform panel)
        {
            var scopeLabel = selectedRankingView == RankingView.TeamMember
                ? LocalGameRepository.GetTeamDisplayName(currentUser?.TeamId)
                : RankingViewLabel(selectedRankingView);
            var scopeCopy = UsesConstrainedLayout()
                ? $"{scopeLabel} / {RankingPeriodLabel(selectedRankingPeriod)} / 承認済み"
                : $"{scopeLabel} / {RankingPeriodLabel(selectedRankingPeriod)} / 承認済みログのみ";
            var scope = AddText(panel, scopeCopy, UsesConstrainedLayout() ? 19 : 24, FontStyle.Bold, theme.Cyan, 42, TextAnchor.MiddleCenter);
            PreserveFullSingleLineLabel(scope, UsesConstrainedLayout() ? 15 : 18);
            if (selectedRankingView == RankingView.Team)
            {
                AddTeamRankingEntries(panel, repository.GetTeamDevelopmentTimeRanking(selectedRankingPeriod));
                return;
            }

            var entries = selectedRankingView == RankingView.TeamMember
                ? repository.GetTeamMemberDevelopmentTimeRanking(currentUser?.TeamId, selectedRankingPeriod)
                : repository.GetDevelopmentTimeRanking(selectedRankingPeriod);
            AddDevelopmentTimeRankingEntries(panel, entries);
        }

        private void AddDevelopmentTimeRankingEntries(Transform panel, IReadOnlyList<DevelopmentTimeRankingEntry> entries)
        {
            var rank = 1;
            if (entries.Count == 0)
            {
                AddText(panel, "表示できる承認済みログはありません。", 24, FontStyle.Bold, theme.MutedText, 46, TextAnchor.MiddleCenter);
                return;
            }

            foreach (var entry in entries.Take(12))
            {
                AddText(panel, $"{rank}. {Shorten(entry.Nickname, UsesConstrainedLayout() ? 12 : 20)}    {FormatMinutes(entry.DurationMinutes)}    {entry.SessionCount}件", UsesConstrainedLayout() ? 21 : 28, FontStyle.Bold, rank == 1 ? theme.Magenta : theme.Text, 46);
                rank += 1;
            }
        }

        private void AddTeamRankingEntries(Transform panel, IReadOnlyList<TeamDevelopmentTimeRankingEntry> entries)
        {
            var rank = 1;
            if (entries.Count == 0)
            {
                AddText(panel, "表示できる班別の承認済みログはありません。", 24, FontStyle.Bold, theme.MutedText, 46, TextAnchor.MiddleCenter);
                return;
            }

            foreach (var entry in entries.Take(12))
            {
                AddText(panel, $"{rank}. {Shorten(entry.TeamName, UsesConstrainedLayout() ? 12 : 20)}    {FormatMinutes(entry.DurationMinutes)}    {entry.MemberCount}人 / {entry.SessionCount}件", UsesConstrainedLayout() ? 21 : 28, FontStyle.Bold, rank == 1 ? theme.Magenta : theme.Text, 46);
                rank += 1;
            }
        }

        private void AddBattleDamageRanking(Transform panel)
        {
            AddText(panel, $"{RankingPeriodLabel(selectedRankingPeriod)} / ボス戦ダメージ", UsesConstrainedLayout() ? 19 : 24, FontStyle.Bold, theme.Cyan, 42, TextAnchor.MiddleCenter);
            var rank = 1;
            var entries = repository.GetBattleDamageRanking(selectedRankingPeriod);
            if (entries.Count == 0)
            {
                AddText(panel, "表示できるボス戦ダメージはありません。", 24, FontStyle.Bold, theme.MutedText, 46, TextAnchor.MiddleCenter);
                return;
            }

            foreach (var entry in entries.Take(12))
            {
                AddText(panel, $"{rank}. {entry.Nickname}    ダメージ {entry.Damage:N0}    開発 {FormatMinutes(entry.ApprovedMinutes)}", 28, FontStyle.Bold, rank == 1 ? theme.Magenta : theme.Text, 46);
                rank += 1;
            }
        }

        private void AddActionGrid(Transform parent, IReadOnlyList<BattleMemberActionOption> options, int columnCount = 3)
        {
            var grid = new GameObject("BattleActionGrid", typeof(RectTransform), typeof(VerticalLayoutGroup));
            grid.transform.SetParent(parent, false);
            var resolvedColumnCount = Mathf.Max(1, columnCount);
            var rowCount = Mathf.Max(1, Mathf.CeilToInt(options.Count / (float)resolvedColumnCount));
            var rowHeight = ResolveMinimumUiLength(48f, 44f);
            var gridHeight = rowCount * rowHeight + Mathf.Max(0, rowCount - 1) * 5f;
            AddLayout(grid, -1, gridHeight);
            var gridLayout = grid.GetComponent<VerticalLayoutGroup>();
            gridLayout.spacing = 5;
            gridLayout.childControlWidth = true;
            gridLayout.childControlHeight = true;
            gridLayout.childForceExpandWidth = true;
            gridLayout.childForceExpandHeight = true;

            for (var start = 0; start < options.Count; start += resolvedColumnCount)
            {
                var row = new GameObject($"BattleActionRow_{start / resolvedColumnCount}", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                row.transform.SetParent(grid.transform, false);
                AddLayout(row, -1, rowHeight);
                var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
                rowLayout.spacing = 5;
                rowLayout.childControlWidth = true;
                rowLayout.childControlHeight = true;
                rowLayout.childForceExpandWidth = true;
                rowLayout.childForceExpandHeight = true;

                var rowOptions = options.Skip(start).Take(resolvedColumnCount).ToList();
                foreach (var option in rowOptions)
                {
                    AddActionGridButton(row.transform, option);
                }

                AddFlexibleGridSpacers(row.transform, resolvedColumnCount - rowOptions.Count);
            }
        }

        private void AddActionGridButton(Transform parent, BattleMemberActionOption option)
        {
            var label = BattleActionGridLabel(option);
            var sprite = option.ActionType switch
            {
                BattleActionType.FullPower => theme.DangerButton,
                BattleActionType.Support or BattleActionType.Guard => theme.SecondaryButton,
                _ => theme.PrimaryButton
            };
            var labelColor = option.ActionType is BattleActionType.Support or BattleActionType.Guard ? theme.Cyan : theme.Text;
            var button = AddBattleButtonCell(parent, $"Action_{option.ActionType}", label, sprite, () =>
            {
                ExecuteBattleAction(option.ActionType);
            // Action cells can contain a second MP line.  Fourteen rendered pixels
            // remains readable while fitting two lines inside the 44 px target.
            }, option.IsAvailable ? labelColor : theme.MutedText, 12);
            button.interactable = option.IsAvailable;
        }

        private void ExecuteBattleAction(BattleActionType actionType)
        {
            if (TrySubmitRemoteBattleAction(actionType))
            {
                return;
            }

            var result = repository.SubmitBattleAction(currentUser.Id, selectedRole, selectedWeapon, actionType);
            battleCommandDeckExpanded = false;
            battleStateExpanded = true;
            SetBattleFeedback(result.Message, BattleFeedbackTone(result));
            StartCoroutine(battleController.PlayAction(result));
            ShowBattle();
        }

        private static string BattleActionGridLabel(BattleMemberActionOption option)
        {
            var action = option.ActionType switch
            {
                BattleActionType.Strong => "強攻",
                BattleActionType.FullPower => "全力",
                BattleActionType.Support => "支援",
                BattleActionType.Guard => "ガード",
                _ => "通常"
            };
            if (!option.IsAvailable)
            {
                return $"{action}\nMP不足";
            }

            return option.MpCost > 0 ? $"{action}\nMP{option.MpCost}" : action;
        }

        private void AddActionButton(Transform parent, BattleMemberActionOption option)
        {
            var label = option.IsAvailable ? option.Label : $"{option.Label} / MP不足";
            var button = AddButton(parent, label, option.ActionType == BattleActionType.FullPower ? theme.DangerButton : theme.PrimaryButton, () =>
            {
                if (TrySubmitRemoteBattleAction(option.ActionType))
                {
                    return;
                }

                var result = repository.SubmitBattleAction(currentUser.Id, selectedRole, selectedWeapon, option.ActionType);
                battleCommandDeckExpanded = false;
                battleStateExpanded = true;
                SetBattleFeedback(result.Message, BattleFeedbackTone(result));
                StartCoroutine(battleController.PlayAction(result));
                ShowBattle();
            });
            button.interactable = option.IsAvailable;
        }

        private bool TrySubmitRemoteBattleAction(BattleActionType actionType)
        {
            if (supabase is not { IsConfigured: true })
            {
                return HandleUnavailableAuthoritativeBackend();
            }

            if (!supabase.HasSession)
            {
                SetBattleFeedback("セッション期限切れです。再ログインしてください。", FeedbackTone.Warning);
                ShowLogin();
                return true;
            }

            if (isNetworkBusy)
            {
                SetBattleFeedback("通信中です。前の行動結果を待ってください。", FeedbackTone.Waiting);
                ShowBattle();
                return true;
            }

            StartCoroutine(SubmitRemoteBattleAction(actionType));
            return true;
        }

        private IEnumerator SubmitRemoteBattleAction(BattleActionType actionType)
        {
            var operationUserId = currentUser?.Id;
            if (string.IsNullOrWhiteSpace(operationUserId))
            {
                loginErrorMessage = "再ログインしてから行動を選んでください。";
                ShowLogin();
                yield break;
            }

            var pending = BattleActionPendingOperationStore.GetOrCreate(
                operationUserId,
                selectedRole,
                selectedWeapon,
                actionType);
            isNetworkBusy = true;
            BattleActionResponseDto response = null;
            yield return supabase.SubmitBattleAction(
                pending.Role,
                pending.Weapon,
                pending.ActionType,
                pending.IdempotencyKey,
                result => response = result);
            isNetworkBusy = false;
            if (ReturnToLoginIfRemoteSessionExpired(response))
            {
                yield break;
            }

            if (response?.Ok == true && response.ActionResult != null && response.BattleDelta != null)
            {
                BattleActionPendingOperationStore.Complete(operationUserId, pending.IdempotencyKey);
                var actionResult = response.ActionResult.ToDomain();
                ApplyRemoteBattleDelta(response);
                if (actionResult != null)
                {
                    SetBattleFeedback(actionResult.Message, BattleFeedbackTone(actionResult));
                    StartCoroutine(battleController.PlayAction(actionResult));
                }
            }
            else
            {
                var errorCode = response?.ErrorCode ?? response?.Error;
                if (BattleActionPendingOperationStore.IsDefinitiveNonCommitFailure(errorCode))
                {
                    BattleActionPendingOperationStore.Complete(operationUserId, pending.IdempotencyKey);
                    SetBattleFeedback("行動は受理されませんでした。最新状態を確認して選び直してください。", FeedbackTone.Warning);
                }
                else
                {
                    SetBattleFeedback(
                        RemoteErrorMessage("行動結果を確認できませんでした。同じ行動として再確認できます。"),
                        FeedbackTone.Danger);
                }
            }

            battleCommandDeckExpanded = false;
            battleStateExpanded = true;
            ShowBattle();
        }

        private bool TryStartRemoteBattle(Action afterStart)
        {
            if (supabase is not { IsConfigured: true })
            {
                return HandleUnavailableAuthoritativeBackend();
            }

            if (!supabase.HasSession)
            {
                SetBattleFeedback("セッション期限切れです。再ログインしてください。", FeedbackTone.Warning);
                ShowLogin();
                return true;
            }

            if (isNetworkBusy)
            {
                SetBattleFeedback("通信中です。開始処理の完了を待ってください。", FeedbackTone.Waiting);
                afterStart?.Invoke();
                return true;
            }

            StartCoroutine(StartRemoteBattle(afterStart));
            return true;
        }

        private IEnumerator StartRemoteBattle(Action afterStart)
        {
            isNetworkBusy = true;
            SupabaseGameApiResponseDto response = null;
            yield return supabase.StartBattle(result => response = result);
            isNetworkBusy = false;
            if (ReturnToLoginIfRemoteSessionExpired(response))
            {
                yield break;
            }

            if (response?.Ok == true)
            {
                ApplyRemoteBattleDelta(response);
                battleController.SetControlledParticipant(null);
                SetBattleFeedback("ボス戦開始", FeedbackTone.Battle);
                afterStart?.Invoke();
                yield break;
            }
            else
            {
                SetBattleFeedback(RemoteErrorMessage("通信できませんでした。開始状態を確認してください。"), FeedbackTone.Danger);
            }

            ShowMentorDashboard();
        }

        private bool TryRefreshRemoteSnapshot(Action afterRefresh, bool useFrontDisplaySnapshot = false)
        {
            if (supabase is not { IsConfigured: true })
            {
                return HandleUnavailableAuthoritativeBackend();
            }

            if (!useFrontDisplaySnapshot && !supabase.HasSession)
            {
                loginErrorMessage = "セッション期限切れです。再ログインしてください。";
                ShowLogin();
                return true;
            }

            if (isNetworkBusy)
            {
                return true;
            }

            StartCoroutine(RefreshRemoteSnapshot(afterRefresh, useFrontDisplaySnapshot));
            return true;
        }

        private IEnumerator RefreshRemoteSnapshot(Action afterRefresh, bool useFrontDisplaySnapshot = false)
        {
            isNetworkBusy = true;
            SupabaseGameApiResponseDto response = null;
            yield return useFrontDisplaySnapshot
                ? supabase.GetFrontDisplaySnapshot(result => response = result)
                : supabase.GetSnapshot(result => response = result);
            isNetworkBusy = false;
            if (!useFrontDisplaySnapshot && ReturnToLoginIfRemoteSessionExpired(response))
            {
                yield break;
            }

            if (response?.Ok == true)
            {
                if (ShouldUseSeparateFrontDisplayRepository(
                        useFrontDisplaySnapshot,
                        IsActiveProductionScene(RasshiineProductionScene.FrontDisplay)))
                {
                    // The projector preview uses its own public-only cache. Never merge
                    // it into the authenticated repository and never render private
                    // role-aware records on a screen intended for the room.
                    ApplyRemoteFrontDisplayProjection(response);
                }
                else
                {
                    ApplyRemoteSnapshot(response);
                }
            }
            else if (useFrontDisplaySnapshot)
            {
                SetBattleFeedback(RemoteErrorMessage("前面表示を更新できませんでした。通信状態を確認してください。"), FeedbackTone.Warning);
            }

            afterRefresh?.Invoke();
        }

        private static bool ShouldUseSeparateFrontDisplayRepository(
            bool frontDisplayRequested,
            bool standaloneFrontDisplay)
        {
            return frontDisplayRequested && !standaloneFrontDisplay;
        }

        private void RefreshFrontDisplayNow()
        {
            if (!TryRefreshRemoteSnapshot(ShowFrontScreen, true))
            {
                ShowFrontScreen();
            }
        }

        private bool TryResetRemoteBattle(Action afterReset)
        {
            if (supabase is not { IsConfigured: true })
            {
                return HandleUnavailableAuthoritativeBackend();
            }

            if (!supabase.HasSession)
            {
                SetBattleFeedback("セッション期限切れです。再ログインしてください。", FeedbackTone.Warning);
                ShowLogin();
                return true;
            }

            if (isNetworkBusy)
            {
                SetBattleFeedback("通信中です。次週準備の完了を待ってください。", FeedbackTone.Waiting);
                afterReset?.Invoke();
                return true;
            }

            StartCoroutine(ResetRemoteBattle(afterReset));
            return true;
        }

        private IEnumerator ResetRemoteBattle(Action afterReset)
        {
            var operationUserId = currentUser?.Id;
            if (string.IsNullOrWhiteSpace(operationUserId))
            {
                loginErrorMessage = "再ログインしてから次週準備を実行してください。";
                ShowLogin();
                yield break;
            }

            if (!RaidResetPendingOperationStore.TryGet(operationUserId, out var pending))
            {
                var expectedRaidEpoch = repository?.ActiveBattle?.RaidEpoch;
                if (!SupabaseRaidEpoch.IsValid(expectedRaidEpoch))
                {
                    SetBattleFeedback("最新のレイド状態を取得しています。確認後にもう一度実行してください。", FeedbackTone.Warning);
                    yield return RefreshRemoteSnapshot(afterReset);
                    yield break;
                }

                pending = RaidResetPendingOperationStore.GetOrCreate(operationUserId, expectedRaidEpoch);
            }

            isNetworkBusy = true;
            ResetBattleResponseDto response = null;
            yield return supabase.ResetBattle(
                pending.IdempotencyKey,
                pending.ExpectedRaidEpoch,
                result => response = result);
            isNetworkBusy = false;
            if (ReturnToLoginIfRemoteSessionExpired(response))
            {
                yield break;
            }

            if (response?.Ok == true)
            {
                RaidResetPendingOperationStore.Complete(operationUserId, pending.IdempotencyKey);
                ApplyRemoteBattleDelta(response);
                SetBattleFeedback("次週の準備完了", FeedbackTone.Success);
            }
            else
            {
                var errorCode = response?.ErrorCode ?? response?.Error;
                if (RaidResetPendingOperationStore.IsDefinitiveNonCommitFailure(errorCode))
                {
                    RaidResetPendingOperationStore.Complete(operationUserId, pending.IdempotencyKey);
                    SetBattleFeedback("次週準備は受理されませんでした。最新状態を確認してやり直してください。", FeedbackTone.Warning);
                    yield return RefreshRemoteSnapshot(afterReset);
                    yield break;
                }

                SetBattleFeedback(
                    RemoteErrorMessage("次週準備の結果を確認できませんでした。同じ操作として再確認できます。"),
                    FeedbackTone.Danger);
            }

            afterReset?.Invoke();
        }

        private bool TryReviewRemoteSession(
            DevSession session,
            bool approve,
            string comment,
            Action refreshAction = null,
            int? correctedDurationMinutes = null)
        {
            refreshAction ??= ShowMentorDashboard;
            if (supabase is not { IsConfigured: true })
            {
                return HandleUnavailableAuthoritativeBackend();
            }

            if (!supabase.HasSession)
            {
                SetMentorFeedback("セッション期限切れです。再ログインしてください。", FeedbackTone.Warning);
                ShowLogin();
                return true;
            }

            if (isNetworkBusy)
            {
                SetMentorFeedback("通信中です。承認処理の完了を待ってください。", FeedbackTone.Waiting);
                refreshAction();
                return true;
            }

            StartCoroutine(ReviewRemoteSession(session, approve, comment, refreshAction, correctedDurationMinutes));
            return true;
        }

        private IEnumerator ReviewRemoteSession(
            DevSession session,
            bool approve,
            string comment,
            Action refreshAction,
            int? correctedDurationMinutes)
        {
            refreshAction ??= ShowMentorDashboard;
            isNetworkBusy = true;
            SupabaseGameApiResponseDto response = null;
            if (approve)
            {
                if (correctedDurationMinutes.HasValue)
                {
                    yield return supabase.ApproveSession(session.Id, correctedDurationMinutes.Value, comment, result => response = result);
                }
                else
                {
                    yield return supabase.ApproveSession(session.Id, comment, result => response = result);
                }
            }
            else
            {
                yield return supabase.RejectSession(session.Id, comment, result => response = result);
            }

            isNetworkBusy = false;
            if (ReturnToLoginIfRemoteSessionExpired(response))
            {
                yield break;
            }

            if (response?.Ok == true)
            {
                ApplyRemoteSnapshot(response);
                var user = repository.Users.FirstOrDefault(item => item.Id == session.UserId);
                SetMentorFeedback(
                    approve
                        ? $"{user?.Nickname ?? "メンバー"} のログを承認しました。正式EXPと戦力へ反映済みです。"
                        : $"{user?.Nickname ?? "メンバー"} のログを却下しました。履歴に理由が残ります。",
                    approve ? FeedbackTone.Success : FeedbackTone.Warning);
            }
            else
            {
                SetMentorFeedback(RemoteErrorMessage("更新できませんでした。通信状態を確認してください。"), FeedbackTone.Danger);
            }

            refreshAction();
        }

        private string RemoteErrorMessage(string fallback)
        {
            var error = supabase?.LastApiError;
            if (error == null || error.Kind == SupabaseApiErrorKind.None)
            {
                return fallback;
            }

            return error.CanRetry ? $"{fallback} もう一度お試しください。" : fallback;
        }

        private bool ReturnToLoginIfRemoteSessionExpired(SupabaseGameApiResponseDto response)
        {
            if (response?.Ok == true)
            {
                return false;
            }

            var expired = response?.AuthExpired == true
                || supabase?.LastApiError?.IsAuthExpired == true;
            if (!expired)
            {
                return false;
            }

            // A bearer-session failure is a privacy boundary, not a recoverable screen-level
            // error. Discard the cached identity/snapshot before rendering another frame of an
            // authenticated screen (particularly important on shared classroom browsers).
            loginErrorMessage = "セッションの有効期限が切れました。再ログインしてください。";
            ShowLogin();
            return true;
        }

        private void AddSelectorRow<T>(Transform parent, System.Collections.Generic.IEnumerable<T> values, T selected, Action<T> onSelect, Func<T, string> getLabel)
        {
            var row = new GameObject($"Selector_{typeof(T).Name}", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            AddLayout(row, -1, 70);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            foreach (var value in values)
            {
                var isSelected = Equals(value, selected);
                var button = ui.CreateButton(row.transform, $"Select_{value}", getLabel(value), isSelected ? theme.PrimaryButton : theme.SecondaryButton, () => onSelect(value), isSelected ? Color.white : theme.Text);
                AddLayout(button.gameObject, 1, -1);
            }
        }

        private void AddSelectorRowCompact<T>(Transform parent, System.Collections.Generic.IEnumerable<T> values, T selected, Action<T> onSelect, Func<T, string> getLabel)
        {
            var row = new GameObject($"SelectorCompact_{typeof(T).Name}", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            AddLayout(row, -1, ResolveMinimumUiLength(48f, 44f));
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 5;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            foreach (var value in values)
            {
                var isSelected = Equals(value, selected);
                var label = getLabel(value);
                AddBattleButtonCell(row.transform, $"SelectCompact_{value}", label, isSelected ? theme.PrimaryButton : theme.SecondaryButton, () => onSelect(value), isSelected ? Color.white : theme.Text, 14);
            }
        }

        private void AddBattleSelectorGrid<T>(
            Transform parent,
            IEnumerable<T> values,
            T selected,
            Action<T> onSelect,
            Func<T, string> getLabel,
            int columnCount)
        {
            var items = values?.ToList() ?? new List<T>();
            var resolvedColumnCount = Mathf.Max(1, columnCount);
            var rowCount = Mathf.Max(1, Mathf.CeilToInt(items.Count / (float)resolvedColumnCount));
            var rowHeight = ResolveMinimumUiLength(48f, 44f);
            var grid = new GameObject($"SelectorGrid_{typeof(T).Name}", typeof(RectTransform), typeof(VerticalLayoutGroup));
            grid.transform.SetParent(parent, false);
            AddLayout(grid, -1, rowCount * rowHeight + Mathf.Max(0, rowCount - 1) * 5f);
            var gridLayout = grid.GetComponent<VerticalLayoutGroup>();
            gridLayout.spacing = 5;
            gridLayout.childControlWidth = true;
            gridLayout.childControlHeight = true;
            gridLayout.childForceExpandWidth = true;
            gridLayout.childForceExpandHeight = true;

            for (var start = 0; start < items.Count; start += resolvedColumnCount)
            {
                var row = new GameObject($"SelectorGridRow_{typeof(T).Name}_{start / resolvedColumnCount}", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                row.transform.SetParent(grid.transform, false);
                AddLayout(row, -1, rowHeight);
                var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
                rowLayout.spacing = 5;
                rowLayout.childControlWidth = true;
                rowLayout.childControlHeight = true;
                rowLayout.childForceExpandWidth = true;
                rowLayout.childForceExpandHeight = true;

                var rowItems = items.Skip(start).Take(resolvedColumnCount).ToList();
                foreach (var value in rowItems)
                {
                    var isSelected = Equals(value, selected);
                    AddBattleButtonCell(
                        row.transform,
                        $"SelectCompact_{value}",
                        getLabel(value),
                        isSelected ? theme.PrimaryButton : theme.SecondaryButton,
                        () => onSelect(value),
                        isSelected ? Color.white : theme.Text,
                        14);
                }

                AddFlexibleGridSpacers(row.transform, resolvedColumnCount - rowItems.Count);
            }
        }

        private void AddFlexibleGridSpacers(Transform parent, int count)
        {
            for (var index = 0; index < count; index++)
            {
                var spacer = new GameObject($"GridSpacer_{index}", typeof(RectTransform), typeof(LayoutElement));
                spacer.transform.SetParent(parent, false);
                AddLayout(spacer, 1f, -1f);
            }
        }

        private void AddSessionSummary(Transform parent, DevSession session, bool mentorControls, Action refreshAction = null)
        {
            refreshAction ??= ShowMentorDashboard;
            var summary = CreateColumn(parent, $"Session_{session.Id}", theme.StatCard, 1f);
            var user = repository.Users.First(item => item.Id == session.UserId);
            var sessionView = devLogPresenter.ToView(session, currentUser);
            AddText(summary, $"{StatusLabel(session.Status)} / {user.Nickname} / {FormatMinutes(session.DurationMinutes)} / 達成度 {session.AchievementRate}%", 24, FontStyle.Bold, StatusColor(session.Status), 40);
            AddText(summary, BuildSessionReviewDetail(session), 20, FontStyle.Bold, theme.Cyan, 32);
            AddText(summary, sessionView.GrowthStateLabel, 20, FontStyle.Bold, session.Status == DevSessionStatus.Approved ? theme.Cyan : theme.Magenta, 32);
            if (sessionView.HasReviewNotification)
            {
                AddText(summary, sessionView.ReviewNotificationLabel, 21, FontStyle.Bold, session.Status == DevSessionStatus.Rejected ? theme.Magenta : theme.Cyan, 44);
            }

            AddText(summary, $"目標: {session.Goal}", 21, FontStyle.Normal, theme.MutedText, 34);
            if (sessionView.CanViewAiEvaluation)
            {
                AddText(summary, $"{sessionView.AiEvaluationSummaryLabel}  /  仮EXP +{session.PreviewExp}", 22, FontStyle.Bold, theme.Magenta, 38);
                AddText(summary, sessionView.AiEvaluationFeedbackLabel, 20, FontStyle.Normal, theme.MutedText, 42);
            }

            if (session.GrowthFeedback != null)
            {
                AddText(summary, session.GrowthFeedback.Summary, 22, FontStyle.Bold, session.GrowthFeedback.HasLevelUp ? theme.Magenta : theme.Cyan, 38);
            }

            if (session.SuspiciousFlags.Count > 0)
            {
                AddText(summary, $"要確認: {string.Join(", ", session.SuspiciousFlags)}", 20, FontStyle.Bold, theme.Magenta, 32);
            }

            var mentorCommentLine = BuildMentorCommentLine(session);
            if (!string.IsNullOrEmpty(mentorCommentLine))
            {
                AddText(summary, mentorCommentLine, 20, FontStyle.Bold, session.Status == DevSessionStatus.Rejected ? theme.Magenta : StatusColor(session.Status), 52);
            }

            if (mentorControls)
            {
                var correctionRow = new GameObject("CorrectionInputs", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                correctionRow.transform.SetParent(summary, false);
                AddLayout(correctionRow, -1, 58);
                var correctionLayout = correctionRow.GetComponent<HorizontalLayoutGroup>();
                correctionLayout.spacing = 10;
                correctionLayout.childControlWidth = true;
                correctionLayout.childForceExpandWidth = true;
                var durationInput = ui.CreateInput(correctionRow.transform, "CorrectedDuration", "修正分");
                durationInput.contentType = InputField.ContentType.IntegerNumber;
                durationInput.text = session.DurationMinutes.ToString();
                AddLayout(durationInput.gameObject, 1, -1);
                AddText(correctionRow.transform, $"達成度 {session.AchievementRate}% / AI評価を保持", 17, FontStyle.Bold, theme.MutedText, 58, TextAnchor.MiddleCenter);

                var commentInput = ui.CreateInput(summary, "MentorCommentInput", "メンターコメント", true);
                AddLayout(commentInput.gameObject, -1, 84);

                var row = new GameObject("ApprovalActions", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                row.transform.SetParent(summary, false);
                AddLayout(row, -1, 58);
                var layout = row.GetComponent<HorizontalLayoutGroup>();
                layout.spacing = 12;
                layout.childControlWidth = true;
                layout.childForceExpandWidth = true;
                var approve = ui.CreateButton(row.transform, "Approve", "承認", theme.PrimaryButton, () =>
                {
                    var comment = ReadReviewComment(commentInput, "確認しました。正式EXPへ反映します。");
                    if (TryReviewRemoteSession(session, true, comment, refreshAction))
                    {
                        return;
                    }

                    var approvedSession = repository.ApproveSession(session.Id, currentUser.Id, comment);
                    SetMentorFeedback(BuildGrowthFeedbackMessage(user.Nickname, approvedSession), FeedbackTone.Success);
                    refreshAction();
                });
                AddLayout(approve.gameObject, 1, -1);
                var approveWithCorrections = ui.CreateButton(row.transform, "ApproveWithCorrections", "修正承認", theme.SecondaryButton, () =>
                {
                    var correctedDuration = ReadReviewInt(durationInput, session.DurationMinutes, 1, 24 * 60);
                    var comment = ReadReviewComment(commentInput, $"開発時間を {correctedDuration}分へ修正して承認");
                    if (TryReviewRemoteSession(session, true, comment, refreshAction, correctedDuration))
                    {
                        return;
                    }

                    var approvedSession = repository.ApproveSessionWithCorrections(
                        session.Id,
                        currentUser.Id,
                        session.AchievementRate,
                        correctedDuration,
                        session.Reflection,
                        session.NextTask,
                        comment);
                    SetMentorFeedback(BuildGrowthFeedbackMessage(user.Nickname, approvedSession), FeedbackTone.Success);
                    refreshAction();
                });
                AddLayout(approveWithCorrections.gameObject, 1, -1);
                var reject = ui.CreateButton(row.transform, "Reject", "却下", theme.DangerButton, () =>
                {
                    var comment = ReadReviewComment(commentInput, "今回は内容を再確認してください。");
                    if (TryReviewRemoteSession(session, false, comment, refreshAction))
                    {
                        return;
                    }

                    repository.RejectSession(session.Id, currentUser.Id, comment);
                    SetMentorFeedback($"{user.Nickname} のログを却下しました。履歴に理由が残ります。", FeedbackTone.Warning);
                    refreshAction();
                });
                AddLayout(reject.gameObject, 1, -1);
            }
        }

        private RectTransform CreateColumn(Transform parent, string name, Sprite sprite, float flexibleWidth)
        {
            var panel = ui.CreatePanel(parent, name, sprite, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(panel.gameObject, flexibleWidth, -1);
            AddVertical(panel, 18, 12);
            return panel;
        }

        private RectTransform CreateDashboardColumn(Transform parent, string name, float flexibleWidth, int padding, int spacing)
        {
            var column = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            column.transform.SetParent(parent, false);
            AddLayout(column, flexibleWidth, -1);
            AddVertical(column.GetComponent<RectTransform>(), padding, spacing);
            return column.GetComponent<RectTransform>();
        }

        private RectTransform CreateDashboardSection(Transform parent, string name, float preferredHeight, Sprite sprite)
        {
            var panel = ui.CreatePanel(parent, name, sprite, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(panel.gameObject, -1, preferredHeight);
            AddVertical(panel, 14, 10);
            return panel;
        }

        private RectTransform CreateDashboardRowSection(Transform parent, string name, float preferredHeight, Sprite sprite)
        {
            var panel = ui.CreatePanel(parent, name, sprite, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(panel.gameObject, -1, preferredHeight);
            AddHorizontal(panel, 16, 14);
            return panel;
        }

        private static float DashboardSectionHeight(params float[] childHeights)
        {
            var visibleHeights = childHeights.Where(height => height > 0f).ToArray();
            if (visibleHeights.Length == 0)
            {
                return 40f;
            }

            return 40f + visibleHeights.Sum() + Mathf.Max(0, visibleHeights.Length - 1) * 14f;
        }

        private RectTransform CreateScrollPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (UsesPortraitLayout())
            {
                anchorMin.x = 0.06f;
                anchorMax.x = 0.94f;
            }
            var rootPanel = ui.CreatePanel(parent, name, theme.LogPanel, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            var viewport = new GameObject("Viewport", typeof(Image), typeof(Mask));
            viewport.transform.SetParent(rootPanel, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            ui.Stretch(viewportRect, 24, 24, -24, -24);
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(0f, 0f);
            contentRect.offsetMax = new Vector2(0f, 0f);
            AddVertical(contentRect, 0, 16);
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = rootPanel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return contentRect;
        }

        private RectTransform CreateDashboardScrollPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rootPanel = ui.CreatePanel(parent, name, theme.RaidPanel, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            var viewport = new GameObject("Viewport", typeof(Image), typeof(Mask));
            viewport.transform.SetParent(rootPanel, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            ui.Stretch(viewportRect, 14, 14, -14, -14);
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            AddVertical(contentRect, 0, 18);
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = rootPanel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return contentRect;
        }

        private RectTransform CreateEmbeddedScrollContent(RectTransform panel, string name, int inset, int spacing)
        {
            var viewport = new GameObject($"{name}Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(panel, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            ui.Stretch(viewportRect, inset, inset, -inset, -inset);
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject($"{name}Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            AddVertical(contentRect, 0, spacing);
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return contentRect;
        }

        private void AddHeader(string title, string subtitle, UnityEngine.Events.UnityAction backAction = null, string backLabel = DefaultBackLabel, bool showSettings = true)
        {
            AddSceneDimmer();
            var header = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            header.SetParent(EnsureRoot(), false);
            var useFullWidth = UsesPortraitLayout() || UsesCompactLandscapeLayout();
            SetAnchored(
                header,
                useFullWidth ? new Vector2(0.06f, 0.84f) : new Vector2(0.43f, 0.84f),
                useFullWidth ? new Vector2(0.94f, 0.955f) : new Vector2(0.96f, 0.955f));
            AddHorizontal(header, 12, 10);
            var compactSideWidth = theme.SettingsIcon != null ? 64 : 160;
            if (backAction == null)
            {
                AddHeaderSpacer(header, compactSideWidth);
            }
            else if (backLabel == DefaultBackLabel && theme.BackIcon != null)
            {
                var back = CreateHeaderIconButton(header, "BackButton", theme.BackIcon, backAction);
                var iconTargetLength = ResolveMinimumUiLength(64f, 48f);
                AddLayout(back.gameObject, iconTargetLength, iconTargetLength);
            }
            else
            {
                var back = ui.CreateButton(header, "BackButton", backLabel, theme.SecondaryButton, backAction);
                AddLayout(back.gameObject, Mathf.Clamp(74 + backLabel.Length * 28, 150, 230), -1);
            }

            var titleBox = new GameObject("TitleBox", typeof(RectTransform), typeof(VerticalLayoutGroup));
            titleBox.transform.SetParent(header, false);
            AddLayout(titleBox, 1, -1);
            AddVertical(titleBox.GetComponent<RectTransform>(), 0, 2, TextAnchor.MiddleCenter);
            var titleLabel = AddDisplayText(titleBox.transform, title, UsesConstrainedLayout() ? 26 : 32, theme.Text, 48, TextAnchor.MiddleCenter);
            PreserveFullSingleLineLabel(titleLabel, UsesConstrainedLayout() ? 20 : 24);
            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                AddText(titleBox.transform, subtitle, 17, FontStyle.Bold, theme.MutedText, 28, TextAnchor.MiddleCenter);
            }

            if (currentUser != null && showSettings)
            {
                var settings = theme.SettingsIcon != null
                    ? CreateHeaderIconButton(header, "SettingsButton", theme.SettingsIcon, ShowSettings)
                    : ui.CreateButton(header, "SettingsButton", SettingsLabel, theme.SecondaryButton, ShowSettings);
                var settingsTargetLength = theme.SettingsIcon != null ? ResolveMinimumUiLength(64f, 48f) : 160f;
                AddLayout(settings.gameObject, settingsTargetLength, theme.SettingsIcon != null ? settingsTargetLength : -1);
            }
            else
            {
                AddHeaderSpacer(header, compactSideWidth);
            }

        }

        private bool UsesPortraitLayout()
        {
            if (Application.isPlaying && Screen.width > 0 && Screen.height > 0)
            {
                return Screen.height > Screen.width * 1.25f;
            }

            if (root == null)
            {
                return false;
            }

            var size = root.rect.size;
            return size.x > 0f && size.y > size.x * 1.25f;
        }

        private bool UsesCompactLandscapeLayout()
        {
            if (UsesPortraitLayout())
            {
                return false;
            }

            var viewport = ResolveLoginViewportSize();
            return viewport.x > 0f && viewport.y > 0f && viewport.y < 480f;
        }

        private bool UsesConstrainedLayout()
        {
            return UsesPortraitLayout() || UsesCompactLandscapeLayout();
        }

        private int ResponsiveCollectionPageSize(int compact, int portrait, int desktop)
        {
            if (UsesCompactLandscapeLayout())
            {
                return Mathf.Max(1, compact);
            }

            if (UsesPortraitLayout())
            {
                return Mathf.Max(1, portrait);
            }

            return Mathf.Max(1, desktop);
        }

        private static int ClampPage(int page, int itemCount, int pageSize)
        {
            var safePageSize = Mathf.Max(1, pageSize);
            var pageCount = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0, itemCount) / (float)safePageSize));
            return Mathf.Clamp(page, 0, pageCount - 1);
        }

        private Button CreateHeaderIconButton(
            Transform parent,
            string name,
            Sprite icon,
            UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var frame = buttonObject.GetComponent<Image>();
            frame.sprite = theme.WaypointNavRing ?? theme.SecondaryButton;
            frame.type = Image.Type.Simple;
            frame.preserveAspect = true;
            frame.color = Color.white;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = frame;
            button.onClick.AddListener(onClick);
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.84f, 0.96f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.64f, 0.84f, 0.94f, 1f);
            colors.disabledColor = new Color(0.42f, 0.48f, 0.56f, 0.45f);
            button.colors = colors;

            var iconObject = new GameObject($"{name}_Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(buttonObject.transform, false);
            var iconImage = iconObject.GetComponent<Image>();
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.color = theme.Text;
            iconImage.raycastTarget = false;
            SetRelativeRect(iconImage.rectTransform, new Vector2(0.30f, 0.30f), new Vector2(0.70f, 0.70f));
            return button;
        }

        private void AddSceneDimmer()
        {
            var existing = root != null ? root.Find("SceneDimmer") : null;
            if (existing != null)
            {
                return;
            }

            var dimmerObject = new GameObject("SceneDimmer", typeof(RectTransform), typeof(Image));
            dimmerObject.transform.SetParent(EnsureRoot(), false);
            var image = dimmerObject.GetComponent<Image>();
            image.sprite = null;
            image.color = new Color(0.015f, 0.045f, 0.085f, 0.52f);
            image.raycastTarget = false;
            ui.Stretch(image.rectTransform, 0f, 0f, 0f, 0f);
            dimmerObject.transform.SetAsFirstSibling();
        }

        private void AddBattleHeader(string title, UnityEngine.Events.UnityAction backAction)
        {
            var compactLandscape = UsesCompactLandscapeLayout();
            var backButton = CreateBrightBattleCornerButton(
                EnsureRoot(),
                "BattleBackButton",
                theme.BackIcon,
                backAction);
            SetBattleCornerButton(backButton.GetComponent<RectTransform>(), false);

            var settingsButton = CreateBrightBattleCornerButton(
                EnsureRoot(),
                "BattleSettingsButton",
                theme.SettingsIcon,
                ShowSettings);
            SetBattleCornerButton(settingsButton.GetComponent<RectTransform>(), true);

            var titleText = ui.CreateText(EnsureRoot(), "BattleHeaderTitle", title, ResolveUiFontSize(compactLandscape ? 20 : 28), FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            PreserveFullSingleLineLabel(titleText, compactLandscape ? 18 : 23);
            AddBattleTextShadow(titleText, new Color(0.06f, 0.14f, 0.24f, 0.72f), new Vector2(1.5f, -2f));
            AddBattleTextOutline(titleText);
            if (compactLandscape)
            {
                SetAnchored(titleText.rectTransform, new Vector2(0.32f, 0.928f), new Vector2(0.68f, 0.987f));
            }
            else
            {
                SetAnchored(titleText.rectTransform, new Vector2(0.30f, 0.929f), new Vector2(0.70f, 0.987f));
            }
            AddDeveloperNavigation();
        }

        private Button CreateBrightBattleCornerButton(
            Transform parent,
            string name,
            Sprite icon,
            UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Shadow));
            buttonObject.transform.SetParent(parent, false);
            var frame = buttonObject.GetComponent<Image>();
            frame.sprite = theme.BattleCircleButton ?? theme.HexBadge ?? theme.SecondaryButton;
            frame.type = Image.Type.Simple;
            frame.preserveAspect = true;
            frame.color = Color.white;

            var shadow = buttonObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0.05f, 0.12f, 0.20f, 0.28f);
            shadow.effectDistance = new Vector2(0f, -4f);
            shadow.useGraphicAlpha = true;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = frame;
            button.onClick.AddListener(onClick);
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(0.93f, 0.98f, 1f, 1f),
                pressedColor = new Color(0.79f, 0.91f, 0.98f, 1f),
                selectedColor = new Color(0.93f, 0.98f, 1f, 1f),
                disabledColor = new Color(0.72f, 0.75f, 0.78f, 0.55f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };

            if (icon != null)
            {
                var iconObject = new GameObject($"{name}_Icon", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(buttonObject.transform, false);
                var iconImage = iconObject.GetComponent<Image>();
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
                iconImage.color = new Color(0.12f, 0.17f, 0.25f, 1f);
                iconImage.raycastTarget = false;
                SetRelativeRect(iconImage.rectTransform, new Vector2(0.27f, 0.27f), new Vector2(0.73f, 0.73f));
            }

            return button;
        }

        private static void AddBattleTextShadow(Text text, Color color, Vector2 distance)
        {
            if (text == null || text.GetComponent<Shadow>() != null)
            {
                return;
            }

            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private static void AddBattleTextOutline(Text text)
        {
            if (text == null || text.GetComponent<Outline>() != null)
            {
                return;
            }

            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.05f, 0.12f, 0.20f, 0.88f);
            outline.effectDistance = new Vector2(1.15f, -1.15f);
            outline.useGraphicAlpha = true;
        }

        private void SetBattleCornerButton(RectTransform rect, bool rightAligned)
        {
            if (rect == null)
            {
                return;
            }

            var anchor = new Vector2(rightAligned ? 0.975f : 0.018f, 0.975f);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(rightAligned ? 1f : 0f, 1f);
            var size = ResolveMinimumUiLength(72f, 48f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private void AddDeveloperNavigation()
        {
            if (!ShouldShowDeveloperNavigation())
            {
                return;
            }

            var existing = root != null ? root.Find("DeveloperNavigation") : null;
            if (existing != null)
            {
                DestroyRuntimeObject(existing.gameObject);
            }

            var nav = ui.CreatePanel(EnsureRoot(), "DeveloperNavigation", theme.StatCard, new Vector2(0.32f, 0.012f), new Vector2(0.68f, 0.155f), Vector2.zero, Vector2.zero);
            AddDeveloperNavigationBackdrop(nav);
            AddDeveloperNavigationGrid(nav);
            AddDeveloperNavigationButton(nav, "ログイン", ShowLogin);
            AddDeveloperNavigationButton(nav, "ホーム", ShowDeveloperMemberHome);
            AddDeveloperNavigationButton(nav, "開発ログ", ShowDeveloperDevLog);
            AddDeveloperNavigationButton(nav, "ボス戦", ShowDeveloperBattle);
            AddDeveloperNavigationButton(nav, "全体", ShowFrontScreen);
            AddDeveloperNavigationButton(nav, "メンター", ShowDeveloperMentorDashboard);
            nav.transform.SetAsLastSibling();
            ScheduleDeveloperNavigationFront();
        }

        private void ScheduleDeveloperNavigationFront()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                return;
            }

            if (developerNavigationFrontRoutine != null)
            {
                StopCoroutine(developerNavigationFrontRoutine);
            }

            developerNavigationFrontRoutine = StartCoroutine(BringDeveloperNavigationToFrontNextFrame());
        }

        private IEnumerator BringDeveloperNavigationToFrontNextFrame()
        {
            yield return null;
            var nav = root != null ? root.Find("DeveloperNavigation") : null;
            if (nav != null)
            {
                nav.SetAsLastSibling();
            }

            developerNavigationFrontRoutine = null;
        }

        private static void AddDeveloperNavigationGrid(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            var layout = GetOrAddLayoutGroup<GridLayoutGroup>(rect);
            if (layout == null)
            {
                return;
            }

            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = new Vector2(8f, 8f);
            layout.cellSize = new Vector2(132f, 34f);
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            layout.childAlignment = TextAnchor.MiddleCenter;
        }

        private void AddDeveloperNavigationBackdrop(RectTransform parent)
        {
            var fillObject = new GameObject("DeveloperNavigationFill", typeof(Image), typeof(LayoutElement));
            fillObject.transform.SetParent(parent, false);

            var layout = fillObject.GetComponent<LayoutElement>();
            layout.ignoreLayout = true;

            var fill = fillObject.GetComponent<Image>();
            fill.sprite = null;
            fill.color = new Color(0.98f, 0.95f, 0.82f, 0.9f);
            fill.raycastTarget = false;
            ui.Stretch(fill.rectTransform, 18f, 7f, -18f, -7f);
            fillObject.transform.SetAsFirstSibling();
        }

        private void AddDeveloperNavigationButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var button = ui.CreateButton(parent, $"DevNav_{label}", label, theme.SecondaryButton, onClick, theme.Text);
            AddLayout(button.gameObject, 1, 48);
        }

        private static void AddDeveloperNavigationClickFallback(GameObject target, UnityEngine.Events.UnityAction onClick)
        {
            if (target == null || onClick == null)
            {
                return;
            }

            var trigger = target.GetComponent<EventTrigger>() ?? target.AddComponent<EventTrigger>();
            trigger.triggers.Clear();
            var pointerClick = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            pointerClick.callback.AddListener(_ => onClick.Invoke());
            trigger.triggers.Add(pointerClick);
        }

        private static bool ShouldShowDeveloperNavigation()
        {
            return false;
        }

        private UserProfile EnsureDeveloperUser(UserRole role)
        {
            if (!EnsureRemoteSnapshotReadyForNavigation())
            {
                return null;
            }

            var user = currentUser != null && currentUser.Role == role
                ? currentUser
                : repository.Users.FirstOrDefault(item => item.Role == role && item.IsActive);
            if (user == null && IsVisualPreviewActive())
            {
                try
                {
                    user = repository.LoginAs(role);
                }
                catch (InvalidOperationException)
                {
                    user = null;
                }
            }

            if (user == null)
            {
                return null;
            }

            currentUser = user;
            RasshiineRuntimeSession.SetUser(user);
            PersistRuntimeSnapshot();
            return currentUser;
        }

        private void ShowDeveloperMemberHome()
        {
            if (!EnsureRemoteSnapshotReadyForNavigation())
            {
                return;
            }

            if (EnsureDeveloperUser(UserRole.Member) == null)
            {
                ShowLogin();
                return;
            }

            ShowMemberHome();
        }

        private void ShowDeveloperDevLog()
        {
            if (!EnsureRemoteSnapshotReadyForNavigation())
            {
                return;
            }

            if (EnsureDeveloperUser(UserRole.Member) == null)
            {
                ShowLogin();
                return;
            }

            ShowDevLog();
        }

        private void ShowDeveloperBattle()
        {
            if (!EnsureRemoteSnapshotReadyForNavigation())
            {
                return;
            }

            if (EnsureDeveloperUser(UserRole.Member) == null)
            {
                ShowLogin();
                return;
            }

            if (IsVisualPreviewActive() && repository.ActiveBattle.Status == BattleStatus.Scheduled)
            {
                var mentorId = repository.Mentors.FirstOrDefault(item => item.IsActive)?.Id ?? currentUser.Id;
                repository.StartBattle(mentorId);
            }

            selectedRole = BattleRole.Attacker;
            selectedWeapon = WeaponKind.Blade;
            SyncBattleControlledParticipant();
            ShowBattle();
        }

        private void ShowDeveloperMentorDashboard()
        {
            if (!EnsureRemoteSnapshotReadyForNavigation())
            {
                return;
            }

            if (EnsureDeveloperUser(UserRole.Mentor) == null)
            {
                ShowLogin();
                return;
            }

            ShowMentorDashboard();
        }

        private void AddHeaderSpacer(Transform parent, float width)
        {
            var spacer = new GameObject("HeaderSpacer", typeof(RectTransform));
            spacer.transform.SetParent(parent, false);
            AddLayout(spacer, width, -1);
        }

        private Button AddButton(Transform parent, string label, Sprite sprite, UnityEngine.Events.UnityAction onClick)
        {
            var button = ui.CreateButton(parent, label, label, sprite, onClick);
            AddLayout(button.gameObject, -1, 72);
            return button;
        }

        private Button AddDashboardAction(Transform parent, string label, Sprite sprite, UnityEngine.Events.UnityAction onClick)
        {
            var button = ui.CreateButton(parent, $"DashboardAction_{label}", label, sprite, onClick);
            AddLayout(button.gameObject, 1, -1);
            return button;
        }

        private void AddHomeNavigationArc(RectTransform hud)
        {
            var arc = CreateReferenceImage(hud, "HomeNavigationArcLine", null, 55f, 104f, 438f, 2f, new Color(theme.Gold.r, theme.Gold.g, theme.Gold.b, 0.56f), false);
            arc.localRotation = Quaternion.Euler(0f, 0f, 8f);

            CreateHomeNavigationButton(hud, "HomeNav_ホーム", "ホーム", theme.HomeIcon, 30f, 54f, true, ShowMemberHome);
            CreateHomeNavigationButton(hud, "HomeNav_チーム", "チーム", theme.TeamIcon, 134f, 42f, false, ShowMemberTeam);
            CreateHomeNavigationButton(hud, "HomeNav_目標", "目標", theme.GoalIcon, 238f, 30f, false, ShowDevLog);
            CreateHomeNavigationButton(hud, "HomeNav_記録", "記録", theme.RecordIcon, 342f, 23f, false, ShowMemberHistory);
            CreateHomeNavigationButton(hud, "SettingsButton", "設定", theme.SettingsIcon, 446f, 18f, false, ShowSettings);
        }

        private void ApplyPortraitHomeLayout(RectTransform hud)
        {
            if (hud == null || !UsesPortraitLayout())
            {
                return;
            }

            var arc = hud.Find("HomeNavigationArcLine") as RectTransform;
            if (arc != null)
            {
                SetRelativeRect(arc, new Vector2(0.04f, 0.925f), new Vector2(0.96f, 0.928f));
                arc.localRotation = Quaternion.identity;
            }

            var navNames = new[] { "HomeNav_ホーム", "HomeNav_チーム", "HomeNav_目標", "HomeNav_記録", "SettingsButton" };
            for (var index = 0; index < navNames.Length; index += 1)
            {
                var nav = hud.Find(navNames[index]) as RectTransform;
                if (nav == null)
                {
                    continue;
                }

                var xMin = 0.015f + index * 0.197f;
                SetRelativeRect(nav, new Vector2(xMin, 0.855f), new Vector2(xMin + 0.165f, 0.988f));
                ScaleChildTextForCompactCanvas(nav);
            }

            SetPortraitHomeRect(hud, "PlayerStatusZone", new Vector2(0.06f, 0.675f), new Vector2(0.94f, 0.835f), true);
            SetPortraitHomeRect(hud, "WorldGlyph", new Vector2(0.07f, 0.605f), new Vector2(0.12f, 0.642f));
            SetPortraitHomeRect(hud, "WorldLabel", new Vector2(0.135f, 0.602f), new Vector2(0.86f, 0.642f), true);
            SetPortraitHomeRect(hud, "WorldLabelEnglish", new Vector2(0.135f, 0.565f), new Vector2(0.86f, 0.602f), true);
            SetPortraitHomeRect(hud, "PrimaryDevelopmentCompass", new Vector2(0.17f, 0.025f), new Vector2(0.83f, 0.305f), true);
            SetPortraitHomeRect(hud, "NextRaidRibbon", new Vector2(0.08f, 0.325f), new Vector2(0.92f, 0.455f), true);
            SetPortraitHomeRect(hud, "HomeWelcomeToast", new Vector2(0.08f, 0.485f), new Vector2(0.92f, 0.545f));
            SetPortraitHomeRect(hud, "HomeWelcomeGlyph", new Vector2(0.095f, 0.493f), new Vector2(0.17f, 0.538f));
            SetPortraitHomeRect(hud, "HomeWelcomeText", new Vector2(0.185f, 0.493f), new Vector2(0.89f, 0.538f), true);
        }

        private void SetPortraitHomeRect(
            RectTransform hud,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            bool scaleText = false)
        {
            var rect = hud != null ? hud.Find(name) as RectTransform : null;
            if (rect == null)
            {
                return;
            }

            SetRelativeRect(rect, anchorMin, anchorMax);
            if (scaleText)
            {
                ScaleChildTextForCompactCanvas(rect);
            }
        }

        private void ScaleChildTextForCompactCanvas(RectTransform parent)
        {
            if (parent == null)
            {
                return;
            }

            foreach (var text in parent.GetComponentsInChildren<Text>(true))
            {
                text.fontSize = ResolveUiFontSize(text.fontSize);
            }
        }

        private void CreateHomeNavigationButton(
            RectTransform hud,
            string name,
            string label,
            Sprite icon,
            float x,
            float y,
            bool selected,
            UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(hud, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            SetReferenceRect(rect, x, y, 92f, 116f);

            var raycastSurface = buttonObject.GetComponent<Image>();
            raycastSurface.sprite = null;
            raycastSurface.color = new Color(0f, 0f, 0f, 0.01f);

            var ringObject = new GameObject("Ring", typeof(RectTransform), typeof(Image));
            ringObject.transform.SetParent(rect, false);
            var ring = ringObject.GetComponent<Image>();
            ring.sprite = theme.WaypointNavRing;
            ring.type = Image.Type.Simple;
            ring.preserveAspect = true;
            ring.color = selected ? Color.white : new Color(0.86f, 0.91f, 0.96f, 0.9f);
            ring.raycastTarget = false;
            SetRelativeRect(ring.rectTransform, new Vector2(0.06f, 0.28f), new Vector2(0.94f, 0.98f));

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = ring;
            button.onClick.AddListener(onClick);
            var colors = button.colors;
            colors.normalColor = ring.color;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.72f, 0.94f, 1f, 1f);
            colors.disabledColor = new Color(0.5f, 0.55f, 0.62f, 0.45f);
            colors.colorMultiplier = 1f;
            button.colors = colors;

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(rect, false);
            var iconImage = iconObject.GetComponent<Image>();
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.color = selected ? theme.Text : theme.Gold;
            iconImage.raycastTarget = false;
            SetRelativeRect(iconImage.rectTransform, new Vector2(0.28f, 0.48f), new Vector2(0.72f, 0.82f));

            var labelText = ui.CreateText(rect, "Label", label, 15, FontStyle.Bold, theme.Text, TextAnchor.MiddleCenter);
            labelText.resizeTextForBestFit = false;
            SetRelativeRect(labelText.rectTransform, new Vector2(0f, 0.02f), new Vector2(1f, 0.30f));
        }

        private void AddHomePlayerStatus(RectTransform hud, CharacterStats stats)
        {
            var zone = new GameObject("PlayerStatusZone", typeof(RectTransform)).GetComponent<RectTransform>();
            zone.SetParent(hud, false);
            SetReferenceRect(zone, 1015f, 36f, 395f, 169f);
            // The generated frame has 58 px of transparent top padding in its 256 px texture.
            // Rendering that texture directly into a 100 px rect compresses the visible artwork to
            // roughly 53 px and leaves the first text row outside the painted panel. Oversizing it
            // restores a 100 px visible frame while retaining the authored crest silhouette.
            CreateLocalImage(zone, "PlayerStatusFrame", theme.WaypointPlayerStatusFrame, 0f, -43f, 395f, 188f, 395f, 169f, Color.white, false);
            CreateLocalImage(zone, "PlayerCrest", theme.WaypointCrest, 10f, 3f, 82f, 94f, 395f, 169f, Color.white, true);

            var statusSafeArea = CreateLocalMaskArea(zone, "PlayerStatusTextSafeArea", 98f, 6f, 282f, 78f, 395f, 169f);
            AddLocalSingleLineText(statusSafeArea, "PlayerNickname", Shorten(currentUser.Nickname, 10), 24, 15, FontStyle.Bold, theme.Text, 5f, 0f, 166f, 36f, 282f, 78f, TextAnchor.MiddleLeft);
            AddLocalSingleLineText(statusSafeArea, "PlayerLevel", $"LEVEL  {stats.Level}", 16, 11, FontStyle.Bold, theme.Gold, 176f, 2f, 104f, 32f, 282f, 78f, TextAnchor.MiddleCenter);
            AddLocalSingleLineText(statusSafeArea, "PlayerExpLabel", $"EXP  {stats.Exp:N0} / {stats.ExpToNextLevel:N0}", 15, 10, FontStyle.Bold, theme.Text, 5f, 42f, 273f, 27f, 282f, 78f, TextAnchor.MiddleLeft);

            CreateLocalImage(zone, "PlayerExpTrack", null, 103f, 79f, 274f, 5f, 395f, 169f, new Color(0.47f, 0.73f, 0.84f, 0.34f), false);
            var expRatio = stats.ExpToNextLevel > 0 ? Mathf.Clamp01(stats.Exp / (float)stats.ExpToNextLevel) : 0f;
            CreateLocalImage(zone, "PlayerExpFill", null, 103f, 79f, 274f * expRatio, 5f, 395f, 169f, theme.Cyan, false);
            CreateLocalImage(zone, "PlayerStateDivider", null, 103f, 109f, 274f, 1f, 395f, 169f, new Color(theme.Gold.r, theme.Gold.g, theme.Gold.b, 0.28f), false);

            var stateSafeArea = CreateLocalMaskArea(zone, "PlayerStateSafeArea", 103f, 115f, 274f, 48f, 395f, 169f);
            CreateLocalImage(stateSafeArea, "PlayerStateGlyph", theme.GoalIcon, 0f, 4f, 32f, 32f, 274f, 48f, theme.Gold, true);
            AddLocalSingleLineText(stateSafeArea, "PlayerTodayState", "今日の目標を決めよう", 15, 10, FontStyle.Bold, theme.Text, 40f, 2f, 232f, 36f, 274f, 48f, TextAnchor.MiddleLeft);
        }

        private void AddHomeWorldLabel(RectTransform hud)
        {
            CreateReferenceImage(hud, "WorldGlyph", theme.GoalIcon, 48f, 260f, 18f, 18f, theme.Gold, true);
            AddReferenceText(hud, "WorldLabel", "蒼天のウェイポイントテラス", 15, FontStyle.Bold, theme.Gold, 72f, 254f, 296f, 30f, TextAnchor.MiddleLeft);
            AddReferenceText(hud, "WorldLabelEnglish", "Skybound Waypoint Terrace", 13, FontStyle.Normal, theme.Text, 72f, 288f, 260f, 24f, TextAnchor.MiddleLeft);
        }

        private void AddHomeDevelopmentCompass(RectTransform hud)
        {
            var buttonObject = new GameObject("PrimaryDevelopmentCompass", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(hud, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            SetReferenceRect(rect, 574f, 732f, 292f, 292f);

            var image = buttonObject.GetComponent<Image>();
            image.sprite = theme.WaypointCompass;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(ShowDevLog);
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.86f, 0.98f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.68f, 0.91f, 0.98f, 1f);
            button.colors = colors;

            var label = ui.CreateDisplayText(rect, "PrimaryDevelopmentLabel", "開発をはじめる", 29, theme.Text, TextAnchor.MiddleCenter);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 20;
            label.resizeTextMaxSize = 29;
            SetRelativeRect(label.rectTransform, new Vector2(0.16f, 0.03f), new Vector2(0.84f, 0.30f));
        }

        private void AddHomeNextRaidRibbon(RectTransform hud)
        {
            var buttonObject = new GameObject("NextRaidRibbon", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(hud, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            SetReferenceRect(rect, 965f, 846f, 445f, 136f);
            var frame = buttonObject.GetComponent<Image>();
            frame.sprite = theme.WaypointNextRaidFrame;
            frame.type = Image.Type.Simple;
            frame.preserveAspect = true;
            frame.color = Color.white;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = frame;
            button.onClick.AddListener(ShowBattle);
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 0.98f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.76f, 0.90f, 0.95f, 1f);
            button.colors = colors;

            var iconObject = new GameObject("RaidIcon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(rect, false);
            var icon = iconObject.GetComponent<Image>();
            icon.sprite = theme.BattleIcon;
            icon.color = theme.Gold;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            SetRelativeRect(icon.rectTransform, new Vector2(0.055f, 0.27f), new Vector2(0.165f, 0.73f));

            // The generated ribbon artwork occupies y=59..193 of a 256 px texture. Keep both
            // copy rows inside that painted band and clip them as a final containment guarantee.
            var textSafeArea = new GameObject("NextRaidTextSafeArea", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            textSafeArea.SetParent(rect, false);
            SetRelativeRect(textSafeArea, new Vector2(0.205f, 0.30f), new Vector2(0.895f, 0.72f));

            var eyebrow = ui.CreateText(textSafeArea, "NextRaidEyebrow", "次回レイド", 14, FontStyle.Bold, theme.MutedText, TextAnchor.MiddleLeft);
            ConfigureSingleLineLabel(eyebrow, 14);
            eyebrow.resizeTextMinSize = 10;
            SetRelativeRect(eyebrow.rectTransform, new Vector2(0f, 0.61f), new Vector2(0.56f, 1f));

            var date = ui.CreateDisplayText(textSafeArea, "NextRaidDate", NextRaidLabel(), 25, theme.Text, TextAnchor.MiddleLeft);
            ConfigureSingleLineLabel(date, 25);
            date.resizeTextMinSize = 16;
            SetRelativeRect(date.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.56f));
        }

        private void AddHomeWelcomeToast(RectTransform hud)
        {
            var toast = ui.CreatePanel(hud, "HomeWelcomeToast", theme.NotificationPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            SetReferenceRect(toast, 32f, 954f, 328f, 50f);
            var glyph = CreateReferenceImage(hud, "HomeWelcomeGlyph", theme.WaypointCrest, 40f, 961f, 34f, 34f, new Color(theme.Cyan.r, theme.Cyan.g, theme.Cyan.b, 0.72f), true);
            glyph.SetAsLastSibling();
            AddReferenceText(hud, "HomeWelcomeText", $"ようこそ、{Shorten(currentUser.Nickname, 10)}さん", 13, FontStyle.Bold, theme.Text, 82f, 961f, 258f, 34f, TextAnchor.MiddleLeft);
        }

        private void ShowMemberTeam()
        {
            ClearRoot();
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Home);
            AddHeader("チーム", LocalGameRepository.GetTeamDisplayName(currentUser.TeamId), ShowMemberHome);
            if (UsesConstrainedLayout())
            {
                BuildResponsiveMemberTeam();
                return;
            }

            var useFullWidth = UsesPortraitLayout() || UsesCompactLandscapeLayout();
            var scroll = CreateScrollPanel(
                root,
                "MemberTeamPanel",
                new Vector2(useFullWidth ? 0.06f : 0.43f, 0.06f),
                new Vector2(useFullWidth ? 0.94f : 0.96f, 0.82f));
            AddText(scroll, "仲間の現在地", 30, FontStyle.Bold, theme.Text, 46, TextAnchor.MiddleCenter);
            AddText(scroll, "同じ班の公開プロフィールと今週の承認済み開発時間です。", 18, FontStyle.Bold, theme.MutedText, 38, TextAnchor.MiddleCenter);

            var characterMenus = CreateHudRow(scroll, "CharacterMenus", 62);
            var wardrobe = ui.CreateButton(characterMenus, "OpenCosmeticWardrobe", "着せ替え", theme.SecondaryButton, ShowCosmeticWardrobe, theme.Text);
            AddLayout(wardrobe.gameObject, 1f, -1);
            var wish = ui.CreateButton(characterMenus, "OpenWeaponWish", "武器祈願", theme.PrimaryButton, ShowWeaponWish, Color.white);
            AddLayout(wish.gameObject, 1f, -1);

            foreach (var member in repository.Members.Where(member => member.TeamId == currentUser.TeamId && member.IsActive))
            {
                var memberStats = repository.GetStats(member.Id);
                var row = ui.CreatePanel(scroll, $"TeamMember_{member.Id}", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                AddLayout(row.gameObject, -1, 72);
                AddHorizontal(row, 16, 12);
                var name = ui.CreateText(row, "Name", member.Nickname, 21, FontStyle.Bold, member.Id == currentUser.Id ? theme.Gold : theme.Text);
                AddLayout(name.gameObject, 1f, -1);
                var level = ui.CreateText(row, "Level", $"Lv.{memberStats.Level}", 17, FontStyle.Bold, theme.Cyan, TextAnchor.MiddleCenter);
                AddLayout(level.gameObject, 80f, -1);
                var time = ui.CreateText(row, "WeeklyTime", FormatMinutes(repository.GetApprovedMinutesThisWeek(member.Id)), 17, FontStyle.Bold, theme.MutedText, TextAnchor.MiddleRight);
                AddLayout(time.gameObject, 110f, -1);
            }

            var rankingButton = ui.CreateButton(scroll, "OpenTeamRanking", "ランキングを見る", theme.SecondaryButton, () =>
            {
                selectedRankingView = RankingView.TeamMember;
                ShowRanking();
            });
            AddLayout(rankingButton.gameObject, -1, 56);
        }

        private void BuildResponsiveMemberTeam()
        {
            var content = CreateScrollPanel(root, "MemberTeamPanel", new Vector2(0.04f, 0.035f), new Vector2(0.96f, 0.82f));
            var menus = CreateHudRow(content, "CharacterMenus", 60);
            var wardrobe = ui.CreateButton(menus, "OpenCosmeticWardrobe", "着せ替え", theme.SecondaryButton, ShowCosmeticWardrobe, theme.Text);
            AddLayout(wardrobe.gameObject, 1f, -1);
            var wish = ui.CreateButton(menus, "OpenWeaponWish", "武器祈願", theme.PrimaryButton, ShowWeaponWish, Color.white);
            AddLayout(wish.gameObject, 1f, -1);

            var members = repository.Members
                .Where(member => member.TeamId == currentUser.TeamId && member.IsActive)
                .OrderByDescending(member => member.Id == currentUser.Id)
                .ThenBy(member => member.Nickname)
                .ToList();
            var pageSize = ResponsiveCollectionPageSize(2, 4, 8);
            teamPage = ClampPage(teamPage, members.Count, pageSize);
            var roster = CreateDashboardSection(content, "ResponsiveMemberTeamRoster", -1f, theme.LogPanel);
            AddText(roster, "仲間の現在地", 25, FontStyle.Bold, theme.Text, 36, TextAnchor.MiddleCenter);
            foreach (var member in members.Skip(teamPage * pageSize).Take(pageSize))
            {
                var stats = repository.GetStats(member.Id);
                var row = ui.CreatePanel(roster, $"TeamMember_{member.Id}", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                AddLayout(row.gameObject, -1, 86);
                AddHorizontal(row, 12, 10);
                var memberName = AddText(row, Shorten(member.Nickname, 13), 20, FontStyle.Bold, member.Id == currentUser.Id ? theme.Gold : theme.Text, 86, TextAnchor.MiddleLeft);
                AddLayout(memberName.gameObject, 1.2f, -1);
                var memberLevel = AddText(row, $"Lv.{stats.Level}", 17, FontStyle.Bold, theme.Cyan, 86, TextAnchor.MiddleCenter);
                AddLayout(memberLevel.gameObject, 0.45f, -1);
                var memberTime = AddText(row, $"今週 {FormatMinutes(repository.GetApprovedMinutesThisWeek(member.Id))}", 16, FontStyle.Bold, theme.MutedText, 86, TextAnchor.MiddleRight);
                AddLayout(memberTime.gameObject, 0.75f, -1);
            }

            AddResponsivePagination(roster, "MemberTeam", members.Count, pageSize, teamPage, page =>
            {
                teamPage = page;
                ShowMemberTeam();
            });
            var rankingButton = ui.CreateButton(content, "OpenTeamRanking", "チームランキングを見る", theme.SecondaryButton, () =>
            {
                selectedRankingView = RankingView.TeamMember;
                ShowRanking();
            }, theme.Text);
            AddLayout(rankingButton.gameObject, -1, 56);
        }

        private void ShowCosmeticWardrobe()
        {
            EnsureEditorCosmeticPreview();
            ClearRoot();
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Home);
            AddHeader("キャラクター", "装いと武器を組み合わせる", ShowMemberTeam);
            var useFullWidth = UsesPortraitLayout() || UsesCompactLandscapeLayout();
            var scroll = CreateScrollPanel(
                root,
                "CosmeticWardrobePanel",
                new Vector2(useFullWidth ? 0.06f : 0.43f, 0.06f),
                new Vector2(useFullWidth ? 0.94f : 0.96f, 0.82f));
            AddText(scroll, "旅装を整える", 30, FontStyle.Bold, theme.Text, 44, TextAnchor.MiddleCenter);
            AddText(scroll, "所持している装備を選ぶと、ホームのHeroにもすぐ反映されます。", 17, FontStyle.Bold, theme.MutedText, 40, TextAnchor.MiddleCenter);

            if (cosmeticInventory == null)
            {
                AddCosmeticInventoryGate(scroll, ShowCosmeticWardrobe);
                return;
            }

            if (UsesConstrainedLayout())
            {
                BuildResponsiveCosmeticWardrobe(scroll);
                return;
            }

            var overview = CreateHudRow(scroll, "CosmeticPreviewRow", 224);
            AddMemberTinyHeroPreview(overview, currentUser.Nickname, repository.GetStats(currentUser.Id));
            var loadout = ui.CreatePanel(overview, "EquippedLoadout", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(loadout.gameObject, 1.18f, -1);
            AddVertical(loadout, 14, 5, TextAnchor.UpperLeft);
            AddText(loadout, "いまの装備", 20, FontStyle.Bold, theme.Gold, 28);
            foreach (var slot in CosmeticSlots())
            {
                AddText(loadout, $"{CosmeticSlotLabel(slot)}  {EquippedCosmeticLabel(slot)}", 15, FontStyle.Bold, theme.Text, 24);
            }

            if (!string.IsNullOrWhiteSpace(cosmeticFeedbackMessage))
            {
                AddFeedbackBanner(scroll, cosmeticFeedbackMessage, cosmeticFeedbackTone, 66);
            }

            AddSelectorRowCompact(
                scroll,
                CosmeticSlots(),
                selectedCosmeticSlot,
                slot =>
                {
                    selectedCosmeticSlot = slot;
                    ShowCosmeticWardrobe();
                },
                CosmeticSlotLabel);

            var catalog = (cosmeticInventory.Catalog ?? new List<CosmeticItemDto>())
                .Where(item => item != null && string.Equals(NormalizeCosmeticSlot(item.Slot), selectedCosmeticSlot, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => CosmeticRarityOrder(item.Rarity))
                .ThenBy(item => item.Label, StringComparer.Ordinal)
                .ToList();
            if (catalog.Count == 0)
            {
                AddText(scroll, "このカテゴリの旅装はまだありません。", 17, FontStyle.Bold, theme.MutedText, 54, TextAnchor.MiddleCenter);
            }
            else
            {
                foreach (var item in catalog)
                {
                    AddCosmeticCatalogRow(scroll, item);
                }
            }

            var related = CreateHudRow(scroll, "WardrobeRelatedMenus", 54);
            var openWish = ui.CreateButton(related, "WardrobeOpenWeaponWish", "武器祈願へ", theme.PrimaryButton, ShowWeaponWish, Color.white);
            AddLayout(openWish.gameObject, 1f, -1);
            var refresh = ui.CreateButton(related, "RefreshCosmeticInventory", "所持品を更新", theme.SecondaryButton, () => BeginCosmeticInventoryRefresh(ShowCosmeticWardrobe), theme.Text);
            refresh.interactable = !cosmeticRequestInFlight;
            AddLayout(refresh.gameObject, 1f, -1);
        }

        private void BuildResponsiveCosmeticWardrobe(Transform parent)
        {
            var equipped = ui.CreatePanel(parent, "ResponsiveEquippedLoadout", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(equipped.gameObject, -1, 76);
            AddHorizontal(equipped, 12, 10);
            var equippedHeading = AddText(equipped, "現在の旅装", 18, FontStyle.Bold, theme.Gold, 76, TextAnchor.MiddleLeft);
            AddLayout(equippedHeading.gameObject, 0.65f, -1);
            var equippedValue = AddText(equipped, $"{CosmeticSlotLabel(selectedCosmeticSlot)}  /  {Shorten(EquippedCosmeticLabel(selectedCosmeticSlot), 18)}", 17, FontStyle.Bold, theme.Text, 76, TextAnchor.MiddleRight);
            AddLayout(equippedValue.gameObject, 1.35f, -1);

            AddSelectorRowCompact(
                parent,
                CosmeticSlots(),
                selectedCosmeticSlot,
                slot =>
                {
                    selectedCosmeticSlot = slot;
                    wardrobePage = 0;
                    ShowCosmeticWardrobe();
                },
                CosmeticSlotLabel);

            if (!string.IsNullOrWhiteSpace(cosmeticFeedbackMessage))
            {
                AddFeedbackBanner(parent, cosmeticFeedbackMessage, cosmeticFeedbackTone, 62);
            }

            var catalog = (cosmeticInventory.Catalog ?? new List<CosmeticItemDto>())
                .Where(item => item != null && string.Equals(NormalizeCosmeticSlot(item.Slot), selectedCosmeticSlot, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => CosmeticRarityOrder(item.Rarity))
                .ThenBy(item => item.Label, StringComparer.Ordinal)
                .ToList();
            if (catalog.Count == 0)
            {
                AddText(parent, "このカテゴリの旅装はまだありません。", 17, FontStyle.Bold, theme.MutedText, 54, TextAnchor.MiddleCenter);
            }
            else
            {
                var pageSize = ResponsiveCollectionPageSize(1, 2, 6);
                wardrobePage = ClampPage(wardrobePage, catalog.Count, pageSize);
                foreach (var item in catalog.Skip(wardrobePage * pageSize).Take(pageSize))
                {
                    AddCosmeticCatalogRow(parent, item);
                }

                AddResponsivePagination(parent, "Wardrobe", catalog.Count, pageSize, wardrobePage, page =>
                {
                    wardrobePage = page;
                    ShowCosmeticWardrobe();
                });
            }

            var related = CreateHudRow(parent, "WardrobeRelatedMenus", 58);
            var wish = ui.CreateButton(related, "WardrobeOpenWeaponWish", "武器祈願へ", theme.PrimaryButton, ShowWeaponWish, Color.white);
            AddLayout(wish.gameObject, 1f, -1);
            var refresh = ui.CreateButton(related, "RefreshCosmeticInventory", "所持品を更新", theme.SecondaryButton, () => BeginCosmeticInventoryRefresh(ShowCosmeticWardrobe), theme.Text);
            refresh.interactable = !cosmeticRequestInFlight;
            AddLayout(refresh.gameObject, 1f, -1);
        }

        private void ShowWeaponWish()
        {
            EnsureEditorCosmeticPreview();
            ClearRoot();
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Home);
            AddHeader("武器祈願", "開発の積み重ねを旅の力へ", ShowMemberTeam);
            var useFullWidth = UsesPortraitLayout() || UsesCompactLandscapeLayout();
            var scroll = CreateScrollPanel(
                root,
                "WeaponWishPanel",
                new Vector2(useFullWidth ? 0.06f : 0.43f, 0.06f),
                new Vector2(useFullWidth ? 0.94f : 0.96f, 0.82f));
            AddDisplayText(scroll, "星巡りの武器祈願", UsesConstrainedLayout() ? 28 : 32, theme.Text, UsesConstrainedLayout() ? 40 : 48, TextAnchor.MiddleCenter);
            AddText(scroll, "承認済み開発で得た祈願石から、旅装を1点迎えます。", 16, FontStyle.Bold, theme.MutedText, UsesConstrainedLayout() ? 34 : 48, TextAnchor.MiddleCenter);

            if (cosmeticInventory == null)
            {
                AddCosmeticInventoryGate(scroll, ShowWeaponWish);
                return;
            }

            var ownedWeapons = (cosmeticInventory.Owned ?? new List<OwnedCosmeticDto>())
                .Where(owned => owned?.Item != null && string.Equals(NormalizeCosmeticSlot(owned.Item.Slot), "weapon", StringComparison.OrdinalIgnoreCase))
                .OrderBy(owned => CosmeticRarityOrder(owned.Item.Rarity))
                .ToList();
            AddWeaponWishRevealStage(scroll);

            var actionRow = CreateHudRow(scroll, "WeaponWishActions", 82);
            AddFrontDisplayMetric(actionRow, "所持祈願石", cosmeticInventory.AvailableCredits.ToString("N0"), theme.Gold, 26, "WishCreditCard");
            var draw = ui.CreateButton(actionRow, "RollWeaponWish", cosmeticRequestInFlight ? "星をたどっています…" : "祈願する  ×1", theme.PrimaryButton, TryRollCosmeticGacha, Color.white);
            draw.interactable = !cosmeticRequestInFlight && cosmeticInventory.AvailableCredits > 0;
            AddLayout(draw.gameObject, 1.7f, -1);

            if (!string.IsNullOrWhiteSpace(cosmeticFeedbackMessage))
            {
                AddFeedbackBanner(scroll, cosmeticFeedbackMessage, cosmeticFeedbackTone, 62);
            }

            var inventoryRow = ui.CreatePanel(scroll, "WeaponInventorySummary", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // Keep the inner wardrobe action above 44 px after the card's vertical
            // padding is applied at the 1366x768 WebGL canvas scale.
            AddLayout(inventoryRow.gameObject, -1, 70);
            AddHorizontal(inventoryRow, 12, 10);
            var rarest = ownedWeapons.FirstOrDefault();
            var inventorySummary = AddText(inventoryRow, rarest == null
                ? "所持武器はまだありません"
                : $"所持武器 {ownedWeapons.Count}種  ・  最高レア {CosmeticRarityLabel(rarest.Item.Rarity)}", 16, FontStyle.Bold, rarest == null ? theme.MutedText : CosmeticRarityColor(rarest.Item.Rarity), 64, TextAnchor.MiddleLeft);
            AddLayout(inventorySummary.gameObject, 1f, -1);
            var wardrobe = ui.CreateButton(inventoryRow, "WishOpenWardrobe", "所持品を見て装備", theme.SecondaryButton, () =>
            {
                selectedCosmeticSlot = "weapon";
                ShowCosmeticWardrobe();
            }, theme.Text);
            AddLayout(wardrobe.gameObject, 220f, -1);

            if (cosmeticInventory.AvailableCredits <= 0)
            {
                AddText(scroll, "祈願石は、開発ログが承認されると増えます。", 15, FontStyle.Bold, theme.Warning, 30, TextAnchor.MiddleCenter);
            }
        }

        private void AddWeaponWishRevealStage(Transform parent)
        {
            var result = lastCosmeticGachaResult;
            var rarity = result?.rarity ?? "rare";
            var accent = CosmeticRarityColor(rarity);
            var stage = ui.CreatePanel(parent, "WishResultCard", theme.NotificationPanel ?? theme.RaidPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(stage.gameObject, -1, UsesCompactLandscapeLayout() ? 148f : UsesPortraitLayout() ? 220f : 230f);
            AddHorizontal(stage, 18, 14);

            var visual = new GameObject("WishRewardVisual", typeof(RectTransform), typeof(Image));
            visual.transform.SetParent(stage, false);
            AddLayout(visual, UsesCompactLandscapeLayout() ? 190f : 220f, -1);
            var image = visual.GetComponent<Image>();
            image.sprite = theme.BattleIcon ?? theme.WaypointCrest ?? theme.HexBadge;
            image.preserveAspect = true;
            image.color = accent;
            image.raycastTarget = false;

            var copy = new GameObject("WishRewardCopy", typeof(RectTransform));
            copy.transform.SetParent(stage, false);
            AddLayout(copy, 1f, -1);
            AddVertical(copy.GetComponent<RectTransform>(), 0, 4, TextAnchor.MiddleLeft);
            AddText(copy.transform, result == null ? "NEXT WISH" : result.wasDuplicate ? "STAR ECHO" : "NEW JOURNEY GEAR", 14, FontStyle.Bold, accent, 28);
            AddDisplayText(copy.transform, result == null ? "星が旅装を選んでいます" : Shorten(result.label, 18), result == null ? 24 : 31, result == null ? theme.Text : accent, 48);
            AddText(copy.transform, result == null
                ? "祈願石1個で武器・衣装・装飾のいずれかを獲得"
                : $"{CosmeticRarityLabel(result.rarity)}  /  {CosmeticSlotLabel(result.slot)}  /  {(result.wasDuplicate ? $"所持 ×{result.quantity}" : "新規獲得")}", 16, FontStyle.Bold, theme.MutedText, 36);
        }

        private void AddCosmeticInventoryGate(Transform parent, UnityEngine.Events.UnityAction refreshAction)
        {
            var message = cosmeticRequestInFlight
                ? "旅装の記録を読み込んでいます。"
                : string.IsNullOrWhiteSpace(cosmeticFeedbackMessage)
                    ? "旅装の記録を読み込めませんでした。"
                    : cosmeticFeedbackMessage;
            AddText(parent, message, 18, FontStyle.Bold, cosmeticRequestInFlight ? theme.Cyan : theme.Warning, 62, TextAnchor.MiddleCenter);
            var retry = ui.CreateButton(parent, "RetryCosmeticInventory", "もう一度読み込む", theme.SecondaryButton, () => BeginCosmeticInventoryRefresh(refreshAction), theme.Text);
            retry.interactable = !cosmeticRequestInFlight;
            AddLayout(retry.gameObject, -1, 56);

            if (!cosmeticInventoryLoadAttempted && !cosmeticRequestInFlight)
            {
                BeginCosmeticInventoryRefresh(refreshAction);
            }
        }

        private void AddCosmeticCatalogRow(Transform parent, CosmeticItemDto item)
        {
            var owned = FindOwnedCosmetic(item.Id);
            var equipped = IsCosmeticEquipped(item.Id);
            var row = ui.CreatePanel(parent, $"Cosmetic_{item.Code}", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(row.gameObject, -1, 92);
            AddHorizontal(row, 14, 10);

            var description = new GameObject("Description", typeof(RectTransform), typeof(VerticalLayoutGroup));
            description.transform.SetParent(row, false);
            AddLayout(description, 1f, -1);
            AddVertical(description.GetComponent<RectTransform>(), 0, 2, TextAnchor.MiddleLeft);
            AddText(description.transform, item.Label, 19, FontStyle.Bold, owned != null ? theme.Text : theme.MutedText, 30);
            AddText(description.transform, $"{CosmeticRarityLabel(item.Rarity)}  /  {(owned != null ? $"所持 ×{owned.Quantity}" : "未所持")}", 14, FontStyle.Bold, CosmeticRarityColor(item.Rarity), 24);

            var buttonLabel = equipped ? "装備中" : owned != null ? "装備" : "未所持";
            var button = ui.CreateButton(row, $"Equip_{item.Code}", buttonLabel, equipped ? theme.PrimaryButton : theme.SecondaryButton, () => TryEquipCosmetic(item), equipped ? Color.white : theme.Text);
            button.interactable = owned != null && !equipped && !cosmeticRequestInFlight;
            AddLayout(button.gameObject, 132f, -1);
        }

        private void BeginCosmeticInventoryRefresh(UnityEngine.Events.UnityAction refreshAction)
        {
            if (cosmeticRequestInFlight || isNetworkBusy)
            {
                return;
            }

#if UNITY_EDITOR || (UNITY_WEBGL && DEVELOPMENT_BUILD)
            if (!Application.isPlaying || IsVisualPreviewActive())
            {
                EnsureEditorCosmeticPreview();
                refreshAction?.Invoke();
                return;
            }
#endif

            StartCoroutine(RefreshCosmeticInventory());
        }

        private IEnumerator RefreshCosmeticInventory()
        {
            cosmeticInventoryLoadAttempted = true;
            if (supabase is not { IsConfigured: true } || !supabase.HasSession)
            {
                loginErrorMessage = "旅装の記録へ接続できません。再ログインしてください。";
                ShowLogin();
                yield break;
            }

            cosmeticRequestInFlight = true;
            isNetworkBusy = true;
            CosmeticInventoryResponseDto response = null;
            yield return supabase.GetCosmeticInventory(result => response = result);
            isNetworkBusy = false;
            cosmeticRequestInFlight = false;
            if (ReturnToLoginIfRemoteSessionExpired(response))
            {
                yield break;
            }

            if (response?.Ok == true && response.Cosmetics != null)
            {
                cosmeticInventory = response.Cosmetics;
                cosmeticFeedbackMessage = string.Empty;
                ApplyCosmeticsToHomeHero();
            }
            else
            {
                cosmeticFeedbackMessage = RemoteErrorMessage("旅装の記録を読み込めませんでした。");
                cosmeticFeedbackTone = FeedbackTone.Danger;
            }

            RefreshVisibleCosmeticScreen();
        }

        private void TryEquipCosmetic(CosmeticItemDto item)
        {
            if (item == null || cosmeticRequestInFlight || isNetworkBusy || FindOwnedCosmetic(item.Id) == null)
            {
                return;
            }

#if UNITY_EDITOR || (UNITY_WEBGL && DEVELOPMENT_BUILD)
            if (!Application.isPlaying || IsVisualPreviewActive())
            {
                ApplyEditorCosmeticEquip(item);
                ShowCosmeticWardrobe();
                return;
            }
#endif

            StartCoroutine(EquipCosmeticRemote(item));
        }

        private IEnumerator EquipCosmeticRemote(CosmeticItemDto item)
        {
            cosmeticRequestInFlight = true;
            isNetworkBusy = true;
            EquipCosmeticResponseDto response = null;
            yield return supabase.EquipCosmetic(item.Id, result => response = result);
            isNetworkBusy = false;
            cosmeticRequestInFlight = false;
            if (ReturnToLoginIfRemoteSessionExpired(response))
            {
                yield break;
            }

            if (response?.Ok == true && response.Cosmetics != null)
            {
                cosmeticInventory = response.Cosmetics;
                cosmeticFeedbackMessage = $"{item.Label}を装備しました。ホームのHeroにも反映されています。";
                cosmeticFeedbackTone = FeedbackTone.Success;
                ApplyCosmeticsToHomeHero();
            }
            else
            {
                cosmeticFeedbackMessage = RemoteErrorMessage("装備を変更できませんでした。");
                cosmeticFeedbackTone = FeedbackTone.Danger;
            }

            RefreshVisibleCosmeticScreen();
        }

        private void TryRollCosmeticGacha()
        {
            if (cosmeticInventory == null || cosmeticInventory.AvailableCredits <= 0 ||
                cosmeticRequestInFlight || isNetworkBusy)
            {
                return;
            }

#if UNITY_EDITOR || (UNITY_WEBGL && DEVELOPMENT_BUILD)
            if (!Application.isPlaying || IsVisualPreviewActive())
            {
                ApplyEditorCosmeticGacha();
                ShowWeaponWish();
                return;
            }
#endif

            StartCoroutine(RollCosmeticGachaRemote());
        }

        private IEnumerator RollCosmeticGachaRemote()
        {
            var operationUserId = currentUser?.Id;
            if (string.IsNullOrWhiteSpace(operationUserId))
            {
                cosmeticFeedbackMessage = "再ログインしてから祈願してください。";
                cosmeticFeedbackTone = FeedbackTone.Danger;
                RefreshVisibleCosmeticScreen();
                yield break;
            }

            var idempotencyKey = CosmeticGachaPendingOperationStore.GetOrCreate(operationUserId);
            cosmeticRequestInFlight = true;
            isNetworkBusy = true;
            RollCosmeticGachaResponseDto response = null;
            yield return supabase.RollCosmeticGacha(idempotencyKey, result => response = result);
            isNetworkBusy = false;
            cosmeticRequestInFlight = false;
            if (ReturnToLoginIfRemoteSessionExpired(response))
            {
                yield break;
            }

            if (response?.Ok == true && response.GachaResult != null && response.Cosmetics != null)
            {
                CosmeticGachaPendingOperationStore.Complete(operationUserId, idempotencyKey);
                cosmeticInventory = response.Cosmetics;
                lastCosmeticGachaResult = response.GachaResult;
                cosmeticFeedbackMessage = response.GachaResult.wasDuplicate
                    ? $"{response.GachaResult.label}が重なり、所持数が{response.GachaResult.quantity}になりました。"
                    : $"{response.GachaResult.label}を獲得しました。";
                cosmeticFeedbackTone = FeedbackTone.Success;
            }
            else
            {
                cosmeticFeedbackMessage = response?.ErrorCode?.Contains("credit", StringComparison.OrdinalIgnoreCase) == true
                    ? "祈願石が足りません。開発ログの承認を待ちましょう。"
                    : RemoteErrorMessage("祈願の結果を確認できませんでした。同じ祈願として再確認できます。");
                cosmeticFeedbackTone = FeedbackTone.Danger;
            }

            RefreshVisibleCosmeticScreen();
        }

        private void RefreshVisibleCosmeticScreen()
        {
            if (root == null)
            {
                return;
            }

            var visibleScreen = root.GetComponentsInChildren<RectTransform>(true)
                .Select(rect => rect.name)
                .FirstOrDefault(name => string.Equals(name, "CosmeticWardrobePanel", StringComparison.Ordinal) ||
                                        string.Equals(name, "WeaponWishPanel", StringComparison.Ordinal));
            if (string.Equals(visibleScreen, "CosmeticWardrobePanel", StringComparison.Ordinal))
            {
                ShowCosmeticWardrobe();
            }
            else if (string.Equals(visibleScreen, "WeaponWishPanel", StringComparison.Ordinal))
            {
                ShowWeaponWish();
            }
        }

        private void ApplyCosmeticsToHomeHero()
        {
            neonCityBackdrop?.SetHomeHeroCosmetics(cosmeticInventory);
        }

        private void ResetCosmeticState()
        {
            cosmeticInventory = null;
            lastCosmeticGachaResult = null;
            selectedCosmeticSlot = "weapon";
            cosmeticFeedbackMessage = string.Empty;
            cosmeticFeedbackTone = FeedbackTone.Info;
            cosmeticInventoryLoadAttempted = false;
            cosmeticRequestInFlight = false;
            ApplyCosmeticsToHomeHero();
        }

        private void EnsureEditorCosmeticPreview()
        {
#if UNITY_EDITOR || (UNITY_WEBGL && DEVELOPMENT_BUILD)
            if ((!Application.isPlaying || IsVisualPreviewActive()) && cosmeticInventory == null)
            {
                cosmeticInventory = CreateEditorCosmeticPreview();
                cosmeticInventoryLoadAttempted = true;
            }
#endif
        }

#if UNITY_EDITOR || (UNITY_WEBGL && DEVELOPMENT_BUILD)
        private CosmeticInventoryDto CreateEditorCosmeticPreview()
        {
            var catalog = new List<CosmeticItemDto>
            {
                CosmeticPreviewItem("preview-weapon-bronze", "tinyhero_bronze_blade", "青銅の旅人剣", "weapon", "common", "tinyhero/weapon/bronze_blade"),
                CosmeticPreviewItem("preview-weapon-bow", "tinyhero_forest_bow", "翠風の弓", "weapon", "rare", "tinyhero/weapon/forest_bow"),
                CosmeticPreviewItem("preview-weapon-staff", "tinyhero_starlight_staff", "星巡りの杖", "weapon", "epic", "tinyhero/weapon/starlight_staff"),
                CosmeticPreviewItem("preview-weapon-sun", "tinyhero_suncrest_blade", "日輪の剣", "weapon", "legendary", "tinyhero/weapon/suncrest_blade"),
                CosmeticPreviewItem("preview-head", "tinyhero_scout_hood", "風読みのフード", "head", "common", "tinyhero/head/scout_hood"),
                CosmeticPreviewItem("preview-body", "tinyhero_guardian_coat", "蒼衛のコート", "body", "rare", "tinyhero/body/guardian_coat"),
                CosmeticPreviewItem("preview-back", "tinyhero_leaf_cape", "若葉のケープ", "back", "rare", "tinyhero/back/leaf_cape"),
                CosmeticPreviewItem("preview-accessory", "tinyhero_star_charm", "星雫のチャーム", "accessory", "epic", "tinyhero/accessory/star_charm")
            };
            var inventory = new CosmeticInventoryDto
            {
                AvailableCredits = 3,
                LifetimeEarned = 8,
                LifetimeSpent = 5,
                Version = 1,
                Catalog = catalog,
                Owned = catalog.Select(item => new OwnedCosmeticDto { Quantity = 1, Item = item }).ToList()
            };
            inventory.Equipped = new List<EquippedCosmeticLoadoutDto>
            {
                new() { Slot = "weapon", Item = catalog[0] },
                new() { Slot = "head", Item = catalog[4] },
                new() { Slot = "body", Item = catalog[5] },
                new() { Slot = "back", Item = catalog[6] },
                new() { Slot = "accessory", Item = catalog[7] }
            };
            return inventory;
        }

        private static CosmeticItemDto CosmeticPreviewItem(string id, string code, string label, string slot, string rarity, string assetKey)
        {
            return new CosmeticItemDto
            {
                Id = id,
                Code = code,
                Label = label,
                Description = string.Empty,
                Slot = slot,
                Rarity = rarity,
                UnityAssetKey = assetKey
            };
        }

        private void ApplyEditorCosmeticEquip(CosmeticItemDto item)
        {
            cosmeticInventory.Equipped ??= new List<EquippedCosmeticLoadoutDto>();
            var slot = NormalizeCosmeticSlot(item.Slot);
            cosmeticInventory.Equipped.RemoveAll(entry => entry != null && string.Equals(NormalizeCosmeticSlot(entry.Slot), slot, StringComparison.OrdinalIgnoreCase));
            cosmeticInventory.Equipped.Add(new EquippedCosmeticLoadoutDto { Slot = slot, Item = item });
            cosmeticInventory.Version++;
            cosmeticFeedbackMessage = $"{item.Label}を装備しました。";
            cosmeticFeedbackTone = FeedbackTone.Success;
            ApplyCosmeticsToHomeHero();
        }

        private void ApplyEditorCosmeticGacha()
        {
            var item = cosmeticInventory.Catalog.First(catalogItem => string.Equals(catalogItem.Code, "tinyhero_suncrest_blade", StringComparison.Ordinal));
            var owned = FindOwnedCosmetic(item.Id);
            var duplicate = owned != null;
            if (owned == null)
            {
                owned = new OwnedCosmeticDto { Quantity = 0, Item = item };
                cosmeticInventory.Owned.Add(owned);
            }

            owned.Quantity++;
            cosmeticInventory.AvailableCredits--;
            cosmeticInventory.LifetimeSpent++;
            cosmeticInventory.Version++;
            lastCosmeticGachaResult = new CosmeticGachaResultDto
            {
                itemId = item.Id,
                code = item.Code,
                label = item.Label,
                slot = item.Slot,
                rarity = item.Rarity,
                unityAssetKey = item.UnityAssetKey,
                wasDuplicate = duplicate,
                quantity = owned.Quantity,
                remainingCredits = cosmeticInventory.AvailableCredits
            };
            cosmeticFeedbackMessage = duplicate ? $"{item.Label}が重なりました。" : $"{item.Label}を獲得しました。";
            cosmeticFeedbackTone = FeedbackTone.Success;
        }
#endif

        private OwnedCosmeticDto FindOwnedCosmetic(string itemId)
        {
            return (cosmeticInventory?.Owned ?? new List<OwnedCosmeticDto>())
                .FirstOrDefault(owned => owned?.Item != null && string.Equals(owned.Item.Id, itemId, StringComparison.Ordinal));
        }

        private bool IsCosmeticEquipped(string itemId)
        {
            return (cosmeticInventory?.Equipped ?? new List<EquippedCosmeticLoadoutDto>())
                .Any(entry => entry?.Item != null && string.Equals(entry.Item.Id, itemId, StringComparison.Ordinal));
        }

        private string EquippedCosmeticLabel(string slot)
        {
            return (cosmeticInventory?.Equipped ?? new List<EquippedCosmeticLoadoutDto>())
                       .LastOrDefault(entry => entry?.Item != null && string.Equals(NormalizeCosmeticSlot(entry.Slot), NormalizeCosmeticSlot(slot), StringComparison.OrdinalIgnoreCase))
                       ?.Item.Label
                   ?? "標準装備";
        }

        private static string[] CosmeticSlots()
        {
            return new[] { "weapon", "head", "body", "back", "accessory" };
        }

        private static string NormalizeCosmeticSlot(string slot)
        {
            return slot?.Trim().ToLowerInvariant() switch
            {
                "mainhand" => "weapon",
                "main_hand" => "weapon",
                "main-hand" => "weapon",
                var normalized => normalized ?? string.Empty
            };
        }

        private static string CosmeticSlotLabel(string slot)
        {
            return NormalizeCosmeticSlot(slot) switch
            {
                "weapon" => "武器",
                "head" => "頭",
                "body" => "衣装",
                "back" => "背中",
                "accessory" => "装飾",
                _ => "旅装"
            };
        }

        private static int CosmeticRarityOrder(string rarity)
        {
            return rarity?.Trim().ToLowerInvariant() switch
            {
                "legendary" => 0,
                "epic" => 1,
                "rare" => 2,
                _ => 3
            };
        }

        private string CosmeticRarityLabel(string rarity)
        {
            return rarity?.Trim().ToLowerInvariant() switch
            {
                "legendary" => "伝説",
                "epic" => "希少",
                "rare" => "上質",
                _ => "通常"
            };
        }

        private Color CosmeticRarityColor(string rarity)
        {
            return rarity?.Trim().ToLowerInvariant() switch
            {
                "legendary" => theme.Gold,
                "epic" => new Color(0.78f, 0.66f, 0.96f, 1f),
                "rare" => theme.Cyan,
                _ => theme.Text
            };
        }

        private void ShowMemberHistory()
        {
            ClearRoot();
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Home);
            AddHeader("記録", "開発のあゆみ", ShowMemberHome);
            var useFullWidth = UsesPortraitLayout() || UsesCompactLandscapeLayout();
            var scroll = CreateScrollPanel(
                root,
                "MemberHistoryPanel",
                new Vector2(useFullWidth ? 0.06f : 0.43f, 0.06f),
                new Vector2(useFullWidth ? 0.94f : 0.96f, 0.82f));
            var history = devLogPresenter.Build(repository, currentUser, false, false).History;
            if (UsesConstrainedLayout())
            {
                BuildResponsiveMemberHistory(scroll, history.Select(item => item.Session).ToList());
                return;
            }

            AddText(scroll, $"最近の開発記録  {history.Count}件", 30, FontStyle.Bold, theme.Text, 46);
            if (history.Count == 0)
            {
                AddText(scroll, "まだ開発記録はありません。", 18, FontStyle.Bold, theme.MutedText, 44, TextAnchor.MiddleCenter);
            }
            else
            {
                foreach (var session in history)
                {
                    AddSessionSummary(scroll, session.Session, false);
                }
            }

            var related = CreateHudRow(scroll, "RecordRelatedMenus", 58);
            AddDashboardAction(related, "作品", theme.SecondaryButton, ShowProducts);
            AddDashboardAction(related, "実績", theme.SecondaryButton, ShowAchievements);
        }

        private void BuildResponsiveMemberHistory(Transform parent, IReadOnlyList<DevSession> history)
        {
            AddText(parent, $"最近の開発記録  {history.Count}件", 25, FontStyle.Bold, theme.Text, 38);
            if (history.Count == 0)
            {
                AddText(parent, "まだ開発記録はありません。", 18, FontStyle.Bold, theme.MutedText, 54, TextAnchor.MiddleCenter);
            }
            else
            {
                var pageSize = ResponsiveCollectionPageSize(1, 2, 8);
                historyPage = ClampPage(historyPage, history.Count, pageSize);
                foreach (var session in history.Skip(historyPage * pageSize).Take(pageSize))
                {
                    var card = ui.CreatePanel(parent, $"ResponsiveHistory_{session.Id}", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    AddLayout(card.gameObject, -1, 132);
                    AddVertical(card, 12, 4);
                    AddText(card, $"{StatusLabel(session.Status)}  /  {FormatMinutes(session.DurationMinutes)}  /  達成度 {session.AchievementRate}%", 19, FontStyle.Bold, StatusColor(session.Status), 30);
                    AddText(card, $"目標  {Shorten(session.Goal, 34)}", 17, FontStyle.Bold, theme.Text, 28);
                    var result = session.Evaluation == null
                        ? "AI評価を確認中"
                        : $"AI評価 {RankLabel(session.Evaluation.Rank)}  /  EXP +{Mathf.Max(session.PreviewExp, session.GrowthFeedback?.ExpGained ?? 0)}";
                    AddText(card, result, 16, FontStyle.Bold, session.Status == DevSessionStatus.Approved ? theme.Cyan : theme.MutedText, 28);
                }

                AddResponsivePagination(parent, "MemberHistory", history.Count, pageSize, historyPage, page =>
                {
                    historyPage = page;
                    ShowMemberHistory();
                });
            }

            var related = CreateHudRow(parent, "RecordRelatedMenus", 58);
            AddDashboardAction(related, "作品", theme.SecondaryButton, ShowProducts);
            AddDashboardAction(related, "実績", theme.SecondaryButton, ShowAchievements);
        }

        private string NextRaidLabel()
        {
            if (repository.ActiveBattle.IsActive)
            {
                return "開催中  ・  参加する";
            }

            var date = DateTime.Now.Date;
            var daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)date.DayOfWeek + 7) % 7;
            if (daysUntilSaturday == 0)
            {
                daysUntilSaturday = 7;
            }

            var nextRaid = date.AddDays(daysUntilSaturday);
            return $"{nextRaid:M月d日}（土）13:00";
        }

        private RectTransform CreateLocalMaskArea(
            Transform parent,
            string name,
            float x,
            float y,
            float width,
            float height,
            float parentWidth,
            float parentHeight)
        {
            var area = new GameObject(name, typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            area.SetParent(parent, false);
            SetLocalReferenceRect(area, x, y, width, height, parentWidth, parentHeight);
            return area;
        }

        private RectTransform CreateLocalImage(
            Transform parent,
            string name,
            Sprite sprite,
            float x,
            float y,
            float width,
            float height,
            float parentWidth,
            float parentHeight,
            Color color,
            bool preserveAspect)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = color;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            SetLocalReferenceRect(image.rectTransform, x, y, width, height, parentWidth, parentHeight);
            return image.rectTransform;
        }

        private Text AddLocalSingleLineText(
            Transform parent,
            string name,
            string value,
            int size,
            int minimumSize,
            FontStyle style,
            Color color,
            float x,
            float y,
            float width,
            float height,
            float parentWidth,
            float parentHeight,
            TextAnchor alignment)
        {
            var text = ui.CreateText(parent, name, value, size, style, color, alignment);
            ConfigureSingleLineLabel(text, size);
            text.resizeTextMinSize = Mathf.Clamp(minimumSize, 8, size);
            SetLocalReferenceRect(text.rectTransform, x, y, width, height, parentWidth, parentHeight);
            return text;
        }

        private RectTransform CreateReferenceImage(
            Transform parent,
            string name,
            Sprite sprite,
            float x,
            float y,
            float width,
            float height,
            Color color,
            bool preserveAspect)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = color;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            SetReferenceRect(image.rectTransform, x, y, width, height);
            return image.rectTransform;
        }

        private Text AddReferenceText(
            Transform parent,
            string name,
            string value,
            int size,
            FontStyle style,
            Color color,
            float x,
            float y,
            float width,
            float height,
            TextAnchor alignment)
        {
            var text = ui.CreateText(parent, name, value, size, style, color, alignment);
            DisableTextFitGuard(text);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(10, size - 4);
            text.resizeTextMaxSize = size;
            SetReferenceRect(text.rectTransform, x, y, width, height);
            return text;
        }

        private void AddMemberTinyHeroPreview(Transform parent, string nickname, CharacterStats stats)
        {
            var card = ui.CreatePanel(parent, "MemberTinyHeroPreview", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(card.gameObject, 0.82f, -1);
            AddVertical(card, 10, 4, TextAnchor.UpperCenter);
            AddText(card, "TEAM DEV", 14, FontStyle.Bold, theme.MutedText, 20, TextAnchor.MiddleCenter);

            var previewFrame = new GameObject("TinyHeroRenderFrame", typeof(RectTransform));
            previewFrame.transform.SetParent(card, false);
            AddLayout(previewFrame, -1, 132);
            var preview = new GameObject("TinyHeroRender", typeof(RectTransform), typeof(RawImage));
            preview.transform.SetParent(previewFrame.transform, false);
            var previewRect = preview.GetComponent<RectTransform>();
            previewRect.anchorMin = new Vector2(0.5f, 0.5f);
            previewRect.anchorMax = new Vector2(0.5f, 0.5f);
            previewRect.pivot = new Vector2(0.5f, 0.5f);
            previewRect.sizeDelta = new Vector2(132f, 132f);
            previewRect.anchoredPosition = Vector2.zero;
            var rawImage = preview.GetComponent<RawImage>();
            rawImage.texture = EnsureMemberPreviewTexture();
            rawImage.color = Color.white;
            rawImage.raycastTarget = false;

            var title = stats.Titles.Count > 0 ? stats.Titles[0] : "開発メンバー";
            AddText(card, $"{Shorten(nickname, 7)} / {title}", 15, FontStyle.Bold, theme.Text, 26, TextAnchor.MiddleCenter);
        }

        private void AddButtonOverlayLabel(Button button, string label, int fontSize)
        {
            AddButtonOverlayLabel(button, label, fontSize, theme.Text);
        }

        private Button AddBattleButtonCell(Transform parent, string name, string label, Sprite sprite, UnityEngine.Events.UnityAction onClick, Color labelColor, int fontSize, float flexibleWidth = 1f)
        {
            var cell = new GameObject($"{name}_Cell", typeof(RectTransform));
            cell.transform.SetParent(parent, false);
            AddLayout(cell, flexibleWidth, -1);
            cell.GetComponent<LayoutElement>().minWidth = ResolveMinimumUiLength(48f, 48f);

            var resolvedLabelColor = sprite == theme.PrimaryButton ? Color.white : labelColor;
            var button = ui.CreateButton(cell.transform, name, label, sprite, onClick, resolvedLabelColor);
            var buttonRect = button.GetComponent<RectTransform>();
            ui.Stretch(buttonRect, 0, 0, 0, 0);
            // Live Option-3 labels already convert their target pixel size through
            // NeonUiFactory.ResolveLiveFontSize. Passing an app-resolved size here
            // applied the WebGL canvas correction twice and made short command labels
            // overflow even on desktop. Baked labels still need the app-side scaling.
            var labelSize = theme.UseOption3LiveUi
                ? fontSize + 2
                : ResolveUiFontSize(fontSize + 2);
            ui.SetButtonBakedLabel(button, label, labelSize, resolvedLabelColor);
            return button;
        }

        private void AddButtonOverlayLabel(Button button, string label, int fontSize, Color color)
        {
            ui.SetButtonBakedLabel(button, label, fontSize, color);
        }

        private static void ConfigureFloatingLabel(Text labelText, int fontSize)
        {
            labelText.raycastTarget = false;
            labelText.horizontalOverflow = HorizontalWrapMode.Wrap;
            labelText.verticalOverflow = VerticalWrapMode.Truncate;
            labelText.resizeTextForBestFit = true;
            labelText.resizeTextMinSize = Mathf.Max(8, fontSize - 5);
            labelText.resizeTextMaxSize = fontSize;
            var labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
            labelLayout.ignoreLayout = true;
        }

        private void AddCatalogRail(string name, bool productsSelected)
        {
            var rail = ui.CreatePanel(EnsureRoot(), name, theme.LogPanel, new Vector2(0.04f, 0.13f), new Vector2(0.215f, 0.79f), Vector2.zero, Vector2.zero);
            AddVertical(rail, 14, 12, TextAnchor.UpperCenter);
            AddRailButton(rail, "プロダクト", productsSelected ? theme.PrimaryButton : theme.SecondaryButton, ShowProducts, productsSelected);
            AddRailButton(rail, "実績", productsSelected ? theme.SecondaryButton : theme.PrimaryButton, ShowAchievements, !productsSelected);
            AddSpacer(rail, 8);
            AddText(rail, productsSelected ? "公開URL" : "申請・報酬", 16, FontStyle.Bold, theme.MutedText, 26, TextAnchor.MiddleCenter);
        }

        private void AddRailButton(Transform parent, string label, Sprite sprite, UnityEngine.Events.UnityAction onClick, bool selected = false)
        {
            var button = ui.CreateButton(parent, $"Rail_{label}", label, sprite, onClick, selected ? Color.white : theme.Text);
            AddLayout(button.gameObject, -1, 58);
        }

        private void AddCatalogIcon(Transform parent, string name, Sprite icon, Color color)
        {
            var iconObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(parent, false);
            AddLayout(iconObject, 76, -1);
            var image = iconObject.GetComponent<Image>();
            image.sprite = icon != null ? icon : theme.HexBadge;
            image.preserveAspect = true;
            image.color = color;
            image.raycastTarget = false;
        }

        private void AddMiniHomeCard(Transform parent, string title, string detail, Color accentColor, UnityEngine.Events.UnityAction onClick)
        {
            var card = ui.CreatePanel(parent, $"MiniHome_{title}", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(card.gameObject, 1, -1);
            AddVertical(card, 8, 4);
            AddText(card, title, 21, FontStyle.Bold, theme.Text, 26);
            AddText(card, detail, 17, FontStyle.Bold, accentColor, 26);
            var button = ui.CreateButton(card, $"Open_{title}", "開く", theme.SecondaryButton, onClick);
            AddLayout(button.gameObject, -1, 34);
        }

        private void AddDashboardNavCard(Transform parent, string title, string detail, string status, Color statusColor, Sprite buttonSprite, UnityEngine.Events.UnityAction onClick)
        {
            var card = ui.CreatePanel(parent, $"DashboardNav_{title}", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(card.gameObject, 1, -1);
            AddVertical(card, 10, 2);
            AddText(card, title, 19, FontStyle.Bold, theme.Text, 22);
            AddText(card, detail, 13, FontStyle.Bold, theme.MutedText, 20);
            AddText(card, status, 16, FontStyle.Bold, statusColor, 22);
            var button = ui.CreateButton(card, $"Open_{title}", "開く", buttonSprite, onClick);
            AddLayout(button.gameObject, -1, 30);
        }

        private void AddDashboardHeroCard(
            Transform parent,
            string eyebrow,
            string title,
            string detail,
            Color accentColor,
            Sprite buttonSprite,
            UnityEngine.Events.UnityAction onClick,
            float flexibleWidth)
        {
            var card = ui.CreatePanel(parent, $"DashboardHero_{eyebrow}", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(card.gameObject, flexibleWidth, -1);
            AddVertical(card, 10, 3);
            AddText(card, eyebrow, 14, FontStyle.Bold, theme.MutedText, 18);
            AddText(card, title, 30, FontStyle.Bold, accentColor, 36);
            AddText(card, detail, 15, FontStyle.Bold, theme.Text, 21);
            var button = ui.CreateButton(card, $"HeroOpen_{eyebrow}", "開く", buttonSprite, onClick);
            AddLayout(button.gameObject, -1, 30);
        }

        private void AddCompactSessionPreview(Transform parent, DevSession session, UnityEngine.Events.UnityAction onOpen)
        {
            var row = ui.CreatePanel(parent, $"PendingPreview_{session.Id}", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(row.gameObject, -1, 58);
            AddHorizontal(row, 12, 10);

            var user = repository.Users.FirstOrDefault(item => item.Id == session.UserId);
            var name = user?.Nickname ?? "不明";
            var status = StatusLabel(session.Status);
            var nameText = ui.CreateText(row, $"PendingUser_{session.Id}", name, 20, FontStyle.Bold, theme.Text);
            AddLayout(nameText.gameObject, 130, -1);
            var statusText = ui.CreateText(row, $"PendingStatus_{session.Id}", $"{FormatMinutes(session.DurationMinutes)} / {status}", 18, FontStyle.Bold, StatusColor(session.Status));
            AddLayout(statusText.gameObject, 170, -1);
            var goal = ui.CreateText(row, $"PendingGoal_{session.Id}", Shorten(session.Goal, 24), 18, FontStyle.Normal, theme.MutedText);
            AddLayout(goal.gameObject, 1, -1);
            var button = ui.CreateButton(row, $"OpenPending_{session.Id}", "確認", theme.SecondaryButton, onOpen);
            AddLayout(button.gameObject, 112, -1);
        }

        private void AddDashboardMetric(Transform parent, string label, string value, Color valueColor)
        {
            var metric = ui.CreatePanel(parent, $"DashboardMetric_{label}", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(metric.gameObject, 1, -1);
            AddVertical(metric, 8, 0, TextAnchor.MiddleCenter);
            AddText(metric, label, 13, FontStyle.Bold, theme.MutedText, 20, TextAnchor.MiddleCenter);
            AddText(metric, value, 22, FontStyle.Bold, valueColor, 34, TextAnchor.MiddleCenter);
        }

        private void AddMentorStatusLine(Transform parent, string label, string value, string detail, Color valueColor)
        {
            var row = ui.CreatePanel(parent, $"MentorStatus_{label}", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(row.gameObject, -1, 52);
            AddHorizontal(row, 14, 12);

            var labelText = ui.CreateText(row, $"MentorStatus_{label}_Label", label, 15, FontStyle.Bold, theme.MutedText, TextAnchor.MiddleLeft);
            AddLayout(labelText.gameObject, 130, -1);

            var valueText = ui.CreateText(row, $"MentorStatus_{label}_Value", value, 24, FontStyle.Bold, valueColor, TextAnchor.MiddleLeft);
            AddLayout(valueText.gameObject, 210, -1);

            var detailText = ui.CreateText(row, $"MentorStatus_{label}_Detail", detail, 18, FontStyle.Bold, theme.MutedText, TextAnchor.MiddleLeft);
            AddLayout(detailText.gameObject, 1, -1);
        }

        private void AddProgress(Transform parent, float value01, bool magenta, float height)
        {
            var progress = ui.CreateProgressBar(parent, "Progress", value01, magenta);
            AddLayout(progress.gameObject, -1, height);
        }

        private void AddReadableProgress(Transform parent, float value01, bool magenta, float height, string label)
        {
            var row = ui.CreatePanel(
                parent,
                $"ReadableProgress_{parent.childCount}",
                theme.NotificationPanel != null ? theme.NotificationPanel : theme.StatCard,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            AddLayout(row.gameObject, -1, height);
            AddHorizontal(row, 10, 8);

            var caption = ui.CreateText(row, "ReadableProgressLabel", label, 14, FontStyle.Bold, theme.Text, TextAnchor.MiddleLeft);
            AddLayout(caption.gameObject, UsesCompactLandscapeLayout() ? 92f : 124f, -1);

            var progress = ui.CreateProgressBar(row, "ReadableProgressBar", Mathf.Clamp01(value01), magenta);
            AddLayout(progress.gameObject, 1f, -1);

            var percent = ui.CreateText(row, "ReadableProgressValue", $"{Mathf.RoundToInt(Mathf.Clamp01(value01) * 100f)}%", 14, FontStyle.Bold, magenta ? theme.Magenta : theme.Cyan, TextAnchor.MiddleRight);
            AddLayout(percent.gameObject, 54f, -1);
        }

        private void AddResponsivePaneTabs(
            Transform parent,
            string prefix,
            string primaryLabel,
            string secondaryLabel,
            bool primarySelected,
            UnityEngine.Events.UnityAction primaryAction,
            UnityEngine.Events.UnityAction secondaryAction)
        {
            var tabs = CreateHudRow(parent, $"{prefix}PaneTabs", 58f);
            var primary = ui.CreateButton(
                tabs,
                $"{prefix}ListTab",
                primaryLabel,
                primarySelected ? theme.PrimaryButton : theme.SecondaryButton,
                primaryAction,
                primarySelected ? Color.white : theme.Text);
            AddLayout(primary.gameObject, 1f, -1);

            var secondary = ui.CreateButton(
                tabs,
                $"{prefix}FormTab",
                secondaryLabel,
                primarySelected ? theme.SecondaryButton : theme.PrimaryButton,
                secondaryAction,
                primarySelected ? theme.Text : Color.white);
            AddLayout(secondary.gameObject, 1f, -1);
        }

        private void AddResponsivePagination(
            Transform parent,
            string prefix,
            int totalItems,
            int pageSize,
            int currentPage,
            Action<int> setPage)
        {
            var safePageSize = Mathf.Max(1, pageSize);
            var pageCount = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0, totalItems) / (float)safePageSize));
            if (pageCount <= 1)
            {
                return;
            }

            var safeCurrentPage = Mathf.Clamp(currentPage, 0, pageCount - 1);
            var pagination = CreateHudRow(parent, $"{prefix}Pagination", 56f);
            var previous = ui.CreateButton(
                pagination,
                $"{prefix}PreviousPage",
                "前へ",
                theme.SecondaryButton,
                () => setPage?.Invoke(Mathf.Max(0, safeCurrentPage - 1)),
                theme.Text);
            AddLayout(previous.gameObject, 0.8f, -1);
            previous.interactable = safeCurrentPage > 0;

            var indicator = ui.CreateText(
                pagination,
                $"{prefix}PageIndicator",
                $"{safeCurrentPage + 1} / {pageCount}",
                17,
                FontStyle.Bold,
                theme.Cyan,
                TextAnchor.MiddleCenter);
            AddLayout(indicator.gameObject, 1.15f, -1);

            var next = ui.CreateButton(
                pagination,
                $"{prefix}NextPage",
                "次へ",
                theme.SecondaryButton,
                () => setPage?.Invoke(Mathf.Min(pageCount - 1, safeCurrentPage + 1)),
                theme.Text);
            AddLayout(next.gameObject, 0.8f, -1);
            next.interactable = safeCurrentPage < pageCount - 1;
        }

        private RectTransform CreateHudRow(Transform parent, string name, float height)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            AddLayout(row, -1, height);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return row.GetComponent<RectTransform>();
        }

        private RectTransform CreateBattleAnchoredRow(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, int spacing)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            row.SetParent(parent, false);
            SetAnchored(row, anchorMin, anchorMax);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return row;
        }

        private RectTransform CreateBattleAnchoredColumn(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, int spacing)
        {
            var column = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            column.SetParent(parent, false);
            SetAnchored(column, anchorMin, anchorMax);
            var layout = column.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return column;
        }

        private void AddHudMetric(Transform parent, string label, string value, Color valueColor)
        {
            var metric = ui.CreatePanel(parent, $"HudMetric_{label}", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(metric.gameObject, 1, -1);
            AddVertical(metric, 8, 0, TextAnchor.MiddleCenter);
            AddText(metric, label, 12, FontStyle.Bold, theme.MutedText, 17, TextAnchor.MiddleCenter);
            AddText(metric, value, 20, FontStyle.Bold, valueColor, 28, TextAnchor.MiddleCenter);
        }

        private void AddBattleHudMetric(Transform parent, string label, string value, Color valueColor)
        {
            var metric = ui.CreatePanel(parent, $"BattleMetric_{label}", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(metric.gameObject, 1, -1);
            AddVertical(metric, 5, 0, TextAnchor.MiddleCenter);
            AddText(metric, label, 12, FontStyle.Bold, theme.MutedText, ResolveMinimumUiLength(18f, 14f), TextAnchor.MiddleCenter);
            AddText(metric, value, 18, FontStyle.Bold, valueColor, ResolveMinimumUiLength(28f, 22f), TextAnchor.MiddleCenter);
        }

        private void AddBattleBandMetric(Transform parent, string label, string value, Color valueColor)
        {
            var metric = ui.CreatePanel(parent, $"BattleBandMetric_{label}", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(metric.gameObject, 1, -1);
            AddAnchoredText(metric, $"BattleBandMetric_{label}_Label", label, 12, FontStyle.Bold, theme.MutedText, new Vector2(0.08f, 0.59f), new Vector2(0.92f, 0.86f), TextAnchor.MiddleCenter);
            AddAnchoredText(metric, $"BattleBandMetric_{label}_Value", value, 18, FontStyle.Bold, valueColor, new Vector2(0.08f, 0.14f), new Vector2(0.92f, 0.58f), TextAnchor.MiddleCenter);
        }

        private void AddBattleHudBacking(RectTransform panel)
        {
            if (panel == null)
            {
                return;
            }

            var fill = new GameObject("BattleHudBacking", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            fill.transform.SetParent(panel, false);
            var fillRect = fill.GetComponent<RectTransform>();
            ui.Stretch(fillRect, 10, 10, -10, -10);
            fill.GetComponent<LayoutElement>().ignoreLayout = true;
            var image = fill.GetComponent<Image>();
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = new Color(0.035f, 0.105f, 0.18f, 0.92f);
            image.raycastTarget = false;
            fill.transform.SetAsFirstSibling();

            var sheen = new GameObject("BattleHudSheen", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            sheen.transform.SetParent(panel, false);
            sheen.GetComponent<LayoutElement>().ignoreLayout = true;
            var sheenImage = sheen.GetComponent<Image>();
            sheenImage.color = new Color(0.10f, 0.58f, 0.82f, 0.075f);
            sheenImage.raycastTarget = false;
            SetAnchored(sheenImage.rectTransform, new Vector2(0.025f, 0.52f), new Vector2(0.975f, 0.90f));

            AddBattleHudAccent(panel, "BattleHudAccentTop", new Vector2(0.055f, 0.925f), new Vector2(0.945f, 0.94f), new Color(theme.Cyan.r, theme.Cyan.g, theme.Cyan.b, 0.58f));
            AddBattleHudAccent(panel, "BattleHudAccentBottom", new Vector2(0.08f, 0.055f), new Vector2(0.92f, 0.07f), new Color(theme.Gold.r, theme.Gold.g, theme.Gold.b, 0.46f));
            AddBattleHudAccent(panel, "BattleHudAccentLeft", new Vector2(0.018f, 0.18f), new Vector2(0.024f, 0.82f), new Color(theme.Gold.r, theme.Gold.g, theme.Gold.b, 0.32f));
            AddBattleHudAccent(panel, "BattleHudAccentRight", new Vector2(0.976f, 0.18f), new Vector2(0.982f, 0.82f), new Color(theme.Cyan.r, theme.Cyan.g, theme.Cyan.b, 0.28f));
        }

        private static void AddBattleHudAccent(RectTransform panel, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var accent = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            accent.transform.SetParent(panel, false);
            accent.GetComponent<LayoutElement>().ignoreLayout = true;
            var image = accent.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            accent.transform.SetAsLastSibling();
        }

        private void AddBattleSectionLabel(Transform parent, string label)
        {
            AddText(parent, label, 14, FontStyle.Bold, theme.Cyan, ResolveMinimumUiLength(22f, 18f));
        }

        private void AddBattleSignalLine(Transform parent, string message, FeedbackTone tone)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var panel = ui.CreatePanel(parent, $"BattleSignal_{tone}_{parent.childCount}", theme.NotificationPanel != null ? theme.NotificationPanel : theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(panel.gameObject, -1, ResolveMinimumUiLength(38f, 32f));
            AddHorizontal(panel, 10, 8);

            var accent = new GameObject("Accent", typeof(Image));
            accent.transform.SetParent(panel, false);
            AddLayout(accent, 7, -1);
            accent.GetComponent<Image>().color = FeedbackColor(tone);

            var label = $"{FeedbackHeading(tone)} / {Shorten(message, 54)}";
            var text = ui.CreateText(panel, "BattleSignalText", label, ResolveUiFontSize(13), FontStyle.Bold, theme.Text, TextAnchor.MiddleLeft);
            AddLayout(text.gameObject, 1, -1);
        }

        private void AddFrontDisplayMetric(Transform parent, string label, string value, Color valueColor, int valueSize, string semanticName = null)
        {
            var metric = ui.CreatePanel(
                parent,
                string.IsNullOrWhiteSpace(semanticName) ? $"FrontMetric_{label}" : semanticName,
                theme.NotificationPanel != null ? theme.NotificationPanel : theme.StatCard,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            AddLayout(metric.gameObject, 1, -1);
            AddVertical(metric, 8, 0, TextAnchor.MiddleCenter);
            AddText(metric, label, 15, FontStyle.Bold, theme.MutedText, 20, TextAnchor.MiddleCenter);
            AddText(metric, value, valueSize, FontStyle.Bold, valueColor, Mathf.Max(36, valueSize + 10), TextAnchor.MiddleCenter);
        }

        private void AddFeedbackBanner(Transform parent, string message, FeedbackTone tone, float height)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var usesNarrowAccountColumn = string.Equals(parent.name, "MentorAccountFormScrollContent", StringComparison.Ordinal);
            var resolvedHeight = Mathf.Max(height, usesNarrowAccountColumn ? 120f : 78f);
            var panel = ui.CreatePanel(parent, $"Feedback_{tone}_{parent.childCount}", theme.NotificationPanel != null ? theme.NotificationPanel : theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(panel.gameObject, -1, resolvedHeight);
            AddHorizontal(panel, 9, 12);

            var accent = new GameObject("Accent", typeof(Image));
            accent.transform.SetParent(panel, false);
            AddLayout(accent, 14, -1);
            accent.GetComponent<Image>().color = FeedbackColor(tone);

            var textBox = new GameObject("FeedbackText", typeof(RectTransform));
            textBox.transform.SetParent(panel, false);
            AddLayout(textBox, 1, -1);
            AddVertical(textBox.GetComponent<RectTransform>(), 0, 2);
            AddText(textBox.transform, FeedbackHeading(tone), 18, FontStyle.Bold, FeedbackColor(tone), 24);
            AddText(
                textBox.transform,
                Shorten(message, usesNarrowAccountColumn ? 24 : 36),
                16,
                FontStyle.Bold,
                theme.Text,
                resolvedHeight - 44f);

            var canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0.92f;
            StartCoroutine(AnimateFeedback(panel, canvasGroup));
        }

        private IEnumerator AnimateFeedback(RectTransform panel, CanvasGroup canvasGroup)
        {
            var startScale = panel.localScale;
            const float duration = 0.5f;
            var elapsed = 0f;
            while (elapsed < duration && panel != null && canvasGroup != null)
            {
                elapsed += Time.deltaTime;
                var pulse = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
                canvasGroup.alpha = Mathf.Lerp(0.92f, 1f, pulse);
                panel.localScale = startScale * (1f + pulse * 0.012f);
                yield return null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            if (panel != null)
            {
                panel.localScale = startScale;
            }
        }

        private Color FeedbackColor(FeedbackTone tone)
        {
            return tone switch
            {
                FeedbackTone.Success => theme.Cyan,
                FeedbackTone.Waiting => theme.Cyan,
                FeedbackTone.Warning => theme.Warning,
                FeedbackTone.Danger => theme.Danger,
                FeedbackTone.Battle => theme.Gold,
                _ => theme.MutedText
            };
        }

        private static string FeedbackHeading(FeedbackTone tone)
        {
            return tone switch
            {
                FeedbackTone.Success => "SUCCESS",
                FeedbackTone.Waiting => "PROCESSING",
                FeedbackTone.Warning => "NOTICE",
                FeedbackTone.Danger => "ERROR",
                FeedbackTone.Battle => "BATTLE RESULT",
                _ => "INFO"
            };
        }

        private void SetSessionFeedback(string message, FeedbackTone tone)
        {
            lastSessionMessage = message;
            lastSessionTone = tone;
        }

        private void SetProductFeedback(string message, FeedbackTone tone)
        {
            lastProductMessage = message;
            lastProductTone = tone;
        }

        private void SetAchievementFeedback(string message, FeedbackTone tone)
        {
            lastAchievementMessage = message;
            lastAchievementTone = tone;
        }

        private void SetMentorFeedback(string message, FeedbackTone tone)
        {
            lastMentorMessage = message;
            lastMentorTone = tone;
        }

        private void SetBattleFeedback(string message, FeedbackTone tone)
        {
            lastBattleMessage = message;
            lastBattleTone = tone;
        }

        private void ClearRetainedDevLogDrafts()
        {
            retainedGoalDraft = string.Empty;
            retainedReflectionDraft = string.Empty;
            retainedNextTaskDraft = string.Empty;
            retainedAchievementRate = 75f;
        }

        private void ClearRetainedProductDrafts()
        {
            retainedProductTitleDraft = string.Empty;
            retainedProductUrlDraft = string.Empty;
            retainedProductDescriptionDraft = string.Empty;
        }

        private void ClearRetainedAchievementDrafts()
        {
            retainedAchievementTitleDraft = string.Empty;
            retainedAchievementDescriptionDraft = string.Empty;
        }

        private void ClearRetainedMentorAccountDrafts()
        {
            retainedMemberLoginIdDraft = string.Empty;
            retainedMemberNicknameDraft = string.Empty;
            retainedMemberTeamIdDraft = null;
        }

        private void ClearAuthenticatedUiState()
        {
            // This player is intended for shared classroom browsers. User-authored drafts and
            // mentor feedback can contain private development notes or a newly issued temporary
            // password, so none of it may survive a logout/session-expiry boundary.
            retainedLoginId = string.Empty;
            ClearRetainedDevLogDrafts();
            ClearRetainedProductDrafts();
            ClearRetainedAchievementDrafts();
            ClearRetainedMentorAccountDrafts();
            lastSessionMessage = string.Empty;
            lastProductMessage = string.Empty;
            lastAchievementMessage = string.Empty;
            lastMentorMessage = string.Empty;
            lastBattleMessage = "メンターの開始待ち";
            CancelAiEvaluationSnapshotPolling();
            ClearTemporaryPasswordReveal();
        }

        private static FeedbackTone BattleFeedbackTone(BattleActionResult result)
        {
            if (result == null)
            {
                return FeedbackTone.Info;
            }

            return result.ActionType switch
            {
                BattleActionType.Support => FeedbackTone.Success,
                BattleActionType.Guard => FeedbackTone.Warning,
                _ => FeedbackTone.Battle
            };
        }

        private static string Shorten(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : $"{trimmed.Substring(0, Mathf.Max(0, maxLength - 1))}…";
        }

        private Text AddText(Transform parent, string text, int size, FontStyle style, Color color, float height, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            if (parent == null)
            {
                return null;
            }

            var displaySize = ResolveUiFontSize(size);
            var label = ui.CreateText(parent, $"Text_{parent.childCount}", text, displaySize, style, color, anchor);
            if (anchor != TextAnchor.UpperLeft && height <= displaySize * 1.8f)
            {
                ConfigureSingleLineLabel(label, displaySize);
            }

            AddLayout(label.gameObject, -1, height);
            return label;
        }

        private Text AddDisplayText(Transform parent, string text, int size, Color color, float height, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            if (parent == null)
            {
                return null;
            }

            var displaySize = ResolveUiFontSize(size);
            var label = ui.CreateDisplayText(parent, $"DisplayText_{parent.childCount}", text, displaySize, color, anchor);
            if (anchor != TextAnchor.UpperLeft && height <= displaySize * 1.8f)
            {
                ConfigureSingleLineLabel(label, displaySize);
            }

            AddLayout(label.gameObject, -1, height);
            return label;
        }

        private Text AddOverlayText(RectTransform parent, string text, int size, FontStyle style, Color color, float inset, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var displaySize = ResolveUiFontSize(size);
            var label = ui.CreateText(parent, $"OverlayText_{parent.childCount}", text, displaySize, style, color, anchor);
            ConfigureSingleLineLabel(label, displaySize);
            DisableTextFitGuard(label);
            var rect = label.rectTransform;
            ui.Stretch(rect, inset, inset, -inset, -inset);
            label.transform.SetAsLastSibling();
            return label;
        }

        private Text AddAnchoredText(RectTransform parent, string name, string text, int size, FontStyle style, Color color, Vector2 anchorMin, Vector2 anchorMax, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var displaySize = ResolveUiFontSize(size);
            var label = ui.CreateText(parent, name, text, displaySize, style, color, anchor);
            ConfigureSingleLineLabel(label, displaySize);
            SetAnchored(label.rectTransform, anchorMin, anchorMax);
            label.transform.SetAsLastSibling();
            return label;
        }

        private int ResolveUiFontSize(int size)
        {
            if (size <= 0)
            {
                return size;
            }

            var baseSize = Mathf.Max(10, Mathf.RoundToInt(size * DefaultUiTextScale));
            var scale = ResolveLoginUiScale();
            return Mathf.Clamp(Mathf.CeilToInt(baseSize / scale), baseSize, baseSize * 4);
        }

        private static void SetAnchored(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetReferenceRect(RectTransform rect, float x, float y, float width, float height)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(x / ReferenceUiWidth, 1f - (y + height) / ReferenceUiHeight);
            rect.anchorMax = new Vector2((x + width) / ReferenceUiWidth, 1f - y / ReferenceUiHeight);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetLocalReferenceRect(
            RectTransform rect,
            float x,
            float y,
            float width,
            float height,
            float parentWidth,
            float parentHeight)
        {
            if (rect == null || parentWidth <= 0f || parentHeight <= 0f)
            {
                return;
            }

            rect.anchorMin = new Vector2(x / parentWidth, 1f - (y + height) / parentHeight);
            rect.anchorMax = new Vector2((x + width) / parentWidth, 1f - y / parentHeight);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRelativeRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ConfigureSingleLineLabel(Text labelText, int fontSize)
        {
            if (labelText == null)
            {
                return;
            }

            labelText.horizontalOverflow = HorizontalWrapMode.Wrap;
            labelText.verticalOverflow = VerticalWrapMode.Truncate;
            labelText.resizeTextForBestFit = true;
            labelText.resizeTextMinSize = Mathf.Max(8, Mathf.RoundToInt(fontSize * 0.62f));
            labelText.resizeTextMaxSize = fontSize;
        }

        private static void PreserveFullSingleLineLabel(Text labelText, int minimumFontSize)
        {
            if (labelText == null)
            {
                return;
            }

            // UiTextFitGuard is intentionally conservative for arbitrary user copy,
            // but critical headings and compact metric values must shrink as a
            // complete label instead of being replaced by a meaningless ellipsis.
            ConfigureSingleLineLabel(labelText, labelText.fontSize);
            var canvas = labelText.GetComponentInParent<Canvas>();
            var canvasScale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            var minimumFontUnits = Mathf.CeilToInt(Mathf.Max(8f, minimumFontSize) / canvasScale);
            labelText.resizeTextMinSize = Mathf.Clamp(minimumFontUnits, 8, labelText.fontSize);
            var guard = labelText.GetComponent<UiTextFitGuard>();
            if (guard == null)
            {
                guard = labelText.gameObject.AddComponent<UiTextFitGuard>();
            }

            guard.ConfigurePreserveFullText(labelText.resizeTextMinSize);
        }

        private static void DisableTextFitGuard(Text text)
        {
            if (text == null)
            {
                return;
            }

            var guard = text.GetComponent<UiTextFitGuard>();
            if (guard == null)
            {
                return;
            }

            guard.enabled = false;
            if (Application.isPlaying)
            {
                Destroy(guard);
            }
            else
            {
                DestroyImmediate(guard);
            }
        }

        private static void AddVertical(RectTransform rect, int padding, int spacing, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            if (rect == null)
            {
                return;
            }

            var layout = GetOrAddLayoutGroup<VerticalLayoutGroup>(rect);
            if (layout == null)
            {
                return;
            }

            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = alignment;
        }

        private static void AddHorizontal(RectTransform rect, int padding, int spacing)
        {
            if (rect == null)
            {
                return;
            }

            var layout = GetOrAddLayoutGroup<HorizontalLayoutGroup>(rect);
            if (layout == null)
            {
                return;
            }

            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
        }

        private static T GetOrAddLayoutGroup<T>(RectTransform rect) where T : LayoutGroup
        {
            var existing = rect.GetComponent<LayoutGroup>();
            if (existing is T typed)
            {
                return typed;
            }

            return existing == null ? rect.gameObject.AddComponent<T>() : null;
        }

        private void AddSpacer(Transform parent, float height)
        {
            if (parent == null)
            {
                return;
            }

            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(parent, false);
            AddLayout(spacer, -1, height);
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private void AddLayout(GameObject target, float flexibleWidth, float preferredHeight)
        {
            if (target == null)
            {
                return;
            }

            var layout = target.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = target.AddComponent<LayoutElement>();
            }

            if (flexibleWidth > 10f)
            {
                layout.preferredWidth = flexibleWidth;
                layout.flexibleWidth = 0f;
            }
            else if (flexibleWidth > 0f)
            {
                layout.flexibleWidth = flexibleWidth;
            }

            if (flexibleWidth > 0f && target.GetComponent<Selectable>() != null)
            {
                // Horizontal layout groups are allowed to shrink preferred
                // widths toward zero. Give every interactive child a real
                // physical floor so compact WebGL and phone layouts never turn
                // icon or action buttons into sub-44 px slivers.
                layout.minWidth = Mathf.Max(layout.minWidth, ResolveMinimumUiLength(44f, 44f));
            }

            var resolvedPreferredHeight = preferredHeight;
            if (preferredHeight > 0f)
            {
                var inScrollContent = target.GetComponentInParent<ScrollRect>() != null;
                if (inScrollContent)
                {
                    var minimumPixels = target.GetComponent<Selectable>() != null
                        ? Mathf.Max(preferredHeight, 48f)
                        : preferredHeight;
                    resolvedPreferredHeight = ResolveMinimumUiLength(preferredHeight, minimumPixels);
                }
                else if (target.GetComponent<Selectable>() != null)
                {
                    resolvedPreferredHeight = ResolveMinimumUiLength(preferredHeight, 48f);
                }

                layout.preferredHeight = resolvedPreferredHeight;
            }
            layout.minHeight = resolvedPreferredHeight > 0f
                ? target.GetComponent<Selectable>() != null
                    ? Mathf.Min(resolvedPreferredHeight, ResolveMinimumUiLength(48f, 48f))
                    : Mathf.Min(resolvedPreferredHeight, 48f)
                : 0f;
        }

        private static string FormatMinutes(int minutes)
        {
            return $"{minutes / 60}:{minutes % 60:00}";
        }

        private static string ProductionSceneLabel(RasshiineProductionScene scene)
        {
            return scene switch
            {
                RasshiineProductionScene.Login => "ログイン",
                RasshiineProductionScene.MemberHome => "ホーム",
                RasshiineProductionScene.DevLog => "開発ログ",
                RasshiineProductionScene.MentorDashboard => "メンターダッシュボード",
                RasshiineProductionScene.Battle => "ボス戦",
                RasshiineProductionScene.FrontDisplay => "全体画面",
                _ => scene.ToString()
            };
        }

        private static string StatusLabel(DevSessionStatus status)
        {
            return status switch
            {
                DevSessionStatus.InProgress => "進行中",
                DevSessionStatus.Pending => "承認待ち",
                DevSessionStatus.Approved => "承認済み",
                DevSessionStatus.Rejected => "却下",
                DevSessionStatus.Incomplete => "未完了",
                DevSessionStatus.NeedsReview => "要確認",
                DevSessionStatus.AiPending => "AI評価待ち",
                _ => status.ToString()
            };
        }

        private static string ReviewFilterLabel(DevSessionReviewFilter filter)
        {
            return filter switch
            {
                DevSessionReviewFilter.Pending => "承認待ち",
                DevSessionReviewFilter.NeedsReview => "要確認",
                DevSessionReviewFilter.AiPending => "AI評価待ち",
                _ => "すべて"
            };
        }

        private static string InitialPasswordStateLabel(UserProfile user)
        {
            if (!user.IsActive)
            {
                return "停止中";
            }

            return user.InitialPasswordChanged ? "初期PW変更済み" : "初期PW未変更";
        }

        private static string UserRoleLabel(UserRole role)
        {
            return role == UserRole.Mentor ? "メンター" : "メンバー";
        }

        private string BuildAuditLogLine(AuditLogEntry log)
        {
            var actor = repository.Users.FirstOrDefault(user => user.Id == log.ActorUserId)?.Nickname ?? "system";
            var time = log.CreatedAtUtc == default ? string.Empty : $"{log.CreatedAtUtc.ToLocalTime():M/d HH:mm}";
            return $"{time} / {AuditActionLabel(log.ActionType)} / {log.TargetType}:{ShortId(log.TargetId)} / {actor}";
        }

        private static string AuditActionLabel(string actionType)
        {
            return actionType switch
            {
                "session.approve" => "開発ログ承認",
                "session.correction_approve" => "修正承認",
                "session.reject" => "開発ログ却下",
                "achievement.approve" => "実績承認",
                "achievement.reject" => "実績却下",
                "product.hide" => "作品非表示",
                "account.create" => "アカウント発行",
                "account.temporary_password_issue" => "一時PW発行",
                "account.initial_password_change" => "初期PW変更",
                _ => actionType
            };
        }

        private static string ShortId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= 8)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, 8);
        }

        private string BuildReviewQueueSummary()
        {
            var all = repository.GetPendingSessions();
            return $"すべて {all.Count}件  /  承認待ち {repository.GetPendingSessions(DevSessionReviewFilter.Pending).Count}件  /  要確認 {repository.GetPendingSessions(DevSessionReviewFilter.NeedsReview).Count}件  /  AI評価待ち {repository.GetPendingSessions(DevSessionReviewFilter.AiPending).Count}件  /  未完了 {all.Count(session => session.Status == DevSessionStatus.Incomplete)}件";
        }

        private string BuildSessionReviewDetail(DevSession session)
        {
            var startedAt = session.StartedAtUtc == default ? string.Empty : $"{session.StartedAtUtc.ToLocalTime():M/d HH:mm}";
            return session.Status switch
            {
                DevSessionStatus.NeedsReview => $"不審ログフラグ確認: {string.Join(", ", session.SuspiciousFlags)} / {startedAt}",
                DevSessionStatus.AiPending => $"AI評価未完了: {session.AiEvaluationFailureReason} / 承認時は暫定評価を反映 / {startedAt}",
                DevSessionStatus.Incomplete => $"3時間超過の未完了ログ。修正承認で時間と達成度を補正 / {startedAt}",
                DevSessionStatus.Pending => $"AI評価済み。承認で正式EXPへ反映 / {startedAt}",
                _ => startedAt
            };
        }

        private string BuildMentorCommentLine(DevSession session)
        {
            if (string.IsNullOrWhiteSpace(session.MentorComment))
            {
                return string.Empty;
            }

            var mentorName = repository.Users.FirstOrDefault(user => user.Id == session.ApprovedBy)?.Nickname ?? "メンター";
            var reviewedAt = session.ApprovedAtUtc.HasValue ? $" / {session.ApprovedAtUtc.Value.ToLocalTime():M/d HH:mm}" : string.Empty;
            var label = session.Status switch
            {
                DevSessionStatus.Approved => "承認コメント",
                DevSessionStatus.Rejected => "却下コメント",
                _ => "メンターコメント"
            };
            return $"{label}: {session.MentorComment} / {mentorName}{reviewedAt}";
        }

        private static string BuildGrowthFeedbackMessage(string nickname, DevSession session)
        {
            var growth = session.GrowthFeedback?.Summary ?? $"EXP +{session.PreviewExp}";
            return $"{nickname} のログを承認しました。{growth} / 戦力へ反映済みです。";
        }

        private Color StatusColor(DevSessionStatus status)
        {
            return status switch
            {
                DevSessionStatus.Pending => theme.Warning,
                DevSessionStatus.NeedsReview => theme.Warning,
                DevSessionStatus.AiPending => theme.Cyan,
                DevSessionStatus.Incomplete => theme.Warning,
                DevSessionStatus.Approved => theme.Cyan,
                DevSessionStatus.Rejected => theme.Danger,
                _ => theme.Text
            };
        }

        private static string RankLabel(AiRank rank)
        {
            return rank == AiRank.APlus ? "A+" : rank.ToString();
        }

        private static int ReadReviewInt(InputField input, int fallback, int min, int max)
        {
            return int.TryParse(input.text, out var value) ? Mathf.Clamp(value, min, max) : fallback;
        }

        private static string ReadReviewComment(InputField input, string fallback)
        {
            return string.IsNullOrWhiteSpace(input.text) ? fallback : input.text.Trim();
        }

        private static string RankingPeriodLabel(RankingPeriod period)
        {
            return period switch
            {
                RankingPeriod.Hourly => "毎時",
                RankingPeriod.Weekly => "週間",
                RankingPeriod.Term => "期内",
                RankingPeriod.AllTime => "全期間",
                _ => period.ToString()
            };
        }

        private static string RankingViewLabel(RankingView view)
        {
            return view switch
            {
                RankingView.Overall => "全体",
                RankingView.TeamMember => "班内",
                RankingView.Team => "班別",
                _ => view.ToString()
            };
        }

        private static string RankingKindLabel(RankingKind kind)
        {
            return kind switch
            {
                RankingKind.DevelopmentTime => "開発時間",
                RankingKind.BattleDamage => "ダメージ",
                _ => kind.ToString()
            };
        }

        private static string BattleStatusLabel(BattleStatus status)
        {
            return status switch
            {
                BattleStatus.Scheduled => "開始待ち",
                BattleStatus.Active => "開催中",
                BattleStatus.Completed => "終了",
                _ => status.ToString()
            };
        }

        private static string BattlePhaseLabel(BattlePhase phase)
        {
            return phase switch
            {
                BattlePhase.TurnStart => "TURN START",
                BattlePhase.ActionSelect => "COMMAND",
                BattlePhase.Resolving => "RESOLVE",
                BattlePhase.Result => "RESULT",
                BattlePhase.Completed => "COMPLETE",
                _ => phase.ToString()
            };
        }

        private static string BattleActionShortLabel(BattleActionType actionType)
        {
            return actionType switch
            {
                BattleActionType.Strong => "STRONG",
                BattleActionType.FullPower => "FULL POWER",
                BattleActionType.Support => "SUPPORT",
                BattleActionType.Guard => "GUARD",
                _ => "NORMAL"
            };
        }

        private static string RoleLabel(BattleRole role)
        {
            return role switch
            {
                BattleRole.Attacker => "アタッカー",
                BattleRole.Healer => "ヒーラー",
                BattleRole.Defender => "ディフェンダー",
                BattleRole.Supporter => "サポーター",
                _ => role.ToString()
            };
        }

        private static string RoleShortLabel(BattleRole role)
        {
            return role switch
            {
                BattleRole.Attacker => "攻撃",
                BattleRole.Healer => "回復",
                BattleRole.Defender => "防御",
                BattleRole.Supporter => "支援",
                _ => role.ToString()
            };
        }

        private static string RankingVisibilityLabel(bool rankingVisible)
        {
            return rankingVisible ? "ランキング表示" : "ランキング非表示";
        }

        private static string WeaponLabel(WeaponKind weapon)
        {
            return weapon switch
            {
                WeaponKind.Blade => "ブレード",
                WeaponKind.Rifle => "ライフル",
                WeaponKind.Cannon => "キャノン",
                WeaponKind.Shield => "シールド",
                WeaponKind.DebugTool => "デバッグ",
                WeaponKind.ReleaseGear => "リリース",
                WeaponKind.ContestGear => "コンテスト",
                _ => weapon.ToString()
            };
        }

        private static string WeaponShortLabel(WeaponKind weapon)
        {
            return weapon switch
            {
                WeaponKind.Blade => "ブレ",
                WeaponKind.Rifle => "ライ",
                WeaponKind.Cannon => "キャ",
                WeaponKind.Shield => "盾",
                WeaponKind.DebugTool => "デバ",
                WeaponKind.ReleaseGear => "リリ",
                WeaponKind.ContestGear => "コン",
                _ => weapon.ToString()
            };
        }

        private static string AchievementTypeLabel(AchievementType type)
        {
            return type switch
            {
                AchievementType.ContestSubmission => "大会提出",
                AchievementType.Release => "リリース",
                AchievementType.Update => "アップデート",
                AchievementType.Award => "大会受賞",
                AchievementType.ContinuousDev => "継続開発",
                _ => type.ToString()
            };
        }

        private static string AchievementStatusLabel(AchievementStatus status)
        {
            return status switch
            {
                AchievementStatus.Pending => "承認待ち",
                AchievementStatus.Approved => "承認済み",
                AchievementStatus.Rejected => "却下",
                _ => status.ToString()
            };
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.SetActive(false);
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            eventSystem.SetActive(true);
            DontDestroyOnLoad(eventSystem);
        }
    }
}
