using AttackOnRasshiine.Runtime.Data;
using AttackOnRasshiine.Runtime.Services;

namespace AttackOnRasshiine.Runtime.Scene
{
    public static class RasshiineRuntimeSession
    {
        public static UserProfile CurrentUser { get; private set; }
        public static GameSnapshot Snapshot { get; private set; }
        public static string SessionToken { get; private set; }

        public static void SetUser(UserProfile user)
        {
            CurrentUser = user;
        }

        public static void SetSnapshot(GameSnapshot snapshot)
        {
            Snapshot = snapshot;
        }

        public static void SetSessionToken(string sessionToken)
        {
            SessionToken = SupabaseSessionToken.IsValid(sessionToken)
                ? sessionToken.Trim()
                : string.Empty;
        }

        public static void Clear()
        {
            CurrentUser = null;
            Snapshot = null;
            SessionToken = string.Empty;
        }
    }
}
