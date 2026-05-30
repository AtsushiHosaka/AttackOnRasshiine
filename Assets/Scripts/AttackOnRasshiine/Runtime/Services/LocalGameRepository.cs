using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AttackOnRasshiine.Runtime.Data;
using UnityEngine;

namespace AttackOnRasshiine.Runtime.Services
{
    public sealed class LocalGameRepository
    {
        public const string AiFallbackFeedback = "AI評価失敗のため暫定評価です。記録は保存されました。";

        private const string DefaultAiEvaluationFailureReason = "ai_evaluation_failed";
        private const string FallbackModelName = "local-rule-fallback";
        private const string PasswordHashPrefix = "sha256:";

        private readonly List<UserProfile> users = new();
        private readonly Dictionary<string, string> passwordHashesByUser = new();
        private readonly Dictionary<string, CharacterStats> statsByUser = new();
        private readonly List<DevSession> sessions = new();
        private readonly List<ProductEntry> products = new();
        private readonly List<AchievementEntry> achievements = new();
        private readonly List<AuditLogEntry> auditLogs = new();
        private readonly List<WeaponDefinition> weapons;
        private BossBattleState activeBattle;
        private int mentorBossIndex;

        public LocalGameRepository()
        {
            weapons = GameSeedData.CreateWeapons();
            SeedUsers();
            SeedSessions();
            activeBattle = CreateBattleState(BattleStatus.Scheduled, GetDefaultMentorUserId());
        }

        public IReadOnlyList<UserProfile> Users => users;
        public IReadOnlyList<WeaponDefinition> Weapons => weapons;
        public IReadOnlyList<UserProfile> Mentors => users.Where(user => user.Role == UserRole.Mentor).ToList();
        public IReadOnlyList<UserProfile> Members => users.Where(user => user.Role == UserRole.Member).ToList();
        public IReadOnlyList<DevSession> Sessions => sessions;
        public IReadOnlyList<ProductEntry> Products => products;
        public IReadOnlyList<AchievementEntry> Achievements => achievements;
        public IReadOnlyList<AuditLogEntry> AuditLogs => auditLogs;
        public BossBattleState ActiveBattle => activeBattle;

        public UserProfile LoginAs(UserRole role)
        {
            return users.First(user => user.Role == role);
        }

        public UserProfile Authenticate(string loginId, string password)
        {
            var normalizedLoginId = loginId?.Trim();
            var user = users.FirstOrDefault(item => string.Equals(item.LoginId, normalizedLoginId, StringComparison.OrdinalIgnoreCase));
            if (user == null || !user.IsActive)
            {
                return null;
            }

            return IsPasswordMatch(user.Id, password) ? user : null;
        }

        public bool RequiresInitialPasswordChange(UserProfile user)
        {
            return user is { IsActive: true, InitialPasswordChanged: false };
        }

        public MemberAccountProvisioningResult CreateMemberAccount(string mentorUserId, string loginId, string nickname, string teamId, bool rankingVisible = true)
        {
            return CreateAccount(mentorUserId, loginId, nickname, UserRole.Member, teamId, rankingVisible);
        }

        public MemberAccountProvisioningResult CreateMentorAccount(string mentorUserId, string loginId, string nickname, string teamId, bool rankingVisible)
        {
            return CreateAccount(mentorUserId, loginId, nickname, UserRole.Mentor, teamId, rankingVisible);
        }

        private MemberAccountProvisioningResult CreateAccount(string mentorUserId, string loginId, string nickname, UserRole role, string teamId, bool rankingVisible)
        {
            var mentor = GetMentor(mentorUserId);
            if (role != UserRole.Member && role != UserRole.Mentor)
            {
                throw new InvalidOperationException("作成できるロールではありません。");
            }

            var normalizedLoginId = NormalizeLoginId(loginId);
            if (users.Any(user => string.Equals(user.LoginId, normalizedLoginId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("同じログインIDのユーザーがいます。");
            }

            var temporaryPassword = GenerateTemporaryPassword();
            var user = new UserProfile
            {
                Id = Guid.NewGuid().ToString("N"),
                LoginId = normalizedLoginId,
                Nickname = NormalizeRequired(nickname, "表示名を入力してください。"),
                Role = role,
                TeamId = NormalizeTeamId(teamId),
                RankingVisible = rankingVisible,
                InitialPasswordChanged = false,
                IsActive = true
            };

            users.Add(user);
            passwordHashesByUser[user.Id] = HashPassword(temporaryPassword);
            statsByUser[user.Id] = ApplyGrowthUnlocks(EnsureStatsCollections(new CharacterStats()));
            RecordAudit(mentor.Id, "account.create", "user", user.Id, string.Empty, DescribeUser(user));
            RecordAudit(mentor.Id, "account.temporary_password_issue", "user", user.Id, string.Empty, $"{DescribeUser(user)};temporaryPasswordIssued=true");
            return new MemberAccountProvisioningResult
            {
                User = user,
                TemporaryPassword = temporaryPassword
            };
        }

        public MemberAccountProvisioningResult IssueTemporaryPassword(string mentorUserId, string userId)
        {
            var mentor = GetMentor(mentorUserId);
            var user = users.FirstOrDefault(item => item.Id == userId && item.Role == UserRole.Member && item.IsActive);
            if (user == null)
            {
                throw new InvalidOperationException("有効なメンバーアカウントが見つかりません。");
            }

            var before = DescribeUser(user);
            var temporaryPassword = GenerateTemporaryPassword();
            user.InitialPasswordChanged = false;
            passwordHashesByUser[user.Id] = HashPassword(temporaryPassword);
            RecordAudit(mentor.Id, "account.temporary_password_issue", "user", user.Id, before, $"{DescribeUser(user)};temporaryPasswordIssued=true");
            return new MemberAccountProvisioningResult
            {
                User = user,
                TemporaryPassword = temporaryPassword
            };
        }

        public UserProfile ChangePassword(string userId, string currentPassword, string newPassword)
        {
            var user = users.FirstOrDefault(item => item.Id == userId && item.IsActive);
            if (user == null)
            {
                throw new InvalidOperationException("有効なユーザーが見つかりません。");
            }

            if (!IsPasswordMatch(user.Id, currentPassword))
            {
                throw new InvalidOperationException("現在のパスワードが違います。");
            }

            var before = DescribeUser(user);
            passwordHashesByUser[user.Id] = HashPassword(NormalizePassword(newPassword));
            user.InitialPasswordChanged = true;
            RecordAudit(user.Id, "account.initial_password_change", "user", user.Id, before, DescribeUser(user));
            return user;
        }

        public CharacterStats GetStats(string userId)
        {
            if (statsByUser.TryGetValue(userId, out var stats))
            {
                return ApplyGrowthUnlocks(EnsureStatsCollections(stats));
            }

            var fallback = new CharacterStats();
            statsByUser[userId] = ApplyGrowthUnlocks(EnsureStatsCollections(fallback));
            return statsByUser[userId];
        }

        public IReadOnlyList<WeaponKind> GetAvailableBattleWeapons(string userId)
        {
            var stats = GetStats(userId);
            var unlockedWeapons = new HashSet<WeaponKind>(stats.UnlockedWeapons);
            var availableWeapons = weapons
                .Where(weapon => unlockedWeapons.Contains(weapon.Kind))
                .Select(weapon => weapon.Kind)
                .ToList();

            if (availableWeapons.Count == 0)
            {
                availableWeapons.Add(WeaponKind.Blade);
            }

            return availableWeapons;
        }

        public DevSession GetActiveSession(string userId)
        {
            return sessions.FirstOrDefault(session => session.UserId == userId && IsResumableSessionStatus(session.Status));
        }

        public IReadOnlyList<DevSession> GetSessionsForUser(string userId)
        {
            return sessions.Where(session => session.UserId == userId)
                .OrderByDescending(session => session.StartedAtUtc)
                .ToList();
        }

        public IReadOnlyList<DevSession> GetPendingSessions(DevSessionReviewFilter filter = DevSessionReviewFilter.All)
        {
            return sessions.Where(session => IsReviewQueueStatus(session.Status) && MatchesReviewFilter(session.Status, filter))
                .OrderByDescending(session => session.StartedAtUtc)
                .ToList();
        }

        public IReadOnlyList<ProductEntry> GetVisibleProducts()
        {
            return products.Where(product => product.IsPublic)
                .OrderByDescending(product => product.CreatedAtUtc)
                .ToList();
        }

        public IReadOnlyList<ProductEntry> GetProductsForUser(string userId)
        {
            return products.Where(product => product.IsPublic || product.UserId == userId)
                .OrderByDescending(product => product.CreatedAtUtc)
                .ToList();
        }

        public IReadOnlyList<ProductEntry> GetProductsForMentor()
        {
            return products.OrderByDescending(product => product.CreatedAtUtc).ToList();
        }

        public void ApplyProductUpdate(ProductEntry product)
        {
            if (product == null || string.IsNullOrWhiteSpace(product.Id))
            {
                return;
            }

            var existing = products.FirstOrDefault(item => item.Id == product.Id);
            if (existing == null)
            {
                products.Add(product);
                return;
            }

            existing.UserId = product.UserId;
            existing.Title = product.Title;
            existing.Url = product.Url;
            existing.Description = product.Description;
            existing.IsPublic = product.IsPublic;
            existing.HiddenBy = product.HiddenBy;
            existing.CreatedAtUtc = product.CreatedAtUtc;
        }

        public ProductEntry RegisterProduct(string userId, string title, string url, string description)
        {
            var user = users.FirstOrDefault(item => item.Id == userId);
            if (user == null || user.Role != UserRole.Member)
            {
                throw new InvalidOperationException("メンバーだけがプロダクトURLを登録できます。");
            }

            var normalizedTitle = NormalizeRequired(title, "プロダクト名を入力してください。");
            var normalizedUrl = NormalizeProductUrl(url);
            var product = new ProductEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = userId,
                Title = normalizedTitle,
                Url = normalizedUrl,
                Description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim(),
                IsPublic = true,
                HiddenBy = string.Empty,
                CreatedAtUtc = DateTime.UtcNow
            };
            products.Add(product);
            return product;
        }

        public void HideProduct(string productId, string mentorUserId)
        {
            var mentor = users.FirstOrDefault(item => item.Id == mentorUserId);
            if (mentor == null || mentor.Role != UserRole.Mentor)
            {
                throw new InvalidOperationException("メンターだけがプロダクトURLを非表示にできます。");
            }

            var product = products.First(item => item.Id == productId);
            var before = DescribeProduct(product);
            product.IsPublic = false;
            product.HiddenBy = mentorUserId;
            RecordAudit(mentorUserId, "product.hide", "product", product.Id, before, DescribeProduct(product));
        }

        public IReadOnlyList<AchievementEntry> GetAchievementsForUser(string userId)
        {
            return achievements.Where(achievement => achievement.UserId == userId)
                .OrderByDescending(achievement => achievement.CreatedAtUtc)
                .ToList();
        }

        public IReadOnlyList<AchievementEntry> GetPendingAchievements()
        {
            return achievements.Where(achievement => achievement.Status == AchievementStatus.Pending)
                .OrderByDescending(achievement => achievement.CreatedAtUtc)
                .ToList();
        }

        public IReadOnlyList<AchievementEntry> GetRecentAchievements()
        {
            return achievements.OrderByDescending(achievement => achievement.CreatedAtUtc).ToList();
        }

        public void ApplyAchievementUpdate(AchievementEntry achievement)
        {
            if (achievement == null || string.IsNullOrWhiteSpace(achievement.Id))
            {
                return;
            }

            var existing = achievements.FirstOrDefault(item => item.Id == achievement.Id);
            if (existing == null)
            {
                achievements.Add(achievement);
                return;
            }

            existing.UserId = achievement.UserId;
            existing.Type = achievement.Type;
            existing.Title = achievement.Title;
            existing.Description = achievement.Description;
            existing.Status = achievement.Status;
            existing.ApprovedBy = achievement.ApprovedBy;
            existing.ApprovedAtUtc = achievement.ApprovedAtUtc;
            existing.CreatedAtUtc = achievement.CreatedAtUtc;
            existing.HasRewardWeapon = achievement.HasRewardWeapon;
            existing.RewardWeapon = achievement.RewardWeapon;
            existing.RewardTitle = achievement.RewardTitle;
            existing.RewardSkill = achievement.RewardSkill;
        }

        public IReadOnlyList<AuditLogEntry> GetAuditLogsForTarget(string targetType, string targetId)
        {
            return auditLogs.Where(log => log.TargetType == targetType && log.TargetId == targetId)
                .OrderByDescending(log => log.CreatedAtUtc)
                .ToList();
        }

        public IReadOnlyList<AuditLogEntry> GetRecentAuditLogs(int count = 10)
        {
            return auditLogs.OrderByDescending(log => log.CreatedAtUtc)
                .Take(Mathf.Max(0, count))
                .ToList();
        }

        public AchievementEntry SubmitAchievement(string userId, AchievementType type, string title, string description)
        {
            var user = users.FirstOrDefault(item => item.Id == userId);
            if (user == null || user.Role != UserRole.Member)
            {
                throw new InvalidOperationException("メンバーだけが実績を申請できます。");
            }

            var achievement = new AchievementEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = userId,
                Type = type,
                Title = NormalizeRequired(title, "実績名を入力してください。"),
                Description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim(),
                Status = AchievementStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow
            };
            achievements.Add(achievement);
            return achievement;
        }

