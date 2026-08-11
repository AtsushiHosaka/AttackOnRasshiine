using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using AttackOnRasshiine.Runtime.Scene;
using AttackOnRasshiine.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AttackOnRasshiine.Editor
{
    public sealed class RaidGameAppUiLayoutEditModeTests
    {
        private const string LongJapaneseText = "ボス戦UIの情報整理とプレイヤー体験を高めるために長い説明文を入力しても画面が崩れないことを確認するテキスト";
        private const string LongAuthenticationError = "本番APIに接続できません。ネットワーク設定と通信状態を確認してから、少し時間をおいてもう一度ログインしてください。問題が続く場合は担当メンターに連絡してください。";

        [Test]
        public void AllPrimaryScreensPassLayoutQaWithLongUserContent()
        {
            var failures = RunAllPrimaryScreensLayoutQa();
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [TestCase(1440f, 1024f)]
        [TestCase(808f, 570f)]
        [TestCase(390f, 844f)]
        public void LoginStatusAreaContainsLongAuthenticationErrorsWithoutOverlap(float width, float height)
        {
            var harness = CreateHarness(new Vector2(width, height));
            try
            {
                SetField(harness.App, "loginErrorMessage", LongAuthenticationError);
                Invoke(harness.App, "ShowLogin");

                var panel = FindRect(harness.Root, "LoginPanel");
                var password = FindInput(harness.Root, "PasswordInput").GetComponent<RectTransform>();
                var statusArea = FindRect(harness.Root, "LoginStatusArea");
                var statusMessage = statusArea.GetComponentsInChildren<Text>(true).Single(text => text.name == "LoginStatusMessage");
                var loginButton = FindButton(harness.Root, "Login").GetComponent<RectTransform>();

                var layoutIssues = UiLayoutQa.Scan(harness.Root);
                Assert.IsNotNull(statusArea.GetComponent<RectMask2D>(), "Authentication status must be clipped to its dedicated frame.");
                Assert.AreEqual(LongAuthenticationError, GetField(harness.App, "loginErrorMessage"), "The complete authentication error must remain available to the login state.");
                Assert.That(statusMessage.text.Length, Is.LessThanOrEqualTo(32), "The compact status slot should use concise display copy.");
                StringAssert.EndsWith("…", statusMessage.text, "Long authentication guidance should be shortened deliberately instead of overflowing.");
                Assert.GreaterOrEqual(EffectivePixelFontSize(statusMessage), 14f, "Authentication status must remain at least 14 rendered pixels.");
                AssertRectContains(statusArea, statusMessage.rectTransform, "Authentication status text must respect the status-area safe padding.");
                AssertRectsDoNotOverlap(panel, password, statusArea, "Password input and authentication status must never overlap.");
                AssertRectsDoNotOverlap(panel, statusArea, loginButton, "Authentication status and login button must never overlap.");
                Assert.IsEmpty(layoutIssues, $"Login layout QA failed at {width:0}x{height:0}: {string.Join(" | ", layoutIssues)}");

                if (height > width * 1.25f)
                {
                    Assert.LessOrEqual(panel.anchorMin.x, 0.071f, "Portrait login should use the wider safe-area panel.");
                    Assert.GreaterOrEqual(panel.anchorMax.x, 0.929f, "Portrait login should use the wider safe-area panel.");
                }
            }
            finally
            {
                harness.Destroy();
            }
        }

        [TestCase(1440f, 1024f)]
        [TestCase(808f, 570f)]
        [TestCase(390f, 844f)]
        public void LoginKeyboardFocusOrderPreservesTheContainedLayout(float width, float height)
        {
            var harness = CreateHarness(new Vector2(width, height));
            GameObject eventSystemObject = null;
            try
            {
                Invoke(harness.App, "ShowLogin");
                var panel = FindRect(harness.Root, "LoginPanel");
                var loginId = FindInput(harness.Root, "LoginIdInput");
                var password = FindInput(harness.Root, "PasswordInput");
                var loginButton = FindButton(harness.Root, "Login");

                Assert.AreEqual(Navigation.Mode.Explicit, loginId.navigation.mode);
                Assert.AreSame(password, loginId.navigation.selectOnDown);
                Assert.AreSame(loginId, password.navigation.selectOnUp);
                Assert.AreSame(loginButton, password.navigation.selectOnDown);
                Assert.AreSame(password, loginButton.navigation.selectOnUp);
                Assert.IsTrue(HasSubmitTrigger(password), "Password Enter/Submit must trigger login.");

                eventSystemObject = new GameObject("LoginKeyboardFocusEventSystem", typeof(EventSystem));
                var eventSystem = eventSystemObject.GetComponent<EventSystem>();
                eventSystem.SetSelectedGameObject(loginId.gameObject);
                Assert.AreSame(loginId.gameObject, eventSystem.currentSelectedGameObject);
                eventSystem.SetSelectedGameObject(password.gameObject);
                Assert.AreSame(password.gameObject, eventSystem.currentSelectedGameObject);
                eventSystem.SetSelectedGameObject(loginButton.gameObject);
                Assert.AreSame(loginButton.gameObject, eventSystem.currentSelectedGameObject);

                AssertRectContains(panel, loginId.GetComponent<RectTransform>(), "Login ID input must remain within the panel while focused.");
                AssertRectContains(panel, password.GetComponent<RectTransform>(), "Password input must remain within the panel while focused.");
                AssertRectContains(panel, loginButton.GetComponent<RectTransform>(), "Login button must remain within the panel while focused.");
                Assert.IsEmpty(UiLayoutQa.Scan(harness.Root), $"Focused login layout QA failed at {width:0}x{height:0}.");
            }
            finally
            {
                if (eventSystemObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(eventSystemObject);
                }

                harness.Destroy();
            }
        }

        [TestCase(808f, 570f)]
        [TestCase(1440f, 1024f)]
        public void LandscapeLoginCompositionReservesRightHandScenicStage(float width, float height)
        {
            var harness = CreateHarness(new Vector2(width, height));
            try
            {
                Invoke(harness.App, "ShowLogin");
                var panel = FindRect(harness.Root, "LoginPanel");
                var brand = FindRect(harness.Root, "LoginBrand");
                var rootRect = harness.Root.rect;
                var panelRect = RelativeRect(harness.Root, panel);
                var compactLandscape = width < 1000f;

                Assert.GreaterOrEqual(
                    panelRect.xMin,
                    rootRect.xMin + rootRect.width * 0.05f,
                    "The login card must keep a safe inset from the left edge.");
                Assert.LessOrEqual(
                    panelRect.xMax,
                    rootRect.xMin + rootRect.width * (compactLandscape ? 0.37f : 0.36f),
                    "The login card must leave the waypoint and floating island visible on the right.");
                Assert.LessOrEqual(
                    panelRect.width,
                    rootRect.width * (compactLandscape ? 0.32f : 0.28f),
                    "The login card must not dominate the low-poly world at landscape WebGL sizes.");
                Assert.GreaterOrEqual(
                    panelRect.yMin,
                    rootRect.yMin + rootRect.height * 0.10f,
                    "The login card must leave a lower scenic strip for the terrace steps.");
                AssertRectsDoNotOverlap(harness.Root, panel, brand, "The compact login card and crest lockup must not overlap.");
                AssertRectContains(harness.Root, panel, "The landscape login card must stay inside the WebGL viewport.");
                AssertRectContains(harness.Root, brand, "The crest lockup must stay inside the WebGL viewport.");
                Assert.IsFalse(harness.Root.GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == "LoginTitle"), "The rejected full-width title bar must not return.");
                Assert.IsEmpty(UiLayoutQa.Scan(harness.Root), $"Landscape login layout QA failed at {width:0}x{height:0}.");
            }
            finally
            {
                harness.Destroy();
            }
        }

        [TestCase(808f, 570f)]
        [TestCase(1440f, 1024f)]
        public void LoginUsesDedicatedNavySurfacesAndLiveTextCta(float width, float height)
        {
            var harness = CreateHarness(new Vector2(width, height));
            try
            {
                ((RasshiineTheme)GetField(harness.App, "theme")).UseHeatUiSkin = true;
                Invoke(harness.App, "ShowLogin");

                var theme = (RasshiineTheme)GetField(harness.App, "theme");
                AssertIntegratedLoginPanel(harness.Root, theme);
                var contentLayer = FindRect(harness.Root, "LoginContentLayer");
                var contentCanvas = contentLayer.GetComponent<Canvas>();
                Assert.IsNotNull(contentCanvas);
                Assert.IsTrue(contentCanvas.overrideSorting, "Live login content must remain above the opaque generated panel art.");
                Assert.Greater(contentCanvas.sortingOrder, 0);
                AssertLoginSurface(harness.Root, "LoginStatusArea", "LoginStatusAreaFill", "LoginStatusDedicatedFrameSlot", 0.92f);
                var divider = FindRect(harness.Root, "LoginHeadingDivider").GetComponent<Image>();
                Assert.IsNull(divider.sprite, "The divider must be a flat rule, not the ornate generated divider art.");
                Assert.Greater(divider.color.a, 0f, "The flat divider rule must be visible below the login heading.");
                foreach (var textName in new[] { "LoginTitleText", "LoginIdLabel", "PasswordLabel" })
                {
                    var label = FindText(harness.Root, textName);
                    Assert.IsNotNull(label.transform.parent.GetComponent<Image>(), $"{textName} must render in its own overlay surface above the opaque panel art.");
                    Assert.AreEqual(VerticalWrapMode.Overflow, label.verticalOverflow, $"{textName} must not emit zero WebGL vertices when the dynamic-font line box is fractionally taller than its rect.");
                }
                foreach (var inputName in new[] { "LoginIdInput", "PasswordInput" })
                {
                    var input = FindInput(harness.Root, inputName);
                    Assert.IsNull(input.GetComponent<Image>().sprite, $"{inputName} must use a flat border, not the ornate generated frame art.");
                    Assert.IsNotNull(input.transform.Find("LoginInputFill"), $"{inputName} must keep an opaque navy fill behind live text.");
                    var heatVisual = input.transform.Find("HeatInputFieldPrefabVisual");
                    Assert.IsTrue(heatVisual == null || !heatVisual.gameObject.activeSelf, $"{inputName} must hide the generic Heat prefab visual.");
                }
                var loginButton = FindButton(harness.Root, "Login");
                Assert.IsNull(loginButton.GetComponent<Image>().sprite, "Login CTA must be a flat gold fill, not the ornate generated button art.");
                var liveLabel = loginButton.GetComponentsInChildren<Text>(true).Single(text => text.name == "LoginButtonLabel");
                Assert.AreEqual("ログイン", liveLabel.text);
                Assert.IsNull(loginButton.GetComponentInChildren<BakedTextButtonImage>(true), "Login CTA must use live Text instead of a runtime-baked texture.");
                Assert.IsNotNull(FindRect(harness.Root, "LoginCrest"), "The selected crest lockup must be present.");
                Assert.IsNotNull(FindRect(harness.Root, "LoginBrandTitle"), "The selected Mincho brand title must be present.");
                var sunRays = FindRect(harness.Root, "LoginSunRays").GetComponent<Image>();
                Assert.AreSame(theme.LoginSunRays, sunRays.sprite, "The generated daylight overlay must support the upper-left sky atmosphere.");
                Assert.IsFalse(sunRays.raycastTarget);
                Assert.IsFalse(sunRays.preserveAspect, "The rays must reach the left viewport edge without a visible aspect-fit seam.");
                Assert.IsEmpty(UiLayoutQa.Scan(harness.Root), $"Login palette layout QA failed at {width:0}x{height:0}.");
            }
            finally
            {
                harness.Destroy();
            }
        }

        [TestCase(1440f, 1024f)]
        [TestCase(808f, 570f)]
        [TestCase(390f, 844f)]
        public void LoginTypographyAndTargetsRemainReadableInRenderedPixels(float width, float height)
        {
            var harness = CreateHarness(new Vector2(width, height));
            try
            {
                Invoke(harness.App, "ShowLogin");
                var labels = new[]
                {
                    FindText(harness.Root, "LoginIdLabel"),
                    FindText(harness.Root, "PasswordLabel"),
                    FindText(harness.Root, "LoginStatusMessage"),
                    FindText(harness.Root, "LoginButtonLabel")
                };

                foreach (var label in labels)
                {
                    Assert.GreaterOrEqual(
                        EffectivePixelFontSize(label),
                        14f,
                        $"{label.name} must remain at least 14 rendered pixels at {width:0}x{height:0}.");
                }

                var loginId = FindInput(harness.Root, "LoginIdInput");
                var password = FindInput(harness.Root, "PasswordInput");
                Assert.GreaterOrEqual(EffectivePixelFontSize(loginId.textComponent), 16f);
                Assert.GreaterOrEqual(EffectivePixelFontSize(password.textComponent), 16f);
                Assert.GreaterOrEqual(EffectivePixelHeight(harness.Root, loginId.GetComponent<RectTransform>()), 44f, "Login ID target must remain at least 44 rendered pixels tall.");
                Assert.GreaterOrEqual(EffectivePixelHeight(harness.Root, password.GetComponent<RectTransform>()), 44f, "Password target must remain at least 44 rendered pixels tall.");
                Assert.GreaterOrEqual(EffectivePixelHeight(harness.Root, FindButton(harness.Root, "Login").GetComponent<RectTransform>()), 44f, "Login CTA must remain at least 44 rendered pixels tall.");
                Assert.IsEmpty(UiLayoutQa.Scan(harness.Root), $"Readable login layout QA failed at {width:0}x{height:0}.");
            }
            finally
            {
                harness.Destroy();
            }
        }

        [TestCase(390f, 844f)]
        [TestCase(808f, 570f)]
        [TestCase(1440f, 1024f)]
        public void LoginFlatPanelKeepsTitleStatusAndCtaClearOfEachOther(float width, float height)
        {
            // The login panel moved from a 9-sliced ornate frame to a flat rectangle
            // (see AssertIntegratedLoginPanel), so there is no sliced-border safe zone
            // to test against anymore. This keeps the layout-spacing invariants that
            // still matter: content stays inside the flat panel, and the status/CTA
            // rows do not crowd each other or overflow the button's own label.
            var harness = CreateHarness(new Vector2(width, height));
            try
            {
                Invoke(harness.App, "ShowLogin");

                var panel = FindRect(harness.Root, "LoginPanel");
                var panelImage = panel.GetComponent<Image>();
                var title = FindText(harness.Root, "LoginTitleText");
                var statusArea = FindRect(harness.Root, "LoginStatusArea");
                var loginButton = FindButton(harness.Root, "Login");
                var buttonRect = loginButton.GetComponent<RectTransform>();
                var buttonImage = loginButton.GetComponent<Image>();
                var buttonLabel = FindText(harness.Root, "LoginButtonLabel");
                var canvasScale = EffectiveCanvasScale(harness.Root);

                Assert.IsNotNull(panelImage);
                Assert.AreEqual(Image.Type.Simple, panelImage.type);
                Assert.IsNull(panelImage.sprite);
                Assert.IsNotNull(buttonImage);
                Assert.AreEqual(Image.Type.Simple, buttonImage.type);
                Assert.IsNull(buttonImage.sprite);

                var panelRect = panel.rect;
                var titleRect = RelativeRect(panel, title.rectTransform);
                var ctaRect = RelativeRect(panel, buttonRect);
                var titleTopClearancePixels = (panelRect.yMax - titleRect.yMax) * canvasScale;
                var ctaBottomClearancePixels = (ctaRect.yMin - panelRect.yMin) * canvasScale;

                Assert.GreaterOrEqual(
                    titleTopClearancePixels + 0.5f,
                    0f,
                    $"Login title must stay inside the flat panel at {width:0}x{height:0}.");
                Assert.GreaterOrEqual(
                    ctaBottomClearancePixels + 0.5f,
                    0f,
                    $"Login CTA must stay inside the flat panel at {width:0}x{height:0}.");

                var statusRect = RelativeRect(panel, statusArea);
                var statusToCtaGapPixels = (statusRect.yMin - ctaRect.yMax) * canvasScale;
                Assert.GreaterOrEqual(
                    statusToCtaGapPixels,
                    8f,
                    $"Status and CTA rows need at least 8 rendered pixels of separation at {width:0}x{height:0}.");

                var ctaCenterHeightPixels = ctaRect.height * canvasScale;
                var labelPreferredHeightPixels = buttonLabel.preferredHeight * canvasScale;
                Assert.GreaterOrEqual(
                    ctaCenterHeightPixels,
                    labelPreferredHeightPixels + 2f,
                    $"The CTA must be tall enough to hold its live label at {width:0}x{height:0}.");
            }
            finally
            {
                harness.Destroy();
            }
        }

        [Test]
        public void PortraitLoginCardCompressesHeightByFifteenToTwentyPercent()
        {
            var harness = CreateHarness(new Vector2(390f, 844f));
            try
            {
                Invoke(harness.App, "ShowLogin");
                var panel = FindRect(harness.Root, "LoginPanel");
                var panelRatio = RelativeRect(harness.Root, panel).height / harness.Root.rect.height;
                var compressionRatio = panelRatio / 0.685f;
                Assert.That(compressionRatio, Is.InRange(0.80f, 0.85f), "Portrait login card should be 15–20% shorter than the previous 68.5%-height card.");
                AssertRectsDoNotOverlap(harness.Root, panel, FindRect(harness.Root, "LoginBrand"), "Portrait crest lockup and card must not overlap.");
                AssertRectContains(harness.Root, panel, "Portrait login card must remain inside the viewport.");
                Assert.IsEmpty(UiLayoutQa.Scan(harness.Root));
            }
            finally
            {
                harness.Destroy();
            }
        }

        [Test]
        public void LoginFailureUpdatesStatusInPlaceAndRetainsOnlyLoginId()
        {
            var harness = CreateHarness(new Vector2(808f, 570f));
            GameObject eventSystemObject = null;
            try
            {
                eventSystemObject = new GameObject("LoginFailureEventSystem", typeof(EventSystem));
                var eventSystem = eventSystemObject.GetComponent<EventSystem>();
                Invoke(harness.App, "ShowLogin");
                var panel = FindRect(harness.Root, "LoginPanel");
                var status = FindText(harness.Root, "LoginStatusMessage");
                var loginId = FindInput(harness.Root, "LoginIdInput");
                var password = FindInput(harness.Root, "PasswordInput");
                loginId.text = harness.Member.LoginId;
                password.text = "wrong-password";

                InvokeWithArgs(harness.App, "TryLocalLogin", loginId.text, password.text);
                Canvas.ForceUpdateCanvases();

                Assert.AreSame(panel, FindRect(harness.Root, "LoginPanel"), "Authentication failure must not rebuild the login card.");
                Assert.AreSame(status, FindText(harness.Root, "LoginStatusMessage"), "Authentication failure must update the existing status Text.");
                Assert.AreEqual(harness.Member.LoginId, loginId.text, "Login ID should be retained after failure.");
                Assert.IsEmpty(password.text, "Password should be cleared after failure.");
                Assert.AreEqual("IDまたはパスワードが違います", status.text);
                Assert.AreSame(password, GetField(harness.App, "lastLoginFocusTarget"), "Password should regain focus after failure.");
                Assert.IsTrue(FindButton(harness.Root, "Login").interactable);
            }
            finally
            {
                if (eventSystemObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(eventSystemObject);
                }

                harness.Destroy();
            }
        }

        [Test]
        public void LoginBusyAndEnterSubmitStatesAreVisibleWithoutRebuilding()
        {
            var harness = CreateHarness(new Vector2(808f, 570f));
            GameObject eventSystemObject = null;
            try
            {
                eventSystemObject = new GameObject("LoginSubmitEventSystem", typeof(EventSystem));
                var eventSystem = eventSystemObject.GetComponent<EventSystem>();
                Invoke(harness.App, "ShowLogin");
                var panel = FindRect(harness.Root, "LoginPanel");
                var button = FindButton(harness.Root, "Login");
                var buttonLabel = FindText(harness.Root, "LoginButtonLabel");
                var loginId = FindInput(harness.Root, "LoginIdInput");
                var password = FindInput(harness.Root, "PasswordInput");

                InvokeWithArgs(harness.App, "SetLoginBusyState", true);
                Assert.IsFalse(button.interactable);
                Assert.IsFalse(loginId.interactable);
                Assert.IsFalse(password.interactable);
                Assert.AreEqual("接続中…", buttonLabel.text);

                InvokeWithArgs(harness.App, "SetLoginBusyState", false);
                Assert.IsTrue(button.interactable);
                Assert.AreEqual("ログイン", buttonLabel.text);

                var submit = password.GetComponent<EventTrigger>().triggers.Single(entry => entry.eventID == EventTriggerType.Submit);
                submit.callback.Invoke(new BaseEventData(eventSystem));
                Assert.AreSame(panel, FindRect(harness.Root, "LoginPanel"), "Enter validation must update the existing login card.");
                Assert.AreEqual("ログインIDとパスワードを入力してください", FindText(harness.Root, "LoginStatusMessage").text);
                Assert.AreSame(loginId, GetField(harness.App, "lastLoginFocusTarget"), "Missing Login ID should receive focus after Enter submit.");
            }
            finally
            {
                if (eventSystemObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(eventSystemObject);
                }

                harness.Destroy();
            }
        }

        [Test]
        public void MemberHomeNavigationButtonsOpenFocusedScreens()
        {
            var harness = CreateHarness(new Vector2(1440f, 1024f));
            try
            {
                SeedLongContent(harness);
                SetField(harness.App, "currentUser", harness.Member);

                Invoke(harness.App, "ShowMemberHome");
                InvokeButton(harness.Root, "HomeNav_目標");
                AssertScreenContains(harness.Root, "開発ログ");

                Invoke(harness.App, "ShowMemberHome");
                InvokeButton(harness.Root, "HomeNav_チーム");
                AssertScreenContains(harness.Root, "仲間の現在地");

                Invoke(harness.App, "ShowMemberHome");
                InvokeButton(harness.Root, "HomeNav_記録");
                AssertScreenContains(harness.Root, "最近の開発記録");

                Invoke(harness.App, "ShowMemberHome");
                InvokeButton(harness.Root, "PrimaryDevelopmentCompass");
                AssertScreenContains(harness.Root, "開発ログ");

                Invoke(harness.App, "ShowMemberHome");
                InvokeButton(harness.Root, "SettingsButton");
                AssertScreenContains(harness.Root, "設定");
            }
            finally
            {
                harness.Destroy();
            }
        }

        [Test]
        public void MentorNavigationButtonsOpenManagementScreens()
        {
            var harness = CreateHarness(new Vector2(1280f, 720f));
            try
            {
                SeedLongContent(harness);
                SetField(harness.App, "currentUser", harness.Mentor);

                Invoke(harness.App, "ShowMentorDashboard");
                InvokeButton(harness.Root, "MentorOperationsButton");
                AssertScreenContains(harness.Root, "運用メニュー");

                Invoke(harness.App, "ShowMentorOperations");
                InvokeButton(harness.Root, "DashboardAction_チーム状況");
                AssertScreenContains(harness.Root, "チーム状況");

                Invoke(harness.App, "ShowMentorOperations");
                InvokeButton(harness.Root, "DashboardAction_作品管理");
                AssertScreenContains(harness.Root, "プロダクト");

                Invoke(harness.App, "ShowMentorOperations");
                InvokeButton(harness.Root, "DashboardAction_実績承認");
                AssertScreenContains(harness.Root, "実績");

                Invoke(harness.App, "ShowMentorOperations");
                InvokeButton(harness.Root, "DashboardAction_アカウント管理");
                AssertScreenContains(harness.Root, "アカウント管理");
            }
            finally
            {
                harness.Destroy();
            }
        }

        [Test]
        public void MentorAccountListPaginatesFiftyTwoPlusAccountsTwelveAtATime()
        {
            var harness = CreateHarness(new Vector2(1280f, 720f));
            try
            {
                SetField(harness.App, "currentUser", harness.Mentor);
                SeedAccountsUntil(harness, 53);

                Invoke(harness.App, "ShowMentorAccountList");

                Assert.AreEqual(12, FindMemberAccountRows(harness.Root).Count, "The account list must instantiate only one 12-row page for 52+ connected users.");
                var firstPageRows = FindMemberAccountRows(harness.Root).Select(rect => rect.name).ToArray();
                var firstPageIndicator = FindText(harness.Root, "MentorAccountPageIndicator");
                StringAssert.Contains("1 / 5", firstPageIndicator.text);
                StringAssert.Contains("1–12 / 53件", firstPageIndicator.text);
                Assert.IsFalse(FindButton(harness.Root, "MentorAccountPreviousPage").interactable, "Previous must be disabled on the first page.");
                Assert.IsTrue(FindButton(harness.Root, "MentorAccountNextPage").interactable, "Next must be available while more accounts remain.");

                InvokeButton(harness.Root, "MentorAccountNextPage");

                Assert.AreEqual(1, GetField(harness.App, "mentorAccountPage"));
                var secondPageRows = FindMemberAccountRows(harness.Root).Select(rect => rect.name).ToArray();
                Assert.AreEqual(12, secondPageRows.Length, "Moving forward must still instantiate only one page.");
                Assert.IsEmpty(firstPageRows.Intersect(secondPageRows), "Next must replace the first page with a different account slice.");
                var secondPageIndicator = FindText(harness.Root, "MentorAccountPageIndicator");
                StringAssert.Contains("2 / 5", secondPageIndicator.text);
                StringAssert.Contains("13–24 / 53件", secondPageIndicator.text);
                Assert.IsTrue(FindButton(harness.Root, "MentorAccountPreviousPage").interactable);
                Assert.IsTrue(FindButton(harness.Root, "MentorAccountNextPage").interactable);
            }
            finally
            {
                harness.Destroy();
            }
        }

        [Test]
        public void MentorAccountRoleAndRankingRerendersRetainAllDraftInputs()
        {
            var harness = CreateHarness(new Vector2(1280f, 720f));
            try
            {
                SetField(harness.App, "currentUser", harness.Mentor);
                Invoke(harness.App, "ShowMentorAccounts");

                SetInputDraft(harness.Root, "MemberLoginIdInput", "retained-login-52");
                SetInputDraft(harness.Root, "MemberNicknameInput", "入力中のニックネーム");
                SetInputDraft(harness.Root, "MemberTeamIdInput", "retained-team");

                InvokeButton(harness.Root, "Select_Mentor");

                Assert.AreEqual(UserRole.Mentor, GetField(harness.App, "selectedAccountRole"));
                AssertMentorAccountDrafts(harness.Root, "retained-login-52", "入力中のニックネーム", "retained-team");

                InvokeButton(harness.Root, "Select_False");

                Assert.AreEqual(false, GetField(harness.App, "selectedAccountRankingVisible"));
                AssertMentorAccountDrafts(harness.Root, "retained-login-52", "入力中のニックネーム", "retained-team");
            }
            finally
            {
                harness.Destroy();
            }
        }

        [TestCase(1280f, 720f)]
        [TestCase(1366f, 768f)]
        [TestCase(1672f, 941f)]
        [TestCase(1920f, 1080f)]
        [TestCase(808f, 570f)]
        [TestCase(1440f, 1024f)]
        [TestCase(844f, 390f)]
        [TestCase(390f, 844f)]
        public void ActiveBattleMatchesBrightReferenceHudZonesWithoutRedundantTopState(float width, float height)
        {
            var harness = CreateHarness(new Vector2(width, height));
            try
            {
                SetField(harness.App, "currentUser", harness.Member);
                harness.Repository.StartBattle(harness.Mentor.Id);
                Invoke(harness.App, "ShowBattle");

                var headerTitle = FindText(harness.Root, "BattleHeaderTitle");
                var bossHp = FindRect(harness.Root, "BattleBossHpHud");
                var bossName = FindText(harness.Root, "BattleBossName");
                var bossHpValue = FindText(harness.Root, "BattleBossHpValue");
                var bossBacking = FindRect(harness.Root, "BattleBossHpBacking");
                var actionBand = FindRect(harness.Root, "BattleActionBand");
                var commandSummary = harness.Root.GetComponentsInChildren<Text>(true)
                    .FirstOrDefault(text => text.name == "BattleCommandSummary");
                var turnValue = FindText(harness.Root, "BattleBandMetric_TURN_Value");
                var commandButton = FindButton(harness.Root, "OpenCommandDeck");
                Assert.IsTrue(harness.Root.GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == "QuickAttack_SwordMark"),
                    "Attack uses a purpose-built Unity UI sword mark, not an unrelated playback icon or emoji.");
                Assert.IsTrue(harness.Root.GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == "BattleBossMark_Core"),
                    "The boss portrait and turn cue need a distinct low-poly crystal mark.");
                var rootRect = harness.Root.rect;
                var bossHpRect = RelativeRect(harness.Root, bossHp);

                Assert.AreEqual("ボス戦", headerTitle.text);
                if (width < 1000f || height < 600f)
                {
                    Assert.IsNotNull(commandSummary, "Constrained layouts retain the compact role/weapon summary.");
                    Assert.AreEqual("攻撃 / ブレード", commandSummary.text, "The compact command summary must use a complete, understandable weapon label.");
                }
                else
                {
                    Assert.IsNull(commandSummary, "The desktop reference keeps the arena clean and does not repeat role/weapon copy above the commands.");
                }
                Assert.IsFalse(harness.Root.GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == "BattleHeaderSubtitle"),
                    "Turn/state copy belongs in the bottom action band; a second top heading is absent from the approved bright reference.");
                Assert.IsFalse(harness.Root.GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == "BattleBossTitle"), "The retired duplicate phase slot must not return.");
                Assert.IsFalse(harness.Root.GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == "BattleCompactStateHud"), "Active state metrics belong inside the single bottom action band.");
                Assert.AreEqual(1, harness.Root.GetComponentsInChildren<RectTransform>(true).Count(rect => rect.name == "BattleActionBand"));
                Assert.GreaterOrEqual(bossHpRect.yMin, rootRect.yMin + rootRect.height * 0.79f, "The HP frame must stay high enough to leave the boss head and upper body unobscured.");
                Assert.GreaterOrEqual(EffectivePixelHeight(harness.Root, bossHp), 50f, "The compact HP frame must still keep readable internal space at WebGL sizes.");
                Assert.GreaterOrEqual(EffectivePixelFontSize(headerTitle), 18f, "The battle title must read as a primary heading.");
                Assert.GreaterOrEqual(EffectivePixelFontSize(bossName), 16f, "Boss identity must remain readable at every supported viewport.");
                if (width < 1000f || height < 600f)
                {
                    Assert.IsTrue(bossHpValue.gameObject.activeSelf, "Constrained layouts retain the numeric boss HP fallback.");
                    Assert.GreaterOrEqual(EffectivePixelFontSize(bossHpValue), 13f, "Boss HP values must not collapse into decorative noise.");
                }
                else
                {
                    Assert.IsFalse(bossHpValue.gameObject.activeSelf,
                        "The desktop reference uses a clean boss-name-and-red-bar pill without redundant numeric HP copy.");
                }
                Assert.GreaterOrEqual(bossBacking.GetComponent<Image>().color.a, 0.94f,
                    "The outlined boss HUD needs an opaque charcoal backing instead of showing the cyan sky through its centre.");
                Assert.LessOrEqual(bossBacking.GetComponent<Image>().color.r, 0.10f,
                    "The boss HUD fill must remain an unmistakable dark navy pill rather than a cyan sky banner.");
                Assert.GreaterOrEqual(EffectivePixelFontSize(turnValue), 14f, "Turn status must remain legible in the command band.");
                if (commandSummary != null)
                {
                    Assert.GreaterOrEqual(EffectivePixelFontSize(commandSummary), 12.5f, "The compact command summary must remain readable without aggressive best-fit shrinking.");
                }
                Assert.GreaterOrEqual(EffectiveButtonLabelSize(harness.Root, commandButton), 14f,
                    "The baked command label must account for the WebGL canvas scale instead of shrinking below readability.");
                var turnOrder = harness.Root.GetComponentsInChildren<RectTransform>(true)
                    .FirstOrDefault(rect => rect.name == "BattleTurnOrderStrip");
                if (width >= 1000f && height >= 600f)
                {
                    Assert.IsNotNull(turnOrder, "Wide battle layouts should retain the readable portrait turn strip from the approved reference.");
                    Assert.AreEqual(7, turnOrder.childCount, "The turn strip should show four portraits separated by three explicit arrows.");
                    Assert.AreEqual("BattleTurnOrderParticipant_0", turnOrder.GetChild(0).name);
                    Assert.AreEqual("BattleTurnOrderArrow_0", turnOrder.GetChild(1).name);
                    Assert.AreEqual("BattleTurnOrderParticipant_1", turnOrder.GetChild(2).name);
                    Assert.AreEqual("BattleTurnOrderArrow_1", turnOrder.GetChild(3).name);
                    Assert.AreEqual("BattleTurnOrderBoss", turnOrder.GetChild(4).name,
                        "The boss interruption portrait should sit between the second and third party turns like the approved reference.");
                    Assert.AreEqual("BattleTurnOrderArrow_2", turnOrder.GetChild(5).name);
                    Assert.AreEqual("BattleTurnOrderParticipant_2", turnOrder.GetChild(6).name);
                    Assert.AreEqual(3, turnOrder.GetComponentsInChildren<RectTransform>(true).Count(rect => rect.name == "BattleTurnPortrait_Face"),
                        "Every party turn item must read as a character portrait rather than an abstract action icon.");
                    Assert.AreEqual(3, turnOrder.GetComponentsInChildren<RectTransform>(true).Count(rect => rect.name == "BattleTurnPortrait_AuthoredHero"),
                        "The visible hero turns must use the authored low-poly portrait sprites instead of code-native face approximations.");
                    Assert.AreEqual(1, turnOrder.GetComponentsInChildren<RectTransform>(true).Count(rect => rect.name == "BattleTurnPortrait_AuthoredBoss"),
                        "The boss turn must use the authored Coral Beetle portrait sprite.");
                    Assert.IsTrue(turnOrder.GetComponentsInChildren<Image>(true)
                        .Where(image => image.name == "BattleTurnPortrait_AuthoredHero" || image.name == "BattleTurnPortrait_AuthoredBoss")
                        .All(image => image.sprite != null && image.color == Color.white),
                        "Authored portraits must render as untinted real raster assets.");
                    var authoredBossHud = harness.Root.GetComponentsInChildren<Image>(true)
                        .Single(image => image.name == "BattleTurnPortrait_AuthoredBossHud");
                    Assert.IsNotNull(authoredBossHud.sprite, "The upper-left boss pill should use the same authored Coral Beetle identity as the turn order.");
                    Assert.AreEqual(Color.white, authoredBossHud.color, "The authored boss HUD portrait must remain untinted.");
                    Assert.AreEqual(3, turnOrder.GetComponentsInChildren<RectTransform>(true).Count(rect => rect.name.StartsWith("BattleTurnOrderArrow_", StringComparison.Ordinal)),
                        "Three existing arrow assets should make the four-step chronology unambiguous.");
                    AssertRectContains(harness.Root, turnOrder, "The turn strip must stay inside the WebGL viewport.");
                    Assert.LessOrEqual(
                        bossHpRect.width / rootRect.width,
                        0.335f,
                        "The desktop boss bar should remain a compact upper-left pill instead of spanning most of the arena.");
                    Assert.GreaterOrEqual(
                        bossHpRect.width / rootRect.width,
                        0.30f,
                        "The desktop boss bar must still have enough width for the boss name, HP value, and portrait.");
                    Assert.AreSame(
                        UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.LoginStatusFramePath),
                        bossHp.GetComponent<Image>().sprite,
                        "The desktop boss HUD should use the thin status frame instead of the heavy login CTA chrome.");
                    Assert.GreaterOrEqual(
                        EffectivePixelHeight(harness.Root, commandButton.GetComponent<RectTransform>()),
                        145f,
                        "Desktop command hexes should retain the large, game-like scale of the approved battle reference.");
                    Assert.GreaterOrEqual(EffectiveButtonLabelSize(harness.Root, commandButton), 21f,
                        "Desktop command labels must be readable at a glance like the approved bright reference.");

                    var partyCards = harness.Root.GetComponentsInChildren<RectTransform>(true)
                        .Where(rect => rect.name.StartsWith("BattlePartyCard_", StringComparison.Ordinal))
                        .ToArray();
                    Assert.AreEqual(3, partyCards.Length, "The bright battle HUD should show exactly three readable party cards.");
                    foreach (var partyCard in partyCards)
                    {
                        var nickname = partyCard.GetComponentsInChildren<Text>(true).Single(text => text.name == "Nickname");
                        var hpValue = partyCard.GetComponentsInChildren<Text>(true).Single(text => text.name == "HPValue");
                        var mpValue = partyCard.GetComponentsInChildren<Text>(true).Single(text => text.name == "MPValue");
                        Assert.GreaterOrEqual(EffectivePixelHeight(harness.Root, partyCard), 112f, "Desktop party cards need the taller readable proportion of the approved reference.");
                        Assert.IsFalse(nickname.gameObject.activeSelf,
                            "Desktop combat cards should omit profile names and reserve their clean white surface for HP/MP, matching the reference.");
                        Assert.GreaterOrEqual(EffectivePixelFontSize(hpValue), 17f, "Desktop HP values must remain readable.");
                        Assert.GreaterOrEqual(EffectivePixelFontSize(mpValue), 17f, "Desktop MP values must remain readable.");
                        Assert.AreEqual(TextAnchor.MiddleRight, hpValue.alignment, "HP values should lock to the readable right edge of each stat card.");
                        Assert.AreEqual(TextAnchor.MiddleRight, mpValue.alignment, "MP values should lock to the readable right edge of each stat card.");
                        Assert.LessOrEqual(partyCard.rect.xMax - RelativeRect(partyCard, hpValue.rectTransform).xMax, partyCard.rect.width * 0.05f,
                            "HP values need the full right-side value column used by the reference card.");
                        Assert.LessOrEqual(partyCard.rect.xMax - RelativeRect(partyCard, mpValue.rectTransform).xMax, partyCard.rect.width * 0.05f,
                            "MP values need the full right-side value column used by the reference card.");
                    }

                    var quickAttackDesktop = FindButton(harness.Root, "QuickAttack").GetComponent<RectTransform>();
                    var quickGuardDesktop = FindButton(harness.Root, "QuickGuard").GetComponent<RectTransform>();
                    var skillDesktop = commandButton.GetComponent<RectTransform>();
                    var turnBadgeDesktop = FindRect(harness.Root, "BattleTurnBadge");
                    Assert.Greater(
                        EffectivePixelHeight(harness.Root, skillDesktop),
                        EffectivePixelHeight(harness.Root, quickAttackDesktop) * 1.15f,
                        "The selected skill hex should be visibly larger than the flanking attack and guard actions.");
                    Assert.AreEqual(
                        EffectivePixelHeight(harness.Root, quickAttackDesktop),
                        EffectivePixelHeight(harness.Root, quickGuardDesktop),
                        1.5f,
                        "Attack and guard should form an even pair around the larger skill command.");
                    Assert.AreEqual(
                        RelativeRect(harness.Root, quickAttackDesktop).yMin,
                        RelativeRect(harness.Root, quickGuardDesktop).yMin,
                        1.5f,
                        "The flanking command hexes should share one bottom baseline.");
                    Assert.AreEqual(
                        RelativeRect(harness.Root, quickAttackDesktop).yMin,
                        RelativeRect(harness.Root, skillDesktop).yMin,
                        1.5f,
                        "All three command hit targets should align to the bottom action edge.");
                    Assert.LessOrEqual(
                        RelativeRect(harness.Root, skillDesktop).yMin,
                        RelativeRect(harness.Root, quickAttackDesktop).yMin,
                        "The larger skill hex should reach at least as low as the flanking commands.");
                    AssertRectsDoNotOverlap(harness.Root, turnBadgeDesktop, quickAttackDesktop,
                        "The TURN pill should sit directly above the command row without covering Attack.");
                    AssertRectsDoNotOverlap(harness.Root, turnBadgeDesktop, skillDesktop,
                        "The TURN pill should sit directly above the command row without covering Skill.");
                }
                else
                {
                    Assert.IsNull(turnOrder, "Constrained layouts should preserve arena space instead of squeezing in the desktop turn strip.");
                }
                AssertRectsDoNotOverlap(harness.Root, headerTitle.rectTransform, bossHp, "The centred battle title and upper-left boss HUD need independent zones.");
                AssertRectsDoNotOverlap(harness.Root, bossHp, actionBand, "The boss HP frame and bottom action band must leave the arena readable.");
                if (commandSummary != null)
                {
                    AssertRectsDoNotOverlap(actionBand, commandSummary.rectTransform, commandButton.GetComponent<RectTransform>(), "The command summary and command button need separate safe areas.");
                }
                var quickAttack = FindButton(harness.Root, "QuickAttack");
                var quickGuard = FindButton(harness.Root, "QuickGuard");
                AssertRectsDoNotOverlap(actionBand, quickAttack.GetComponent<RectTransform>(), commandButton.GetComponent<RectTransform>(), "Attack and Skill hexes must not overlap.");
                AssertRectsDoNotOverlap(actionBand, commandButton.GetComponent<RectTransform>(), quickGuard.GetComponent<RectTransform>(), "Skill and Guard hexes must not overlap.");
                Assert.GreaterOrEqual(EffectivePixelHeight(harness.Root, commandButton.GetComponent<RectTransform>()), 44f, "The compact command target must remain at least 44 rendered pixels tall.");
                AssertRectContains(harness.Root, bossHp, "The boss HP frame must stay inside the WebGL viewport.");
                AssertRectContains(harness.Root, actionBand, "The bottom action band must stay inside the WebGL viewport.");
                Assert.IsEmpty(UiLayoutQa.Scan(harness.Root), $"Active battle layout QA failed at {width:0}x{height:0}.");
            }
            finally
            {
                harness.Destroy();
            }
        }

        [TestCase(1280f, 720f)]
        [TestCase(1366f, 768f)]
        [TestCase(1920f, 1080f)]
        [TestCase(808f, 570f)]
        [TestCase(1440f, 1024f)]
        [TestCase(844f, 390f)]
        [TestCase(390f, 844f)]
        public void ExpandedBattleCommandDrawerKeepsEveryActionTargetAtLeastFortyFourPixels(float width, float height)
        {
            var harness = CreateHarness(new Vector2(width, height));
            try
            {
                SetField(harness.App, "currentUser", harness.Member);
                harness.Repository.StartBattle(harness.Mentor.Id);
                Invoke(harness.App, "ShowBattle");
                InvokeButton(harness.Root, "OpenCommandDeck");

                var drawer = FindRect(harness.Root, "BattleCommandHud");
                var bossHp = FindRect(harness.Root, "BattleBossHpHud");
                Assert.IsFalse(harness.Root.GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == "BattleActionBand"), "The compact band should yield to the focused command drawer.");
                AssertRectsDoNotOverlap(harness.Root, drawer, bossHp, "The command drawer must not cover boss identity or HP.");

                var drawerButtons = drawer.GetComponentsInChildren<Button>(true);
                Assert.IsNotEmpty(drawerButtons);
                foreach (var button in drawerButtons)
                {
                    Assert.GreaterOrEqual(
                        EffectivePixelHeight(harness.Root, button.GetComponent<RectTransform>()),
                        44f,
                        $"{button.name} must remain at least 44 rendered pixels tall at {width:0}x{height:0}.");
                    Assert.GreaterOrEqual(
                        EffectiveButtonLabelSize(harness.Root, button),
                        12.5f,
                        $"{button.name} must bake or render its label at a readable size at {width:0}x{height:0}.");
                }

                Assert.IsEmpty(UiLayoutQa.Scan(harness.Root), $"Expanded battle command layout QA failed at {width:0}x{height:0}.");
            }
            finally
            {
                harness.Destroy();
            }
        }

        [TestCase(1440f, 1024f)]
        [TestCase(844f, 390f)]
        [TestCase(390f, 844f)]
        public void ScheduledAndResultBattleHudsReflowWithoutCollisions(float width, float height)
        {
            var harness = CreateHarness(new Vector2(width, height));
            try
            {
                SetField(harness.App, "currentUser", harness.Member);
                harness.Repository.ActiveBattle.Status = BattleStatus.Scheduled;
                Invoke(harness.App, "ShowBattle");

                var scheduledState = FindRect(harness.Root, "BattleCompactStateHud");
                var scheduledCommand = FindRect(harness.Root, "BattleCompactCommandHud");
                AssertRectsDoNotOverlap(harness.Root, scheduledState, scheduledCommand, "Scheduled state and action panels need separate responsive zones.");
                foreach (var button in scheduledCommand.GetComponentsInChildren<Button>(true))
                {
                    Assert.GreaterOrEqual(EffectivePixelHeight(harness.Root, button.GetComponent<RectTransform>()), 44f);
                    Assert.GreaterOrEqual(EffectiveButtonLabelSize(harness.Root, button), 12.5f);
                }
                Assert.IsEmpty(UiLayoutQa.Scan(harness.Root), $"Scheduled battle layout QA failed at {width:0}x{height:0}.");

                harness.Repository.ActiveBattle.Status = BattleStatus.Completed;
                harness.Repository.ActiveBattle.Phase = BattlePhase.Completed;
                harness.Repository.ActiveBattle.Outcome = BattleOutcome.Victory;
                harness.Repository.ActiveBattle.Boss.CurrentHp = 0;
                Invoke(harness.App, "ShowBattle");

                var resultState = FindRect(harness.Root, "BattleCompactStateHud");
                var resultCommand = FindRect(harness.Root, "BattleCompactCommandHud");
                AssertRectsDoNotOverlap(harness.Root, resultState, resultCommand, "Result state and action panels need separate responsive zones.");
                Assert.GreaterOrEqual(
                    EffectivePixelHeight(harness.Root, FindButton(harness.Root, "OpenBattleResult").GetComponent<RectTransform>()),
                    44f);
                Assert.IsEmpty(UiLayoutQa.Scan(harness.Root), $"Collapsed result battle layout QA failed at {width:0}x{height:0}.");

                SetField(harness.App, "battleStateExpanded", true);
                Invoke(harness.App, "ShowBattle");
                var resultStatePanel = FindRect(harness.Root, "BattleStateHud");
                Assert.IsFalse(harness.Root.GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == "BattleCompactStateHud"),
                    "Expanded result state should replace the redundant compact metric strip.");
                Assert.IsFalse(harness.Root.GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == "BattleCompactCommandHud"),
                    "Expanded result state must contain its result action instead of competing with a second floating CTA panel.");
                Assert.GreaterOrEqual(
                    EffectivePixelHeight(harness.Root, FindButton(harness.Root, "OpenBattleResult").GetComponent<RectTransform>()),
                    44f,
                    "The integrated result-details action must remain a real touch target.");
                AssertBattleMetricValuesReadable(harness.Root, resultStatePanel, width, height);
                Assert.IsEmpty(UiLayoutQa.Scan(harness.Root), $"Expanded result state layout QA failed at {width:0}x{height:0}.");

                InvokeButton(harness.Root, "OpenBattleResult");
                var resultPanel = FindRect(harness.Root, "BattleResultHud");
                Assert.IsFalse(harness.Root.GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == "BattleCompactStateHud"),
                    "The focused result panel should replace the compact state panel instead of overlapping it.");
                foreach (var button in resultPanel.GetComponentsInChildren<Button>(true))
                {
                    Assert.GreaterOrEqual(
                        EffectivePixelHeight(harness.Root, button.GetComponent<RectTransform>()),
                        44f,
                        $"{button.name} must remain a usable result action at {width:0}x{height:0}.");
                }
                Assert.IsEmpty(UiLayoutQa.Scan(harness.Root), $"Expanded result battle layout QA failed at {width:0}x{height:0}.");
            }
            finally
            {
                harness.Destroy();
            }
        }

        [TestCase(1440f, 1024f)]
        [TestCase(844f, 390f)]
        [TestCase(390f, 844f)]
        public void CooperativeTurnStatePanelKeepsTheCommandBandAndBossHudClear(float width, float height)
        {
            var harness = CreateHarness(new Vector2(width, height));
            try
            {
                SetField(harness.App, "currentUser", harness.Member);
                harness.Repository.StartBattle(harness.Mentor.Id);
                SetField(harness.App, "battleStateExpanded", true);
                SetField(harness.App, "lastBattleMessage", "連携攻撃成功 / 次の役割と武器を選択してください");
                Invoke(harness.App, "ShowBattle");

                var statePanel = FindRect(harness.Root, "BattleStateHud");
                var actionBand = FindRect(harness.Root, "BattleActionBand");
                var bossHp = FindRect(harness.Root, "BattleBossHpHud");
                AssertRectsDoNotOverlap(harness.Root, statePanel, actionBand, "Expanded turn status must not obscure the command band.");
                AssertRectsDoNotOverlap(harness.Root, statePanel, bossHp, "Expanded turn status must not obscure boss identity or HP.");
                AssertRectContains(harness.Root, statePanel, "Expanded turn status must stay inside the supported viewport.");
                AssertBattleMetricValuesReadable(harness.Root, statePanel, width, height);
                Assert.IsEmpty(UiLayoutQa.Scan(harness.Root), $"Cooperative turn layout QA failed at {width:0}x{height:0}.");
            }
            finally
            {
                harness.Destroy();
            }
        }

        [Test]
        public void BattleFollowCameraPreservesReferenceHorizontalFramingOnCompactWebGlAspect()
        {
            var wideFov = RaidFollowCamera.CalculateResponsiveBattleFieldOfView(54f, 16f / 9f);
            var compactFov = RaidFollowCamera.CalculateResponsiveBattleFieldOfView(54f, 808f / 570f);

            Assert.AreEqual(54f, wideFov, 0.001f);
            Assert.Greater(compactFov, wideFov, "A compact WebGL aspect needs more vertical FOV to preserve the same party width.");
            Assert.LessOrEqual(compactFov, 72f, "Responsive framing must not become an extreme fisheye view.");
        }

        [Test]
        public void NonBattleInputFieldsAreInteractiveAndLimited()
        {
            var harness = CreateHarness(new Vector2(1280f, 720f));
            try
            {
                SeedLongContent(harness);
                foreach (var testCase in new[]
                         {
                             (User: harness.Member, Method: "ShowDevLog", Inputs: new[] { "ReflectionInput", "NextTaskInput" }),
                             (User: harness.Member, Method: "ShowProducts", Inputs: new[] { "ProductTitleInput", "ProductUrlInput", "ProductDescriptionInput" }),
                             (User: harness.Member, Method: "ShowAchievements", Inputs: new[] { "AchievementTitleInput", "AchievementDescriptionInput" }),
                             (User: harness.Mentor, Method: "ShowMentorAccounts", Inputs: new[] { "MemberLoginIdInput", "MemberNicknameInput", "MemberTeamIdInput" })
                         })
                {
                    SetField(harness.App, "currentUser", testCase.User);
                    Invoke(harness.App, testCase.Method);
                    foreach (var inputName in testCase.Inputs)
                    {
                        var input = FindInput(harness.Root, inputName);
                        Assert.IsTrue(input.interactable, $"{testCase.Method} {inputName} should be interactable.");
                        Assert.IsTrue(input.targetGraphic == null || input.targetGraphic.raycastTarget, $"{testCase.Method} {inputName} should receive raycasts.");
                        Assert.Greater(input.characterLimit, 0, $"{testCase.Method} {inputName} should have a character limit.");
                    }
                }
            }
            finally
            {
                harness.Destroy();
            }
        }

        [Test]
        public void FrontDisplayUsesDevelopmentBoardCopyOnly()
        {
            var harness = CreateHarness(new Vector2(1280f, 720f));
            try
            {
                SeedLongContent(harness);
                SetField(harness.App, "currentUser", null);

                Invoke(harness.App, "ShowFrontScreen");

                AssertScreenContains(harness.Root, "開発状況");
                AssertScreenContains(harness.Root, "今週の開発");
                AssertButtonExists(harness.Root, "ログインして記録");
                AssertButtonExists(harness.Root, "ボス戦に参加");
                AssertScreenDoesNotContain(harness.Root, "表示専用");
                AssertScreenDoesNotContain(harness.Root, "自動更新");
                AssertScreenDoesNotContain(harness.Root, "CLASSROOM");
                AssertScreenDoesNotContain(harness.Root, "BOSS HP");
            }
            finally
            {
                harness.Destroy();
            }
        }

        [Test]
        public void AuthenticatedProjectorPreviewDoesNotRenderPrivateRepositoryBeforePublicProjection()
        {
            var harness = CreateHarness(new Vector2(1280f, 720f));
            try
            {
                const string privateNickname = "非公開プロジェクター漏えい検証メンバー";
                var privateMember = harness.Repository.CreateUserAccount(
                    harness.Mentor.Id,
                    "private-projector-member",
                    privateNickname,
                    UserRole.Member,
                    "private-team",
                    false).User;
                harness.Repository.StartBattle(harness.Mentor.Id);
                harness.Repository.SubmitBattleAction(
                    privateMember.Id,
                    BattleRole.Attacker,
                    WeaponKind.Blade,
                    BattleActionType.Strong);
                SetField(harness.App, "currentUser", harness.Member);

                Invoke(harness.App, "ShowFrontScreen");

                AssertScreenDoesNotContain(harness.Root, privateNickname);
                Assert.IsNotNull(
                    GetField(harness.App, "frontDisplayRepository"),
                    "Authenticated projector preview must use a distinct public cache.");
                Assert.AreNotSame(
                    harness.Repository,
                    GetField(harness.App, "frontDisplayRepository"));
            }
            finally
            {
                harness.Destroy();
            }
        }

        [Test]
        public void DevLogPrimaryActionsAreVisible()
        {
            var harness = CreateHarness(new Vector2(1280f, 720f));
            try
            {
                SetField(harness.App, "currentUser", harness.Member);
                Invoke(harness.App, "ShowDevLog");
                AssertButtonExists(harness.Root, "この目標で開始");

                harness.Repository.StartSession(harness.Member.Id, LongJapaneseText);
                Invoke(harness.App, "ShowDevLog");
                AssertButtonExists(harness.Root, "開発を終了して記録する");
            }
            finally
            {
                harness.Destroy();
            }
        }

        [TestCase(844f, 390f)]
        [TestCase(390f, 844f)]
        public void ResponsiveFrontDisplayKeepsPhaseIdentityAndActionsVisible(float width, float height)
        {
            var harness = CreateHarness(new Vector2(width, height));
            try
            {
                SetField(harness.App, "currentUser", null);

                Invoke(harness.App, "ShowFrontScreen");
                AssertScreenContains(harness.Root, "次のレイドを準備中");
                AssertResponsiveFrontControls(harness.Root, width, height);

                harness.Repository.StartBattle(harness.Mentor.Id);
                Invoke(harness.App, "ShowFrontScreen");
                AssertScreenContains(harness.Root, "LIVE — 仲間が戦闘中");
                AssertResponsiveFrontControls(harness.Root, width, height);

                harness.Repository.ActiveBattle.Status = BattleStatus.Completed;
                harness.Repository.ActiveBattle.Phase = BattlePhase.Completed;
                harness.Repository.ActiveBattle.Outcome = BattleOutcome.Victory;
                harness.Repository.ActiveBattle.Boss.CurrentHp = 0;
                Invoke(harness.App, "ShowFrontScreen");
                AssertScreenContains(harness.Root, "勝利 — RAID COMPLETE");
                AssertScreenContains(harness.Root, "報酬を集計しました");
                AssertResponsiveFrontControls(harness.Root, width, height);
            }
            finally
            {
                harness.Destroy();
            }
        }

        [TestCase(844f, 390f)]
        [TestCase(390f, 844f)]
        public void CriticalResponsiveScreensKeepCompleteHeadingsAndMeaningfulMetricValues(float width, float height)
        {
            var harness = CreateHarness(new Vector2(width, height));
            try
            {
                SetField(harness.App, "currentUser", harness.Member);
                Invoke(harness.App, "ShowInitialPasswordChange");
                AssertScreenContains(harness.Root, "初回パスワード変更");
                AssertNoEllipsisOnlyLabels(FindRect(harness.Root, "InitialPasswordPanel"));
                Assert.IsEmpty(UiLayoutQa.Scan(harness.Root));

                SetField(harness.App, "currentUser", null);
                Invoke(harness.App, "ShowFrontScreen");
                AssertScreenContains(harness.Root, "今週の開発");
                AssertNoEllipsisOnlyLabels(FindRect(harness.Root, width < height ? "FrontPortraitMetrics" : "FrontCompactMetrics"));
                AssertNoEllipsisOnlyLabels(FindRect(harness.Root, "FrontHighlightCard"));
                Assert.IsEmpty(UiLayoutQa.Scan(harness.Root));

                SetField(harness.App, "currentUser", harness.Mentor);
                Invoke(harness.App, "ShowMentorDashboard");
                AssertScreenContains(harness.Root, "メンター画面");
                AssertNoEllipsisOnlyLabels(FindRect(harness.Root, "MentorStatusPanel"));
                Assert.IsEmpty(UiLayoutQa.Scan(harness.Root));

                SetField(harness.App, "currentUser", harness.Member);
                Invoke(harness.App, "ShowRanking");
                AssertScreenContains(harness.Root, "開発時間ランキング");
                var rankingPanel = FindRect(harness.Root, "RankingPanel");
                AssertNoEllipsisOnlyLabels(rankingPanel);
                Assert.IsEmpty(UiLayoutQa.Scan(harness.Root));
            }
            finally
            {
                harness.Destroy();
            }
        }

        [TestCase(844f, 390f)]
        [TestCase(390f, 844f)]
        public void ResponsiveMemberCatalogsExposeFocusedListAndFormModes(float width, float height)
        {
            var harness = CreateHarness(new Vector2(width, height));
            try
            {
                SeedLongContent(harness);
                SetField(harness.App, "currentUser", harness.Member);

                Invoke(harness.App, "ShowProducts");
                AssertButtonExists(harness.Root, "ProductListTab");
                AssertButtonExists(harness.Root, "ProductFormTab");
                Assert.IsNotNull(FindRect(harness.Root, "ResponsiveProductList"));
                InvokeButton(harness.Root, "ProductFormTab");
                FindInput(harness.Root, "ProductTitleInput");
                FindInput(harness.Root, "ProductUrlInput");
                FindInput(harness.Root, "ProductDescriptionInput");
                AssertButtonExists(harness.Root, "RegisterProductUrl");
                Assert.IsEmpty(UiLayoutQa.Scan(harness.Root), $"Responsive product form failed at {width:0}x{height:0}.");

                Invoke(harness.App, "ShowAchievements");
                AssertButtonExists(harness.Root, "AchievementListTab");
                AssertButtonExists(harness.Root, "AchievementFormTab");
                InvokeButton(harness.Root, "AchievementFormTab");
                FindInput(harness.Root, "AchievementTitleInput");
                FindInput(harness.Root, "AchievementDescriptionInput");
                AssertButtonExists(harness.Root, "SubmitAchievement");
                Assert.IsEmpty(UiLayoutQa.Scan(harness.Root), $"Responsive achievement form failed at {width:0}x{height:0}.");
            }
            finally
            {
                harness.Destroy();
            }
        }

        [TestCase(844f, 390f)]
        [TestCase(390f, 844f)]
        public void ResponsiveMemberAndMentorScreensKeepProductionActionsDiscoverable(float width, float height)
        {
            var harness = CreateHarness(new Vector2(width, height));
            try
            {
                SeedLongContent(harness);
                SetField(harness.App, "currentUser", harness.Member);

                Invoke(harness.App, "ShowMemberTeam");
                AssertButtonExists(harness.Root, "OpenCosmeticWardrobe");
                AssertButtonExists(harness.Root, "OpenWeaponWish");
                AssertButtonExists(harness.Root, "OpenTeamRanking");

                Invoke(harness.App, "ShowCosmeticWardrobe");
                Assert.IsNotNull(FindRect(harness.Root, "ResponsiveEquippedLoadout"));
                AssertButtonExists(harness.Root, "WardrobeOpenWeaponWish");
                AssertButtonExists(harness.Root, "RefreshCosmeticInventory");

                Invoke(harness.App, "ShowWeaponWish");
                Assert.IsNotNull(FindRect(harness.Root, "WishResultCard"));
                Assert.IsNotNull(FindRect(harness.Root, "WishRewardVisual"));
                AssertButtonExists(harness.Root, "RollWeaponWish");
                AssertButtonExists(harness.Root, "WishOpenWardrobe");

                Invoke(harness.App, "ShowSettings");
                AssertButtonExists(harness.Root, "SettingsFrontDisplay");
                AssertButtonExists(harness.Root, "SettingsReturn");
                AssertButtonExists(harness.Root, "SettingsLogout");

                SetField(harness.App, "currentUser", harness.Mentor);
                Invoke(harness.App, "ShowMentorOperations");
                AssertButtonExists(harness.Root, "DashboardAction_前面表示");
                AssertButtonExists(harness.Root, "DashboardAction_チーム状況");
                AssertButtonExists(harness.Root, "DashboardAction_作品管理");
                AssertButtonExists(harness.Root, "DashboardAction_実績承認");
                AssertButtonExists(harness.Root, "DashboardAction_アカウント管理");
                AssertButtonExists(harness.Root, "MentorOperationsBattleAction");

                Invoke(harness.App, "ShowMentorReviewQueue");
                AssertButtonExists(harness.Root, "Approve");
                AssertButtonExists(harness.Root, "OpenCorrection");
                AssertButtonExists(harness.Root, "Reject");
                InvokeButton(harness.Root, "OpenCorrection");
                FindInput(harness.Root, "CorrectedDuration");
                FindInput(harness.Root, "MentorCommentInput");
                AssertButtonExists(harness.Root, "ApproveWithCorrections");

                Invoke(harness.App, "ShowMentorAccounts");
                FindInput(harness.Root, "MemberLoginIdInput");
                FindInput(harness.Root, "MemberNicknameInput");
                FindInput(harness.Root, "MemberTeamIdInput");
                AssertButtonExists(harness.Root, "アカウントを発行");
                AssertButtonExists(harness.Root, "MentorAccountListTab");
                Assert.IsEmpty(UiLayoutQa.Scan(harness.Root), $"Responsive mentor controls failed at {width:0}x{height:0}.");
            }
            finally
            {
                harness.Destroy();
            }
        }

        [TestCase(844f, 390f, 1)]
        [TestCase(390f, 844f, 2)]
        public void ResponsiveMentorAccountListUsesBoundedPageSizes(float width, float height, int expectedRows)
        {
            var harness = CreateHarness(new Vector2(width, height));
            try
            {
                SetField(harness.App, "currentUser", harness.Mentor);
                SeedAccountsUntil(harness, 53);

                Invoke(harness.App, "ShowMentorAccountList");

                Assert.AreEqual(expectedRows, FindMemberAccountRows(harness.Root).Count);
                AssertButtonExists(harness.Root, "MentorAccountNextPage");
                AssertRectContains(harness.Root, FindRect(harness.Root, "MentorAccountPagination"), "Pagination must remain inside the supported viewport.");
                Assert.IsEmpty(UiLayoutQa.Scan(harness.Root), $"Responsive account paging failed at {width:0}x{height:0}.");
            }
            finally
            {
                harness.Destroy();
            }
        }

        [TestCase(844f, 390f)]
        [TestCase(390f, 844f)]
        public void TemporaryPasswordRequiresExplicitRevealAndClearsWhenClosed(float width, float height)
        {
            var harness = CreateHarness(new Vector2(width, height));
            try
            {
                SetField(harness.App, "currentUser", harness.Mentor);
                Invoke(harness.App, "ShowMentorAccounts");
                SetInputDraft(harness.Root, "MemberLoginIdInput", $"responsive-{width:0}-{height:0}");
                SetInputDraft(harness.Root, "MemberNicknameInput", "テスト冒険者");
                SetInputDraft(harness.Root, "MemberTeamIdInput", "blue");

                InvokeButton(harness.Root, "アカウントを発行");

                var secret = (string)GetField(harness.App, "pendingTemporaryPasswordReveal");
                Assert.IsNotEmpty(secret);
                StringAssert.DoesNotContain(secret, (string)GetField(harness.App, "lastMentorMessage"));
                Assert.IsFalse(harness.Root.GetComponentsInChildren<Text>(true).Any(text => text.text.Contains(secret, StringComparison.Ordinal)), "A generated password must not render before an explicit reveal action.");
                AssertButtonExists(harness.Root, "TemporaryPasswordRevealAction");

                InvokeButton(harness.Root, "TemporaryPasswordRevealAction");
                Assert.IsTrue(harness.Root.GetComponentsInChildren<Text>(true).Any(text => text.text.Contains(secret, StringComparison.Ordinal)), "Explicit reveal must show the one-time password to the mentor.");
                Assert.AreEqual(true, GetField(harness.App, "temporaryPasswordRevealVisible"));

                InvokeButton(harness.Root, "TemporaryPasswordRevealAction");
                Assert.AreEqual(string.Empty, GetField(harness.App, "pendingTemporaryPasswordReveal"));
                Assert.IsFalse(harness.Root.GetComponentsInChildren<Text>(true).Any(text => text.text.Contains(secret, StringComparison.Ordinal)), "Closing the reveal must remove the secret from the UI tree.");
            }
            finally
            {
                harness.Destroy();
            }
        }

        [TestCase(844f, 390f)]
        [TestCase(390f, 844f)]
        public void AiPendingDevLogShowsAReadableManualRefreshFallback(float width, float height)
        {
            var harness = CreateHarness(new Vector2(width, height));
            try
            {
                var client = (SupabaseGameClient)GetField(harness.App, "supabase");
                var config = new SupabaseRuntimeConfigDto
                {
                    Enabled = true,
                    SupabaseUrl = "https://example.supabase.co/",
                    SupabasePublishableKey = "sb_publishable_ui-audit-key",
                    ApiContractVersion = SupabaseGameApiContract.CurrentVersion,
                    ApiFunctionName = SupabaseGameApiContract.DefaultFunctionName
                };
                Assert.IsTrue(client.TryConfigureFromJson(JsonUtility.ToJson(config)));
                client.RestoreSessionToken("ui-audit-session-token");

                SetField(harness.App, "currentUser", harness.Member);
                harness.Repository.StartSession(harness.Member.Id, "AI評価の反映確認を実装する");
                harness.Repository.CompleteSessionWithAiFailure(harness.Member.Id, 80, "有限再取得を実装した", "回帰テストを確認する", "ai_pending");
                Invoke(harness.App, "ShowDevLog");

                AssertScreenContains(harness.Root, "AI評価待ち");
                var refresh = FindButton(harness.Root, "AiEvaluationRefresh");
                Assert.GreaterOrEqual(EffectivePixelHeight(harness.Root, refresh.GetComponent<RectTransform>()), 44f);
                Assert.IsEmpty(UiLayoutQa.Scan(harness.Root), $"AI pending fallback failed at {width:0}x{height:0}.");
            }
            finally
            {
                harness.Destroy();
            }
        }

        [TestCase(844f, 390f)]
        [TestCase(390f, 844f)]
        public void ConnectionGateKeepsItsReasonAndRecoveryActionVisible(float width, float height)
        {
            var harness = CreateHarness(new Vector2(width, height));
            try
            {
                InvokeWithArgs(harness.App, "ShowSupabaseConnectionGate", "冒険者の記録を読み込めません", LongAuthenticationError);

                var gate = FindRect(harness.Root, "JourneyConnectionGate");
                AssertScreenContains(harness.Root, "冒険者の記録を");
                AssertScreenContains(harness.Root, "読み込めません");
                var retry = FindButton(harness.Root, "もう一度試す");
                Assert.GreaterOrEqual(EffectivePixelHeight(harness.Root, retry.GetComponent<RectTransform>()), 44f);
                AssertRectContains(harness.Root, gate, "Connection recovery must remain inside the viewport.");
                Assert.IsEmpty(UiLayoutQa.Scan(harness.Root), $"Connection gate failed at {width:0}x{height:0}.");
            }
            finally
            {
                harness.Destroy();
            }
        }

        [TestCase(844f, 390f)]
        [TestCase(390f, 844f)]
        public void WeaponWishResultUsesARewardStageInsteadOfAFlatInventoryList(float width, float height)
        {
            var harness = CreateHarness(new Vector2(width, height));
            try
            {
                SetField(harness.App, "currentUser", harness.Member);
                Invoke(harness.App, "EnsureEditorCosmeticPreview");
                Invoke(harness.App, "ApplyEditorCosmeticGacha");
                Invoke(harness.App, "ShowWeaponWish");

                Assert.IsNotNull(FindRect(harness.Root, "WishResultCard"));
                Assert.IsNotNull(FindRect(harness.Root, "WishRewardVisual"));
                AssertScreenContains(harness.Root, "STAR ECHO");
                AssertScreenContains(harness.Root, "日輪の剣");
                AssertButtonExists(harness.Root, "RollWeaponWish");
                Assert.IsEmpty(UiLayoutQa.Scan(harness.Root), $"Weapon wish result failed at {width:0}x{height:0}.");
            }
            finally
            {
                harness.Destroy();
            }
        }

        public static IReadOnlyList<string> RunAllPrimaryScreensLayoutQa()
        {
            var failures = new List<string>();
            var resolutions = new[]
            {
                // The published design contract explicitly claims a portrait WebGL
                // composition; audit every production screen there, not only Login.
                new Vector2(390f, 844f),
                new Vector2(844f, 390f),
                new Vector2(960f, 540f),
                new Vector2(1024f, 576f),
                new Vector2(1280f, 720f),
                new Vector2(1366f, 768f),
                new Vector2(1440f, 1024f),
                new Vector2(1600f, 900f),
                new Vector2(1920f, 1080f)
            };

            foreach (var resolution in resolutions)
            {
                var harness = CreateHarness(resolution);
                try
                {
                    SeedLongContent(harness);
                    foreach (var testCase in CreateScreenCases(harness))
                    {
                        SetField(harness.App, "currentUser", testCase.User);
                        SetField(harness.App, "lastBattleMessage", $"{LongJapaneseText} / {testCase.Name}");
                        SetField(harness.App, "lastSessionMessage", $"{LongJapaneseText} / {testCase.Name}");
                        SetField(harness.App, "lastProductMessage", $"{LongJapaneseText} / {testCase.Name}");
                        SetField(harness.App, "lastAchievementMessage", $"{LongJapaneseText} / {testCase.Name}");
                        SetField(harness.App, "lastMentorMessage", $"{LongJapaneseText} / {testCase.Name}");

                        Invoke(harness.App, testCase.Method);
                        RecordLayoutIssues(resolution, testCase.Name, harness.Root, failures);
                    }

                    SetField(harness.App, "currentUser", null);
                    Invoke(harness.App, "ShowSupabaseSyncingScene");
                    RecordLayoutIssues(resolution, "StartupLoading", harness.Root, failures);
                    InvokeWithArgs(harness.App, "ShowSupabaseConnectionGate", LongJapaneseText, LongAuthenticationError);
                    RecordLayoutIssues(resolution, "ConnectionGate", harness.Root, failures);

                    SetField(harness.App, "currentUser", harness.Member);
                    SetField(harness.App, "battleCommandDeckExpanded", true);
                    harness.Repository.ActiveBattle.Status = BattleStatus.Active;
                    harness.Repository.ActiveBattle.Phase = BattlePhase.ActionSelect;
                    Invoke(harness.App, "ShowBattle");
                    RecordLayoutIssues(resolution, "BattleExpandedCommands", harness.Root, failures);

                    SetField(harness.App, "battleCommandDeckExpanded", false);
                    harness.Repository.ActiveBattle.Status = BattleStatus.Scheduled;
                    harness.Repository.ActiveBattle.Phase = BattlePhase.TurnStart;
                    Invoke(harness.App, "ShowBattle");
                    RecordLayoutIssues(resolution, "BattleScheduled", harness.Root, failures);

                    harness.Repository.ActiveBattle.Status = BattleStatus.Completed;
                    harness.Repository.ActiveBattle.Phase = BattlePhase.Completed;
                    harness.Repository.ActiveBattle.Outcome = BattleOutcome.Victory;
                    harness.Repository.ActiveBattle.Boss.CurrentHp = 0;
                    Invoke(harness.App, "ShowBattle");
                    RecordLayoutIssues(resolution, "BattleResult", harness.Root, failures);
                }
                finally
                {
                    harness.Destroy();
                }
            }

            return failures;
        }

        private static void RecordLayoutIssues(Vector2 resolution, string name, RectTransform root, ICollection<string> failures)
        {
            var issues = UiLayoutQa.Scan(root);
            if (issues.Count > 0)
            {
                failures.Add($"{resolution.x:0}x{resolution.y:0} {name}: {string.Join(" | ", issues.Take(6))}");
            }
        }

        public static IReadOnlyList<string> RunNonBattleInteractionQa()
        {
            var failures = new List<string>();
            RunQaCase("MemberHome navigation", () => new RaidGameAppUiLayoutEditModeTests().MemberHomeNavigationButtonsOpenFocusedScreens(), failures);
            RunQaCase("Mentor navigation", () => new RaidGameAppUiLayoutEditModeTests().MentorNavigationButtonsOpenManagementScreens(), failures);
            RunQaCase("Non-battle input fields", () => new RaidGameAppUiLayoutEditModeTests().NonBattleInputFieldsAreInteractiveAndLimited(), failures);
            return failures;
        }

        private static void RunQaCase(string name, Action action, ICollection<string> failures)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failures.Add($"{name}: {exception.Message}");
            }
        }

        private static IReadOnlyList<(string Name, UserProfile User, string Method)> CreateScreenCases(Harness harness)
        {
            return new (string Name, UserProfile User, string Method)[]
            {
                ("Login", null, "ShowLogin"),
                ("InitialPasswordChange", harness.Member, "ShowInitialPasswordChange"),
                ("MemberHome", harness.Member, "ShowMemberHome"),
                ("DevLog", harness.Member, "ShowDevLog"),
                ("MemberProducts", harness.Member, "ShowProducts"),
                ("MemberAchievements", harness.Member, "ShowAchievements"),
                ("MemberTeam", harness.Member, "ShowMemberTeam"),
                ("CosmeticWardrobe", harness.Member, "ShowCosmeticWardrobe"),
                ("WeaponWish", harness.Member, "ShowWeaponWish"),
                ("MemberHistory", harness.Member, "ShowMemberHistory"),
                ("Battle", harness.Member, "ShowBattle"),
                ("FrontDisplay", null, "ShowFrontScreen"),
                ("MemberSettings", harness.Member, "ShowSettings"),
                ("Ranking", harness.Member, "ShowRanking"),
                ("MentorDashboard", harness.Mentor, "ShowMentorDashboard"),
                ("MentorOperations", harness.Mentor, "ShowMentorOperations"),
                ("MentorReviewQueue", harness.Mentor, "ShowMentorReviewQueue"),
                ("MentorTeamStatus", harness.Mentor, "ShowMentorTeamStatus"),
                ("MentorAccounts", harness.Mentor, "ShowMentorAccounts"),
                ("MentorAccountList", harness.Mentor, "ShowMentorAccountList"),
                ("MentorProducts", harness.Mentor, "ShowProducts"),
                ("MentorAchievements", harness.Mentor, "ShowAchievements")
            };
        }

        private static void SeedLongContent(Harness harness)
        {
            harness.Member.Nickname = "ものすごく長い表示名のハッカーくん";
            harness.Mentor.Nickname = "ものすごく長い表示名のメンターさん";
            harness.Repository.StartBattle(harness.Mentor.Id);
            harness.Repository.SubmitBattleAction(harness.Member.Id, BattleRole.Attacker, WeaponKind.Blade, BattleActionType.Strong);

            var session = harness.Repository.StartSession(harness.Member.Id, LongJapaneseText);
            harness.Repository.CompleteSession(harness.Member.Id, 75, $"{LongJapaneseText}。学びと気づきを長めに書く。", $"{LongJapaneseText}。次の作業も長めに書く。");
            Assert.IsNotNull(session);
            harness.Repository.StartSession(harness.Member.Id, $"{LongJapaneseText}。現在進行中のセッション目標。");

            harness.Repository.RegisterProduct(
                harness.Member.Id,
                "とても長いプロダクト名でもカードから文字が飛び出さない検証用タイトル",
                "https://example.com/projects/attack-on-rasshiine/ui-layout-quality-assurance-long-url-case",
                $"{LongJapaneseText}。プロダクト紹介文が長くても枠内で省略される。");

            harness.Repository.SubmitAchievement(
                harness.Member.Id,
                AchievementType.Release,
                "とても長い実績タイトルでも表示が崩れない検証用の申請名",
                $"{LongJapaneseText}。実績説明も長めに入れる。");
        }

        private static void SeedAccountsUntil(Harness harness, int totalUserCount)
        {
            var seedIndex = 0;
            while (harness.Repository.Users.Count < totalUserCount)
            {
                harness.Repository.CreateUserAccount(
                    harness.Mentor.Id,
                    $"page-user-{seedIndex:00}",
                    $"ページ確認メンバー {seedIndex + 1}",
                    UserRole.Member,
                    seedIndex % 2 == 0 ? "blue" : "magenta",
                    true);
                seedIndex += 1;
            }
        }

        private static IReadOnlyList<RectTransform> FindMemberAccountRows(RectTransform root)
        {
            return root.GetComponentsInChildren<RectTransform>(true)
                .Where(rect => rect.name.StartsWith("MemberAccount_", StringComparison.Ordinal))
                .ToList();
        }

        private static void SetInputDraft(RectTransform root, string inputName, string value)
        {
            var input = FindInput(root, inputName);
            input.SetTextWithoutNotify(value);
            input.onValueChanged.Invoke(value);
        }

        private static void AssertMentorAccountDrafts(RectTransform root, string loginId, string nickname, string teamId)
        {
            Assert.AreEqual(loginId, FindInput(root, "MemberLoginIdInput").text, "Login ID must survive selector-driven re-rendering.");
            Assert.AreEqual(nickname, FindInput(root, "MemberNicknameInput").text, "Nickname must survive selector-driven re-rendering.");
            Assert.AreEqual(teamId, FindInput(root, "MemberTeamIdInput").text, "Team ID must survive selector-driven re-rendering.");
        }

        private static Harness CreateHarness(Vector2 resolution)
        {
            var host = new GameObject("RaidGameAppUiQaHarness");
            var app = host.AddComponent<RaidGameApp>();
            var theme = new GameObject("UiQaTheme").AddComponent<RasshiineTheme>();
            theme.transform.SetParent(host.transform, false);
            theme.UseHeatUiSkin = false;
            theme.UseOption3LiveUi = true;
            theme.LoginPanelFrame = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.LoginPanelFramePath);
            theme.LoginInputFrame = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.LoginInputFramePath);
            theme.LoginStatusFrame = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.LoginStatusFramePath);
            theme.LoginDivider = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.LoginDividerPath);
            theme.LoginCtaButton = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.LoginCtaButtonPath);
            theme.LoginSunRays = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.LoginSunRaysPath);
            var repository = new LocalGameRepository();
            var ui = new NeonUiFactory(theme);
            var canvas = ui.CreateCanvas("UiQaCanvas");
            canvas.transform.SetParent(host.transform, false);
            var canvasRect = canvas.GetComponent<RectTransform>();
            var canvasScale = CanvasScaleFor(resolution);
            canvas.scaleFactor = canvasScale;
            var canvasScaler = canvas.GetComponent<CanvasScaler>();
            if (canvasScaler != null)
            {
                canvasScaler.enabled = false;
            }
            canvasRect.sizeDelta = resolution / canvasScale;

            var root = new GameObject("ScreenRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(canvas.transform, false);
            ui.Stretch(root, 0, 0, 0, 0);

            SetField(app, "theme", theme);
            SetField(app, "repository", repository);
            SetField(app, "supabase", new SupabaseGameClient());
            SetField(app, "devLogPresenter", new DevLogPresenter());
            SetField(app, "ui", ui);
            SetField(app, "root", root);

            return new Harness(host, app, root, repository);
        }

        private static float CanvasScaleFor(Vector2 resolution)
        {
            var widthScale = Mathf.Max(0.01f, resolution.x / 1440f);
            var heightScale = Mathf.Max(0.01f, resolution.y / 1024f);
            return Mathf.Sqrt(widthScale * heightScale);
        }

        private static void Invoke(RaidGameApp app, string methodName)
        {
            var method = typeof(RaidGameApp).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            method.Invoke(app, null);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)GetField(app, "root"));
        }

        private static object InvokeWithArgs(RaidGameApp app, string methodName, params object[] arguments)
        {
            var method = typeof(RaidGameApp).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            var result = method.Invoke(app, arguments);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)GetField(app, "root"));
            return result;
        }

        private static void InvokeButton(RectTransform root, string name)
        {
            var button = root.GetComponentsInChildren<Button>(true).FirstOrDefault(item => item.name == name);
            Assert.IsNotNull(button, name);
            Assert.IsTrue(button.interactable, $"{name} should be interactable.");
            button.onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        }

        private static void AssertScreenContains(RectTransform root, string expectedText)
        {
            Assert.IsTrue(
                root.GetComponentsInChildren<Text>(true).Any(text => text.text.Contains(expectedText, StringComparison.Ordinal)),
                $"Screen should contain {expectedText}.");
        }

        private static void AssertScreenDoesNotContain(RectTransform root, string unexpectedText)
        {
            Assert.IsFalse(
                root.GetComponentsInChildren<Text>(true).Any(text => text.text.Contains(unexpectedText, StringComparison.Ordinal)),
                $"Screen should not contain {unexpectedText}.");
        }

        private static void AssertButtonExists(RectTransform root, string name)
        {
            var button = root.GetComponentsInChildren<Button>(true).FirstOrDefault(item => item.name == name);
            Assert.IsNotNull(button, name);
            Assert.IsTrue(button.interactable, $"{name} should be interactable.");
        }

        private static void AssertResponsiveFrontControls(RectTransform root, float width, float height)
        {
            var phaseBanner = FindRect(root, "FrontPhaseBanner");
            var development = FindButton(root, "ログインして記録");
            var battle = FindButton(root, "ボス戦を見る");
            AssertRectContains(root, phaseBanner, "The public-board phase banner must remain inside the viewport.");
            Assert.GreaterOrEqual(EffectivePixelHeight(root, development.GetComponent<RectTransform>()), 44f);
            Assert.GreaterOrEqual(EffectivePixelHeight(root, battle.GetComponent<RectTransform>()), 44f);
            Assert.GreaterOrEqual(EffectiveButtonLabelSize(root, development), 12.5f);
            Assert.GreaterOrEqual(EffectiveButtonLabelSize(root, battle), 12.5f);
            Assert.IsEmpty(UiLayoutQa.Scan(root), $"Responsive front display failed at {width:0}x{height:0}.");
        }

        private static void AssertNoEllipsisOnlyLabels(RectTransform parent)
        {
            var meaningless = parent.GetComponentsInChildren<Text>(true)
                .Where(text => string.Equals(text.text?.Trim(), "…", StringComparison.Ordinal) ||
                               string.Equals(text.text?.Trim(), "...", StringComparison.Ordinal))
                .Select(text => text.name)
                .ToArray();
            Assert.IsEmpty(meaningless, $"{parent.name} must not reduce a heading or metric to an ellipsis-only placeholder.");
        }

        private static InputField FindInput(RectTransform root, string name)
        {
            var input = root.GetComponentsInChildren<InputField>(true).FirstOrDefault(item => item.name == name);
            Assert.IsNotNull(input, name);
            return input;
        }

        private static Button FindButton(RectTransform root, string name)
        {
            var button = root.GetComponentsInChildren<Button>(true).FirstOrDefault(item => item.name == name);
            Assert.IsNotNull(button, name);
            return button;
        }

        private static Text FindText(RectTransform root, string name)
        {
            var text = root.GetComponentsInChildren<Text>(true).FirstOrDefault(item => item.name == name);
            Assert.IsNotNull(text, name);
            return text;
        }

        private static RectTransform FindRect(RectTransform root, string name)
        {
            var rect = root.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(item => item.name == name);
            Assert.IsNotNull(rect, name);
            return rect;
        }

        private static void AssertLoginSurface(
            RectTransform root,
            string name,
            string fillName,
            string dedicatedFrameSlotName,
            float minimumSurfaceAlpha)
        {
            var panel = FindRect(root, name);
            var fill = panel.Find(fillName)?.GetComponent<Image>();
            var dedicatedFrameSlot = panel.Find(dedicatedFrameSlotName)?.GetComponent<Image>();
            var frame = panel.GetComponent<Image>();

            Assert.IsNotNull(fill, $"{name} must use a dedicated navy fill.");
            Assert.IsNotNull(frame, $"{name} must keep a restrained temporary gold frame.");
            Assert.IsNotNull(dedicatedFrameSlot, $"{name} must expose a generated 9-slice frame slot.");
            Assert.Greater(fill.color.b, fill.color.g, $"{name} surface should read as navy rather than brown.");
            Assert.Greater(fill.color.g, fill.color.r, $"{name} surface should read as navy rather than brown.");
            Assert.GreaterOrEqual(fill.color.a, minimumSurfaceAlpha, $"{name} must preserve text contrast over the variable 3D background.");
            Assert.AreEqual(Color.clear, frame.color, $"{name} temporary rectangular frame must disappear once the generated frame is assigned.");
            Assert.IsNotNull(dedicatedFrameSlot.sprite, $"{name} generated frame must be assigned.");
            Assert.AreEqual(Color.white, dedicatedFrameSlot.color, $"{name} generated frame must render without tint distortion.");
        }

        private static void AssertIntegratedLoginPanel(RectTransform root, RasshiineTheme theme)
        {
            // The login panel intentionally moved off the ornate double-line/
            // scalloped-corner/diamond-finial sprite frame onto a flat-color rectangle,
            // so it reads as low-poly-consistent geometry instead of "gacha game" chrome.
            var panel = FindRect(root, "LoginPanel");
            var frame = panel.GetComponent<Image>();
            var fill = panel.Find("LoginPanelFill")?.GetComponent<Image>();
            var dedicatedFrameSlot = panel.Find("LoginPanelDedicatedFrameSlot")?.GetComponent<Image>();

            Assert.IsNotNull(frame);
            Assert.IsNull(frame.sprite, "The login panel must be a flat rectangle, not the ornate generated frame art.");
            Assert.AreEqual(Image.Type.Simple, frame.type);
            Assert.Greater(frame.color.a, 0f, "The flat frame color must be visible as a thin border.");
            Assert.IsNotNull(fill);
            Assert.Greater(fill.color.a, 0f, "The flat navy fill must be visible.");
            Assert.IsNotNull(dedicatedFrameSlot, "Keep the named hook for future frame-only variants.");
            Assert.IsNull(dedicatedFrameSlot.sprite, "No ornate frame art should be assigned to the flat login panel.");
        }

        private static bool HasSubmitTrigger(InputField input)
        {
            var trigger = input != null ? input.GetComponent<EventTrigger>() : null;
            return trigger != null && trigger.triggers != null && trigger.triggers.Any(entry => entry.eventID == EventTriggerType.Submit);
        }

        private static float EffectivePixelFontSize(Text text)
        {
            var canvas = text != null ? text.GetComponentInParent<Canvas>() : null;
            var scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            return text != null ? text.fontSize * scale : 0f;
        }

        private static float EffectiveButtonLabelSize(RectTransform root, Button button)
        {
            if (button == null)
            {
                return 0f;
            }

            var liveText = button.GetComponentInChildren<Text>(true);
            if (liveText != null)
            {
                return EffectivePixelFontSize(liveText);
            }

            var baked = button.GetComponentInChildren<BakedTextButtonImage>(true);
            if (baked == null)
            {
                return 0f;
            }

            var fontSizeField = typeof(BakedTextButtonImage).GetField("fontSize", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(fontSizeField);
            return (int)fontSizeField.GetValue(baked) * EffectiveCanvasScale(root);
        }

        private static void AssertBattleMetricValuesReadable(RectTransform root, RectTransform statePanel, float width, float height)
        {
            var metrics = statePanel.GetComponentsInChildren<RectTransform>(true)
                .Where(rect => rect.name.StartsWith("BattleMetric_", StringComparison.Ordinal))
                .ToArray();
            Assert.AreEqual(6, metrics.Length, $"The expanded battle state should expose six populated metrics at {width:0}x{height:0}.");
            foreach (var metric in metrics)
            {
                var texts = metric.GetComponentsInChildren<Text>(true);
                Assert.GreaterOrEqual(texts.Length, 2, $"{metric.name} needs both label and value.");
                Assert.IsTrue(texts.All(text => !string.IsNullOrWhiteSpace(text.text)), $"{metric.name} must not render as an empty frame.");
                Assert.GreaterOrEqual(
                    texts.Max(EffectivePixelFontSize),
                    14f,
                    $"{metric.name} value must remain readable at {width:0}x{height:0}.");
            }
        }

        private static float EffectivePixelHeight(RectTransform root, RectTransform target)
        {
            return RelativeRect(root, target).height * EffectiveCanvasScale(root);
        }

        private static float EffectiveCanvasScale(RectTransform root)
        {
            var canvas = root != null ? root.GetComponentInParent<Canvas>() : null;
            return canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        }

        private static Vector4 EffectiveSlicedBorders(Image image)
        {
            Assert.IsNotNull(image);
            Assert.IsNotNull(image.sprite);
            var multipliedPixelsPerUnit = Mathf.Max(0.01f, image.pixelsPerUnit * image.pixelsPerUnitMultiplier);
            var borders = image.sprite.border / multipliedPixelsPerUnit;
            var size = image.rectTransform.rect.size;

            var horizontalBorderTotal = borders.x + borders.z;
            if (horizontalBorderTotal > 0f && size.x < horizontalBorderTotal)
            {
                var horizontalScale = size.x / horizontalBorderTotal;
                borders.x *= horizontalScale;
                borders.z *= horizontalScale;
            }

            var verticalBorderTotal = borders.y + borders.w;
            if (verticalBorderTotal > 0f && size.y < verticalBorderTotal)
            {
                var verticalScale = size.y / verticalBorderTotal;
                borders.y *= verticalScale;
                borders.w *= verticalScale;
            }

            return borders;
        }

        private static void AssertRectContains(RectTransform parent, RectTransform child, string message)
        {
            var childRect = RelativeRect(parent, child);
            var parentRect = parent.rect;
            const float tolerance = 0.5f;
            Assert.GreaterOrEqual(childRect.xMin, parentRect.xMin - tolerance, message);
            Assert.LessOrEqual(childRect.xMax, parentRect.xMax + tolerance, message);
            Assert.GreaterOrEqual(childRect.yMin, parentRect.yMin - tolerance, message);
            Assert.LessOrEqual(childRect.yMax, parentRect.yMax + tolerance, message);
        }

        private static void AssertRectsDoNotOverlap(RectTransform reference, RectTransform first, RectTransform second, string message)
        {
            Assert.IsFalse(RelativeRect(reference, first).Overlaps(RelativeRect(reference, second)), message);
        }

        private static Rect RelativeRect(RectTransform reference, RectTransform target)
        {
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(reference, target);
            return Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private static object GetField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            return field.GetValue(target);
        }

        private sealed class Harness
        {
            public Harness(GameObject host, RaidGameApp app, RectTransform root, LocalGameRepository repository)
            {
                Host = host;
                App = app;
                Root = root;
                Repository = repository;
                Member = repository.Members[0];
                Mentor = repository.Mentors[0];
            }

            public GameObject Host { get; }
            public RaidGameApp App { get; }
            public RectTransform Root { get; }
            public LocalGameRepository Repository { get; }
            public UserProfile Member { get; }
            public UserProfile Mentor { get; }

            public void Destroy()
            {
                UnityEngine.Object.DestroyImmediate(Host);
            }
        }
    }
}
