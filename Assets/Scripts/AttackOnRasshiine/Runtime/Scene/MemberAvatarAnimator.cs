using System.Collections;
using UnityEngine;

namespace AttackOnRasshiine.Runtime.Scene
{
    public enum MemberBattleAnimation
    {
        Idle,
        Move,
        Attack,
        StrongAttack,
        FullPowerAttack,
        Support,
        Guard,
        Victory,
        Hit
    }

    public sealed class MemberAvatarAnimator : MonoBehaviour
    {
        private const int BaseLayer = 0;
        private const float DefaultFade = 0.08f;

        private static readonly string[] MoveStates =
        {
            "MoveFWD_Battle_InPlace_SwordAndShield",
            "MoveFWD_Normal_InPlace_SwordAndShield",
            "SprintFWD_Battle_InPlace_SwordAndShield"
        };

        private static readonly string[] AttackStates =
        {
            "Attack01_SwordAndShiled",
            "Combo01_InPlace_SwordAndShield",
            "Attack02_SwordAndShiled"
        };

        private static readonly string[] StrongAttackStates =
        {
            "Attack03_SwordAndShiled",
            "Combo03_InPlace_SwordAndShield",
            "Attack04_SwordAndShiled"
        };

        private static readonly string[] FullPowerAttackStates =
        {
            "Combo04_InPlace_SwordAndShield",
            "Attack04_SwordAndShiled",
            "Attack03_SwordAndShiled"
        };

        private static readonly string[] SupportStates =
        {
            "LevelUp_Battle_SwordAndShield",
            "SenseSomething_Start_SwordAndShield",
            "Challenging_Battle_SwordAndShield"
        };

        private static readonly string[] GuardStates =
        {
            "Defend_SwordAndShield",
            "DefendHit_SwordAndShield",
            "Challenging_Battle_SwordAndShield"
        };

        private static readonly string[] VictoryStates =
        {
            "Victory_Battle_SwordAndShield",
            "Dance_SwordAndShield",
            "LevelUp_Battle_SwordAndShield"
        };

        private static readonly string[] HitStates =
        {
            "GetHit01_SwordAndShield",
            "GetHit02_SwordAndShield",
            "Dizzy_SwordAndShield"
        };

        private readonly struct TransformPose
        {
            public TransformPose(Transform target)
            {
                Transform = target;
                LocalPosition = target.localPosition;
                LocalRotation = target.localRotation;
                LocalScale = target.localScale;
            }

            public readonly Transform Transform;
            public readonly Vector3 LocalPosition;
            public readonly Quaternion LocalRotation;
            public readonly Vector3 LocalScale;
        }

        private Animator animator;
        private TransformPose[] restPose = System.Array.Empty<TransformPose>();
        private string currentState;
        private Coroutine returnToIdleCoroutine;
        private int animationSeed;
        private int battleActionPlayCount;
        private bool isInBattleAction;

        public bool HasPlayableAnimator => animator != null && animator.runtimeAnimatorController != null;
        public bool IsInBattleAction => isInBattleAction;
        public int AnimationSeed => animationSeed;
        public string CurrentStateName => currentState ?? string.Empty;

        private void Awake()
        {
            ResolveAnimator();
            ForceIdlePose();
        }

        public void ResolveAnimator()
        {
            animator = GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                return;
            }

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            CaptureRestPose();
            if (!isInBattleAction)
            {
                animator.enabled = false;
            }
        }

        public void ConfigureVariation(int seed)
        {
            animationSeed = seed == int.MinValue ? 0 : Mathf.Abs(seed);
            battleActionPlayCount = animationSeed % 7;
            currentState = null;

            ForceIdlePose();
        }

        public void PlayIdle(float fade = DefaultFade)
        {
            if (isInBattleAction)
            {
                return;
            }

            ForceIdlePose();
        }

        public void PlayMove()
        {
            if (isInBattleAction)
            {
                return;
            }

            PlayFirstAvailable(MoveStates, DefaultFade, true, ResolveLoopVariantOffset(MoveStates), 0f);
        }

        public void PlayBattleAnimation(MemberBattleAnimation animation, float returnDelay = 0.72f)
        {
            if (returnToIdleCoroutine != null)
            {
                StopCoroutine(returnToIdleCoroutine);
                returnToIdleCoroutine = null;
            }

            var candidates = animation switch
            {
                MemberBattleAnimation.Move => MoveStates,
                MemberBattleAnimation.Attack => AttackStates,
                MemberBattleAnimation.StrongAttack => StrongAttackStates,
                MemberBattleAnimation.FullPowerAttack => FullPowerAttackStates,
                MemberBattleAnimation.Support => SupportStates,
                MemberBattleAnimation.Guard => GuardStates,
                MemberBattleAnimation.Victory => VictoryStates,
                MemberBattleAnimation.Hit => HitStates,
                _ => null
            };

            isInBattleAction = true;
            battleActionPlayCount++;
            if (!PlayFirstAvailable(candidates, DefaultFade, false, ResolveActionVariantOffset(animation, candidates), 0f))
            {
                isInBattleAction = false;
                return;
            }

            if (Application.isPlaying && isActiveAndEnabled)
            {
                returnToIdleCoroutine = StartCoroutine(ReturnToIdleAfter(returnDelay));
            }
        }