        public AchievementEntry ApproveAchievement(string achievementId, string mentorUserId)
        {
            var mentor = users.FirstOrDefault(item => item.Id == mentorUserId);
            if (mentor == null || mentor.Role != UserRole.Mentor)
            {
                throw new InvalidOperationException("メンターだけが実績を承認できます。");
            }

            var achievement = achievements.First(item => item.Id == achievementId);
            if (achievement.Status == AchievementStatus.Approved)
            {
                ApplyAchievementReward(achievement);
                return achievement;
            }

            if (achievement.Status != AchievementStatus.Pending)
            {
                throw new InvalidOperationException("承認待ちの実績ではありません。");
            }

            var before = DescribeAchievement(achievement);
            achievement.Status = AchievementStatus.Approved;
            achievement.ApprovedBy = mentorUserId;
            achievement.ApprovedAtUtc = DateTime.UtcNow;
            ApplyAchievementReward(achievement);
            RecordAudit(mentorUserId, "achievement.approve", "achievement", achievement.Id, before, DescribeAchievement(achievement));
            return achievement;
        }

        public AchievementEntry RejectAchievement(string achievementId, string mentorUserId)
        {
            var mentor = users.FirstOrDefault(item => item.Id == mentorUserId);
            if (mentor == null || mentor.Role != UserRole.Mentor)
            {
                throw new InvalidOperationException("メンターだけが実績を却下できます。");
            }

            var achievement = achievements.First(item => item.Id == achievementId);
            if (achievement.Status == AchievementStatus.Rejected)
            {
                return achievement;
            }

            if (achievement.Status != AchievementStatus.Pending)
            {
                throw new InvalidOperationException("承認待ちの実績ではありません。");
            }

            var before = DescribeAchievement(achievement);
            achievement.Status = AchievementStatus.Rejected;
            achievement.ApprovedBy = mentorUserId;
            achievement.ApprovedAtUtc = DateTime.UtcNow;
            RecordAudit(mentorUserId, "achievement.reject", "achievement", achievement.Id, before, DescribeAchievement(achievement));
            return achievement;
        }

        public DevSession StartSession(string userId, string goal)
        {
            if (GetActiveSession(userId) != null)
            {
                throw new InvalidOperationException("進行中のセッションがあります。");
            }

            var session = new DevSession
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = userId,
                StartedAtUtc = DateTime.UtcNow,
                Goal = string.IsNullOrWhiteSpace(goal) ? "新しいガジェットを実装" : goal.Trim(),
                Status = DevSessionStatus.InProgress
            };
            sessions.Add(session);
            return session;
        }

        public DevSession CompleteSession(string userId, int achievementRate, string reflection, string nextTask)
        {
            var session = CompleteActiveSession(userId, achievementRate, reflection, nextTask);
            session.Evaluation = EvaluateSession(session);
            session.AiEvaluationFailureReason = string.Empty;
            session.Status = GetPendingReviewStatus(session);
            return session;
        }

        public DevSession CompleteSessionWithAiFailure(string userId, int achievementRate, string reflection, string nextTask, string failureReason)
        {
            var session = CompleteActiveSession(userId, achievementRate, reflection, nextTask);
            session.Evaluation = null;
            session.AiEvaluationFailureReason = NormalizeAiFailureReason(failureReason);
            session.Status = DevSessionStatus.AiPending;
            return session;
        }

        public DevSession RetryAiEvaluation(string sessionId)
        {
            var session = GetAiPendingSession(sessionId);
            session.Evaluation = EvaluateSession(session);
            session.AiEvaluationFailureReason = string.Empty;
            session.Status = GetPendingReviewStatus(session);
            return session;
        }

        public DevSession ApplyAiEvaluationFallback(string sessionId)
        {
            var session = GetAiPendingSession(sessionId);
            session.Evaluation = EvaluateSession(session);
            session.Evaluation.ModelName = FallbackModelName;
            session.Evaluation.Feedback = AiFallbackFeedback;
            session.AiEvaluationFailureReason = NormalizeAiFailureReason(session.AiEvaluationFailureReason);
            session.Status = GetPendingReviewStatus(session);
            return session;
        }

        public DevSession ApproveSession(string sessionId, string mentorUserId, string comment)
        {
            var mentor = GetMentor(mentorUserId);
            var session = sessions.First(item => item.Id == sessionId);
            if (session.Status == DevSessionStatus.Approved)
            {
                return session;
            }

            EnsureReviewableSession(session);
            if (session.Status == DevSessionStatus.AiPending && session.Evaluation == null)
            {
                ApplyAiEvaluationFallback(session.Id);
            }

            var before = DescribeSession(session);
            return ApplySessionApproval(session, mentor.Id, comment, "session.approve", before);
        }

        public DevSession ApproveSessionWithCorrections(
            string sessionId,
            string mentorUserId,
            int correctedAchievementRate,
            int correctedDurationMinutes,
            string correctedReflection,
            string correctedNextTask,
            string comment)
        {
            var mentor = GetMentor(mentorUserId);
            var session = sessions.First(item => item.Id == sessionId);
            if (session.Status == DevSessionStatus.Approved)
            {
                return session;
            }

            EnsureReviewableSession(session);
            var before = DescribeSession(session);
            ApplySessionCorrections(session, correctedAchievementRate, correctedDurationMinutes, correctedReflection, correctedNextTask);
            session.Evaluation = EvaluateSession(session);
            session.AiEvaluationFailureReason = string.Empty;
            var reviewComment = NormalizeReviewComment(comment, $"修正承認: 達成度 {session.AchievementRate}% / 開発時間 {session.DurationMinutes}分");
            return ApplySessionApproval(session, mentor.Id, reviewComment, "session.correction_approve", before);
        }

        public DevSession RejectSession(string sessionId, string mentorUserId, string comment)
        {
            var mentor = GetMentor(mentorUserId);
            var session = sessions.First(item => item.Id == sessionId);
            if (session.Status == DevSessionStatus.Rejected)
            {
                return session;
            }

            EnsureReviewableSession(session);
            var before = DescribeSession(session);
            session.Status = DevSessionStatus.Rejected;
            session.ApprovedBy = mentor.Id;
            session.ApprovedAtUtc = DateTime.UtcNow;
            session.MentorComment = NormalizeReviewComment(comment, "却下しました。内容を見直してください。");
            RecordAudit(mentor.Id, "session.reject", "session", session.Id, before, DescribeSession(session));
            return session;
        }

