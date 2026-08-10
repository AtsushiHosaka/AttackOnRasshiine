using System;
using System.Globalization;

namespace AttackOnRasshiine.Runtime.Services
{
    public static class RuntimeUrlSecurity
    {
        public static bool TryNormalizeHttpsOrigin(string value, out string normalized)
        {
            normalized = string.Empty;
            if (!TryCreatePublicHttpsUri(value, out var uri)
                || !string.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal)
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment))
            {
                return false;
            }

            normalized = uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
            return true;
        }

        public static bool TryNormalizeExternalHttpsUrl(string value, out string normalized)
        {
            normalized = string.Empty;
            if (!TryCreatePublicHttpsUri(value, out var uri))
            {
                return false;
            }

            normalized = uri.AbsoluteUri;
            return true;
        }

        private static bool TryCreatePublicHttpsUri(string value, out Uri uri)
        {
            uri = null;
            if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var candidate)
                || !string.Equals(candidate.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(candidate.Host)
                || !string.IsNullOrEmpty(candidate.UserInfo)
                || !IsPublicDnsHost(candidate))
            {
                return false;
            }

            uri = candidate;
            return true;
        }

        private static bool IsPublicDnsHost(Uri uri)
        {
            if (uri.HostNameType != UriHostNameType.Dns)
            {
                return false;
            }

            var host = uri.IdnHost.TrimEnd('.');
            if (host.Length == 0 || host.IndexOf('.') <= 0)
            {
                return false;
            }

            return !string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                && !host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
                && !host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
                && !host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)
                && !host.EndsWith(".home.arpa", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static class SupabaseRuntimeConfigSecurity
    {
        public const string PublishableKeyPrefix = "sb_publishable_";

        private static readonly string[] ForbiddenJsonNames =
        {
            "SupabaseSecretKey",
            "SupabaseServiceRoleKey",
            "SUPABASE_SECRET_KEY",
            "SUPABASE_SERVICE_ROLE_KEY",
            "SUPABASE_DB_PASSWORD",
            "SUPABASE_ACCESS_TOKEN"
        };

        public static bool IsPublishableKey(string value)
        {
            var key = value?.Trim();
            return !string.IsNullOrEmpty(key)
                && key.StartsWith(PublishableKeyPrefix, StringComparison.Ordinal)
                && key.Length > PublishableKeyPrefix.Length;
        }

        public static bool ContainsForbiddenSecretConfiguration(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            if (json.IndexOf("sb_secret_", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            foreach (var name in ForbiddenJsonNames)
            {
                if (json.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static class SupabaseSessionToken
    {
        public const int MaximumLength = 128;

        public static bool IsValid(string value)
        {
            var token = value?.Trim();
            if (string.IsNullOrEmpty(token) || token.Length > MaximumLength)
            {
                return false;
            }

            foreach (var character in token)
            {
                if (character < 0x21 || character > 0x7e)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public static class SupabaseGameApiEndpoint
    {
        private const string FunctionPathPrefix = "/functions/v1/";

        public static bool TryResolvePath(string apiFunctionName, string apiPath, out string resolvedPath)
        {
            var explicitPath = apiPath?.Trim();
            if (!string.IsNullOrEmpty(explicitPath))
            {
                var functionNameFromPath = explicitPath.StartsWith(FunctionPathPrefix, StringComparison.Ordinal)
                    ? explicitPath.Substring(FunctionPathPrefix.Length)
                    : string.Empty;
                if (!IsSafeFunctionName(functionNameFromPath))
                {
                    resolvedPath = string.Empty;
                    return false;
                }

                resolvedPath = FunctionPathPrefix + functionNameFromPath;
                return true;
            }

            var functionName = string.IsNullOrWhiteSpace(apiFunctionName)
                ? SupabaseGameApiContract.DefaultFunctionName
                : apiFunctionName.Trim();
            if (!IsSafeFunctionName(functionName))
            {
                resolvedPath = string.Empty;
                return false;
            }

            resolvedPath = FunctionPathPrefix + functionName;
            return true;
        }

        public static string Build(string supabaseUrl, string apiPath)
        {
            return $"{supabaseUrl?.Trim().TrimEnd('/')}{apiPath}";
        }

        private static bool IsSafeFunctionName(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
            {
                return false;
            }

            foreach (var character in value)
            {
                if (!char.IsLetterOrDigit(character) && character != '-' && character != '_')
                {
                    return false;
                }
            }

            return true;
        }
    }

    public static class SupabaseIdempotencyKey
    {
        public const int MinimumLength = 8;
        public const int MaximumLength = 128;

        public static string CreateCosmeticGachaKey()
        {
            return Create("gacha");
        }

        public static string CreateBattleActionKey()
        {
            return Create("battle");
        }

        public static string CreateBattleResetKey()
        {
            return Create("reset");
        }

        public static bool IsValid(string value)
        {
            var key = value?.Trim();
            if (string.IsNullOrEmpty(key) || key.Length < MinimumLength || key.Length > MaximumLength)
            {
                return false;
            }

            foreach (var character in key)
            {
                if (!char.IsLetterOrDigit(character)
                    && character != '.'
                    && character != '_'
                    && character != ':'
                    && character != '-')
                {
                    return false;
                }
            }

            return true;
        }

        private static string Create(string operation)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture);
            return $"aor:{operation}:{timestamp}:{Guid.NewGuid():N}";
        }
    }

    public static class SupabaseRaidEpoch
    {
        public static bool IsValid(string value)
        {
            return Guid.TryParseExact(value?.Trim(), "D", out var parsed)
                && parsed != Guid.Empty;
        }
    }

    public static class SupabaseResetBattleContract
    {
        public static bool IsValidSuccessResponse(
            ResetBattleResponseDto response,
            string expectedRaidEpoch)
        {
            var result = response?.ResetResult;
            var battle = response?.BattleDelta;
            if (response?.Ok != true
                || result == null
                || battle == null
                || !SupabaseRaidEpoch.IsValid(expectedRaidEpoch)
                || !SupabaseRaidEpoch.IsValid(result.PreviousRaidEpoch)
                || !SupabaseRaidEpoch.IsValid(result.RaidEpoch)
                || !SupabaseRaidEpoch.IsValid(result.BattleId)
                || !string.Equals(result.PreviousRaidEpoch, expectedRaidEpoch.Trim(), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(battle.Id, result.BattleId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(battle.RaidEpoch, result.RaidEpoch, StringComparison.OrdinalIgnoreCase)
                || string.Equals(result.RaidEpoch, result.PreviousRaidEpoch, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(result.BossName)
                || string.IsNullOrWhiteSpace(result.BossType))
            {
                return false;
            }

            return string.Equals(battle.BossName, result.BossName, StringComparison.Ordinal)
                && string.Equals(battle.BossType, result.BossType, StringComparison.Ordinal);
        }
    }

    public static class SupabaseGameApiRequestFactory
    {
        public static SupabaseGameApiRequestDto Logout(string sessionToken)
        {
            return new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.Logout,
                SessionToken = Normalize(sessionToken)
            };
        }

        public static CosmeticInventoryRequestDto CosmeticInventory(string sessionToken)
        {
            return new CosmeticInventoryRequestDto
            {
                SessionToken = Normalize(sessionToken)
            };
        }

        public static RollCosmeticGachaRequestDto RollCosmeticGacha(string sessionToken, string idempotencyKey)
        {
            return new RollCosmeticGachaRequestDto
            {
                SessionToken = Normalize(sessionToken),
                IdempotencyKey = Normalize(idempotencyKey)
            };
        }

        public static EquipCosmeticRequestDto EquipCosmetic(string sessionToken, string itemId)
        {
            return new EquipCosmeticRequestDto
            {
                SessionToken = Normalize(sessionToken),
                ItemId = Normalize(itemId)
            };
        }

        public static BattleActionRequestDto BattleAction(
            string sessionToken,
            string idempotencyKey,
            int role,
            int weapon,
            int actionType)
        {
            return new BattleActionRequestDto
            {
                SessionToken = Normalize(sessionToken),
                IdempotencyKey = Normalize(idempotencyKey),
                Role = role,
                Weapon = weapon,
                ActionType = actionType
            };
        }

        public static ResetBattleRequestDto ResetBattle(
            string sessionToken,
            string idempotencyKey,
            string expectedRaidEpoch)
        {
            return new ResetBattleRequestDto
            {
                SessionToken = Normalize(sessionToken),
                IdempotencyKey = Normalize(idempotencyKey),
                ExpectedRaidEpoch = Normalize(expectedRaidEpoch)
            };
        }

        private static string Normalize(string value)
        {
            return value?.Trim() ?? string.Empty;
        }
    }
}
