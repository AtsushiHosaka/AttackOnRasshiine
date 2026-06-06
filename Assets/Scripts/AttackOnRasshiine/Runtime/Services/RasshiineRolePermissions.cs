using AttackOnRasshiine.Runtime.Data;

namespace AttackOnRasshiine.Runtime.Services
{
    public enum RasshiinePermissionOperation
    {
        Login,
        CreateAccount,
        CreateOwnDevLog,
        EditOwnDevLog,
        ViewAllDevLogs,
        ReviewDevLog,
        SubmitAchievement,
        ReviewAchievement,
        AdjustBossHp,
        ParticipateBattle,
        ViewFrontDisplay,
        ViewRanking,
        RegisterProductUrl,
        HideProductUrl
    }

    public static class RasshiineRolePermissions
    {
        public static bool IsAllowed(UserRole? role, RasshiinePermissionOperation operation)
        {
            if (!role.HasValue)
            {
                return false;
            }

            return role.Value switch
            {
                UserRole.Mentor => operation is
                    RasshiinePermissionOperation.Login or
                    RasshiinePermissionOperation.CreateAccount or
                    RasshiinePermissionOperation.EditOwnDevLog or
                    RasshiinePermissionOperation.ViewAllDevLogs or
                    RasshiinePermissionOperation.ReviewDevLog or
                    RasshiinePermissionOperation.ReviewAchievement or
                    RasshiinePermissionOperation.AdjustBossHp or
                    RasshiinePermissionOperation.ParticipateBattle or
                    RasshiinePermissionOperation.ViewFrontDisplay or
                    RasshiinePermissionOperation.ViewRanking or
                    RasshiinePermissionOperation.HideProductUrl,
                UserRole.Member => operation is
                    RasshiinePermissionOperation.Login or
                    RasshiinePermissionOperation.CreateOwnDevLog or
                    RasshiinePermissionOperation.EditOwnDevLog or
                    RasshiinePermissionOperation.SubmitAchievement or
                    RasshiinePermissionOperation.ParticipateBattle or
                    RasshiinePermissionOperation.ViewFrontDisplay or
                    RasshiinePermissionOperation.ViewRanking or
                    RasshiinePermissionOperation.RegisterProductUrl,
                _ => false
            };
        }
    }
}
