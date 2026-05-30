using System;
using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class DevelopmentTimeRankingEditModeTests
    {
        [Test]
        public void DevelopmentTimeRankingFiltersByPeriod()
        {
            var repository = new LocalGameRepository();
            DisableSeedSessions(repository);
            var member = repository.Members[0];
            var nowUtc = new DateTime(2030, 5, 15, 12, 0, 0, DateTimeKind.Utc);

            AddApprovedSession(repository, member, nowUtc.AddMinutes(-30), 30);
            AddApprovedSession(repository, member, nowUtc.AddHours(-2), 120);
            AddApprovedSession(repository, member, new DateTime(2030, 4, 10, 12, 0, 0, DateTimeKind.Utc), 80);
            AddApprovedSession(repository, member, new DateTime(2030, 3, 31, 12, 0, 0, DateTimeKind.Utc), 240);

            Assert.AreEqual(30, repository.GetDevelopmentTimeRanking(RankingPeriod.Hourly, nowUtc).Single().DurationMinutes);
            Assert.AreEqual(150, repository.GetDevelopmentTimeRanking(RankingPeriod.Weekly, nowUtc).Single().DurationMinutes);
            Assert.AreEqual(230, repository.GetDevelopmentTimeRanking(RankingPeriod.Term, nowUtc).Single().DurationMinutes);
            Assert.AreEqual(470, repository.GetDevelopmentTimeRanking(RankingPeriod.AllTime, nowUtc).Single().DurationMinutes);
        }

        [Test]
        public void DevelopmentTimeRankingUsesApprovedVisibleMembersAndNicknameOrder()
        {
            var repository = new LocalGameRepository();
            DisableSeedSessions(repository);
            var first = repository.Members[0];
            var hidden = repository.Members[1];
            var leader = repository.Members[2];
            hidden.RankingVisible = false;
            var nowUtc = new DateTime(2030, 5, 15, 12, 0, 0, DateTimeKind.Utc);

            AddApprovedSession(repository, first, nowUtc.AddMinutes(-10), 60);
            AddApprovedSession(repository, hidden, nowUtc.AddMinutes(-10), 200);
            AddApprovedSession(repository, leader, nowUtc.AddMinutes(-10), 90);
            AddRejectedSession(repository, first, nowUtc.AddMinutes(-10), 300);

            var ranking = repository.GetDevelopmentTimeRanking(RankingPeriod.Weekly, nowUtc).ToList();

            Assert.AreEqual(2, ranking.Count);
            Assert.AreEqual(leader.Nickname, ranking[0].Nickname);
            Assert.AreEqual(90, ranking[0].DurationMinutes);
            Assert.AreEqual(first.Nickname, ranking[1].Nickname);
            Assert.AreEqual(60, ranking[1].DurationMinutes);
            CollectionAssert.DoesNotContain(ranking.Select(entry => entry.Nickname), hidden.Nickname);
        }

        [Test]
        public void TermRankingUsesAprilToSeptemberWindow()
        {
            var repository = new LocalGameRepository();
            DisableSeedSessions(repository);
            var member = repository.Members[0];
            var nowUtc = new DateTime(2030, 10, 15, 12, 0, 0, DateTimeKind.Utc);

            AddApprovedSession(repository, member, new DateTime(2030, 3, 31, 23, 59, 0, DateTimeKind.Utc), 100);
            AddApprovedSession(repository, member, new DateTime(2030, 4, 1, 0, 0, 0, DateTimeKind.Utc), 70);
            AddApprovedSession(repository, member, new DateTime(2030, 9, 30, 23, 59, 0, DateTimeKind.Utc), 80);
            AddApprovedSession(repository, member, new DateTime(2030, 10, 1, 0, 0, 0, DateTimeKind.Utc), 90);

            Assert.AreEqual(150, repository.GetDevelopmentTimeRanking(RankingPeriod.Term, nowUtc).Single().DurationMinutes);
            Assert.AreEqual(340, repository.GetDevelopmentTimeRanking(RankingPeriod.AllTime, nowUtc).Single().DurationMinutes);
        }

        private static void DisableSeedSessions(LocalGameRepository repository)
        {
            foreach (var session in repository.Sessions)
            {
                session.Status = DevSessionStatus.Rejected;
            }
        }

        private static void AddApprovedSession(LocalGameRepository repository, UserProfile member, DateTime endedAtUtc, int durationMinutes)
        {
            AddSession(repository, member, endedAtUtc, durationMinutes, DevSessionStatus.Approved);
        }

        private static void AddRejectedSession(LocalGameRepository repository, UserProfile member, DateTime endedAtUtc, int durationMinutes)
        {
            AddSession(repository, member, endedAtUtc, durationMinutes, DevSessionStatus.Rejected);
        }

        private static void AddSession(LocalGameRepository repository, UserProfile member, DateTime endedAtUtc, int durationMinutes, DevSessionStatus status)
        {
            var session = repository.StartSession(member.Id, "ランキング検証");
            session.StartedAtUtc = endedAtUtc.AddMinutes(-durationMinutes);
            session.EndedAtUtc = endedAtUtc;
            session.DurationMinutes = durationMinutes;
            session.Status = status;
            session.Evaluation = new AiEvaluation
            {
                TotalScore = 80,
                Rank = AiRank.A,
                ExpMultiplier = 1.6f,
                Feedback = "ランキング検証",
                ModelName = "test"
            };
        }
    }
}
