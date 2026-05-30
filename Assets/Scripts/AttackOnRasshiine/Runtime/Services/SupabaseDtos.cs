using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AttackOnRasshiine.Runtime.Data;

namespace AttackOnRasshiine.Runtime.Services
{
    [Serializable]
    public sealed class SupabaseRuntimeConfigDto
    {
        public bool Enabled = true;
        public string SupabaseUrl;
        public string SupabasePublishableKey;
    }

    [Serializable]
    public sealed class SupabaseGameApiRequestDto
    {
        public string Action;
        public string SessionToken;
        public string LoginId;
        public string Password;
        public string UserId;
        public string SessionId;
        public string AchievementId;
        public string Goal;
        public int AchievementRate;
        public int AchievementType;
        public string Title;
        public string Description;
        public string Reflection;
        public string NextTask;
        public string Comment;
        public int Role;
        public int Weapon;
        public int ActionType;
        public float Multiplier;
    }

    [Serializable]
    public sealed class SupabaseGameApiResponseDto
    {
        public bool Ok;
        public string Error;
        public string SessionToken;
        public UserProfileDto User;
        public GameSnapshotDto Snapshot;
        public DevSessionDto Session;
        public BattleActionResultDto ActionResult;
    }

    [Serializable]
    public sealed class GameSnapshotDto
    {
        public List<UserProfileDto> Users = new();
        public List<CharacterStatsDto> Stats = new();
        public List<WeaponDefinitionDto> Weapons = new();
        public List<DevSessionDto> Sessions = new();
        public List<ProductEntryDto> Products = new();
        public List<AchievementEntryDto> Achievements = new();
        public List<AuditLogEntryDto> AuditLogs = new();
        public BossBattleStateDto ActiveBattle;
    }

    [Serializable]
    public sealed class UserProfileDto
    {
        public string Id;
        public string LoginId;
        public string Nickname;
        public int Role;
        public string TeamId;
        public bool RankingVisible;
        public bool InitialPasswordChanged;
        public bool IsActive;
    }

    [Serializable]
    public sealed class CharacterStatsDto
    {
        public string UserId;
        public int Level;
        public int Exp;
        public int Hp;
        public int Atk;
        public int Def;
        public int Mp;
        public List<int> UnlockedWeapons = new();
        public List<string> Titles = new();
        public List<string> Skills = new();
    }

    [Serializable]
    public sealed class AiEvaluationDto
    {
        public int TotalScore;
        public int Rank;
        public int GoalScore;
        public int SpecificityScore;
        public int LearningScore;
        public int NextActionScore;
        public int ContinuityScore;
        public float ExpMultiplier;
        public string Feedback;
        public string ModelName;
    }

    [Serializable]
    public sealed class DevSessionDto
    {
        public string Id;
        public string UserId;
        public string StartedAtUtc;
        public string EndedAtUtc;
        public int DurationMinutes;
        public string Goal;
        public int AchievementRate;
        public string Reflection;
        public string NextTask;
        public int Status;
        public List<string> SuspiciousFlags = new();
        public string MentorComment;
        public string ApprovedBy;
        public string ApprovedAtUtc;
        public string AiEvaluationFailureReason;
        public AiEvaluationDto Evaluation;
    }

    [Serializable]
    public sealed class WeaponDefinitionDto
    {
        public string Id;
        public int Kind;
        public string DisplayName;
        public string Description;
        public int MpEfficiencyBonus;
        public float DamageMultiplier;
        public int PreferredRole;
        public bool IsSpecial;
    }

    [Serializable]
    public sealed class ProductEntryDto
    {
        public string Id;
        public string UserId;
        public string Title;
        public string Url;
        public string Description;
        public bool IsPublic;
        public string HiddenBy;
        public string CreatedAtUtc;
    }

    [Serializable]
    public sealed class AchievementEntryDto
    {
        public string Id;
        public string UserId;
        public int Type;
        public string Title;
        public string Description;
        public int Status;
        public string ApprovedBy;
        public string ApprovedAtUtc;
        public string CreatedAtUtc;
        public bool HasRewardWeapon;
        public int RewardWeapon;
        public string RewardTitle;
        public string RewardSkill;
    }

    [Serializable]
    public sealed class AuditLogEntryDto
    {
        public string Id;
        public string ActorUserId;
        public string ActionType;
        public string TargetType;
        public string TargetId;
        public string Before;
        public string After;
        public string CreatedAtUtc;
    }

    [Serializable]
    public sealed class BattleParticipantDto
    {
        public string UserId;
        public string Nickname;
        public CharacterStatsDto Stats;
        public int Role;
        public int Weapon;
        public int CurrentHp;
        public int CurrentMp;
        public int TotalDamage;
        public int TotalHeal;
        public int SupportCount;
    }

