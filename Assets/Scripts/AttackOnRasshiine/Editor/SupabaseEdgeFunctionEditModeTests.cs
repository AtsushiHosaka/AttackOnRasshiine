using System.IO;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;
using UnityEngine;

namespace AttackOnRasshiine.Editor
{
    public sealed class SupabaseEdgeFunctionEditModeTests
    {
        private static string FunctionRoot => Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "..",
            "..",
            "AttackOnRasshiineSupabase",
            "supabase",
            "functions",
            SupabaseGameApiContract.DefaultFunctionName));

        [Test]
        public void VersionedGameApiEdgeFunctionScaffoldExists()
        {
            AssertFileExists("index.ts");
            AssertFileExists("contract.ts");
            AssertFileExists("types.ts");
            AssertFileExists("http.ts");
            AssertFileExists("handlers/account.ts");
            AssertFileExists("handlers/sessions.ts");
            AssertFileExists("evaluation-dispatch.ts");
            AssertFileExists("handlers/cosmetics.ts");
            AssertFileExists("handlers/battle.ts");
        }

        [Test]
        public void VersionedGameApiMatchesUnityContractAndActions()
        {
            var index = Read("index.ts");
            var contract = Read("contract.ts");
            var http = Read("http.ts");

            StringAssert.Contains($"contractVersion = \"{SupabaseGameApiContract.CurrentVersion}\"", contract);
            StringAssert.Contains("Deno.serve", index);
            StringAssert.Contains("requestContractVersion", index);
            StringAssert.Contains("contract_mismatch", index);
            StringAssert.Contains("ContractVersion: contractVersion", http);
            StringAssert.Contains("Access-Control-Allow-Origin", Read("constants.ts"));

            AssertActionCovered(index, SupabaseGameApiActions.Health);
            AssertActionCovered(index, SupabaseGameApiActions.Login);
            AssertActionCovered(index, SupabaseGameApiActions.RestoreSession);
            AssertActionCovered(index, SupabaseGameApiActions.ChangePassword);
            AssertActionCovered(index, SupabaseGameApiActions.CreateAccount);
            AssertActionCovered(index, SupabaseGameApiActions.IssueTemporaryPassword);
            AssertActionCovered(index, SupabaseGameApiActions.Logout);
            AssertActionCovered(index, SupabaseGameApiActions.Snapshot);
            AssertActionCovered(index, SupabaseGameApiActions.FrontDisplaySnapshot);
            AssertActionCovered(index, SupabaseGameApiActions.StartSession);
            AssertActionCovered(index, SupabaseGameApiActions.CompleteSession);
            AssertActionCovered(index, SupabaseGameApiActions.SessionHistory);
            AssertActionCovered(index, SupabaseGameApiActions.ApproveSession);
            AssertActionCovered(index, SupabaseGameApiActions.RejectSession);
            AssertActionCovered(index, SupabaseGameApiActions.SubmitAchievement);
            AssertActionCovered(index, SupabaseGameApiActions.ApproveAchievement);
            AssertActionCovered(index, SupabaseGameApiActions.RejectAchievement);
            AssertActionCovered(index, SupabaseGameApiActions.RegisterProduct);
            AssertActionCovered(index, SupabaseGameApiActions.HideProduct);
            AssertActionCovered(index, SupabaseGameApiActions.CosmeticInventory);
            AssertActionCovered(index, SupabaseGameApiActions.RollCosmeticGacha);
            AssertActionCovered(index, SupabaseGameApiActions.EquipCosmetic);
            AssertActionCovered(index, SupabaseGameApiActions.BattleAction);
            AssertActionCovered(index, SupabaseGameApiActions.StartBattle);
            AssertActionCovered(index, SupabaseGameApiActions.ResetBattle);
            AssertActionCovered(index, SupabaseGameApiActions.SetBossHp);
            AssertActionCovered(index, SupabaseGameApiActions.BattleResult);
            AssertActionCovered(index, SupabaseGameApiActions.Rankings);
        }

        [Test]
        public void CompleteSessionPersistsEvaluationWithFallback()
        {
            var sessions = Read("handlers/sessions.ts");
            var evaluation = Read("evaluation.ts");
            var dispatch = Read("evaluation-dispatch.ts");

            StringAssert.Contains("handleCompleteSession", sessions);
            StringAssert.Contains("complete_dev_session", sessions);
            StringAssert.Contains("assertEvaluationDispatchReady();", sessions);
            StringAssert.Contains("await dispatchSessionEvaluation(session.id, evaluateSessionToTerminal)", sessions);
            StringAssert.Contains("DENO_DEPLOYMENT_ID", dispatch);
            StringAssert.Contains("runtime.waitUntil(promise)", dispatch);
            StringAssert.Contains("await taskRunner(devSessionId)", dispatch);
            StringAssert.Contains("/functions/v1/evaluate-dev-session", evaluation);
            StringAssert.Contains("evaluateHeuristically", evaluation);
            StringAssert.Contains("record_ai_evaluation_result", evaluation);
            StringAssert.Contains("record_ai_evaluation_failure", evaluation);
            StringAssert.Contains("axis_scores", evaluation);
            StringAssert.Contains("failure_reason", evaluation);
            StringAssert.Contains("game-api-heuristic-fallback", evaluation);
        }

        [Test]
        public void LoginCosmeticsAndBattleMutationsUsePersistentSupabaseOperations()
        {
            var account = Read("handlers/account.ts");
            var cosmetics = Read("handlers/cosmetics.ts");
            var battle = Read("handlers/battle.ts");
            var config = Read("config.ts");

            StringAssert.Contains("handleLogin", account);
            StringAssert.Contains("authenticate_app_user", account);
            StringAssert.Contains("SessionToken", account);
            StringAssert.Contains("draw_cosmetic_gacha", cosmetics);
            StringAssert.Contains("equip_cosmetic_item", cosmetics);
            StringAssert.Contains("submit_raid_action_once", battle);
            StringAssert.Contains("idempotency_key_required", cosmetics);
            StringAssert.Contains("idempotency_key_required", battle);
            StringAssert.Contains("SUPABASE_URL", config);
            StringAssert.Contains("SUPABASE_SERVICE_ROLE_KEY", config);
        }

        private static string Read(string relativePath)
        {
            var path = Path.Combine(FunctionRoot, relativePath);
            Assert.IsTrue(File.Exists(path), path);
            return File.ReadAllText(path);
        }

        private static void AssertFileExists(string relativePath)
        {
            var path = Path.Combine(FunctionRoot, relativePath);
            Assert.IsTrue(File.Exists(path), path);
        }

        private static void AssertActionCovered(string source, string action)
        {
            StringAssert.Contains($"\"{action}\"", source);
        }
    }
}
