using System;
using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class AchievementApprovalEditModeTests
    {
        [Test]
        public void MemberCanSubmitAchievementAsPending()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];

            var achievement = repository.SubmitAchievement(member.Id, AchievementType.ContestSubmission, "U-22提出", "提出URLを添えて申請");

            Assert.AreEqual(member.Id, achievement.UserId);
            Assert.AreEqual(AchievementType.ContestSubmission, achievement.Type);
            Assert.AreEqual("U-22提出", achievement.Title);
            Assert.AreEqual("提出URLを添えて申請", achievement.Description);
            Assert.AreEqual(AchievementStatus.Pending, achievement.Status);
            CollectionAssert.Contains(repository.GetPendingAchievements().Select(item => item.Id), achievement.Id);
            CollectionAssert.Contains(repository.GetAchievementsForUser(member.Id).Select(item => item.Id), achievement.Id);
        }

        [Test]
        public void MentorApprovalUnlocksRewardAndWritesAuditLog()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var mentor = repository.Mentors[0];
            var achievement = repository.SubmitAchievement(member.Id, AchievementType.Release, "初回リリース", "公開版を提出");

            var approved = repository.ApproveAchievement(achievement.Id, mentor.Id);

            Assert.AreEqual(AchievementStatus.Approved, approved.Status);
            Assert.AreEqual(mentor.Id, approved.ApprovedBy);
            Assert.NotNull(approved.ApprovedAtUtc);
            Assert.IsTrue(approved.HasRewardWeapon);
            Assert.AreEqual(WeaponKind.ReleaseGear, approved.RewardWeapon);
            CollectionAssert.Contains(repository.GetStats(member.Id).UnlockedWeapons, WeaponKind.ReleaseGear);
            CollectionAssert.Contains(repository.GetStats(member.Id).Titles, "リリース職人");
            CollectionAssert.Contains(repository.GetStats(member.Id).Skills, "リリースブースト");

            var auditLog = repository.GetAuditLogsForTarget("achievement", achievement.Id).Single();
            Assert.AreEqual("achievement.approve", auditLog.ActionType);
            Assert.AreEqual(mentor.Id, auditLog.ActorUserId);
            StringAssert.Contains("status=Pending", auditLog.Before);
            StringAssert.Contains("status=Approved", auditLog.After);
        }

        [Test]
        public void RepeatedApprovalDoesNotDuplicateRewardsOrAuditLogs()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var mentor = repository.Mentors[0];
            var achievement = repository.SubmitAchievement(member.Id, AchievementType.Award, "入賞", "コンテスト受賞");

            repository.ApproveAchievement(achievement.Id, mentor.Id);
            repository.ApproveAchievement(achievement.Id, mentor.Id);

            var stats = repository.GetStats(member.Id);
            Assert.AreEqual(1, stats.UnlockedWeapons.Count(weapon => weapon == WeaponKind.ContestGear));
            Assert.AreEqual(1, stats.Titles.Count(title => title == "受賞者"));
            Assert.AreEqual(1, repository.GetAuditLogsForTarget("achievement", achievement.Id).Count);
        }

        [Test]
        public void MentorRejectionWritesAuditLogWithoutReward()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var mentor = repository.Mentors[0];
            var achievement = repository.SubmitAchievement(member.Id, AchievementType.Update, "更新申請", "確認待ち");

            var rejected = repository.RejectAchievement(achievement.Id, mentor.Id);

            Assert.AreEqual(AchievementStatus.Rejected, rejected.Status);
            Assert.IsFalse(rejected.HasRewardWeapon);
            CollectionAssert.DoesNotContain(repository.GetStats(member.Id).UnlockedWeapons, WeaponKind.ReleaseGear);
            var auditLog = repository.GetAuditLogsForTarget("achievement", achievement.Id).Single();
            Assert.AreEqual("achievement.reject", auditLog.ActionType);
            StringAssert.Contains("status=Rejected", auditLog.After);
        }

        [Test]
        public void MemberCannotApproveAchievement()
        {
            var repository = new LocalGameRepository();
            var owner = repository.Members[0];
            var otherMember = repository.Members[1];
            var achievement = repository.SubmitAchievement(owner.Id, AchievementType.ContinuousDev, "30日継続", "継続開発");

            Assert.Throws<InvalidOperationException>(() => repository.ApproveAchievement(achievement.Id, otherMember.Id));

            Assert.AreEqual(AchievementStatus.Pending, achievement.Status);
            Assert.AreEqual(0, repository.GetAuditLogsForTarget("achievement", achievement.Id).Count);
        }
    }
}
