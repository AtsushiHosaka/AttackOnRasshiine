using System;
using System.Collections;
using System.Linq;
using AttackOnRasshiine.Runtime.Battle;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Scene;
using AttackOnRasshiine.Runtime.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AttackOnRasshiine.Runtime.UI
{
    public sealed class RaidGameApp : MonoBehaviour
    {
        [SerializeField] private RasshiineTheme theme;
        [SerializeField] private RaidBattleController battleController;
        [SerializeField] private AnimatedSkybox animatedSkybox;
        [SerializeField] private NeonCityBackdrop neonCityBackdrop;

        private LocalGameRepository repository;
        private SupabaseGameClient supabase;
        private NeonUiFactory ui;
        private RectTransform root;
        private UserProfile currentUser;
        private BattleRole selectedRole = BattleRole.Attacker;
        private WeaponKind selectedWeapon = WeaponKind.Blade;
        private AchievementType selectedAchievementType = AchievementType.Release;
        private DevSessionReviewFilter selectedReviewFilter = DevSessionReviewFilter.All;
        private RankingPeriod selectedRankingPeriod = RankingPeriod.Weekly;
        private string lastBattleMessage = "メンターの開始待ち";
        private string lastSessionMessage = string.Empty;
        private string lastProductMessage = string.Empty;
        private string lastAchievementMessage = string.Empty;
        private string loginErrorMessage = string.Empty;
        private bool isNetworkBusy;

        private InputField loginIdInput;
        private InputField passwordInput;
        private InputField goalInput;
        private InputField reflectionInput;
        private InputField nextTaskInput;
        private InputField productTitleInput;
        private InputField productUrlInput;
        private InputField productDescriptionInput;
        private InputField achievementTitleInput;
        private InputField achievementDescriptionInput;
        private Slider achievementSlider;

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
            ShowLogin();
            StartCoroutine(LoadSupabaseConfig());
        }

        private IEnumerator LoadSupabaseConfig()
        {
            yield return supabase.LoadConfig();
        }

        private void ApplyRemoteSnapshot(SupabaseGameApiResponseDto response)
        {
            if (response?.Snapshot == null)
            {
                return;
            }

            repository.ApplySnapshot(response.Snapshot.ToSnapshot());
            battleController.LoadBattle(repository.ActiveBattle);
            if (currentUser != null)
            {
                battleController.SetControlledParticipant(currentUser.Role == UserRole.Member && repository.ActiveBattle.IsActive ? currentUser.Id : null);
            }
        }

        private void CreateRoot()
        {
            var canvas = ui.CreateCanvas("Rasshiine Game UI");
            root = new GameObject("ScreenRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(canvas.transform, false);
            ui.Stretch(root, 0, 0, 0, 0);
        }

        private void SetBackdrop(NeonCityBackdrop.BackdropPreset preset)
        {
            neonCityBackdrop?.SetPreset(preset);
        }

        private void ShowLogin()
        {
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Login);
            currentUser = null;
            supabase?.ClearSession();
            battleController?.SetControlledParticipant(null);
            ui.Clear(root);
            var panel = ui.CreatePanel(root, "LoginPanel", theme.RaidPanel, new Vector2(0.22f, 0.17f), new Vector2(0.78f, 0.83f), Vector2.zero, Vector2.zero);
            AddVertical(panel, 28, 20, TextAnchor.UpperCenter);

            AddText(panel, "Attack On Rasshiine", 56, FontStyle.Bold, theme.Text, 76, TextAnchor.MiddleCenter);
            AddSpacer(panel, 36);

            loginIdInput = ui.CreateInput(panel, "LoginIdInput", "ログインID");
            AddLayout(loginIdInput.gameObject, -1, 72);
            passwordInput = ui.CreateInput(panel, "PasswordInput", "パスワード");
            passwordInput.contentType = InputField.ContentType.Password;
            AddLayout(passwordInput.gameObject, -1, 72);

            if (!string.IsNullOrEmpty(loginErrorMessage))
            {
                AddText(panel, loginErrorMessage, 22, FontStyle.Bold, theme.Gold, 36, TextAnchor.MiddleCenter);
            }

            var loginButton = ui.CreateButton(panel, "Login", "ログイン", theme.PrimaryButton, () =>
            {
                TryLogin(loginIdInput.text, passwordInput.text);
            });
            AddLayout(loginButton.gameObject, -1, 86);

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

            TryLocalLogin(loginId, password);
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

            currentUser = user;
            loginErrorMessage = string.Empty;
            battleController?.SetControlledParticipant(currentUser.Role == UserRole.Member && repository.ActiveBattle.IsActive ? currentUser.Id : null);
            if (currentUser.Role == UserRole.Mentor)
            {
                ShowMentorDashboard();
            }
            else
            {
                ShowMemberHome();
            }
        }

        private IEnumerator TrySupabaseLogin(string loginId, string password)
        {
            isNetworkBusy = true;
            SupabaseGameApiResponseDto response = null;
            yield return supabase.Login(loginId, password, result => response = result);
            isNetworkBusy = false;

            if (response?.Ok != true || response.User == null)
            {
                loginErrorMessage = "IDまたはパスワードが違います";
                ShowLogin();
                yield break;
            }

            ApplyRemoteSnapshot(response);
            currentUser = response.User.ToDomain();
            loginErrorMessage = string.Empty;
            battleController?.SetControlledParticipant(currentUser.Role == UserRole.Member && repository.ActiveBattle.IsActive ? currentUser.Id : null);
            if (currentUser.Role == UserRole.Mentor)
            {
                ShowMentorDashboard();
            }
            else
            {
                ShowMemberHome();
            }
        }

        private void ShowMemberHome()
        {
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Home);
            ui.Clear(root);
            AddHeader("ホーム", currentUser.Nickname, ShowLogin, "ログアウト");
            var content = ui.CreatePanel(root, "HomeContent", theme.LogPanel, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.82f), Vector2.zero, Vector2.zero);
            AddHorizontal(content, 24, 22);

            var statsPanel = CreateColumn(content, "StatsPanel", theme.RaidPanel, 0.36f);
            var stats = repository.GetStats(currentUser.Id);
            AddText(statsPanel, currentUser.Nickname, 42, FontStyle.Bold, theme.Text, 60);
            AddText(statsPanel, $"Lv.{stats.Level}  EXP {stats.Exp}/{stats.ExpToNextLevel}", 26, FontStyle.Bold, theme.Cyan, 42);
            AddProgress(statsPanel, stats.Exp / (float)stats.ExpToNextLevel, false, 54);
            AddText(statsPanel, $"HP {stats.Hp}   ATK {stats.Atk}   DEF {stats.Def}   MP {stats.Mp}", 24, FontStyle.Normal, theme.Text, 42);
            AddText(statsPanel, $"今週の承認済み開発時間: {FormatMinutes(repository.GetApprovedMinutesThisWeek(currentUser.Id))}", 24, FontStyle.Normal, theme.MutedText, 42);
            AddText(statsPanel, $"承認待ちログ: {repository.GetSessionsForUser(currentUser.Id).Count(session => session.Status != DevSessionStatus.Approved && session.Status != DevSessionStatus.Rejected)}件", 24, FontStyle.Normal, theme.MutedText, 42);
            if (stats.Titles.Count > 0)
            {
                AddText(statsPanel, $"称号: {string.Join(" / ", stats.Titles.Take(2))}", 22, FontStyle.Bold, theme.Gold, 38);
            }

            if (stats.UnlockedWeapons.Count > 0)
            {
                AddText(statsPanel, $"解放武器: {string.Join(" / ", stats.UnlockedWeapons.Select(WeaponLabel))}", 22, FontStyle.Bold, theme.Cyan, 38);
            }

            var actionPanel = CreateColumn(content, "ActionPanel", theme.RaidPanel, 0.64f);
            AddText(actionPanel, "今日の行動", 38, FontStyle.Bold, theme.Text, 54);
            AddButton(actionPanel, "開発ログへ", theme.PrimaryButton, ShowDevLog);
            AddButton(actionPanel, "プロダクトURL登録", theme.SecondaryButton, ShowProducts);
            AddButton(actionPanel, "実績申請", theme.SecondaryButton, ShowAchievements);
            if (repository.ActiveBattle is { IsActive: true })
            {
                AddButton(actionPanel, "ボス戦に参加", theme.SecondaryButton, ShowBattle);
            }
            else if (repository.ActiveBattle is { Status: BattleStatus.Completed })
            {
                AddButton(actionPanel, "ボス戦の結果", theme.SecondaryButton, ShowBattle);
            }
            else
            {
                AddText(actionPanel, "ボス戦は開始待ち", 26, FontStyle.Bold, theme.Cyan, 52);
                AddButton(actionPanel, "状態更新", theme.SecondaryButton, () =>
                {
                    if (!TryRefreshRemoteSnapshot(ShowMemberHome))
                    {
                        ShowMemberHome();
                    }
                });
            }
            AddButton(actionPanel, "ランキングを見る", theme.SecondaryButton, ShowRanking);
        }

        private void ShowDevLog()
        {
            ui.Clear(root);
            AddHeader("開発ログ", string.Empty, ShowMemberHome);
            var scroll = CreateScrollPanel(root, "DevLogScroll", new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.82f));
            var active = repository.GetActiveSession(currentUser.Id);

            var current = CreateColumn(scroll, "CurrentSession", theme.RaidPanel, 1f);
            AddText(current, "現在のセッション", 34, FontStyle.Bold, theme.Text, 48);
            if (active == null)
            {
                AddText(current, "新しいセッション", 24, FontStyle.Bold, theme.Cyan, 38);
                goalInput = ui.CreateInput(current, "GoalInput", "今日の開発目標を入力");
                AddLayout(goalInput.gameObject, -1, 84);
                AddButton(current, "新しいセッションを開始", theme.PrimaryButton, () =>
                {
                    if (TryStartRemoteSession(goalInput.text))
                    {
                        return;
                    }

                    repository.StartSession(currentUser.Id, goalInput.text);
                    lastSessionMessage = "開始しました";
                    ShowDevLog();
                });
            }
            else
            {
                var elapsed = DateTime.UtcNow - active.StartedAtUtc;
                AddText(current, $"セッション中  /  経過時間 {elapsed.Hours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}", 28, FontStyle.Bold, theme.Magenta, 44);
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
                    if (TryCompleteRemoteSession(active.Id, Mathf.RoundToInt(achievementSlider.value), reflectionInput.text, nextTaskInput.text))
                    {
                        return;
                    }

                    var saved = repository.CompleteSession(currentUser.Id, Mathf.RoundToInt(achievementSlider.value), reflectionInput.text, nextTaskInput.text);
                    lastSessionMessage = $"AI評価 {RankLabel(saved.Evaluation.Rank)} / 仮EXP +{saved.PreviewExp} / {StatusLabel(saved.Status)}";
                    ShowDevLog();
                });
            }

            if (!string.IsNullOrWhiteSpace(lastSessionMessage))
            {
                AddText(current, lastSessionMessage, 23, FontStyle.Normal, theme.MutedText, 58);
            }

            var history = CreateColumn(scroll, "History", theme.LogPanel, 1f);
            AddText(history, "セッション履歴", 32, FontStyle.Bold, theme.Text, 48);
            foreach (var session in repository.GetSessionsForUser(currentUser.Id).Take(5))
            {
                AddSessionSummary(history, session, false);
            }
        }

        private void ShowProducts()
        {
            ui.Clear(root);
            var isMentor = currentUser.Role == UserRole.Mentor;
            UnityEngine.Events.UnityAction backAction = isMentor ? ShowMentorDashboard : ShowMemberHome;
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
                    try
                    {
                        repository.RegisterProduct(currentUser.Id, productTitleInput.text, productUrlInput.text, productDescriptionInput.text);
                        lastProductMessage = "登録しました。全員に公開されます。";
                    }
                    catch (Exception exception)
                    {
                        lastProductMessage = exception.Message;
                    }

                    ShowProducts();
                });
            }

            if (!string.IsNullOrWhiteSpace(lastProductMessage))
            {
                var message = CreateColumn(scroll, "ProductMessage", theme.StatCard, 1f);
                AddText(message, lastProductMessage, 24, FontStyle.Bold, theme.Gold, 42);
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
            ui.Clear(root);
            var isMentor = currentUser.Role == UserRole.Mentor;
            UnityEngine.Events.UnityAction backAction = isMentor ? ShowMentorDashboard : ShowMemberHome;
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
                        lastAchievementMessage = "申請しました。メンター承認後に報酬が反映されます。";
                    }
                    catch (Exception exception)
                    {
                        lastAchievementMessage = exception.Message;
                    }

                    ShowAchievements();
                });
            }

            if (!string.IsNullOrWhiteSpace(lastAchievementMessage))
            {
                var message = CreateColumn(scroll, "AchievementMessage", theme.StatCard, 1f);
                AddText(message, lastAchievementMessage, 24, FontStyle.Bold, theme.Gold, 42);
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
            if (supabase is not { IsConfigured: true } || string.IsNullOrEmpty(supabase.SessionToken) || isNetworkBusy)
            {
                return false;
            }

            StartCoroutine(StartRemoteSession(goal));
            return true;
        }

        private bool TrySubmitRemoteAchievement(AchievementType type, string title, string description)
        {
            if (supabase is not { IsConfigured: true } || string.IsNullOrEmpty(supabase.SessionToken))
            {
                return false;
            }

            if (isNetworkBusy)
            {
                lastAchievementMessage = "通信中です。少し待ってから操作してください。";
                ShowAchievements();
                return true;
            }

            StartCoroutine(SubmitRemoteAchievement(type, title, description));
            return true;
        }

        private IEnumerator SubmitRemoteAchievement(AchievementType type, string title, string description)
        {
            isNetworkBusy = true;
            SupabaseGameApiResponseDto response = null;
            yield return supabase.SubmitAchievement(type, title, description, result => response = result);
            isNetworkBusy = false;

            if (response?.Ok == true)
            {
                ApplyRemoteSnapshot(response);
                lastAchievementMessage = "申請しました。メンター承認後に報酬が反映されます。";
            }
            else
            {
                lastAchievementMessage = "申請できませんでした";
            }

            ShowAchievements();
        }

        private bool TryReviewRemoteAchievement(string achievementId, bool approve, string title)
        {
            if (supabase is not { IsConfigured: true } || string.IsNullOrEmpty(supabase.SessionToken))
            {
                return false;
            }

            if (isNetworkBusy)
            {
                lastAchievementMessage = "通信中です。少し待ってから操作してください。";
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
                ApplyRemoteSnapshot(response);
                lastAchievementMessage = approve
                    ? $"{title} を承認し、報酬を付与しました。"
                    : $"{title} を却下しました。";
            }
            else
            {
                lastAchievementMessage = "更新できませんでした";
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
                lastSessionMessage = "開始しました";
            }
            else
            {
                lastSessionMessage = "保存できませんでした";
            }

            ShowDevLog();
        }

        private bool TryCompleteRemoteSession(string sessionId, int achievementRate, string reflection, string nextTask)
        {
            if (supabase is not { IsConfigured: true } || string.IsNullOrEmpty(supabase.SessionToken) || isNetworkBusy)
            {
                return false;
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
                lastSessionMessage = saved?.Evaluation != null
                    ? $"AI評価 {RankLabel(saved.Evaluation.Rank)} / 仮EXP +{saved.PreviewExp} / {StatusLabel(saved.Status)}"
                    : "AI評価待ちです";
            }
            else
            {
                lastSessionMessage = "保存できませんでした";
            }

            ShowDevLog();
        }

        private void ShowBattle()
        {
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Battle);
            ui.Clear(root);
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
            AddText(statePanel, battle.IsActive ? $"TURN {Mathf.Min(battle.TurnNumber, battle.TurnCount)} / {battle.TurnCount}   参加者 {battle.Participants.Count}人" : $"参加予定 {battle.Participants.Count}人", 26, FontStyle.Bold, theme.Cyan, 42);
            AddText(statePanel, $"BOSS HP {battle.Boss.CurrentHp:N0} / {battle.Boss.MaxHp:N0}", 30, FontStyle.Bold, theme.Text, 48);
            AddProgress(statePanel, battle.Boss.CurrentHp / (float)battle.Boss.MaxHp, true, 54);
            AddText(statePanel, $"TEAM DAMAGE {battle.TotalDamage:N0}", 32, FontStyle.Bold, theme.Gold, 52);
            AddText(statePanel, lastBattleMessage, 24, FontStyle.Normal, theme.MutedText, 86);
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

                        repository.StartBattle();
                        battleController.LoadBattle(repository.ActiveBattle);
                        battleController.SetControlledParticipant(null);
                        lastBattleMessage = "ボス戦開始";
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
                AddText(statePanel, battle.Boss.CurrentHp <= 0 ? "勝利。努力報酬を付与できます。" : "3ターン終了。次回に向けて開発ログを積み上げよう。", 28, FontStyle.Bold, battle.Boss.CurrentHp <= 0 ? theme.Mint : theme.Gold, 54);
                if (currentUser.Role == UserRole.Mentor)
                {
                    AddButton(statePanel, "次週の準備", theme.DangerButton, () =>
                    {
                        if (TryResetRemoteBattle(ShowBattle))
                        {
                            return;
                        }

                        repository.ResetBattle();
                        battleController.LoadBattle(repository.ActiveBattle);
                        lastBattleMessage = "次週の準備完了";
                        ShowBattle();
                    });
                }
            }

            var actionPanel = CreateColumn(content, "BattleActions", theme.RaidPanel, 0.54f);
            var participant = repository.GetParticipant(currentUser.Id);
            if (participant == null)
            {
                AddButton(actionPanel, "前に映す画面", theme.PrimaryButton, ShowFrontScreen);
                return;
            }

            AddText(actionPanel, $"{participant.Nickname}  HP {participant.CurrentHp}/{participant.Stats.Hp}  MP {participant.CurrentMp}/{participant.Stats.Mp}", 28, FontStyle.Bold, theme.Text, 50);
            AddText(actionPanel, "役割選択", 24, FontStyle.Bold, theme.Cyan, 36);
            AddSelectorRow(actionPanel, Enum.GetValues(typeof(BattleRole)).Cast<BattleRole>(), selectedRole, value =>
            {
                selectedRole = value;
                ShowBattle();
            }, RoleLabel);

            AddText(actionPanel, "武器選択", 24, FontStyle.Bold, theme.Cyan, 36);
            var availableWeapons = repository.Weapons
                .Where(weapon => !weapon.IsSpecial || participant.Stats.Level >= 5 || participant.Stats.UnlockedWeapons.Contains(weapon.Kind))
                .Select(weapon => weapon.Kind)
                .ToList();
            if (availableWeapons.Count == 0)
            {
                availableWeapons.Add(WeaponKind.Blade);
            }

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
            AddActionButton(actionPanel, "通常攻撃", BattleActionType.Normal);
            AddActionButton(actionPanel, "強攻撃 / MP10", BattleActionType.Strong);
            AddActionButton(actionPanel, "全力攻撃 / MP20", BattleActionType.FullPower);
            AddActionButton(actionPanel, "支援行動 / MP10", BattleActionType.Support);
            AddActionButton(actionPanel, "ガード", BattleActionType.Guard);
        }

        private void ShowFrontScreen()
        {
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Battle);
            ui.Clear(root);
            UnityEngine.Events.UnityAction backAction = ShowMemberHome;
            if (currentUser.Role == UserRole.Mentor)
            {
                backAction = ShowMentorDashboard;
            }
            AddHeader("全体画面", string.Empty, backAction);
            var battle = repository.ActiveBattle;
            var panel = ui.CreatePanel(root, "FrontPanel", theme.RaidPanel, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.82f), Vector2.zero, Vector2.zero);
            AddHorizontal(panel, 24, 24);

            var left = CreateColumn(panel, "FrontLeft", theme.LogPanel, 0.56f);
            AddText(left, battle.Boss.Name, 64, FontStyle.Bold, theme.Magenta, 86, TextAnchor.MiddleCenter);
            if (battle.Status == BattleStatus.Scheduled)
            {
                AddText(left, "開始待機", 52, FontStyle.Bold, theme.Cyan, 74, TextAnchor.MiddleCenter);
                AddText(left, $"今週の開発時間 {FormatMinutes(repository.GetTotalApprovedMinutes(RankingPeriod.Weekly))}", 42, FontStyle.Bold, theme.Gold, 68, TextAnchor.MiddleCenter);
                AddText(left, $"BOSS HP {battle.Boss.MaxHp:N0}", 38, FontStyle.Bold, theme.Text, 60, TextAnchor.MiddleCenter);
                var waiting = CreateColumn(panel, "FrontWaiting", theme.RaidPanel, 0.44f);
                AddText(waiting, "ゲーム開始でレイドへ", 38, FontStyle.Bold, theme.Text, 58);
                AddText(waiting, $"参加予定 {battle.Participants.Count} / {repository.Members.Count}", 30, FontStyle.Bold, theme.Cyan, 50);
                return;
            }

            AddText(left, $"BOSS 残りHP {battle.Boss.CurrentHp / (float)battle.Boss.MaxHp:P0}", 46, FontStyle.Bold, theme.Text, 68, TextAnchor.MiddleCenter);
            AddProgress(left, battle.Boss.CurrentHp / (float)battle.Boss.MaxHp, true, 76);
            AddText(left, $"TEAM DAMAGE {battle.TotalDamage:N0}", 48, FontStyle.Bold, theme.Gold, 80, TextAnchor.MiddleCenter);
            AddText(left, $"TURN {Mathf.Min(battle.TurnNumber, battle.TurnCount)} / {battle.TurnCount}    参加 {battle.Participants.Count} / {repository.Members.Count}", 34, FontStyle.Bold, theme.Cyan, 54, TextAnchor.MiddleCenter);

            var right = CreateColumn(panel, "FrontRight", theme.RaidPanel, 0.44f);
            var highlight = battle.Participants.OrderByDescending(item => item.TotalDamage + item.TotalHeal + item.SupportCount * 30).FirstOrDefault();
            AddText(right, "今週の注目貢献者", 34, FontStyle.Bold, theme.Text, 56);
            if (highlight != null)
            {
                AddText(right, highlight.Nickname, 48, FontStyle.Bold, theme.Magenta, 68);
                AddText(right, $"Damage {highlight.TotalDamage:N0} / Heal {highlight.TotalHeal:N0} / Support {highlight.SupportCount}", 26, FontStyle.Bold, theme.Cyan, 44);
                AddText(right, $"今週の開発時間 {FormatMinutes(repository.GetApprovedMinutesThisWeek(highlight.UserId))}", 26, FontStyle.Normal, theme.MutedText, 44);
            }

            AddText(right, "NEXT HIGHLIGHT", 28, FontStyle.Bold, theme.Cyan, 46);
            foreach (var participant in battle.Participants.OrderByDescending(item => item.TotalDamage).Take(4))
            {
                AddText(right, $"{participant.Nickname}  {RoleLabel(participant.Role)}  {participant.TotalDamage:N0}", 24, FontStyle.Normal, theme.Text, 36);
            }
        }

        private void ShowMentorDashboard()
        {
            SetBackdrop(NeonCityBackdrop.BackdropPreset.Home);
            ui.Clear(root);
            AddHeader("メンターダッシュボード", $"{currentUser.Nickname} / 承認・管理・ボス調整", ShowLogin, "ログアウト");
            var content = ui.CreatePanel(root, "MentorContent", theme.LogPanel, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.82f), Vector2.zero, Vector2.zero);
            AddHorizontal(content, 22, 20);

            var overview = CreateColumn(content, "Overview", theme.RaidPanel, 0.36f);
            AddText(overview, "今週の状況", 36, FontStyle.Bold, theme.Text, 54);
            AddText(overview, $"チーム総開発時間 {FormatMinutes(repository.GetTotalApprovedMinutes())}", 26, FontStyle.Bold, theme.Cyan, 42);
            AddText(overview, $"承認待ち {repository.GetPendingSessions().Count}件", 26, FontStyle.Bold, theme.Magenta, 42);
            AddText(overview, $"要確認 {repository.GetPendingSessions(DevSessionReviewFilter.NeedsReview).Count}件 / AI評価待ち {repository.GetPendingSessions(DevSessionReviewFilter.AiPending).Count}件", 22, FontStyle.Bold, theme.Gold, 38);
            AddText(overview, $"実績承認待ち {repository.GetPendingAchievements().Count}件", 26, FontStyle.Bold, theme.Gold, 42);
            AddText(overview, $"ボス戦 {BattleStatusLabel(repository.ActiveBattle.Status)}", 26, FontStyle.Bold, theme.Gold, 42);
            AddText(overview, $"ボスHP {repository.ActiveBattle.Boss.CurrentHp:N0}/{repository.ActiveBattle.Boss.MaxHp:N0}", 26, FontStyle.Bold, theme.Text, 42);
            AddButton(overview, "前に映す画面", theme.PrimaryButton, ShowFrontScreen);
            AddButton(overview, "プロダクト管理", theme.SecondaryButton, ShowProducts);
            AddButton(overview, "実績承認", theme.SecondaryButton, ShowAchievements);
            if (repository.ActiveBattle.Status == BattleStatus.Scheduled)
            {
                AddButton(overview, "ゲーム開始", theme.PrimaryButton, () =>
                {
                    if (TryStartRemoteBattle(ShowMentorDashboard))
                    {
                        return;
                    }

                    repository.StartBattle();
                    battleController.LoadBattle(repository.ActiveBattle);
                    battleController.SetControlledParticipant(null);
                    lastBattleMessage = "ボス戦開始";
                    ShowMentorDashboard();
                });
            }
            else
            {
                AddButton(overview, "ボス戦を確認", theme.SecondaryButton, ShowBattle);
            }

            if (repository.ActiveBattle.Status != BattleStatus.Active)
            {
                AddButton(overview, "次週の準備", theme.DangerButton, () =>
                {
                    if (TryResetRemoteBattle(ShowMentorDashboard))
                    {
                        return;
                    }

                    repository.ResetBattle();
                    battleController.LoadBattle(repository.ActiveBattle);
                    lastBattleMessage = "次週の準備完了";
                    ShowMentorDashboard();
                });
            }

            var pending = CreateColumn(content, "Pending", theme.RaidPanel, 0.64f);
            AddText(pending, "承認待ち一覧", 36, FontStyle.Bold, theme.Text, 54);
            AddSelectorRow(pending, Enum.GetValues(typeof(DevSessionReviewFilter)).Cast<DevSessionReviewFilter>(), selectedReviewFilter, value =>
            {
                selectedReviewFilter = value;
                ShowMentorDashboard();
            }, ReviewFilterLabel);
            AddText(pending, BuildReviewQueueSummary(), 22, FontStyle.Bold, theme.Cyan, 34);
            var items = repository.GetPendingSessions(selectedReviewFilter).Take(5).ToList();
            if (items.Count == 0)
            {
                AddText(pending, $"{ReviewFilterLabel(selectedReviewFilter)}の対象はありません。", 26, FontStyle.Bold, theme.Mint, 52);
            }

            foreach (var session in items)
            {
                AddSessionSummary(pending, session, true);
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
                lastAchievementMessage = $"{achievement.Title} を承認し、報酬を付与しました。";
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
                lastAchievementMessage = $"{achievement.Title} を却下しました。";
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
                    repository.HideProduct(product.Id, currentUser.Id);
                    lastProductMessage = $"{product.Title} を非表示にしました。";
                    ShowProducts();
                });
                return;
            }

            var hiddenBy = repository.Users.FirstOrDefault(user => user.Id == product.HiddenBy);
            AddText(summary, $"非表示にしたメンター: {hiddenBy?.Nickname ?? "不明"}", 19, FontStyle.Normal, theme.Gold, 32);
        }

        private void ShowRanking()
        {
            ui.Clear(root);
            AddHeader("ランキング", string.Empty, ShowMemberHome);
            var panel = ui.CreatePanel(root, "RankingPanel", theme.RaidPanel, new Vector2(0.18f, 0.1f), new Vector2(0.82f, 0.8f), Vector2.zero, Vector2.zero);
            AddVertical(panel, 26, 18);
            AddText(panel, "開発時間ランキング", 40, FontStyle.Bold, theme.Text, 60, TextAnchor.MiddleCenter);
            AddSelectorRow(panel, Enum.GetValues(typeof(RankingPeriod)).Cast<RankingPeriod>(), selectedRankingPeriod, value =>
            {
                selectedRankingPeriod = value;
                ShowRanking();
            }, RankingPeriodLabel);

            AddText(panel, $"{RankingPeriodLabel(selectedRankingPeriod)} / 承認済みログのみ", 24, FontStyle.Bold, theme.Cyan, 42, TextAnchor.MiddleCenter);
            var rank = 1;
            var entries = repository.GetDevelopmentTimeRanking(selectedRankingPeriod);
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

        private void AddActionButton(Transform parent, string label, BattleActionType actionType)
        {
            AddButton(parent, label, actionType == BattleActionType.FullPower ? theme.DangerButton : theme.PrimaryButton, () =>
            {
                if (TrySubmitRemoteBattleAction(actionType))
                {
                    return;
                }

                var result = repository.SubmitBattleAction(currentUser.Id, selectedRole, selectedWeapon, actionType);
                lastBattleMessage = result.Message;
                StartCoroutine(battleController.PlayAction(result));
                ShowBattle();
            });
        }

        private bool TrySubmitRemoteBattleAction(BattleActionType actionType)
        {
            if (supabase is not { IsConfigured: true } || string.IsNullOrEmpty(supabase.SessionToken) || isNetworkBusy)
            {
                return false;
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
                    lastBattleMessage = actionResult.Message;
                    StartCoroutine(battleController.PlayAction(actionResult));
                }
            }
            else
            {
                lastBattleMessage = "通信できませんでした";
            }

            ShowBattle();
        }

        private bool TryStartRemoteBattle(Action afterStart)
        {
            if (supabase is not { IsConfigured: true } || string.IsNullOrEmpty(supabase.SessionToken) || isNetworkBusy)
            {
                return false;
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
                lastBattleMessage = "ボス戦開始";
                afterStart?.Invoke();
                yield break;
            }
            else
            {
                lastBattleMessage = "通信できませんでした";
            }

            ShowMentorDashboard();
        }

        private bool TryRefreshRemoteSnapshot(Action afterRefresh)
        {
            if (supabase is not { IsConfigured: true } || string.IsNullOrEmpty(supabase.SessionToken) || isNetworkBusy)
            {
                return false;
            }

            StartCoroutine(RefreshRemoteSnapshot(afterRefresh));
            return true;
        }

        private IEnumerator RefreshRemoteSnapshot(Action afterRefresh)
        {
            isNetworkBusy = true;
            SupabaseGameApiResponseDto response = null;
            yield return supabase.GetSnapshot(result => response = result);
            isNetworkBusy = false;

            if (response?.Ok == true)
            {
                ApplyRemoteSnapshot(response);
            }

            afterRefresh?.Invoke();
        }

        private bool TryResetRemoteBattle(Action afterReset)
        {
            if (supabase is not { IsConfigured: true } || string.IsNullOrEmpty(supabase.SessionToken) || isNetworkBusy)
            {
                return false;
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
                lastBattleMessage = "次週の準備完了";
            }
            else
            {
                lastBattleMessage = "通信できませんでした";
            }

            afterReset?.Invoke();
        }

        private bool TryReviewRemoteSession(string sessionId, bool approve)
        {
            if (supabase is not { IsConfigured: true } || string.IsNullOrEmpty(supabase.SessionToken) || isNetworkBusy)
            {
                return false;
            }

            StartCoroutine(ReviewRemoteSession(sessionId, approve));
            return true;
        }

        private IEnumerator ReviewRemoteSession(string sessionId, bool approve)
        {
            isNetworkBusy = true;
            SupabaseGameApiResponseDto response = null;
            var comment = approve ? "確認しました。正式EXPへ反映します。" : "今回は内容を再確認してください。";
            if (approve)
            {
                yield return supabase.ApproveSession(sessionId, comment, result => response = result);
            }
            else
            {
                yield return supabase.RejectSession(sessionId, comment, result => response = result);
            }

            isNetworkBusy = false;

            if (response?.Ok == true)
            {
                ApplyRemoteSnapshot(response);
            }

            ShowMentorDashboard();
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

        private void AddSessionSummary(Transform parent, DevSession session, bool mentorControls)
        {
            var summary = CreateColumn(parent, $"Session_{session.Id}", theme.StatCard, 1f);
            var user = repository.Users.First(item => item.Id == session.UserId);
            AddText(summary, $"{StatusLabel(session.Status)} / {user.Nickname} / {FormatMinutes(session.DurationMinutes)} / 達成度 {session.AchievementRate}%", 24, FontStyle.Bold, StatusColor(session.Status), 40);
            AddText(summary, BuildSessionReviewDetail(session), 20, FontStyle.Bold, theme.Cyan, 32);
            AddText(summary, $"目標: {session.Goal}", 21, FontStyle.Normal, theme.MutedText, 34);
            if (session.Evaluation != null)
            {
                AddText(summary, $"AI評価 {RankLabel(session.Evaluation.Rank)}  {session.Evaluation.TotalScore}/100  仮EXP +{session.PreviewExp}", 22, FontStyle.Bold, theme.Magenta, 38);
                AddText(summary, session.Evaluation.Feedback, 20, FontStyle.Normal, theme.MutedText, 42);
            }

            if (session.SuspiciousFlags.Count > 0)
            {
                AddText(summary, $"要確認: {string.Join(", ", session.SuspiciousFlags)}", 20, FontStyle.Bold, theme.Gold, 32);
            }

            if (mentorControls)
            {
                var row = new GameObject("ApprovalActions", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                row.transform.SetParent(summary, false);
                AddLayout(row, -1, 58);
                var layout = row.GetComponent<HorizontalLayoutGroup>();
                layout.spacing = 12;
                layout.childControlWidth = true;
                layout.childForceExpandWidth = true;
                var approve = ui.CreateButton(row.transform, "Approve", "承認", theme.PrimaryButton, () =>
                {
                    if (TryReviewRemoteSession(session.Id, true))
                    {
                        return;
                    }

                    repository.ApproveSession(session.Id, currentUser.Id, "確認しました。正式EXPへ反映します。");
                    ShowMentorDashboard();
                });
                AddLayout(approve.gameObject, 1, -1);
                var reject = ui.CreateButton(row.transform, "Reject", "却下", theme.DangerButton, () =>
                {
                    if (TryReviewRemoteSession(session.Id, false))
                    {
                        return;
                    }

                    repository.RejectSession(session.Id, currentUser.Id, "今回は内容を再確認してください。");
                    ShowMentorDashboard();
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

        private void AddHeader(string title, string subtitle, UnityEngine.Events.UnityAction backAction, string backLabel = "戻る")
        {
            var header = ui.CreatePanel(root, "Header", theme.RaidPanel, new Vector2(0.04f, 0.84f), new Vector2(0.96f, 0.96f), Vector2.zero, Vector2.zero);
            AddHorizontal(header, 18, 16);
            var back = ui.CreateButton(header, "BackButton", backLabel, theme.SecondaryButton, backAction);
            AddLayout(back.gameObject, 170, -1);

            var titleBox = new GameObject("TitleBox", typeof(RectTransform), typeof(VerticalLayoutGroup));
            titleBox.transform.SetParent(header, false);
            AddLayout(titleBox, 1, -1);
            AddVertical(titleBox.GetComponent<RectTransform>(), 0, 2);
            AddText(titleBox.transform, title, 40, FontStyle.Bold, theme.Text, 54);
            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                AddText(titleBox.transform, subtitle, 20, FontStyle.Normal, theme.MutedText, 32);
            }
        }

        private void AddButton(Transform parent, string label, Sprite sprite, UnityEngine.Events.UnityAction onClick)
        {
            var button = ui.CreateButton(parent, label, label, sprite, onClick);
            AddLayout(button.gameObject, -1, 72);
        }

        private void AddProgress(Transform parent, float value01, bool magenta, float height)
        {
            var progress = ui.CreateProgressBar(parent, "Progress", value01, magenta);
            AddLayout(progress.gameObject, -1, height);
        }

        private Text AddText(Transform parent, string text, int size, FontStyle style, Color color, float height, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var label = ui.CreateText(parent, $"Text_{parent.childCount}", text, size, style, color, anchor);
            AddLayout(label.gameObject, -1, height);
            return label;
        }

        private static void AddVertical(RectTransform rect, int padding, int spacing, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
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
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
        }

        private static void AddSpacer(Transform parent, float height)
        {
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(parent, false);
            AddLayout(spacer, -1, height);
        }

        private static void AddLayout(GameObject target, float flexibleWidth, float preferredHeight)
        {
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

        private string BuildReviewQueueSummary()
        {
            return $"すべて {repository.GetPendingSessions().Count}件  /  承認待ち {repository.GetPendingSessions(DevSessionReviewFilter.Pending).Count}件  /  要確認 {repository.GetPendingSessions(DevSessionReviewFilter.NeedsReview).Count}件  /  AI評価待ち {repository.GetPendingSessions(DevSessionReviewFilter.AiPending).Count}件";
        }

        private string BuildSessionReviewDetail(DevSession session)
        {
            var startedAt = session.StartedAtUtc == default ? string.Empty : $"{session.StartedAtUtc.ToLocalTime():M/d HH:mm}";
            return session.Status switch
            {
                DevSessionStatus.NeedsReview => $"不審ログフラグ確認: {string.Join(", ", session.SuspiciousFlags)} / {startedAt}",
                DevSessionStatus.AiPending => $"AI評価未完了: {session.AiEvaluationFailureReason} / 承認時は暫定評価を反映 / {startedAt}",
                DevSessionStatus.Pending => $"AI評価済み。承認で正式EXPへ反映 / {startedAt}",
                _ => startedAt
            };
        }

        private Color StatusColor(DevSessionStatus status)
        {
            return status switch
            {
                DevSessionStatus.Pending => theme.Magenta,
                DevSessionStatus.NeedsReview => theme.Gold,
                DevSessionStatus.AiPending => theme.Cyan,
                DevSessionStatus.Approved => theme.Mint,
                DevSessionStatus.Rejected => theme.MutedText,
                _ => theme.Text
            };
        }

        private static string RankLabel(AiRank rank)
        {
            return rank == AiRank.APlus ? "A+" : rank.ToString();
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
