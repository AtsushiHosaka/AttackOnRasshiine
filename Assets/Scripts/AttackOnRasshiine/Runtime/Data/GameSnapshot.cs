using System.Collections.Generic;

namespace AttackOnRasshiine.Runtime.Data
{
    public sealed class GameSnapshot
    {
        public List<UserProfile> Users = new();
        public List<CharacterStatsRecord> Stats = new();
        public List<WeaponDefinition> Weapons = new();
        public List<DevSession> Sessions = new();
        public BossBattleState ActiveBattle;
    }

    public sealed class CharacterStatsRecord
    {
        public string UserId;
        public CharacterStats Stats;
    }
}
