using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AttackOnRasshiine.Runtime.Scene
{
    public sealed class MemberAvatarController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4.2f;
        [SerializeField] private float turnSpeed = 14f;

        private Transform bossAnchor;
        private Rect arenaBounds = new(-8.5f, -8.8f, 17f, 9.8f);
        private float idleTime;

        public void Configure(Transform newBossAnchor, Rect newArenaBounds)
        {
            bossAnchor = newBossAnchor;
            arenaBounds = newArenaBounds;
        }

        private void Update()
        {
            idleTime += Time.deltaTime;
            if (IsTextInputActive())
            {
                FaceBoss();
                ApplyBob();
                return;
            }

            var input = ReadMoveInput();
            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            if (input.sqrMagnitude > 0.001f)
            {
                Move(input);
            }
            else
            {
                FaceBoss();
                ApplyBob();
            }
        }

        private void Move(Vector2 input)
        {
            var cameraTransform = Camera.main != null ? Camera.main.transform : null;
            var forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            var right = cameraTransform != null ? cameraTransform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            var move = forward * input.y + right * input.x;
            if (move.sqrMagnitude < 0.001f)
            {
                return;
            }

            move.Normalize();
            var position = transform.localPosition + move * (moveSpeed * Time.deltaTime);
            position.x = Mathf.Clamp(position.x, arenaBounds.xMin, arenaBounds.xMax);
            position.z = Mathf.Clamp(position.z, arenaBounds.yMin, arenaBounds.yMax);
            position.y = Mathf.Sin(idleTime * 4f) * 0.025f;
            transform.localPosition = position;

            var targetRotation = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
        }

        private void FaceBoss()
        {
            if (bossAnchor == null)
            {
                return;
            }

            var toBoss = bossAnchor.position - transform.position;
            toBoss.y = 0f;
            if (toBoss.sqrMagnitude < 0.001f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(toBoss.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-turnSpeed * 0.45f * Time.deltaTime));
        }

        private void ApplyBob()
        {
            var position = transform.localPosition;
            position.y = Mathf.Sin(idleTime * 1.8f) * 0.035f;
            transform.localPosition = position;
        }

        private static Vector2 ReadMoveInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return Vector2.zero;
            }

            var x = 0f;
            var y = 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                x -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                x += 1f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                y -= 1f;
            }

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                y += 1f;
            }

            return new Vector2(x, y);
        }

        private static bool IsTextInputActive()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null && selected.GetComponentInParent<InputField>() != null;
        }
    }
}
