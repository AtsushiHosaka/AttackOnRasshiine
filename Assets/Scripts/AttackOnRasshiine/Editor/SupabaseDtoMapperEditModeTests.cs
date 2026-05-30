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
            Assert.AreEqual(1, stats.Hp);
            Assert.AreEqual(0, stats.Atk);
            CollectionAssert.AreEqual(new[] { WeaponKind.Blade, WeaponKind.ContestGear, WeaponKind.Rifle }, stats.UnlockedWeapons);
            CollectionAssert.AreEqual(new[] { "初回" }, stats.Titles);
            Assert.IsNotNull(stats.Skills);
            Assert.IsEmpty(stats.Skills);
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
        public void ApiRequestCarriesCurrentContractVersion()
        {
            var request = new SupabaseGameApiRequestDto();

            Assert.AreEqual(SupabaseGameApiContract.CurrentVersion, request.ContractVersion);
            Assert.AreEqual("front-display-snapshot", SupabaseGameApiActions.FrontDisplaySnapshot);
            Assert.AreEqual("register-product", SupabaseGameApiActions.RegisterProduct);
            Assert.AreEqual("hide-product", SupabaseGameApiActions.HideProduct);
        }
    }
}
