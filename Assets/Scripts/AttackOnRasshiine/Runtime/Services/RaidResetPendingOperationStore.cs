using System;
using System.Text;
using UnityEngine;

namespace AttackOnRasshiine.Runtime.Services
{
    public sealed class PendingRaidResetOperation
    {
        public PendingRaidResetOperation(string idempotencyKey, string expectedRaidEpoch)
        {
            IdempotencyKey = idempotencyKey;
            ExpectedRaidEpoch = expectedRaidEpoch;
        }

        public string IdempotencyKey { get; }
        public string ExpectedRaidEpoch { get; }
    }

    /// <summary>
    /// Persists the reset request until the authoritative response is observed. A retry must
    /// retain both the key and the old epoch: if the first response was lost after commit, the
    /// server can then replay the result instead of resetting the next raid by mistake.
    /// </summary>
    public static class RaidResetPendingOperationStore
    {
        private const string StoragePrefix = "AttackOnRasshiine.PendingRaidReset.";

        public static PendingRaidResetOperation GetOrCreate(string userId, string expectedRaidEpoch)
        {
            if (TryGet(userId, out var pending))
            {
                return pending;
            }

            if (!SupabaseRaidEpoch.IsValid(expectedRaidEpoch))
            {
                throw new ArgumentException("A valid raid epoch is required for a pending reset.", nameof(expectedRaidEpoch));
            }

            var dto = new PendingRaidResetDto
            {
                IdempotencyKey = SupabaseIdempotencyKey.CreateBattleResetKey(),
                ExpectedRaidEpoch = expectedRaidEpoch.Trim()
            };
            PlayerPrefs.SetString(GetStorageKey(userId), JsonUtility.ToJson(dto));
            PlayerPrefs.Save();
            return ToOperation(dto);
        }

        public static bool TryGet(string userId, out PendingRaidResetOperation operation)
        {
            operation = null;
            var storageKey = GetStorageKey(userId);
            var json = PlayerPrefs.GetString(storageKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                var dto = JsonUtility.FromJson<PendingRaidResetDto>(json);
                if (!IsValid(dto))
                {
                    Delete(storageKey);
                    return false;
                }

                operation = ToOperation(dto);
                return true;
            }
            catch (Exception)
            {
                Delete(storageKey);
                return false;
            }
        }

        public static bool Complete(string userId, string idempotencyKey)
        {
            if (!TryGet(userId, out var pending)
                || !string.Equals(pending.IdempotencyKey, idempotencyKey?.Trim(), StringComparison.Ordinal))
            {
                return false;
            }

            Delete(GetStorageKey(userId));
            return true;
        }

        public static bool IsDefinitiveNonCommitFailure(string errorCode)
        {
            return errorCode is
                "idempotency_key_conflict" or
                "idempotency_key_invalid" or
                "battle_epoch_conflict" or
                "battle_epoch_required" or
                "battle_not_found" or
                "invalid_identifier" or
                "mentor_required" or
                "forbidden";
        }

        public static void ClearForUser(string userId)
        {
            Delete(GetStorageKey(userId));
        }

        private static bool IsValid(PendingRaidResetDto dto)
        {
            return dto != null
                && SupabaseIdempotencyKey.IsValid(dto.IdempotencyKey)
                && SupabaseRaidEpoch.IsValid(dto.ExpectedRaidEpoch);
        }

        private static PendingRaidResetOperation ToOperation(PendingRaidResetDto dto)
        {
            return new PendingRaidResetOperation(
                dto.IdempotencyKey.Trim(),
                dto.ExpectedRaidEpoch.Trim());
        }

        private static string GetStorageKey(string userId)
        {
            var normalizedUserId = userId?.Trim();
            if (string.IsNullOrEmpty(normalizedUserId))
            {
                throw new ArgumentException("A user id is required for a pending raid reset.", nameof(userId));
            }

            var encodedUserId = Convert.ToBase64String(Encoding.UTF8.GetBytes(normalizedUserId))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            return StoragePrefix + encodedUserId;
        }

        private static void Delete(string storageKey)
        {
            if (PlayerPrefs.HasKey(storageKey))
            {
                PlayerPrefs.DeleteKey(storageKey);
                PlayerPrefs.Save();
            }
        }

        [Serializable]
        private sealed class PendingRaidResetDto
        {
            public string IdempotencyKey;
            public string ExpectedRaidEpoch;
        }
    }
}