    [Serializable]
    public sealed class MentorBossDto
    {
        public string Id;
        public string Name;
        public string BossType;
        public int MaxHp;
        public int CurrentHp;
        public int Def;
        public string StoryTeaser;
    }

    [Serializable]
    public sealed class BattleActionResultDto
    {
        public string UserId;
        public string Nickname;
        public int Role;
        public int Weapon;
        public int ActionType;
        public int TurnNumber;
        public int MpCost;
        public int Damage;
        public int Heal;
        public string SupportEffect;
        public string Message;
    }

    [Serializable]
    public sealed class BattleActionSelectionDto
    {
        public string BattleId;
        public string UserId;
        public string Nickname;
        public int TurnNumber;
        public int Role;
        public int Weapon;
        public int ActionType;
        public int MpCost;
        public int Damage;
        public int Heal;
        public string SupportEffect;
        public string SelectedAtUtc;
    }

    [Serializable]
    public sealed class BossBattleStateDto
    {
        public string Id;
        public MentorBossDto Boss;
        public List<BattleParticipantDto> Participants = new();
        public List<BattleActionSelectionDto> ActionSelections = new();
        public int Status;
        public int TurnNumber;
        public int TurnCount;
        public int Phase;
        public int Outcome;
        public string Result;
        public int TotalDamage;
        public string HighlightUserId;
    }

    public static class SupabaseDtoMapper
    {
        public static GameSnapshot ToSnapshot(this GameSnapshotDto dto)
        {
            var snapshot = new GameSnapshot();
            if (dto == null)
            {
                return snapshot;
            }

            foreach (var user in dto.Users ?? new List<UserProfileDto>())
            {
                snapshot.Users.Add(user.ToDomain());
            }

            foreach (var stats in dto.Stats ?? new List<CharacterStatsDto>())
            {
                snapshot.Stats.Add(new CharacterStatsRecord
                {
                    UserId = stats.UserId,
                    Stats = stats.ToDomain()
                });
            }

            foreach (var weapon in dto.Weapons ?? new List<WeaponDefinitionDto>())
            {
                snapshot.Weapons.Add(weapon.ToDomain());
            }

            foreach (var session in dto.Sessions ?? new List<DevSessionDto>())
            {
                snapshot.Sessions.Add(session.ToDomain());
            }

            foreach (var product in dto.Products ?? new List<ProductEntryDto>())
            {
                snapshot.Products.Add(product.ToDomain());
            }

            foreach (var achievement in dto.Achievements ?? new List<AchievementEntryDto>())
            {
                snapshot.Achievements.Add(achievement.ToDomain());
            }

            foreach (var auditLog in dto.AuditLogs ?? new List<AuditLogEntryDto>())
            {
                snapshot.AuditLogs.Add(auditLog.ToDomain());
            }

            snapshot.ActiveBattle = dto.ActiveBattle.ToDomain();
            return snapshot;
        }

        public static UserProfile ToDomain(this UserProfileDto dto)
        {
            if (dto == null)
            {
                return null;
            }

            return new UserProfile
            {
                Id = dto.Id,
                LoginId = dto.LoginId,
                Nickname = dto.Nickname,
                Role = ClampEnum<UserRole>(dto.Role),
                TeamId = dto.TeamId,
                RankingVisible = dto.RankingVisible,
                InitialPasswordChanged = dto.InitialPasswordChanged,
                IsActive = dto.IsActive
            };
        }

        public static CharacterStats ToDomain(this CharacterStatsDto dto)
        {
            if (dto == null)
            {
                return new CharacterStats();
            }

            return new CharacterStats
            {
                Level = Math.Max(1, dto.Level),
                Exp = Math.Max(0, dto.Exp),
                Hp = Math.Max(1, dto.Hp),
                Atk = Math.Max(0, dto.Atk),
                Def = Math.Max(0, dto.Def),
                Mp = Math.Max(0, dto.Mp),
                UnlockedWeapons = (dto.UnlockedWeapons ?? new List<int>()).Select(ClampEnum<WeaponKind>).Distinct().ToList(),
                Titles = (dto.Titles ?? new List<string>()).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct().ToList(),
                Skills = (dto.Skills ?? new List<string>()).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct().ToList()
            };
        }

