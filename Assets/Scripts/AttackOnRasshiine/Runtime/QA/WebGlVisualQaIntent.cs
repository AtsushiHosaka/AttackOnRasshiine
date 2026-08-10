#if UNITY_EDITOR || (UNITY_WEBGL && DEVELOPMENT_BUILD)
using System;
using System.Collections.Generic;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Scene;

namespace AttackOnRasshiine.Runtime.QA
{
    /// <summary>
    /// A closed set of visual states that may be rendered by a local Development
    /// WebGL player. The enum deliberately prevents query-string reflection or
    /// arbitrary method dispatch into RaidGameApp.
    /// </summary>
    public enum WebGlVisualQaScreen
    {
        Loading,
        ConnectionGate,
        Login,
        LoginError,
        InitialPassword,
        MemberHome,
        DevLogIdle,
        DevLogActive,
        MemberProducts,
        MemberAchievements,
        MemberTeam,
        Wardrobe,
        WeaponWish,
        WeaponWishResult,
        MemberHistory,
        MemberSettings,
        Ranking,
        MentorDashboard,
        MentorOperations,
        MentorReview,
        MentorTeam,
        MentorAccounts,
        MentorAccountList,
        MentorAccountListPageTwo,
        MentorProducts,
        MentorAchievements,
        MentorSettings,
        BattleMemberScheduled,
        BattleMentorScheduled,
        BattleActive,
        BattleExpanded,
        BattleCoopTurn,
        BattleResult,
        FrontScheduled,
        FrontActive,
        FrontResult
    }

    public sealed class WebGlVisualQaIntent
    {
        public WebGlVisualQaIntent(
            string slug,
            WebGlVisualQaScreen screen,
            RasshiineProductionScene scene,
            UserRole? role)
        {
            Slug = slug;
            Screen = screen;
            Scene = scene;
            Role = role;
        }

        public string Slug { get; }
        public WebGlVisualQaScreen Screen { get; }
        public RasshiineProductionScene Scene { get; }
        public UserRole? Role { get; }
    }

    /// <summary>
    /// Carries a validated visual-QA request from the production Boot scene to
    /// the requested production scene. This source is absent from release WebGL
    /// compilation because the entire file is preprocessor guarded.
    /// </summary>
    public static class WebGlVisualQaContext
    {
        public const string ReadyLogPrefix = "[AOR_VISUAL_QA_READY] ";
        public const int MaximumUrlLength = 2048;

        private const string EnableKey = "aor-visual-qa";
        private const string ScreenKey = "screen";
        private static WebGlVisualQaIntent activeIntent;

        public static bool IsActive => activeIntent != null;

        public static bool TryActivate(string absoluteUrl, out WebGlVisualQaIntent intent, out string rejectionReason)
        {
            if (!TryParse(absoluteUrl, out intent, out rejectionReason))
            {
                return false;
            }

            activeIntent = intent;
            return true;
        }

        public static bool TryGet(out WebGlVisualQaIntent intent)
        {
            intent = activeIntent;
            return intent != null;
        }

        public static void Clear()
        {
            activeIntent = null;
        }

        public static bool TryParse(string absoluteUrl, out WebGlVisualQaIntent intent, out string rejectionReason)
        {
            intent = null;
            rejectionReason = string.Empty;
            if (string.IsNullOrWhiteSpace(absoluteUrl) || absoluteUrl.Length > MaximumUrlLength)
            {
                rejectionReason = "url_length";
                return false;
            }

            if (!Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var uri))
            {
                rejectionReason = "url_shape";
                return false;
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                rejectionReason = "url_scheme";
                return false;
            }

            if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
            {
                rejectionReason = "url_credentials_or_fragment";
                return false;
            }

            var host = (uri.DnsSafeHost ?? string.Empty).Trim('[', ']').ToLowerInvariant();
            if (host != "localhost" && host != "127.0.0.1" && host != "::1")
            {
                rejectionReason = "url_not_loopback";
                return false;
            }

            if (!TryReadStrictQuery(uri.Query, out var query, out rejectionReason))
            {
                return false;
            }

            if (!query.TryGetValue(EnableKey, out var enabled) || enabled != "1")
            {
                rejectionReason = "qa_not_enabled";
                return false;
            }

