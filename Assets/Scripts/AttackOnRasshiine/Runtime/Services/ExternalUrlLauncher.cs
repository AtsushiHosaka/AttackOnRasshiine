using System.Runtime.InteropServices;
using UnityEngine;

namespace AttackOnRasshiine.Runtime.Services
{
    /// <summary>
    /// Opens user-published HTTPS links without exposing the game tab through
    /// window.opener. Application.OpenURL's Unity 6 WebGL implementation opens
    /// a blank target without noopener, so WebGL uses the hardened bridge.
    /// </summary>
    public static class ExternalUrlLauncher
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void AOR_OpenExternalUrlNoOpener(string url);
#endif

        public static bool TryOpen(string value)
        {
            if (!RuntimeUrlSecurity.TryNormalizeExternalHttpsUrl(value, out var normalized))
            {
                return false;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            AOR_OpenExternalUrlNoOpener(normalized);
#else
            Application.OpenURL(normalized);
#endif
            return true;
        }
    }
}
