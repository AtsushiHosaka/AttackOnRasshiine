using System;
using System.Collections.Generic;
using UnityEngine;

namespace AttackOnRasshiine.Runtime.Data
{
    [Serializable]
    public sealed class UserProfile
    {
        public string Id;
        public string LoginId;
        public string Nickname;
        public UserRole Role;
        public string TeamId;
        public bool RankingVisible = true;
        public bool InitialPasswordChanged = true;
        public bool IsActive = true;
    }

    [Serializable]
    public sealed class CharacterStats
    {
        public int Level = 1;
        public int Exp;
        public int Hp = 100;
        public int Atk = 10;
        public int Def = 5;
        public int Mp = 30;
        public List<WeaponKind> UnlockedWeapons = new();
        public List<string> Titles = new();
        public List<string> Skills = new();

        public int ExpToNextLevel => 50 + Level * 25;

        public void RecalculateDerivedStats()
        {
            Hp = 100 + (Level - 1) * 10;
            Atk = 10 + (Level - 1) * 2;
            Def = 5 + (Level - 1);
            Mp = 30 + (Level - 1) * 2;
        }

        public int AddExp(int amount)
        {
            var levelsGained = 0;
            Exp += Mathf.Max(0, amount);
            while (Exp >= ExpToNextLevel)
            {
                Exp -= ExpToNextLevel;
                Level += 1;
                levelsGained += 1;
            }

            RecalculateDerivedStats();
            return levelsGained;
        }
    }

    [Serializable]
    public sealed class AiEvaluation
    {
        public int TotalScore;
        public AiRank Rank;
        public int GoalScore;
        public int SpecificityScore;
        public int LearningScore;
        public int NextActionScore;
        public int ContinuityScore;
        public float ExpMultiplier;
        public string Feedback;
        public string ModelName = "local-rule-preview";
    }

    [Serializable]
    public sealed class DevSession
    {
        public string Id;
        public string UserId;
        public DateTime StartedAtUtc;
        public DateTime? EndedAtUtc;
        public int DurationMinutes;
        public string Goal;
        public int AchievementRate;
        public string Reflection;
        public string NextTask;
        public DevSessionStatus Status;
        public List<string> SuspiciousFlags = new();
        public string MentorComment;
        public string ApprovedBy;
        public DateTime? ApprovedAtUtc;
        public string AiEvaluationFailureReason;
        public AiEvaluation Evaluation;

        public int PreviewExp => DevelopmentExpCalculator.Calculate(DurationMinutes, Evaluation);
    }

    [Serializable]
    public sealed class DevelopmentTimeRankingEntry
    {
        public string Nickname;
        public int DurationMinutes;
        public int SessionCount;
    }

    [Serializable]
    public sealed class TeamDevelopmentTimeRankingEntry
    {
        public string TeamId;
        public string TeamName;
        public int DurationMinutes;
        public int SessionCount;
        public int MemberCount;
    }

    [Serializable]
    public sealed class BattleDamageRankingEntry
    {
        public string Nickname;
        public int Damage;
        public int ApprovedMinutes;
    }

    [Serializable]
    public sealed class BattleResultContributor
    {
        public string UserId;
        public string Nickname;
        public string TeamName;
        public int Damage;
        public int Heal;
        public int SupportCount;
        public int ApprovedMinutes;
        public int ContributionScore;
        public string HighlightContext;
        public int RewardExp;
        public bool IsMvp;
    }

    [Serializable]
    public sealed class BattleResultSummary
    {
        public BattleOutcome Outcome = BattleOutcome.Undecided;
        public bool IsVictory;
        public string ResultTitle;
        public string ResultMessage;
        public string RewardSummary;
        public int BossCurrentHp;
        public int BossMaxHp;
        public int TeamDamage;
        public int ParticipantCount;
        public List<BattleResultContributor> Contributors = new();
    }

    [Serializable]
    public sealed class BattlePartyStatus
    {
        public int ParticipantCount;
        public int AliveCount;
        public int CurrentHp;
        public int MaxHp;
        public int CurrentMp;
        public int MaxMp;
    }

    [Serializable]
    public sealed class BattleMemberActionOption
    {
        public BattleActionType ActionType;
        public string Label;
        public int MpCost;
        public bool IsAvailable;
    }

