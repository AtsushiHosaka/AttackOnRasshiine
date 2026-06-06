using System;
using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using AttackOnRasshiine.Runtime.UI;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class IncompleteSessionDetectionEditModeTests
    {
        [Test]
        public void OverdueInProgressSessionIsMarkedIncompleteAndRemainsResumable()
        {
            var repository = new LocalGameRepository();
            var presenter = new DevLogPresenter();
            var member = repository.Members[0];
            var session = repository.StartSession(member.Id, "未完了検出を検証する");
            var now = DateTime.UtcNow;
            session.StartedAtUtc = now.AddHours(-3).AddMinutes(-1);

            var changed = repository.MarkOverdueSessionsIncomplete(now);
            var state = presenter.Build(repository, member, false, false);

            Assert.AreEqual(1, changed);
            Assert.AreEqual(DevSessionStatus.Incomplete, session.Status);
            Assert.AreSame(session, repository.GetActiveSession(member.Id));
            Assert.IsTrue(presenter.ToView(session).CanResume);
            Assert.AreSame(session, state.ActiveSession);
        }

        [Test]
        public void IncompleteSessionAppearsInMentorReviewQueueAndCanBeCorrected()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var mentor = repository.Mentors[0];
            var session = repository.StartSession(member.Id, "修正承認を検証する");
            var now = DateTime.UtcNow;
            session.StartedAtUtc = now.AddHours(-4);

            repository.MarkOverdueSessionsIncomplete(now);

            CollectionAssert.Contains(repository.GetPendingSessions().Select(item => item.Id), session.Id);

            var approved = repository.ApproveSessionWithCorrections(
                session.Id,
                mentor.Id,
                72,
                180,
                "実際の作業内容をメンターが補正した",
                "次は保存内容の見直しを行う",
                "未完了ログを補正して承認");

            Assert.AreEqual(DevSessionStatus.Approved, approved.Status);
            Assert.AreEqual(72, approved.AchievementRate);
            Assert.AreEqual(180, approved.DurationMinutes);
            Assert.AreEqual("実際の作業内容をメンターが補正した", approved.Reflection);
            Assert.AreEqual("次は保存内容の見直しを行う", approved.NextTask);
            Assert.NotNull(approved.Evaluation);
            Assert.GreaterOrEqual(approved.PreviewExp, 0);
        }
    }
}
