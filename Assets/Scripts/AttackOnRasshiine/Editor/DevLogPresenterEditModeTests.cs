using System;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Scene;
using AttackOnRasshiine.Runtime.Services;
using AttackOnRasshiine.Runtime.UI;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class DevLogPresenterEditModeTests
    {
        [TearDown]
        public void TearDown()
        {
            RasshiineRuntimeSession.Clear();
        }

        [Test]
        public void BuildDetectsActiveSessionAndReviewQueues()
        {
            var repository = new LocalGameRepository();
            var presenter = new DevLogPresenter();
            var member = repository.Members[0];
            var baseline = presenter.Build(repository, member, true, false);

            repository.StartSession(member.Id, "本番DevLogの承認待ち表示を確認する");
            repository.CompleteSession(member.Id, 80, "記録を保存した", "次の導線を見る");
            repository.StartSession(member.Id, "AI評価待ち表示を確認する");
            repository.CompleteSessionWithAiFailure(member.Id, 75, "通信失敗を確認した", "再試行する", "timeout");
            var active = repository.StartSession(member.Id, "未完了セッションを復帰する");

            var state = presenter.Build(repository, member, true, true);

            Assert.AreSame(active, state.ActiveSession);
            Assert.IsTrue(state.HasActiveSession);
            Assert.AreEqual("本番API保存", state.ApiModeLabel);
            Assert.IsTrue(state.IsBusy);
            Assert.Greater(state.PendingCount, baseline.PendingCount);
            Assert.Greater(state.AiPendingCount, baseline.AiPendingCount);
        }

        [Test]
        public void StartSessionStoresGoalAndPreventsDuplicateInProgressSessions()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var startedBefore = DateTime.UtcNow;

            var session = repository.StartSession(member.Id, "  Heat UIの開発ログ画面を整える  ");

            Assert.AreEqual(member.Id, session.UserId);
            Assert.AreEqual("Heat UIの開発ログ画面を整える", session.Goal);
            Assert.AreEqual(DevSessionStatus.InProgress, session.Status);
            Assert.GreaterOrEqual(session.StartedAtUtc, startedBefore);
            Assert.AreSame(session, repository.GetActiveSession(member.Id));
            Assert.Throws<InvalidOperationException>(() => repository.StartSession(member.Id, "別の作業を開始"));
        }

        [Test]
        public void ValidateCompletionRequiresReflectionAndNextTask()
        {
            var presenter = new DevLogPresenter();

            Assert.IsFalse(presenter.ValidateStart(" ").IsValid);
            Assert.IsFalse(presenter.ValidateCompletion(101, "振り返り", "次").IsValid);
            Assert.IsFalse(presenter.ValidateCompletion(80, string.Empty, "次").IsValid);
            Assert.IsFalse(presenter.ValidateCompletion(80, "振り返り", string.Empty).IsValid);
            Assert.IsTrue(presenter.ValidateCompletion(80, "振り返り", "次").IsValid);
        }

        [Test]
        public void SessionViewDistinguishesTentativeAndFormalGrowth()
        {
            var repository = new LocalGameRepository();
            var presenter = new DevLogPresenter();
            var member = repository.Members[0];
            var mentor = repository.Mentors[0];

            repository.StartSession(member.Id, "仮成長を確認する");
            var pending = repository.CompleteSession(member.Id, 90, "実装した", "承認を待つ");
            var pendingView = presenter.ToView(pending);

            StringAssert.Contains("仮成長", pendingView.GrowthStateLabel);
            Assert.Greater(pendingView.PendingExp, 0);

            var approved = repository.ApproveSession(pending.Id, mentor.Id, "正式反映");
            var approvedView = presenter.ToView(approved);

            StringAssert.Contains("正式成長", approvedView.GrowthStateLabel);
            Assert.Greater(approvedView.FormalExp, 0);
        }

        [Test]
        public void BuildRestoresIncompleteSessionFromRuntimeSnapshot()
        {
            var source = new LocalGameRepository();
            var presenter = new DevLogPresenter();
            var member = source.Members[0];
            var active = source.StartSession(member.Id, "Scene遷移後も未完了セッションを復帰する");
            active.Status = DevSessionStatus.Incomplete;

            RasshiineRuntimeSession.SetSnapshot(source.CreateSnapshot());
            var restored = new LocalGameRepository();
            restored.ApplySnapshot(RasshiineRuntimeSession.Snapshot);
            var state = presenter.Build(restored, member, false, false);

            Assert.AreEqual(active.Id, state.ActiveSession?.Id);
            Assert.AreEqual("デモ保存", state.ApiModeLabel);
            Assert.IsTrue(presenter.ToView(state.ActiveSession).CanResume);
        }
    }
}
