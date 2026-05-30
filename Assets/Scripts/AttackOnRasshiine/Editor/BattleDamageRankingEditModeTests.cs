using System;
using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class BattleDamageRankingEditModeTests
    {
        [Test]
        public void BattleDamageRankingUsesVisibleMembersWithApprovedLogsInPeriod()
        {
            var repository = new LocalGameRepository();
            DisableSeedSessions(repository);
            var visible = repository.Members[0];
            var hidden = repository.Members[1];
            var rejected = repository.Members[2];
            var noDamage = repository.Members[3];
            hidden.RankingVisible = false;
            var nowUtc = new DateTime(2030, 5, 15, 12, 0, 0, DateTimeKind.Utc);

            AddSession(repository, visible, nowUtc.AddMinutes(-20), 60, DevSessionStatus.Approved);
            AddSession(repository, hidden, nowUtc.AddMinutes(-20), 80, DevSessionStatus.Approved);
            AddSession(repository, rejected, nowUtc.AddMinutes(-20), 120, DevSessionStatus.Rejected);
            AddSession(repository, noDamage, nowUtc.AddMinutes(-20), 45, DevSessionStatus.Approved);
            SetDamage(repository, visible.Id, 300);
            SetDamage(repository, hidden.Id, 500);
            SetDamage(repository, rejected.Id, 700);
            SetDamage(repository, noDamage.Id, 0);

            var ranking = repository.GetBattleDamageRanking(RankingPeriod.Hourly, nowUtc).ToList();

            Assert.AreEqual(1, ranking.Count);
            Assert.AreEqual(visible.Nickname, ranking[0].Nickname);
            Assert.AreEqual(300, ranking[0].Damage);
            Assert.AreEqual(60, ranking[0].ApprovedMinutes);
        }

        [Test]
        public void BattleDamageRankingSortsByDamageThenApprovedMinutes()
        {
            var repository = new LocalGameRepository();
            DisableSeedSessions(repository);
            var low = repository.Members[0];
            var tiedLonger = repository.Members[1];
            var tiedShorter = repository.Members[2];
            var nowUtc = new DateTime(2030, 5, 15, 12, 0, 0, DateTimeKind.Utc);

            AddSession(repository, low, nowUtc.AddMinutes(-10), 300, DevSessionStatus.Approved);
            AddSession(repository, tiedLonger, nowUtc.AddMinutes(-10), 120, DevSessionStatus.Approved);
            AddSession(repository, tiedShorter, nowUtc.AddMinutes(-10), 60, DevSessionStatus.Approved);
            SetDamage(repository, low.Id, 100);
            SetDamage(repository, tiedLonger.Id, 400);
            SetDamage(repository, tiedShorter.Id, 400);

            var ranking = repository.GetBattleDamageRanking(RankingPeriod.Weekly, nowUtc).ToList();

            Assert.AreEqual(tiedLonger.Nickname, ranking[0].Nickname);
            Assert.AreEqual(tiedShorter.Nickname, ranking[1].Nickname);
            Assert.AreEqual(low.Nickname, ranking[2].Nickname);
        }

        private static void DisableSeedSessions(LocalGameRepository repository)
        {
            foreach (var session in repository.Sessions)
            {
                session.Status = DevSessionStatus.Rejected;
            }
        }

        private static void AddSession(LocalGameRepository repository, UserProfile member, DateTime endedAtUtc, int durationMinutes, DevSessionStatus status)
        {
            var session = repository.StartSession(member.Id, "ダメージランキング検証");
            session.StartedAtUtc = endedAtUtc.AddMinutes(-durationMinutes);
            session.EndedAtUtc = endedAtUtc;
            session.DurationMinutes = durationMinutes;
            session.Status = status;
            session.Evaluation = new AiEvaluation
            {
                TotalScore = 80,
                Rank = AiRank.A,
                ExpMultiplier = 1.6f,
                Feedback = "ダメージランキング検証",
                ModelName = "test"
            };
        }

        private static void SetDamage(LocalGameRepository repository, string userId, int damage)
        {
            var participant = repository.ActiveBattle.Participants.First(item => item.UserId == userId);
            participant.TotalDamage = damage;
        }
    }
}
