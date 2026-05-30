using System;
using System.Collections.Generic;
using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using UnityEngine;

namespace AttackOnRasshiine.Runtime.Services
{
    public sealed class LocalGameRepository
    {
        public const string AiFallbackFeedback = "AI評価失敗のため暫定評価です。記録は保存されました。";

        private const string DefaultAiEvaluationFailureReason = "ai_evaluation_failed";
        private const string FallbackModelName = "local-rule-fallback";

        private readonly List<UserProfile> users = new();
        private readonly Dictionary<string, CharacterStats> statsByUser = new();
        private readonly List<DevSession> sessions = new();
        private readonly List<WeaponDefinition> weapons;
        private BossBattleState activeBattle;
        private int mentorBossIndex;

        public LocalGameRepository()
        {
            weapons = GameSeedData.CreateWeapons();
            SeedUsers();
            SeedSessions();
            activeBattle = CreateBattleState(BattleStatus.Scheduled);
        }

        public IReadOnlyList<UserProfile> Users => users;
        public IReadOnlyList<WeaponDefinition> Weapons => weapons;
        public IReadOnlyList<UserProfile> Mentors => users.Where(user => user.Role == UserRole.Mentor).ToList();
        public IReadOnlyList<UserProfile> Members => users.Where(user => user.Role == UserRole.Member).ToList();
        public IReadOnlyList<DevSession> Sessions => sessions;
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

            return password == "password" ? user : null;
        }

        public CharacterStats GetStats(string userId)
        {
            if (statsByUser.TryGetValue(userId, out var stats))
            {
                return stats;
            }

            var fallback = new CharacterStats();
            statsByUser[userId] = fallback;
            return fallback;
        }

        public DevSession GetActiveSession(string userId)
        {
            return sessions.FirstOrDefault(session => session.UserId == userId && session.Status == DevSessionStatus.InProgress);
        }

        public IReadOnlyList<DevSession> GetSessionsForUser(string userId)
        {
            return sessions.Where(session => session.UserId == userId)
                .OrderByDescending(session => session.StartedAtUtc)
                .ToList();
        }

