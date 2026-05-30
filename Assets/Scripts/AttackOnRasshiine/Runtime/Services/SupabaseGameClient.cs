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
        public string SessionToken { get; private set; }
        public string LastError { get; private set; }

        public void ClearSession()
        {
            SessionToken = string.Empty;
        }

        public IEnumerator LoadConfig()
        {
            LastError = string.Empty;
            var path = $"{Application.streamingAssetsPath}/{ConfigFileName}";
            using var request = UnityWebRequest.Get(path);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                IsConfigured = false;
                LastError = "Supabase設定が見つかりません";
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
                LastError = $"Supabase設定を読めません: {exception.Message}";
                yield break;
            }

            supabaseUrl = NormalizeUrl(config?.SupabaseUrl);
            publishableKey = config?.SupabasePublishableKey?.Trim();
            IsConfigured = config != null
                && config.Enabled
                && !string.IsNullOrWhiteSpace(supabaseUrl)
                && !string.IsNullOrWhiteSpace(publishableKey)
                && !supabaseUrl.Contains("xxxxxxxx", StringComparison.OrdinalIgnoreCase);

            if (!IsConfigured)
            {
                LastError = "Supabase設定が空です";
            }
        }

        public IEnumerator Login(string loginId, string password, Action<SupabaseGameApiResponseDto> onComplete)
        {
            var payload = new SupabaseGameApiRequestDto
            {
                Action = "login",
                LoginId = loginId,
                Password = password
            };
            yield return Send(payload, response =>
            {
                if (response?.Ok == true)
                {
                    SessionToken = response.SessionToken;
                }

                onComplete?.Invoke(response);
            });
        }

        public IEnumerator GetSnapshot(Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = "snapshot",
                SessionToken = SessionToken
            }, onComplete);
        }

        public IEnumerator StartSession(string goal, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = "start-session",
                SessionToken = SessionToken,
                Goal = goal
            }, onComplete);
        }

        public IEnumerator CompleteSession(string sessionId, int achievementRate, string reflection, string nextTask, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = "complete-session",
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
                Action = "approve-session",
                SessionToken = SessionToken,
                SessionId = sessionId,
                Comment = comment
            }, onComplete);
        }

        public IEnumerator RejectSession(string sessionId, string comment, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = "reject-session",
                SessionToken = SessionToken,
                SessionId = sessionId,
                Comment = comment
            }, onComplete);
        }

        public IEnumerator SubmitAchievement(AchievementType type, string title, string description, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = "submit-achievement",
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
                Action = "approve-achievement",
                SessionToken = SessionToken,
                AchievementId = achievementId
            }, onComplete);
        }

        public IEnumerator RejectAchievement(string achievementId, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = "reject-achievement",
                SessionToken = SessionToken,
                AchievementId = achievementId
            }, onComplete);
        }

        public IEnumerator SubmitBattleAction(BattleRole role, WeaponKind weapon, BattleActionType actionType, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = "battle-action",
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
                Action = "start-battle",
                SessionToken = SessionToken
            }, onComplete);
        }

        public IEnumerator ResetBattle(Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = "reset-battle",
                SessionToken = SessionToken
            }, onComplete);
        }

        public IEnumerator SetBossHpMultiplier(float multiplier, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = "set-boss-hp",
                SessionToken = SessionToken,
                Multiplier = multiplier
            }, onComplete);
        }

        private IEnumerator Send(SupabaseGameApiRequestDto payload, Action<SupabaseGameApiResponseDto> onComplete)
        {
            if (!IsConfigured)
            {
                onComplete?.Invoke(new SupabaseGameApiResponseDto { Ok = false, Error = LastError });
                yield break;
            }

            var url = $"{supabaseUrl}/functions/v1/game-api";
            var json = JsonUtility.ToJson(payload);
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("apikey", publishableKey);
            request.SetRequestHeader("Authorization", $"Bearer {publishableKey}");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                LastError = request.error;
                onComplete?.Invoke(new SupabaseGameApiResponseDto { Ok = false, Error = request.error });
                yield break;
            }

            SupabaseGameApiResponseDto response;
            try
            {
                response = JsonUtility.FromJson<SupabaseGameApiResponseDto>(request.downloadHandler.text);
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                onComplete?.Invoke(new SupabaseGameApiResponseDto { Ok = false, Error = exception.Message });
                yield break;
            }

            if (response == null)
            {
                response = new SupabaseGameApiResponseDto { Ok = false, Error = "empty_response" };
            }

            if (!response.Ok)
            {
                LastError = response.Error;
            }

            onComplete?.Invoke(response);
        }

        private static string NormalizeUrl(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().TrimEnd('/');
        }
    }
}
