namespace AttackOnRasshiine.Runtime.Scene
{
    /// <summary>
    /// Holds an allow-listed browser route while Unity moves between production scenes.
    /// The value is intentionally separate from the authenticated session so a requested
    /// battle survives the login scene without persisting credentials in the browser.
    /// </summary>
    public static class RasshiineWebRouteIntent
    {
        private static bool hasPendingScene;
        private static RasshiineProductionScene pendingScene;

        public static bool TrySet(string route)
        {
            var normalized = route?.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "home":
                    Clear();
                    return true;
                case "battle":
                    pendingScene = RasshiineProductionScene.Battle;
                    hasPendingScene = true;
                    return true;
                case "front-display":
                    pendingScene = RasshiineProductionScene.FrontDisplay;
                    hasPendingScene = true;
                    return true;
                default:
                    return false;
            }
        }

        public static bool TryPeek(out RasshiineProductionScene scene)
        {
            scene = pendingScene;
            return hasPendingScene;
        }

        public static bool Consume(RasshiineProductionScene expectedScene)
        {
            if (!hasPendingScene || pendingScene != expectedScene)
            {
                return false;
            }

            Clear();
            return true;
        }

        public static void Clear()
        {
            hasPendingScene = false;
            pendingScene = default;
        }
    }
}
