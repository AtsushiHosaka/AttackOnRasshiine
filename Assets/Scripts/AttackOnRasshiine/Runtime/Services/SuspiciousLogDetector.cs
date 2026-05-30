using System.Collections.Generic;
using System.Linq;
using AttackOnRasshiine.Runtime.Data;

namespace AttackOnRasshiine.Runtime.Services
{
    public static class SuspiciousLogDetector
    {
        public const string LongSessionFlag = "長時間セッション";
        public const string ShortReflectionFlag = "振り返り不足";
        public const string ShortGoalFlag = "目標不足";
        public const string ShortNextTaskFlag = "次回内容不足";
        public const string NeedsReviewFlag = "要確認";

        private const int LongSessionMinutes = 180;
        private const int ZeroAchievementLongMinutes = 90;
        private const int DailyTotalReviewMinutes = 480;
        private const int ConsecutivePerfectThreshold = 3;
        private const int MinMeaningfulTextLength = 3;

        public static List<string> Detect(DevSession session, IEnumerable<DevSession> userSessions = null)
        {
            var flags = new List<string>();
            if (session == null)
            {
                return flags;
            }

            if (session.DurationMinutes >= LongSessionMinutes)
            {
                flags.Add(LongSessionFlag);
            }

            if (IsTooShort(session.Reflection))
            {
                flags.Add(ShortReflectionFlag);
            }

            if (IsTooShort(session.Goal))
            {
                flags.Add(ShortGoalFlag);
            }

            if (IsTooShort(session.NextTask))
            {
                flags.Add(ShortNextTaskFlag);
            }

            if (session.AchievementRate == 0 && session.DurationMinutes >= ZeroAchievementLongMinutes)
            {
                AddNeedsReview(flags);
            }

            var sameUserSessions = BuildSameUserSessionList(session, userSessions);
            if (HasTooManyConsecutivePerfectSessions(session, sameUserSessions))
            {
                AddNeedsReview(flags);
            }

            if (HasLongDailyTotal(session, sameUserSessions))
            {
                AddNeedsReview(flags);
            }

            return flags;
        }

        private static bool IsTooShort(string value)
        {
            return string.IsNullOrWhiteSpace(value) || value.Trim().Length <= MinMeaningfulTextLength;
        }

        private static void AddNeedsReview(ICollection<string> flags)
        {
            if (!flags.Contains(NeedsReviewFlag))
            {
                flags.Add(NeedsReviewFlag);
            }
        }

        private static List<DevSession> BuildSameUserSessionList(DevSession session, IEnumerable<DevSession> userSessions)
        {
            var sessions = userSessions?
                .Where(item => item != null && item.UserId == session.UserId)
                .ToList() ?? new List<DevSession>();

            if (!sessions.Any(item => item.Id == session.Id))
            {
                sessions.Add(session);
            }

            return sessions;
        }

        private static bool HasTooManyConsecutivePerfectSessions(DevSession session, IReadOnlyList<DevSession> sameUserSessions)
        {
            if (session.AchievementRate < 100)
            {
                return false;
            }

            var orderedSessions = sameUserSessions
                .OrderByDescending(item => item.StartedAtUtc)
                .ToList();
            var currentIndex = orderedSessions.FindIndex(item => item.Id == session.Id);
            if (currentIndex < 0)
            {
                return false;
            }

            var consecutiveCount = 0;
            for (var index = currentIndex; index < orderedSessions.Count; index++)
            {
                if (orderedSessions[index].AchievementRate != 100)
                {
                    break;
                }

                consecutiveCount += 1;
                if (consecutiveCount >= ConsecutivePerfectThreshold)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasLongDailyTotal(DevSession session, IEnumerable<DevSession> sameUserSessions)
        {
            var sessionDate = session.StartedAtUtc.Date;
            var dailyTotalMinutes = sameUserSessions
                .Where(item => item.StartedAtUtc.Date == sessionDate)
                .Sum(item => item.DurationMinutes);
            return dailyTotalMinutes >= DailyTotalReviewMinutes;
        }
    }
}
