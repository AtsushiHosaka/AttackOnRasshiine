using System;
using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class MentorAccountOperationsEditModeTests
    {
        [Test]
        public void MentorCanCreateMemberAccountWithTemporaryPasswordAndAuditTrail()
        {
            var repository = new LocalGameRepository();
            var mentor = repository.Mentors[0];
            var beforeCount = repository.Members.Count;

            var result = repository.CreateMemberAccount(mentor.Id, " new.member ", "新メンバー", "magenta");

            Assert.AreEqual(beforeCount + 1, repository.Members.Count);
            Assert.AreEqual("new.member", result.User.LoginId);
            Assert.AreEqual("新メンバー", result.User.Nickname);
            Assert.AreEqual(UserRole.Member, result.User.Role);
            Assert.AreEqual("magenta", result.User.TeamId);
            Assert.IsFalse(result.User.InitialPasswordChanged);
            Assert.IsTrue(result.User.IsActive);
            Assert.IsNotEmpty(result.TemporaryPassword);
            Assert.AreSame(result.User, repository.Authenticate("new.member", result.TemporaryPassword));
            Assert.AreEqual(1, repository.GetStats(result.User.Id).Level);

            var auditActions = repository.GetAuditLogsForTarget("user", result.User.Id).Select(log => log.ActionType).ToArray();
            CollectionAssert.Contains(auditActions, "account.create");
            CollectionAssert.Contains(auditActions, "account.temporary_password_issue");
        }

        [Test]
        public void MemberCannotCreateAccountsOrIssueTemporaryPasswords()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var target = repository.Members[1];
            var beforeCount = repository.Members.Count;

            Assert.Throws<InvalidOperationException>(() => repository.CreateMemberAccount(member.Id, "blocked.member", "権限なし", "blue"));
            Assert.Throws<InvalidOperationException>(() => repository.IssueTemporaryPassword(member.Id, target.Id));

            Assert.AreEqual(beforeCount, repository.Members.Count);
            Assert.IsEmpty(repository.GetAuditLogsForTarget("user", target.Id));
        }

        [Test]
        public void TemporaryPasswordResetAndPasswordChangeUpdateInitialPasswordState()
        {
            var repository = new LocalGameRepository();
            var mentor = repository.Mentors[0];
            var member = repository.Members[0];

            var issued = repository.IssueTemporaryPassword(mentor.Id, member.Id);

            Assert.IsFalse(member.InitialPasswordChanged);
            Assert.IsNull(repository.Authenticate(member.LoginId, "password"));
            Assert.AreSame(member, repository.Authenticate(member.LoginId, issued.TemporaryPassword));

            var changed = repository.ChangePassword(member.Id, issued.TemporaryPassword, "new-password");

            Assert.AreSame(member, changed);
            Assert.IsTrue(member.InitialPasswordChanged);
            Assert.IsNull(repository.Authenticate(member.LoginId, issued.TemporaryPassword));
            Assert.AreSame(member, repository.Authenticate(member.LoginId, "new-password"));
            var auditActions = repository.GetAuditLogsForTarget("user", member.Id).Select(log => log.ActionType).ToArray();
            CollectionAssert.Contains(auditActions, "account.temporary_password_issue");
            CollectionAssert.Contains(auditActions, "account.initial_password_change");
        }
    }
}
