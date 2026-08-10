using System;
using System.Text;
using AttackOnRasshiine.Runtime.Data;
using UnityEngine;

namespace AttackOnRasshiine.Runtime.Services
{
    public sealed class PendingBattleActionOperation
    {
        public PendingBattleActionOperation(
            string idempotencyKey,
            BattleRole role,
            WeaponKind weapon,
            BattleActionType actionType)
        {
            IdempotencyKey = idempotencyKey;
            Role = role;
            Weapon = weapon;
            ActionType = actionType;
        }

        public string IdempotencyKey { get; }
        public BattleRole Role { get; }
        public WeaponKind Weapon { get; }
        public BattleActionType ActionType { get; }
    }

    /// <summary>
    /// Keeps the exact action payload and idempotency key until the authoritative API
    /// confirms a result. This closes the ambiguous network-failure window where creating
    /// a fresh key on retry could otherwise commit a second raid action.
    /// </summary>
    public static class BattleActionPendingOperationStore
    {
        private const string StoragePrefix = "AttackOnRasshiine.PendingBattleAction.";

        public static PendingBattleActionOperation GetOrCreate(
            string userId,
            BattleRole role,
            WeaponKind weapon,
            BattleActionType actionType)
        {
            ValidatePayload(role, weapon, actionType);
            if (TryGet(userId, out var pending))
            {
                return pending;
            }

            var dto = new PendingBattleActionDto
            {
                IdempotencyKey = SupabaseIdempotencyKey.CreateBattleActionKey(),
                Role = (int)role,
                Weapon = (int)weapon,
                ActionType = (int)actionType
            };
            PlayerPrefs.SetString(GetStorageKey(userId), JsonUtility.ToJson(dto));
            PlayerPrefs.Save();
            return ToOperation(dto);
        }

        public static bool TryGet(string userId, out PendingBattleActionOperation operation)
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
                var dto = JsonUtility.FromJson<PendingBattleActionDto>(json);
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
                "battle_not_found" or
                "battle_not_active" or
                "battle_turns_exhausted" or
                "raid_turn_already_committed" or
                "turn_out_of_range" or
                "weapon_not_unlocked" or
                "insufficient_mp" or
                "member_required" or
                "forbidden";
        }

        public static void ClearForUser(string userId)
        {
            Delete(GetStorageKey(userId));
        }

        private static void ValidatePayload(BattleRole role, WeaponKind weapon, BattleActionType actionType)
        {
            if (!Enum.IsDefined(typeof(BattleRole), role))
            {
                throw new ArgumentOutOfRangeException(nameof(role));
            }

            if (!Enum.IsDefined(typeof(WeaponKind), weapon))
            {
                throw new ArgumentOutOfRangeException(nameof(weapon));
            }

            if (!Enum.IsDefined(typeof(BattleActionType), actionType))
            {
                throw new ArgumentOutOfRangeException(nameof(actionType));
            }
        }

        private static bool IsValid(PendingBattleActionDto dto)
        {
            return dto != null
                && SupabaseIdempotencyKey.IsValid(dto.IdempotencyKey)
                && Enum.IsDefined(typeof(BattleRole), dto.Role)
                && Enum.IsDefined(typeof(WeaponKind), dto.Weapon)
                && Enum.IsDefined(typeof(BattleActionType), dto.ActionType);
        }

        private static PendingBattleActionOperation ToOperation(PendingBattleActionDto dto)
        {
            return new PendingBattleActionOperation(
                dto.IdempotencyKey.Trim(),
                (BattleRole)dto.Role,
                (WeaponKind)dto.Weapon,
                (BattleActionType)dto.ActionType);
        }

        private static string GetStorageKey(string userId)
        {
            var normalizedUserId = userId?.Trim();
            if (string.IsNullOrEmpty(normalizedUserId))
            {
                throw new ArgumentException("A user id is required for a pending battle action.", nameof(userId));
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
        private sealed class PendingBattleActionDto
        {
            public string IdempotencyKey;
            public int Role;
            public int Weapon;
            public int ActionType;
        }
    }
}
