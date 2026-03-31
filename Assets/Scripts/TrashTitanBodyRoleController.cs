using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TrashMan
{
    public class TrashTitanBodyRoleController : MonoBehaviour, ITrashTitanRoleController
    {
        [SerializeField] private TrashTitanRig rig;
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float strafeSpeed = 1.75f;
        [SerializeField] private float turnSpeed = 75f;
        [SerializeField] private float bodySmoothing = 8f;

        private Vector3 bodyVelocity;

        public TrashTitanRole Role => TrashTitanRole.Body;

        private void Awake()
        {
            rig ??= GetComponent<TrashTitanRig>();
        }

        public void TickRoleInput(float deltaTime)
        {
            if (rig == null || !rig.EnsureReady())
            {
                return;
            }

            float bodyForward = TrashTitanInputUtility.GetAxis(
                KeyCode.UpArrow,
                KeyCode.DownArrow,
                Key.UpArrow,
                Key.DownArrow);

            float bodyStrafe = TrashTitanInputUtility.GetAxis(
                KeyCode.RightArrow,
                KeyCode.LeftArrow,
                Key.RightArrow,
                Key.LeftArrow);

            float bodyTurn = TrashTitanInputUtility.GetAxis(
                KeyCode.Period,
                KeyCode.Comma,
                Key.Period,
                Key.Comma);

            Transform movementRoot = rig.MovementRoot;

            Vector3 desiredVelocity =
                (movementRoot.forward * (bodyForward * moveSpeed)) +
                (movementRoot.right * (bodyStrafe * strafeSpeed));

            bodyVelocity = Vector3.Lerp(bodyVelocity, desiredVelocity, 1f - Mathf.Exp(-bodySmoothing * deltaTime));
            movementRoot.position += bodyVelocity * deltaTime;
            movementRoot.Rotate(Vector3.up, bodyTurn * turnSpeed * deltaTime, Space.World);
        }
    }
}
