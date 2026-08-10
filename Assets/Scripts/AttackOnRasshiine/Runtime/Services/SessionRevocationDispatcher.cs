using System.Collections;
using UnityEngine;

namespace AttackOnRasshiine.Runtime.Services
{
    /// <summary>
    /// Finishes server-side logout independently of the scene-owned game UI.
    /// The UI can erase a shared-device bearer immediately while this short-lived,
    /// in-memory runner retries revocation across the transition to the login scene.
    /// </summary>
    public sealed class SessionRevocationDispatcher : MonoBehaviour
    {
        private const int MaximumAttempts = 3;

        public static void Enqueue(SupabaseGameClient revocationClient)
        {
            if (!Application.isPlaying || revocationClient is not { IsConfigured: true, HasSession: true })
            {
                return;
            }

            var host = new GameObject(
                "AttackOnRasshiine Session Revocation",
                typeof(SessionRevocationDispatcher));
            DontDestroyOnLoad(host);
            var runner = host.GetComponent<SessionRevocationDispatcher>();
            runner.StartCoroutine(runner.RevokeThenDestroy(revocationClient));
        }

        private IEnumerator RevokeThenDestroy(SupabaseGameClient client)
        {
            var revoked = false;
            for (var attempt = 0; attempt < MaximumAttempts && client.HasSession; attempt++)
            {
                SupabaseGameApiResponseDto response = null;
                yield return client.Logout(result => response = result);
                if (response?.Ok == true || !client.HasSession)
                {
                    revoked = true;
                    break;
                }

                if (attempt + 1 < MaximumAttempts)
                {
                    yield return new WaitForSecondsRealtime(Mathf.Pow(2f, attempt));
                }
            }

            if (!revoked)
            {
                Debug.LogWarning("Server session revocation could not be confirmed after local logout.");
            }

            Destroy(gameObject);
        }
    }
}
