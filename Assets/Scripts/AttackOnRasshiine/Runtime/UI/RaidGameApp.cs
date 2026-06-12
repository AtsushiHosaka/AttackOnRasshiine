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

namespace AttackOnRasshiine.Runtime.UI
{
    public sealed class RaidGameApp : MonoBehaviour
    {
        private const float BattleStatePollIntervalSeconds = 5f;
        private const float InitialBattleStatePollDelaySeconds = 0.5f;
        private const string DefaultBackLabel = "戻る";
        private const string SettingsLabel = "設定";
#if UNITY_EDITOR
        private const string EditorPreviewEnabledKey = "AttackOnRasshiine.EditorPreview.Enabled";
        private const string EditorPreviewLoginIdKey = "AttackOnRasshiine.EditorPreview.LoginId";
#endif

        [SerializeField] private RasshiineTheme theme;
        [SerializeField] private RaidBattleController battleController;
        [SerializeField] private AnimatedSkybox animatedSkybox;
        [SerializeField] private NeonCityBackdrop neonCityBackdrop;

        private LocalGameRepository repository;
        private SupabaseGameClient supabase;
        private RasshiineSceneRouter sceneRouter;
        private DevLogPresenter devLogPresenter;
        private NeonUiFactory ui;
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
        private bool pollingFrontDisplaySnapshot;
        private RasshiineProductionScene activeProductionScene = RasshiineProductionScene.Login;
        private RasshiineProductionScene settingsReturnScene = RasshiineProductionScene.Login;

        private InputField loginIdInput;
        private InputField passwordInput;
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

            repository = new LocalGameRepository();
            supabase = new SupabaseGameClient();
            devLogPresenter = new DevLogPresenter();
            supabase.RestoreSessionToken(RasshiineRuntimeSession.SessionToken);
            if (RasshiineRuntimeSession.Snapshot != null)
            {
                repository.ApplySnapshot(RasshiineRuntimeSession.Snapshot);
            }

            currentUser = RasshiineRuntimeSession.CurrentUser;
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

            battleController?.LoadBattle(repository.ActiveBattle);
            ShowStartupScene();
            StartCoroutine(LoadSupabaseConfig());
        }

        private void OnDestroy()
        {
            StopBattleStatePolling();
        }

        private IEnumerator LoadSupabaseConfig()
        {
            yield return supabase.LoadConfig();
        }

#if UNITY_EDITOR
        private void TryApplyEditorPreviewUser()
        {
            if (!Application.isEditor || !EditorPrefs.GetBool(EditorPreviewEnabledKey, false))
            {
                return;
            }

            EditorPrefs.SetBool(EditorPreviewEnabledKey, false);
            var previewLoginId = EditorPrefs.GetString(EditorPreviewLoginIdKey, "mentor1");
            var previewUser = repository.Users.FirstOrDefault(user => string.Equals(user.LoginId, previewLoginId, StringComparison.OrdinalIgnoreCase));
            if (previewUser == null)
            {
                Debug.LogWarning($"AttackOnRasshiine editor preview user was not found: {previewLoginId}");
                return;
            }

            currentUser = previewUser;
            RasshiineRuntimeSession.SetUser(previewUser);
            RasshiineRuntimeSession.SetSnapshot(repository.CreateSnapshot());
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
            battleController?.LoadBattle(repository.ActiveBattle);
            if (currentUser != null)
            {
                battleController?.SetControlledParticipant(currentUser.Role == UserRole.Member && repository.ActiveBattle.IsActive ? currentUser.Id : null);
            }
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
            ui.Clear(EnsureRoot());
        }

        private void SetBackdrop(NeonCityBackdrop.BackdropPreset preset)
        {
            neonCityBackdrop?.SetPreset(preset);
        }

        private void MarkScene(RasshiineProductionScene scene)
        {
            activeProductionScene = scene;
            sceneRouter?.SetCurrentScene(scene);
            ConfigureBattleStatePolling(scene);
        }

        private void ConfigureBattleStatePolling(RasshiineProductionScene scene)
        {
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

            StopBattleStatePolling();
            pollingFrontDisplaySnapshot = useFrontDisplaySnapshot;
            battleStatePollingRoutine = StartCoroutine(PollBattleState(useFrontDisplaySnapshot));
        }

        private void StopBattleStatePolling()
        {
            if (battleStatePollingRoutine == null)
            {
                return;
            }

            StopCoroutine(battleStatePollingRoutine);
            battleStatePollingRoutine = null;
        }

