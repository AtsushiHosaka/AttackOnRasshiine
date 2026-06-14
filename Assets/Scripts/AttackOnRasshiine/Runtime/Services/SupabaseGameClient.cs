using System;
using System.Collections;
using System.Text;
using AttackOnRasshiine.Runtime.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace AttackOnRasshiine.Runtime.Services
{
    public sealed class SupabaseGameClient
    {
        private const string ConfigFileName = "supabase-config.json";

        private string supabaseUrl;
        private string publishableKey;

        public bool IsConfigured { get; private set; }
        public bool IsBusy { get; private set; }
        public bool UseDemoRepositoryFallback { get; private set; }
        public string SessionToken { get; private set; }
        public SupabaseApiError LastApiError { get; private set; } = SupabaseApiError.None;
        public string LastError => LastApiError?.Message ?? string.Empty;
        public bool HasSession => !string.IsNullOrEmpty(SessionToken);
        public bool CanUseDemoRepositoryFallback => !IsConfigured && UseDemoRepositoryFallback;

        public void ClearSession()
        {
            SessionToken = string.Empty;
            LastApiError = SupabaseApiError.None;
        }

        public void RestoreSessionToken(string sessionToken)
        {
            StoreSessionToken(sessionToken);
        }

        public IEnumerator LoadConfig()
        {
            LastApiError = SupabaseApiError.None;
            var path = $"{Application.streamingAssetsPath}/{ConfigFileName}";
            using var request = UnityWebRequest.Get(path);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                IsConfigured = false;
                UseDemoRepositoryFallback = false;
                LastApiError = SupabaseApiError.Configuration("Supabase設定が見つかりません");
                yield break;
            }

            SupabaseRuntimeConfigDto config;
            try
            {
                config = JsonUtility.FromJson<SupabaseRuntimeConfigDto>(request.downloadHandler.text);
            }
            catch (Exception exception)
            {
                IsConfigured = false;
                UseDemoRepositoryFallback = false;
                LastApiError = SupabaseApiError.Configuration($"Supabase設定を読めません: {exception.Message}");
                yield break;
            }

            supabaseUrl = NormalizeUrl(config?.SupabaseUrl);
            publishableKey = config?.SupabasePublishableKey?.Trim();
            UseDemoRepositoryFallback = config?.UseDemoRepositoryFallback ?? false;
            var configVersion = config?.ApiContractVersion?.Trim();
            if (!string.IsNullOrWhiteSpace(configVersion) && configVersion != SupabaseGameApiContract.CurrentVersion)
            {
                IsConfigured = false;
                LastApiError = new SupabaseApiError(
                    SupabaseApiErrorKind.ContractMismatch,
                    "contract_mismatch",
                    $"Supabase API契約バージョンが違います: {configVersion}",
                    0,
                    0);
                yield break;
            }

            IsConfigured = config != null
                && config.Enabled
                && !string.IsNullOrWhiteSpace(supabaseUrl)
                && !string.IsNullOrWhiteSpace(publishableKey)
                && !supabaseUrl.Contains("xxxxxxxx", StringComparison.OrdinalIgnoreCase);

            if (!IsConfigured)
            {
                LastApiError = SupabaseApiError.Configuration("Supabase設定が空です");
            }
        }

        public IEnumerator Login(string loginId, string password, Action<SupabaseGameApiResponseDto> onComplete)
        {
            var payload = new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.Login,
                LoginId = loginId,
                Password = password
            };
            yield return Send(payload, response =>
            {
                if (response?.Ok == true)
                {
                    StoreSessionToken(response.SessionToken);
                }

                onComplete?.Invoke(response);
            });
        }

        public IEnumerator ChangePassword(string currentPassword, string newPassword, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.ChangePassword,
                SessionToken = SessionToken,
                Password = currentPassword,
                NewPassword = newPassword
            }, onComplete);
        }

        public IEnumerator CreateAccount(string loginId, string nickname, UserRole role, string teamId, bool rankingVisible, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.CreateAccount,
                SessionToken = SessionToken,
                LoginId = loginId,
                Nickname = nickname,
                Role = (int)role,
                TeamId = teamId,
                RankingVisible = rankingVisible
            }, onComplete);
        }

        public IEnumerator IssueTemporaryPassword(string userId, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.IssueTemporaryPassword,
                SessionToken = SessionToken,
                UserId = userId
            }, onComplete);
        }

        public IEnumerator GetSnapshot(Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.Snapshot,
                SessionToken = SessionToken
            }, onComplete);
        }

        public IEnumerator GetFrontDisplaySnapshot(Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.FrontDisplaySnapshot
            }, onComplete);
        }

        public IEnumerator StartSession(string goal, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.StartSession,
                SessionToken = SessionToken,
                Goal = goal
            }, onComplete);
        }

        public IEnumerator CompleteSession(string sessionId, int achievementRate, string reflection, string nextTask, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.CompleteSession,
                SessionToken = SessionToken,
                SessionId = sessionId,
                AchievementRate = achievementRate,
                Reflection = reflection,
                NextTask = nextTask
            }, onComplete);
        }

        public IEnumerator ApproveSession(string sessionId, string comment, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.ApproveSession,
                SessionToken = SessionToken,
                SessionId = sessionId,
                Comment = comment
            }, onComplete);
        }

        public IEnumerator RejectSession(string sessionId, string comment, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.RejectSession,
                SessionToken = SessionToken,
                SessionId = sessionId,
                Comment = comment
            }, onComplete);
        }

        public IEnumerator SubmitAchievement(AchievementType type, string title, string description, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.SubmitAchievement,
                SessionToken = SessionToken,
                AchievementType = (int)type,
                Title = title,
                Description = description
            }, onComplete);
        }

        public IEnumerator ApproveAchievement(string achievementId, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.ApproveAchievement,
                SessionToken = SessionToken,
                AchievementId = achievementId
            }, onComplete);
        }

        public IEnumerator RejectAchievement(string achievementId, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.RejectAchievement,
                SessionToken = SessionToken,
                AchievementId = achievementId
            }, onComplete);
        }

        public IEnumerator RegisterProduct(string title, string url, string description, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.RegisterProduct,
                SessionToken = SessionToken,
                Title = title,
                Url = url,
                Description = description
            }, onComplete);
        }

        public IEnumerator HideProduct(string productId, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.HideProduct,
                SessionToken = SessionToken,
                ProductId = productId
            }, onComplete);
        }

        public IEnumerator SubmitBattleAction(BattleRole role, WeaponKind weapon, BattleActionType actionType, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.BattleAction,
                SessionToken = SessionToken,
                Role = (int)role,
                Weapon = (int)weapon,
                ActionType = (int)actionType
            }, onComplete);
        }

        public IEnumerator StartBattle(Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.StartBattle,
                SessionToken = SessionToken
            }, onComplete);
        }

        public IEnumerator ResetBattle(Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.ResetBattle,
                SessionToken = SessionToken
            }, onComplete);
        }

        public IEnumerator SetBossHpMultiplier(float multiplier, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.SetBossHp,
                SessionToken = SessionToken,
                Multiplier = multiplier
            }, onComplete);
        }

        private IEnumerator Send(SupabaseGameApiRequestDto payload, Action<SupabaseGameApiResponseDto> onComplete)
        {
            if (!IsConfigured)
            {
                var error = LastApiError != null && LastApiError.Kind != SupabaseApiErrorKind.None
                    ? LastApiError
                    : SupabaseApiError.Configuration("Supabase設定が読み込まれていません");
                CompleteWithFailure(onComplete, error);
                yield break;
            }

            if (IsBusy)
            {
                CompleteWithFailure(onComplete, SupabaseApiError.Busy());
                yield break;
            }

            IsBusy = true;
            LastApiError = SupabaseApiError.None;
            var url = $"{supabaseUrl}{SupabaseGameApiContract.FunctionPath}";
            var json = JsonUtility.ToJson(payload);
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("apikey", publishableKey);
            request.SetRequestHeader("X-AOR-Contract-Version", SupabaseGameApiContract.CurrentVersion);

            yield return request.SendWebRequest();
            IsBusy = false;

            if (request.result != UnityWebRequest.Result.Success)
            {
                var error = SupabaseApiError.Transport(request.responseCode, request.error);
                CompleteWithFailure(onComplete, error);
                yield break;
            }

            SupabaseGameApiResponseDto response;
            try
            {
                response = JsonUtility.FromJson<SupabaseGameApiResponseDto>(request.downloadHandler.text);
            }
            catch (Exception exception)
            {
                CompleteWithFailure(onComplete, SupabaseApiError.Serialization(exception.Message));
                yield break;
            }

            if (response == null)
            {
                CompleteWithFailure(onComplete, SupabaseApiError.EmptyResponse());
                yield break;
            }

            if (!response.Ok)
            {
                var error = SupabaseApiError.FromResponse(response, request.responseCode);
                CompleteWithFailure(onComplete, error, response);
                yield break;
            }

            StoreSessionToken(response.SessionToken);
            LastApiError = SupabaseApiError.None;
            onComplete?.Invoke(response);
        }

        private void CompleteWithFailure(Action<SupabaseGameApiResponseDto> onComplete, SupabaseApiError error, SupabaseGameApiResponseDto response = null)
        {
            LastApiError = error ?? SupabaseApiError.EmptyResponse();
            if (LastApiError.IsAuthExpired)
            {
                SessionToken = string.Empty;
            }

            response ??= new SupabaseGameApiResponseDto();
            response.Ok = false;
            response.ErrorCode = LastApiError.Code;
            response.Error = LastApiError.Message;
            response.AuthExpired = LastApiError.IsAuthExpired;
            response.RetryAfterSeconds = LastApiError.RetryAfterSeconds;
            onComplete?.Invoke(response);
        }

        private void StoreSessionToken(string sessionToken)
        {
            if (!string.IsNullOrWhiteSpace(sessionToken))
            {
                SessionToken = sessionToken.Trim();
            }
        }

        private static string NormalizeUrl(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().TrimEnd('/');
        }
    }
}
