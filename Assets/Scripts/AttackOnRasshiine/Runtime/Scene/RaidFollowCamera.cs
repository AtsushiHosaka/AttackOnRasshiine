using UnityEngine;

namespace AttackOnRasshiine.Runtime.Scene
{
    public sealed class RaidFollowCamera : MonoBehaviour
    {
        private const float ReferenceBattleAspect = 16f / 9f;
        private const float BattleFollowFovFloor = 54f;
        private const float BattleFollowDistanceFloor = 9.45f;
        private const float BattleFollowHeightFloor = 5.25f;
        private const float BattleArenaAxisBlendFloor = 0.86f;
        private const float BattleCenterlineBlendFloor = 0.78f;
        private const float BattleBossLookBlendFloor = 0.68f;
        private const float BattleBossLookHeight = 1.8f;
        private const float BattleBossCompositionOffset = 1.85f;

        [SerializeField] private Vector3 overviewPosition = new(0f, 2.75f, -10.8f);
        [SerializeField] private Vector3 overviewEuler = new(23f, 0f, 0f);
        [SerializeField] private float overviewFieldOfView = 48f;
        [SerializeField] private float followFieldOfView = 54f;
        [SerializeField] private float followDistance = 8.4f;
        [SerializeField] private float followHeight = 4.15f;
        [SerializeField] private float followSideOffset = 0.2f;
        [SerializeField] private float lookHeight = 1.18f;
        [SerializeField] private float bossLookBlend = 0.52f;
        [SerializeField] private float bossBackBlend = 0.46f;
        [SerializeField] private float centerlineBlend = 0.78f;
        [SerializeField] private float arenaAxisBlend = 0.86f;
        [SerializeField] private float positionSharpness = 8.5f;
        [SerializeField] private float rotationSharpness = 10f;
        [SerializeField] private float fovSharpness = 7f;

        private Transform target;
        private Transform bossAnchor;
        private Camera cameraComponent;

        private void Awake()
        {
            cameraComponent = GetComponent<Camera>();
            if (cameraComponent != null)
            {
                cameraComponent.fieldOfView = overviewFieldOfView;
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
                MoveFov(overviewFieldOfView, 0.78f);
                return;
            }

            var targetPosition = target.position;
            var awayFromBoss = Vector3.back;
            var bossCompositionPoint = bossAnchor != null
                ? ResolveBossCompositionPoint(bossAnchor.position)
                : Vector3.zero;
            if (bossAnchor != null)
            {
                awayFromBoss = targetPosition - bossCompositionPoint;
                awayFromBoss.y = 0f;
                if (awayFromBoss.sqrMagnitude < 0.001f)
                {
                    awayFromBoss = Vector3.back;
                }
            }

            awayFromBoss.Normalize();
            var behindTarget = -target.forward;
            behindTarget.y = 0f;
            if (behindTarget.sqrMagnitude < 0.001f)
            {
                behindTarget = awayFromBoss;
            }

            behindTarget.Normalize();
            var backDirection = Vector3.Slerp(behindTarget, awayFromBoss, bossBackBlend).normalized;
            if (bossAnchor != null)
            {
                // The raid arena has an authored front/back axis. Biasing the camera toward that
                // axis keeps the full party, boss silhouette and arena rings readable instead of
                // swinging to the controlled member's side of the boss.
                var resolvedAxisBlend = Mathf.Max(arenaAxisBlend, BattleArenaAxisBlendFloor);
                backDirection = Vector3.Slerp(backDirection, Vector3.back, resolvedAxisBlend).normalized;
            }

            var sideDirection = Vector3.Cross(Vector3.up, backDirection).normalized;
            var resolvedDistance = bossAnchor != null ? Mathf.Max(followDistance, BattleFollowDistanceFloor) : followDistance;
            var resolvedHeight = bossAnchor != null ? Mathf.Max(followHeight, BattleFollowHeightFloor) : followHeight;
            var desiredPosition = targetPosition + backDirection * resolvedDistance + sideDirection * followSideOffset + Vector3.up * resolvedHeight;
            var centerX = bossAnchor != null ? bossAnchor.position.x : 0f;
            var resolvedCenterlineBlend = bossAnchor != null ? Mathf.Max(centerlineBlend, BattleCenterlineBlendFloor) : centerlineBlend;
            desiredPosition.x = Mathf.Lerp(desiredPosition.x, centerX, resolvedCenterlineBlend);
            desiredPosition.y = Mathf.Max(desiredPosition.y, bossAnchor != null ? bossAnchor.position.y + BattleFollowHeightFloor : 3.1f);
            var lookPoint = targetPosition + Vector3.up * lookHeight;
            if (bossAnchor != null)
            {
                lookPoint = Vector3.Lerp(
                    lookPoint,
                    bossCompositionPoint + Vector3.up * BattleBossLookHeight,
                    Mathf.Max(bossLookBlend, BattleBossLookBlendFloor));
            }

            var desiredRotation = Quaternion.LookRotation(lookPoint - desiredPosition, Vector3.up);
            MoveCamera(desiredPosition, desiredRotation, 1f);
            var baseFollowFov = bossAnchor != null ? Mathf.Max(followFieldOfView, BattleFollowFovFloor) : followFieldOfView;
            MoveFov(CalculateResponsiveBattleFieldOfView(baseFollowFov, CurrentAspect()), 1f);
        }

        public static float CalculateResponsiveBattleFieldOfView(float baselineVerticalFov, float aspect)
        {
            var safeBaseline = Mathf.Clamp(baselineVerticalFov, 1f, 120f);
            var safeAspect = Mathf.Max(0.5f, aspect);
            if (safeAspect >= ReferenceBattleAspect)
            {
                return safeBaseline;
            }

            var baselineRadians = safeBaseline * Mathf.Deg2Rad;
            var referenceHalfHorizontal = Mathf.Atan(Mathf.Tan(baselineRadians * 0.5f) * ReferenceBattleAspect);
            var responsiveVertical = 2f * Mathf.Atan(Mathf.Tan(referenceHalfHorizontal) / safeAspect) * Mathf.Rad2Deg;
            return Mathf.Clamp(responsiveVertical, safeBaseline, 72f);
        }

        private static Vector3 ResolveBossCompositionPoint(Vector3 anchorPosition)
        {
            return anchorPosition + Vector3.right * BattleBossCompositionOffset;
        }

        private float CurrentAspect()
        {
            if (cameraComponent == null)
            {
                cameraComponent = GetComponent<Camera>();
            }

            if (cameraComponent != null && cameraComponent.aspect > 0f)
            {
                return cameraComponent.aspect;
            }

            return Screen.height > 0 ? Screen.width / (float)Screen.height : ReferenceBattleAspect;
        }

        private void MoveCamera(Vector3 desiredPosition, Quaternion desiredRotation, float speedScale)
        {
            var positionLerp = 1f - Mathf.Exp(-positionSharpness * speedScale * Time.deltaTime);
            var rotationLerp = 1f - Mathf.Exp(-rotationSharpness * speedScale * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, positionLerp);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationLerp);
        }

        private void MoveFov(float desiredFieldOfView, float speedScale)
        {
            if (cameraComponent == null)
            {
                cameraComponent = GetComponent<Camera>();
            }

            if (cameraComponent == null)
            {
                return;
            }

            var fovLerp = 1f - Mathf.Exp(-fovSharpness * speedScale * Time.deltaTime);
            cameraComponent.fieldOfView = Mathf.Lerp(cameraComponent.fieldOfView, desiredFieldOfView, fovLerp);
        }
    }
}
