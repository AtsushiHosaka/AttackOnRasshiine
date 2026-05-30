namespace AttackOnRasshiine.Runtime.Data
{
    public enum UserRole
    {
        Member,
        Mentor
    }

    public enum DevSessionStatus
    {
        InProgress,
        Pending,
        Approved,
        Rejected,
        Incomplete,
        NeedsReview,
        AiPending
    }

    public enum AiRank
    {
        S,
        APlus,
        A,
        B,
        C,
        D
    }

    public enum BattleRole
    {
        Attacker,
        Healer,
        Defender,
        Supporter
    }

    public enum WeaponKind
    {
        Blade,
        Rifle,
        Cannon,
        Shield,
        DebugTool,
        ReleaseGear,
        ContestGear
    }

    public enum AchievementType
    {
        ContestSubmission,
        Release,
        Update,
        Award,
        ContinuousDev
    }

    public enum AchievementStatus
    {
        Pending,
        Approved,
        Rejected
    }

    public enum BattleActionType
    {
        Normal,
        Strong,
        FullPower,
        Support,
        Guard
    }

    public enum BattleStatus
    {
        Scheduled,
        Active,
        Completed
    }

    public enum BattlePhase
    {
        TurnStart,
        ActionSelect,
        Resolving,
        Result,
        Completed
    }
}
