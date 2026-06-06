using System.IO;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class SupabaseSchemaMigrationEditModeTests
    {
        private const string MigrationPath = "supabase/migrations/20260606115000_initial_game_schema.sql";

        [Test]
        public void InitialMigrationDefinesRequiredTables()
        {
            var sql = LoadMigrationSql();
            var requiredTables = new[]
            {
                "users",
                "teams",
                "dev_sessions",
                "ai_evaluations",
                "character_stats",
                "boss_battles",
                "battle_actions",
                "achievements",
                "products"
            };

            foreach (var table in requiredTables)
            {
                StringAssert.Contains($"create table public.{table}", sql);
                StringAssert.Contains($"alter table public.{table} enable row level security", sql);
            }
        }

        [Test]
        public void InitialMigrationDefinesSpecificationEnums()
        {
            var sql = LoadMigrationSql();

            StringAssert.Contains("create type public.user_role as enum ('member', 'mentor')", sql);
            StringAssert.Contains("'in_progress', 'pending', 'approved', 'rejected', 'incomplete', 'needs_review', 'ai_pending'", sql);
            StringAssert.Contains("create type public.battle_role as enum ('attacker', 'healer', 'defender', 'supporter')", sql);
            StringAssert.Contains("create type public.achievement_status as enum ('pending', 'approved', 'rejected')", sql);
        }

        [Test]
        public void InitialMigrationDefinesCoreRelationships()
        {
            var sql = LoadMigrationSql();

            StringAssert.Contains("team_id uuid references public.teams(id)", sql);
            StringAssert.Contains("create unique index users_login_id_unique_idx on public.users (lower(login_id))", sql);
            StringAssert.Contains("user_id uuid not null references public.users(id)", sql);
            StringAssert.Contains("dev_session_id uuid not null references public.dev_sessions(id)", sql);
            StringAssert.Contains("battle_id uuid not null references public.boss_battles(id)", sql);
            StringAssert.Contains("created_by uuid not null references public.users(id)", sql);
            StringAssert.Contains("hidden_by uuid references public.users(id)", sql);
        }

        private static string LoadMigrationSql()
        {
            Assert.IsTrue(File.Exists(MigrationPath), $"{MigrationPath} should exist.");
            return File.ReadAllText(MigrationPath).ToLowerInvariant();
        }
    }
}