            if (!query.TryGetValue(ScreenKey, out var slug) || !TryResolveScreen(slug, out intent))
            {
                rejectionReason = "screen_not_allowed";
                return false;
            }

            return true;
        }

        public static IReadOnlyList<string> AllowedScreenSlugs => new[]
        {
            "loading",
            "connection-gate",
            "login",
            "login-error",
            "initial-password",
            "member-home",
            "dev-log-idle",
            "dev-log-active",
            "member-products",
            "member-achievements",
            "member-team",
            "wardrobe",
            "weapon-wish",
            "weapon-wish-result",
            "member-history",
            "member-settings",
            "ranking",
            "mentor-dashboard",
            "mentor-operations",
            "mentor-review",
            "mentor-team",
            "mentor-accounts",
            "mentor-account-list",
            "mentor-account-list-page2",
            "mentor-products",
            "mentor-achievements",
            "mentor-settings",
            "battle-member-scheduled",
            "battle-mentor-scheduled",
            "battle-active",
            "battle-expanded",
            "battle-coop-turn",
            "battle-result",
            "front-scheduled",
            "front-active",
            "front-result"
        };

        public static bool TryResolveScreen(string slug, out WebGlVisualQaIntent intent)
        {
            intent = slug switch
            {
                "loading" => Intent(slug, WebGlVisualQaScreen.Loading, RasshiineProductionScene.Login),
                "connection-gate" => Intent(slug, WebGlVisualQaScreen.ConnectionGate, RasshiineProductionScene.Login),
                "login" => Intent(slug, WebGlVisualQaScreen.Login, RasshiineProductionScene.Login),
                "login-error" => Intent(slug, WebGlVisualQaScreen.LoginError, RasshiineProductionScene.Login),
                "initial-password" => Intent(slug, WebGlVisualQaScreen.InitialPassword, RasshiineProductionScene.Login, UserRole.Member),
                "member-home" => Intent(slug, WebGlVisualQaScreen.MemberHome, RasshiineProductionScene.MemberHome, UserRole.Member),
                "dev-log-idle" => Intent(slug, WebGlVisualQaScreen.DevLogIdle, RasshiineProductionScene.DevLog, UserRole.Member),
                "dev-log-active" => Intent(slug, WebGlVisualQaScreen.DevLogActive, RasshiineProductionScene.DevLog, UserRole.Member),
                "member-products" => Intent(slug, WebGlVisualQaScreen.MemberProducts, RasshiineProductionScene.MemberHome, UserRole.Member),
                "member-achievements" => Intent(slug, WebGlVisualQaScreen.MemberAchievements, RasshiineProductionScene.MemberHome, UserRole.Member),
                "member-team" => Intent(slug, WebGlVisualQaScreen.MemberTeam, RasshiineProductionScene.MemberHome, UserRole.Member),
                "wardrobe" => Intent(slug, WebGlVisualQaScreen.Wardrobe, RasshiineProductionScene.MemberHome, UserRole.Member),
                "weapon-wish" => Intent(slug, WebGlVisualQaScreen.WeaponWish, RasshiineProductionScene.MemberHome, UserRole.Member),
                "weapon-wish-result" => Intent(slug, WebGlVisualQaScreen.WeaponWishResult, RasshiineProductionScene.MemberHome, UserRole.Member),
                "member-history" => Intent(slug, WebGlVisualQaScreen.MemberHistory, RasshiineProductionScene.MemberHome, UserRole.Member),
                "member-settings" => Intent(slug, WebGlVisualQaScreen.MemberSettings, RasshiineProductionScene.MemberHome, UserRole.Member),
                "ranking" => Intent(slug, WebGlVisualQaScreen.Ranking, RasshiineProductionScene.MemberHome, UserRole.Member),
                "mentor-dashboard" => Intent(slug, WebGlVisualQaScreen.MentorDashboard, RasshiineProductionScene.MentorDashboard, UserRole.Mentor),
                "mentor-operations" => Intent(slug, WebGlVisualQaScreen.MentorOperations, RasshiineProductionScene.MentorDashboard, UserRole.Mentor),
                "mentor-review" => Intent(slug, WebGlVisualQaScreen.MentorReview, RasshiineProductionScene.MentorDashboard, UserRole.Mentor),
                "mentor-team" => Intent(slug, WebGlVisualQaScreen.MentorTeam, RasshiineProductionScene.MentorDashboard, UserRole.Mentor),
                "mentor-accounts" => Intent(slug, WebGlVisualQaScreen.MentorAccounts, RasshiineProductionScene.MentorDashboard, UserRole.Mentor),
                "mentor-account-list" => Intent(slug, WebGlVisualQaScreen.MentorAccountList, RasshiineProductionScene.MentorDashboard, UserRole.Mentor),
                "mentor-account-list-page2" => Intent(slug, WebGlVisualQaScreen.MentorAccountListPageTwo, RasshiineProductionScene.MentorDashboard, UserRole.Mentor),
                "mentor-products" => Intent(slug, WebGlVisualQaScreen.MentorProducts, RasshiineProductionScene.MentorDashboard, UserRole.Mentor),
                "mentor-achievements" => Intent(slug, WebGlVisualQaScreen.MentorAchievements, RasshiineProductionScene.MentorDashboard, UserRole.Mentor),
                "mentor-settings" => Intent(slug, WebGlVisualQaScreen.MentorSettings, RasshiineProductionScene.MentorDashboard, UserRole.Mentor),
                "battle-member-scheduled" => Intent(slug, WebGlVisualQaScreen.BattleMemberScheduled, RasshiineProductionScene.Battle, UserRole.Member),
                "battle-mentor-scheduled" => Intent(slug, WebGlVisualQaScreen.BattleMentorScheduled, RasshiineProductionScene.Battle, UserRole.Mentor),
                "battle-active" => Intent(slug, WebGlVisualQaScreen.BattleActive, RasshiineProductionScene.Battle, UserRole.Member),
                "battle-expanded" => Intent(slug, WebGlVisualQaScreen.BattleExpanded, RasshiineProductionScene.Battle, UserRole.Member),
                "battle-coop-turn" => Intent(slug, WebGlVisualQaScreen.BattleCoopTurn, RasshiineProductionScene.Battle, UserRole.Member),
                "battle-result" => Intent(slug, WebGlVisualQaScreen.BattleResult, RasshiineProductionScene.Battle, UserRole.Member),
                "front-scheduled" => Intent(slug, WebGlVisualQaScreen.FrontScheduled, RasshiineProductionScene.FrontDisplay),
                "front-active" => Intent(slug, WebGlVisualQaScreen.FrontActive, RasshiineProductionScene.FrontDisplay),
                "front-result" => Intent(slug, WebGlVisualQaScreen.FrontResult, RasshiineProductionScene.FrontDisplay),
                _ => null
            };
            return intent != null;
        }

