using System;
using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class AiEvaluationFailureEditModeTests
    {
        [Test]
        public void AiFailureKeepsCompletedSessionDataAsPendingEvaluation()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            repository.StartSession(member.Id, "ボス戦HUDを直す");

            var completed = repository.CompleteSessionWithAiFailure(
                member.Id,
                75,
                "HPバーの表示崩れを修正して検証した",
                "次はMP表示を確認する",
                " Gemini timeout ");

            Assert.AreEqual(DevSessionStatus.AiPending, completed.Status);
            Assert.IsNull(completed.Evaluation);
            Assert.AreEqual("Gemini timeout", completed.AiEvaluationFailureReason);
            Assert.AreEqual("HPバーの表示崩れを修正して検証した", completed.Reflection);
            Assert.AreEqual("次はMP表示を確認する", completed.NextTask);
            Assert.Greater(completed.DurationMinutes, 0);
            CollectionAssert.Contains(repository.GetPendingSessions().Select(session => session.Id), completed.Id);
        }

        [Test]
        public void RetryAiEvaluationMovesSessionToReviewQueueWithEvaluation()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            repository.StartSession(member.Id, "ログ保存を確認する");
            var completed = repository.CompleteSessionWithAiFailure(member.Id, 80, "保存失敗時の状態を検証した", "再試行導線を見る", "network error");

            var retried = repository.RetryAiEvaluation(completed.Id);

            Assert.AreEqual(DevSessionStatus.Pending, retried.Status);
            Assert.NotNull(retried.Evaluation);
            Assert.AreEqual(string.Empty, retried.AiEvaluationFailureReason);
            Assert.Greater(retried.PreviewExp, 0);
        }

        [Test]
        public void FallbackEvaluationAllowsMentorApprovalWithoutLosingSession()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var mentor = repository.Mentors[0];
            repository.StartSession(member.Id, "承認前の暫定評価を確認する");
            var completed = repository.CompleteSessionWithAiFailure(member.Id, 65, "AI待ちでもログが残ることを確認した", "承認時のEXP反映を見る", string.Empty);

            var fallback = repository.ApplyAiEvaluationFallback(completed.Id);

            Assert.AreEqual(DevSessionStatus.Pending, fallback.Status);
            Assert.NotNull(fallback.Evaluation);
            Assert.AreEqual(LocalGameRepository.AiFallbackFeedback, fallback.Evaluation.Feedback);
            Assert.AreEqual("local-rule-fallback", fallback.Evaluation.ModelName);
            Assert.AreEqual("ai_evaluation_failed", fallback.AiEvaluationFailureReason);

            var expBeforeApproval = repository.GetStats(member.Id).Exp;
            repository.ApproveSession(fallback.Id, mentor.Id, "暫定評価で承認");

            Assert.AreEqual(DevSessionStatus.Approved, fallback.Status);
            Assert.Greater(repository.GetStats(member.Id).Exp, expBeforeApproval);
        }
    }
}