    [Serializable]
    public sealed class FrontDisplayHighlight
    {
        public string UserId;
        public string Nickname;
        public BattleRole Role;
        public int Damage;
        public int Heal;
        public int SupportCount;
        public int ApprovedMinutes;
        public int ContributionScore;
        public string HighlightContext;
        public bool IsTopHighlight;
    }

    [Serializable]
    public sealed class FrontDisplaySummary
    {
        public string BossName;
        public string PhaseLabel;
        public BattleOutcome Outcome = BattleOutcome.Undecided;
        public bool IsScheduled;
        public bool IsCompleted;
        public bool IsVictory;
        public int BossCurrentHp;
        public int BossMaxHp;
        public float BossHpRatio;
        public int TeamDamage;
        public int TurnNumber;
        public int TurnCount;
        public int ParticipantCount;
        public int MemberCount;
        public int WeeklyApprovedMinutes;
        public string ResultTitle;
        public string RewardSummary;
        public FrontDisplayHighlight TopHighlight;
        public List<FrontDisplayHighlight> Highlights = new();
    }

    [Serializable]
    public sealed class WeaponDefinition
    {
        public WeaponKind Kind;
        public string DisplayName;
        public string Description;
        public int MpEfficiencyBonus;
        public float DamageMultiplier = 1f;
        public BattleRole PreferredRole;
        public bool IsSpecial;
    }

    [Serializable]
    public sealed class BattleParticipant
    {
        public string UserId;
        public string Nickname;
        public CharacterStats Stats;
        public BattleRole Role;
        public WeaponKind Weapon;
        public int CurrentHp;
        public int CurrentMp;
        public int TotalDamage;
        public int TotalHeal;
        public int SupportCount;

        public bool IsAlive => CurrentHp > 0;
    }

    [Serializable]
    public sealed class MentorBoss
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
    public sealed class BattleActionResult
    {
        public string UserId;
        public string Nickname;
        public BattleRole Role;
        public WeaponKind Weapon;
        public BattleActionType ActionType;
        public int TurnNumber;
        public int MpCost;
        public int Damage;
        public int Heal;
        public string SupportEffect;
        public string Message;
    }

    [Serializable]
    public sealed class BossBattleState
    {
        public string Id;
        public MentorBoss Boss;
        public List<BattleParticipant> Participants = new();
        public DateTime WeekStartDateUtc;
        public int BaseHp;
        public float HpMultiplier = 1f;
        public BattleStatus Status = BattleStatus.Scheduled;
        public int TurnNumber = 1;
        public int TurnCount = 3;
        public BattlePhase Phase = BattlePhase.ActionSelect;
        public BattleOutcome Outcome = BattleOutcome.Undecided;
        public int TotalDamage;
        public string HighlightUserId;
        public string CreatedByUserId;
        public DateTime CreatedAtUtc;
        public DateTime? StartedAtUtc;
        public DateTime? CompletedAtUtc;
        public List<BattleActionResult> Actions = new();
        public bool IsActive => Status == BattleStatus.Active;
        public bool IsCompleted => Status == BattleStatus.Completed || Phase == BattlePhase.Completed || Outcome != BattleOutcome.Undecided || TurnNumber > TurnCount || Boss.CurrentHp <= 0;
    }

    [Serializable]
    public sealed class ProductEntry
    {
        public string Id;
        public string UserId;
        public string Title;
        public string Url;
        public string Description;
        public bool IsPublic = true;
        public string HiddenBy;
        public DateTime CreatedAtUtc;
    }

    [Serializable]
    public sealed class AchievementEntry
    {
        public string Id;
        public string UserId;
        public AchievementType Type;
        public string Title;
        public string Description;
        public AchievementStatus Status = AchievementStatus.Pending;
        public string ApprovedBy;
        public DateTime? ApprovedAtUtc;
        public DateTime CreatedAtUtc;
        public bool HasRewardWeapon;
        public WeaponKind RewardWeapon;
        public string RewardTitle;
        public string RewardSkill;
    }

    [Serializable]
    public sealed class AuditLogEntry
    {
        public string Id;
        public string ActorUserId;
        public string ActionType;
        public string TargetType;
        public string TargetId;
        public string Before;
        public string After;
        public DateTime CreatedAtUtc;
    }
}