        private DevSession ApplySessionApproval(DevSession session, string mentorUserId, string comment, string actionType, string before)
        {
            session.Status = DevSessionStatus.Approved;
            session.ApprovedBy = mentorUserId;
            session.ApprovedAtUtc = DateTime.UtcNow;
            session.MentorComment = NormalizeReviewComment(comment, "確認しました。正式EXPへ反映します。");
            session.GrowthFeedback = ApplyGrowthFeedback(session.UserId, session.PreviewExp);
            ApplyGrowthUnlocks(GetStats(session.UserId));
            RebuildBattleFromApprovedLogs();
            RecordAudit(mentorUserId, actionType, "session", session.Id, before, DescribeSession(session));
            return session;
        }

        private CharacterGrowthFeedback ApplyGrowthFeedback(string userId, int expGained)
        {
            var stats = GetStats(userId);
            var beforeLevel = stats.Level;
            var beforeExp = stats.Exp;
            var beforeHp = stats.Hp;
            var beforeAtk = stats.Atk;
            var beforeDef = stats.Def;
            var beforeMp = stats.Mp;
            stats.AddExp(expGained);
            return new CharacterGrowthFeedback
            {
                UserId = userId,
                ExpGained = Mathf.Max(0, expGained),
                LevelBefore = beforeLevel,
                LevelAfter = stats.Level,
                ExpBefore = beforeExp,
                ExpAfter = stats.Exp,
                HpIncrease = Mathf.Max(0, stats.Hp - beforeHp),
                AtkIncrease = Mathf.Max(0, stats.Atk - beforeAtk),
                DefIncrease = Mathf.Max(0, stats.Def - beforeDef),
                MpIncrease = Mathf.Max(0, stats.Mp - beforeMp)
            };
        }

        private void ApplySessionCorrections(DevSession session, int achievementRate, int durationMinutes, string reflection, string nextTask)
        {
            session.AchievementRate = Mathf.Clamp(achievementRate, 0, 100);
            session.DurationMinutes = Mathf.Max(1, durationMinutes);
            if (session.EndedAtUtc.HasValue)
            {
                session.StartedAtUtc = session.EndedAtUtc.Value.AddMinutes(-session.DurationMinutes);
            }

            if (!string.IsNullOrWhiteSpace(reflection))
            {
                session.Reflection = reflection.Trim();
            }

            if (!string.IsNullOrWhiteSpace(nextTask))
            {
                session.NextTask = nextTask.Trim();
            }

            session.SuspiciousFlags = SuspiciousLogDetector.Detect(session, sessions.Where(item => item.UserId == session.UserId && item.Id != session.Id));
        }

        public int GetApprovedMinutesThisWeek(string userId)
        {
            return GetApprovedSessionsForPeriod(RankingPeriod.Weekly, DateTime.UtcNow)
                .Where(session => session.UserId == userId)
                .Sum(session => session.DurationMinutes);
        }

        public int GetTotalApprovedMinutes()
        {
            return sessions.Where(session => session.Status == DevSessionStatus.Approved).Sum(session => session.DurationMinutes);
        }

        public int GetTotalApprovedMinutes(RankingPeriod period)
        {
            return GetApprovedSessionsForPeriod(period, DateTime.UtcNow).Sum(session => session.DurationMinutes);
        }

        public List<DevSession> GetRankingSessions()
        {
            return sessions.Where(session => session.Status == DevSessionStatus.Approved)
                .OrderByDescending(session => session.DurationMinutes)
                .ToList();
        }

        public IReadOnlyList<DevelopmentTimeRankingEntry> GetDevelopmentTimeRanking(RankingPeriod period, DateTime? nowUtc = null)
        {
            return BuildDevelopmentTimeRanking(period, null, nowUtc ?? DateTime.UtcNow);
        }

        public IReadOnlyList<DevelopmentTimeRankingEntry> GetTeamMemberDevelopmentTimeRanking(string teamId, RankingPeriod period, DateTime? nowUtc = null)
        {
            if (string.IsNullOrWhiteSpace(teamId))
            {
                return Array.Empty<DevelopmentTimeRankingEntry>();
            }

            return BuildDevelopmentTimeRanking(period, user => string.Equals(user.TeamId, teamId, StringComparison.Ordinal), nowUtc ?? DateTime.UtcNow);
        }

        public IReadOnlyList<TeamDevelopmentTimeRankingEntry> GetTeamDevelopmentTimeRanking(RankingPeriod period, DateTime? nowUtc = null)
        {
            var visibleMembersById = users
                .Where(user => user.Role == UserRole.Member && user.RankingVisible && !string.IsNullOrWhiteSpace(user.TeamId))
                .ToDictionary(user => user.Id, StringComparer.Ordinal);

            return GetApprovedSessionsForPeriod(period, nowUtc ?? DateTime.UtcNow)
                .Where(session => visibleMembersById.ContainsKey(session.UserId))
                .GroupBy(session => visibleMembersById[session.UserId].TeamId, StringComparer.Ordinal)
                .Select(group => new TeamDevelopmentTimeRankingEntry
                {
                    TeamId = group.Key,
                    TeamName = GetTeamDisplayName(group.Key),
                    DurationMinutes = group.Sum(session => session.DurationMinutes),
                    SessionCount = group.Count(),
                    MemberCount = group.Select(session => session.UserId).Distinct(StringComparer.Ordinal).Count()
                })
                .Where(entry => entry.DurationMinutes > 0)
                .OrderByDescending(entry => entry.DurationMinutes)
                .ThenBy(entry => entry.TeamName, StringComparer.Ordinal)
                .ToList();
        }

        public static string GetTeamDisplayName(string teamId)
        {
            if (string.IsNullOrWhiteSpace(teamId))
            {
                return "未設定班";
            }

            return teamId switch
            {
                "blue" => "ブルー班",
                "magenta" => "マゼンタ班",
                _ => $"{teamId}班"
            };
        }

        private IReadOnlyList<DevelopmentTimeRankingEntry> BuildDevelopmentTimeRanking(RankingPeriod period, Func<UserProfile, bool> userFilter, DateTime nowUtc)
        {
            var entries = new List<DevelopmentTimeRankingEntry>();
            var approvedSessions = GetApprovedSessionsForPeriod(period, nowUtc);
            foreach (var group in approvedSessions.GroupBy(session => session.UserId))
            {
                var user = users.FirstOrDefault(item => item.Id == group.Key);
                if (user == null || user.Role != UserRole.Member || !user.RankingVisible || userFilter?.Invoke(user) == false)
                {
                    continue;
                }

                var durationMinutes = group.Sum(session => session.DurationMinutes);
                if (durationMinutes <= 0)
                {
                    continue;
                }

                entries.Add(new DevelopmentTimeRankingEntry
                {
                    Nickname = user.Nickname,
                    DurationMinutes = durationMinutes,
                    SessionCount = group.Count()
                });
            }

            return entries
                .OrderByDescending(entry => entry.DurationMinutes)
                .ThenBy(entry => entry.Nickname, StringComparer.Ordinal)
                .ToList();
        }

        public IReadOnlyList<BattleDamageRankingEntry> GetBattleDamageRanking(RankingPeriod period, DateTime? nowUtc = null)
        {
            var approvedMinutesByUser = GetApprovedSessionsForPeriod(period, nowUtc ?? DateTime.UtcNow)
                .GroupBy(session => session.UserId)
                .ToDictionary(group => group.Key, group => group.Sum(session => session.DurationMinutes));
            var entries = new List<BattleDamageRankingEntry>();
            foreach (var participant in activeBattle?.Participants ?? new List<BattleParticipant>())
            {
                var user = users.FirstOrDefault(item => item.Id == participant.UserId);
                if (user == null || user.Role != UserRole.Member || !user.RankingVisible)
                {
                    continue;
                }

                if (!approvedMinutesByUser.TryGetValue(user.Id, out var approvedMinutes) || approvedMinutes <= 0 || participant.TotalDamage <= 0)
                {
                    continue;
                }

                entries.Add(new BattleDamageRankingEntry
                {
                    Nickname = user.Nickname,
                    Damage = participant.TotalDamage,
                    ApprovedMinutes = approvedMinutes
                });
            }

            return entries
                .OrderByDescending(entry => entry.Damage)
                .ThenByDescending(entry => entry.ApprovedMinutes)
                .ThenBy(entry => entry.Nickname, StringComparer.Ordinal)
                .ToList();
        }

        public BattleResultSummary GetBattleResultSummary(RankingPeriod rewardPeriod = RankingPeriod.Weekly, DateTime? nowUtc = null)
        {
            if (activeBattle == null)
            {
                return new BattleResultSummary
                {
                    ResultTitle = "NO BATTLE",
                    ResultMessage = "ボス戦データがありません。",
                    RewardSummary = "報酬なし"
                };
            }

            var outcome = EnsureBattleOutcomeSaved(activeBattle);
            var isVictory = outcome == BattleOutcome.Victory;
            var contributors = GetBattleContributors(rewardPeriod, nowUtc, isVictory).ToList();

            var mvp = contributors.FirstOrDefault();
            return new BattleResultSummary
            {
                Outcome = outcome,
                IsVictory = isVictory,
                ResultTitle = isVictory ? "VICTORY" : "TIME UP",
                ResultMessage = isVictory
                    ? "ボス撃破。チームの開発成果が勝利につながりました。"
                    : "3ターン終了。残りHPを確認して次回の開発ログへつなげましょう。",
                RewardSummary = isVictory
                    ? $"勝利報酬: MVP {mvp?.Nickname ?? "-"} +{mvp?.RewardExp ?? 0}EXP / 参加者は貢献に応じてEXP"
                    : "参加報酬: 開発時間と貢献に応じたEXPを次回へ持ち越し",
                BossCurrentHp = activeBattle.Boss.CurrentHp,
                BossMaxHp = activeBattle.Boss.MaxHp,
                TeamDamage = activeBattle.TotalDamage,
                ParticipantCount = activeBattle.Participants.Count,
                Contributors = contributors
            };
        }