        private IEnumerator PollBattleState(bool useFrontDisplaySnapshot)
        {
            yield return new WaitForSeconds(InitialBattleStatePollDelaySeconds);
            while (true)
            {
                if (supabase is { IsConfigured: true } && !isNetworkBusy)
                {
                    yield return RefreshRemoteSnapshot(null, useFrontDisplaySnapshot);
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

        private void ShowLogin()
        {
            MarkScene(RasshiineProductionScene.Login);
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Login);
            currentUser = null;
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
            var titlePanel = ui.CreatePanel(root, "LoginTitle", theme.RaidPanel, new Vector2(0.18f, 0.80f), new Vector2(0.82f, 0.95f), Vector2.zero, Vector2.zero);
            AddVertical(titlePanel, 8, 0, TextAnchor.MiddleCenter);
            AddText(titlePanel, "Attack On Rasshiine", 48, FontStyle.Bold, theme.Text, 62, TextAnchor.MiddleCenter);
            AddText(titlePanel, "開発ログとボス戦をつなぐ運用ハブ", 18, FontStyle.Bold, theme.Cyan, 28, TextAnchor.MiddleCenter);

            var panel = ui.CreatePanel(root, "LoginPanel", theme.RaidPanel, new Vector2(0.28f, 0.12f), new Vector2(0.72f, 0.76f), Vector2.zero, Vector2.zero);
            AddVertical(panel, 24, 12, TextAnchor.UpperCenter);

            AddText(panel, "ログイン", 32, FontStyle.Bold, theme.Text, 46, TextAnchor.MiddleCenter);

            loginIdInput = ui.CreateInput(panel, "LoginIdInput", "ログインID");
            AddLayout(loginIdInput.gameObject, -1, 66);
            passwordInput = ui.CreateInput(panel, "PasswordInput", "パスワード");
            passwordInput.contentType = InputField.ContentType.Password;
            AddLayout(passwordInput.gameObject, -1, 66);

            if (!string.IsNullOrEmpty(loginErrorMessage))
            {
                AddText(panel, loginErrorMessage, 20, FontStyle.Bold, theme.Magenta, 34, TextAnchor.MiddleCenter);
            }

            var loginButton = ui.CreateButton(panel, "Login", "ログイン", theme.PrimaryButton, () =>
            {
                TryLogin(loginIdInput.text, passwordInput.text);
            });
            AddLayout(loginButton.gameObject, -1, 70);

            AddText(panel, "運用サマリ", 22, FontStyle.Bold, theme.MutedText, 30, TextAnchor.MiddleCenter);
            var metrics = CreateHudRow(panel, "LoginPreviewMetrics", 82);
            AddHudMetric(metrics, "TEAM DEV", FormatMinutes(repository.GetTotalApprovedMinutes()), theme.Cyan);
            AddHudMetric(metrics, "承認待ち", $"{repository.GetPendingSessions().Count}件", theme.Magenta);
            AddHudMetric(metrics, "BOSS HP", $"{repository.ActiveBattle.Boss.CurrentHp:N0}", theme.Mint);

        }

        private void TryLogin(string loginId, string password)
        {
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

            loginErrorMessage = RemoteErrorMessage("本番APIに接続できません。設定と通信状態を確認してください。");
            ShowLogin();
        }

        private void TryLocalLogin(string loginId, string password)
        {
            var user = repository.Authenticate(loginId, password);
            if (user == null)
            {
                loginErrorMessage = "IDまたはパスワードが違います";
                ShowLogin();
                return;
            }

            CompleteLogin(user, password);
        }

        private IEnumerator TrySupabaseLogin(string loginId, string password)
        {
            isNetworkBusy = true;
            SupabaseGameApiResponseDto response = null;
            yield return supabase.Login(loginId, password, result => response = result);
            isNetworkBusy = false;

            if (response?.Ok != true || response.User == null)
            {
                loginErrorMessage = RemoteErrorMessage("IDまたはパスワードが違います");
                ShowLogin();
                yield break;
            }

            ApplyRemoteSnapshot(response);
            CompleteLogin(response.User.ToDomain(), password);
        }

        private void CompleteLogin(UserProfile user, string password)
        {
            currentUser = user;
            RasshiineRuntimeSession.SetUser(user);
            RasshiineRuntimeSession.SetSessionToken(supabase.SessionToken);
            PersistRuntimeSnapshot();
            loginErrorMessage = string.Empty;
            initialPasswordChangeMessage = string.Empty;
            battleController?.SetControlledParticipant(currentUser.Role == UserRole.Member && repository.ActiveBattle.IsActive ? currentUser.Id : null);
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

        private void ShowInitialPasswordChange()
        {
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Login);
            ClearRoot();
            var panel = ui.CreatePanel(root, "InitialPasswordPanel", theme.RaidPanel, new Vector2(0.22f, 0.16f), new Vector2(0.78f, 0.84f), Vector2.zero, Vector2.zero);
            AddVertical(panel, 24, 18, TextAnchor.UpperCenter);

            AddText(panel, "初回パスワード変更", 46, FontStyle.Bold, theme.Text, 66, TextAnchor.MiddleCenter);
            AddText(panel, $"{currentUser.Nickname} / {UserRoleLabel(currentUser.Role)}", 24, FontStyle.Bold, theme.Cyan, 40, TextAnchor.MiddleCenter);
            AddText(panel, "初期パスワードのままでは利用を開始できません。", 22, FontStyle.Bold, theme.Gold, 42, TextAnchor.MiddleCenter);

            initialNewPasswordInput = ui.CreateInput(panel, "InitialNewPasswordInput", "新しいパスワード");
            initialNewPasswordInput.contentType = InputField.ContentType.Password;
            AddLayout(initialNewPasswordInput.gameObject, -1, 66);
            initialConfirmPasswordInput = ui.CreateInput(panel, "InitialConfirmPasswordInput", "新しいパスワードを再入力");
            initialConfirmPasswordInput.contentType = InputField.ContentType.Password;
            AddLayout(initialConfirmPasswordInput.gameObject, -1, 66);

            if (!string.IsNullOrWhiteSpace(initialPasswordChangeMessage))
            {
                AddText(panel, initialPasswordChangeMessage, 21, FontStyle.Bold, theme.Gold, 42, TextAnchor.MiddleCenter);
            }

            AddButton(panel, "変更して開始", theme.PrimaryButton, TryCompleteInitialPasswordChange);
            AddButton(panel, "ログアウト", theme.SecondaryButton, ShowLogin);
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
            MarkScene(RasshiineProductionScene.MemberHome);
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Home);
            ClearRoot();
            AddHeader("メンバーHOME", currentUser.Nickname, null);
            var content = ui.CreatePanel(root, "HomeContent", theme.RaidPanel, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.82f), Vector2.zero, Vector2.zero);
            AddVertical(content, 22, 18);

            var stats = repository.GetStats(currentUser.Id);
            var pendingCount = repository.GetSessionsForUser(currentUser.Id).Count(session => session.Status != DevSessionStatus.Approved && session.Status != DevSessionStatus.Rejected);
            var top = CreateHudRow(content, "MemberHomeTop", 184);
            var statsPanel = ui.CreatePanel(top, "StatsPanel", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(statsPanel.gameObject, 1.6f, -1);
            AddVertical(statsPanel, 16, 10);
            AddText(statsPanel, $"{currentUser.Nickname}  Lv.{stats.Level}", 34, FontStyle.Bold, theme.Text, 46);
            AddText(statsPanel, $"EXP {stats.Exp:N0} / {stats.ExpToNextLevel:N0}", 20, FontStyle.Bold, theme.Cyan, 28);
            AddProgress(statsPanel, stats.Exp / (float)stats.ExpToNextLevel, false, 42);
            var statChips = CreateHudRow(statsPanel, "MemberStatChips", 54);
            AddHudMetric(statChips, "HP", stats.Hp.ToString("N0"), theme.Mint);
            AddHudMetric(statChips, "ATK", stats.Atk.ToString("N0"), theme.Magenta);
            AddHudMetric(statChips, "DEF", stats.Def.ToString("N0"), theme.Cyan);
            AddHudMetric(statChips, "MP", stats.Mp.ToString("N0"), theme.Text);

            var weeklyPanel = ui.CreatePanel(top, "WeeklyPanel", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(weeklyPanel.gameObject, 1f, -1);
            AddVertical(weeklyPanel, 16, 10);
            AddText(weeklyPanel, "最新状況", 27, FontStyle.Bold, theme.Text, 38);
            AddText(weeklyPanel, $"TEAM DEV  {FormatMinutes(repository.GetApprovedMinutesThisWeek(currentUser.Id))}", 22, FontStyle.Bold, theme.Cyan, 34);
            AddText(weeklyPanel, $"承認待ち  {pendingCount}件", 22, FontStyle.Bold, pendingCount > 0 ? theme.Magenta : theme.Mint, 34);
            AddText(weeklyPanel, $"BOSS  {BattleStatusLabel(repository.ActiveBattle.Status)}", 22, FontStyle.Bold, theme.Gold, 34);

            var actionRow = CreateHudRow(content, "MemberHomeActions", 136);
            AddDashboardAction(actionRow, "開発ログ", theme.PrimaryButton, ShowDevLog);
            AddDashboardAction(actionRow, repository.ActiveBattle.IsActive ? "ボス戦に参加" : "ボス戦", theme.SecondaryButton, ShowBattle);
            AddDashboardAction(actionRow, "ランキング", theme.SecondaryButton, ShowRanking);

            var detailRow = CreateHudRow(content, "MemberHomeDetails", 118);
            AddMiniHomeCard(detailRow, "作品", "公開URLを登録", theme.Cyan, ShowProducts);
            AddMiniHomeCard(detailRow, "実績", "申請と報酬を確認", theme.Mint, ShowAchievements);
            if (stats.Titles.Count > 0)
            {
                AddMiniHomeCard(detailRow, "称号", string.Join(" / ", stats.Titles.Take(2)), theme.Gold, ShowAchievements);
            }
            else
            {
                AddMiniHomeCard(detailRow, "武器", stats.UnlockedWeapons.Count > 0 ? string.Join(" / ", stats.UnlockedWeapons.Take(2).Select(WeaponLabel)) : "基本装備", theme.MutedText, ShowBattle);
            }
        }

        private void ShowDevLog()
        {
            MarkScene(RasshiineProductionScene.DevLog);
            ClearRoot();
            AddHeader("開発ログ", string.Empty, ShowMemberHome);
            var scroll = CreateScrollPanel(root, "DevLogScroll", new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.82f));
            var devLogState = devLogPresenter.Build(repository, currentUser, supabase is { IsConfigured: true }, isNetworkBusy);
            var active = devLogState.ActiveSession;

            var current = CreateColumn(scroll, "CurrentSession", theme.RaidPanel, 1f);
            AddText(current, "現在のセッション", 34, FontStyle.Bold, theme.Text, 48);
            AddText(current, $"{devLogState.ApiModeLabel} / 承認待ち {devLogState.PendingCount} / AI評価待ち {devLogState.AiPendingCount} / 要確認 {devLogState.NeedsReviewCount}", 22, FontStyle.Bold, theme.Cyan, 38);
            if (active == null)
            {
                AddText(current, "新しいセッション", 24, FontStyle.Bold, theme.Cyan, 38);
                goalInput = ui.CreateInput(current, "GoalInput", "今日の開発目標を入力");
                AddLayout(goalInput.gameObject, -1, 84);
                AddButton(current, "新しいセッションを開始", theme.PrimaryButton, () =>
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
                    PersistRuntimeSnapshot();
                    SetSessionFeedback("開始しました。今日の目標に集中できます。", FeedbackTone.Success);
                    ShowDevLog();
                });
            }
            else
            {
                var activeView = devLogPresenter.ToView(active);
                var elapsed = DateTime.UtcNow - active.StartedAtUtc;
                AddText(current, $"セッション中  /  経過時間 {elapsed.Hours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}", 28, FontStyle.Bold, theme.Magenta, 44);
                AddText(current, activeView.ReviewStateLabel, 22, FontStyle.Bold, theme.Gold, 36);
                AddText(current, $"今回の開発目標: {active.Goal}", 25, FontStyle.Normal, theme.Text, 48);
                AddText(current, "達成度", 22, FontStyle.Bold, theme.Cyan, 34);
                achievementSlider = ui.CreateSlider(current, "AchievementSlider");
                AddLayout(achievementSlider.gameObject, -1, 64);
                reflectionInput = ui.CreateInput(current, "ReflectionInput", "ふりかえり・気づき", true);
                AddLayout(reflectionInput.gameObject, -1, 124);
                nextTaskInput = ui.CreateInput(current, "NextTaskInput", "次のタスク", true);
                AddLayout(nextTaskInput.gameObject, -1, 96);
                AddButton(current, "記録を保存する", theme.PrimaryButton, () =>
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
                    PersistRuntimeSnapshot();
                    SetSessionFeedback($"AI評価 {RankLabel(saved.Evaluation.Rank)} / 仮EXP +{saved.PreviewExp} / {StatusLabel(saved.Status)}", FeedbackTone.Success);
                    ShowDevLog();
                });
            }

            if (!string.IsNullOrWhiteSpace(lastSessionMessage))
            {
                AddFeedbackBanner(current, lastSessionMessage, lastSessionTone, 74);
            }

            var history = CreateColumn(scroll, "History", theme.LogPanel, 1f);
            AddText(history, $"セッション履歴 {devLogState.History.Count}件", 32, FontStyle.Bold, theme.Text, 48);
            if (devLogState.History.Count == 0)
            {
                AddText(history, "まだ保存されたセッション履歴はありません。", 22, FontStyle.Bold, theme.MutedText, 42);
            }