        public IReadOnlyList<DevSession> GetPendingSessions()
        {
            return sessions.Where(session => session.Status is DevSessionStatus.Pending or DevSessionStatus.NeedsReview or DevSessionStatus.AiPending)
                .OrderByDescending(session => session.StartedAtUtc)
                .ToList();
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

        public void ApproveSession(string sessionId, string mentorUserId, string comment)
        {
            var session = sessions.First(item => item.Id == sessionId);
            if (session.Status == DevSessionStatus.Approved)
            {
                return;
            }

            if (session.Status == DevSessionStatus.AiPending && session.Evaluation == null)
            {
                ApplyAiEvaluationFallback(session.Id);
            }

            session.Status = DevSessionStatus.Approved;
            session.ApprovedBy = mentorUserId;
            session.ApprovedAtUtc = DateTime.UtcNow;
            session.MentorComment = comment;
            statsByUser[session.UserId].AddExp(session.PreviewExp);
            RebuildBattleFromApprovedLogs();
        }

        public void RejectSession(string sessionId, string mentorUserId, string comment)
        {
            var session = sessions.First(item => item.Id == sessionId);
            session.Status = DevSessionStatus.Rejected;
            session.ApprovedBy = mentorUserId;
            session.ApprovedAtUtc = DateTime.UtcNow;
            session.MentorComment = comment;
        }

        public int GetApprovedMinutesThisWeek(string userId)
        {
            return sessions.Where(session => session.UserId == userId && session.Status == DevSessionStatus.Approved).Sum(session => session.DurationMinutes);
        }

        public int GetTotalApprovedMinutes()
        {
            return sessions.Where(session => session.Status == DevSessionStatus.Approved).Sum(session => session.DurationMinutes);
        }

        public List<DevSession> GetRankingSessions()
        {
            return sessions.Where(session => session.Status == DevSessionStatus.Approved)
                .OrderByDescending(session => session.DurationMinutes)
                .ToList();
        }

        public BattleParticipant GetParticipant(string userId)
        {
            if (activeBattle is not { IsActive: true })
            {
                return null;
            }

            return activeBattle.Participants.FirstOrDefault(participant => participant.UserId == userId);
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

            participant.Role = role;
            participant.Weapon = weaponKind;
            var weapon = weapons.First(item => item.Kind == weaponKind);
            var mpCost = GetMpCost(actionType, weapon);
            var availableMp = Mathf.Max(0, participant.CurrentMp);
            if (availableMp < mpCost)
            {
                actionType = BattleActionType.Normal;
                mpCost = 0;
            }

            participant.CurrentMp = Mathf.Max(0, participant.CurrentMp - mpCost);
            var roleDamage = role == BattleRole.Attacker ? 1.25f : role == BattleRole.Supporter ? 0.85f : 0.75f;
            var actionDamage = actionType switch
            {
                BattleActionType.Strong => 1.8f,
                BattleActionType.FullPower => 3.0f,
                BattleActionType.Support => 0.4f,
                BattleActionType.Guard => 0.2f,
                _ => 1.0f
            };

            var damage = Mathf.Max(0, Mathf.RoundToInt((participant.Stats.Atk * roleDamage * actionDamage * weapon.DamageMultiplier) - activeBattle.Boss.Def));
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

            activeBattle.Boss.CurrentHp = Mathf.Max(0, activeBattle.Boss.CurrentHp - damage);
            activeBattle.TotalDamage += damage;
            participant.TotalDamage += damage;
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

            AdvanceTurnIfNeeded();
            return result;
        }

        public void ResetBattle()
        {
            mentorBossIndex = (mentorBossIndex + 1) % GameSeedData.MentorNames.Length;
            activeBattle = CreateBattleState(BattleStatus.Scheduled);
        }

        public void StartBattle()
        {
            if (activeBattle == null || activeBattle.Status == BattleStatus.Completed)
            {
                activeBattle = CreateBattleState(BattleStatus.Scheduled);
            }

            activeBattle.Status = BattleStatus.Active;
            activeBattle.Phase = BattlePhase.ActionSelect;
            activeBattle.TurnNumber = 1;
            activeBattle.TotalDamage = 0;
            activeBattle.HighlightUserId = string.Empty;
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

        public void SetBossHpMultiplier(float multiplier)
        {
            var maxHp = Mathf.Max(2500, Mathf.RoundToInt(activeBattle.Boss.MaxHp * multiplier));
            activeBattle.Boss.MaxHp = maxHp;
            activeBattle.Boss.CurrentHp = activeBattle.Status == BattleStatus.Scheduled ? maxHp : Mathf.Min(activeBattle.Boss.CurrentHp, maxHp);
        }

        public void ApplySnapshot(GameSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            users.Clear();
            users.AddRange(snapshot.Users ?? new List<UserProfile>());

            weapons.Clear();
            weapons.AddRange(snapshot.Weapons ?? new List<WeaponDefinition>());

            sessions.Clear();
            sessions.AddRange(snapshot.Sessions ?? new List<DevSession>());

            statsByUser.Clear();
            foreach (var record in snapshot.Stats ?? new List<CharacterStatsRecord>())
            {
                if (!string.IsNullOrWhiteSpace(record.UserId) && record.Stats != null)
                {
                    statsByUser[record.UserId] = record.Stats;
                }
            }

            if (snapshot.ActiveBattle != null)
            {
                activeBattle = snapshot.ActiveBattle;
                foreach (var participant in activeBattle.Participants)
                {
                    if (!string.IsNullOrWhiteSpace(participant.UserId) && participant.Stats != null)
                    {
                        statsByUser[participant.UserId] = participant.Stats;
                    }
                }
            }
        }

        private void SeedUsers()
        {
            for (var index = 0; index < GameSeedData.MentorNames.Length; index++)
            {
                users.Add(new UserProfile
                {
                    Id = $"mentor-{index + 1}",
                    LoginId = $"mentor{index + 1}",
                    Nickname = GameSeedData.MentorNames[index],
                    Role = UserRole.Mentor,
                    TeamId = "mentor"
                });
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
                var userId = $"member-{index + 1}";
                users.Add(new UserProfile
                {
                    Id = userId,
                    LoginId = $"member{index + 1}",
                    Nickname = GameSeedData.MemberNames[index],
                    Role = UserRole.Member,
                    TeamId = index < 3 ? "blue" : "magenta"
                });
                statsByUser[userId] = stats;
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
                    ApprovedBy = "mentor-1",
                    ApprovedAtUtc = DateTime.UtcNow.AddDays(-index)
                });
                statsByUser[member.Id].AddExp(Mathf.RoundToInt(seedMinutes[index] * evaluation.ExpMultiplier));
                index += 1;
            }
        }