        public IReadOnlyList<BattleResultContributor> GetBattleContributors(RankingPeriod period = RankingPeriod.Weekly, DateTime? nowUtc = null, bool? rewardAsVictory = null)
        {
            if (activeBattle == null)
            {
                return new List<BattleResultContributor>();
            }

            var approvedMinutesByUser = GetApprovedSessionsForPeriod(period, nowUtc ?? DateTime.UtcNow)
                .GroupBy(session => session.UserId)
                .ToDictionary(group => group.Key, group => group.Sum(session => session.DurationMinutes));
            var isVictory = rewardAsVictory ?? ResolveBattleOutcome(activeBattle) == BattleOutcome.Victory;
            var contributors = activeBattle.Participants
                .Select(participant =>
                {
                    approvedMinutesByUser.TryGetValue(participant.UserId, out var approvedMinutes);
                    var score = CalculateContributionScore(participant);
                    var rewardExp = isVictory
                        ? Mathf.Max(30, Mathf.RoundToInt(score * 0.08f) + approvedMinutes / 4)
                        : Mathf.Max(10, Mathf.RoundToInt(score * 0.03f) + approvedMinutes / 10);
                    var user = users.FirstOrDefault(item => item.Id == participant.UserId);
                    return new BattleResultContributor
                    {
                        UserId = participant.UserId,
                        Nickname = participant.Nickname,
                        TeamName = GetTeamDisplayName(user?.TeamId),
                        Damage = participant.TotalDamage,
                        Heal = participant.TotalHeal,
                        SupportCount = participant.SupportCount,
                        ApprovedMinutes = approvedMinutes,
                        ContributionScore = score,
                        HighlightContext = BuildContributionContext(participant, approvedMinutes),
                        RewardExp = rewardExp
                    };
                })
                .OrderByDescending(entry => entry.ContributionScore)
                .ThenByDescending(entry => entry.ApprovedMinutes)
                .ThenBy(entry => entry.Nickname, StringComparer.Ordinal)
                .ToList();

            if (contributors.Count > 0)
            {
                contributors[0].IsMvp = true;
            }

            return contributors;
        }

        public FrontDisplaySummary GetFrontDisplaySummary(RankingPeriod period = RankingPeriod.Weekly, DateTime? nowUtc = null)
        {
            if (activeBattle == null)
            {
                return new FrontDisplaySummary
                {
                    BossName = "NO BATTLE",
                    PhaseLabel = "NO DATA",
                    BossHpRatio = 0f
                };
            }

            var currentTime = nowUtc ?? DateTime.UtcNow;
            var contributors = GetBattleContributors(period, currentTime).ToList();
            var participantsByUser = activeBattle.Participants.ToDictionary(participant => participant.UserId);
            var highlights = contributors
                .Select(contributor =>
                {
                    participantsByUser.TryGetValue(contributor.UserId, out var participant);
                    return new FrontDisplayHighlight
                    {
                        UserId = contributor.UserId,
                        Nickname = contributor.Nickname,
                        Role = participant?.Role ?? BattleRole.Attacker,
                        Damage = contributor.Damage,
                        Heal = contributor.Heal,
                        SupportCount = contributor.SupportCount,
                        ApprovedMinutes = contributor.ApprovedMinutes,
                        ContributionScore = contributor.ContributionScore,
                        HighlightContext = contributor.HighlightContext,
                        IsTopHighlight = contributor.IsMvp
                    };
                })
                .ToList();
            var result = activeBattle.IsCompleted ? GetBattleResultSummary(period, currentTime) : null;
            var isCompleted = activeBattle.IsCompleted;
            return new FrontDisplaySummary
            {
                BossName = activeBattle.Boss.Name,
                PhaseLabel = activeBattle.Status == BattleStatus.Scheduled ? "開始待機" : isCompleted ? "RESULT" : "LIVE RAID",
                Outcome = result?.Outcome ?? BattleOutcome.Undecided,
                IsScheduled = activeBattle.Status == BattleStatus.Scheduled,
                IsCompleted = isCompleted,
                IsVictory = result?.IsVictory ?? false,
                BossCurrentHp = activeBattle.Boss.CurrentHp,
                BossMaxHp = activeBattle.Boss.MaxHp,
                BossHpRatio = activeBattle.Boss.MaxHp <= 0 ? 0f : Mathf.Clamp01(activeBattle.Boss.CurrentHp / (float)activeBattle.Boss.MaxHp),
                TeamDamage = activeBattle.TotalDamage,
                TurnNumber = Mathf.Min(activeBattle.TurnNumber, activeBattle.TurnCount),
                TurnCount = activeBattle.TurnCount,
                ParticipantCount = activeBattle.Participants.Count,
                MemberCount = Members.Count,
                WeeklyApprovedMinutes = GetApprovedSessionsForPeriod(period, currentTime).Sum(session => session.DurationMinutes),
                ResultTitle = result?.ResultTitle ?? string.Empty,
                RewardSummary = result?.RewardSummary ?? string.Empty,
                TopHighlight = highlights.FirstOrDefault(),
                Highlights = highlights
            };
        }

        public BattleResultContributor GetHighlightedContributor(RankingPeriod period = RankingPeriod.Weekly, DateTime? nowUtc = null)
        {
            return GetBattleContributors(period, nowUtc).FirstOrDefault(entry => entry.ContributionScore > 0 || entry.ApprovedMinutes > 0);
        }

        private static int CalculateContributionScore(BattleParticipant participant)
        {
            return participant.TotalDamage + participant.TotalHeal + participant.SupportCount * 30;
        }

        private static string BuildContributionContext(BattleParticipant participant, int approvedMinutes)
        {
            var parts = new List<string>();
            if (participant.TotalDamage > 0)
            {
                parts.Add($"攻撃 {participant.TotalDamage:N0}");
            }

            if (participant.TotalHeal > 0)
            {
                parts.Add($"回復 {participant.TotalHeal:N0}");
            }

            if (participant.SupportCount > 0)
            {
                parts.Add($"支援 {participant.SupportCount}回");
            }

            if (approvedMinutes > 0)
            {
                parts.Add($"承認開発 {approvedMinutes}分");
            }

            return parts.Count == 0 ? "次の行動で見せ場を作ろう" : string.Join(" / ", parts.Take(3));
        }

        private static BattleOutcome EnsureBattleOutcomeSaved(BossBattleState battle)
        {
            var outcome = ResolveBattleOutcome(battle);
            if (outcome != BattleOutcome.Undecided && battle.Outcome == BattleOutcome.Undecided)
            {
                battle.Outcome = outcome;
            }

            if (outcome != BattleOutcome.Undecided)
            {
                battle.Status = BattleStatus.Completed;
                battle.Phase = BattlePhase.Completed;
            }

            return outcome;
        }

        private static BattleOutcome ResolveBattleOutcome(BossBattleState battle)
        {
            if (battle == null)
            {
                return BattleOutcome.Undecided;
            }

            if (battle.Outcome != BattleOutcome.Undecided)
            {
                return battle.Outcome;
            }

            if (battle.Boss.CurrentHp <= 0)
            {
                return BattleOutcome.Victory;
            }

            if (battle.Status == BattleStatus.Completed || battle.Phase == BattlePhase.Completed || battle.TurnNumber > battle.TurnCount)
            {
                return BattleOutcome.Defeat;
            }

            return BattleOutcome.Undecided;
        }

        public BattleParticipant GetParticipant(string userId)
        {
            if (activeBattle is not { IsActive: true })
            {
                return null;
            }

            return activeBattle.Participants.FirstOrDefault(participant => participant.UserId == userId);
        }

        public BattlePartyStatus GetBattlePartyStatus()
        {
            var participants = activeBattle?.Participants ?? new List<BattleParticipant>();
            return new BattlePartyStatus
            {
                ParticipantCount = participants.Count,
                AliveCount = participants.Count(participant => participant.IsAlive),
                CurrentHp = participants.Sum(participant => Mathf.Max(0, participant.CurrentHp)),
                MaxHp = participants.Sum(participant => Mathf.Max(0, participant.Stats != null ? participant.Stats.Hp : 0)),
                CurrentMp = participants.Sum(participant => Mathf.Max(0, participant.CurrentMp)),
                MaxMp = participants.Sum(participant => Mathf.Max(0, participant.Stats != null ? participant.Stats.Mp : 0))
            };
        }

        public IReadOnlyList<BattleMemberActionOption> GetBattleActionOptions(string userId, WeaponKind weaponKind)
        {
            var participant = GetParticipant(userId);
            if (participant == null)
            {
                return new List<BattleMemberActionOption>();
            }

            var weapon = ResolveBattleWeapon(weaponKind);
            var weaponUnlocked = IsWeaponUnlocked(participant.Stats, weaponKind);
            var actions = new[]
            {
                BattleActionType.Normal,
                BattleActionType.Strong,
                BattleActionType.FullPower,
                BattleActionType.Support,
                BattleActionType.Guard
            };

            return actions.Select(action =>
            {
                var mpCost = GetMpCost(action, weapon);
                return new BattleMemberActionOption
                {
                    ActionType = action,
                    Label = BattleActionLabel(action, mpCost),
                    MpCost = mpCost,
                    IsAvailable = weaponUnlocked && participant.CurrentMp >= mpCost
                };
            }).ToList();
        }

