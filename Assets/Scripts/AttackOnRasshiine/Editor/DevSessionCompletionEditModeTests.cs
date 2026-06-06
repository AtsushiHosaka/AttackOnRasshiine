using System;
using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class DevSessionCompletionEditModeTests
    {
        [Test]
        public void CompleteSessionStoresDurationAndMovesToReviewQueue()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var active = repository.StartSession(member.Id, "終了処理を検証する");
            active.StartedAtUtc = DateTime.UtcNow.AddMinutes(-42);

            var completed = repository.CompleteSession(
                member.Id,
                86,
                "実装と検証を行い、保存後の状態遷移を確認した",
                "次は承認画面で表示を確認する");

            Assert.AreSame(active, completed);
            Assert.AreEqual(DevSessionStatus.Pending, completed.Status);
            Assert.NotNull(completed.EndedAtUtc);
            Assert.GreaterOrEqual(completed.DurationMinutes, 40);
            Assert.AreEqual(86, completed.AchievementRate);
            Assert.AreEqual("実装と検証を行い、保存後の状態遷移を確認した", completed.Reflection);
            Assert.AreEqual("次は承認画面で表示を確認する", completed.NextTask);
            Assert.NotNull(completed.Evaluation);
            Assert.IsNull(repository.GetActiveSession(member.Id));
            CollectionAssert.Contains(repository.GetPendingSessions().Select(session => session.Id), completed.Id);
        }

        [Test]
        public void CompleteSessionWithAiFailureStoresInputAndMovesToAiPending()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var active = repository.StartSession(member.Id, "AI失敗時の終了処理を検証する");
            active.StartedAtUtc = DateTime.UtcNow.AddMinutes(-31);

            var completed = repository.CompleteSessionWithAiFailure(
                member.Id,
                64,
                "AI評価が失敗してもログの保存内容を維持する",
                "次は再評価の導線を確認する",
                " gateway timeout ");

            Assert.AreSame(active, completed);
            Assert.AreEqual(DevSessionStatus.AiPending, completed.Status);
            Assert.NotNull(completed.EndedAtUtc);
            Assert.GreaterOrEqual(completed.DurationMinutes, 30);
            Assert.AreEqual(64, completed.AchievementRate);
            Assert.AreEqual("AI評価が失敗してもログの保存内容を維持する", completed.Reflection);
            Assert.AreEqual("次は再評価の導線を確認する", completed.NextTask);
            Assert.IsNull(completed.Evaluation);
            Assert.AreEqual("gateway timeout", completed.AiEvaluationFailureReason);
            Assert.IsNull(repository.GetActiveSession(member.Id));
            CollectionAssert.Contains(repository.GetPendingSessions().Select(session => session.Id), completed.Id);
        }
    }
}
