using System;
using System.Collections;
using System.IO;
using System.Text;
using AttackOnRasshiine.Runtime.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace AttackOnRasshiine.Runtime.Services
{
    public sealed class SupabaseGameClient
    {
        private const string ConfigFileName = "supabase-config.json";
        private const int ApiRequestTimeoutSeconds = 20;
        public const int MaximumApiResponseBytes = 2 * 1024 * 1024;
        public const int MaximumConfigResponseBytes = 32 * 1024;

        private string supabaseUrl;
        private string publishableKey;
        private string apiPath = SupabaseGameApiContract.DefaultRuntimeFunctionPath;
        private string frontDisplayEtag = string.Empty;
        private string battleStateEtag = string.Empty;

        public bool IsConfigured { get; private set; }
        public bool IsBusy { get; private set; }
        public bool UseDemoRepositoryFallback { get; private set; }
        public string SessionToken { get; private set; }
        public SupabaseApiError LastApiError { get; private set; } = SupabaseApiError.None;
        public string LastError => LastApiError?.Message ?? string.Empty;
        public bool HasSession => !string.IsNullOrEmpty(SessionToken);
        public bool CanUseDemoRepositoryFallback => !IsConfigured && UseDemoRepositoryFallback;
        public string ApiPath => apiPath;
        public string ApiEndpoint => IsConfigured ? SupabaseGameApiEndpoint.Build(supabaseUrl, apiPath) : string.Empty;

        public void ClearSession()
        {
            SessionToken = string.Empty;
            battleStateEtag = string.Empty;
            LastApiError = SupabaseApiError.None;
        }

        public void RestoreSessionToken(string sessionToken)
        {
            SessionToken = SupabaseSessionToken.IsValid(sessionToken)
                ? sessionToken.Trim()
                : string.Empty;
        }

        public SupabaseGameClient CreateSessionRevocationClient()
        {
            if (!IsConfigured || !HasSession)
            {
                return null;
            }

            var client = CreateAnonymousClient();
            client.RestoreSessionToken(SessionToken);
            return client.HasSession ? client : null;
        }

        public SupabaseGameClient CreateAnonymousClient()
        {
            if (!IsConfigured)
            {
                return null;
            }

            return new SupabaseGameClient
            {
                supabaseUrl = supabaseUrl,
                publishableKey = publishableKey,
                apiPath = apiPath,
                IsConfigured = true,
                UseDemoRepositoryFallback = false
            };
        }

        public IEnumerator LoadConfig()
        {
            LastApiError = SupabaseApiError.None;
            var path = $"{Application.streamingAssetsPath}/{ConfigFileName}";
            var json = string.Empty;
#if UNITY_EDITOR
            var editorPath = Path.Combine(Application.dataPath, "StreamingAssets", ConfigFileName);
            if (File.Exists(editorPath))
            {
                try
                {
                    json = File.ReadAllText(editorPath);
                }
                catch (Exception exception)
                {
                    IsConfigured = false;
                    UseDemoRepositoryFallback = false;
                    LastApiError = SupabaseApiError.Configuration($"Supabase設定を読めません: {exception.Message}");
                    yield break;
                }
            }
#endif
#if UNITY_EDITOR || !UNITY_WEBGL
            if (string.IsNullOrWhiteSpace(json)
                && !path.Contains("://", StringComparison.Ordinal)
                && File.Exists(path))
            {
                try
                {
                    json = File.ReadAllText(path);
                }
                catch (Exception exception)
                {
                    IsConfigured = false;
                    UseDemoRepositoryFallback = false;
                    LastApiError = SupabaseApiError.Configuration($"Supabase設定を読めません: {exception.Message}");
                    yield break;
                }
            }
#endif
            if (string.IsNullOrWhiteSpace(json))
            {
                using var request = UnityWebRequest.Get(path);
                var boundedConfig = new BoundedDownloadHandler(MaximumConfigResponseBytes);
                request.downloadHandler = boundedConfig;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success || boundedConfig.LimitExceeded)
                {
                    IsConfigured = false;
                    UseDemoRepositoryFallback = false;
                    LastApiError = SupabaseApiError.Configuration("Supabase設定が見つかりません");
                    yield break;
                }

                json = request.downloadHandler.text;
            }

            TryConfigureFromJson(json);
        }

        public bool TryConfigureFromJson(string json)
        {
            ResetConfiguration();
            if (string.IsNullOrWhiteSpace(json))
            {
                LastApiError = SupabaseApiError.Configuration("Supabase設定が空です");
                return false;
            }

            if (SupabaseRuntimeConfigSecurity.ContainsForbiddenSecretConfiguration(json))
            {
                LastApiError = SupabaseApiError.Configuration("公開クライアント用設定に秘密鍵を含めることはできません");
                return false;
            }

            SupabaseRuntimeConfigDto config;
            try
            {
                config = JsonUtility.FromJson<SupabaseRuntimeConfigDto>(json);
            }
            catch (Exception exception)
            {
                LastApiError = SupabaseApiError.Configuration($"Supabase設定を読めません: {exception.Message}");
                return false;
            }

            if (config == null)
            {
                LastApiError = SupabaseApiError.Configuration("Supabase設定が空です");
                return false;
            }

#if UNITY_EDITOR
            UseDemoRepositoryFallback = config.UseDemoRepositoryFallback;
#else
            // Seed accounts and local mutations are an editor-only preview facility. A
            // release player must fail closed when its authoritative API is unavailable.
            UseDemoRepositoryFallback = false;
#endif
            var configVersion = config.ApiContractVersion?.Trim();
            if (!string.IsNullOrWhiteSpace(configVersion)
                && !string.Equals(configVersion, SupabaseGameApiContract.CurrentVersion, StringComparison.Ordinal))
            {
                LastApiError = SupabaseApiError.ContractMismatch(configVersion);
                return false;
            }

            var candidateKey = config.SupabasePublishableKey?.Trim();
            if (!config.Enabled)
            {
                LastApiError = SupabaseApiError.Configuration("Supabase接続が無効です");
                return false;
            }

            if (!RuntimeUrlSecurity.TryNormalizeHttpsOrigin(config.SupabaseUrl, out var normalizedUrl))
            {
                LastApiError = SupabaseApiError.Configuration("Supabase URLは公開HTTPS originで指定してください");
                return false;
            }

            if (normalizedUrl.Contains("xxxxxxxx", StringComparison.OrdinalIgnoreCase))
            {
                LastApiError = SupabaseApiError.Configuration("Supabase URLが空です");
                return false;
            }

            if (!SupabaseRuntimeConfigSecurity.IsPublishableKey(candidateKey))
            {
                LastApiError = SupabaseApiError.Configuration("Supabase Publishable Keyが必要です");
                return false;
            }

            if (!SupabaseGameApiEndpoint.TryResolvePath(config.ApiFunctionName, config.ApiPath, out var resolvedApiPath))
            {
                LastApiError = SupabaseApiError.Configuration("Supabase APIパスが不正です");
                return false;
            }

            supabaseUrl = normalizedUrl;
            publishableKey = candidateKey;
            apiPath = resolvedApiPath;
            IsConfigured = true;
            LastApiError = SupabaseApiError.None;
            return true;
        }

        public IEnumerator Login(string loginId, string password, Action<SupabaseGameApiResponseDto> onComplete)
        {
            var payload = new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.Login,
                LoginId = loginId,
                Password = password
            };
            SupabaseGameApiResponseDto response = null;
            yield return Send(payload, result => response = result);
            if (response?.Ok == true && !RequireFreshSessionToken(response))
            {
                onComplete?.Invoke(response);
                yield break;
            }
            if (response?.Ok == true && response.User?.InitialPasswordChanged == true)
            {
                yield return AttachFreshSnapshot(response, true);
            }
            onComplete?.Invoke(response);
        }

        public IEnumerator RestoreSession(Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.RestoreSession,
                SessionToken = SessionToken
            }, response =>
            {
                if (response?.Ok == true && !string.IsNullOrWhiteSpace(response.SessionToken))
                {
                    StoreSessionToken(response.SessionToken);
                }

                onComplete?.Invoke(response);
            });
        }

        public IEnumerator Logout(Action<SupabaseGameApiResponseDto> onComplete)
        {
            if (!HasSession)
            {
                onComplete?.Invoke(new SupabaseGameApiResponseDto
                {
                    Ok = true,
                    ContractVersion = SupabaseGameApiContract.CurrentVersion
                });
                yield break;
            }

            yield return Send(SupabaseGameApiRequestFactory.Logout(SessionToken), response =>
            {
                if (response?.Ok == true)
                {
                    ClearSession();
                }

                onComplete?.Invoke(response);
            });
        }

        public IEnumerator ChangePassword(string currentPassword, string newPassword, Action<SupabaseGameApiResponseDto> onComplete)
        {
            var payload = new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.ChangePassword,
                SessionToken = SessionToken,
                Password = currentPassword,
                NewPassword = newPassword
            };
            SupabaseGameApiResponseDto response = null;
            yield return Send(payload, result => response = result);
            if (response?.Ok == true && !RequireFreshSessionToken(response))
            {
                onComplete?.Invoke(response);
                yield break;
            }
            if (response?.Ok == true)
            {
                yield return AttachFreshSnapshot(response, false);
            }
            onComplete?.Invoke(response);
        }

        public IEnumerator CreateAccount(string loginId, string nickname, UserRole role, string teamId, bool rankingVisible, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return SendMutationAndRefresh(new SupabaseGameApiRequestDto
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
            yield return SendMutationAndRefresh(new SupabaseGameApiRequestDto
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
            yield return SendTyped(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.FrontDisplaySnapshot
            }, onComplete, ConditionalResponseKind.FrontDisplay);
        }

        public IEnumerator GetBattleState(Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return SendTyped(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.BattleState,
                SessionToken = SessionToken
            }, onComplete, ConditionalResponseKind.BattleState);
        }

        public IEnumerator StartSession(string goal, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return SendMutationAndRefresh(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.StartSession,
                SessionToken = SessionToken,
                Goal = goal
            }, onComplete);
        }

        public IEnumerator CompleteSession(string sessionId, int achievementRate, string reflection, string nextTask, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return SendMutationAndRefresh(new SupabaseGameApiRequestDto
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
            yield return SendMutationAndRefresh(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.ApproveSession,
                SessionToken = SessionToken,
                SessionId = sessionId,
                Comment = comment
            }, onComplete);
        }

        public IEnumerator ApproveSession(string sessionId, int durationMinutes, string comment, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return SendMutationAndRefresh(new ApproveSessionRequestDto
            {
                SessionToken = SessionToken,
                SessionId = sessionId,
                DurationMinutes = durationMinutes,
                Comment = comment
            }, onComplete);
        }

        public IEnumerator RejectSession(string sessionId, string comment, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return SendMutationAndRefresh(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.RejectSession,
                SessionToken = SessionToken,
                SessionId = sessionId,
                Comment = comment
            }, onComplete);
        }

        public IEnumerator SubmitAchievement(AchievementType type, string title, string description, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return SendMutationAndRefresh(new SupabaseGameApiRequestDto
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
            yield return SendMutationAndRefresh(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.ApproveAchievement,
                SessionToken = SessionToken,
                AchievementId = achievementId
            }, onComplete);
        }

        public IEnumerator RejectAchievement(string achievementId, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return SendMutationAndRefresh(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.RejectAchievement,
                SessionToken = SessionToken,
                AchievementId = achievementId
            }, onComplete);
        }

        public IEnumerator RegisterProduct(string title, string url, string description, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return SendMutationAndRefresh(new SupabaseGameApiRequestDto
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
            yield return SendMutationAndRefresh(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.HideProduct,
                SessionToken = SessionToken,
                ProductId = productId
            }, onComplete);
        }

        public IEnumerator GetCosmeticInventory(Action<CosmeticInventoryResponseDto> onComplete)
        {
            yield return SendTyped(
                SupabaseGameApiRequestFactory.CosmeticInventory(SessionToken),
                onComplete);
        }

        public IEnumerator RollCosmeticGacha(string idempotencyKey, Action<RollCosmeticGachaResponseDto> onComplete)
        {
            if (!SupabaseIdempotencyKey.IsValid(idempotencyKey))
            {
                CompleteWithFailure(onComplete, SupabaseApiError.RequestValidation("idempotency_key_invalid"));
                yield break;
            }

            yield return SendTyped(
                SupabaseGameApiRequestFactory.RollCosmeticGacha(SessionToken, idempotencyKey),
                onComplete);
        }

        public IEnumerator EquipCosmetic(string itemId, Action<EquipCosmeticResponseDto> onComplete)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                CompleteWithFailure(onComplete, SupabaseApiError.RequestValidation("item_id_required"));
                yield break;
            }

            yield return SendTyped(
                SupabaseGameApiRequestFactory.EquipCosmetic(SessionToken, itemId),
                onComplete);
        }

        public IEnumerator SubmitBattleAction(BattleRole role, WeaponKind weapon, BattleActionType actionType, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return SubmitBattleAction(
                role,
                weapon,
                actionType,
                SupabaseIdempotencyKey.CreateBattleActionKey(),
                response => onComplete?.Invoke(response));
        }

        public IEnumerator SubmitBattleAction(
            BattleRole role,
            WeaponKind weapon,
            BattleActionType actionType,
            string idempotencyKey,
            Action<BattleActionResponseDto> onComplete)
        {
            if (!SupabaseIdempotencyKey.IsValid(idempotencyKey))
            {
                CompleteWithFailure(onComplete, SupabaseApiError.RequestValidation("idempotency_key_invalid"));
                yield break;
            }

            yield return SendTyped(
                SupabaseGameApiRequestFactory.BattleAction(
                    SessionToken,
                    idempotencyKey,
                    (int)role,
                    (int)weapon,
                    (int)actionType),
                onComplete);
        }

        public IEnumerator StartBattle(Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.StartBattle,
                SessionToken = SessionToken
            }, onComplete);
        }

        public IEnumerator ResetBattle(
            string idempotencyKey,
            string expectedRaidEpoch,
            Action<ResetBattleResponseDto> onComplete)
        {
            if (!SupabaseIdempotencyKey.IsValid(idempotencyKey))
            {
                CompleteWithFailure(onComplete, SupabaseApiError.RequestValidation("idempotency_key_invalid"));
                yield break;
            }

            if (!SupabaseRaidEpoch.IsValid(expectedRaidEpoch))
            {
                CompleteWithFailure(onComplete, SupabaseApiError.RequestValidation("battle_epoch_invalid"));
                yield break;
            }

            ResetBattleResponseDto response = null;
            yield return SendTyped<ResetBattleResponseDto>(
                SupabaseGameApiRequestFactory.ResetBattle(
                    SessionToken,
                    idempotencyKey,
                    expectedRaidEpoch),
                result => response = result);

            if (response?.Ok == true
                && !SupabaseResetBattleContract.IsValidSuccessResponse(response, expectedRaidEpoch))
            {
                CompleteWithFailure(
                    onComplete,
                    SupabaseApiError.Serialization("reset_battle_response_invalid"),
                    response);
                yield break;
            }

            onComplete?.Invoke(response);
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

        public IEnumerator GetRankings(Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.Rankings,
                SessionToken = SessionToken
            }, onComplete);
        }

        private IEnumerator Send(SupabaseGameApiRequestDto payload, Action<SupabaseGameApiResponseDto> onComplete)
        {
            yield return SendTyped(payload, onComplete);
        }

        private IEnumerator SendMutationAndRefresh(object payload, Action<SupabaseGameApiResponseDto> onComplete)
        {
            SupabaseGameApiResponseDto response = null;
            yield return SendTyped<SupabaseGameApiResponseDto>(payload, result => response = result);
            if (response?.Ok == true && HasSession)
            {
                yield return AttachFreshSnapshot(response, false);
            }
            onComplete?.Invoke(response);
        }

        private IEnumerator AttachFreshSnapshot(SupabaseGameApiResponseDto response, bool required)
        {
            SupabaseGameApiResponseDto fresh = null;
            yield return Send(new SupabaseGameApiRequestDto
            {
                Action = SupabaseGameApiActions.Snapshot,
                SessionToken = SessionToken
            }, result => fresh = result);

            if (fresh?.Ok == true && fresh.Snapshot != null)
            {
                response.Snapshot = fresh.Snapshot;
                LastApiError = SupabaseApiError.None;
                yield break;
            }

            if (!HasSession || fresh?.AuthExpired == true)
            {
                response.Ok = false;
                response.AuthExpired = true;
                response.ErrorCode = fresh?.ErrorCode ?? "auth_expired";
                response.Error = response.ErrorCode;
                yield break;
            }

            if (required)
            {
                response.Ok = false;
                response.ErrorCode = "snapshot_refresh_failed";
                response.Error = response.ErrorCode;
                yield break;
            }

            // The write acknowledgement remains authoritative even if this
            // independent read failed. Do not make callers retry a committed
            // mutation; normal polling/manual refresh will converge state.
            LastApiError = SupabaseApiError.None;
        }

        private IEnumerator SendTyped<TResponse>(
            object payload,
            Action<TResponse> onComplete,
            ConditionalResponseKind conditionalKind = ConditionalResponseKind.None)
            where TResponse : SupabaseGameApiResponseDto, new()
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

            using var requestBusyLease = BeginRequestBusyLease();
            LastApiError = SupabaseApiError.None;
            var url = SupabaseGameApiEndpoint.Build(supabaseUrl, apiPath);
            var json = JsonUtility.ToJson(payload);
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.timeout = ApiRequestTimeoutSeconds;
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            var boundedDownload = new BoundedDownloadHandler(MaximumApiResponseBytes);
            request.downloadHandler = boundedDownload;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("apikey", publishableKey);
            request.SetRequestHeader(SupabaseGameApiContract.ContractHeader, SupabaseGameApiContract.CurrentVersion);
            var etag = GetConditionalEtag(conditionalKind);
            if (!string.IsNullOrEmpty(etag))
            {
                request.SetRequestHeader("If-None-Match", etag);
            }

            yield return request.SendWebRequest();

            if (request.responseCode == 304 && conditionalKind != ConditionalResponseKind.None)
            {
                LastApiError = SupabaseApiError.None;
                onComplete?.Invoke(new TResponse
                {
                    Ok = true,
                    NotModified = true,
                    ContractVersion = SupabaseGameApiContract.CurrentVersion
                });
                yield break;
            }

            if (request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.DataProcessingError)
            {
                var error = boundedDownload.LimitExceeded
                    ? SupabaseApiError.Serialization("response_too_large")
                    : SupabaseApiError.Transport(request.responseCode, request.error);
                CompleteWithFailure(onComplete, error);
                yield break;
            }

            TResponse response;
            try
            {
                response = JsonUtility.FromJson<TResponse>(request.downloadHandler.text);
            }
            catch (Exception exception)
            {
                var error = request.result == UnityWebRequest.Result.ProtocolError
                    ? SupabaseApiError.Transport(request.responseCode, request.error)
                    : SupabaseApiError.Serialization(exception.Message);
                CompleteWithFailure(onComplete, error);
                yield break;
            }

            if (response == null)
            {
                CompleteWithFailure(onComplete, SupabaseApiError.EmptyResponse());
                yield break;
            }

            // Authentication status is a privacy boundary and must take precedence over
            // body/contract parsing. Gateways sometimes replace a 401 body with their own
            // JSON shape; treating that as a mere contract error would retain a stale bearer.
            if (request.responseCode == 401)
            {
                CompleteWithFailure(onComplete, SupabaseApiError.Transport(request.responseCode, request.error), response);
                yield break;
            }

            var contractError = SupabaseGameApiContract.ValidateResponseVersion(response.ContractVersion);
            if (contractError.Kind != SupabaseApiErrorKind.None)
            {
                CompleteWithFailure(onComplete, contractError, response);
                yield break;
            }

            if (!response.Ok)
            {
                var error = SupabaseApiError.FromResponse(response, request.responseCode);
                CompleteWithFailure(onComplete, error, response);
                yield break;
            }

            if (request.result == UnityWebRequest.Result.ProtocolError)
            {
                CompleteWithFailure(onComplete, SupabaseApiError.Transport(request.responseCode, request.error), response);
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(response.SessionToken)
                && !StoreSessionToken(response.SessionToken))
            {
                SessionToken = string.Empty;
                CompleteWithFailure(onComplete, SupabaseApiError.Serialization("invalid_session_token"), response);
                yield break;
            }

            StoreConditionalEtag(conditionalKind, request.GetResponseHeader("ETag"));

            LastApiError = SupabaseApiError.None;
            onComplete?.Invoke(response);
        }

        private void CompleteWithFailure<TResponse>(Action<TResponse> onComplete, SupabaseApiError error, TResponse response = null)
            where TResponse : SupabaseGameApiResponseDto, new()
        {
            LastApiError = error ?? SupabaseApiError.EmptyResponse();
            if (LastApiError.IsAuthExpired)
            {
                SessionToken = string.Empty;
            }

            response ??= new TResponse();
            response.Ok = false;
            response.ContractVersion ??= SupabaseGameApiContract.CurrentVersion;
            response.ErrorCode = LastApiError.Code;
            response.Error = LastApiError.Code;
            response.AuthExpired = LastApiError.IsAuthExpired;
            response.RetryAfterSeconds = LastApiError.RetryAfterSeconds;
            onComplete?.Invoke(response);
        }

        private void ResetConfiguration()
        {
            IsConfigured = false;
            UseDemoRepositoryFallback = false;
            supabaseUrl = string.Empty;
            publishableKey = string.Empty;
            apiPath = SupabaseGameApiContract.DefaultRuntimeFunctionPath;
            frontDisplayEtag = string.Empty;
            battleStateEtag = string.Empty;
            LastApiError = SupabaseApiError.None;
        }

        private string GetConditionalEtag(ConditionalResponseKind kind)
        {
            return kind switch
            {
                ConditionalResponseKind.FrontDisplay => frontDisplayEtag,
                ConditionalResponseKind.BattleState => battleStateEtag,
                _ => string.Empty
            };
        }

        private void StoreConditionalEtag(ConditionalResponseKind kind, string value)
        {
            var etag = value?.Trim();
            var quotedTagStart = etag != null && etag.StartsWith("W/\"", StringComparison.Ordinal)
                ? 2
                : 0;
            if (kind == ConditionalResponseKind.None
                || string.IsNullOrEmpty(etag)
                || etag.Length != 66 + quotedTagStart
                || etag[quotedTagStart] != '"'
                || etag[^1] != '"')
            {
                return;
            }

            for (var index = quotedTagStart + 1; index < etag.Length - 1; index++)
            {
                var character = etag[index];
                if (!Uri.IsHexDigit(character))
                {
                    return;
                }
            }

            if (kind == ConditionalResponseKind.FrontDisplay)
            {
                frontDisplayEtag = etag;
            }
            else if (kind == ConditionalResponseKind.BattleState)
            {
                battleStateEtag = etag;
            }
        }

        private enum ConditionalResponseKind
        {
            None,
            FrontDisplay,
            BattleState
        }

        private IDisposable BeginRequestBusyLease()
        {
            IsBusy = true;
            return new RequestBusyLease(this);
        }

        private sealed class RequestBusyLease : IDisposable
        {
            private SupabaseGameClient owner;

            public RequestBusyLease(SupabaseGameClient owner)
            {
                this.owner = owner;
            }

            public void Dispose()
            {
                if (owner == null)
                {
                    return;
                }

                owner.IsBusy = false;
                owner = null;
            }
        }

        private bool RequireFreshSessionToken(SupabaseGameApiResponseDto response)
        {
            if (response != null && StoreSessionToken(response.SessionToken))
            {
                return true;
            }

            SessionToken = string.Empty;
            LastApiError = SupabaseApiError.Serialization("invalid_session_token");
            response ??= new SupabaseGameApiResponseDto();
            response.Ok = false;
            response.Error = LastApiError.Code;
            response.ErrorCode = LastApiError.Code;
            return false;
        }

        private bool StoreSessionToken(string sessionToken)
        {
            if (!SupabaseSessionToken.IsValid(sessionToken))
            {
                return false;
            }

            SessionToken = sessionToken.Trim();
            return true;
        }
    }

    internal sealed class BoundedDownloadHandler : DownloadHandlerScript
    {
        private const int ReceiveBufferBytes = 32 * 1024;
        private readonly int maximumBytes;
        private readonly MemoryStream received = new();

        public bool LimitExceeded { get; private set; }
        public int ReceivedBytes => checked((int)received.Length);

        public BoundedDownloadHandler(int maximumBytes)
            : base(new byte[ReceiveBufferBytes])
        {
            if (maximumBytes < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            }

            this.maximumBytes = maximumBytes;
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength <= 0)
            {
                return true;
            }

            if (dataLength > data.Length || received.Length + dataLength > maximumBytes)
            {
                LimitExceeded = true;
                return false;
            }

            received.Write(data, 0, dataLength);
            return true;
        }

        protected override byte[] GetData()
        {
            return received.ToArray();
        }

        protected override string GetText()
        {
            return Encoding.UTF8.GetString(received.GetBuffer(), 0, ReceivedBytes);
        }
    }
}