        public BattleActionResult SubmitBattleAction(string userId, BattleRole role, WeaponKind weaponKind, BattleActionType actionType)
        {
            if (activeBattle is not { IsActive: true })
            {
                return new BattleActionResult
                {
                    UserId = userId,
                    Nickname = GetNickname(userId),
                    ActionType = actionType,
                    TurnNumber = activeBattle?.TurnNumber ?? 1,
                    Message = "メンターがゲーム開始するまで待機中です。"
                };
            }

            if (activeBattle.IsCompleted)
            {
                return new BattleActionResult
                {
                    UserId = userId,
                    Nickname = GetNickname(userId),
                    ActionType = actionType,
                    TurnNumber = activeBattle.TurnNumber,
                    Message = "ボス戦は終了しています。"
                };
            }

            var participant = GetParticipant(userId);
            if (participant == null)
            {
                throw new InvalidOperationException("参加者が見つかりません。");
            }

            activeBattle.Phase = BattlePhase.Resolving;
            participant.Role = role;
            if (!IsWeaponUnlocked(participant.Stats, weaponKind))
            {
                weaponKind = WeaponKind.Blade;
            }

            participant.Weapon = weaponKind;
            var weapon = ResolveBattleWeapon(weaponKind);
            var mpCost = GetMpCost(actionType, weapon);
            var availableMp = Mathf.Max(0, participant.CurrentMp);
            if (availableMp < mpCost)
            {
                actionType = BattleActionType.Normal;
                mpCost = 0;
            }

            participant.CurrentMp = Mathf.Max(0, participant.CurrentMp - mpCost);
            var potentialDamage = BattleDamageCalculator.Calculate(participant.Stats, weapon, activeBattle.Boss, role, actionType);
            var heal = 0;
            var support = string.Empty;
            if (actionType == BattleActionType.Support)
            {
                ApplySupport(participant, role, out heal, out support);
            }

            if (actionType == BattleActionType.Guard)
            {
                participant.CurrentMp = Mathf.Min(participant.Stats.Mp, participant.CurrentMp + 6);
                support = "次ターンに備えてMPを回復";
            }

            var damage = ApplyBossDamage(participant, potentialDamage);
            participant.TotalHeal += heal;
            if (!string.IsNullOrEmpty(support))
            {
                participant.SupportCount += 1;
            }

            activeBattle.HighlightUserId = userId;
            var teamFollowUpDamage = activeBattle.Boss.CurrentHp > 0 ? ResolveTeamFollowUp(userId) : 0;
            var result = new BattleActionResult
            {
                UserId = userId,
                Nickname = participant.Nickname,
                Role = role,
                Weapon = weaponKind,
                ActionType = actionType,
                TurnNumber = activeBattle.TurnNumber,
                MpCost = mpCost,
                Damage = damage,
                Heal = heal,
                SupportEffect = support,
                Message = BuildActionMessage(participant.Nickname, actionType, damage, heal, support, teamFollowUpDamage)
            };

            PersistBattleAction(result);
            AdvanceTurnIfNeeded();
            return result;
        }

        public void ResetBattle(string creatorUserId = null)
        {
            mentorBossIndex = (mentorBossIndex + 1) % GameSeedData.MentorNames.Length;
            activeBattle = CreateBattleState(BattleStatus.Scheduled, ResolveBattleCreator(creatorUserId));
        }

        public void StartBattle(string creatorUserId = null)
        {
            if (activeBattle == null || activeBattle.Status == BattleStatus.Completed)
            {
                activeBattle = CreateBattleState(BattleStatus.Scheduled, ResolveBattleCreator(creatorUserId));
            }

            activeBattle.CreatedByUserId = ResolveBattleCreator(creatorUserId, activeBattle.CreatedByUserId);
            activeBattle.CreatedAtUtc = activeBattle.CreatedAtUtc == default ? DateTime.UtcNow : activeBattle.CreatedAtUtc;
            activeBattle.StartedAtUtc = DateTime.UtcNow;
            activeBattle.CompletedAtUtc = null;
            activeBattle.Status = BattleStatus.Active;
            activeBattle.Phase = BattlePhase.TurnStart;
            activeBattle.Outcome = BattleOutcome.Undecided;
            activeBattle.TurnNumber = 1;
            activeBattle.TotalDamage = 0;
            activeBattle.HighlightUserId = string.Empty;
            activeBattle.Actions ??= new List<BattleActionResult>();
            activeBattle.Actions.Clear();
            activeBattle.Boss.CurrentHp = activeBattle.Boss.MaxHp;
            foreach (var participant in activeBattle.Participants)
            {
                participant.CurrentHp = participant.Stats.Hp;
                participant.CurrentMp = participant.Stats.Mp;
                participant.TotalDamage = 0;
                participant.TotalHeal = 0;
                participant.SupportCount = 0;
            }
        }

        public void AdvanceBattlePhase()
        {
            if (activeBattle is not { IsActive: true })
            {
                return;
            }

            if (activeBattle.IsCompleted)
            {
                EnsureBattleOutcomeSaved(activeBattle);
                return;
            }

            switch (activeBattle.Phase)
            {
                case BattlePhase.TurnStart:
                    activeBattle.Phase = BattlePhase.ActionSelect;
                    break;
                case BattlePhase.ActionSelect:
                    activeBattle.Phase = BattlePhase.Resolving;
                    break;
                case BattlePhase.Resolving:
                    activeBattle.Phase = BattlePhase.Result;
                    break;
                case BattlePhase.Result:
                    AdvanceTurnIfNeeded();
                    break;
                case BattlePhase.Completed:
                    EnsureBattleOutcomeSaved(activeBattle);
                    break;
                default:
                    activeBattle.Phase = BattlePhase.TurnStart;
                    break;
            }
        }

        public void SetBossHpMultiplier(float multiplier)
        {
            var baseHp = activeBattle.BaseHp > 0 ? activeBattle.BaseHp : activeBattle.Boss.MaxHp;
            var hpMultiplier = Mathf.Max(0.1f, multiplier);
            var maxHp = Mathf.Max(2500, Mathf.RoundToInt(baseHp * hpMultiplier));
            activeBattle.BaseHp = baseHp;
            activeBattle.HpMultiplier = hpMultiplier;
            activeBattle.Boss.MaxHp = maxHp;
            activeBattle.Boss.CurrentHp = activeBattle.Status == BattleStatus.Scheduled ? maxHp : Mathf.Min(activeBattle.Boss.CurrentHp, maxHp);
        }

        public GameSnapshot CreateSnapshot()
        {
            return new GameSnapshot
            {
                Users = new List<UserProfile>(users),
                Stats = statsByUser.Select(entry => new CharacterStatsRecord
                {
                    UserId = entry.Key,
                    Stats = entry.Value
                }).ToList(),
                Weapons = new List<WeaponDefinition>(weapons),
                Sessions = new List<DevSession>(sessions),
                Products = new List<ProductEntry>(products),
                Achievements = new List<AchievementEntry>(achievements),
                AuditLogs = new List<AuditLogEntry>(auditLogs),
                ActiveBattle = activeBattle
            };
        }

        public void ApplySnapshot(GameSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            var previousPasswords = new Dictionary<string, string>(passwordHashesByUser);
            users.Clear();
            users.AddRange(snapshot.Users ?? new List<UserProfile>());
            RebuildPasswordFallbacks(previousPasswords);

            weapons.Clear();
            var snapshotWeapons = (snapshot.Weapons ?? new List<WeaponDefinition>())
                .Where(weapon => weapon != null)
                .ToList();
            weapons.AddRange(snapshotWeapons.Count > 0 ? snapshotWeapons : GameSeedData.CreateWeapons());

            sessions.Clear();
            sessions.AddRange(snapshot.Sessions ?? new List<DevSession>());

            products.Clear();
            products.AddRange((snapshot.Products ?? new List<ProductEntry>()).Where(product => product != null));

            achievements.Clear();
            achievements.AddRange((snapshot.Achievements ?? new List<AchievementEntry>()).Where(achievement => achievement != null));

            auditLogs.Clear();
            auditLogs.AddRange((snapshot.AuditLogs ?? new List<AuditLogEntry>()).Where(log => log != null));

            statsByUser.Clear();
            foreach (var record in snapshot.Stats ?? new List<CharacterStatsRecord>())
            {
                if (!string.IsNullOrWhiteSpace(record.UserId) && record.Stats != null)
                {
                    statsByUser[record.UserId] = ApplyGrowthUnlocks(EnsureStatsCollections(record.Stats));
                }
            }

            if (snapshot.ActiveBattle != null)
            {
                ApplyBattleSnapshot(snapshot.ActiveBattle);
            }

            ApplyApprovedAchievementRewards();
            ApplyGrowthUnlocksForKnownStats();
        }

        public void ApplyBattleSnapshot(BossBattleState battle)
        {
            if (battle == null)
            {
                return;
            }

            activeBattle = NormalizeBattleSnapshot(battle);
            foreach (var participant in activeBattle.Participants)
            {
                if (!string.IsNullOrWhiteSpace(participant.UserId) && participant.Stats != null)
                {
                    statsByUser[participant.UserId] = ApplyGrowthUnlocks(EnsureStatsCollections(participant.Stats));
                }
            }
        }

        private void SeedUsers()
        {
            for (var index = 0; index < GameSeedData.MentorNames.Length; index++)
            {
                var user = new UserProfile
                {
                    Id = $"mentor-{index + 1}",
                    LoginId = $"mentor{index + 1}",
                    Nickname = GameSeedData.MentorNames[index],
                    Role = UserRole.Mentor,
                    TeamId = "mentor"
                };
                users.Add(user);
                passwordHashesByUser[user.Id] = HashPassword("password");
            }

            for (var index = 0; index < GameSeedData.MemberNames.Length; index++)
            {
                var level = 2 + index;
                var stats = new CharacterStats
                {
                    Level = level,
                    Exp = 20 + index * 35
                };
                stats.RecalculateDerivedStats();
                ApplyGrowthUnlocks(stats);
                var userId = $"member-{index + 1}";
                var user = new UserProfile
                {
                    Id = userId,
                    LoginId = $"member{index + 1}",
                    Nickname = GameSeedData.MemberNames[index],
                    Role = UserRole.Member,
                    TeamId = index < 3 ? "blue" : "magenta"
                };
                users.Add(user);
                passwordHashesByUser[user.Id] = HashPassword("password");
                statsByUser[userId] = ApplyGrowthUnlocks(stats);
            }
        }

