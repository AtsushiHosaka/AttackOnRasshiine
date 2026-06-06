using System;
using System.Collections.Generic;
using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class SuspiciousLogDetectorEditModeTests
    {
        [Test]
        public void DetectsSingleSessionRules()
        {
            var session = CreateSession(durationMinutes: 180, goal: "AI", reflection: "良", nextTask: "次", achievementRate: 0);

            var flags = SuspiciousLogDetector.Detect(session);

            CollectionAssert.Contains(flags, SuspiciousLogDetector.LongSessionFlag);
            CollectionAssert.Contains(flags, SuspiciousLogDetector.ShortGoalFlag);
            CollectionAssert.Contains(flags, SuspiciousLogDetector.ShortReflectionFlag);
            CollectionAssert.Contains(flags, SuspiciousLogDetector.ShortNextTaskFlag);
            CollectionAssert.Contains(flags, SuspiciousLogDetector.NeedsReviewFlag);
        }

        [Test]
        public void DetectsConsecutivePerfectAchievements()
        {
            var now = DateTime.UtcNow;
            var history = new List<DevSession>
            {
                CreateSession(id: "previous-2", startedAtUtc: now.AddDays(-2), achievementRate: 100),
                CreateSession(id: "previous-1", startedAtUtc: now.AddDays(-1), achievementRate: 100)
            };
            var current = CreateSession(id: "current", startedAtUtc: now, achievementRate: 100);

            var flags = SuspiciousLogDetector.Detect(current, history);

            CollectionAssert.Contains(flags, SuspiciousLogDetector.NeedsReviewFlag);
        }

        [Test]
        public void DetectsEightHourDailyTotal()
        {
            var day = new DateTime(2026, 5, 30, 8, 0, 0, DateTimeKind.Utc);
            var history = new List<DevSession>
            {
                CreateSession(id: "morning", startedAtUtc: day, durationMinutes: 180),
                CreateSession(id: "afternoon", startedAtUtc: day.AddHours(3), durationMinutes: 180)
            };
            var current = CreateSession(id: "evening", startedAtUtc: day.AddHours(6), durationMinutes: 120);

            var flags = SuspiciousLogDetector.Detect(current, history);

            CollectionAssert.Contains(flags, SuspiciousLogDetector.NeedsReviewFlag);
        }

        [Test]
        public void RepositoryKeepsFlaggedSessionForReviewWithoutAutoRejecting()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var session = repository.StartSession(member.Id, "AI");
            session.StartedAtUtc = DateTime.UtcNow.AddMinutes(-181);

            var completed = repository.CompleteSession(member.Id, 0, "良", "次");

            Assert.AreEqual(DevSessionStatus.NeedsReview, completed.Status);
            Assert.AreNotEqual(DevSessionStatus.Rejected, completed.Status);
            CollectionAssert.Contains(completed.SuspiciousFlags, SuspiciousLogDetector.LongSessionFlag);
            CollectionAssert.Contains(completed.SuspiciousFlags, SuspiciousLogDetector.NeedsReviewFlag);
            CollectionAssert.Contains(repository.GetPendingSessions(DevSessionReviewFilter.NeedsReview).Select(item => item.Id), completed.Id);
        }

        private static DevSession CreateSession(
            string id = "session",
            string userId = "member-1",
            DateTime? startedAtUtc = null,
            int durationMinutes = 60,
            string goal = "レイドUIを改善する",
            string reflection = "実装と検証を行った",
            string nextTask = "次の画面を確認する",
            int achievementRate = 70)
        {
            var startedAt = startedAtUtc ?? DateTime.UtcNow;
            return new DevSession
            {
                Id = id,
                UserId = userId,
                StartedAtUtc = startedAt,
                EndedAtUtc = startedAt.AddMinutes(durationMinutes),
                DurationMinutes = durationMinutes,
                Goal = goal,
                Reflection = reflection,
                NextTask = nextTask,
                AchievementRate = achievementRate,
                Status = DevSessionStatus.Pending
            };
        }
    }
}
