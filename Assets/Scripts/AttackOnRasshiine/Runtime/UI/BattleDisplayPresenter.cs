using System.Collections.Generic;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;

namespace AttackOnRasshiine.Runtime.UI
{
    public sealed class BattleDisplayPresenter
    {
        public BattleSceneState BuildBattle(LocalGameRepository repository, UserProfile user, WeaponKind selectedWeapon, bool apiConfigured, bool isBusy)
        {
            var battle = repository.ActiveBattle;
            var participant = user == null ? null : repository.GetParticipant(user.Id);
            var state = new BattleSceneState
            {
                Battle = battle,
                PartyStatus = repository.GetBattlePartyStatus(),
                Participant = participant,
                ResultSummary = battle.IsCompleted ? repository.GetBattleResultSummary() : null,
                SyncModeLabel = apiConfigured ? "API同期" : "デモ同期",
                IsSyncing = isBusy,
                CanStartBattle = user?.Role == UserRole.Mentor && battle.Status == BattleStatus.Scheduled,
                CanSubmitAction = user?.Role == UserRole.Member && battle.IsActive && participant != null
            };

            if (state.CanSubmitAction)
            {
                foreach (var option in repository.GetBattleActionOptions(user.Id, selectedWeapon))
                {
                    state.ActionOptions.Add(option);
                }
            }

            return state;
        }

        public FrontDisplaySceneState BuildFrontDisplay(LocalGameRepository repository, bool apiConfigured, bool isPolling, bool displayOnly)
        {
            var summary = repository.GetFrontDisplaySummary();
            return new FrontDisplaySceneState
            {
                Summary = summary,
                SyncModeLabel = apiConfigured ? "API表示同期" : "デモ表示",
                IsPolling = isPolling,
                IsDisplayOnly = displayOnly,
                LastUpdatedActionCount = repository.ActiveBattle?.Actions?.Count ?? 0
            };
        }
    }

    public sealed class BattleSceneState
    {
        public BossBattleState Battle;
        public BattlePartyStatus PartyStatus;
        public BattleParticipant Participant;
        public BattleResultSummary ResultSummary;
        public string SyncModeLabel;
        public bool IsSyncing;
        public bool CanStartBattle;
        public bool CanSubmitAction;
        public List<BattleMemberActionOption> ActionOptions = new();
    }

    public sealed class FrontDisplaySceneState
    {
        public FrontDisplaySummary Summary;
        public string SyncModeLabel;
        public bool IsPolling;
        public bool IsDisplayOnly;
        public int LastUpdatedActionCount;
    }
}