            foreach (var sessionView in devLogState.History)
            {
                AddSessionSummary(history, sessionView.Session, false);
            }
        }

        private void ShowProducts()
        {
            ClearRoot();
            var isMentor = currentUser.Role == UserRole.Mentor;
            UnityEngine.Events.UnityAction backAction = isMentor
                ? (UnityEngine.Events.UnityAction)ShowMentorDashboard
                : ShowMemberHome;
            AddHeader("プロダクト", isMentor ? "公開URLの確認と非表示" : "作品URLの登録と共有", backAction);
            var scroll = CreateScrollPanel(root, "ProductsScroll", new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.82f));

            if (!isMentor)
            {
                var form = CreateColumn(scroll, "ProductForm", theme.RaidPanel, 1f);
                AddText(form, "作品URLを登録", 34, FontStyle.Bold, theme.Text, 48);
                productTitleInput = ui.CreateInput(form, "ProductTitleInput", "プロダクト名");
                AddLayout(productTitleInput.gameObject, -1, 76);
                productUrlInput = ui.CreateInput(form, "ProductUrlInput", "https://example.com");
                AddLayout(productUrlInput.gameObject, -1, 76);
                productDescriptionInput = ui.CreateInput(form, "ProductDescriptionInput", "紹介コメント", true);
                AddLayout(productDescriptionInput.gameObject, -1, 112);
                AddButton(form, "公開URLを登録", theme.PrimaryButton, () =>
                {
                    if (TryRegisterRemoteProduct(productTitleInput.text, productUrlInput.text, productDescriptionInput.text))
                    {
                        return;
                    }

                    try
                    {
                        repository.RegisterProduct(currentUser.Id, productTitleInput.text, productUrlInput.text, productDescriptionInput.text);
                        SetProductFeedback("登録しました。全員に公開されます。", FeedbackTone.Success);
                    }
                    catch (Exception exception)
                    {
                        SetProductFeedback(exception.Message, FeedbackTone.Danger);
                    }

                    ShowProducts();
                });
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
            ClearRoot();
            var isMentor = currentUser.Role == UserRole.Mentor;
            UnityEngine.Events.UnityAction backAction = isMentor
                ? (UnityEngine.Events.UnityAction)ShowMentorDashboard
                : ShowMemberHome;
            AddHeader("実績", isMentor ? "申請の承認と報酬付与" : "大会・リリース・継続開発の申請", backAction);
            var scroll = CreateScrollPanel(root, "AchievementsScroll", new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.82f));

            if (!isMentor)
            {
                var form = CreateColumn(scroll, "AchievementForm", theme.RaidPanel, 1f);
                AddText(form, "実績を申請", 34, FontStyle.Bold, theme.Text, 48);
                AddText(form, "種別", 22, FontStyle.Bold, theme.Cyan, 32);
                AddSelectorRow(form, Enum.GetValues(typeof(AchievementType)).Cast<AchievementType>(), selectedAchievementType, value =>
                {
                    selectedAchievementType = value;
                    ShowAchievements();
                }, AchievementTypeLabel);
                achievementTitleInput = ui.CreateInput(form, "AchievementTitleInput", "実績名");
                AddLayout(achievementTitleInput.gameObject, -1, 76);
                achievementDescriptionInput = ui.CreateInput(form, "AchievementDescriptionInput", "説明・URL・補足", true);
                AddLayout(achievementDescriptionInput.gameObject, -1, 112);
                AddButton(form, "申請する", theme.PrimaryButton, () =>
                {
                    if (TrySubmitRemoteAchievement(selectedAchievementType, achievementTitleInput.text, achievementDescriptionInput.text))
                    {
                        return;
                    }

                    try
                    {
                        repository.SubmitAchievement(currentUser.Id, selectedAchievementType, achievementTitleInput.text, achievementDescriptionInput.text);
                        SetAchievementFeedback("申請しました。メンター承認後に報酬が反映されます。", FeedbackTone.Success);
                    }
                    catch (Exception exception)
                    {
                        SetAchievementFeedback(exception.Message, FeedbackTone.Danger);
                    }

                    ShowAchievements();
                });
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
                    AddText(list, "承認待ちの実績申請はありません。", 24, FontStyle.Bold, theme.Mint, 44);
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

        private bool TryStartRemoteSession(string goal)
        {
            if (supabase is not { IsConfigured: true })
            {
                return false;
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
                return false;
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
                return false;
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

            StartCoroutine(RegisterRemoteProduct(title, url, description));
            return true;
        }

        private IEnumerator RegisterRemoteProduct(string title, string url, string description)
        {
            isNetworkBusy = true;
            SupabaseGameApiResponseDto response = null;
            yield return supabase.RegisterProduct(title, url, description, result => response = result);
            isNetworkBusy = false;

            if (response?.Ok == true)
            {
                ApplyRemoteSnapshotPreservingModerationRecords(response);
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
                return false;
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

            if (response?.Ok == true)
            {
                ApplyRemoteSnapshotPreservingModerationRecords(response);
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
                return false;
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

            if (response?.Ok == true)
            {
                ApplyRemoteSnapshot(response);
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
                return false;
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

            if (response?.Ok == true)
            {
                ApplyRemoteSnapshot(response);
                var saved = repository.GetSessionsForUser(currentUser.Id).FirstOrDefault(item => item.Id == sessionId);
                if (saved?.Evaluation != null)
                {
                    SetSessionFeedback($"AI評価 {RankLabel(saved.Evaluation.Rank)} / 仮EXP +{saved.PreviewExp} / {StatusLabel(saved.Status)}", FeedbackTone.Success);
                }
                else
                {
                    SetSessionFeedback("AI評価待ちです。完了後に承認待ちへ反映されます。", FeedbackTone.Waiting);
                }
            }
            else
            {
                SetSessionFeedback(RemoteErrorMessage("保存できませんでした。通信状態を確認してください。"), FeedbackTone.Danger);
            }

            ShowDevLog();
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
            AddHeader("ボス戦", BattleStatusLabel(battle.Status), backAction);
            var content = ui.CreatePanel(root, "BattleContent", theme.LogPanel, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.82f), Vector2.zero, Vector2.zero);
            AddHorizontal(content, 24, 22);

            var statePanel = CreateColumn(content, "BattleState", theme.RaidPanel, 0.46f);
            AddText(statePanel, battle.Boss.Name, 42, FontStyle.Bold, theme.Magenta, 62);
            var bossMetrics = CreateHudRow(statePanel, "BossMetrics", 74);
            AddHudMetric(bossMetrics, battle.IsActive ? "TURN" : "STATUS", battle.IsActive ? $"{Mathf.Min(battle.TurnNumber, battle.TurnCount)} / {battle.TurnCount}" : BattleStatusLabel(battle.Status), theme.Cyan);
            AddHudMetric(bossMetrics, "参加", $"{battle.Participants.Count}人", theme.Text);
            AddHudMetric(bossMetrics, "TEAM DAMAGE", $"{battle.TotalDamage:N0}", theme.Gold);
            AddText(statePanel, $"BOSS HP {battle.Boss.CurrentHp:N0} / {battle.Boss.MaxHp:N0}", 30, FontStyle.Bold, theme.Text, 42);
            AddProgress(statePanel, battle.Boss.CurrentHp / (float)battle.Boss.MaxHp, true, 66);
            var partyStatus = repository.GetBattlePartyStatus();
            var partyMetrics = CreateHudRow(statePanel, "PartyMetrics", 74);
            AddHudMetric(partyMetrics, "PARTY HP", $"{partyStatus.CurrentHp:N0} / {partyStatus.MaxHp:N0}", theme.Mint);
            AddHudMetric(partyMetrics, "PARTY MP", $"{partyStatus.CurrentMp:N0} / {partyStatus.MaxMp:N0}", theme.Cyan);
            AddHudMetric(partyMetrics, "ALIVE", $"{partyStatus.AliveCount} / {partyStatus.ParticipantCount}", theme.Text);
            AddFeedbackBanner(statePanel, lastBattleMessage, lastBattleTone, 92);
            if (battle.Status == BattleStatus.Scheduled)
            {
                var waitingPanel = CreateColumn(content, "BattleActions", theme.RaidPanel, 0.54f);
                AddText(waitingPanel, "開始待機", 38, FontStyle.Bold, theme.Text, 58);
                AddText(waitingPanel, "家での開発ログが今週の戦力になります。", 26, FontStyle.Bold, theme.Cyan, 54);
                if (currentUser.Role == UserRole.Mentor)
                {
                    AddButton(waitingPanel, "ゲーム開始", theme.PrimaryButton, () =>
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
                    AddButton(waitingPanel, "状態更新", theme.SecondaryButton, () =>
                    {
                        if (!TryRefreshRemoteSnapshot(ShowBattle))
                        {
                            ShowBattle();
                        }
                    });
                    AddButton(waitingPanel, "開発ログへ", theme.PrimaryButton, ShowDevLog);
                }

                return;
            }

            if (battle.IsCompleted)
            {
                var summary = repository.GetBattleResultSummary();
                AddText(statePanel, summary.ResultMessage, 28, FontStyle.Bold, summary.IsVictory ? theme.Mint : theme.Gold, 72);
                if (currentUser.Role == UserRole.Mentor)
                {
                    AddButton(statePanel, "次週の準備", theme.DangerButton, () =>
                    {
                        if (TryResetRemoteBattle(ShowBattle))
                        {
                            return;
                        }

                        repository.ResetBattle(currentUser.Id);
                        battleController.LoadBattle(repository.ActiveBattle);
                        SetBattleFeedback("次週の準備完了", FeedbackTone.Success);
                        ShowBattle();
                    });
                }

                var resultPanel = CreateColumn(content, "BattleResult", theme.RaidPanel, 0.54f);
                AddBattleResultPanel(resultPanel, summary, currentUser.Id);
                return;
            }

            var actionPanel = CreateColumn(content, "BattleActions", theme.RaidPanel, 0.54f);
            var participant = repository.GetParticipant(currentUser.Id);
            if (participant == null)
            {
                AddButton(actionPanel, "前に映す画面", theme.PrimaryButton, ShowFrontScreen);
                return;
            }

            AddText(actionPanel, $"{participant.Nickname}  HP {participant.CurrentHp}/{participant.Stats.Hp}  MP {participant.CurrentMp}/{participant.Stats.Mp}  {RoleLabel(selectedRole)}", 28, FontStyle.Bold, theme.Text, 50);
            AddProgress(actionPanel, participant.CurrentHp / (float)Mathf.Max(participant.Stats.Hp, 1), false, 34);
            AddProgress(actionPanel, participant.CurrentMp / (float)Mathf.Max(participant.Stats.Mp, 1), false, 34);
            var memberMetrics = CreateHudRow(actionPanel, "MemberBattleMetrics", 62);
            AddHudMetric(memberMetrics, "ROLE", RoleLabel(selectedRole), theme.Gold);
            AddHudMetric(memberMetrics, "WEAPON", WeaponLabel(selectedWeapon), theme.Cyan);
            AddHudMetric(memberMetrics, "TURN", $"{Mathf.Min(battle.TurnNumber, battle.TurnCount)} / {battle.TurnCount}", theme.Text);
            AddText(actionPanel, "役割選択", 24, FontStyle.Bold, theme.Cyan, 36);
            AddSelectorRow(actionPanel, Enum.GetValues(typeof(BattleRole)).Cast<BattleRole>(), selectedRole, value =>
            {
                selectedRole = value;
                ShowBattle();
            }, RoleLabel);

            AddText(actionPanel, "武器選択", 24, FontStyle.Bold, theme.Cyan, 36);
            var availableWeapons = repository.GetAvailableBattleWeapons(participant.UserId).ToList();

            if (!availableWeapons.Contains(selectedWeapon))
            {
                selectedWeapon = availableWeapons[0];
            }

            AddSelectorRow(actionPanel, availableWeapons, selectedWeapon, value =>
            {
                selectedWeapon = value;
                ShowBattle();
            }, WeaponLabel);

            AddText(actionPanel, "行動", 24, FontStyle.Bold, theme.Cyan, 36);
            foreach (var option in repository.GetBattleActionOptions(currentUser.Id, selectedWeapon))
            {
                AddActionButton(actionPanel, option);
            }
        }

        private void AddBattleResultPanel(Transform panel, BattleResultSummary summary, string userId)
        {
            AddText(panel, summary.ResultTitle, 42, FontStyle.Bold, summary.IsVictory ? theme.Mint : theme.Gold, 62, TextAnchor.MiddleCenter);
            AddText(panel, $"BOSS HP {summary.BossCurrentHp:N0} / {summary.BossMaxHp:N0}", 28, FontStyle.Bold, theme.Text, 42, TextAnchor.MiddleCenter);
            AddText(panel, $"TEAM DAMAGE {summary.TeamDamage:N0}    参加 {summary.ParticipantCount}人", 28, FontStyle.Bold, theme.Cyan, 44, TextAnchor.MiddleCenter);
            AddText(panel, summary.RewardSummary, 22, FontStyle.Bold, theme.Gold, 52, TextAnchor.MiddleCenter);

            var personal = summary.Contributors.FirstOrDefault(entry => entry.UserId == userId);
            if (personal != null)
            {
                AddText(panel, "あなたの貢献", 26, FontStyle.Bold, theme.Text, 40);
                AddText(panel, $"{personal.TeamName} / {personal.HighlightContext} / Score {personal.ContributionScore:N0} / 報酬 +{personal.RewardExp}EXP", 22, FontStyle.Bold, theme.Cyan, 48);
            }

            AddText(panel, "貢献ランキング", 26, FontStyle.Bold, theme.Text, 40);
            var rank = 1;
            foreach (var entry in summary.Contributors.Take(5))
            {
                var mvp = entry.IsMvp ? "MVP " : string.Empty;
                AddText(panel, $"{rank}. {mvp}{entry.Nickname}  {entry.TeamName}  Damage {entry.Damage:N0}  +{entry.RewardExp}EXP", 22, FontStyle.Bold, entry.IsMvp ? theme.Gold : theme.Text, 38);
                AddText(panel, entry.HighlightContext, 18, FontStyle.Normal, theme.MutedText, 30);
                rank += 1;
            }

            AddButton(panel, "前に映す画面", theme.PrimaryButton, ShowFrontScreen);
        }

        private void ShowFrontScreen()
        {
            var readOnlyDisplay = currentUser == null || IsActiveProductionScene(RasshiineProductionScene.FrontDisplay);
            MarkScene(RasshiineProductionScene.FrontDisplay);
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Battle);
            ClearRoot();
            UnityEngine.Events.UnityAction backAction = RefreshFrontDisplayNow;
            var headerSubtitle = "表示専用 / 自動更新";
            var backLabel = "更新";
            if (!readOnlyDisplay && currentUser != null)
            {
                backAction = currentUser.Role == UserRole.Mentor
                    ? (UnityEngine.Events.UnityAction)ShowMentorDashboard
                    : ShowMemberHome;
                headerSubtitle = string.Empty;
                backLabel = "戻る";
            }

            AddHeader("全体画面", headerSubtitle, backAction, backLabel);
            var panel = ui.CreatePanel(root, "FrontPanel", theme.RaidPanel, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.82f), Vector2.zero, Vector2.zero);
            AddHorizontal(panel, 24, 24);

            var summary = repository.GetFrontDisplaySummary();
            var left = CreateColumn(panel, "FrontLeft", theme.LogPanel, 0.62f);
            AddText(left, summary.BossName, 64, FontStyle.Bold, theme.Magenta, 78, TextAnchor.MiddleCenter);
            AddText(left, summary.PhaseLabel, 38, FontStyle.Bold, summary.IsScheduled ? theme.Cyan : summary.IsCompleted ? theme.Gold : theme.Mint, 48, TextAnchor.MiddleCenter);
            if (summary.IsScheduled)
            {
                AddText(left, $"今週の開発時間 {FormatMinutes(summary.WeeklyApprovedMinutes)}", 44, FontStyle.Bold, theme.Gold, 64, TextAnchor.MiddleCenter);
                AddText(left, $"BOSS HP {summary.BossMaxHp:N0}", 40, FontStyle.Bold, theme.Text, 56, TextAnchor.MiddleCenter);
                AddProgress(left, 1f, true, 82);
                var waiting = CreateColumn(panel, "FrontWaiting", theme.RaidPanel, 0.38f);
                AddText(waiting, "ゲーム開始でレイドへ", 38, FontStyle.Bold, theme.Text, 58, TextAnchor.MiddleCenter);
                AddFrontDisplayMetric(waiting, "参加予定", $"{summary.ParticipantCount} / {summary.MemberCount}", theme.Cyan, 42);
                AddFrontDisplayMetric(waiting, "表示モード", "CLASSROOM", theme.Gold, 36);
                return;
            }

            AddText(left, $"BOSS HP {summary.BossCurrentHp:N0} / {summary.BossMaxHp:N0}", 48, FontStyle.Bold, theme.Text, 64, TextAnchor.MiddleCenter);
            AddProgress(left, summary.BossHpRatio, true, 82);
            var frontMetrics = CreateHudRow(left, "FrontMetrics", 82);
            AddFrontDisplayMetric(frontMetrics, "残りHP", $"{summary.BossHpRatio:P0}", theme.Magenta, 34);
            AddFrontDisplayMetric(frontMetrics, "TURN", $"{summary.TurnNumber} / {summary.TurnCount}", theme.Cyan, 34);
            AddFrontDisplayMetric(frontMetrics, "TEAM DAMAGE", $"{summary.TeamDamage:N0}", theme.Gold, 34);
            AddText(left, $"参加 {summary.ParticipantCount} / {summary.MemberCount}", 34, FontStyle.Bold, theme.Cyan, 46, TextAnchor.MiddleCenter);
            if (summary.IsCompleted)
            {
                AddText(left, summary.ResultTitle, 52, FontStyle.Bold, summary.IsVictory ? theme.Mint : theme.Gold, 70, TextAnchor.MiddleCenter);
                AddText(left, summary.RewardSummary, 28, FontStyle.Bold, theme.Gold, 54, TextAnchor.MiddleCenter);
            }
            AddFeedbackBanner(left, lastBattleMessage, lastBattleTone, 70);

            var right = CreateColumn(panel, "FrontRight", theme.RaidPanel, 0.38f);
            AddText(right, "今週の注目貢献者", 34, FontStyle.Bold, theme.Text, 56);
            if (summary.TopHighlight != null)
            {
                var highlight = summary.TopHighlight;
                AddText(right, highlight.Nickname, 54, FontStyle.Bold, theme.Magenta, 70, TextAnchor.MiddleCenter);
                AddText(right, RoleLabel(highlight.Role), 30, FontStyle.Bold, theme.Gold, 42, TextAnchor.MiddleCenter);
                AddFrontDisplayMetric(right, "CONTRIBUTION", $"{highlight.ContributionScore:N0}", theme.Gold, 42);
                AddText(right, highlight.HighlightContext, 24, FontStyle.Bold, theme.Cyan, 40, TextAnchor.MiddleCenter);
                AddText(right, $"今週の開発時間 {FormatMinutes(highlight.ApprovedMinutes)}", 24, FontStyle.Normal, theme.MutedText, 40, TextAnchor.MiddleCenter);
            }

            AddText(right, "NEXT HIGHLIGHT", 28, FontStyle.Bold, theme.Cyan, 46);
            foreach (var highlight in summary.Highlights.Skip(1).Take(4))
            {
                AddText(right, $"{highlight.Nickname}  {RoleLabel(highlight.Role)}  {highlight.ContributionScore:N0}", 24, FontStyle.Bold, theme.Text, 36);
            }
        }

        private void ShowSettings()
        {
            if (currentUser == null)
            {
                ShowLogin();
                return;
            }

            settingsReturnScene = activeProductionScene;
            StopBattleStatePolling();
            SetBackdrop(activeProductionScene == RasshiineProductionScene.Battle || activeProductionScene == RasshiineProductionScene.FrontDisplay
                ? NeonCityBackdrop.BackdropPreset.Battle
                : NeonCityBackdrop.BackdropPreset.Home);
            ClearRoot();
            AddHeader("設定", $"{currentUser.Nickname} / {UserRoleLabel(currentUser.Role)}", ReturnFromSettings, DefaultBackLabel, false);

            var content = ui.CreatePanel(EnsureRoot(), "SettingsContent", theme.LogPanel, new Vector2(0.18f, 0.12f), new Vector2(0.82f, 0.80f), Vector2.zero, Vector2.zero);
            AddVertical(content, 28, 18, TextAnchor.UpperCenter);

            AddText(content, "セッション", 38, FontStyle.Bold, theme.Text, 56, TextAnchor.MiddleCenter);
            AddText(content, $"ログインID {currentUser.LoginId}  /  {UserRoleLabel(currentUser.Role)}", 24, FontStyle.Bold, theme.Cyan, 42, TextAnchor.MiddleCenter);
            AddText(content, supabase is { IsConfigured: true } ? "Supabase 接続モード" : "ローカルデモモード", 24, FontStyle.Bold, theme.Gold, 42, TextAnchor.MiddleCenter);
            AddText(content, $"現在の画面 {ProductionSceneLabel(settingsReturnScene)}", 22, FontStyle.Normal, theme.MutedText, 38, TextAnchor.MiddleCenter);

            AddButton(content, "状態更新", theme.SecondaryButton, () =>
            {
                if (!TryRefreshRemoteSnapshot(ShowSettings))
                {
                    ShowSettings();
                }
            });

            AddButton(content, "前に映す画面", theme.SecondaryButton, ShowFrontScreen);
            AddButton(content, "戻る", theme.PrimaryButton, ReturnFromSettings);
            AddButton(content, "ログアウト", theme.DangerButton, ShowLogin);
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

            MarkScene(RasshiineProductionScene.MentorDashboard);
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Home);
            ClearRoot();
            AddHeader("メンターダッシュボード", $"{currentUser.Nickname} / 指揮ハブ", null);

            var pendingSessionCount = repository.GetPendingSessions().Count;
            var needsReviewCount = repository.GetPendingSessions(DevSessionReviewFilter.NeedsReview).Count;
            var pendingAchievementCount = repository.GetPendingAchievements().Count;
            var battle = repository.ActiveBattle;
            var topContributor = repository.GetHighlightedContributor();
            var reviewLabel = needsReviewCount > 0
                ? $"要確認 {needsReviewCount}件"
                : pendingSessionCount > 0
                    ? $"承認待ち {pendingSessionCount}件"
                    : "通常運用";
            var reviewColor = needsReviewCount > 0 ? theme.Gold : pendingSessionCount > 0 ? theme.Magenta : theme.Mint;

            var rail = ui.CreatePanel(root, "MentorNavRail", theme.RaidPanel, new Vector2(0.04f, 0.08f), new Vector2(0.16f, 0.82f), Vector2.zero, Vector2.zero);
            AddVertical(rail, 16, 12);
            AddText(rail, "NAV", 18, FontStyle.Bold, theme.Cyan, 32, TextAnchor.MiddleCenter);
            AddRailButton(rail, "承認", needsReviewCount > 0 ? theme.DangerButton : theme.PrimaryButton, ShowMentorReviewQueue);
            AddRailButton(rail, "ボス", theme.SecondaryButton, ShowBattle);
            AddRailButton(rail, "チーム", theme.SecondaryButton, ShowMentorTeamStatus);
            AddRailButton(rail, "操作", theme.SecondaryButton, ShowMentorOperations);
            AddRailButton(rail, "設定", theme.SecondaryButton, ShowSettings);

            var scroll = CreateDashboardScrollPanel(root, "MentorDashboardScroll", new Vector2(0.18f, 0.07f), new Vector2(0.96f, 0.82f));
            var commandCenter = CreateDashboardSection(scroll, "MentorCommandCenter", DashboardSectionHeight(72f, 148f), theme.RaidPanel);
            var metrics = CreateHudRow(commandCenter, "MentorDashboardMetrics", 72);
            AddHudMetric(metrics, "承認待ち", $"{pendingSessionCount}件", pendingSessionCount > 0 ? theme.Magenta : theme.Mint);
            AddHudMetric(metrics, "要確認", $"{needsReviewCount}件", needsReviewCount > 0 ? theme.Gold : theme.Mint);
            AddHudMetric(metrics, "TEAM DEV", FormatMinutes(repository.GetTotalApprovedMinutes()), theme.Cyan);
            AddHudMetric(metrics, "BOSS HP", $"{battle.Boss.CurrentHp:N0}/{battle.Boss.MaxHp:N0}", theme.Text);

            var primaryCommands = CreateHudRow(commandCenter, "MentorPrimaryCommands", 148);
            AddDashboardNavCard(primaryCommands, "承認レビュー", "AI評価と不審ログを確認", reviewLabel, reviewColor, needsReviewCount > 0 ? theme.DangerButton : theme.PrimaryButton, ShowMentorReviewQueue);
            AddDashboardNavCard(primaryCommands, "ボス戦", $"状態 {BattleStatusLabel(battle.Status)}", $"参加 {battle.Participants.Count}人", theme.Magenta, theme.SecondaryButton, ShowBattle);
            AddDashboardNavCard(primaryCommands, "チーム状況", $"メンバー {repository.Members.Count}人", topContributor != null ? $"TOP {topContributor.Nickname}" : "集計待ち", topContributor != null ? theme.Mint : theme.MutedText, theme.SecondaryButton, ShowMentorTeamStatus);

            if (needsReviewCount > 0)
            {
                AddFeedbackBanner(scroll, $"不審ログが {needsReviewCount} 件あります。承認レビューで内容・時間・AI評価を確認してください。", FeedbackTone.Warning, 70);
            }
            else if (!string.IsNullOrWhiteSpace(lastMentorMessage))
            {
                AddFeedbackBanner(scroll, lastMentorMessage, lastMentorTone, 70);
            }

            var detailDeck = CreateDashboardSection(scroll, "MentorDetailDeck", DashboardSectionHeight(34f, 64f), theme.RaidPanel);
            AddText(detailDeck, "主要操作", 24, FontStyle.Bold, theme.Text, 34);
            var detailRow = CreateHudRow(detailDeck, "MentorDetailCommands", 64);
            AddDashboardAction(detailRow, $"実績承認\n{pendingAchievementCount}件", pendingAchievementCount > 0 ? theme.PrimaryButton : theme.SecondaryButton, ShowAchievements);
            AddDashboardAction(detailRow, "作品管理\nURL確認", theme.SecondaryButton, ShowProducts);
            AddDashboardAction(detailRow, "アカウント\n招待・初期PW", theme.SecondaryButton, ShowMentorAccounts);
            AddDashboardAction(detailRow, "前面表示\n会場画面", theme.SecondaryButton, ShowFrontScreen);

            var logPreview = CreateDashboardSection(scroll, "MentorPendingLogPreview", DashboardSectionHeight(40f, 58f, 58f, 58f, 74f), theme.LogPanel);
            AddText(logPreview, "承認キュー", 28, FontStyle.Bold, theme.Text, 40);
            var previewItems = repository.GetPendingSessions(DevSessionReviewFilter.All).Take(3).ToList();
            if (previewItems.Count == 0)
            {
                AddText(logPreview, "承認待ちログはありません。", 22, FontStyle.Bold, theme.Mint, 58, TextAnchor.MiddleCenter);
            }
            else
            {
                foreach (var session in previewItems)
                {
                    AddCompactSessionPreview(logPreview, session, ShowMentorReviewQueue);
                }
            }
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

            MarkScene(RasshiineProductionScene.MentorDashboard);
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Home);
            ClearRoot();
            AddHeader("運用メニュー", "前面表示・作品・実績・アカウント", ShowMentorDashboard);
            var scroll = CreateDashboardScrollPanel(root, "MentorOperationsScroll", new Vector2(0.1f, 0.06f), new Vector2(0.9f, 0.82f));
            var battle = repository.ActiveBattle;
            var operationsHeight = DashboardSectionHeight(46f, 76f, 84f, 58f, 72f, battle.IsCompleted ? 72f : 0f);
            var operations = CreateDashboardSection(scroll, "MentorOperations", operationsHeight, theme.RaidPanel);
            AddText(operations, "運用ショートカット", 32, FontStyle.Bold, theme.Text, 46);
            var actionRow = CreateHudRow(operations, "MentorPrimaryActions", 76);
            AddDashboardAction(actionRow, "前面表示", theme.PrimaryButton, ShowFrontScreen);
            AddDashboardAction(actionRow, "作品管理", theme.SecondaryButton, ShowProducts);
            AddDashboardAction(actionRow, "実績承認", theme.SecondaryButton, ShowAchievements);
            AddDashboardAction(actionRow, "アカウント管理", theme.SecondaryButton, ShowMentorAccounts);
            var battleMetrics = CreateHudRow(operations, "MentorBattleMetrics", 84);
            AddHudMetric(battleMetrics, "STATUS", BattleStatusLabel(battle.Status), theme.Gold);
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

            MarkScene(RasshiineProductionScene.MentorDashboard);
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Home);
            ClearRoot();
            AddHeader("承認レビュー", $"{ReviewFilterLabel(selectedReviewFilter)} / {repository.GetPendingSessions(selectedReviewFilter).Count}件", ShowMentorDashboard);
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
                AddText(empty, $"{ReviewFilterLabel(selectedReviewFilter)}の対象はありません。", 26, FontStyle.Bold, theme.Mint, 54, TextAnchor.MiddleCenter);
                return;
            }

            foreach (var session in items)
            {
                AddSessionSummary(scroll, session, true, ShowMentorReviewQueue);
            }
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

            MarkScene(RasshiineProductionScene.MentorDashboard);
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Home);
            ClearRoot();
            AddHeader("チーム状況", $"メンバー {repository.Members.Count}人 / メンター {repository.Mentors.Count}人", ShowMentorDashboard);
            var scroll = CreateScrollPanel(root, "MentorTeamScroll", new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.82f));
            var topContributor = repository.GetHighlightedContributor();
            var teamHeight = DashboardSectionHeight(46f, 92f, topContributor != null ? 36f : 34f, topContributor != null ? 32f : 0f);
            var team = CreateDashboardSection(scroll, "MentorTeamOverview", teamHeight, theme.RaidPanel);
            AddText(team, "今週のチーム", 32, FontStyle.Bold, theme.Text, 46);
            var metrics = CreateHudRow(team, "MentorTeamMetrics", 92);
            AddHudMetric(metrics, "TEAM DEV", FormatMinutes(repository.GetTotalApprovedMinutes()), theme.Cyan);
            AddHudMetric(metrics, "MEMBERS", $"{repository.Members.Count}人", theme.Text);
            AddHudMetric(metrics, "MENTORS", $"{repository.Mentors.Count}人", theme.Text);
            AddHudMetric(metrics, "実績承認", $"{repository.GetPendingAchievements().Count}件", repository.GetPendingAchievements().Count > 0 ? theme.Gold : theme.Mint);
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
            AddHeader("アカウント管理", $"{currentUser.Nickname} / 発行・初期パスワード", ShowMentorDashboard);
            var scroll = CreateScrollPanel(root, "MentorAccountsScroll", new Vector2(0.16f, 0.06f), new Vector2(0.84f, 0.82f));
            var accountPanel = CreateDashboardSection(scroll, "MentorAccountPanel", 1600f, theme.RaidPanel);
            if (!string.IsNullOrWhiteSpace(lastMentorMessage))
            {
                AddFeedbackBanner(accountPanel, lastMentorMessage, lastMentorTone, 74);
            }

            AddMentorAccountSection(accountPanel, ShowMentorAccounts);
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
            AddText(parent, "アカウント管理", 28, FontStyle.Bold, theme.Text, 42);
            memberLoginIdInput = ui.CreateInput(parent, "MemberLoginIdInput", "login-id");
            AddLayout(memberLoginIdInput.gameObject, -1, 58);
            memberNicknameInput = ui.CreateInput(parent, "MemberNicknameInput", "表示名");
            AddLayout(memberNicknameInput.gameObject, -1, 58);
            memberTeamIdInput = ui.CreateInput(parent, "MemberTeamIdInput", "team: blue / magenta / mentor");
            memberTeamIdInput.text = selectedAccountRole == UserRole.Mentor ? "mentor" : "blue";
            AddLayout(memberTeamIdInput.gameObject, -1, 58);
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
            AddButton(parent, "アカウントを発行", theme.PrimaryButton, () =>
            {
                if (TryCreateRemoteAccount(memberLoginIdInput.text, memberNicknameInput.text, selectedAccountRole, memberTeamIdInput.text, selectedAccountRankingVisible, refreshAction))
                {
                    return;
                }

                try
                {
                    var result = repository.CreateUserAccount(currentUser.Id, memberLoginIdInput.text, memberNicknameInput.text, selectedAccountRole, memberTeamIdInput.text, selectedAccountRankingVisible);
                    PersistRuntimeSnapshot();
                    SetMentorFeedback($"{result.User.Nickname} を発行しました。初回パスワード: {result.TemporaryPassword}", FeedbackTone.Success);
                }
                catch (Exception exception)
                {
                    SetMentorFeedback(exception.Message, FeedbackTone.Danger);
                }

                refreshAction();
            });

            AddText(parent, "初期パスワード状態", 22, FontStyle.Bold, theme.Cyan, 34);
            foreach (var member in repository.Users.OrderBy(user => user.LoginId).Take(6))
            {
                AddMemberAccountSummary(parent, member, refreshAction);
            }
        }

        private void AddMemberAccountSummary(Transform parent, UserProfile member, Action refreshAction)
        {
            var summary = CreateColumn(parent, $"MemberAccount_{member.Id}", theme.StatCard, 1f);
            AddText(summary, $"{member.Nickname} / {member.LoginId}", 20, FontStyle.Bold, member.IsActive ? theme.Text : theme.MutedText, 32);
            AddText(summary, $"{UserRoleLabel(member.Role)} / {member.TeamId} / {RankingVisibilityLabel(member.RankingVisible)} / {InitialPasswordStateLabel(member)}", 18, FontStyle.Bold, member.InitialPasswordChanged ? theme.Mint : theme.Gold, 32);
            AddButton(summary, "一時PW再発行", theme.SecondaryButton, () =>
            {
                if (TryIssueRemoteTemporaryPassword(member.Id, member.Nickname, refreshAction))
                {
                    return;
                }

                try
                {
                    var result = repository.IssueTemporaryPassword(currentUser.Id, member.Id);
                    PersistRuntimeSnapshot();
                    SetMentorFeedback($"{result.User.Nickname} の一時パスワード: {result.TemporaryPassword}", FeedbackTone.Success);
                }
                catch (Exception exception)
                {
                    SetMentorFeedback(exception.Message, FeedbackTone.Danger);
                }

                refreshAction();
            });
        }

        private bool TryCreateRemoteAccount(string loginId, string nickname, UserRole role, string teamId, bool rankingVisible, Action refreshAction = null)
        {
            refreshAction ??= ShowMentorDashboard;
            if (supabase is not { IsConfigured: true })
            {
                return false;
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

            if (response?.Ok == true)
            {
                ApplyRemoteSnapshot(response);
                var user = response.User?.ToDomain();
                SetMentorFeedback($"{user?.Nickname ?? "アカウント"} を発行しました。初回パスワード: {response.TemporaryPassword}", FeedbackTone.Success);
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
                return false;
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

            if (response?.Ok == true)
            {
                ApplyRemoteSnapshot(response);
                var user = response.User?.ToDomain();
                SetMentorFeedback($"{user?.Nickname ?? nickname} の初回パスワードを再発行しました: {response.TemporaryPassword}", FeedbackTone.Success);
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
            var summary = CreateColumn(parent, $"Achievement_{achievement.Id}", theme.StatCard, 1f);
            var user = repository.Users.FirstOrDefault(item => item.Id == achievement.UserId);
            var statusColor = achievement.Status == AchievementStatus.Approved
                ? theme.Mint
                : achievement.Status == AchievementStatus.Rejected
                    ? theme.MutedText
                    : theme.Gold;
            AddText(summary, $"{user?.Nickname ?? "不明"} / {AchievementTypeLabel(achievement.Type)} / {AchievementStatusLabel(achievement.Status)}", 24, FontStyle.Bold, statusColor, 40);
            AddText(summary, achievement.Title, 22, FontStyle.Bold, theme.Text, 36);
            if (!string.IsNullOrWhiteSpace(achievement.Description))
            {
                AddText(summary, achievement.Description, 20, FontStyle.Normal, theme.MutedText, 42);
            }

            if (achievement.Status == AchievementStatus.Approved)
            {
                var rewardWeapon = achievement.HasRewardWeapon ? $" / {WeaponLabel(achievement.RewardWeapon)}" : string.Empty;
                AddText(summary, $"報酬: {achievement.RewardTitle} / {achievement.RewardSkill}{rewardWeapon}", 20, FontStyle.Bold, theme.Cyan, 36);
            }

            if (!mentorControls || achievement.Status != AchievementStatus.Pending)
            {
                return;
            }

            var row = new GameObject("AchievementActions", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(summary, false);
            AddLayout(row, -1, 58);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            var approve = ui.CreateButton(row.transform, "ApproveAchievement", "承認", theme.PrimaryButton, () =>
            {
                if (TryReviewRemoteAchievement(achievement.Id, true, achievement.Title))
                {
                    return;
                }

                repository.ApproveAchievement(achievement.Id, currentUser.Id);
                SetAchievementFeedback($"{achievement.Title} を承認し、報酬を付与しました。", FeedbackTone.Success);
                ShowAchievements();
            });
            AddLayout(approve.gameObject, 1, -1);
            var reject = ui.CreateButton(row.transform, "RejectAchievement", "却下", theme.DangerButton, () =>
            {
                if (TryReviewRemoteAchievement(achievement.Id, false, achievement.Title))
                {
                    return;
                }

                repository.RejectAchievement(achievement.Id, currentUser.Id);
                SetAchievementFeedback($"{achievement.Title} を却下しました。", FeedbackTone.Warning);
                ShowAchievements();
            });
            AddLayout(reject.gameObject, 1, -1);
        }

        private void AddProductSummary(Transform parent, ProductEntry product, bool mentorControls)
        {
            var summary = CreateColumn(parent, $"Product_{product.Id}", theme.StatCard, 1f);
            var owner = repository.Users.FirstOrDefault(user => user.Id == product.UserId);
            var postedAt = product.CreatedAtUtc == default ? string.Empty : $" / {product.CreatedAtUtc.ToLocalTime():M/d HH:mm}";
            var status = product.IsPublic ? "公開中" : "非公開";
            AddText(summary, $"{product.Title} / {owner?.Nickname ?? "不明"} / {status}{postedAt}", 24, FontStyle.Bold, product.IsPublic ? theme.Text : theme.MutedText, 40);
            AddText(summary, product.Url, 21, FontStyle.Normal, theme.Cyan, 36);
            if (!string.IsNullOrWhiteSpace(product.Description))
            {
                AddText(summary, product.Description, 20, FontStyle.Normal, theme.MutedText, 46);
            }

            if (!mentorControls)
            {
                return;
            }

            if (product.IsPublic)
            {
                AddButton(summary, "不適切なURLとして非表示", theme.DangerButton, () =>
                {
                    if (TryHideRemoteProduct(product.Id, product.Title))
                    {
                        return;
                    }

                    repository.HideProduct(product.Id, currentUser.Id);
                    SetProductFeedback($"{product.Title} を非表示にしました。", FeedbackTone.Warning);
                    ShowProducts();
                });
                return;
            }

            var hiddenBy = repository.Users.FirstOrDefault(user => user.Id == product.HiddenBy);
            AddText(summary, $"非表示にしたメンター: {hiddenBy?.Nickname ?? "不明"}", 19, FontStyle.Normal, theme.Gold, 32);
        }

        private void ShowRanking()
        {
            ClearRoot();
            AddHeader("ランキング", string.Empty, ShowMemberHome);
            var panel = ui.CreatePanel(root, "RankingPanel", theme.RaidPanel, new Vector2(0.18f, 0.1f), new Vector2(0.82f, 0.8f), Vector2.zero, Vector2.zero);
            AddVertical(panel, 26, 18);
            AddText(panel, $"{RankingKindLabel(selectedRankingKind)}ランキング", 40, FontStyle.Bold, theme.Text, 60, TextAnchor.MiddleCenter);
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

        private void AddDevelopmentTimeRanking(Transform panel)
        {
            var scopeLabel = selectedRankingView == RankingView.TeamMember
                ? LocalGameRepository.GetTeamDisplayName(currentUser?.TeamId)
                : RankingViewLabel(selectedRankingView);
            AddText(panel, $"{scopeLabel} / {RankingPeriodLabel(selectedRankingPeriod)} / 承認済みログのみ", 24, FontStyle.Bold, theme.Cyan, 42, TextAnchor.MiddleCenter);
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
                AddText(panel, $"{rank}. {entry.Nickname}    {FormatMinutes(entry.DurationMinutes)}    {entry.SessionCount}件", 28, FontStyle.Bold, rank == 1 ? theme.Gold : theme.Text, 46);
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
                AddText(panel, $"{rank}. {entry.TeamName}    {FormatMinutes(entry.DurationMinutes)}    {entry.MemberCount}人 / {entry.SessionCount}件", 28, FontStyle.Bold, rank == 1 ? theme.Gold : theme.Text, 46);
                rank += 1;
            }
        }

        private void AddBattleDamageRanking(Transform panel)
        {
            AddText(panel, $"{RankingPeriodLabel(selectedRankingPeriod)} / ボス戦ダメージ", 24, FontStyle.Bold, theme.Cyan, 42, TextAnchor.MiddleCenter);
            var rank = 1;
            var entries = repository.GetBattleDamageRanking(selectedRankingPeriod);
            if (entries.Count == 0)
            {
                AddText(panel, "表示できるボス戦ダメージはありません。", 24, FontStyle.Bold, theme.MutedText, 46, TextAnchor.MiddleCenter);
                return;
            }

            foreach (var entry in entries.Take(12))
            {
                AddText(panel, $"{rank}. {entry.Nickname}    ダメージ {entry.Damage:N0}    開発 {FormatMinutes(entry.ApprovedMinutes)}", 28, FontStyle.Bold, rank == 1 ? theme.Gold : theme.Text, 46);
                rank += 1;
            }
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
                return false;
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
            isNetworkBusy = true;
            SupabaseGameApiResponseDto response = null;
            yield return supabase.SubmitBattleAction(selectedRole, selectedWeapon, actionType, result => response = result);
            isNetworkBusy = false;

            if (response?.Ok == true)
            {
                var actionResult = response.ActionResult.ToDomain();
                ApplyRemoteSnapshot(response);
                if (actionResult != null)
                {
                    SetBattleFeedback(actionResult.Message, BattleFeedbackTone(actionResult));
                    StartCoroutine(battleController.PlayAction(actionResult));
                }
            }
            else
            {
                SetBattleFeedback(RemoteErrorMessage("通信できませんでした。行動は反映されていません。"), FeedbackTone.Danger);
            }

            ShowBattle();
        }

        private bool TryStartRemoteBattle(Action afterStart)
        {
            if (supabase is not { IsConfigured: true })
            {
                return false;
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

            if (response?.Ok == true)
            {
                ApplyRemoteSnapshot(response);
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
                return false;
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

            if (response?.Ok == true)
            {
                ApplyRemoteSnapshot(response, useFrontDisplaySnapshot);
            }
            else if (useFrontDisplaySnapshot)
            {
                SetBattleFeedback(RemoteErrorMessage("前面表示を更新できませんでした。通信状態を確認してください。"), FeedbackTone.Warning);
            }

            afterRefresh?.Invoke();
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
                return false;
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
            isNetworkBusy = true;
            SupabaseGameApiResponseDto response = null;
            yield return supabase.ResetBattle(result => response = result);
            isNetworkBusy = false;

            if (response?.Ok == true)
            {
                ApplyRemoteSnapshot(response);
                SetBattleFeedback("次週の準備完了", FeedbackTone.Success);
            }
            else
            {
                SetBattleFeedback(RemoteErrorMessage("通信できませんでした。次週準備は完了していません。"), FeedbackTone.Danger);
            }

            afterReset?.Invoke();
        }

        private bool TryReviewRemoteSession(DevSession session, bool approve, string comment, Action refreshAction = null)
        {
            refreshAction ??= ShowMentorDashboard;
            if (supabase is not { IsConfigured: true })
            {
                return false;
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

            StartCoroutine(ReviewRemoteSession(session, approve, comment, refreshAction));
            return true;
        }

        private IEnumerator ReviewRemoteSession(DevSession session, bool approve, string comment, Action refreshAction)
        {
            refreshAction ??= ShowMentorDashboard;
            isNetworkBusy = true;
            SupabaseGameApiResponseDto response = null;
            if (approve)
            {
                yield return supabase.ApproveSession(session.Id, comment, result => response = result);
            }
            else
            {
                yield return supabase.RejectSession(session.Id, comment, result => response = result);
            }

            isNetworkBusy = false;

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

            var retry = error.CanRetry ? " 再試行できます。" : string.Empty;
            return $"{fallback} ({error.Message}){retry}";
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
                var button = ui.CreateButton(row.transform, $"Select_{value}", getLabel(value), isSelected ? theme.PrimaryButton : theme.SecondaryButton, () => onSelect(value), isSelected ? theme.Gold : theme.Text);
                AddLayout(button.gameObject, 1, -1);
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
            AddText(summary, sessionView.GrowthStateLabel, 20, FontStyle.Bold, session.Status == DevSessionStatus.Approved ? theme.Mint : theme.Gold, 32);
            if (sessionView.HasReviewNotification)
            {
                AddText(summary, sessionView.ReviewNotificationLabel, 21, FontStyle.Bold, session.Status == DevSessionStatus.Rejected ? theme.Gold : theme.Mint, 44);
            }

            AddText(summary, $"目標: {session.Goal}", 21, FontStyle.Normal, theme.MutedText, 34);
            if (sessionView.CanViewAiEvaluation)
            {
                AddText(summary, $"{sessionView.AiEvaluationSummaryLabel}  /  仮EXP +{session.PreviewExp}", 22, FontStyle.Bold, theme.Magenta, 38);
                AddText(summary, sessionView.AiEvaluationFeedbackLabel, 20, FontStyle.Normal, theme.MutedText, 42);
            }

            if (session.GrowthFeedback != null)
            {
                AddText(summary, session.GrowthFeedback.Summary, 22, FontStyle.Bold, session.GrowthFeedback.HasLevelUp ? theme.Gold : theme.Mint, 38);
            }

            if (session.SuspiciousFlags.Count > 0)
            {
                AddText(summary, $"要確認: {string.Join(", ", session.SuspiciousFlags)}", 20, FontStyle.Bold, theme.Gold, 32);
            }

            var mentorCommentLine = BuildMentorCommentLine(session);
            if (!string.IsNullOrEmpty(mentorCommentLine))
            {
                AddText(summary, mentorCommentLine, 20, FontStyle.Bold, session.Status == DevSessionStatus.Rejected ? theme.Gold : StatusColor(session.Status), 52);
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
                var achievementInput = ui.CreateInput(correctionRow.transform, "CorrectedAchievement", "修正達成度");
                achievementInput.contentType = InputField.ContentType.IntegerNumber;
                achievementInput.text = session.AchievementRate.ToString();
                AddLayout(achievementInput.gameObject, 1, -1);

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
                    var correctedAchievementRate = ReadReviewInt(achievementInput, session.AchievementRate, 0, 100);
                    var comment = ReadReviewComment(commentInput, $"修正承認: {correctedDuration}分 / 達成度 {correctedAchievementRate}%");
                    var approvedSession = repository.ApproveSessionWithCorrections(
                        session.Id,
                        currentUser.Id,
                        correctedAchievementRate,
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

        private RectTransform CreateDashboardSection(Transform parent, string name, float preferredHeight, Sprite sprite)
        {
            var panel = ui.CreatePanel(parent, name, sprite, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(panel.gameObject, -1, preferredHeight);
            AddVertical(panel, 20, 14);
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
            var rootPanel = ui.CreatePanel(parent, name, theme.LogPanel, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            var viewport = new GameObject("Viewport", typeof(Image), typeof(Mask));
            viewport.transform.SetParent(rootPanel, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            ui.Stretch(viewportRect, 24, 24, -24, -24);
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.12f);
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
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.06f);
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

        private void AddHeader(string title, string subtitle, UnityEngine.Events.UnityAction backAction = null, string backLabel = DefaultBackLabel, bool showSettings = true)
        {
            var header = ui.CreatePanel(EnsureRoot(), "Header", theme.RaidPanel, new Vector2(0.04f, 0.84f), new Vector2(0.96f, 0.96f), Vector2.zero, Vector2.zero);
            AddHorizontal(header, 18, 16);
            if (backAction == null)
            {
                AddHeaderSpacer(header, 160);
            }
            else if (backLabel == DefaultBackLabel && theme.BackIcon != null)
            {
                var back = ui.CreateIconButton(header, "BackButton", theme.BackIcon, theme.SecondaryButton, backAction, theme.Text);
                AddLayout(back.gameObject, 76, -1);
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
            AddText(titleBox.transform, title, 38, FontStyle.Bold, theme.Text, 52, TextAnchor.MiddleCenter);
            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                AddText(titleBox.transform, subtitle, 18, FontStyle.Normal, theme.MutedText, 30, TextAnchor.MiddleCenter);
            }

            if (currentUser != null && showSettings)
            {
                var settings = ui.CreateButton(header, "SettingsButton", SettingsLabel, theme.SecondaryButton, ShowSettings);
                AddLayout(settings.gameObject, 160, -1);
            }
            else
            {
                AddHeaderSpacer(header, 160);
            }
        }

        private static void AddHeaderSpacer(Transform parent, float width)
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

        private void AddRailButton(Transform parent, string label, Sprite sprite, UnityEngine.Events.UnityAction onClick)
        {
            var button = ui.CreateButton(parent, $"Rail_{label}", label, sprite, onClick);
            AddLayout(button.gameObject, -1, 58);
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
            AddVertical(card, 8, 4);
            AddText(card, title, 20, FontStyle.Bold, theme.Text, 24);
            AddText(card, detail, 15, FontStyle.Bold, theme.MutedText, 28);
            AddText(card, status, 17, FontStyle.Bold, statusColor, 24);
            var button = ui.CreateButton(card, $"Open_{title}", "開く", buttonSprite, onClick);
            AddLayout(button.gameObject, -1, 36);
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

        private void AddHudMetric(Transform parent, string label, string value, Color valueColor)
        {
            var metric = ui.CreatePanel(parent, $"HudMetric_{label}", theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(metric.gameObject, 1, -1);
            AddVertical(metric, 7, 1, TextAnchor.UpperCenter);
            AddText(metric, label, 12, FontStyle.Bold, theme.MutedText, 17, TextAnchor.MiddleCenter);
            AddText(metric, value, 20, FontStyle.Bold, valueColor, 29, TextAnchor.MiddleCenter);
        }

        private void AddFrontDisplayMetric(Transform parent, string label, string value, Color valueColor, int valueSize)
        {
            var metric = ui.CreatePanel(parent, $"FrontMetric_{label}", theme.NotificationPanel != null ? theme.NotificationPanel : theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
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

            var panel = ui.CreatePanel(parent, $"Feedback_{tone}_{parent.childCount}", theme.NotificationPanel != null ? theme.NotificationPanel : theme.StatCard, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddLayout(panel.gameObject, -1, height);
            AddHorizontal(panel, 14, 12);

            var accent = new GameObject("Accent", typeof(Image));
            accent.transform.SetParent(panel, false);
            AddLayout(accent, 14, -1);
            accent.GetComponent<Image>().color = FeedbackColor(tone);

            var textBox = new GameObject("FeedbackText", typeof(RectTransform));
            textBox.transform.SetParent(panel, false);
            AddLayout(textBox, 1, -1);
            AddVertical(textBox.GetComponent<RectTransform>(), 0, 2);
            AddText(textBox.transform, FeedbackHeading(tone), 18, FontStyle.Bold, FeedbackColor(tone), 24);
            AddText(textBox.transform, message, 22, FontStyle.Bold, theme.Text, height - 36);

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
                FeedbackTone.Success => theme.Mint,
                FeedbackTone.Waiting => theme.Cyan,
                FeedbackTone.Warning => theme.Gold,
                FeedbackTone.Danger => theme.Magenta,
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

            var label = ui.CreateText(parent, $"Text_{parent.childCount}", text, size, style, color, anchor);
            AddLayout(label.gameObject, -1, height);
            return label;
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
            layout.childForceExpandWidth = true;
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

        private static void AddSpacer(Transform parent, float height)
        {
            if (parent == null)
            {
                return;
            }

            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(parent, false);
            AddLayout(spacer, -1, height);
        }

        private static void AddLayout(GameObject target, float flexibleWidth, float preferredHeight)
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

            if (preferredHeight > 0f)
            {
                layout.preferredHeight = preferredHeight;
            }
            layout.minHeight = preferredHeight > 0f ? Mathf.Min(preferredHeight, 48f) : 0f;
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
                DevSessionStatus.Pending => theme.Magenta,
                DevSessionStatus.NeedsReview => theme.Gold,
                DevSessionStatus.AiPending => theme.Cyan,
                DevSessionStatus.Incomplete => theme.Gold,
                DevSessionStatus.Approved => theme.Mint,
                DevSessionStatus.Rejected => theme.MutedText,
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
