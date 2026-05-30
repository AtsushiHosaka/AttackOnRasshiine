using System;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class SessionReviewEditModeTests
    {
        [Test]
        public void CorrectionApprovalUsesCorrectedValuesAndAppliesExpOnce()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var mentor = repository.Mentors[0];
            repository.StartSession(member.Id, "修正承認の反映を確認する");
            var session = repository.CompleteSession(member.Id, 95, "初回入力", "次回入力");
            var stats = repository.GetStats(member.Id);
            var expBefore = stats.Exp;

            var reviewed = repository.ApproveSessionWithCorrections(
                session.Id,
                mentor.Id,
                40,
                30,
                "修正後の振り返り",
                "修正後の次タスク",
                "修正承認しました");

            Assert.AreEqual(DevSessionStatus.Approved, reviewed.Status);
            Assert.AreEqual(40, reviewed.AchievementRate);
            Assert.AreEqual(30, reviewed.DurationMinutes);
            Assert.AreEqual("修正後の振り返り", reviewed.Reflection);
            Assert.AreEqual("修正後の次タスク", reviewed.NextTask);
            Assert.AreEqual("修正承認しました", reviewed.MentorComment);
            Assert.NotNull(reviewed.Evaluation);
            Assert.AreEqual(expBefore + reviewed.PreviewExp, stats.Exp);

            repository.ApproveSession(reviewed.Id, mentor.Id, "再承認");

            Assert.AreEqual(expBefore + reviewed.PreviewExp, stats.Exp);
        }

        [Test]
        public void RejectedSessionDoesNotApplyExpAndCannotBeApprovedLater()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var mentor = repository.Mentors[0];
            repository.StartSession(member.Id, "却下時の反映を確認する");
            var session = repository.CompleteSession(member.Id, 80, "検証した", "次を見る");
            var expBefore = repository.GetStats(member.Id).Exp;

            var rejected = repository.RejectSession(session.Id, mentor.Id, "内容を再確認してください。");

            Assert.AreEqual(DevSessionStatus.Rejected, rejected.Status);
            Assert.AreEqual(expBefore, repository.GetStats(member.Id).Exp);
            Assert.Throws<InvalidOperationException>(() => repository.ApproveSession(session.Id, mentor.Id, "後から承認"));
        }

        [Test]
        public void MemberCannotReviewSession()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            repository.StartSession(member.Id, "メンター権限を確認する");
            var session = repository.CompleteSession(member.Id, 80, "検証した", "次を見る");

            Assert.Throws<InvalidOperationException>(() => repository.ApproveSession(session.Id, member.Id, "本人承認"));
            Assert.Throws<InvalidOperationException>(() => repository.RejectSession(session.Id, member.Id, "本人却下"));
        }
    }
}