        public static WeaponDefinition ToDomain(this WeaponDefinitionDto dto)
        {
            return new WeaponDefinition
            {
                Kind = ClampEnum<WeaponKind>(dto.Kind),
                DisplayName = dto.DisplayName,
                Description = dto.Description,
                MpEfficiencyBonus = dto.MpEfficiencyBonus,
                DamageMultiplier = dto.DamageMultiplier <= 0f ? 1f : dto.DamageMultiplier,
                PreferredRole = ClampEnum<BattleRole>(dto.PreferredRole),
                IsSpecial = dto.IsSpecial
            };
        }

        public static DevSession ToDomain(this DevSessionDto dto)
        {
            if (dto == null)
            {
                return null;
            }

            return new DevSession
            {
                Id = dto.Id,
                UserId = dto.UserId,
                StartedAtUtc = ParseUtc(dto.StartedAtUtc),
                EndedAtUtc = ParseNullableUtc(dto.EndedAtUtc),
                DurationMinutes = dto.DurationMinutes,
                Goal = dto.Goal,
                AchievementRate = dto.AchievementRate,
                Reflection = dto.Reflection,
                NextTask = dto.NextTask,
                Status = ClampEnum<DevSessionStatus>(dto.Status),
                SuspiciousFlags = dto.SuspiciousFlags ?? new List<string>(),
                MentorComment = dto.MentorComment,
                ApprovedBy = dto.ApprovedBy,
                ApprovedAtUtc = ParseNullableUtc(dto.ApprovedAtUtc),
                AiEvaluationFailureReason = dto.AiEvaluationFailureReason,
                Evaluation = dto.Evaluation.ToDomain()
            };
        }

        public static AiEvaluation ToDomain(this AiEvaluationDto dto)
        {
            if (dto == null)
            {
                return null;
            }

            return new AiEvaluation
            {
                TotalScore = dto.TotalScore,
                Rank = ClampEnum<AiRank>(dto.Rank),
                GoalScore = dto.GoalScore,
                SpecificityScore = dto.SpecificityScore,
                LearningScore = dto.LearningScore,
                NextActionScore = dto.NextActionScore,
                ContinuityScore = dto.ContinuityScore,
                ExpMultiplier = dto.ExpMultiplier,
                Feedback = dto.Feedback,
                ModelName = dto.ModelName
            };
        }

        public static ProductEntry ToDomain(this ProductEntryDto dto)
        {
            if (dto == null)
            {
                return null;
            }

            return new ProductEntry
            {
                Id = dto.Id,
                UserId = dto.UserId,
                Title = dto.Title,
                Url = dto.Url,
                Description = dto.Description,
                IsPublic = dto.IsPublic,
                HiddenBy = dto.HiddenBy,
                CreatedAtUtc = ParseUtc(dto.CreatedAtUtc)
            };
        }

        public static AchievementEntry ToDomain(this AchievementEntryDto dto)
        {
            if (dto == null)
            {
                return null;
            }

            return new AchievementEntry
            {
                Id = dto.Id,
                UserId = dto.UserId,
                Type = ClampEnum<AchievementType>(dto.Type),
                Title = dto.Title,
                Description = dto.Description,
                Status = ClampEnum<AchievementStatus>(dto.Status),
                ApprovedBy = dto.ApprovedBy,
                ApprovedAtUtc = ParseNullableUtc(dto.ApprovedAtUtc),
                CreatedAtUtc = ParseUtc(dto.CreatedAtUtc),
                HasRewardWeapon = dto.HasRewardWeapon,
                RewardWeapon = ClampEnum<WeaponKind>(dto.RewardWeapon),
                RewardTitle = dto.RewardTitle,
                RewardSkill = dto.RewardSkill
            };
        }

        public static AuditLogEntry ToDomain(this AuditLogEntryDto dto)
        {
            if (dto == null)
            {
                return null;
            }

            return new AuditLogEntry
            {
                Id = dto.Id,
                ActorUserId = dto.ActorUserId,
                ActionType = dto.ActionType,
                TargetType = dto.TargetType,
                TargetId = dto.TargetId,
                Before = dto.Before,
                After = dto.After,
                CreatedAtUtc = ParseUtc(dto.CreatedAtUtc)
            };
        }

        public static BattleActionResult ToDomain(this BattleActionResultDto dto)
        {
            if (dto == null)
            {
                return null;
            }

            return new BattleActionResult
            {
                UserId = dto.UserId,
                Nickname = dto.Nickname,
                Role = ClampEnum<BattleRole>(dto.Role),
                Weapon = ClampEnum<WeaponKind>(dto.Weapon),
                ActionType = ClampEnum<BattleActionType>(dto.ActionType),
                TurnNumber = dto.TurnNumber,
                MpCost = dto.MpCost,
                Damage = dto.Damage,
                Heal = dto.Heal,
                SupportEffect = dto.SupportEffect,
                Message = dto.Message
            };
        }