        private void SeedSessions()
        {
            var seedMinutes = new[] { 155, 128, 98, 302, 74, 184 };
            var index = 0;
            foreach (var member in Members)
            {
                var evaluation = new AiEvaluation
                {
                    TotalScore = 76 + index * 3,
                    Rank = index > 3 ? AiRank.APlus : AiRank.A,
                    ExpMultiplier = index > 3 ? 1.8f : 1.6f,
                    GoalScore = 84,
                    SpecificityScore = 88,
                    LearningScore = 78 + index,
                    NextActionScore = 82 + index,
                    ContinuityScore = 80,
                    Feedback = "目標と次回タスクが具体的で、継続開発につながっています。"
                };
                sessions.Add(new DevSession
                {
                    Id = Guid.NewGuid().ToString("N"),
                    UserId = member.Id,
                    StartedAtUtc = DateTime.UtcNow.AddDays(-index - 1).AddMinutes(-seedMinutes[index]),
                    EndedAtUtc = DateTime.UtcNow.AddDays(-index - 1),
                    DurationMinutes = seedMinutes[index],
                    Goal = "レイドUIの入力と演出を改善",
                    AchievementRate = 70 + index * 4,
                    Reflection = "操作の流れを確認し、見えづらい状態表示を整理した。",
                    NextTask = "ボス戦中の状態更新と軽量化を進める。",
                    Status = DevSessionStatus.Approved,
                    Evaluation = evaluation,
                    MentorComment = "確認しました。次の開発もこの調子で進めましょう。",
                    ApprovedBy = "mentor-1",
                    ApprovedAtUtc = DateTime.UtcNow.AddDays(-index)
                });
                var stats = GetStats(member.Id);
                stats.AddExp(DevelopmentExpCalculator.Calculate(seedMinutes[index], evaluation));
                ApplyGrowthUnlocks(stats);
                index += 1;
            }
        }

        private IReadOnlyList<DevSession> GetApprovedSessionsForPeriod(RankingPeriod period, DateTime nowUtc)
        {
            return sessions.Where(session => session.Status == DevSessionStatus.Approved && IsSessionInRankingPeriod(session, period, nowUtc)).ToList();
        }

        private static bool IsSessionInRankingPeriod(DevSession session, RankingPeriod period, DateTime nowUtc)
        {
            var occurredAtUtc = session.EndedAtUtc ?? session.StartedAtUtc;
            if (occurredAtUtc > nowUtc)
            {
                return false;
            }

            if (period == RankingPeriod.AllTime)
            {
                return true;
            }

            return period switch
            {
                RankingPeriod.Hourly => occurredAtUtc >= nowUtc.AddHours(-1),
                RankingPeriod.Weekly => occurredAtUtc >= GetWeekStartUtc(nowUtc),
                RankingPeriod.Term => IsInCurrentTerm(occurredAtUtc, nowUtc),
                _ => true
            };
        }

        private static DateTime GetWeekStartUtc(DateTime nowUtc)
        {
            var daysSinceMonday = ((int)nowUtc.DayOfWeek + 6) % 7;
            return nowUtc.Date.AddDays(-daysSinceMonday);
        }

        private static bool IsInCurrentTerm(DateTime occurredAtUtc, DateTime nowUtc)
        {
            var termYear = nowUtc.Month < 4 ? nowUtc.Year - 1 : nowUtc.Year;
            var termStartUtc = new DateTime(termYear, 4, 1, 0, 0, 0, DateTimeKind.Utc);
            var termEndUtc = termStartUtc.AddMonths(6);
            return occurredAtUtc >= termStartUtc && occurredAtUtc < termEndUtc;
        }

        private string ResolveBattleCreator(string requestedUserId, string fallbackUserId = null)
        {
            var normalized = requestedUserId?.Trim();
            if (!string.IsNullOrWhiteSpace(normalized) && users.Any(user => user.Id == normalized && user.Role == UserRole.Mentor))
            {
                return normalized;
            }

            if (!string.IsNullOrWhiteSpace(fallbackUserId))
            {
                return fallbackUserId;
            }

            return GetDefaultMentorUserId();
        }

        private string GetDefaultMentorUserId()
        {
            return users.FirstOrDefault(user => user.Role == UserRole.Mentor)?.Id ?? users.FirstOrDefault()?.Id ?? string.Empty;
        }

        private static DateTime GetWeekStartDateUtc(DateTime utcNow)
        {
            var date = utcNow.ToUniversalTime().Date;
            var mondayOffset = ((int)date.DayOfWeek + 6) % 7;
            return date.AddDays(-mondayOffset);
        }

        private BossBattleState CreateBattleState(BattleStatus status, string createdByUserId)
        {
            var approvedWeight = Mathf.Max(1000, sessions.Where(session => session.Status == DevSessionStatus.Approved).Sum(session => DevelopmentExpCalculator.Calculate(session.DurationMinutes, session.Evaluation?.ExpMultiplier ?? 1f)));
            var maxHp = Mathf.RoundToInt(approvedWeight * 2.5f);
            var bossName = GameSeedData.MentorNames[mentorBossIndex];
            var createdAtUtc = DateTime.UtcNow;
            var battle = new BossBattleState
            {
                Id = Guid.NewGuid().ToString("N"),
                WeekStartDateUtc = GetWeekStartDateUtc(createdAtUtc),
                BaseHp = maxHp,
                HpMultiplier = 1f,
                Status = status,
                Phase = status == BattleStatus.Completed ? BattlePhase.Completed : BattlePhase.TurnStart,
                CreatedByUserId = ResolveBattleCreator(createdByUserId),
                CreatedAtUtc = createdAtUtc,
                Boss = new MentorBoss
                {
                    Id = $"boss-{mentorBossIndex + 1}",
                    Name = $"メンター・{bossName}",
                    BossType = "コードマスター",
                    MaxHp = maxHp,
                    CurrentHp = maxHp,
                    Def = 4 + mentorBossIndex,
                    StoryTeaser = "なぜメンターが襲ってくるのか。次の勝利で記録断片が解放される。"
                }
            };

            var roles = new[] { BattleRole.Attacker, BattleRole.Healer, BattleRole.Defender, BattleRole.Supporter, BattleRole.Attacker, BattleRole.Supporter };
            var weaponKinds = new[] { WeaponKind.Blade, WeaponKind.Rifle, WeaponKind.Shield, WeaponKind.DebugTool, WeaponKind.Cannon, WeaponKind.Rifle };
            var index = 0;
            foreach (var member in Members)
            {
                var stats = GetStats(member.Id);
                battle.Participants.Add(new BattleParticipant
                {
                    UserId = member.Id,
                    Nickname = member.Nickname,
                    Stats = stats,
                    Role = roles[index % roles.Length],
                    Weapon = weaponKinds[index % weaponKinds.Length],
                    CurrentHp = stats.Hp,
                    CurrentMp = stats.Mp
                });
                index += 1;
            }

            return battle;
        }

        private BossBattleState NormalizeBattleSnapshot(BossBattleState battle)
        {
            battle.Actions ??= new List<BattleActionResult>();
            battle.Participants ??= new List<BattleParticipant>();
            battle.Boss ??= new MentorBoss
            {
                Id = "remote-boss",
                Name = "NO BATTLE",
                MaxHp = 1,
                CurrentHp = 0
            };
            battle.CreatedByUserId = ResolveBattleCreator(battle.CreatedByUserId);
            battle.CreatedAtUtc = battle.CreatedAtUtc == default ? DateTime.UtcNow : battle.CreatedAtUtc;
            battle.WeekStartDateUtc = battle.WeekStartDateUtc == default ? GetWeekStartDateUtc(battle.CreatedAtUtc) : battle.WeekStartDateUtc;
            battle.BaseHp = battle.BaseHp > 0 ? battle.BaseHp : Mathf.Max(1, battle.Boss.MaxHp);
            battle.HpMultiplier = battle.HpMultiplier > 0f ? battle.HpMultiplier : 1f;
            battle.TurnCount = Mathf.Max(1, battle.TurnCount);
            battle.TurnNumber = Mathf.Max(1, battle.TurnNumber);
            battle.Boss.MaxHp = Mathf.Max(1, battle.Boss.MaxHp);
            battle.Boss.CurrentHp = Mathf.Clamp(battle.Boss.CurrentHp, 0, battle.Boss.MaxHp);
            EnsureBattleOutcomeSaved(battle);
            return battle;
        }

        private void RebuildBattleFromApprovedLogs()
        {
            if (activeBattle is { Status: BattleStatus.Active or BattleStatus.Completed })
            {
                return;
            }

            var previousBossIndex = mentorBossIndex;
            activeBattle = CreateBattleState(BattleStatus.Scheduled, GetDefaultMentorUserId());
            mentorBossIndex = previousBossIndex;
        }

        private DevSession CompleteActiveSession(string userId, int achievementRate, string reflection, string nextTask)
        {
            var session = GetActiveSession(userId);
            if (session == null)
            {
                throw new InvalidOperationException("進行中のセッションがありません。");
            }

            session.EndedAtUtc = DateTime.UtcNow;
            session.DurationMinutes = Mathf.Max(25, Mathf.RoundToInt((float)(session.EndedAtUtc.Value - session.StartedAtUtc).TotalMinutes));
            session.AchievementRate = Mathf.Clamp(achievementRate, 0, 100);
            session.Reflection = string.IsNullOrWhiteSpace(reflection) ? "実装の進め方と詰まりどころを整理した。" : reflection.Trim();
            session.NextTask = string.IsNullOrWhiteSpace(nextTask) ? "動作確認とUIフィードバックを改善する。" : nextTask.Trim();
            session.SuspiciousFlags = SuspiciousLogDetector.Detect(session, sessions.Where(item => item.UserId == userId));
            return session;
        }

