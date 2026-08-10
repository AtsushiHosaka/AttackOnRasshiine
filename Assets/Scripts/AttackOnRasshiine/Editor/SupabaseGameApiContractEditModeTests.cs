using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;
using UnityEngine;

namespace AttackOnRasshiine.Editor
{
    public sealed class SupabaseGameApiContractEditModeTests
    {
        private static string ContractManifestPath => Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "..",
            "..",
            "AttackOnRasshiineSupabase",
            "contracts",
            "game-api.contract.json"));

        [Test]
        public void UnityContractMatchesCanonicalManifest()
        {
            Assert.IsTrue(File.Exists(ContractManifestPath), ContractManifestPath);
            var contract = File.ReadAllText(ContractManifestPath);

            StringAssert.Contains($"\"contractVersion\": \"{SupabaseGameApiContract.CurrentVersion}\"", contract);
            StringAssert.Contains($"\"apiPath\": \"{SupabaseGameApiContract.FunctionPath}\"", contract);
            StringAssert.Contains($"\"contractHeader\": \"{SupabaseGameApiContract.ContractHeader}\"", contract);
            StringAssert.Contains("\"allowedKeyKind\": \"supabase_publishable\"", contract);
            StringAssert.Contains("\"IdempotencyKey\"", contract);

            AssertAction(contract, SupabaseGameApiActions.Health);
            AssertAction(contract, SupabaseGameApiActions.Login);
            AssertAction(contract, SupabaseGameApiActions.ChangePassword);
            AssertAction(contract, SupabaseGameApiActions.CreateAccount);
            AssertAction(contract, SupabaseGameApiActions.IssueTemporaryPassword);
            AssertAction(contract, SupabaseGameApiActions.RestoreSession);
            AssertAction(contract, SupabaseGameApiActions.Logout);
            AssertAction(contract, SupabaseGameApiActions.Snapshot);
            AssertAction(contract, SupabaseGameApiActions.FrontDisplaySnapshot);
            AssertAction(contract, SupabaseGameApiActions.StartSession);
            AssertAction(contract, SupabaseGameApiActions.CompleteSession);
            AssertAction(contract, SupabaseGameApiActions.SessionHistory);
            AssertAction(contract, SupabaseGameApiActions.ApproveSession);
            AssertAction(contract, SupabaseGameApiActions.RejectSession);
            AssertAction(contract, SupabaseGameApiActions.SubmitAchievement);
            AssertAction(contract, SupabaseGameApiActions.ApproveAchievement);
            AssertAction(contract, SupabaseGameApiActions.RejectAchievement);
            AssertAction(contract, SupabaseGameApiActions.RegisterProduct);
            AssertAction(contract, SupabaseGameApiActions.HideProduct);
            AssertAction(contract, SupabaseGameApiActions.CosmeticInventory);
            AssertAction(contract, SupabaseGameApiActions.RollCosmeticGacha);
            AssertAction(contract, SupabaseGameApiActions.EquipCosmetic);
            AssertAction(contract, SupabaseGameApiActions.BattleAction);
            AssertAction(contract, SupabaseGameApiActions.StartBattle);
            AssertAction(contract, SupabaseGameApiActions.ResetBattle);
            AssertAction(contract, SupabaseGameApiActions.SetBossHp);
            AssertAction(contract, SupabaseGameApiActions.BattleResult);
            AssertAction(contract, SupabaseGameApiActions.Rankings);
        }

        [Test]
        public void NewPublicClientDefaultsToVersionedGameApiEndpoint()
        {
            var config = ValidConfig();
            config.ApiFunctionName = null;
            config.ApiPath = null;
            var client = new SupabaseGameClient();

            Assert.IsTrue(client.TryConfigureFromJson(JsonUtility.ToJson(config)));
            Assert.AreEqual("/functions/v1/game-api-v2", client.ApiPath);
            Assert.AreEqual("https://example.supabase.co/functions/v1/game-api-v2", client.ApiEndpoint);
        }

        [Test]
        public void ExplicitSafeApiPathOverridesFunctionName()
        {
            var config = ValidConfig();
            config.ApiFunctionName = "ignored-name";
            config.ApiPath = "/functions/v1/game-api-preview_3";
            var client = new SupabaseGameClient();

            Assert.IsTrue(client.TryConfigureFromJson(JsonUtility.ToJson(config)));
            Assert.AreEqual("/functions/v1/game-api-preview_3", client.ApiPath);
        }

        [TestCase("https://attacker.example/functions/v1/game-api-v2")]
        [TestCase("/functions/v1/../admin")]
        [TestCase("/functions/v1/game-api-v2?debug=1")]
        [TestCase("/rest/v1/private")]
        public void UnsafeApiPathIsRejected(string unsafePath)
        {
            var config = ValidConfig();
            config.ApiPath = unsafePath;
            var client = new SupabaseGameClient();

            Assert.IsFalse(client.TryConfigureFromJson(JsonUtility.ToJson(config)));
            Assert.AreEqual(SupabaseApiErrorKind.Configuration, client.LastApiError.Kind);
            Assert.AreEqual(string.Empty, client.ApiEndpoint);
        }

        [TestCase("http://example.supabase.co")]
        [TestCase("https://user:password@example.supabase.co")]
        [TestCase("https://example.supabase.co/rest/v1")]
        [TestCase("https://example.supabase.co?redirect=https://attacker.example")]
        [TestCase("https://127.0.0.1")]
        [TestCase("https://supabase.local")]
        public void RuntimeConfigRejectsNonOriginOrNonPublicSupabaseUrls(string unsafeUrl)
        {
            var config = ValidConfig();
            config.SupabaseUrl = unsafeUrl;
            var client = new SupabaseGameClient();

            Assert.IsFalse(client.TryConfigureFromJson(JsonUtility.ToJson(config)));
            Assert.AreEqual(SupabaseApiErrorKind.Configuration, client.LastApiError.Kind);
            Assert.AreEqual(string.Empty, client.ApiEndpoint);
        }

        [Test]
        public void ExternalProductUrlsRequirePublicHttpsButRetainQueryAndFragment()
        {
            Assert.IsTrue(RuntimeUrlSecurity.TryNormalizeExternalHttpsUrl(
                " https://example.com/play?id=42#controls ",
                out var normalized));
            Assert.AreEqual("https://example.com/play?id=42#controls", normalized);

            Assert.IsFalse(RuntimeUrlSecurity.TryNormalizeExternalHttpsUrl("http://example.com/play", out _));
            Assert.IsFalse(RuntimeUrlSecurity.TryNormalizeExternalHttpsUrl("https://user:secret@example.com/play", out _));
            Assert.IsFalse(RuntimeUrlSecurity.TryNormalizeExternalHttpsUrl("https://localhost/play", out _));
            Assert.IsFalse(RuntimeUrlSecurity.TryNormalizeExternalHttpsUrl("https://192.168.1.5/play", out _));
        }

        [Test]
        public void RuntimeConfigAcceptsOnlyPublishableKeys()
        {
            var publishableClient = new SupabaseGameClient();
            Assert.IsTrue(publishableClient.TryConfigureFromJson(JsonUtility.ToJson(ValidConfig())));

            var secretConfig = ValidConfig();
            secretConfig.SupabasePublishableKey = "sb_secret_not-a-real-key";
            var secretClient = new SupabaseGameClient();
            Assert.IsFalse(secretClient.TryConfigureFromJson(JsonUtility.ToJson(secretConfig)));
            Assert.AreEqual(SupabaseApiErrorKind.Configuration, secretClient.LastApiError.Kind);
            StringAssert.DoesNotContain(secretConfig.SupabasePublishableKey, secretClient.LastError);

            var serviceConfig = ValidConfig();
            serviceConfig.SupabasePublishableKey = "service_role_not-a-real-key";
            var serviceClient = new SupabaseGameClient();
            Assert.IsFalse(serviceClient.TryConfigureFromJson(JsonUtility.ToJson(serviceConfig)));
            StringAssert.DoesNotContain(serviceConfig.SupabasePublishableKey, serviceClient.LastError);
        }

        [Test]
        public void RuntimeConfigRejectsForbiddenServiceKeyFieldsEvenWhenPublishableKeyExists()
        {
            const string json = "{\"Enabled\":true,\"SupabaseUrl\":\"https://example.supabase.co\","
                + "\"SupabasePublishableKey\":\"sb_publishable_not-a-real-key\","
                + "\"SUPABASE_SERVICE_ROLE_KEY\":\"not-a-real-service-key\","
                + "\"ApiContractVersion\":\"2026-07-14.1\"}";
            var client = new SupabaseGameClient();

            Assert.IsFalse(client.TryConfigureFromJson(json));
            Assert.AreEqual(SupabaseApiErrorKind.Configuration, client.LastApiError.Kind);
            StringAssert.DoesNotContain("not-a-real-service-key", client.LastError);
        }

        [Test]
        public void ConfigAndResponseContractMismatchAreRejected()
        {
            var config = ValidConfig();
            config.ApiContractVersion = "2026-05-30";
            var client = new SupabaseGameClient();

            Assert.IsFalse(client.TryConfigureFromJson(JsonUtility.ToJson(config)));
            Assert.AreEqual(SupabaseApiErrorKind.ContractMismatch, client.LastApiError.Kind);
            Assert.AreEqual("contract_mismatch", client.LastApiError.Code);

            var oldResponse = SupabaseGameApiContract.ValidateResponseVersion("2026-05-30");
            var missingResponse = SupabaseGameApiContract.ValidateResponseVersion(null);
            var currentResponse = SupabaseGameApiContract.ValidateResponseVersion(SupabaseGameApiContract.CurrentVersion);

            Assert.AreEqual(SupabaseApiErrorKind.ContractMismatch, oldResponse.Kind);
            Assert.AreEqual(SupabaseApiErrorKind.ContractMismatch, missingResponse.Kind);
            Assert.AreEqual(SupabaseApiErrorKind.None, currentResponse.Kind);
        }

        [Test]
        public void TypedCosmeticRequestsSerializeCanonicalWireFields()
        {
            var inventoryJson = JsonUtility.ToJson(SupabaseGameApiRequestFactory.CosmeticInventory(" session-token "));
            var gachaKey = SupabaseIdempotencyKey.CreateCosmeticGachaKey();
            var gachaJson = JsonUtility.ToJson(SupabaseGameApiRequestFactory.RollCosmeticGacha("session-token", gachaKey));
            var equipJson = JsonUtility.ToJson(SupabaseGameApiRequestFactory.EquipCosmetic("session-token", "item-id"));

            StringAssert.Contains("\"Action\":\"cosmetic-inventory\"", inventoryJson);
            StringAssert.Contains("\"ContractVersion\":\"2026-07-14.1\"", inventoryJson);
            StringAssert.Contains("\"SessionToken\":\"session-token\"", inventoryJson);
            StringAssert.DoesNotContain("Password", inventoryJson);

            var gacha = JsonUtility.FromJson<RollCosmeticGachaRequestDto>(gachaJson);
            Assert.AreEqual(SupabaseGameApiActions.RollCosmeticGacha, gacha.Action);
            Assert.AreEqual(gachaKey, gacha.IdempotencyKey);

            var equip = JsonUtility.FromJson<EquipCosmeticRequestDto>(equipJson);
            Assert.AreEqual(SupabaseGameApiActions.EquipCosmetic, equip.Action);
            Assert.AreEqual("item-id", equip.ItemId);
        }

        [Test]
        public void LogoutRequestCarriesTheCurrentSessionToTheServer()
        {
            var request = SupabaseGameApiRequestFactory.Logout(" session-token ");
            var json = JsonUtility.ToJson(request);
            var roundTrip = JsonUtility.FromJson<SupabaseGameApiRequestDto>(json);

            Assert.AreEqual(SupabaseGameApiActions.Logout, roundTrip.Action);
            Assert.AreEqual("session-token", roundTrip.SessionToken);
            Assert.AreEqual(SupabaseGameApiContract.CurrentVersion, roundTrip.ContractVersion);
        }

        [Test]
        public void PendingGachaOperationSurvivesReloadStyleReadsUntilAuthoritativeCompletion()
        {
            var firstUserId = $"gacha-retry-a-{Guid.NewGuid():N}";
            var secondUserId = $"gacha-retry-b-{Guid.NewGuid():N}";
            try
            {
                var firstKey = CosmeticGachaPendingOperationStore.GetOrCreate(firstUserId);
                var reloadedKey = CosmeticGachaPendingOperationStore.GetOrCreate(firstUserId);
                var otherUserKey = CosmeticGachaPendingOperationStore.GetOrCreate(secondUserId);

                Assert.AreEqual(firstKey, reloadedKey);
                Assert.AreNotEqual(firstKey, otherUserKey);
                Assert.IsFalse(CosmeticGachaPendingOperationStore.Complete(firstUserId, otherUserKey));
                Assert.IsTrue(CosmeticGachaPendingOperationStore.TryGet(firstUserId, out var stillPending));
                Assert.AreEqual(firstKey, stillPending);

                Assert.IsTrue(CosmeticGachaPendingOperationStore.Complete(firstUserId, firstKey));
                Assert.IsFalse(CosmeticGachaPendingOperationStore.TryGet(firstUserId, out _));
                Assert.AreNotEqual(firstKey, CosmeticGachaPendingOperationStore.GetOrCreate(firstUserId));
            }
            finally
            {
                CosmeticGachaPendingOperationStore.ClearForUser(firstUserId);
                CosmeticGachaPendingOperationStore.ClearForUser(secondUserId);
            }
        }

        [Test]
        public void PendingBattleActionRetriesTheExactOriginalPayloadUntilAuthoritativeCompletion()
        {
            var userId = $"battle-retry-{Guid.NewGuid():N}";
            try
            {
                var first = BattleActionPendingOperationStore.GetOrCreate(
                    userId,
                    BattleRole.Healer,
                    WeaponKind.Rifle,
                    BattleActionType.Support);
                var retryAfterDifferentUiSelection = BattleActionPendingOperationStore.GetOrCreate(
                    userId,
                    BattleRole.Attacker,
                    WeaponKind.Cannon,
                    BattleActionType.FullPower);

                Assert.AreEqual(first.IdempotencyKey, retryAfterDifferentUiSelection.IdempotencyKey);
                Assert.AreEqual(BattleRole.Healer, retryAfterDifferentUiSelection.Role);
                Assert.AreEqual(WeaponKind.Rifle, retryAfterDifferentUiSelection.Weapon);
                Assert.AreEqual(BattleActionType.Support, retryAfterDifferentUiSelection.ActionType);
                Assert.IsFalse(BattleActionPendingOperationStore.Complete(userId, "aor:battle:wrong-key"));
                Assert.IsTrue(BattleActionPendingOperationStore.TryGet(userId, out _));
                Assert.IsTrue(BattleActionPendingOperationStore.Complete(userId, first.IdempotencyKey));
                Assert.IsFalse(BattleActionPendingOperationStore.TryGet(userId, out _));
            }
            finally
            {
                BattleActionPendingOperationStore.ClearForUser(userId);
            }
        }

        [Test]
        public void PendingRaidResetRetriesTheExactOriginalEpochUntilAuthoritativeCompletion()
        {
            var userId = $"reset-retry-{Guid.NewGuid():N}";
            const string originalEpoch = "07ab00cf-0eb1-4a6b-9a16-a5e7dc77f11b";
            const string laterEpoch = "11fb37d8-9cee-411c-85b3-03f43854c82f";
            try
            {
                var first = RaidResetPendingOperationStore.GetOrCreate(userId, originalEpoch);
                var retryAfterSnapshotAdvanced = RaidResetPendingOperationStore.GetOrCreate(userId, laterEpoch);

                Assert.AreEqual(first.IdempotencyKey, retryAfterSnapshotAdvanced.IdempotencyKey);
                Assert.AreEqual(originalEpoch, retryAfterSnapshotAdvanced.ExpectedRaidEpoch);
                StringAssert.StartsWith("aor:reset:", first.IdempotencyKey);
                Assert.IsFalse(RaidResetPendingOperationStore.Complete(userId, "aor:reset:wrong-key"));
                Assert.IsTrue(RaidResetPendingOperationStore.TryGet(userId, out _));
                Assert.IsTrue(RaidResetPendingOperationStore.Complete(userId, first.IdempotencyKey));
                Assert.IsFalse(RaidResetPendingOperationStore.TryGet(userId, out _));
            }
            finally
            {
                RaidResetPendingOperationStore.ClearForUser(userId);
            }
        }

        [Test]
        public void OnlyDefinitiveRaidRejectionsDiscardAnAmbiguousPendingAction()
        {
            Assert.IsTrue(BattleActionPendingOperationStore.IsDefinitiveNonCommitFailure("idempotency_key_conflict"));
            Assert.IsTrue(BattleActionPendingOperationStore.IsDefinitiveNonCommitFailure("battle_turns_exhausted"));
            Assert.IsTrue(BattleActionPendingOperationStore.IsDefinitiveNonCommitFailure("raid_turn_already_committed"));
            Assert.IsTrue(BattleActionPendingOperationStore.IsDefinitiveNonCommitFailure("weapon_not_unlocked"));
            Assert.IsFalse(BattleActionPendingOperationStore.IsDefinitiveNonCommitFailure("rate_limited"));
            Assert.IsFalse(BattleActionPendingOperationStore.IsDefinitiveNonCommitFailure("network"));
            Assert.IsFalse(BattleActionPendingOperationStore.IsDefinitiveNonCommitFailure(null));

            Assert.IsTrue(RaidResetPendingOperationStore.IsDefinitiveNonCommitFailure("battle_epoch_conflict"));
            Assert.IsTrue(RaidResetPendingOperationStore.IsDefinitiveNonCommitFailure("idempotency_key_conflict"));
            Assert.IsFalse(RaidResetPendingOperationStore.IsDefinitiveNonCommitFailure("rate_limited"));
            Assert.IsFalse(RaidResetPendingOperationStore.IsDefinitiveNonCommitFailure("network"));
        }

        [Test]
        public void BattleActionSerializationRetainsCallerIdempotencyKeyForRetry()
        {
            const string retryKey = "aor:battle:retry-fixture-0001";
            var request = SupabaseGameApiRequestFactory.BattleAction("session", retryKey, 2, 3, 1);
            var json = JsonUtility.ToJson(request);
            var roundTrip = JsonUtility.FromJson<BattleActionRequestDto>(json);

            Assert.AreEqual(SupabaseGameApiActions.BattleAction, roundTrip.Action);
            Assert.AreEqual(retryKey, roundTrip.IdempotencyKey);
            Assert.AreEqual(2, roundTrip.Role);
            Assert.AreEqual(3, roundTrip.Weapon);
            Assert.AreEqual(1, roundTrip.ActionType);
        }

        [Test]
        public void ResetBattleSerializationCarriesIdempotencyKeyAndExpectedEpoch()
        {
            const string retryKey = "aor:reset:retry-fixture-0001";
            const string expectedEpoch = "07ab00cf-0eb1-4a6b-9a16-a5e7dc77f11b";
            var request = SupabaseGameApiRequestFactory.ResetBattle(" session ", retryKey, expectedEpoch);
            var roundTrip = JsonUtility.FromJson<ResetBattleRequestDto>(JsonUtility.ToJson(request));

            Assert.AreEqual(SupabaseGameApiActions.ResetBattle, roundTrip.Action);
            Assert.AreEqual(SupabaseGameApiContract.CurrentVersion, roundTrip.ContractVersion);
            Assert.AreEqual("session", roundTrip.SessionToken);
            Assert.AreEqual(retryKey, roundTrip.IdempotencyKey);
            Assert.AreEqual(expectedEpoch, roundTrip.ExpectedRaidEpoch);
        }

        [Test]
        public void ResetBattleSuccessRequiresMatchingResultAndDeltaEpochs()
        {
            const string battleId = "fb49b115-1ebd-434d-910c-4f4526bb8bc2";
            const string previousEpoch = "07ab00cf-0eb1-4a6b-9a16-a5e7dc77f11b";
            const string nextEpoch = "11fb37d8-9cee-411c-85b3-03f43854c82f";
            var response = new ResetBattleResponseDto
            {
                Ok = true,
                ResetResult = new ResetBattleResultDto
                {
                    BattleId = battleId,
                    PreviousRaidEpoch = previousEpoch,
                    RaidEpoch = nextEpoch,
                    BossName = "メンター・かみむー",
                    BossType = "コードマスター"
                },
                BattleDelta = new BattleStateDeltaDto
                {
                    Id = battleId,
                    RaidEpoch = nextEpoch,
                    BossName = "メンター・かみむー",
                    BossType = "コードマスター"
                }
            };

            Assert.IsTrue(SupabaseResetBattleContract.IsValidSuccessResponse(response, previousEpoch));

            response.BattleDelta.RaidEpoch = previousEpoch;
            Assert.IsFalse(SupabaseResetBattleContract.IsValidSuccessResponse(response, previousEpoch));
        }

        [Test]
        public void TypedCosmeticResponsesDeserializeRpcAndInventoryShapes()
        {
            const string json = "{\"Ok\":true,\"ContractVersion\":\"2026-07-14.1\","
                + "\"GachaResult\":{\"drawId\":\"draw-1\",\"itemId\":\"item-1\",\"code\":\"leaf_hat\","
                + "\"label\":\"Leaf Hat\",\"slot\":\"head\",\"rarity\":\"rare\","
                + "\"unityAssetKey\":\"Cosmetics/LeafHat\",\"wasDuplicate\":true,\"quantity\":2,"
                + "\"remainingCredits\":4,\"replayed\":false},"
                + "\"Cosmetics\":{\"AvailableCredits\":4,\"LifetimeEarned\":7,\"LifetimeSpent\":3,\"Version\":9,"
                + "\"Catalog\":[{\"Id\":\"item-1\",\"Code\":\"leaf_hat\",\"Label\":\"Leaf Hat\","
                + "\"Description\":\"A\",\"Slot\":\"head\",\"Rarity\":\"rare\",\"UnityAssetKey\":\"Cosmetics/LeafHat\"}],"
                + "\"Owned\":[],\"Equipped\":[]}}";

            var response = JsonUtility.FromJson<RollCosmeticGachaResponseDto>(json);

            Assert.IsTrue(response.Ok);
            Assert.AreEqual("draw-1", response.GachaResult.drawId);
            Assert.IsTrue(response.GachaResult.wasDuplicate);
            Assert.AreEqual(4, response.GachaResult.remainingCredits);
            Assert.AreEqual(9, response.Cosmetics.Version);
            Assert.AreEqual("Cosmetics/LeafHat", response.Cosmetics.Catalog[0].UnityAssetKey);
        }

        [Test]
        public void EquipAndBattleResponsesDeserializeTypedResults()
        {
            const string equipJson = "{\"Ok\":true,\"ContractVersion\":\"2026-07-14.1\","
                + "\"EquippedCosmetic\":{\"itemId\":\"item-2\",\"code\":\"cape\",\"label\":\"Cape\","
                + "\"slot\":\"back\",\"unityAssetKey\":\"Cosmetics/Cape\",\"equipped\":true},"
                + "\"Cosmetics\":{\"AvailableCredits\":2,\"Catalog\":[],\"Owned\":[],\"Equipped\":[]}}";
            const string battleJson = "{\"Ok\":true,\"ContractVersion\":\"2026-07-14.1\","
                + "\"ActionResult\":{\"UserId\":\"member-1\",\"Role\":1,\"Weapon\":2,\"ActionType\":0,"
                + "\"TurnNumber\":3,\"Damage\":42,\"Replayed\":true},"
                + "\"BattleDelta\":{\"Id\":\"battle-1\",\"CurrentHp\":1200,\"MaxHp\":2500}}";

            var equip = JsonUtility.FromJson<EquipCosmeticResponseDto>(equipJson);
            var battle = JsonUtility.FromJson<BattleActionResponseDto>(battleJson);

            Assert.IsTrue(equip.EquippedCosmetic.equipped);
            Assert.AreEqual("Cosmetics/Cape", equip.EquippedCosmetic.unityAssetKey);
            Assert.AreEqual(42, battle.ActionResult.Damage);
            Assert.IsTrue(battle.ActionResult.Replayed);
            Assert.AreEqual(1200, battle.BattleDelta.CurrentHp);
        }

        [Test]
        public void GeneratedIdempotencyKeysAreUniqueAndDatabaseCompatible()
        {
            var keys = new HashSet<string>();

            for (var index = 0; index < 256; index++)
            {
                var gachaKey = SupabaseIdempotencyKey.CreateCosmeticGachaKey();
                var battleKey = SupabaseIdempotencyKey.CreateBattleActionKey();
                var resetKey = SupabaseIdempotencyKey.CreateBattleResetKey();
                Assert.IsTrue(SupabaseIdempotencyKey.IsValid(gachaKey), gachaKey);
                Assert.IsTrue(SupabaseIdempotencyKey.IsValid(battleKey), battleKey);
                Assert.IsTrue(SupabaseIdempotencyKey.IsValid(resetKey), resetKey);
                StringAssert.StartsWith("aor:gacha:", gachaKey);
                StringAssert.StartsWith("aor:battle:", battleKey);
                StringAssert.StartsWith("aor:reset:", resetKey);
                Assert.IsTrue(keys.Add(gachaKey), gachaKey);
                Assert.IsTrue(keys.Add(battleKey), battleKey);
                Assert.IsTrue(keys.Add(resetKey), resetKey);
            }

            Assert.IsFalse(SupabaseIdempotencyKey.IsValid("short"));
            Assert.IsFalse(SupabaseIdempotencyKey.IsValid("invalid key with spaces"));
            Assert.IsFalse(SupabaseIdempotencyKey.IsValid(new string('x', 129)));
        }

        [Test]
        public void RestoredSessionTokensAreBoundedPrintableBearerValues()
        {
            var client = new SupabaseGameClient();
            client.RestoreSessionToken(" valid-session-token ");
            Assert.AreEqual("valid-session-token", client.SessionToken);

            client.RestoreSessionToken("contains a space");
            Assert.IsFalse(client.HasSession);
            client.RestoreSessionToken(new string('x', SupabaseSessionToken.MaximumLength + 1));
            Assert.IsFalse(client.HasSession);
            client.RestoreSessionToken("line\nbreak");
            Assert.IsFalse(client.HasSession);
        }

        [Test]
        public void IsolatedRevocationClientRetainsOnlyTheCapturedValidatedSession()
        {
            var client = new SupabaseGameClient();
            Assert.IsTrue(client.TryConfigureFromJson(JsonUtility.ToJson(ValidConfig())));
            client.RestoreSessionToken("captured-session-token");

            var revocation = client.CreateSessionRevocationClient();
            Assert.IsNotNull(revocation);
            Assert.AreNotSame(client, revocation);
            Assert.IsTrue(revocation.IsConfigured);
            Assert.AreEqual(client.ApiEndpoint, revocation.ApiEndpoint);
            Assert.AreEqual("captured-session-token", revocation.SessionToken);

            client.ClearSession();
            Assert.IsFalse(client.HasSession);
            Assert.AreEqual(
                "captured-session-token",
                revocation.SessionToken,
                "Immediate local logout must not erase the detached server-revocation bearer.");
        }

        [Test]
        public void AnonymousConfiguredCloneIsLoginReadyWhileOriginalClientIsBusy()
        {
            var client = new SupabaseGameClient();
            Assert.IsTrue(client.TryConfigureFromJson(JsonUtility.ToJson(ValidConfig())));
            client.RestoreSessionToken("authenticated-session-token");

            var begin = typeof(SupabaseGameClient).GetMethod(
                "BeginRequestBusyLease",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(begin);
            var lease = (IDisposable)begin.Invoke(client, null);
            try
            {
                Assert.IsTrue(client.IsBusy);
                var anonymous = client.CreateAnonymousClient();
                Assert.IsNotNull(anonymous);
                Assert.AreNotSame(client, anonymous);
                Assert.IsTrue(anonymous.IsConfigured);
                Assert.AreEqual(client.ApiEndpoint, anonymous.ApiEndpoint);
                Assert.IsFalse(anonymous.IsBusy);
                Assert.IsFalse(anonymous.HasSession);
            }
            finally
            {
                lease.Dispose();
            }
        }

        [Test]
        public void CanonicalErrorFieldIsClassifiedWithoutLegacyErrorCode()
        {
            var mismatch = SupabaseApiError.FromResponse(new SupabaseGameApiResponseDto
            {
                Ok = false,
                ContractVersion = SupabaseGameApiContract.CurrentVersion,
                Error = "contract_mismatch"
            }, 409);
            var rateLimited = SupabaseApiError.FromResponse(new SupabaseGameApiResponseDto
            {
                Ok = false,
                ContractVersion = SupabaseGameApiContract.CurrentVersion,
                Error = "rate_limited",
                RetryAfterSeconds = 3
            }, 429);

            Assert.AreEqual(SupabaseApiErrorKind.ContractMismatch, mismatch.Kind);
            Assert.AreEqual(SupabaseApiErrorKind.Server, rateLimited.Kind);
            Assert.IsTrue(rateLimited.CanRetry);
            Assert.AreEqual(3, rateLimited.RetryAfterSeconds);

            var resetConflict = SupabaseApiError.FromResponse(new SupabaseGameApiResponseDto
            {
                Ok = false,
                Error = "battle_epoch_conflict"
            }, 409);
            Assert.AreEqual(SupabaseApiErrorKind.Conflict, resetConflict.Kind);
            Assert.AreEqual("battle_epoch_conflict", resetConflict.Code);
        }

        private static SupabaseRuntimeConfigDto ValidConfig()
        {
            return new SupabaseRuntimeConfigDto
            {
                Enabled = true,
                SupabaseUrl = "https://example.supabase.co/",
                SupabasePublishableKey = "sb_publishable_not-a-real-key",
                ApiContractVersion = SupabaseGameApiContract.CurrentVersion,
                ApiFunctionName = SupabaseGameApiContract.DefaultFunctionName,
                UseDemoRepositoryFallback = false
            };
        }

        private static void AssertAction(string contract, string action)
        {
            StringAssert.Contains($"\"{action}\"", contract);
        }
    }
}