        public static BattleActionSelection ToDomain(this BattleActionSelectionDto dto)
        {
            if (dto == null)
            {
                return null;
            }

            return new BattleActionSelection
            {
                BattleId = dto.BattleId,
                UserId = dto.UserId,
                Nickname = dto.Nickname,
                TurnNumber = dto.TurnNumber,
                Role = ClampEnum<BattleRole>(dto.Role),
                Weapon = ClampEnum<WeaponKind>(dto.Weapon),
                ActionType = ClampEnum<BattleActionType>(dto.ActionType),
                MpCost = dto.MpCost,
                Damage = dto.Damage,
                Heal = dto.Heal,
                SupportEffect = dto.SupportEffect,
                SelectedAtUtc = ParseUtc(dto.SelectedAtUtc)
            };
        }

        private static BossBattleState ToDomain(this BossBattleStateDto dto)
        {
            if (dto == null)
            {
                return null;
            }

            var status = ClampEnum<BattleStatus>(dto.Status);
            var phase = ClampEnum<BattlePhase>(dto.Phase);
            var battle = new BossBattleState
            {
                Id = dto.Id,
                Boss = dto.Boss.ToDomain(),
                Status = status,
                TurnNumber = dto.TurnNumber,
                TurnCount = dto.TurnCount,
                Phase = phase,
                Outcome = ResolveBattleOutcome(dto, status, phase),
                TotalDamage = dto.TotalDamage,
                HighlightUserId = dto.HighlightUserId
            };

            foreach (var participant in dto.Participants ?? new List<BattleParticipantDto>())
            {
                battle.Participants.Add(participant.ToDomain());
            }

            foreach (var selection in dto.ActionSelections ?? new List<BattleActionSelectionDto>())
            {
                var actionSelection = selection.ToDomain();
                if (actionSelection == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(actionSelection.BattleId))
                {
                    actionSelection.BattleId = battle.Id;
                }

                battle.ActionSelections.Add(actionSelection);
            }

            return battle;
        }

        private static BattleOutcome ResolveBattleOutcome(BossBattleStateDto dto, BattleStatus status, BattlePhase phase)
        {
            var result = dto.Result?.Trim().ToLowerInvariant();
            if (result == "win" || result == "victory")
            {
                return BattleOutcome.Victory;
            }

            if (result == "lose" || result == "loss" || result == "defeat" || result == "time_up")
            {
                return BattleOutcome.Defeat;
            }

            var outcome = ClampEnum<BattleOutcome>(dto.Outcome);
            if (outcome != BattleOutcome.Undecided)
            {
                return outcome;
            }

            if (dto.Boss != null && dto.Boss.CurrentHp <= 0)
            {
                return BattleOutcome.Victory;
            }

            if (status == BattleStatus.Completed || phase == BattlePhase.Completed || dto.TurnNumber > dto.TurnCount)
            {
                return BattleOutcome.Defeat;
            }

            return BattleOutcome.Undecided;
        }

        private static MentorBoss ToDomain(this MentorBossDto dto)
        {
            if (dto == null)
            {
                return new MentorBoss();
            }

            return new MentorBoss
            {
                Id = dto.Id,
                Name = dto.Name,
                BossType = dto.BossType,
                MaxHp = dto.MaxHp,
                CurrentHp = dto.CurrentHp,
                Def = dto.Def,
                StoryTeaser = dto.StoryTeaser
            };
        }

        private static BattleParticipant ToDomain(this BattleParticipantDto dto)
        {
            return new BattleParticipant
            {
                UserId = dto.UserId,
                Nickname = dto.Nickname,
                Stats = dto.Stats.ToDomain(),
                Role = ClampEnum<BattleRole>(dto.Role),
                Weapon = ClampEnum<WeaponKind>(dto.Weapon),
                CurrentHp = dto.CurrentHp,
                CurrentMp = dto.CurrentMp,
                TotalDamage = dto.TotalDamage,
                TotalHeal = dto.TotalHeal,
                SupportCount = dto.SupportCount
            };
        }

        private static T ClampEnum<T>(int value) where T : Enum
        {
            var values = Enum.GetValues(typeof(T));
            var index = Math.Max(0, Math.Min(values.Length - 1, value));
            return (T)values.GetValue(index);
        }

        private static DateTime ParseUtc(string value)
        {
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            {
                return parsed.ToUniversalTime();
            }

            return DateTime.UtcNow;
        }

        private static DateTime? ParseNullableUtc(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return ParseUtc(value);
        }
    }
}
