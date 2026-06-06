using System;
using System.Linq;
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

            Assert.IsFalse(presenter.ValidateCompletion(-1, "振り返り", "次").IsValid);
            Assert.IsFalse(presenter.ValidateStart(" ").IsValid);
            Assert.IsFalse(presenter.ValidateCompletion(101, "振り返り", "次").IsValid);
            Assert.IsFalse(presenter.ValidateCompletion(80, string.Empty, "次").IsValid);
            Assert.IsFalse(presenter.ValidateCompletion(80, "  ", "次").IsValid);
            Assert.IsFalse(presenter.ValidateCompletion(80, "振り返り", string.Empty).IsValid);
            Assert.IsFalse(presenter.ValidateCompletion(80, "振り返り", "  ").IsValid);
            Assert.IsTrue(presenter.ValidateCompletion(0, "振り返り", "次").IsValid);
            Assert.IsTrue(presenter.ValidateCompletion(100, "振り返り", "次").IsValid);
            Assert.IsTrue(presenter.ValidateCompletion(80, "振り返り", "次").IsValid);
        }

        [Test]
        public void CompleteSessionTrimsValidatedReflectionAndNextTask()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            repository.StartSession(member.Id, "入力値の保存を確認する");

            var completed = repository.CompleteSession(
                member.Id,
                100,
                "  振り返りを入力した  ",
                "  次のタスクを入力した  ");

            Assert.AreEqual(100, completed.AchievementRate);
            Assert.AreEqual("振り返りを入力した", completed.Reflection);
            Assert.AreEqual("次のタスクを入力した", completed.NextTask);
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
        public void SessionViewShowsRankAndFeedbackToOwnerAndMentor()
        {
            var repository = new LocalGameRepository();
            var presenter = new DevLogPresenter();
            var member = repository.Members[0];
            var mentor = repository.Mentors[0];

            repository.StartSession(member.Id, "評価ランクを表示する");
            var session = repository.CompleteSession(member.Id, 95, "実装と検証を行い、改善点を整理した", "次は承認画面を確認する");
            session.Evaluation.Rank = AiRank.APlus;
            session.Evaluation.TotalScore = 88;
            session.Evaluation.ExpMultiplier = 1.8f;
            session.Evaluation.Feedback = "目標と次回行動が具体的です。";

            var ownerView = presenter.ToView(session, member);
            var mentorView = presenter.ToView(session, mentor);

            Assert.IsTrue(ownerView.CanViewAiEvaluation);
            Assert.IsTrue(mentorView.CanViewAiEvaluation);
            StringAssert.Contains("A+", ownerView.AiEvaluationSummaryLabel);
            StringAssert.Contains("88/100", ownerView.AiEvaluationSummaryLabel);
            StringAssert.Contains("x1.8", ownerView.AiEvaluationSummaryLabel);
            Assert.AreEqual("目標と次回行動が具体的です。", ownerView.AiEvaluationFeedbackLabel);
            Assert.AreEqual(ownerView.AiEvaluationSummaryLabel, mentorView.AiEvaluationSummaryLabel);
            Assert.AreEqual(ownerView.AiEvaluationFeedbackLabel, mentorView.AiEvaluationFeedbackLabel);
        }

        [Test]
        public void SessionViewHidesRankAndFeedbackFromOtherMembers()
        {
            var repository = new LocalGameRepository();
            var presenter = new DevLogPresenter();
            var mentor = repository.Mentors[0];
            var member = repository.CreateMemberAccount(mentor.Id, "rank.owner", "評価本人", "cyan").User;
            var otherMember = repository.CreateMemberAccount(mentor.Id, "rank.other", "別メンバー", "magenta").User;

            repository.StartSession(member.Id, "評価を他人に見せない");
            var session = repository.CompleteSession(member.Id, 90, "実装と検証を行った", "次は表示権限を見る");

            var otherView = presenter.ToView(session, otherMember);

            Assert.IsFalse(otherView.CanViewAiEvaluation);
            Assert.IsEmpty(otherView.AiEvaluationSummaryLabel);
            Assert.IsEmpty(otherView.AiEvaluationFeedbackLabel);
        }

        [Test]
        public void BuildSurfacesApprovalCorrectionAndRejectionNotifications()
        {
            var repository = new LocalGameRepository();
            var presenter = new DevLogPresenter();
            var member = repository.Members[0];
            var mentor = repository.Mentors[0];

            repository.StartSession(member.Id, "通常承認通知");
            var approvedSource = repository.CompleteSession(member.Id, 90, "実装と検証を行った", "通知を見る");
            var approved = repository.ApproveSession(approvedSource.Id, mentor.Id, "その調子です");

            repository.StartSession(member.Id, "修正承認通知");
            var correctedSource = repository.CompleteSession(member.Id, 70, "修正前", "修正後を確認");
            var corrected = repository.ApproveSessionWithCorrections(
                correctedSource.Id,
                mentor.Id,
                85,
                45,
                "修正後の振り返り",
                "次の改善を見る",
                string.Empty);

            repository.StartSession(member.Id, "却下通知");
            var rejectedSource = repository.CompleteSession(member.Id, 50, "不足している", "書き直す");
            var rejected = repository.RejectSession(rejectedSource.Id, mentor.Id, "内容を具体化してください");

            var state = presenter.Build(repository, member, true, false);
            var approvedView = state.History.First(view => view.Session.Id == approved.Id);
            var correctedView = state.History.First(view => view.Session.Id == corrected.Id);
            var rejectedView = state.History.First(view => view.Session.Id == rejected.Id);

            StringAssert.Contains("承認通知", approvedView.ReviewNotificationLabel);
            StringAssert.Contains("正式EXP +", approvedView.ReviewNotificationLabel);
            StringAssert.Contains("その調子です", approvedView.ReviewNotificationLabel);
            StringAssert.Contains("修正承認通知", correctedView.ReviewNotificationLabel);
            StringAssert.Contains("達成度 85%", correctedView.ReviewNotificationLabel);
            StringAssert.Contains("開発時間 45分", correctedView.ReviewNotificationLabel);
            StringAssert.Contains("却下通知", rejectedView.ReviewNotificationLabel);
            StringAssert.Contains("成長反映なし", rejectedView.ReviewNotificationLabel);
            StringAssert.Contains("内容を具体化してください", rejectedView.ReviewNotificationLabel);
        }

        [Test]
        public void BuildReturnsScopedReadableSessionHistory()
        {
            var repository = new LocalGameRepository();
            var presenter = new DevLogPresenter();
            var mentor = repository.Mentors[0];
            var member = repository.CreateMemberAccount(mentor.Id, "history.member", "履歴メンバー", "cyan").User;
            var otherMember = repository.CreateMemberAccount(mentor.Id, "history.other", "別メンバー", "magenta").User;

            repository.StartSession(member.Id, "古い履歴を作る");
            var older = repository.CompleteSession(member.Id, 72, "古い履歴の表示を確認した", "次の履歴を見る");
            older.StartedAtUtc = DateTime.UtcNow.AddDays(-2);
            older.EndedAtUtc = older.StartedAtUtc.AddMinutes(48);

            repository.StartSession(otherMember.Id, "別ユーザーの履歴を作る");
            var otherSession = repository.CompleteSession(otherMember.Id, 88, "別ユーザーの履歴", "混ざらないことを確認");

            repository.StartSession(member.Id, "新しい履歴を作る");
            var recent = repository.CompleteSession(member.Id, 95, "新しい履歴の表示を確認した", "承認後の表示を見る");
            var approved = repository.ApproveSession(recent.Id, mentor.Id, "履歴から確認済み");

            var state = presenter.Build(repository, member, true, false);

            Assert.AreEqual(2, state.History.Count);
            Assert.IsTrue(state.History.All(view => view.Session.UserId == member.Id));
            CollectionAssert.DoesNotContain(state.History.Select(view => view.Session.Id), otherSession.Id);
            Assert.AreEqual(approved.Id, state.History[0].Session.Id);
            Assert.AreEqual(older.Id, state.History[1].Session.Id);

            var approvedView = state.History[0];
            Assert.AreEqual("承認済み", approvedView.StatusLabel);
            StringAssert.Contains("正式成長", approvedView.GrowthStateLabel);
            Assert.Greater(approvedView.FormalExp, 0);
            Assert.IsTrue(approvedView.CanViewAiEvaluation);
            StringAssert.Contains("AI評価", approvedView.AiEvaluationSummaryLabel);
            Assert.IsFalse(string.IsNullOrWhiteSpace(approved.Evaluation.Feedback));
            Assert.AreEqual(approved.Evaluation.Feedback, approvedView.AiEvaluationFeedbackLabel);
            StringAssert.Contains("履歴から確認済み", approved.MentorComment);
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
