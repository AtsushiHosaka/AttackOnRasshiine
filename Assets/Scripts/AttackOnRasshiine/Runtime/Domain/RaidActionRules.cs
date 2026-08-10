using System;
using System.Collections.Generic;
using System.Linq;
using AttackOnRasshiine.Runtime.Data;

namespace AttackOnRasshiine.Runtime.Domain
{
    public enum RaidActionValidationFailure
    {
        None,
        MissingBattle,
        BattleNotActive,
        BattleCompleted,
        InvalidTurn,
        InvalidAction,
        InvalidRole,
        ParticipantNotFound,
        ParticipantDefeated,
        ParticipantStatsMissing,
        WeaponLocked,
        InvalidMpCost,
        InsufficientMp,
        ActionAlreadySubmitted
    }

    public sealed class RaidActionValidationContext
    {
        public BossBattleState Battle { get; set; }
        public string UserId { get; set; }
        public BattleRole Role { get; set; }
        public WeaponKind Weapon { get; set; }
        public BattleActionType ActionType { get; set; }
        public int MpCost { get; set; }
    }

    public sealed class RaidActionValidationResult
    {
        private RaidActionValidationResult(RaidActionValidationFailure failure, string message)
        {
            Failure = failure;
            Message = message;
        }

        public RaidActionValidationFailure Failure { get; }
        public string Message { get; }
        public bool IsValid => Failure == RaidActionValidationFailure.None;

        public static RaidActionValidationResult Valid()
        {
            return new RaidActionValidationResult(RaidActionValidationFailure.None, string.Empty);
        }

        public static RaidActionValidationResult Invalid(RaidActionValidationFailure failure, string message)
        {
            return new RaidActionValidationResult(failure, message);
        }
    }

    /// <summary>
    /// Side-effect-free validation shared by local play and the eventual authoritative API.
    /// </summary>
    public static class RaidActionValidator
    {
        public static RaidActionValidationResult Validate(RaidActionValidationContext context)
        {
            if (context?.Battle == null)
            {
                return RaidActionValidationResult.Invalid(RaidActionValidationFailure.MissingBattle, "レイドが見つかりません。");
            }

            var battle = context.Battle;
            if (battle.Status == BattleStatus.Completed ||
                battle.Phase == BattlePhase.Completed ||
                battle.Outcome != BattleOutcome.Undecided ||
                battle.Boss == null ||
                battle.Boss.CurrentHp <= 0)
            {
                return RaidActionValidationResult.Invalid(RaidActionValidationFailure.BattleCompleted, "レイドは終了しています。");
            }

            if (battle.Status != BattleStatus.Active)
            {
                return RaidActionValidationResult.Invalid(RaidActionValidationFailure.BattleNotActive, "レイドは開始されていません。");
            }

            if (battle.TurnNumber < 1 || battle.TurnCount < 1 || battle.TurnNumber > battle.TurnCount)
            {
                return RaidActionValidationResult.Invalid(RaidActionValidationFailure.InvalidTurn, "有効なターンではありません。");
            }

            if (!Enum.IsDefined(typeof(BattleActionType), context.ActionType))
            {
                return RaidActionValidationResult.Invalid(RaidActionValidationFailure.InvalidAction, "行動が不正です。");
            }

            if (!Enum.IsDefined(typeof(BattleRole), context.Role))
            {
                return RaidActionValidationResult.Invalid(RaidActionValidationFailure.InvalidRole, "ロールが不正です。");
            }

            var participant = battle.Participants?.FirstOrDefault(item => string.Equals(item.UserId, context.UserId, StringComparison.Ordinal));
            if (participant == null)
            {
                return RaidActionValidationResult.Invalid(RaidActionValidationFailure.ParticipantNotFound, "参加者が見つかりません。");
            }

            if (!participant.IsAlive)
            {
                return RaidActionValidationResult.Invalid(RaidActionValidationFailure.ParticipantDefeated, "戦闘不能のため行動できません。");
            }

            if (participant.Stats == null)
            {
                return RaidActionValidationResult.Invalid(RaidActionValidationFailure.ParticipantStatsMissing, "キャラクター情報がありません。");
            }

            var actions = battle.Actions ?? new List<BattleActionResult>();
            if (actions.Any(action => action != null &&
                                      action.TurnNumber == battle.TurnNumber &&
                                      string.Equals(action.UserId, context.UserId, StringComparison.Ordinal)))
            {
                return RaidActionValidationResult.Invalid(RaidActionValidationFailure.ActionAlreadySubmitted, "このターンの行動は送信済みです。");
            }

            var unlockedWeapons = participant.Stats.UnlockedWeapons ?? new List<WeaponKind>();
            if (!unlockedWeapons.Contains(context.Weapon))
            {
                return RaidActionValidationResult.Invalid(RaidActionValidationFailure.WeaponLocked, "未解放の武器です。");
            }

            if (context.MpCost < 0)
            {
                return RaidActionValidationResult.Invalid(RaidActionValidationFailure.InvalidMpCost, "MP消費量が不正です。");
            }

            if (participant.CurrentMp < context.MpCost)
            {
                return RaidActionValidationResult.Invalid(RaidActionValidationFailure.InsufficientMp, "MPが不足しています。");
            }

            return RaidActionValidationResult.Valid();
        }
    }
}
