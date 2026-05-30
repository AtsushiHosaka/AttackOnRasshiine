using System;
using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class WeaponUnlockEditModeTests
    {
        [Test]
        public void GrowthLevelUnlocksWeaponsAndSkillsIntoStats()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            ResetStats(repository, member.Id, 4);

            var refreshed = repository.GetStats(member.Id);

            CollectionAssert.AreEqual(new[] { WeaponKind.Blade, WeaponKind.Rifle, WeaponKind.Shield, WeaponKind.Cannon }, refreshed.UnlockedWeapons);
            CollectionAssert.Contains(refreshed.Skills, "省MP射撃");
            CollectionAssert.Contains(refreshed.Skills, "ガード支援");
            CollectionAssert.Contains(refreshed.Skills, "チャージ砲撃");
            CollectionAssert.DoesNotContain(refreshed.UnlockedWeapons, WeaponKind.DebugTool);
            CollectionAssert.DoesNotContain(refreshed.UnlockedWeapons, WeaponKind.ReleaseGear);
            CollectionAssert.DoesNotContain(refreshed.UnlockedWeapons, WeaponKind.ContestGear);
        }

        [Test]
        public void ApprovedSessionGrowthUnlocksWeaponAndSkillAfterLevelUp()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var mentor = repository.Mentors[0];
            var stats = ResetStats(repository, member.Id, 1);
            var session = repository.StartSession(member.Id, "武器解放の実装と検証を進める");
            var completed = repository.CompleteSession(
                member.Id,
                100,
                "武器解放の実装と検証を行い、成長反映の改善点を整理した。",
                "次は解放状態のUI確認を続ける。");
            completed.DurationMinutes = GetMinutesNeededToReachLevel(stats, 5, 1.6f);
            completed.Evaluation = new AiEvaluation
            {
                Rank = AiRank.A,
                TotalScore = 82,
                ExpMultiplier = 1.6f,
                Feedback = "武器解放テスト"
            };

            repository.ApproveSession(completed.Id, mentor.Id, "成長反映を確認しました。");

            Assert.GreaterOrEqual(stats.Level, 5);
            CollectionAssert.Contains(stats.UnlockedWeapons, WeaponKind.DebugTool);
            CollectionAssert.Contains(stats.Skills, "デバッグ支援");
            Assert.AreEqual(1, stats.UnlockedWeapons.Count(weapon => weapon == WeaponKind.DebugTool));
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
            CollectionAssert.Contains(stats.Skills, "チャージ砲撃");
            CollectionAssert.Contains(stats.Skills, "デバッグ支援");
            CollectionAssert.DoesNotContain(stats.UnlockedWeapons, WeaponKind.ReleaseGear);
            CollectionAssert.DoesNotContain(stats.UnlockedWeapons, WeaponKind.ContestGear);
        }

        [Test]
        public void AvailableBattleWeaponsUseGrowthAndAchievementUnlocks()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var mentor = repository.Mentors[0];
            var stats = ResetStats(repository, member.Id, 1);

            var initialWeapons = repository.GetAvailableBattleWeapons(member.Id).ToList();

            CollectionAssert.AreEqual(new[] { WeaponKind.Blade }, initialWeapons);
            CollectionAssert.DoesNotContain(initialWeapons, WeaponKind.Rifle);
            CollectionAssert.DoesNotContain(initialWeapons, WeaponKind.Cannon);
            CollectionAssert.DoesNotContain(initialWeapons, WeaponKind.DebugTool);
            CollectionAssert.DoesNotContain(initialWeapons, WeaponKind.ReleaseGear);
            var achievement = repository.SubmitAchievement(member.Id, AchievementType.Release, "初回リリース", "公開URLを提出");

            repository.ApproveAchievement(achievement.Id, mentor.Id);
            var afterAchievement = repository.GetAvailableBattleWeapons(member.Id).ToList();

            CollectionAssert.Contains(afterAchievement, WeaponKind.ReleaseGear);
            CollectionAssert.Contains(stats.Skills, "リリースブースト");
            CollectionAssert.DoesNotContain(afterAchievement, WeaponKind.ContestGear);
        }

        [Test]
        public void LockedBattleWeaponOptionsAreUnavailableAndSubmitFallsBack()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            ResetStats(repository, member.Id, 1);
            repository.StartBattle();

            var options = repository.GetBattleActionOptions(member.Id, WeaponKind.Cannon);
            var result = repository.SubmitBattleAction(member.Id, BattleRole.Attacker, WeaponKind.Cannon, BattleActionType.Normal);

            Assert.IsTrue(options.All(option => !option.IsAvailable));
            Assert.AreEqual(WeaponKind.Blade, result.Weapon);
            Assert.AreEqual(WeaponKind.Blade, repository.GetParticipant(member.Id).Weapon);
        }

        private static CharacterStats ResetStats(LocalGameRepository repository, string userId, int level)
        {
            var stats = repository.GetStats(userId);
            stats.Level = level;
            stats.Exp = 0;
            stats.UnlockedWeapons.Clear();
            stats.Titles.Clear();
            stats.Skills.Clear();
            stats.RecalculateDerivedStats();
            return stats;
        }

        private static int GetMinutesNeededToReachLevel(CharacterStats stats, int targetLevel, float expMultiplier)
        {
            var level = stats.Level;
            var exp = stats.Exp;
            var neededExp = 0;
            while (level < targetLevel)
            {
                var expToNextLevel = 50 + level * 25;
                neededExp += expToNextLevel - exp;
                exp = 0;
                level += 1;
            }

            return (int)Math.Ceiling(neededExp / expMultiplier);
        }
    }
}
