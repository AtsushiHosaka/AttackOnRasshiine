using System.IO;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class SupabaseProjectConfigurationEditModeTests
    {
        private const string LegacyConfigPath = "supabase/config.toml";
        private const string LegacyReadmePath = "supabase/README.md";
        private const string LegacyFunctionPath = "supabase/functions/game-api/index.ts";
        private const string LegacyHandlerPath = "supabase/functions/game-api/handler.ts";
        private const string LegacyHistoryPath = "supabase/history/legacy-game-api/index.ts";
        private const string LegacyMigrationDirectory = "supabase/migrations";
        private const string LegacyBlockerPath = LegacyMigrationDirectory
            + "/20000101000000_deprecated_backend_do_not_deploy.sql";
        private const string LegacyDeployPath = "supabase/scripts/deploy.sh";
        private const string CanonicalRoot = "../AttackOnRasshiineSupabase";
        private const string ConfigPath = CanonicalRoot + "/supabase/config.toml";
        private const string SeedPath = CanonicalRoot + "/supabase/seed.sql";
        private const string HardeningMigrationPath = CanonicalRoot
            + "/supabase/migrations/20260711181532_harden_app_auth_and_function_privileges.sql";

        [Test]
        public void SupabaseProjectScaffoldExists()
        {
            Assert.IsTrue(File.Exists(ConfigPath), $"{ConfigPath} should exist.");
            Assert.IsTrue(File.Exists(SeedPath), $"{SeedPath} should exist.");
            Assert.IsTrue(File.Exists(HardeningMigrationPath), $"{HardeningMigrationPath} should exist.");
        }

        [Test]
        public void CanonicalSupabaseProjectTargetsVersionedGameApi()
        {
            var config = File.ReadAllText(ConfigPath);

            StringAssert.Contains("project_id = \"attack-on-rasshiine\"", config);
            StringAssert.Contains("schemas = [\"public\", \"graphql_public\"]", config);
            StringAssert.Contains("[db.migrations]", config);
            StringAssert.Contains("enabled = true", config);
            StringAssert.Contains("sql_paths = [\"./seed.sql\"]", config);
            StringAssert.Contains("[functions.game-api-v2]", config);
        }

        [Test]
        public void EmbeddedLegacySupabaseProjectCannotDeploy()
        {
            Assert.IsTrue(File.Exists(LegacyReadmePath), LegacyReadmePath);
            Assert.IsTrue(File.Exists(LegacyFunctionPath), LegacyFunctionPath);
            Assert.IsTrue(File.Exists(LegacyHandlerPath), LegacyHandlerPath);
            Assert.IsTrue(File.Exists(LegacyHistoryPath), LegacyHistoryPath);
            Assert.IsTrue(File.Exists(LegacyBlockerPath), LegacyBlockerPath);
            Assert.IsTrue(File.Exists(LegacyDeployPath), LegacyDeployPath);
            var config = File.ReadAllText(LegacyConfigPath);
            var readme = File.ReadAllText(LegacyReadmePath);
            var entrypoint = File.ReadAllText(LegacyFunctionPath);
            var handler = File.ReadAllText(LegacyHandlerPath);
            var blocker = File.ReadAllText(LegacyBlockerPath);
            var deploy = File.ReadAllText(LegacyDeployPath);
            var deployableMigrations = Directory.GetFiles(LegacyMigrationDirectory, "*.sql");

            StringAssert.Contains("[db.migrations]", config);
            StringAssert.Contains("[functions.game-api]", config);
            Assert.That(CountOccurrences(config, "enabled = false"), Is.GreaterThanOrEqualTo(3));
            StringAssert.Contains("sql_paths = []", config);
            StringAssert.Contains("Deprecated backend", readme);
            StringAssert.Contains("AttackOnRasshiineSupabase", readme);
            StringAssert.Contains("game-api-v2", readme);
            StringAssert.Contains("Deno.serve(deprecatedGameApiResponse)", entrypoint);
            StringAssert.Contains("deprecated_backend", handler);
            StringAssert.Contains("status: 410", handler);
            StringAssert.DoesNotContain("SUPABASE_SERVICE_ROLE_KEY", entrypoint + handler);
            StringAssert.DoesNotContain("SUPABASE_SECRET_KEY", entrypoint + handler);
            StringAssert.Contains("deprecated_backend_do_not_deploy", blocker);
            StringAssert.Contains("exit 64", deploy);
            Assert.That(deployableMigrations, Has.Length.EqualTo(1));
            Assert.AreEqual(LegacyBlockerPath, deployableMigrations[0].Replace('\\', '/'));
            Assert.IsFalse(File.Exists("supabase/seed.sql"));
        }

        private static int CountOccurrences(string source, string value)
        {
            var count = 0;
            var offset = 0;
            while ((offset = source.IndexOf(value, offset, System.StringComparison.Ordinal)) >= 0)
            {
                count += 1;
                offset += value.Length;
            }
            return count;
        }
    }
}
