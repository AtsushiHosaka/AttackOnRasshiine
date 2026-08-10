using System;
using System.Reflection;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class SupabaseScalingEditModeTests
    {
        [Test]
        public void WebGlClientUsesExplicitResponseByteLimits()
        {
            Assert.AreEqual(2 * 1024 * 1024, SupabaseGameClient.MaximumApiResponseBytes);
            Assert.AreEqual(32 * 1024, SupabaseGameClient.MaximumConfigResponseBytes);
            Assert.AreEqual("battle-state", SupabaseGameApiActions.BattleState);
        }

        [Test]
        public void ConditionalCacheRetainsProxyWeakSha256Etags()
        {
            var client = new SupabaseGameClient();
            var clientType = typeof(SupabaseGameClient);
            var conditionalKindType = clientType.GetNestedType(
                "ConditionalResponseKind",
                BindingFlags.NonPublic);
            var store = clientType.GetMethod(
                "StoreConditionalEtag",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var get = clientType.GetMethod(
                "GetConditionalEtag",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(conditionalKindType);
            Assert.IsNotNull(store);
            Assert.IsNotNull(get);

            var front = Enum.Parse(conditionalKindType, "FrontDisplay");
            var weakEtag = "W/\"" + new string('a', 64) + "\"";
            store.Invoke(client, new[] { front, weakEtag });

            Assert.AreEqual(weakEtag, get.Invoke(client, new[] { front }));

            store.Invoke(client, new[] { front, "W/\"not-a-sha256\"" });
            Assert.AreEqual(
                weakEtag,
                get.Invoke(client, new[] { front }),
                "Malformed validators must never replace a known-good ETag.");
        }

        [Test]
        public void StreamingDownloadRejectsBytesAboveItsHardLimit()
        {
            var handlerType = typeof(SupabaseGameClient).Assembly.GetType(
                "AttackOnRasshiine.Runtime.Services.BoundedDownloadHandler",
                true);
            var handler = handlerType.GetConstructor(new[] { typeof(int) })?.Invoke(new object[] { 8 });
            Assert.IsNotNull(handler);

            try
            {
                var receive = handlerType.GetMethod(
                    "ReceiveData",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var exceeded = handlerType.GetProperty(
                    "LimitExceeded",
                    BindingFlags.Instance | BindingFlags.Public);
                var receivedBytes = handlerType.GetProperty(
                    "ReceivedBytes",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.IsNotNull(receive);
                Assert.IsNotNull(exceeded);
                Assert.IsNotNull(receivedBytes);

                Assert.IsTrue((bool)receive.Invoke(handler, new object[] { new byte[8], 8 }));
                Assert.AreEqual(8, receivedBytes.GetValue(handler));
                Assert.IsFalse((bool)receive.Invoke(handler, new object[] { new byte[1], 1 }));
                Assert.IsTrue((bool)exceeded.GetValue(handler));
                Assert.AreEqual(8, receivedBytes.GetValue(handler));
            }
            finally
            {
                (handler as IDisposable)?.Dispose();
            }
        }

        [Test]
        public void RaidEpochDeltaResetsOnlyVolatileBattleProgress()
        {
            var repository = new LocalGameRepository();
            var battle = repository.ActiveBattle;
            var participant = battle.Participants[0];
            battle.RaidEpoch = "old-epoch";
            battle.TotalDamage = 99;
            battle.HighlightUserId = participant.UserId;
            battle.Actions.Add(new BattleActionResult { UserId = participant.UserId, Damage = 99 });
            participant.TotalDamage = 99;
            participant.TotalHeal = 5;
            participant.SupportCount = 2;

            repository.ApplyBattleDelta(new BattleStateDeltaDto
            {
                Id = battle.Id,
                RaidEpoch = "new-epoch",
                BossName = "メンター・らっしーね",
                BossType = "コードマスター",
                CurrentHp = 2400,
                MaxHp = 2500,
                Status = (int)BattleStatus.Active,
                TurnNumber = 2,
                StartedAtUtc = "2026-07-14T12:30:00Z"
            });

            Assert.AreEqual("new-epoch", battle.RaidEpoch);
            Assert.AreEqual(2400, battle.Boss.CurrentHp);
            Assert.AreEqual(2500, battle.Boss.MaxHp);
            Assert.AreEqual(BattleStatus.Active, battle.Status);
            Assert.AreEqual(2, battle.TurnNumber);
            Assert.AreEqual(0, battle.TotalDamage);
            Assert.IsEmpty(battle.Actions);
            Assert.AreEqual(0, participant.TotalDamage);
            Assert.AreEqual(0, participant.TotalHeal);
            Assert.AreEqual(0, participant.SupportCount);
            Assert.IsTrue(battle.StartedAtUtc.HasValue);
        }

        [Test]
        public void CompletedBattleDeltaMapsVictoryAndRejectsAnotherBattleId()
        {
            var repository = new LocalGameRepository();
            var battle = repository.ActiveBattle;
            var originalHp = battle.Boss.CurrentHp;

            repository.ApplyBattleDelta(new BattleStateDeltaDto
            {
                Id = "another-battle",
                CurrentHp = 0,
                MaxHp = battle.Boss.MaxHp,
                Status = (int)BattleStatus.Completed
            });
            Assert.AreEqual(originalHp, battle.Boss.CurrentHp);

            repository.ApplyBattleDelta(new BattleStateDeltaDto
            {
                Id = battle.Id,
                RaidEpoch = battle.RaidEpoch,
                CurrentHp = 0,
                MaxHp = battle.Boss.MaxHp,
                Status = (int)BattleStatus.Completed,
                TurnNumber = 3,
                Result = "win",
                CompletedAtUtc = "2026-07-14T12:35:00Z"
            });

            Assert.AreEqual(BattleStatus.Completed, battle.Status);
            Assert.AreEqual(BattlePhase.Completed, battle.Phase);
            Assert.AreEqual(BattleOutcome.Victory, battle.Outcome);
            Assert.AreEqual(0, battle.Boss.CurrentHp);
            Assert.IsTrue(battle.CompletedAtUtc.HasValue);
        }
    }
}
