using System;
using System.Collections.Generic;
using System.Linq;
using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;

namespace AttackOnRasshiine.Runtime.UI
{
    public sealed class DevLogPresenter
    {
        public DevLogSceneState Build(LocalGameRepository repository, UserProfile user, bool apiConfigured, bool isBusy)
        {
            var sessions = repository.GetSessionsForUser(user.Id).ToList();
            var state = new DevLogSceneState
            {
                ActiveSession = repository.GetActiveSession(user.Id),
                ApiModeLabel = apiConfigured ? "本番API保存" : "デモ保存",
                IsBusy = isBusy,
                PendingCount = sessions.Count(IsReviewPending),
                ApprovedCount = sessions.Count(session => session.Status == DevSessionStatus.Approved),
                RejectedCount = sessions.Count(session => session.Status == DevSessionStatus.Rejected),
                AiPendingCount = sessions.Count(session => session.Status == DevSessionStatus.AiPending),
                NeedsReviewCount = sessions.Count(session => session.Status == DevSessionStatus.NeedsReview)
            };

            foreach (var session in sessions)
            {
                state.History.Add(ToView(session, user));
            }

            return state;
        }

        public DevLogValidationResult ValidateStart(string goal)
        {
            return string.IsNullOrWhiteSpace(goal)
                ? DevLogValidationResult.Fail("今日の開発目標を入力してください。")
                : DevLogValidationResult.Ok();
        }

        public DevLogValidationResult ValidateCompletion(int achievementRate, string reflection, string nextTask)
        {
            if (achievementRate < 0 || achievementRate > 100)
            {
                return DevLogValidationResult.Fail("達成度は0〜100%で入力してください。");
            }

            if (string.IsNullOrWhiteSpace(reflection))
            {
                return DevLogValidationResult.Fail("振り返りを入力してください。");
            }

            if (string.IsNullOrWhiteSpace(nextTask))
            {
                return DevLogValidationResult.Fail("次のタスクを入力してください。");
            }

            return DevLogValidationResult.Ok();
        }

        public DevLogSessionView ToView(DevSession session)
        {
            return ToView(session, null);
        }

        public DevLogSessionView ToView(DevSession session, UserProfile viewer)
        {
            var canViewEvaluation = CanViewEvaluation(session, viewer);
            return new DevLogSessionView
            {
                Session = session,
                StatusLabel = StatusLabel(session.Status),
                ReviewStateLabel = ReviewStateLabel(session),
                GrowthStateLabel = GrowthStateLabel(session),
                PendingExp = IsReviewPending(session) ? session.PreviewExp : 0,
                FormalExp = session.Status == DevSessionStatus.Approved ? session.GrowthFeedback?.ExpGained ?? session.PreviewExp : 0,
                CanResume = session.Status == DevSessionStatus.InProgress || session.Status == DevSessionStatus.Incomplete,
                CanViewAiEvaluation = canViewEvaluation,
                AiEvaluationSummaryLabel = canViewEvaluation ? EvaluationSummaryLabel(session) : string.Empty,
                AiEvaluationFeedbackLabel = canViewEvaluation ? session.Evaluation.Feedback ?? string.Empty : string.Empty
            };
        }

        private static bool CanViewEvaluation(DevSession session, UserProfile viewer)
        {
            if (session?.Evaluation == null)
            {
                return false;
            }

            return viewer == null || viewer.Role == UserRole.Mentor || viewer.Id == session.UserId;
        }

        private static string EvaluationSummaryLabel(DevSession session)
        {
            var evaluation = session.Evaluation;
            return $"AI評価 {RankLabel(evaluation.Rank)}  {evaluation.TotalScore}/100  EXP倍率 x{evaluation.ExpMultiplier:0.0}";
        }

        private static string RankLabel(AiRank rank)
        {
            return rank == AiRank.APlus ? "A+" : rank.ToString();
        }

        private static bool IsReviewPending(DevSession session)
        {
            return session.Status == DevSessionStatus.Pending
                || session.Status == DevSessionStatus.NeedsReview
                || session.Status == DevSessionStatus.AiPending;
        }

        private static string ReviewStateLabel(DevSession session)
        {
            return session.Status switch
            {
                DevSessionStatus.InProgress => "未完了セッションを復帰できます",
                DevSessionStatus.Incomplete => "未完了セッション",
                DevSessionStatus.AiPending => "AI評価待ち",
                DevSessionStatus.NeedsReview => "要確認",
                DevSessionStatus.Pending => "承認待ち",
                DevSessionStatus.Approved => "承認済み",
                DevSessionStatus.Rejected => "却下",
                _ => session.Status.ToString()
            };
        }

        private static string GrowthStateLabel(DevSession session)
        {
            return session.Status switch
            {
                DevSessionStatus.Approved => $"正式成長 EXP +{session.GrowthFeedback?.ExpGained ?? session.PreviewExp}",
                DevSessionStatus.Pending or DevSessionStatus.NeedsReview => $"仮成長 EXP +{session.PreviewExp}",
                DevSessionStatus.AiPending => "AI評価後に仮成長を算出",
                DevSessionStatus.Rejected => "成長反映なし",
                _ => "保存前"
            };
        }

        private static string StatusLabel(DevSessionStatus status)
        {
            return status switch
            {
                DevSessionStatus.InProgress => "進行中",
                DevSessionStatus.Pending => "承認待ち",
                DevSessionStatus.Approved => "承認済み",
                DevSessionStatus.Rejected => "却下",
                DevSessionStatus.Incomplete => "未完了",
                DevSessionStatus.NeedsReview => "要確認",
                DevSessionStatus.AiPending => "AI評価待ち",
                _ => status.ToString()
            };
        }
    }

    public sealed class DevLogSceneState
    {
        public DevSession ActiveSession;
        public string ApiModeLabel;
        public bool IsBusy;
        public int PendingCount;
        public int ApprovedCount;
        public int RejectedCount;
        public int AiPendingCount;
        public int NeedsReviewCount;
        public List<DevLogSessionView> History = new();

        public bool HasActiveSession => ActiveSession != null;
    }

    public sealed class DevLogSessionView
    {
        public DevSession Session;
        public string StatusLabel;
        public string ReviewStateLabel;
        public string GrowthStateLabel;
        public int PendingExp;
        public int FormalExp;
        public bool CanResume;
        public bool CanViewAiEvaluation;
        public string AiEvaluationSummaryLabel;
        public string AiEvaluationFeedbackLabel;
    }

    public sealed class DevLogValidationResult
    {
        public bool IsValid;
        public string Message;

        public static DevLogValidationResult Ok()
        {
            return new DevLogValidationResult { IsValid = true, Message = string.Empty };
        }

        public static DevLogValidationResult Fail(string message)
        {
            return new DevLogValidationResult { IsValid = false, Message = message ?? string.Empty };
        }
    }
}
