using System.IO;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class SupabaseSchemaMigrationEditModeTests
    {
        private const string MigrationPath = "supabase/migrations/20260606115000_initial_game_schema.sql";
        private const string EvaluationPersistenceMigrationPath = "supabase/migrations/20260606160000_persist_ai_evaluation_results.sql";
        private const string CharacterStatsGrowthMigrationPath = "supabase/migrations/20260606170000_recalculate_character_stats_from_level.sql";
        private const string RuntimeSchemaMigrationPath = "supabase/migrations/20260613225649_complete_game_api_runtime_schema.sql";

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

        [Test]
        public void EvaluationPersistenceMigrationLinksEvaluationsToSessions()
        {
            Assert.IsTrue(File.Exists(EvaluationPersistenceMigrationPath), $"{EvaluationPersistenceMigrationPath} should exist.");
            var sql = File.ReadAllText(EvaluationPersistenceMigrationPath).ToLowerInvariant();

            StringAssert.Contains("add column if not exists ai_evaluation_failure_reason text", sql);
            StringAssert.Contains("ai_evaluations_dev_session_id_unique", sql);
            StringAssert.Contains("unique (dev_session_id)", sql);
            StringAssert.Contains("ai_evaluations_rank_created_at_idx", sql);
        }

        [Test]
        public void CharacterStatsMigrationRecalculatesDerivedStatsFromLevel()
        {
            Assert.IsTrue(File.Exists(CharacterStatsGrowthMigrationPath), $"{CharacterStatsGrowthMigrationPath} should exist.");
            var sql = File.ReadAllText(CharacterStatsGrowthMigrationPath).ToLowerInvariant();

            StringAssert.Contains("recalculate_character_stats_from_level", sql);
            StringAssert.Contains("new.hp = 100 + (new.level - 1) * 10", sql);
            StringAssert.Contains("new.atk = 10 + (new.level - 1) * 2", sql);
            StringAssert.Contains("new.def = 5 + (new.level - 1)", sql);
            StringAssert.Contains("new.mp = 30 + (new.level - 1) * 2", sql);
            StringAssert.Contains("character_stats_recalculate_derived_stats", sql);
        }

        [Test]
        public void RuntimeSchemaMigrationCompletesUnityDtoPersistence()
        {
            Assert.IsTrue(File.Exists(RuntimeSchemaMigrationPath), $"{RuntimeSchemaMigrationPath} should exist.");
            var sql = File.ReadAllText(RuntimeSchemaMigrationPath).ToLowerInvariant();

            StringAssert.Contains("add column if not exists unlocked_weapons", sql);
            StringAssert.Contains("add column if not exists titles", sql);
            StringAssert.Contains("add column if not exists skills", sql);
            StringAssert.Contains("create table if not exists public.audit_logs", sql);
            StringAssert.Contains("create table if not exists public.battle_participants", sql);
            StringAssert.Contains("add column if not exists boss_def", sql);
            StringAssert.Contains("add column if not exists phase", sql);
            StringAssert.Contains("add column if not exists weapon_kind", sql);
            StringAssert.Contains("grant select, insert, update, delete on all tables in schema public to service_role", sql);
        }

        private static string LoadMigrationSql()
        {
            Assert.IsTrue(File.Exists(MigrationPath), $"{MigrationPath} should exist.");
            return File.ReadAllText(MigrationPath).ToLowerInvariant();
        }
    }
}
