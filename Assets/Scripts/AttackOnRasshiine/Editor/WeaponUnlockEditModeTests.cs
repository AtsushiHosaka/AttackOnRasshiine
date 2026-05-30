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
            var stats = ResetStats(repository, member.Id, 4);

            repository.GetStats(member.Id);

            CollectionAssert.AreEqual(new[] { WeaponKind.Blade, WeaponKind.Rifle, WeaponKind.Shield, WeaponKind.Cannon }, stats.UnlockedWeapons);
            CollectionAssert.Contains(stats.Skills, "省MP射撃");
            CollectionAssert.Contains(stats.Skills, "チャージ砲撃");
            CollectionAssert.DoesNotContain(stats.UnlockedWeapons, WeaponKind.DebugTool);
            CollectionAssert.DoesNotContain(stats.UnlockedWeapons, WeaponKind.ReleaseGear);
        }

        [Test]
        public void SessionApprovalPersistsGrowthUnlocksAfterLevelUp()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var mentor = repository.Mentors[0];
            var stats = ResetStats(repository, member.Id, 1);
            var session = repository.StartSession(member.Id, "武器解放の実装と検証を進める");
            session.StartedAtUtc = DateTime.UtcNow.AddMinutes(-320);

            var completed = repository.CompleteSession(
                member.Id,
                100,
                "武器解放の実装と検証を行い、成長反映の改善点を整理した。",
                "次は解放状態のUI確認を続ける。");

            repository.ApproveSession(completed.Id, mentor.Id, "成長反映を確認しました。");

            Assert.GreaterOrEqual(stats.Level, 5);
            CollectionAssert.Contains(stats.UnlockedWeapons, WeaponKind.DebugTool);
            CollectionAssert.Contains(stats.Skills, "デバッグ支援");
        }

        [Test]
        public void LockedWeaponCannotBeSelectedForBattleAction()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            ResetStats(repository, member.Id, 1);
            repository.StartBattle();

            var options = repository.GetBattleActionOptions(member.Id, WeaponKind.Cannon);

            Assert.IsTrue(options.All(option => !option.IsAvailable));
            Assert.Throws<InvalidOperationException>(() =>
                repository.SubmitBattleAction(member.Id, BattleRole.Attacker, WeaponKind.Cannon, BattleActionType.Normal));
        }

        [Test]
        public void AchievementApprovalUnlocksSpecialWeaponBeforeGrowthThreshold()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var mentor = repository.Mentors[0];
            var stats = ResetStats(repository, member.Id, 1);
            var achievement = repository.SubmitAchievement(member.Id, AchievementType.Release, "初回リリース", "公開版を提出");

            repository.ApproveAchievement(achievement.Id, mentor.Id);

            CollectionAssert.Contains(stats.UnlockedWeapons, WeaponKind.ReleaseGear);
            CollectionAssert.Contains(stats.Skills, "リリースブースト");
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
    }
}
