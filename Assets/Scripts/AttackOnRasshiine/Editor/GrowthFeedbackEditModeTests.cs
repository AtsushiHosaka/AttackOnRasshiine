using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class GrowthFeedbackEditModeTests
    {
        [Test]
        public void ApprovalRecordsExpLevelAndStatGrowthFeedback()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[0];
            var mentor = repository.Mentors[0];
            var stats = repository.GetStats(member.Id);
            stats.Level = 1;
            stats.Exp = 70;
            stats.RecalculateDerivedStats();
            var hpBefore = stats.Hp;
            var atkBefore = stats.Atk;
            var defBefore = stats.Def;
            var mpBefore = stats.Mp;
            repository.StartSession(member.Id, "成長フィードバックを確認する");
            var session = repository.CompleteSession(member.Id, 100, "実装と検証を行い改善した", "次も検証する");
            session.DurationMinutes = 120;
            session.Evaluation = new AiEvaluation
            {
                Rank = AiRank.A,
                TotalScore = 82,
                ExpMultiplier = 1.6f,
                Feedback = "成長フィードバック検証"
            };
            var expectedExp = session.PreviewExp;

            var approved = repository.ApproveSession(session.Id, mentor.Id, "成長確認");
            var feedback = approved.GrowthFeedback;

            Assert.IsNotNull(feedback);
            Assert.AreEqual(member.Id, feedback.UserId);
            Assert.AreEqual(expectedExp, feedback.ExpGained);
            Assert.AreEqual(1, feedback.LevelBefore);
            Assert.Greater(feedback.LevelAfter, feedback.LevelBefore);
            Assert.AreEqual(stats.Level, feedback.LevelAfter);
            Assert.AreEqual(stats.Exp, feedback.ExpAfter);
            Assert.AreEqual(stats.Hp - hpBefore, feedback.HpIncrease);
            Assert.AreEqual(stats.Atk - atkBefore, feedback.AtkIncrease);
            Assert.AreEqual(stats.Def - defBefore, feedback.DefIncrease);
            Assert.AreEqual(stats.Mp - mpBefore, feedback.MpIncrease);
            StringAssert.Contains("EXP +", feedback.Summary);
            StringAssert.Contains("Lv 1->", feedback.Summary);
            StringAssert.Contains("HP+", feedback.Summary);
        }

        [Test]
        public void ApprovalFeedbackIsNonBlockingWhenNoLevelUpOccurs()
        {
            var repository = new LocalGameRepository();
            var member = repository.Members[1];
            var mentor = repository.Mentors[0];
            var stats = repository.GetStats(member.Id);
            stats.Level = 4;
            stats.Exp = 0;
            stats.RecalculateDerivedStats();
            repository.StartSession(member.Id, "小さなEXP反映を確認する");
            var session = repository.CompleteSession(member.Id, 60, "短い検証をした", "次へ進む");
            session.DurationMinutes = 10;
            session.Evaluation = new AiEvaluation
            {
                Rank = AiRank.B,
                TotalScore = 70,
                ExpMultiplier = 1f,
                Feedback = "小さな成長"
            };

            var approved = repository.ApproveSession(session.Id, mentor.Id, "反映");

            Assert.IsNotNull(approved.GrowthFeedback);
            Assert.IsFalse(approved.GrowthFeedback.HasLevelUp);
            Assert.AreEqual(4, approved.GrowthFeedback.LevelAfter);
            Assert.AreEqual(13, approved.GrowthFeedback.ExpGained);
            StringAssert.Contains("Lv維持", approved.GrowthFeedback.Summary);
        }

        [Test]
        public void CharacterStatsLevelUpUsesRequiredExpBoundary()
        {
            var below = new CharacterStats { Level = 1, Exp = 73 };
            below.RecalculateDerivedStats();

            var belowLevels = below.AddExp(1);

            Assert.AreEqual(0, belowLevels);
            Assert.AreEqual(1, below.Level);
            Assert.AreEqual(74, below.Exp);

            var exact = new CharacterStats { Level = 1, Exp = 74 };
            exact.RecalculateDerivedStats();

            var exactLevels = exact.AddExp(1);

            Assert.AreEqual(1, exactLevels);
            Assert.AreEqual(2, exact.Level);
            Assert.AreEqual(0, exact.Exp);
            Assert.AreEqual(110, exact.Hp);
            Assert.AreEqual(12, exact.Atk);
            Assert.AreEqual(6, exact.Def);
            Assert.AreEqual(32, exact.Mp);
        }

        [Test]
        public void CharacterStatsAddExpSupportsMultipleLevelUps()
        {
            var stats = new CharacterStats { Level = 1, Exp = 0 };
            stats.RecalculateDerivedStats();

            var levels = stats.AddExp(301);

            Assert.AreEqual(3, levels);
            Assert.AreEqual(4, stats.Level);
            Assert.AreEqual(1, stats.Exp);
            Assert.AreEqual(130, stats.Hp);
            Assert.AreEqual(16, stats.Atk);
            Assert.AreEqual(8, stats.Def);
            Assert.AreEqual(36, stats.Mp);
        }
    }
}