        private DevSession GetAiPendingSession(string sessionId)
        {
            var session = sessions.First(item => item.Id == sessionId);
            if (session.Status != DevSessionStatus.AiPending)
            {
                throw new InvalidOperationException("AI評価待ちのセッションではありません。");
            }

            return session;
        }

        private static DevSessionStatus GetPendingReviewStatus(DevSession session)
        {
            return session.SuspiciousFlags.Count > 0 ? DevSessionStatus.NeedsReview : DevSessionStatus.Pending;
        }

        private static bool IsReviewQueueStatus(DevSessionStatus status)
        {
            return status is DevSessionStatus.Pending or DevSessionStatus.NeedsReview or DevSessionStatus.AiPending;
        }

        private static bool IsResumableSessionStatus(DevSessionStatus status)
        {
            return status is DevSessionStatus.InProgress or DevSessionStatus.Incomplete;
        }

        private UserProfile GetMentor(string mentorUserId)
        {
            var mentor = users.FirstOrDefault(item => item.Id == mentorUserId);
            if (mentor == null || mentor.Role != UserRole.Mentor)
            {
                throw new InvalidOperationException("メンター権限が必要です。");
            }

            return mentor;
        }

        private static void EnsureReviewableSession(DevSession session)
        {
            if (!IsReviewQueueStatus(session.Status))
            {
                throw new InvalidOperationException("承認待ちの開発ログではありません。");
            }
        }

        private static string NormalizeReviewComment(string comment, string fallback)
        {
            return string.IsNullOrWhiteSpace(comment) ? fallback : comment.Trim();
        }

        private static bool MatchesReviewFilter(DevSessionStatus status, DevSessionReviewFilter filter)
        {
            return filter switch
            {
                DevSessionReviewFilter.Pending => status == DevSessionStatus.Pending,
                DevSessionReviewFilter.NeedsReview => status == DevSessionStatus.NeedsReview,
                DevSessionReviewFilter.AiPending => status == DevSessionStatus.AiPending,
                _ => true
            };
        }

        private static string NormalizeAiFailureReason(string failureReason)
        {
            return string.IsNullOrWhiteSpace(failureReason) ? DefaultAiEvaluationFailureReason : failureReason.Trim();
        }

        private static string NormalizeRequired(string value, string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(errorMessage);
            }