        public void CancelBattleAnimation()
        {
            if (returnToIdleCoroutine != null)
            {
                StopCoroutine(returnToIdleCoroutine);
                returnToIdleCoroutine = null;
            }

            isInBattleAction = false;
            ForceIdlePose();
        }

        private IEnumerator ReturnToIdleAfter(float delay)
        {
            yield return new WaitForSeconds(Mathf.Max(0.05f, delay));
            isInBattleAction = false;
            ForceIdlePose();
            returnToIdleCoroutine = null;
        }

        private void ForceIdlePose()
        {
            currentState = null;
            if (animator == null)
            {
                ResolveAnimator();
            }

            if (animator != null)
            {
                animator.enabled = false;
            }

            RestoreRestPose();
        }

        private void CaptureRestPose()
        {
            if (animator == null)
            {
                restPose = System.Array.Empty<TransformPose>();
                return;
            }

            var root = animator.transform;
            var transforms = root.GetComponentsInChildren<Transform>(true);
            var poses = new TransformPose[Mathf.Max(0, transforms.Length - 1)];
            var poseIndex = 0;
            for (var index = 0; index < transforms.Length; index++)
            {
                var target = transforms[index];
                if (target == root)
                {
                    continue;
                }

                poses[poseIndex++] = new TransformPose(target);
            }

            if (poseIndex != poses.Length)
            {
                System.Array.Resize(ref poses, poseIndex);
            }

            restPose = poses;
        }

        private void RestoreRestPose()
        {
            if (restPose == null)
            {
                return;
            }

            for (var index = 0; index < restPose.Length; index++)
            {
                var pose = restPose[index];
                if (pose.Transform == null)
                {
                    continue;
                }

                pose.Transform.localPosition = pose.LocalPosition;
                pose.Transform.localRotation = pose.LocalRotation;
                pose.Transform.localScale = pose.LocalScale;
            }
        }

        private bool PlayFirstAvailable(string[] stateNames, float fade, bool loopState, int variantOffset = 0, float normalizedTimeOffset = 0f)
        {
            if (animator == null)
            {
                ResolveAnimator();
            }

            if (!HasPlayableAnimator || stateNames == null)
            {
                return false;
            }

            for (var index = 0; index < stateNames.Length; index++)
            {
                var stateName = stateNames[Mathf.Abs(variantOffset + index) % stateNames.Length];
                if (string.IsNullOrEmpty(stateName))
                {
                    continue;
                }

                if (!TryResolveStatePath(stateName, out var resolvedStatePath, out var resolvedStateHash))
                {
                    continue;
                }

                if (loopState && currentState == resolvedStatePath && animator.enabled)
                {
                    return true;
                }

                animator.enabled = true;
                if (loopState)
                {
                    if (fade <= 0f)
                    {
                        animator.Play(resolvedStateHash, BaseLayer, Mathf.Repeat(normalizedTimeOffset, 1f));
                    }
                    else
                    {
                        animator.CrossFade(resolvedStateHash, fade, BaseLayer, Mathf.Repeat(normalizedTimeOffset, 1f));
                    }
                }
                else
                {
                    animator.CrossFadeInFixedTime(resolvedStateHash, fade, BaseLayer, 0f);
                }

                currentState = resolvedStatePath;
                return true;
            }

            return false;
        }

        private bool TryResolveStatePath(string stateName, out string resolvedStatePath, out int resolvedStateHash)
        {
            resolvedStatePath = stateName;
            resolvedStateHash = Animator.StringToHash(stateName);
            if (animator.HasState(BaseLayer, resolvedStateHash))
            {
                return true;
            }

            resolvedStatePath = $"Base Layer.{stateName}";
            resolvedStateHash = Animator.StringToHash(resolvedStatePath);
            return animator.HasState(BaseLayer, resolvedStateHash);
        }

        private int ResolveLoopVariantOffset(string[] stateNames)
        {
            return stateNames == null || stateNames.Length == 0 ? 0 : animationSeed % stateNames.Length;
        }

        private int ResolveActionVariantOffset(MemberBattleAnimation animation, string[] stateNames)
        {
            return CalculateVariantStartIndex(animationSeed, animation, battleActionPlayCount, stateNames?.Length ?? 0);
        }

        public static int CalculateVariantStartIndex(int seed, MemberBattleAnimation animation, int playCount, int candidateCount)
        {
            if (candidateCount <= 0)
            {
                return 0;
            }

            var safeSeed = seed == int.MinValue ? 0 : Mathf.Abs(seed);
            return Mathf.Abs(safeSeed + (int)animation * 5 + playCount * 3) % candidateCount;
        }
    }
}
