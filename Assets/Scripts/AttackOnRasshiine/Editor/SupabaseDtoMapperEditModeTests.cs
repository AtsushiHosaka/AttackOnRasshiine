using System.Collections.Generic;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class SupabaseDtoMapperEditModeTests
    {
        [Test]
        public void NullSnapshotMapsToEmptyDomainSnapshot()
        {
            GameSnapshotDto dto = null;

            var snapshot = dto.ToSnapshot();

            Assert.IsNotNull(snapshot);
            Assert.IsEmpty(snapshot.Users);
            Assert.IsEmpty(snapshot.Stats);
            Assert.IsEmpty(snapshot.Sessions);
            Assert.IsNull(snapshot.ActiveBattle);
        }

        [Test]
        public void SnapshotMapperClampsEnumsAndNormalizesCollections()
        {
            var dto = new GameSnapshotDto
            {
                Users = new List<UserProfileDto>
                {
                    new()
                    {
                        Id = "member-1",
                        LoginId = "member1",
                        Nickname = "メンバー1",
                        Role = 99,
                        TeamId = "blue",
                        RankingVisible = true,
                        InitialPasswordChanged = true,
                        IsActive = true
                    }
                },
                Stats = new List<CharacterStatsDto>
                {
                    new()
                    {
                        UserId = "member-1",
                        Level = 0,
                        Exp = -10,
                        Hp = 0,
                        Atk = -1,
                        Def = -1,
                        Mp = -1,
                        UnlockedWeapons = new List<int> { -1, 999, (int)WeaponKind.Rifle, (int)WeaponKind.Rifle },
                        Titles = new List<string> { "初回", "初回", " " },
                        Skills = null
                    }
                }
            };

            var snapshot = dto.ToSnapshot();
            var user = snapshot.Users[0];
            var stats = snapshot.Stats[0].Stats;

            Assert.AreEqual(UserRole.Mentor, user.Role);
            Assert.AreEqual(1, stats.Level);
            Assert.AreEqual(0, stats.Exp);
            Assert.AreEqual(100, stats.Hp);
            Assert.AreEqual(10, stats.Atk);
            Assert.AreEqual(5, stats.Def);
            Assert.AreEqual(30, stats.Mp);
            CollectionAssert.AreEqual(new[] { WeaponKind.Blade, WeaponKind.ContestGear, WeaponKind.Rifle }, stats.UnlockedWeapons);
            CollectionAssert.AreEqual(new[] { "初回" }, stats.Titles);
            Assert.IsNotNull(stats.Skills);
            Assert.IsEmpty(stats.Skills);
        }

        [Test]
        public void CharacterStatsDtoUsesLevelDerivedStatusRules()
        {
            var dto = new CharacterStatsDto
            {
                Level = 4,
                Exp = 12,
                Hp = 1,
                Atk = 0,
                Def = 0,
                Mp = 0
            };

            var stats = dto.ToDomain();

            Assert.AreEqual(4, stats.Level);
            Assert.AreEqual(12, stats.Exp);
            Assert.AreEqual(130, stats.Hp);
            Assert.AreEqual(16, stats.Atk);
            Assert.AreEqual(8, stats.Def);
            Assert.AreEqual(36, stats.Mp);
        }

        [Test]
        public void ApiErrorClassifiesAuthExpiryAndRetryableServerFailures()
        {
            var expired = SupabaseApiError.FromResponse(
                new SupabaseGameApiResponseDto
                {
                    Ok = false,
                    ErrorCode = "auth_expired",
                    Error = "session expired",
                    AuthExpired = true
                },
                401);
            var server = SupabaseApiError.Transport(503, "service unavailable");

            Assert.AreEqual(SupabaseApiErrorKind.AuthenticationExpired, expired.Kind);
            Assert.IsTrue(expired.IsAuthExpired);
            Assert.IsFalse(expired.CanRetry);
            Assert.AreEqual(SupabaseApiErrorKind.Server, server.Kind);
            Assert.IsTrue(server.CanRetry);
        }

        [Test]
        public void AiEvaluationDtoUsesRankMultiplierTable()
        {
            var dto = new AiEvaluationDto
            {
                Rank = (int)AiRank.B,
                TotalScore = 72,
                ExpMultiplier = 0.1f,
                Feedback = "rank table"
            };

            var evaluation = dto.ToDomain();

            Assert.AreEqual(AiRank.B, evaluation.Rank);
            Assert.AreEqual(1.3f, evaluation.ExpMultiplier);
        }

        [Test]
        public void ApiRequestCarriesCurrentContractVersion()
        {
            var request = new SupabaseGameApiRequestDto();

            Assert.AreEqual(SupabaseGameApiContract.CurrentVersion, request.ContractVersion);
            Assert.AreEqual("2026-07-14.1", SupabaseGameApiContract.CurrentVersion);
            Assert.AreEqual("/functions/v1/game-api-v2", SupabaseGameApiContract.FunctionPath);
            Assert.AreEqual("restore-session", SupabaseGameApiActions.RestoreSession);
            Assert.AreEqual("change-password", SupabaseGameApiActions.ChangePassword);
            Assert.AreEqual("create-account", SupabaseGameApiActions.CreateAccount);
            Assert.AreEqual("issue-temporary-password", SupabaseGameApiActions.IssueTemporaryPassword);
            Assert.AreEqual("front-display-snapshot", SupabaseGameApiActions.FrontDisplaySnapshot);
            Assert.AreEqual("battle-state", SupabaseGameApiActions.BattleState);
            Assert.AreEqual("register-product", SupabaseGameApiActions.RegisterProduct);
            Assert.AreEqual("hide-product", SupabaseGameApiActions.HideProduct);
            Assert.AreEqual("cosmetic-inventory", SupabaseGameApiActions.CosmeticInventory);
            Assert.AreEqual("roll-cosmetic-gacha", SupabaseGameApiActions.RollCosmeticGacha);
            Assert.AreEqual("equip-cosmetic", SupabaseGameApiActions.EquipCosmetic);
            Assert.AreEqual("rankings", SupabaseGameApiActions.Rankings);
        }

        [Test]
        public void SnapshotMapperPreservesTheAuthoritativeRaidEpoch()
        {
            const string raidEpoch = "895522b4-2235-4c22-a0f7-94b58e14478e";
            var dto = new GameSnapshotDto
            {
                ActiveBattle = new BossBattleStateDto
                {
                    Id = "fb49b115-1ebd-434d-910c-4f4526bb8bc2",
                    RaidEpoch = raidEpoch,
                    Boss = new MentorBossDto
                    {
                        Id = "boss-1",
                        Name = "メンター・らっしーね",
                        BossType = "コードマスター",
                        MaxHp = 100,
                        CurrentHp = 100
                    }
                }
            };

            var snapshot = dto.ToSnapshot();

            Assert.AreEqual(raidEpoch, snapshot.ActiveBattle.RaidEpoch);
        }
    }
}
