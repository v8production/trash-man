using UnityEngine;

namespace TrashMan
{
    public class TrashTitanLocalRoleSwitchTester : MonoBehaviour
    {
        [Header("Authority")]
        [SerializeField] private bool isHostAuthority = true;

        [Header("Test Runtime")]
        [SerializeField] private bool disableAnimatorWhileTesting = true;

        [Header("Debug")]
        [SerializeField] private bool logRoleSwitch = true;
        [SerializeField] private TrashTitanRole activeRole = TrashTitanRole.Body;

        [Header("Role Controllers")]
        [SerializeField] private TrashTitanBodyRoleController bodyController;
        [SerializeField] private TrashTitanLeftArmRoleController leftArmController;
        [SerializeField] private TrashTitanRightArmRoleController rightArmController;
        [SerializeField] private TrashTitanLeftLegRoleController leftLegController;
        [SerializeField] private TrashTitanRightLegRoleController rightLegController;

        public TrashTitanRole ActiveRole => activeRole;

        public void SetHostAuthority(bool hostAuthority)
        {
            isHostAuthority = hostAuthority;
        }

        private void Awake()
        {
            bodyController ??= GetComponent<TrashTitanBodyRoleController>();
            leftArmController ??= GetComponent<TrashTitanLeftArmRoleController>();
            rightArmController ??= GetComponent<TrashTitanRightArmRoleController>();
            leftLegController ??= GetComponent<TrashTitanLeftLegRoleController>();
            rightLegController ??= GetComponent<TrashTitanRightLegRoleController>();

            if (disableAnimatorWhileTesting)
            {
                Animator animator = GetComponent<Animator>();
                if (animator != null)
                {
                    animator.enabled = false;
                }
            }
        }

        private void LateUpdate()
        {
            if (!isHostAuthority)
            {
                return;
            }

            HandleRoleSwitchInput();

            float deltaTime = Time.deltaTime;
            switch (activeRole)
            {
                case TrashTitanRole.Body:
                    bodyController?.TickRoleInput(deltaTime);
                    break;
                case TrashTitanRole.LeftArm:
                    leftArmController?.TickRoleInput(deltaTime);
                    break;
                case TrashTitanRole.RightArm:
                    rightArmController?.TickRoleInput(deltaTime);
                    break;
                case TrashTitanRole.LeftLeg:
                    leftLegController?.TickRoleInput(deltaTime);
                    break;
                case TrashTitanRole.RightLeg:
                    rightLegController?.TickRoleInput(deltaTime);
                    break;
            }
        }

        private void HandleRoleSwitchInput()
        {
            if (TrashTitanInputUtility.WasDigitPressedThisFrame(1))
            {
                SetActiveRole(TrashTitanRole.Body);
                return;
            }

            if (TrashTitanInputUtility.WasDigitPressedThisFrame(2))
            {
                SetActiveRole(TrashTitanRole.LeftArm);
                return;
            }

            if (TrashTitanInputUtility.WasDigitPressedThisFrame(3))
            {
                SetActiveRole(TrashTitanRole.RightArm);
                return;
            }

            if (TrashTitanInputUtility.WasDigitPressedThisFrame(4))
            {
                SetActiveRole(TrashTitanRole.LeftLeg);
                return;
            }

            if (TrashTitanInputUtility.WasDigitPressedThisFrame(5))
            {
                SetActiveRole(TrashTitanRole.RightLeg);
            }
        }

        private void SetActiveRole(TrashTitanRole newRole)
        {
            if (activeRole == newRole)
            {
                return;
            }

            activeRole = newRole;
            if (logRoleSwitch)
            {
                Debug.Log($"[TrashTitanLocalRoleSwitchTester] Active role changed to {activeRole}", this);
            }
        }
    }
}