            return value.Trim();
        }

        private bool IsPasswordMatch(string userId, string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            if (string.IsNullOrEmpty(userId) || !passwordHashesByUser.TryGetValue(userId, out var expectedPassword))
            {
                expectedPassword = HashPassword("password");
            }

            return string.Equals(HashPassword(password), ToPasswordHash(expectedPassword), StringComparison.Ordinal);
        }

        private void RebuildPasswordFallbacks(IReadOnlyDictionary<string, string> previousPasswords)
        {
            passwordHashesByUser.Clear();
            foreach (var user in users.Where(item => item != null && !string.IsNullOrWhiteSpace(item.Id)))
            {
                passwordHashesByUser[user.Id] = previousPasswords != null && previousPasswords.TryGetValue(user.Id, out var password)
                    ? ToPasswordHash(password)
                    : HashPassword("password");
            }
        }

        private static string NormalizeLoginId(string value)
        {
            var normalized = NormalizeRequired(value, "ログインIDを入力してください。").ToLowerInvariant();
            if (normalized.Length < 3)
            {
                throw new InvalidOperationException("ログインIDは3文字以上で入力してください。");
            }

            if (normalized.Any(ch => !IsLoginIdCharacter(ch)))
            {
                throw new InvalidOperationException("ログインIDは半角英数字、ハイフン、アンダースコア、ドットで入力してください。");
            }

            return normalized;
        }

        private static bool IsLoginIdCharacter(char ch)
        {
            return ch is >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '-'
                or '_'
                or '.';
        }

        private static string NormalizeTeamId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "blue" : value.Trim().ToLowerInvariant();
        }

        private static string NormalizePassword(string value)
        {
            var normalized = NormalizeRequired(value, "新しいパスワードを入力してください。");
            if (normalized.Length < 8)
            {
                throw new InvalidOperationException("パスワードは8文字以上で入力してください。");
            }

            return normalized;
        }

        private static string GenerateTemporaryPassword()
        {
            return $"AOR-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }

        private static string HashPassword(string value)
        {
            var normalized = NormalizeRequired(value, "パスワードを入力してください。");
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalized));
            return PasswordHashPrefix + Convert.ToBase64String(bytes);
        }

        private static string ToPasswordHash(string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && value.StartsWith(PasswordHashPrefix, StringComparison.Ordinal))
            {
                return value;
            }

            return HashPassword(string.IsNullOrWhiteSpace(value) ? "password" : value);
        }

        private static string NormalizeProductUrl(string value)
        {
            var normalized = NormalizeRequired(value, "URLを入力してください。");
            if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException("httpまたはhttpsのURLを入力してください。");
            }

            return uri.ToString();
        }

        private static CharacterStats EnsureStatsCollections(CharacterStats stats)
        {
            stats.UnlockedWeapons ??= new List<WeaponKind>();
            stats.Titles ??= new List<string>();
            stats.Skills ??= new List<string>();
            return stats;
        }

        private static CharacterStats ApplyGrowthUnlocks(CharacterStats stats)
        {
            EnsureStatsCollections(stats);
            AddGrowthUnlock(stats, 1, WeaponKind.Blade, "基礎攻撃");
            AddGrowthUnlock(stats, 2, WeaponKind.Rifle, "省MP射撃");
            AddGrowthUnlock(stats, 3, WeaponKind.Shield, "ガード支援");
            AddGrowthUnlock(stats, 4, WeaponKind.Cannon, "チャージ砲撃");
            AddGrowthUnlock(stats, 5, WeaponKind.DebugTool, "デバッグ支援");

            if (stats.Level >= 7)
            {
                AddUnique(stats.Skills, "レイド指揮");
            }

            return stats;
        }

        private void ApplyGrowthUnlocksForKnownStats()
        {
            foreach (var stats in statsByUser.Values)
            {
                ApplyGrowthUnlocks(stats);
            }
        }

        private static bool IsWeaponUnlocked(CharacterStats stats, WeaponKind weapon)
        {
            return stats != null && ApplyGrowthUnlocks(stats).UnlockedWeapons.Contains(weapon);
        }

        private static void AddGrowthUnlock(CharacterStats stats, int requiredLevel, WeaponKind weapon, string skill)
        {
            if (stats.Level < requiredLevel)
            {
                return;
            }

            AddUnique(stats.UnlockedWeapons, weapon);
            AddUnique(stats.Skills, skill);
        }

        private void ApplyAchievementReward(AchievementEntry achievement)
        {
            var stats = GetStats(achievement.UserId);
            if (TryGetRewardWeapon(achievement.Type, out var weapon))
            {
                achievement.HasRewardWeapon = true;
                achievement.RewardWeapon = weapon;
                AddUnique(stats.UnlockedWeapons, weapon);
            }

            achievement.RewardTitle = GetRewardTitle(achievement.Type);
            achievement.RewardSkill = GetRewardSkill(achievement.Type);
            AddUnique(stats.Titles, achievement.RewardTitle);
            AddUnique(stats.Skills, achievement.RewardSkill);
        }

        private void ApplyApprovedAchievementRewards()
        {
            foreach (var achievement in achievements.Where(item => item.Status == AchievementStatus.Approved))
            {
                ApplyAchievementReward(achievement);
            }
        }

        private static bool TryGetRewardWeapon(AchievementType type, out WeaponKind weapon)
        {
            switch (type)
            {
                case AchievementType.ContestSubmission:
                case AchievementType.Award:
                    weapon = WeaponKind.ContestGear;
                    return true;
                case AchievementType.Release:
                case AchievementType.Update:
                    weapon = WeaponKind.ReleaseGear;
                    return true;
                default:
                    weapon = WeaponKind.Blade;
                    return false;
            }
        }

        private static string GetRewardTitle(AchievementType type)
        {
            return type switch
            {
                AchievementType.ContestSubmission => "大会挑戦者",
                AchievementType.Release => "リリース職人",
                AchievementType.Update => "改善職人",
                AchievementType.Award => "受賞者",
                AchievementType.ContinuousDev => "継続開発者",
                _ => "実績達成者"
            };
        }

        private static string GetRewardSkill(AchievementType type)
        {
            return type switch
            {
                AchievementType.ContestSubmission => "コンテストブースト",
                AchievementType.Release => "リリースブースト",
                AchievementType.Update => "アップデートブースト",
                AchievementType.Award => "アワードブースト",
                AchievementType.ContinuousDev => "継続力",
                _ => "成長補正"
            };
        }

        private static void AddUnique<T>(List<T> values, T value)
        {
            if (!values.Contains(value))
            {
                values.Add(value);
            }
        }

        private void RecordAudit(string actorUserId, string actionType, string targetType, string targetId, string before, string after)
        {
            auditLogs.Add(new AuditLogEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                ActorUserId = actorUserId,
                ActionType = actionType,
                TargetType = targetType,
                TargetId = targetId,
                Before = before,
                After = after,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        private static string DescribeAchievement(AchievementEntry achievement)
        {
            return $"status={achievement.Status};type={achievement.Type};title={achievement.Title};rewardWeapon={achievement.HasRewardWeapon}:{achievement.RewardWeapon};rewardTitle={achievement.RewardTitle};rewardSkill={achievement.RewardSkill}";
        }

        private static string DescribeProduct(ProductEntry product)
        {
            return $"isPublic={product.IsPublic};hiddenBy={product.HiddenBy};title={product.Title};url={product.Url}";
        }

        private static string DescribeSession(DevSession session)
        {
            return $"status={session.Status};durationMinutes={session.DurationMinutes};achievementRate={session.AchievementRate};mentorComment={session.MentorComment};approvedBy={session.ApprovedBy};previewExp={session.PreviewExp}";
        }

        private static string DescribeUser(UserProfile user)
        {
            return $"loginId={user.LoginId};nickname={user.Nickname};role={user.Role};teamId={user.TeamId};initialPasswordChanged={user.InitialPasswordChanged};isActive={user.IsActive}";
        }

        private static AiEvaluation EvaluateSession(DevSession session)
        {
            var goal = ScoreText(session.Goal, 78);
            var specificity = ScoreText(session.Reflection, 74) + (session.Reflection.Contains("実装") || session.Reflection.Contains("検証") ? 8 : 0);
            var learning = ScoreText(session.Reflection, 70) + (session.Reflection.Contains("詰") || session.Reflection.Contains("改善") ? 8 : 0);
            var next = ScoreText(session.NextTask, 76);
            var continuity = Mathf.Clamp(68 + session.AchievementRate / 4, 0, 100);
            var total = Mathf.Clamp(Mathf.RoundToInt((goal + specificity + learning + next + continuity) / 5f), 0, 100);
            var rank = GetRank(total);
            return new AiEvaluation
            {
                TotalScore = total,
                Rank = rank,
                GoalScore = Mathf.Clamp(goal, 0, 100),
                SpecificityScore = Mathf.Clamp(specificity, 0, 100),
                LearningScore = Mathf.Clamp(learning, 0, 100),
                NextActionScore = Mathf.Clamp(next, 0, 100),
                ContinuityScore = Mathf.Clamp(continuity, 0, 100),
                ExpMultiplier = GetMultiplier(rank),
                Feedback = total >= 85
                    ? "目標、振り返り、次回行動がつながっています。承認後は大きく成長に反映されます。"
                    : "保存できました。次回は何を実装・検証したかをもう少し具体的に書くと評価が伸びます。"
            };
        }

        private static int ScoreText(string text, int baseScore)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 35;
            }

            return Mathf.Clamp(baseScore + Mathf.Min(18, text.Trim().Length / 5), 0, 100);
        }

        private static AiRank GetRank(int score)
        {
            if (score >= 95) return AiRank.S;
            if (score >= 85) return AiRank.APlus;
            if (score >= 75) return AiRank.A;
            if (score >= 60) return AiRank.B;
            if (score >= 40) return AiRank.C;
            return AiRank.D;
        }

        private static float GetMultiplier(AiRank rank)
        {
            return rank switch
            {
                AiRank.S => 2.0f,
                AiRank.APlus => 1.8f,
                AiRank.A => 1.6f,
                AiRank.B => 1.3f,
                AiRank.C => 1.0f,
                _ => 0.8f
            };
        }

        private WeaponDefinition ResolveBattleWeapon(WeaponKind weaponKind)
        {
            var weapon = weapons.FirstOrDefault(item => item.Kind == weaponKind)
                ?? weapons.FirstOrDefault(item => item.Kind == WeaponKind.Blade);
            if (weapon == null)
            {
                throw new InvalidOperationException($"戦闘用武器定義が見つかりません: {weaponKind}");
            }

            return weapon;
        }

        private int GetMpCost(BattleActionType actionType, WeaponDefinition weapon)
        {
            var baseCost = actionType switch
            {
                BattleActionType.Strong => 10,
                BattleActionType.FullPower => 20,
                BattleActionType.Support => 10,
                _ => 0
            };
            return Mathf.Max(0, baseCost - weapon.MpEfficiencyBonus);
        }

        private void PersistBattleAction(BattleActionResult result)
        {
            activeBattle.Actions ??= new List<BattleActionResult>();
            activeBattle.Actions.RemoveAll(action => action.UserId == result.UserId && action.TurnNumber == result.TurnNumber);
            activeBattle.Actions.Add(result);
        }

        private static string BattleActionLabel(BattleActionType actionType, int mpCost)
        {
            var label = actionType switch
            {
                BattleActionType.Strong => "強攻撃",
                BattleActionType.FullPower => "全力攻撃",
                BattleActionType.Support => "支援行動",
                BattleActionType.Guard => "ガード",
                _ => "通常攻撃"
            };
            return mpCost > 0 ? $"{label} / MP{mpCost}" : label;
        }

        private int ApplyBossDamage(BattleParticipant participant, int potentialDamage)
        {
            var damage = Mathf.Min(Mathf.Max(0, potentialDamage), Mathf.Max(0, activeBattle.Boss.CurrentHp));
            activeBattle.Boss.CurrentHp = Mathf.Max(0, activeBattle.Boss.CurrentHp - damage);
            activeBattle.TotalDamage += damage;
            participant.TotalDamage += damage;
            return damage;
        }

        private void ApplySupport(BattleParticipant actor, BattleRole role, out int heal, out string support)
        {
            heal = 0;
            support = string.Empty;
            if (role == BattleRole.Healer)
            {
                var healPool = Mathf.Max(activeBattle.Participants.Count, Mathf.RoundToInt(actor.Stats.Mp * 0.8f));
                var healPerParticipant = Mathf.Max(1, Mathf.CeilToInt(healPool / (float)activeBattle.Participants.Count));
                foreach (var participant in activeBattle.Participants)
                {
                    var before = participant.CurrentHp;
                    participant.CurrentHp = Mathf.Min(participant.Stats.Hp, participant.CurrentHp + healPerParticipant);
                    heal += participant.CurrentHp - before;
                }

                support = heal > 0 ? $"チームHPを{heal}回復" : "チームHPは満タン";
                return;
            }

            if (role == BattleRole.Defender)
            {
                var restoredMp = 0;
                foreach (var participant in activeBattle.Participants)
                {
                    var before = participant.CurrentMp;
                    participant.CurrentMp = Mathf.Min(participant.Stats.Mp, participant.CurrentMp + 2);
                    restoredMp += participant.CurrentMp - before;
                }

                support = restoredMp > 0 ? $"味方を保護しMPを{restoredMp}補助" : "味方を保護";
                return;
            }

            if (role == BattleRole.Supporter)
            {
                var beforeDef = activeBattle.Boss.Def;
                activeBattle.Boss.Def = Mathf.Max(0, activeBattle.Boss.Def - 1);
                support = beforeDef > activeBattle.Boss.Def ? $"敵DEF {beforeDef}->{activeBattle.Boss.Def}" : "敵DEFは最低値";
            }
        }

        private int ResolveTeamFollowUp(string userId)
        {
            var totalDamage = 0;
            foreach (var participant in activeBattle.Participants)
            {
                if (participant.UserId == userId || !participant.IsAlive || activeBattle.Boss.CurrentHp <= 0)
                {
                    continue;
                }

                var roleMultiplier = participant.Role switch
                {
                    BattleRole.Attacker => 1.05f,
                    BattleRole.Supporter => 0.72f,
                    BattleRole.Defender => 0.62f,
                    BattleRole.Healer => 0.48f,
                    _ => 0.7f
                };
                var weapon = ResolveBattleWeapon(participant.Weapon);
                var potentialDamage = Mathf.Max(1, Mathf.RoundToInt(participant.Stats.Atk * roleMultiplier * weapon.DamageMultiplier - activeBattle.Boss.Def));
                var damage = ApplyBossDamage(participant, potentialDamage);
                totalDamage += damage;

                if (participant.Role == BattleRole.Healer)
                {
                    participant.TotalHeal += 4;
                    participant.SupportCount += 1;
                }
                else if (participant.Role == BattleRole.Supporter)
                {
                    participant.SupportCount += 1;
                }
            }

            return totalDamage;
        }

        private void AdvanceTurnIfNeeded()
        {
            if (activeBattle.Boss.CurrentHp <= 0)
            {
                CompleteBattle(BattleOutcome.Victory);
                return;
            }

            activeBattle.Phase = BattlePhase.Result;
            activeBattle.TurnNumber += 1;
            if (activeBattle.TurnNumber > activeBattle.TurnCount)
            {
                CompleteBattle(BattleOutcome.Defeat);
                return;
            }

            activeBattle.Phase = BattlePhase.TurnStart;
        }

        private void CompleteBattle(BattleOutcome outcome)
        {
            activeBattle.Outcome = outcome;
            activeBattle.Phase = BattlePhase.Completed;
            activeBattle.Status = BattleStatus.Completed;
            activeBattle.CompletedAtUtc ??= DateTime.UtcNow;
        }

        private string BuildActionMessage(string nickname, BattleActionType actionType, int damage, int heal, string support, int teamFollowUpDamage)
        {
            var actionName = actionType switch
            {
                BattleActionType.Strong => "強攻撃",
                BattleActionType.FullPower => "全力攻撃",
                BattleActionType.Support => "支援行動",
                BattleActionType.Guard => "ガード",
                _ => "通常攻撃"
            };
            if (heal > 0)
            {
                return $"{nickname}の{actionName}: {support} / チーム追撃 {teamFollowUpDamage}";
            }

            if (!string.IsNullOrEmpty(support))
            {
                return $"{nickname}の{actionName}: {damage}ダメージ / {support} / チーム追撃 {teamFollowUpDamage}";
            }

            return $"{nickname}の{actionName}: {damage}ダメージ / チーム追撃 {teamFollowUpDamage}";
        }

        private string GetNickname(string userId)
        {
            return users.FirstOrDefault(user => user.Id == userId)?.Nickname ?? "UNKNOWN";
        }
    }
}
