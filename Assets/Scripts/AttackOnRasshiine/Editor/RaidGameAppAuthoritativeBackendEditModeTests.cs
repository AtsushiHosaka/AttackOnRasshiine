using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Scene;
using AttackOnRasshiine.Runtime.Services;
using AttackOnRasshiine.Runtime.UI;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class RaidGameAppAuthoritativeBackendEditModeTests
    {
        private const string AppPath =
            "Assets/Scripts/AttackOnRasshiine/Runtime/UI/RaidGameApp.cs";

        [Test]
        public void PlayerMutationsFailClosedWhenAuthoritativeBackendIsUnavailable()
        {
            var source = File.ReadAllText(AppPath);

            StringAssert.Contains("private bool HandleUnavailableAuthoritativeBackend()", source);
            StringAssert.Contains("#if UNITY_EDITOR", source);
            StringAssert.Contains("#elif UNITY_WEBGL && DEVELOPMENT_BUILD", source);
            StringAssert.Contains("if (webGlVisualQaActive)", source);
            StringAssert.Contains("本番APIとの接続を確認して、もう一度お試しください。", source);
            Assert.That(
                Regex.Matches(source, "return HandleUnavailableAuthoritativeBackend\\(\\);").Count,
                Is.EqualTo(13),
                "Every remote mutation/refresh entry point must consume the unavailable " +
                "backend condition instead of falling through to the local repository.");
        }

        [Test]
        public void InitialPasswordChangeCannotFallThroughToLocalRepositoryInPlayerBuilds()
        {
            var source = File.ReadAllText(AppPath);
            var methodStart = source.IndexOf("private void TryCompleteInitialPasswordChange()", System.StringComparison.Ordinal);
            var methodEnd = source.IndexOf("private IEnumerator ChangeRemotePassword", methodStart, System.StringComparison.Ordinal);
            Assert.That(methodStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(methodEnd, Is.GreaterThan(methodStart));

            var method = source.Substring(methodStart, methodEnd - methodStart);
            StringAssert.Contains("if (HandleUnavailableAuthoritativeBackend())", method);
            StringAssert.Contains("repository.ChangePassword", method);
            Assert.That(
                method.IndexOf("HandleUnavailableAuthoritativeBackend", System.StringComparison.Ordinal),
                Is.LessThan(method.IndexOf("repository.ChangePassword", System.StringComparison.Ordinal)));
        }

        [Test]
        public void PublicFrontRouteClearsAuthenticationAndAlwaysReloadsPublicProjection()
        {
            var bootstrap = File.ReadAllText(
                "Assets/Scripts/AttackOnRasshiine/Runtime/Scene/RasshiineSceneBootstrap.cs");
            var app = File.ReadAllText(AppPath);

            var route = ExtractMethod(bootstrap, "if (requestedScene == RasshiineProductionScene.FrontDisplay)",
                "if (requestedScene != RasshiineProductionScene.Battle)");
            StringAssert.Contains("PreparePublicFrontDisplaySession();", route);
            StringAssert.Contains("router.LoadScene(requestedScene);", route);
            StringAssert.DoesNotContain("sceneId != requestedScene", route);

            var awake = ExtractMethod(app, "private void Awake()", "private void Start()");
            var clearIndex = awake.IndexOf("RasshiineRuntimeSession.Clear();", System.StringComparison.Ordinal);
            var restoreIndex = awake.IndexOf("supabase.RestoreSessionToken", System.StringComparison.Ordinal);
            Assert.That(clearIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(clearIndex, Is.LessThan(restoreIndex));
        }

        [Test]
        public void PublicFrontSessionPreparationClearsSeededPrivateRuntimeState()
        {
            var repository = new LocalGameRepository();
            RasshiineRuntimeSession.SetUser(repository.Members[0]);
            RasshiineRuntimeSession.SetSnapshot(repository.CreateSnapshot());
            RasshiineRuntimeSession.SetSessionToken("valid-token");
            try
            {
                var prepare = typeof(RasshiineSceneBootstrap).GetMethod(
                    "PreparePublicFrontDisplaySession",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(prepare, Is.Not.Null);
                prepare.Invoke(null, null);

                Assert.That(RasshiineRuntimeSession.CurrentUser, Is.Null);
                Assert.That(RasshiineRuntimeSession.Snapshot, Is.Null);
                Assert.That(RasshiineRuntimeSession.SessionToken, Is.Empty);
            }
            finally
            {
                RasshiineRuntimeSession.Clear();
            }
        }

        [Test]
        public void PasswordRotationIsPersistedBeforeAuthenticatedSceneNavigation()
        {
            var source = File.ReadAllText(AppPath);
            var method = ExtractMethod(source, "private void CompleteInitialPasswordChange", "private bool TryLoadHomeScene");
            var tokenIndex = method.IndexOf("RasshiineRuntimeSession.SetSessionToken(supabase.SessionToken);", System.StringComparison.Ordinal);
            var navigationIndex = method.IndexOf("ShowPostLoginHome();", System.StringComparison.Ordinal);

            Assert.That(tokenIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(tokenIndex, Is.LessThan(navigationIndex));
        }

        [Test]
        public void PollCancellationUsesGenerationInvalidationInsteadOfInterruptingRequests()
        {
            var source = File.ReadAllText(AppPath);
            var client = File.ReadAllText(
                "Assets/Scripts/AttackOnRasshiine/Runtime/Services/SupabaseGameClient.cs");
            var battleCancel = ExtractMethod(source, "private void StopBattleStatePolling()", "private IEnumerator PollBattleState");
            var aiCancel = ExtractMethod(source, "private void CancelAiEvaluationSnapshotPolling()", "private IEnumerator PollAiEvaluationSnapshot");

            StringAssert.Contains("battleStatePollGeneration++;", battleCancel);
            StringAssert.Contains("aiEvaluationSnapshotPollGeneration++;", aiCancel);
            StringAssert.DoesNotContain("StopCoroutine", battleCancel);
            StringAssert.DoesNotContain("StopCoroutine", aiCancel);
            StringAssert.Contains("generation != battleStatePollGeneration", source);
            StringAssert.Contains("generation != aiEvaluationSnapshotPollGeneration", source);
            StringAssert.Contains("using var requestBusyLease = BeginRequestBusyLease();", client);
        }

        [Test]
        public void DisposingARequestBusyLeaseRestoresClientAvailability()
        {
            var client = new SupabaseGameClient();
            var begin = typeof(SupabaseGameClient).GetMethod(
                "BeginRequestBusyLease",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(begin, Is.Not.Null);

            var lease = (System.IDisposable)begin.Invoke(client, null);
            Assert.That(client.IsBusy, Is.True);

            lease.Dispose();
            Assert.That(client.IsBusy, Is.False);
            Assert.DoesNotThrow(lease.Dispose, "Disposal must be idempotent during nested coroutine teardown.");
        }

        [Test]
        public void FrontRefreshUsesASeparatePublicCacheForAuthenticatedProjectorPreview()
        {
            var source = File.ReadAllText(AppPath);
            var refresh = ExtractMethod(source, "private IEnumerator RefreshRemoteSnapshot", "private void RefreshFrontDisplayNow");

            StringAssert.Contains("ShouldUseSeparateFrontDisplayRepository", refresh);
            StringAssert.Contains("supabase.GetFrontDisplaySnapshot", refresh);
            StringAssert.Contains("supabase.GetSnapshot", refresh);
            StringAssert.Contains("ApplyRemoteFrontDisplayProjection(response);", refresh);
            StringAssert.Contains("ApplyRemoteSnapshot(response);", refresh);
        }

        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        public void FrontProjectionIsolationDecisionIsBehaviorallyStable(
            bool frontRequested,
            bool standaloneFront,
            bool expected)
        {
            var decision = typeof(RaidGameApp).GetMethod(
                "ShouldUseSeparateFrontDisplayRepository",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(decision, Is.Not.Null);
            Assert.That(
                (bool)decision.Invoke(null, new object[] { frontRequested, standaloneFront }),
                Is.EqualTo(expected));
        }

        [Test]
        public void ProjectorStageSelectsPublicBattleAndNeverAuthenticatedBattle()
        {
            var authenticated = new BossBattleState { Id = "private-battle" };
            var publicFront = new BossBattleState { Id = "public-battle" };
            var select = typeof(RaidGameApp).GetMethod(
                "SelectBattleStageState",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(select, Is.Not.Null);

            Assert.AreSame(
                publicFront,
                select.Invoke(null, new object[] { true, authenticated, publicFront }));
            Assert.AreSame(
                authenticated,
                select.Invoke(null, new object[] { false, authenticated, publicFront }));
            Assert.IsNull(
                select.Invoke(null, new object[] { true, authenticated, null }),
                "An empty public projection must clear the stage instead of retaining private participants.");
        }

        [Test]
        public void LogoutDuringBackgroundRequestImmediatelyClearsLocalAuthentication()
        {
            var source = File.ReadAllText(AppPath);
            var requestLogout = ExtractMethod(source, "private void RequestLogout()", "private IEnumerator LogoutRemoteThenShowLogin");
            var showLogin = ExtractMethod(source, "private void ShowLogin()", "private void CreateLoginSunRays");

            var busyIndex = requestLogout.IndexOf("if (isNetworkBusy)", System.StringComparison.Ordinal);
            var revokeIndex = requestLogout.IndexOf("SessionRevocationDispatcher.Enqueue", busyIndex, System.StringComparison.Ordinal);
            var immediateLoginIndex = requestLogout.IndexOf("ShowLogin();", busyIndex, System.StringComparison.Ordinal);
            Assert.That(revokeIndex, Is.GreaterThan(busyIndex));
            Assert.That(revokeIndex, Is.LessThan(immediateLoginIndex));
            Assert.That(immediateLoginIndex, Is.GreaterThan(busyIndex));
            var cloneIndex = showLogin.IndexOf("var anonymousLoginClient = supabase?.CreateAnonymousClient();", System.StringComparison.Ordinal);
            var stopIndex = showLogin.IndexOf("StopAllCoroutines();", System.StringComparison.Ordinal);
            var replaceIndex = showLogin.IndexOf("supabase = anonymousLoginClient;", System.StringComparison.Ordinal);
            Assert.That(cloneIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(cloneIndex, Is.LessThan(stopIndex));
            Assert.That(stopIndex, Is.LessThan(replaceIndex));
            StringAssert.Contains("isNetworkBusy = false;", showLogin);
            StringAssert.Contains("RasshiineRuntimeSession.Clear();", showLogin);
            StringAssert.Contains("supabase?.ClearSession();", showLogin);
        }

        [Test]
        public void FailedForegroundLogoutQueuesDetachedServerRevocationBeforeLocalClear()
        {
            var source = File.ReadAllText(AppPath);
            var logout = ExtractMethod(
                source,
                "private IEnumerator LogoutRemoteThenShowLogin",
                "private void ShowLogin()");

            var failureIndex = logout.IndexOf("if (response?.Ok != true)", System.StringComparison.Ordinal);
            var retryIndex = logout.IndexOf("SessionRevocationDispatcher.Enqueue", failureIndex, System.StringComparison.Ordinal);
            var clearIndex = logout.IndexOf("ShowLogin();", failureIndex, System.StringComparison.Ordinal);
            Assert.That(failureIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(retryIndex, Is.GreaterThan(failureIndex));
            Assert.That(retryIndex, Is.LessThan(clearIndex));
        }

        [Test]
        public void CosmeticRequestsShareTheGlobalNetworkBusyBoundary()
        {
            var source = File.ReadAllText(AppPath);

            StringAssert.Contains("if (cosmeticRequestInFlight || isNetworkBusy)", source);
            StringAssert.Contains("cosmeticRequestInFlight || isNetworkBusy || FindOwnedCosmetic", source);
            Assert.That(
                Regex.Matches(source, "cosmeticRequestInFlight = true;\\s+isNetworkBusy = true;").Count,
                Is.EqualTo(3));
            Assert.That(
                Regex.Matches(source, "isNetworkBusy = false;\\s+cosmeticRequestInFlight = false;").Count,
                Is.EqualTo(3));
        }

        private static string ExtractMethod(string source, string startMarker, string endMarker)
        {
            var start = source.IndexOf(startMarker, System.StringComparison.Ordinal);
            var end = source.IndexOf(endMarker, start, System.StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), startMarker);
            Assert.That(end, Is.GreaterThan(start), endMarker);
            return source.Substring(start, end - start);
        }
    }
}
