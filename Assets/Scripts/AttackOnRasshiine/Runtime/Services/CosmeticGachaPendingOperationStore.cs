using System;
using System.Text;
using UnityEngine;

namespace AttackOnRasshiine.Runtime.Services
{
    /// <summary>
    /// Persists a non-secret idempotency key per account. WebGL PlayerPrefs is backed by
    /// browser storage, allowing an ambiguous request to be retried safely after a reload.
    /// </summary>
    public static class CosmeticGachaPendingOperationStore
    {
        private const string StoragePrefix = "AttackOnRasshiine.PendingCosmeticGacha.";

        public static string GetOrCreate(string userId)
        {
            var storageKey = GetStorageKey(userId);
            var pendingKey = PlayerPrefs.GetString(storageKey, string.Empty)?.Trim();
            if (SupabaseIdempotencyKey.IsValid(pendingKey))
            {
                return pendingKey;
            }

            pendingKey = SupabaseIdempotencyKey.CreateCosmeticGachaKey();
            PlayerPrefs.SetString(storageKey, pendingKey);
            PlayerPrefs.Save();
            return pendingKey;
        }

        public static bool TryGet(string userId, out string idempotencyKey)
        {
            var storageKey = GetStorageKey(userId);
            idempotencyKey = PlayerPrefs.GetString(storageKey, string.Empty)?.Trim() ?? string.Empty;
            if (SupabaseIdempotencyKey.IsValid(idempotencyKey))
            {
                return true;
            }

            idempotencyKey = string.Empty;
            if (PlayerPrefs.HasKey(storageKey))
            {
                PlayerPrefs.DeleteKey(storageKey);
                PlayerPrefs.Save();
            }

            return false;
        }

        public static bool Complete(string userId, string idempotencyKey)
        {
            if (!TryGet(userId, out var pendingKey)
                || !string.Equals(pendingKey, idempotencyKey?.Trim(), StringComparison.Ordinal))
            {
                return false;
            }

            PlayerPrefs.DeleteKey(GetStorageKey(userId));
            PlayerPrefs.Save();
            return true;
        }

        public static void ClearForUser(string userId)
        {
            PlayerPrefs.DeleteKey(GetStorageKey(userId));
            PlayerPrefs.Save();
        }

        private static string GetStorageKey(string userId)
        {
            var normalizedUserId = userId?.Trim();
            if (string.IsNullOrEmpty(normalizedUserId))
            {
                throw new ArgumentException("A user id is required for a pending cosmetic operation.", nameof(userId));
            }

            var encodedUserId = Convert.ToBase64String(Encoding.UTF8.GetBytes(normalizedUserId))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            return StoragePrefix + encodedUserId;
        }
    }
}