        private static WebGlVisualQaIntent Intent(
            string slug,
            WebGlVisualQaScreen screen,
            RasshiineProductionScene scene,
            UserRole? role = null)
        {
            return new WebGlVisualQaIntent(slug, screen, scene, role);
        }

        private static bool TryReadStrictQuery(
            string rawQuery,
            out Dictionary<string, string> query,
            out string rejectionReason)
        {
            query = new Dictionary<string, string>(StringComparer.Ordinal);
            rejectionReason = string.Empty;
            var value = string.IsNullOrEmpty(rawQuery) ? string.Empty : rawQuery.TrimStart('?');
            if (string.IsNullOrEmpty(value))
            {
                rejectionReason = "query_missing";
                return false;
            }

            foreach (var pair in value.Split('&'))
            {
                var separator = pair.IndexOf('=');
                if (separator <= 0 || separator == pair.Length - 1)
                {
                    rejectionReason = "query_shape";
                    return false;
                }

                string key;
                string decodedValue;
                try
                {
                    key = Uri.UnescapeDataString(pair.Substring(0, separator));
                    decodedValue = Uri.UnescapeDataString(pair.Substring(separator + 1));
                }
                catch (UriFormatException)
                {
                    rejectionReason = "query_encoding";
                    return false;
                }

                if ((key != EnableKey && key != ScreenKey) || query.ContainsKey(key))
                {
                    rejectionReason = key == EnableKey || key == ScreenKey ? "query_duplicate" : "query_unknown";
                    return false;
                }

                query.Add(key, decodedValue);
            }

            if (query.Count != 2)
            {
                rejectionReason = "query_incomplete";
                return false;
            }

            return true;
        }
    }
}
#endif
