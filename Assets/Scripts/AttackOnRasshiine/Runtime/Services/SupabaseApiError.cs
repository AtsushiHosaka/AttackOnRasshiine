using System;

namespace AttackOnRasshiine.Runtime.Services
{
    public enum SupabaseApiErrorKind
    {
        None,
        Configuration,
        RequestValidation,
        Busy,
        Network,
        AuthenticationRejected,
        AuthenticationExpired,
        Conflict,
        ContractMismatch,
        Server,
        Serialization,
        EmptyResponse,
        Unknown
    }

    public sealed class SupabaseApiError
    {
        public static readonly SupabaseApiError None = new(SupabaseApiErrorKind.None, string.Empty, string.Empty, 0, 0);

        public SupabaseApiError(SupabaseApiErrorKind kind, string code, string message, long httpStatus, int retryAfterSeconds)
        {
            Kind = kind;
            if (kind == SupabaseApiErrorKind.None)
            {
                Code = string.Empty;
                Message = string.Empty;
                HttpStatus = 0;
                RetryAfterSeconds = 0;
                return;
            }

            Code = string.IsNullOrWhiteSpace(code) ? kind.ToString().ToLowerInvariant() : code.Trim();
            Message = string.IsNullOrWhiteSpace(message) ? Code : message.Trim();
            HttpStatus = httpStatus;
            RetryAfterSeconds = Math.Max(0, retryAfterSeconds);
        }

        public SupabaseApiErrorKind Kind { get; }
        public string Code { get; }
        public string Message { get; }
        public long HttpStatus { get; }
        public int RetryAfterSeconds { get; }
        public bool IsAuthExpired => Kind == SupabaseApiErrorKind.AuthenticationExpired;
        public bool CanRetry => Kind == SupabaseApiErrorKind.Network || Kind == SupabaseApiErrorKind.Server || Kind == SupabaseApiErrorKind.Busy;

        public static SupabaseApiError Configuration(string message)
        {
            return new SupabaseApiError(SupabaseApiErrorKind.Configuration, "configuration", message, 0, 0);
        }

        public static SupabaseApiError Busy()
        {
            return new SupabaseApiError(SupabaseApiErrorKind.Busy, "busy", "request_in_progress", 0, 0);
        }

        public static SupabaseApiError RequestValidation(string code)
        {
            return new SupabaseApiError(SupabaseApiErrorKind.RequestValidation, code, code, 0, 0);
        }

        public static SupabaseApiError ContractMismatch(string responseVersion)
        {
            var reportedVersion = string.IsNullOrWhiteSpace(responseVersion) ? "missing" : responseVersion.Trim();
            return new SupabaseApiError(
                SupabaseApiErrorKind.ContractMismatch,
                "contract_mismatch",
                $"Supabase API契約バージョンが違います (expected={SupabaseGameApiContract.CurrentVersion}, actual={reportedVersion})",
                409,
                0);
        }

        public static SupabaseApiError Serialization(string message)
        {
            return new SupabaseApiError(SupabaseApiErrorKind.Serialization, "serialization", message, 0, 0);
        }

        public static SupabaseApiError EmptyResponse()
        {
            return new SupabaseApiError(SupabaseApiErrorKind.EmptyResponse, "empty_response", "empty_response", 0, 0);
        }

        public static SupabaseApiError Transport(long httpStatus, string message)
        {
            return new SupabaseApiError(ResolveHttpKind(httpStatus), ResolveHttpCode(httpStatus), message, httpStatus, 0);
        }

        public static SupabaseApiError FromResponse(SupabaseGameApiResponseDto response, long httpStatus)
        {
            if (response == null)
            {
                return EmptyResponse();
            }

            if (response.Ok)
            {
                return None;
            }

            var code = string.IsNullOrWhiteSpace(response.ErrorCode)
                ? response.Error?.Trim()
                : response.ErrorCode.Trim();
            var message = string.IsNullOrWhiteSpace(response.Error) ? code : response.Error;
            var kind = ResolveResponseKind(code, response.AuthExpired, httpStatus);
            return new SupabaseApiError(kind, code, message, httpStatus, response.RetryAfterSeconds);
        }

        private static SupabaseApiErrorKind ResolveResponseKind(string code, bool authExpired, long httpStatus)
        {
            if (authExpired || httpStatus == 401 || string.Equals(code, "auth_expired", StringComparison.OrdinalIgnoreCase) || string.Equals(code, "invalid_session", StringComparison.OrdinalIgnoreCase))
            {
                return SupabaseApiErrorKind.AuthenticationExpired;
            }

            if (httpStatus == 403 || string.Equals(code, "forbidden", StringComparison.OrdinalIgnoreCase) || string.Equals(code, "auth_rejected", StringComparison.OrdinalIgnoreCase))
            {
                return SupabaseApiErrorKind.AuthenticationRejected;
            }

            if (string.Equals(code, "contract_mismatch", StringComparison.OrdinalIgnoreCase))
            {
                return SupabaseApiErrorKind.ContractMismatch;
            }

            if (httpStatus == 409)
            {
                return SupabaseApiErrorKind.Conflict;
            }

            if (httpStatus >= 500 || httpStatus == 429 || string.Equals(code, "rate_limited", StringComparison.OrdinalIgnoreCase))
            {
                return SupabaseApiErrorKind.Server;
            }

            return SupabaseApiErrorKind.Unknown;
        }

        private static SupabaseApiErrorKind ResolveHttpKind(long httpStatus)
        {
            if (httpStatus == 401)
            {
                return SupabaseApiErrorKind.AuthenticationExpired;
            }

            if (httpStatus >= 500 || httpStatus == 429)
            {
                return SupabaseApiErrorKind.Server;
            }

            if (httpStatus == 409)
            {
                return SupabaseApiErrorKind.Conflict;
            }

            return SupabaseApiErrorKind.Network;
        }

        private static string ResolveHttpCode(long httpStatus)
        {
            return httpStatus > 0 ? $"http_{httpStatus}" : "network";
        }
    }
}
