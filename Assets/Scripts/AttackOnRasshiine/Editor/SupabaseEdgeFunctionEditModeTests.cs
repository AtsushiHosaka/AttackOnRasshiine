using System.IO;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class SupabaseEdgeFunctionEditModeTests
    {
        private const string GameApiFunctionPath = "supabase/functions/game-api/index.ts";

        [Test]
        public void GameApiEdgeFunctionScaffoldExists()
        {
            Assert.IsTrue(File.Exists(GameApiFunctionPath), $"{GameApiFunctionPath} should exist.");
        }

        [Test]
        public void GameApiEdgeFunctionMatchesUnityContract()
        {
            var source = File.ReadAllText(GameApiFunctionPath);

            StringAssert.Contains($"CURRENT_CONTRACT_VERSION = \"{SupabaseGameApiContract.CurrentVersion}\"", source);
            StringAssert.Contains("Deno.serve", source);
            StringAssert.Contains("Access-Control-Allow-Origin", source);
            StringAssert.Contains("contract_mismatch", source);
            StringAssert.Contains("invalid_session", source);
            StringAssert.Contains("server_error", source);
            StringAssert.Contains("front-display-snapshot", source);

            AssertActionCovered(source, SupabaseGameApiActions.Login);
            AssertActionCovered(source, SupabaseGameApiActions.ChangePassword);
            AssertActionCovered(source, SupabaseGameApiActions.CreateAccount);
            AssertActionCovered(source, SupabaseGameApiActions.IssueTemporaryPassword);
            AssertActionCovered(source, SupabaseGameApiActions.Snapshot);
            AssertActionCovered(source, SupabaseGameApiActions.FrontDisplaySnapshot);
            AssertActionCovered(source, SupabaseGameApiActions.StartSession);
            AssertActionCovered(source, SupabaseGameApiActions.CompleteSession);
            AssertActionCovered(source, SupabaseGameApiActions.ApproveSession);
            AssertActionCovered(source, SupabaseGameApiActions.RejectSession);
            AssertActionCovered(source, SupabaseGameApiActions.SubmitAchievement);
            AssertActionCovered(source, SupabaseGameApiActions.ApproveAchievement);
            AssertActionCovered(source, SupabaseGameApiActions.RejectAchievement);
            AssertActionCovered(source, SupabaseGameApiActions.RegisterProduct);
            AssertActionCovered(source, SupabaseGameApiActions.HideProduct);
            AssertActionCovered(source, SupabaseGameApiActions.BattleAction);
            AssertActionCovered(source, SupabaseGameApiActions.StartBattle);
            AssertActionCovered(source, SupabaseGameApiActions.ResetBattle);
            AssertActionCovered(source, SupabaseGameApiActions.SetBossHp);
        }

        private static void AssertActionCovered(string source, string action)
        {
            StringAssert.Contains($"\"{action}\"", source);
        }
    }
}
