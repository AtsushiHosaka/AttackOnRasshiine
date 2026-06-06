using System.IO;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class SupabaseProjectConfigurationEditModeTests
    {
        private const string ConfigPath = "supabase/config.toml";
        private const string SeedPath = "supabase/seed.sql";
        private const string InitialMigrationPath = "supabase/migrations/20260606115000_initial_game_schema.sql";

        [Test]
        public void SupabaseProjectScaffoldExists()
        {
            Assert.IsTrue(File.Exists(ConfigPath), $"{ConfigPath} should exist.");
            Assert.IsTrue(File.Exists(SeedPath), $"{SeedPath} should exist.");
            Assert.IsTrue(File.Exists(InitialMigrationPath), $"{InitialMigrationPath} should exist.");
        }

        [Test]
        public void SupabaseProjectConfigTargetsLocalGameApiDevelopment()
        {
            var config = File.ReadAllText(ConfigPath);

            StringAssert.Contains("project_id = \"attack-on-rasshiine\"", config);
            StringAssert.Contains("schemas = [\"public\", \"graphql_public\"]", config);
            StringAssert.Contains("[db.migrations]", config);
            StringAssert.Contains("enabled = true", config);
            StringAssert.Contains("sql_paths = [\"./seed.sql\"]", config);
            StringAssert.Contains("[functions.game-api]", config);
        }
    }
}
