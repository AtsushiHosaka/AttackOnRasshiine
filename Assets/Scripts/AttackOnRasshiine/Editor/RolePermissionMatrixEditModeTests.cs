using System;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class RolePermissionMatrixEditModeTests
    {
        [Test]
        public void PermissionMatrixMatchesSpecification()
        {
            AssertAllowed(UserRole.Mentor,
                RasshiinePermissionOperation.Login,
                RasshiinePermissionOperation.CreateAccount,
                RasshiinePermissionOperation.EditOwnDevLog,
                RasshiinePermissionOperation.ViewAllDevLogs,
                RasshiinePermissionOperation.ReviewDevLog,
                RasshiinePermissionOperation.ReviewAchievement,
                RasshiinePermissionOperation.AdjustBossHp,
                RasshiinePermissionOperation.ParticipateBattle,
                RasshiinePermissionOperation.ViewFrontDisplay,
                RasshiinePermissionOperation.ViewRanking,
                RasshiinePermissionOperation.HideProductUrl);

            AssertDenied(UserRole.Mentor,
                RasshiinePermissionOperation.CreateOwnDevLog,
                RasshiinePermissionOperation.SubmitAchievement,
                RasshiinePermissionOperation.RegisterProductUrl);

            AssertAllowed(UserRole.Member,
                RasshiinePermissionOperation.Login,
                RasshiinePermissionOperation.CreateOwnDevLog,
                RasshiinePermissionOperation.EditOwnDevLog,
                RasshiinePermissionOperation.SubmitAchievement,
                RasshiinePermissionOperation.ParticipateBattle,
                RasshiinePermissionOperation.ViewFrontDisplay,
                RasshiinePermissionOperation.ViewRanking,
                RasshiinePermissionOperation.RegisterProductUrl);

            AssertDenied(UserRole.Member,
                RasshiinePermissionOperation.CreateAccount,
                RasshiinePermissionOperation.ViewAllDevLogs,
                RasshiinePermissionOperation.ReviewDevLog,
                RasshiinePermissionOperation.ReviewAchievement,
                RasshiinePermissionOperation.AdjustBossHp,
                RasshiinePermissionOperation.HideProductUrl);

            foreach (RasshiinePermissionOperation operation in Enum.GetValues(typeof(RasshiinePermissionOperation)))
            {
                Assert.IsFalse(RasshiineRolePermissions.IsAllowed(null, operation), $"{operation} should reject guests.");
            }
        }

        [Test]
        public void ProtectedOperationsRejectDisallowedRolesAndGuests()
        {
            var repository = new LocalGameRepository();
            Assert.That(repository.Mentors.Count, Is.GreaterThanOrEqualTo(1), "Test requires at least 1 mentor.");
            Assert.That(repository.Members.Count, Is.GreaterThanOrEqualTo(2), "Test requires at least 2 members.");
            var mentor = repository.Mentors[0];
            var member = repository.Members[0];
            var otherMember = repository.Members[1];
            const string GuestUserId = "guest-user";

            Assert.Throws<InvalidOperationException>(() => repository.CreateMemberAccount(member.Id, "member.blocked", "権限なし", "blue"));
            Assert.Throws<InvalidOperationException>(() => repository.CreateMemberAccount(GuestUserId, "guest.blocked", "ゲスト", "blue"));
            Assert.Throws<InvalidOperationException>(() => repository.IssueTemporaryPassword(member.Id, otherMember.Id));
            Assert.Throws<InvalidOperationException>(() => repository.IssueTemporaryPassword(GuestUserId, otherMember.Id));

            Assert.Throws<InvalidOperationException>(() => repository.StartSession(mentor.Id, "メンターは開始できない"));
            Assert.Throws<InvalidOperationException>(() => repository.StartSession(GuestUserId, "ゲストは開始できない"));

            var memberSession = repository.StartSession(member.Id, "権限テスト");
            Assert.AreEqual(member.Id, memberSession.UserId);
            Assert.Throws<InvalidOperationException>(() => repository.CompleteSession(GuestUserId, 80, "保存", "次へ"));
            var pendingSession = repository.CompleteSession(member.Id, 80, "保存した", "次へ");
            Assert.Throws<InvalidOperationException>(() => repository.ApproveSession(pendingSession.Id, member.Id, "本人承認"));
            Assert.Throws<InvalidOperationException>(() => repository.RejectSession(pendingSession.Id, GuestUserId, "ゲスト却下"));

            Assert.Throws<InvalidOperationException>(() => repository.RegisterProduct(mentor.Id, "Mentor Product", "https://example.com/mentor", "mentor"));
            Assert.Throws<InvalidOperationException>(() => repository.RegisterProduct(GuestUserId, "Guest Product", "https://example.com/guest", "guest"));
            var product = repository.RegisterProduct(member.Id, "Member Product", "https://example.com/member", "member");
            Assert.Throws<InvalidOperationException>(() => repository.HideProduct(product.Id, member.Id));
            Assert.Throws<InvalidOperationException>(() => repository.HideProduct(product.Id, GuestUserId));

            Assert.Throws<InvalidOperationException>(() => repository.SubmitAchievement(mentor.Id, AchievementType.Update, "更新", "mentor"));
            Assert.Throws<InvalidOperationException>(() => repository.SubmitAchievement(GuestUserId, AchievementType.Update, "更新", "guest"));
            var achievement = repository.SubmitAchievement(member.Id, AchievementType.Update, "更新", "member");
            Assert.Throws<InvalidOperationException>(() => repository.ApproveAchievement(achievement.Id, member.Id));
            Assert.Throws<InvalidOperationException>(() => repository.RejectAchievement(achievement.Id, GuestUserId));

            Assert.Throws<InvalidOperationException>(() => repository.StartBattle(member.Id));
            Assert.Throws<InvalidOperationException>(() => repository.StartBattle(GuestUserId));
            Assert.Throws<InvalidOperationException>(() => repository.ResetBattle(member.Id));
            Assert.Throws<InvalidOperationException>(() => repository.SetBossHpMultiplier(member.Id, 2f));

            var waiting = repository.SubmitBattleAction(mentor.Id, BattleRole.Attacker, WeaponKind.Blade, BattleActionType.Normal);
            Assert.IsNotEmpty(waiting.Message);
            StringAssert.Contains("待機中", waiting.Message);
            Assert.Throws<InvalidOperationException>(() => repository.SubmitBattleAction(GuestUserId, BattleRole.Attacker, WeaponKind.Blade, BattleActionType.Normal));

            repository.HideProduct(product.Id, mentor.Id);
            repository.SetBossHpMultiplier(mentor.Id, 2f);
            repository.StartBattle(mentor.Id);

            Assert.IsFalse(product.IsPublic);
            Assert.AreEqual(mentor.Id, product.HiddenBy);
            Assert.AreEqual(BattleStatus.Active, repository.ActiveBattle.Status);
            Assert.AreEqual(2f, repository.ActiveBattle.HpMultiplier);
        }

        private static void AssertAllowed(UserRole role, params RasshiinePermissionOperation[] operations)
        {
            foreach (var operation in operations)
            {
                Assert.IsTrue(RasshiineRolePermissions.IsAllowed(role, operation), $"{role} should allow {operation}.");
            }
        }

        private static void AssertDenied(UserRole role, params RasshiinePermissionOperation[] operations)
        {
            foreach (var operation in operations)
            {
                Assert.IsFalse(RasshiineRolePermissions.IsAllowed(role, operation), $"{role} should deny {operation}.");
            }
        }
    }
}
