using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class WeaponUnlockEditModeTests
    {
        [Test]
        public void ApprovedSessionGrowthUnlocksWeaponAndSkill()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var mentor = repository.Mentors[0];
            var stats = repository.GetStats(member.Id);
            stats.Level = 2;
            stats.Exp = stats.ExpToNextLevel - 1;
            stats.RecalculateDerivedStats();
            stats.UnlockedWeapons.Clear();
            stats.Skills.Clear();
            repository.StartSession(member.Id, "武器解放を確認する");
            var session = repository.CompleteSession(member.Id, 90, "成長で武器が増えることを検証した", "次の武器解放を見る");

            repository.ApproveSession(session.Id, mentor.Id, "成長反映を確認");

            Assert.GreaterOrEqual(stats.Level, 3);
            CollectionAssert.Contains(stats.UnlockedWeapons, WeaponKind.Cannon);
            CollectionAssert.Contains(stats.Skills, "キャノン制御");
            Assert.AreEqual(1, stats.UnlockedWeapons.Count(weapon => weapon == WeaponKind.Cannon));
        }

        [Test]
        public void SnapshotAppliesGrowthUnlocksForPersistedStats()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var stats = new CharacterStats { Level = 5 };
            stats.RecalculateDerivedStats();

            repository.ApplySnapshot(new GameSnapshot
            {
                Users = { member },
                Stats = { new CharacterStatsRecord { UserId = member.Id, Stats = stats } }
            });

            CollectionAssert.Contains(stats.UnlockedWeapons, WeaponKind.Blade);
            CollectionAssert.Contains(stats.UnlockedWeapons, WeaponKind.Rifle);
            CollectionAssert.Contains(stats.UnlockedWeapons, WeaponKind.Shield);
            CollectionAssert.Contains(stats.UnlockedWeapons, WeaponKind.Cannon);
            CollectionAssert.Contains(stats.UnlockedWeapons, WeaponKind.DebugTool);
            CollectionAssert.Contains(stats.Skills, "キャノン制御");
            CollectionAssert.Contains(stats.Skills, "デバッグブレイク");
            CollectionAssert.DoesNotContain(stats.UnlockedWeapons, WeaponKind.ReleaseGear);
            CollectionAssert.DoesNotContain(stats.UnlockedWeapons, WeaponKind.ContestGear);
        }

        [Test]
        public void AvailableBattleWeaponsUseGrowthAndAchievementUnlocks()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var mentor = repository.Mentors[0];
            var stats = repository.GetStats(member.Id);
            stats.Level = 1;
            stats.Exp = 0;
            stats.RecalculateDerivedStats();
            stats.UnlockedWeapons.Clear();
            stats.Skills.Clear();

            var initialWeapons = repository.GetAvailableBattleWeapons(member.Id).ToList();

            CollectionAssert.AreEqual(new[] { WeaponKind.Blade, WeaponKind.Rifle, WeaponKind.Shield }, initialWeapons);
            CollectionAssert.DoesNotContain(initialWeapons, WeaponKind.Cannon);
            CollectionAssert.DoesNotContain(initialWeapons, WeaponKind.DebugTool);
            CollectionAssert.DoesNotContain(initialWeapons, WeaponKind.ReleaseGear);
            var achievement = repository.SubmitAchievement(member.Id, AchievementType.Release, "初回リリース", "公開URLを提出");

            repository.ApproveAchievement(achievement.Id, mentor.Id);
            var afterAchievement = repository.GetAvailableBattleWeapons(member.Id).ToList();

            CollectionAssert.Contains(afterAchievement, WeaponKind.ReleaseGear);
            CollectionAssert.DoesNotContain(afterAchievement, WeaponKind.ContestGear);
        }

        [Test]
        public void LockedBattleWeaponFallsBackToBlade()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var stats = repository.GetStats(member.Id);
            stats.Level = 1;
            stats.Exp = 0;
            stats.RecalculateDerivedStats();
            stats.UnlockedWeapons.Clear();
            stats.Skills.Clear();
            repository.StartBattle();

            var result = repository.SubmitBattleAction(member.Id, BattleRole.Attacker, WeaponKind.ReleaseGear, BattleActionType.Normal);

            Assert.AreEqual(WeaponKind.Blade, result.Weapon);
            Assert.AreEqual(WeaponKind.Blade, repository.GetParticipant(member.Id).Weapon);
        }
    }
}
