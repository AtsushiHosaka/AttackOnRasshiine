using UnityEngine;

namespace AttackOnRasshiine.Runtime.Data
{
    public static class BattleDamageCalculator
    {
        public static int Calculate(CharacterStats stats, WeaponDefinition weapon, MentorBoss boss, BattleRole role, BattleActionType actionType)
        {
            var attack = Mathf.Max(0, stats?.Atk ?? 0);
            var weaponMultiplier = Mathf.Max(0f, weapon?.DamageMultiplier ?? 1f);
            var defense = Mathf.Max(0, boss?.Def ?? 0);
            return Calculate(attack, weaponMultiplier, defense, role, actionType);
        }

        public static int Calculate(int attack, float weaponMultiplier, int defense, BattleRole role, BattleActionType actionType)
        {
            var rawDamage = Mathf.Max(0, attack) *
                GetRoleMultiplier(role) *
                GetActionMultiplier(actionType) *
                Mathf.Max(0f, weaponMultiplier) -
                Mathf.Max(0, defense);
            return Mathf.Max(0, Mathf.RoundToInt(rawDamage));
        }

        public static float GetRoleMultiplier(BattleRole role)
        {
            return role switch
            {
                BattleRole.Attacker => 1.25f,
                BattleRole.Supporter => 0.85f,
                BattleRole.Healer => 0.75f,
                BattleRole.Defender => 0.75f,
                _ => 1.0f
            };
        }

        public static float GetActionMultiplier(BattleActionType actionType)
        {
            return actionType switch
            {
                BattleActionType.Strong => 1.8f,
                BattleActionType.FullPower => 3.0f,
                BattleActionType.Support => 0.4f,
                BattleActionType.Guard => 0.2f,
                _ => 1.0f
            };
        }
    }
}