        private BossBattleState CreateBattleState(BattleStatus status)
        {
            var approvedWeight = Mathf.Max(1000, sessions.Where(session => session.Status == DevSessionStatus.Approved).Sum(session => Mathf.RoundToInt(session.DurationMinutes * (session.Evaluation?.ExpMultiplier ?? 1f))));
            var maxHp = Mathf.RoundToInt(approvedWeight * 2.5f);
            var bossName = GameSeedData.MentorNames[mentorBossIndex];
            var battle = new BossBattleState
            {
                Id = Guid.NewGuid().ToString("N"),
                Status = status,
                Phase = status == BattleStatus.Completed ? BattlePhase.Completed : BattlePhase.ActionSelect,
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
                var stats = statsByUser[member.Id];
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

        private void RebuildBattleFromApprovedLogs()
        {
            if (activeBattle is { Status: BattleStatus.Active or BattleStatus.Completed })
            {
                return;
            }

            var previousBossIndex = mentorBossIndex;
            activeBattle = CreateBattleState(BattleStatus.Scheduled);
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

        private static string NormalizeAiFailureReason(string failureReason)
        {
            return string.IsNullOrWhiteSpace(failureReason) ? DefaultAiEvaluationFailureReason : failureReason.Trim();
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

        private void ApplySupport(BattleParticipant actor, BattleRole role, out int heal, out string support)
        {
            heal = 0;
            support = string.Empty;
            if (role == BattleRole.Healer)
            {
                heal = Mathf.RoundToInt(actor.Stats.Mp * 0.8f);
                foreach (var participant in activeBattle.Participants)
                {
                    participant.CurrentHp = Mathf.Min(participant.Stats.Hp, participant.CurrentHp + Mathf.RoundToInt(heal / (float)activeBattle.Participants.Count));
                }

                support = $"チームを{heal}回復";
                return;
            }

            if (role == BattleRole.Defender)
            {
                foreach (var participant in activeBattle.Participants)
                {
                    participant.CurrentMp = Mathf.Min(participant.Stats.Mp, participant.CurrentMp + 2);
                }

                support = "味方を保護しMPを補助";
                return;
            }

            if (role == BattleRole.Supporter)
            {
                activeBattle.Boss.Def = Mathf.Max(0, activeBattle.Boss.Def - 1);
                support = "敵DEFを低下";
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
                var weapon = weapons.First(item => item.Kind == participant.Weapon);
                var damage = Mathf.Max(1, Mathf.RoundToInt(participant.Stats.Atk * roleMultiplier * weapon.DamageMultiplier - activeBattle.Boss.Def));
                activeBattle.Boss.CurrentHp = Mathf.Max(0, activeBattle.Boss.CurrentHp - damage);
                activeBattle.TotalDamage += damage;
                participant.TotalDamage += damage;
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
                activeBattle.Phase = BattlePhase.Completed;
                activeBattle.Status = BattleStatus.Completed;
                return;
            }

            activeBattle.TurnNumber += 1;
            if (activeBattle.TurnNumber > activeBattle.TurnCount)
            {
                activeBattle.Phase = BattlePhase.Completed;
                activeBattle.Status = BattleStatus.Completed;
            }
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
