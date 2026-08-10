using System;
using System.Linq;
using System.Reflection;
using AttackOnRasshiine.Runtime.Services;
using AttackOnRasshiine.Runtime.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AttackOnRasshiine.Editor
{
    public sealed class WaypointTerraceUiEditModeTests
    {
        [Test]
        public void CanvasUsesApprovedReferenceResolution()
        {
            var themeObject = new GameObject("WaypointCanvasTheme");
            var canvas = new NeonUiFactory(themeObject.AddComponent<RasshiineTheme>()).CreateCanvas("WaypointCanvas");
            try
            {
                var scaler = canvas.GetComponent<CanvasScaler>();
                Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
                Assert.AreEqual(new Vector2(1440f, 1024f), scaler.referenceResolution);
                Assert.AreEqual(0.5f, scaler.matchWidthOrHeight, 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                UnityEngine.Object.DestroyImmediate(themeObject);
            }
        }

        [Test]
        public void GeneratedWaypointAssetsAndFontsAreImportedForUi()
        {
            AssertFont(RasshiineTheme.UiFontPath);
            AssertFont(RasshiineTheme.UiTitleFontPath);
            AssertFont(RasshiineTheme.UiDisplayFontPath);

            AssertSprite(RasshiineTheme.WaypointCompassPath, false);
            AssertSprite(RasshiineTheme.WaypointCrestPath, false);
            AssertSprite(RasshiineTheme.WaypointNavRingPath, false);
            AssertSprite(RasshiineTheme.WaypointNextRaidFramePath, true);
            AssertSprite(RasshiineTheme.WaypointParchmentPath, false);
            AssertSprite(RasshiineTheme.WaypointPlayerStatusFramePath, true);
        }

        [Test]
        public void MemberHomeSceneSerializesWaypointThemeAssignments()
        {
            const string path = "Assets/Scenes/RasshiineMemberHome.unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var theme = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<RasshiineTheme>(true))
                    .Single();
                Assert.IsTrue(theme.UseHeatUiSkin);
                Assert.AreSame(AssetDatabase.LoadAssetAtPath<Font>(RasshiineTheme.UiFontPath), theme.UiFont);
                Assert.AreSame(AssetDatabase.LoadAssetAtPath<Font>(RasshiineTheme.UiTitleFontPath), theme.UiTitleFont);
                Assert.AreSame(AssetDatabase.LoadAssetAtPath<Font>(RasshiineTheme.UiDisplayFontPath), theme.UiDisplayFont);
                Assert.AreSame(AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.WaypointCompassPath), theme.WaypointCompass);
                Assert.AreSame(AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.WaypointCrestPath), theme.WaypointCrest);
                Assert.AreSame(AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.WaypointNavRingPath), theme.WaypointNavRing);
                Assert.AreSame(AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.WaypointNextRaidFramePath), theme.WaypointNextRaidFrame);
                Assert.AreSame(AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.WaypointParchmentPath), theme.WaypointParchment);
                Assert.AreSame(AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.WaypointPlayerStatusFramePath), theme.WaypointPlayerStatusFrame);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void MemberHomeMatchesReferenceAnchorsAndUsesGeneratedAssets()
        {
            using var harness = CreateHarness();
            Invoke(harness.App, "ShowMemberHome");

            AssertReferenceRect(harness.Root, "PlayerStatusZone", 1015f, 36f, 395f, 169f);
            AssertLocalReferenceRect(harness.Root, "PlayerStatusFrame", 0f, -43f, 395f, 188f, 395f, 169f);
            AssertReferenceRect(harness.Root, "WorldGlyph", 48f, 260f, 18f, 18f);
            AssertReferenceRect(harness.Root, "WorldLabel", 72f, 254f, 296f, 30f);
            AssertReferenceRect(harness.Root, "PrimaryDevelopmentCompass", 574f, 732f, 292f, 292f);
            AssertReferenceRect(harness.Root, "NextRaidRibbon", 965f, 846f, 445f, 136f);
            AssertReferenceRect(harness.Root, "HomeWelcomeToast", 32f, 954f, 328f, 50f);

            Assert.AreSame(harness.Theme.WaypointPlayerStatusFrame, FindRect(harness.Root, "PlayerStatusFrame").GetComponent<Image>().sprite);
            Assert.AreSame(harness.Theme.GoalIcon, FindRect(harness.Root, "WorldGlyph").GetComponent<Image>().sprite);
            Assert.AreSame(harness.Theme.WaypointCompass, FindRect(harness.Root, "PrimaryDevelopmentCompass").GetComponent<Image>().sprite);
            Assert.AreSame(harness.Theme.WaypointNextRaidFrame, FindRect(harness.Root, "NextRaidRibbon").GetComponent<Image>().sprite);
            Assert.AreSame(harness.Theme.UiDisplayFont, FindRect(harness.Root, "PrimaryDevelopmentLabel").GetComponent<Text>().font);

            Assert.IsNull(FindOptionalRect(harness.Root, "HomeContent"));
            Assert.IsNull(FindOptionalRect(harness.Root, "StatsPanel"));
            Assert.IsNull(FindOptionalRect(harness.Root, "WeeklyPanel"));
            Assert.IsNull(FindOptionalRect(harness.Root, "MemberHomeActions"));
            Assert.IsNull(FindOptionalRect(harness.Root, "MemberHomeDetails"));
        }

        [Test]
        public void HomeStatusAndRaidCopyStayInsideMaskedArtworkSafeAreas()
        {
            using var harness = CreateHarness();
            harness.Repository.Members[0].Nickname = "とても長い表示名のハッカーくん";

            Invoke(harness.App, "ShowMemberHome");
            AssertCriticalHomeContainment(harness.Root);

            harness.Repository.StartBattle(harness.Repository.Mentors[0].Id);
            Invoke(harness.App, "ShowMemberHome");
            AssertCriticalHomeContainment(harness.Root);
            Assert.AreEqual("開催中  ・  参加する", FindRect(harness.Root, "NextRaidDate").GetComponent<Text>().text);
        }

        [Test]
        public void SecondaryMemberWorkflowUsesOneFocusedPanel()
        {
            using var harness = CreateHarness();
            Invoke(harness.App, "ShowDevLog");

            var focusedPanel = FindRect(harness.Root, "DevLogScroll");
            Assert.AreEqual(new Vector2(0.43f, 0.055f), focusedPanel.anchorMin);
            Assert.AreEqual(new Vector2(0.96f, 0.815f), focusedPanel.anchorMax);
            Assert.IsNotNull(FindRect(harness.Root, "SceneDimmer"));
            Assert.AreEqual(1, harness.Root.GetComponentsInChildren<ScrollRect>(true).Length);
        }

        [Test]
        public void TeamScreenUsesCompactWaypointHeaderAndReadableType()
        {
            using var harness = CreateHarness();
            Invoke(harness.App, "ShowMemberTeam");

            var back = FindRect(harness.Root, "BackButton");
            var settings = FindRect(harness.Root, "SettingsButton");
            Assert.AreSame(harness.Theme.WaypointNavRing, back.GetComponent<Image>().sprite);
            Assert.AreSame(harness.Theme.WaypointNavRing, settings.GetComponent<Image>().sprite);
            Assert.GreaterOrEqual(back.GetComponent<LayoutElement>().preferredWidth, 64f);
            Assert.GreaterOrEqual(settings.GetComponent<LayoutElement>().preferredWidth, 64f);
            Assert.GreaterOrEqual(EffectivePixelWidth(harness.Root, back), 47.9f);
            Assert.GreaterOrEqual(EffectivePixelWidth(harness.Root, settings), 47.9f);

            var dimmer = FindRect(harness.Root, "SceneDimmer").GetComponent<Image>();
            Assert.GreaterOrEqual(dimmer.color.a, 0.5f);

            var memberRows = harness.Root.GetComponentsInChildren<RectTransform>(true)
                .Where(rect => rect.name.StartsWith("TeamMember_"))
                .ToArray();
            Assert.IsNotEmpty(memberRows);
            foreach (var text in memberRows.SelectMany(row => row.GetComponentsInChildren<Text>(true)))
            {
                Assert.GreaterOrEqual(text.fontSize, 17, $"{text.name} should remain readable over the scene.");
            }
        }

        [Test]
        public void TeamRoutesToFocusedWardrobeAndWeaponWishScreens()
        {
            using var harness = CreateHarness();
            Invoke(harness.App, "ShowMemberTeam");
            Assert.IsNotNull(FindRect(harness.Root, "OpenCosmeticWardrobe").GetComponent<Button>());
            Assert.IsNotNull(FindRect(harness.Root, "OpenWeaponWish").GetComponent<Button>());

            Invoke(harness.App, "ShowCosmeticWardrobe");
            var wardrobe = FindRect(harness.Root, "CosmeticWardrobePanel");
            Assert.AreEqual(new Vector2(0.43f, 0.06f), wardrobe.anchorMin);
            Assert.AreEqual(new Vector2(0.96f, 0.82f), wardrobe.anchorMax);
            Assert.IsNotNull(FindRect(harness.Root, "TinyHeroRender"));
            Assert.AreEqual(1, harness.Root.GetComponentsInChildren<ScrollRect>(true).Length);

            Invoke(harness.App, "ShowWeaponWish");
            var wish = FindRect(harness.Root, "WeaponWishPanel");
            Assert.AreEqual(new Vector2(0.43f, 0.06f), wish.anchorMin);
            Assert.AreEqual(new Vector2(0.96f, 0.82f), wish.anchorMax);
            Assert.IsNotNull(FindRect(harness.Root, "WishCreditCard"));
            Assert.IsNotNull(FindRect(harness.Root, "RollWeaponWish"));
            Assert.AreEqual(1, harness.Root.GetComponentsInChildren<ScrollRect>(true).Length);
        }

        [Test]
        public void UserFacingScreensHideInfrastructureAndDeveloperCopy()
        {
            using var harness = CreateHarness();
            foreach (var method in new[] { "ShowMemberHome", "ShowSettings", "ShowSupabaseSyncingScene" })
            {
                Invoke(harness.App, method);
                var copy = string.Join("\n", harness.Root.GetComponentsInChildren<Text>(true).Select(text => text.text));
                foreach (var forbidden in new[] { "Supabase", "同期", "オンライン", "オフライン", "開発者", "デモモード" })
                {
                    StringAssert.DoesNotContain(forbidden, copy, $"{method} must not expose {forbidden}.");
                }

                Assert.IsNull(FindOptionalRect(harness.Root, "DeveloperNavigation"));
            }
        }

        private static Harness CreateHarness()
        {
            var host = new GameObject("WaypointUiHarness");
            var app = host.AddComponent<RaidGameApp>();
            var theme = new GameObject("WaypointUiTheme").AddComponent<RasshiineTheme>();
            theme.transform.SetParent(host.transform, false);
            theme.UseHeatUiSkin = false;
            AssignThemeAssets(theme);

            var repository = new LocalGameRepository();
            var ui = new NeonUiFactory(theme);
            var canvas = ui.CreateCanvas("WaypointUiCanvas");
            canvas.transform.SetParent(host.transform, false);
            var root = new GameObject("ScreenRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(canvas.transform, false);
            ui.Stretch(root, 0f, 0f, 0f, 0f);

            SetField(app, "theme", theme);
            SetField(app, "repository", repository);
            SetField(app, "supabase", new SupabaseGameClient());
            SetField(app, "devLogPresenter", new DevLogPresenter());
            SetField(app, "ui", ui);
            SetField(app, "root", root);
            SetField(app, "currentUser", repository.Members[0]);
            return new Harness(host, app, root, theme, repository);
        }

        private static void AssignThemeAssets(RasshiineTheme theme)
        {
            theme.UiFont = AssetDatabase.LoadAssetAtPath<Font>(RasshiineTheme.UiFontPath);
            theme.UiTitleFont = AssetDatabase.LoadAssetAtPath<Font>(RasshiineTheme.UiTitleFontPath);
            theme.UiDisplayFont = AssetDatabase.LoadAssetAtPath<Font>(RasshiineTheme.UiDisplayFontPath);
            theme.WaypointCompass = AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.WaypointCompassPath);
            theme.WaypointCrest = AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.WaypointCrestPath);
            theme.WaypointNavRing = AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.WaypointNavRingPath);
            theme.WaypointNextRaidFrame = AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.WaypointNextRaidFramePath);
            theme.WaypointParchment = AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.WaypointParchmentPath);
            theme.WaypointPlayerStatusFrame = AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.WaypointPlayerStatusFramePath);
            theme.HomeIcon = AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.HeatMiscIconDir + "/Home (64x).png");
            theme.TeamIcon = AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.HeatMiscIconDir + "/Multiplayer (64x).png");
            theme.GoalIcon = AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.HeatMiscIconDir + "/Achievements (64x).png");
            theme.RecordIcon = AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.HeatMiscIconDir + "/Chapters (64x).png");
            theme.SettingsIcon = AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.HeatMiscIconDir + "/Settings (64x).png");
            theme.BattleIcon = AssetDatabase.LoadAssetAtPath<Sprite>(RasshiineTheme.HeatMiscIconDir + "/Play Circle (64x).png");
        }

        private static void AssertFont(string path)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(path);
            Assert.IsNotNull(font, path);
            Assert.AreEqual(path, AssetDatabase.GetAssetPath(font));
        }

        private static void AssertSprite(string path, bool expectsBorder)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.IsNotNull(sprite, path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.IsNotNull(importer, path);
            Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, path);
            Assert.IsFalse(importer.mipmapEnabled, path);
            Assert.AreEqual(expectsBorder, importer.spriteBorder.sqrMagnitude > 0f, path);
        }

        private static void AssertReferenceRect(RectTransform root, string name, float x, float y, float width, float height)
        {
            var rect = FindRect(root, name);
            var expectedMin = new Vector2(x / 1440f, 1f - (y + height) / 1024f);
            var expectedMax = new Vector2((x + width) / 1440f, 1f - y / 1024f);
            Assert.AreEqual(expectedMin.x, rect.anchorMin.x, 0.0001f, $"{name} anchorMin.x");
            Assert.AreEqual(expectedMin.y, rect.anchorMin.y, 0.0001f, $"{name} anchorMin.y");
            Assert.AreEqual(expectedMax.x, rect.anchorMax.x, 0.0001f, $"{name} anchorMax.x");
            Assert.AreEqual(expectedMax.y, rect.anchorMax.y, 0.0001f, $"{name} anchorMax.y");
            Assert.AreEqual(Vector2.zero, rect.offsetMin, name);
            Assert.AreEqual(Vector2.zero, rect.offsetMax, name);
        }

        private static void AssertLocalReferenceRect(
            RectTransform root,
            string name,
            float x,
            float y,
            float width,
            float height,
            float parentWidth,
            float parentHeight)
        {
            var rect = FindRect(root, name);
            var expectedMin = new Vector2(x / parentWidth, 1f - (y + height) / parentHeight);
            var expectedMax = new Vector2((x + width) / parentWidth, 1f - y / parentHeight);
            Assert.AreEqual(expectedMin.x, rect.anchorMin.x, 0.0001f, $"{name} anchorMin.x");
            Assert.AreEqual(expectedMin.y, rect.anchorMin.y, 0.0001f, $"{name} anchorMin.y");
            Assert.AreEqual(expectedMax.x, rect.anchorMax.x, 0.0001f, $"{name} anchorMax.x");
            Assert.AreEqual(expectedMax.y, rect.anchorMax.y, 0.0001f, $"{name} anchorMax.y");
            Assert.AreEqual(Vector2.zero, rect.offsetMin, name);
            Assert.AreEqual(Vector2.zero, rect.offsetMax, name);
        }

        private static void AssertCriticalHomeContainment(RectTransform root)
        {
            var statusZone = FindRect(root, "PlayerStatusZone");
            var statusSafeArea = FindRect(root, "PlayerStatusTextSafeArea");
            var stateSafeArea = FindRect(root, "PlayerStateSafeArea");
            var ribbon = FindRect(root, "NextRaidRibbon");
            var raidSafeArea = FindRect(root, "NextRaidTextSafeArea");

            Assert.AreSame(statusZone, statusSafeArea.parent);
            Assert.AreSame(statusZone, stateSafeArea.parent);
            Assert.AreSame(ribbon, raidSafeArea.parent);
            Assert.IsNotNull(statusSafeArea.GetComponent<RectMask2D>());
            Assert.IsNotNull(stateSafeArea.GetComponent<RectMask2D>());
            Assert.IsNotNull(raidSafeArea.GetComponent<RectMask2D>());
            AssertRectAnchorsInsideParent(statusSafeArea);
            AssertRectAnchorsInsideParent(stateSafeArea);
            AssertRectAnchorsInsideParent(raidSafeArea);

            var nickname = FindRect(root, "PlayerNickname");
            var level = FindRect(root, "PlayerLevel");
            var exp = FindRect(root, "PlayerExpLabel");
            var state = FindRect(root, "PlayerTodayState");
            var eyebrow = FindRect(root, "NextRaidEyebrow");
            var raidDate = FindRect(root, "NextRaidDate");
            foreach (var textRect in new[] { nickname, level, exp })
            {
                Assert.AreSame(statusSafeArea, textRect.parent, textRect.name);
                AssertRectAnchorsInsideParent(textRect);
                AssertSingleLineContainmentPolicy(textRect.GetComponent<Text>());
            }

            Assert.AreSame(stateSafeArea, state.parent);
            AssertRectAnchorsInsideParent(state);
            AssertSingleLineContainmentPolicy(state.GetComponent<Text>());
            foreach (var textRect in new[] { eyebrow, raidDate })
            {
                Assert.AreSame(raidSafeArea, textRect.parent, textRect.name);
                AssertRectAnchorsInsideParent(textRect);
                AssertSingleLineContainmentPolicy(textRect.GetComponent<Text>());
            }

            Assert.LessOrEqual(nickname.anchorMax.x, level.anchorMin.x, "Nickname and level must not overlap.");
            Assert.LessOrEqual(raidDate.anchorMax.y, eyebrow.anchorMin.y, "Raid eyebrow and date must not overlap.");

            var visibleCopy = string.Join("\n", root.GetComponentsInChildren<Text>(true).Select(text => text.text));
            StringAssert.DoesNotContain("◇", visibleCopy, "Visible iconography must use a sprite, not a text glyph.");
            Assert.IsEmpty(UiLayoutQa.Scan(root));
        }

        private static void AssertRectAnchorsInsideParent(RectTransform rect)
        {
            Assert.GreaterOrEqual(rect.anchorMin.x, 0f, $"{rect.name} left edge");
            Assert.GreaterOrEqual(rect.anchorMin.y, 0f, $"{rect.name} bottom edge");
            Assert.LessOrEqual(rect.anchorMax.x, 1f, $"{rect.name} right edge");
            Assert.LessOrEqual(rect.anchorMax.y, 1f, $"{rect.name} top edge");
            Assert.LessOrEqual(rect.anchorMin.x, rect.anchorMax.x, $"{rect.name} horizontal order");
            Assert.LessOrEqual(rect.anchorMin.y, rect.anchorMax.y, $"{rect.name} vertical order");
            Assert.AreEqual(Vector2.zero, rect.offsetMin, rect.name);
            Assert.AreEqual(Vector2.zero, rect.offsetMax, rect.name);
        }

        private static void AssertSingleLineContainmentPolicy(Text text)
        {
            Assert.IsNotNull(text);
            Assert.AreEqual(HorizontalWrapMode.Wrap, text.horizontalOverflow, text.name);
            Assert.AreEqual(VerticalWrapMode.Truncate, text.verticalOverflow, text.name);
            Assert.IsTrue(text.resizeTextForBestFit, text.name);
            Assert.GreaterOrEqual(text.resizeTextMinSize, 8, text.name);
            Assert.LessOrEqual(text.resizeTextMinSize, text.resizeTextMaxSize, text.name);
        }

        private static RectTransform FindRect(RectTransform root, string name)
        {
            var rect = FindOptionalRect(root, name);
            Assert.IsNotNull(rect, name);
            return rect;
        }

        private static RectTransform FindOptionalRect(RectTransform root, string name)
        {
            return root.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(rect => rect.name == name);
        }

        private static float EffectivePixelWidth(RectTransform root, RectTransform rect)
        {
            Canvas.ForceUpdateCanvases();
            var canvas = root.GetComponentInParent<Canvas>();
            var scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            return rect.rect.width * scale;
        }

        private static void Invoke(RaidGameApp app, string methodName)
        {
            var method = typeof(RaidGameApp).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            method.Invoke(app, null);
            Canvas.ForceUpdateCanvases();
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private sealed class Harness : IDisposable
        {
            public Harness(GameObject host, RaidGameApp app, RectTransform root, RasshiineTheme theme, LocalGameRepository repository)
            {
                Host = host;
                App = app;
                Root = root;
                Theme = theme;
                Repository = repository;
            }

            public GameObject Host { get; }
            public RaidGameApp App { get; }
            public RectTransform Root { get; }
            public RasshiineTheme Theme { get; }
            public LocalGameRepository Repository { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Host);
            }
        }
    }
}
