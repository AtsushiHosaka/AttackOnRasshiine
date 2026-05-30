using System.Collections.Generic;
using System.Linq;
using AttackOnRasshiine.Runtime.Data;

namespace AttackOnRasshiine.Runtime.Battle
{
    public enum BattlePresentationMode
    {
        Member,
        FrontDisplay
    }

    public sealed class BattleStatePresentation
    {
        public BattlePresentationMode Mode;
        public bool RequiresLogin;
        public bool IsReadOnly;
        public string BossName;
        public string PhaseLabel;
        public int BossCurrentHp;
        public int BossMaxHp;
        public float BossHpRatio;
        public int TurnNumber;
        public int TurnCount;
        public int TeamDamage;
        public string PrimaryHighlight;
        public IReadOnlyList<string> ActionLabels = new List<string>();
    }

    public static class BattleStatePresenter
    {
        public static BattleStatePresentation ForMember(
            BossBattleState battle,
            BattleParticipant participant,
            IReadOnlyList<BattleMemberActionOption> actionOptions)
        {
            return new BattleStatePresentation
            {
                Mode = BattlePresentationMode.Member,
                RequiresLogin = true,
                IsReadOnly = false,
                BossName = battle?.Boss?.Name ?? "NO BATTLE",
                PhaseLabel = battle == null ? "NO DATA" : battle.Status.ToString(),
                BossCurrentHp = battle?.Boss?.CurrentHp ?? 0,
                BossMaxHp = battle?.Boss?.MaxHp ?? 0,
                BossHpRatio = Ratio(battle?.Boss?.CurrentHp ?? 0, battle?.Boss?.MaxHp ?? 0),
                TurnNumber = battle?.TurnNumber ?? 0,
                TurnCount = battle?.TurnCount ?? 0,
                TeamDamage = battle?.TotalDamage ?? 0,
                PrimaryHighlight = participant == null ? "参加者未選択" : $"{participant.Nickname} HP {participant.CurrentHp} MP {participant.CurrentMp}",
                ActionLabels = (actionOptions ?? new List<BattleMemberActionOption>())
                    .Select(option => option.Label)
                    .ToList()
            };
        }

        public static BattleStatePresentation ForFrontDisplay(FrontDisplaySummary summary)
        {
            return new BattleStatePresentation
            {
                Mode = BattlePresentationMode.FrontDisplay,
                RequiresLogin = false,
                IsReadOnly = true,
                BossName = summary?.BossName ?? "NO BATTLE",
                PhaseLabel = summary?.PhaseLabel ?? "NO DATA",
                BossCurrentHp = summary?.BossCurrentHp ?? 0,
                BossMaxHp = summary?.BossMaxHp ?? 0,
                BossHpRatio = summary?.BossHpRatio ?? 0f,
                TurnNumber = summary?.TurnNumber ?? 0,
                TurnCount = summary?.TurnCount ?? 0,
                TeamDamage = summary?.TeamDamage ?? 0,
                PrimaryHighlight = summary?.TopHighlight == null
                    ? "ハイライト待ち"
                    : $"{summary.TopHighlight.Nickname} / {summary.TopHighlight.HighlightContext}",
                ActionLabels = new List<string>()
            };
        }

        private static float Ratio(int current, int max)
        {
            return max <= 0 ? 0f : UnityEngine.Mathf.Clamp01(current / (float)max);
        }
    }
}
