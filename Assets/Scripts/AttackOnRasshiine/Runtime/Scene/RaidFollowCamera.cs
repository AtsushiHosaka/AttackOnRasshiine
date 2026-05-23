using UnityEngine;

namespace AttackOnRasshiine.Runtime.Scene
{
    public sealed class RaidFollowCamera : MonoBehaviour
    {
        [SerializeField] private Vector3 overviewPosition = new(0f, 4.6f, -9.2f);
        [SerializeField] private Vector3 overviewEuler = new(26f, 0f, 0f);
        [SerializeField] private float followDistance = 4.9f;
        [SerializeField] private float followHeight = 2.7f;
        [SerializeField] private float lookHeight = 1.25f;
        [SerializeField] private float positionSharpness = 8.5f;
        [SerializeField] private float rotationSharpness = 10f;

        private Transform target;
        private Transform bossAnchor;
        private Camera cameraComponent;

        private void Awake()
        {
            cameraComponent = GetComponent<Camera>();
            if (cameraComponent != null)
            {
                cameraComponent.fieldOfView = 46f;
            }
        }

        public void Follow(Transform newTarget, Transform newBossAnchor)
        {
            target = newTarget;
            bossAnchor = newBossAnchor;
        }

        public void SetOverview()
        {
            target = null;
            bossAnchor = null;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                MoveCamera(overviewPosition, Quaternion.Euler(overviewEuler), 0.78f);
                return;
            }

            var targetPosition = target.position;
            var awayFromBoss = Vector3.back;
            if (bossAnchor != null)
            {
                awayFromBoss = targetPosition - bossAnchor.position;
                awayFromBoss.y = 0f;
                if (awayFromBoss.sqrMagnitude < 0.001f)
                {
                    awayFromBoss = Vector3.back;
                }
            }

            awayFromBoss.Normalize();
            var desiredPosition = targetPosition + awayFromBoss * followDistance + Vector3.up * followHeight;
            var lookPoint = targetPosition + Vector3.up * lookHeight;
            if (bossAnchor != null)
            {
                lookPoint = Vector3.Lerp(lookPoint, bossAnchor.position + Vector3.up * 1.55f, 0.28f);
            }

            var desiredRotation = Quaternion.LookRotation(lookPoint - desiredPosition, Vector3.up);
            MoveCamera(desiredPosition, desiredRotation, 1f);
        }

        private void MoveCamera(Vector3 desiredPosition, Quaternion desiredRotation, float speedScale)
        {
            var positionLerp = 1f - Mathf.Exp(-positionSharpness * speedScale * Time.deltaTime);
            var rotationLerp = 1f - Mathf.Exp(-rotationSharpness * speedScale * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, positionLerp);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationLerp);
        }
    }
}
