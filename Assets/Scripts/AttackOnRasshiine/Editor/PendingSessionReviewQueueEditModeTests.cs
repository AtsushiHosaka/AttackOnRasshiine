using System;
using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class PendingSessionReviewQueueEditModeTests
    {
        [Test]
        public void ReviewQueueIncludesOnlyMentorActionableStatuses()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            repository.StartSession(member.Id, "承認待ち一覧を確認する");
            var pending = repository.CompleteSession(member.Id, 80, "一覧の表示を実装して検証した", "次は状態フィルタを見る");

            var approved = repository.Sessions.First(session => session.Status == DevSessionStatus.Approved);

            var items = repository.GetPendingSessions();

            CollectionAssert.Contains(items.Select(session => session.Id), pending.Id);
            CollectionAssert.DoesNotContain(items.Select(session => session.Id), approved.Id);
            CollectionAssert.AllItemsAreNotNull(items);
            Assert.IsTrue(items.All(session => session.Status is DevSessionStatus.Pending or DevSessionStatus.NeedsReview or DevSessionStatus.AiPending));
        }

        [Test]
        public void ReviewQueueFiltersByPendingNeedsReviewAndAiPending()
        {
            var repository = new LocalGameRepository();
            var pending = CompletePending(repository, repository.Members[0].Id);
            var needsReview = CompleteNeedsReview(repository, repository.Members[1].Id);
            var aiPending = CompleteAiPending(repository, repository.Members[2].Id);

            Assert.AreEqual(new[] { pending.Id }, repository.GetPendingSessions(DevSessionReviewFilter.Pending).Select(session => session.Id));
            Assert.AreEqual(new[] { needsReview.Id }, repository.GetPendingSessions(DevSessionReviewFilter.NeedsReview).Select(session => session.Id));
            Assert.AreEqual(new[] { aiPending.Id }, repository.GetPendingSessions(DevSessionReviewFilter.AiPending).Select(session => session.Id));
        }

        [Test]
        public void ReviewQueueOrdersNewestFirst()
        {
            var repository = new LocalGameRepository();
            var older = CompletePending(repository, repository.Members[0].Id);
            var newer = CompleteAiPending(repository, repository.Members[1].Id);
            older.StartedAtUtc = new DateTime(2026, 5, 28, 10, 0, 0, DateTimeKind.Utc);
            newer.StartedAtUtc = new DateTime(2026, 5, 30, 10, 0, 0, DateTimeKind.Utc);

            var items = repository.GetPendingSessions();

            Assert.AreEqual(newer.Id, items[0].Id);
            Assert.AreEqual(older.Id, items[1].Id);
        }

        private static DevSession CompletePending(LocalGameRepository repository, string userId)
        {
            repository.StartSession(userId, "レイドUIを改善する");
            return repository.CompleteSession(userId, 75, "実装と検証を行った", "次の表示を確認する");
        }

        private static DevSession CompleteNeedsReview(LocalGameRepository repository, string userId)
        {
            var session = repository.StartSession(userId, "AI");
            session.StartedAtUtc = DateTime.UtcNow.AddMinutes(-181);
            return repository.CompleteSession(userId, 0, "良", "次");
        }

        private static DevSession CompleteAiPending(LocalGameRepository repository, string userId)
        {
            repository.StartSession(userId, "AI評価失敗時も保存する");
            return repository.CompleteSessionWithAiFailure(userId, 70, "保存状態を確認した", "再試行導線を見る", "network timeout");
        }
    }
}
