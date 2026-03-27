using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TrashMan
{
    public class TrashTitanRightLegInputController : MonoBehaviour
    {
        [Header("Hip Spherical Mapping")]
        [SerializeField] private float maxThetaDegrees = 55f;
        [SerializeField] private float thetaRadiusPixels = 260f;
        [SerializeField] private bool useScreenCenterAsOrigin = true;
        [SerializeField] private Vector2 mouseOriginPixels = new Vector2(960f, 540f);

        [Header("Input Speed")]
        [SerializeField] private float kneeSpeed = 90f;

        [Header("Angle Limits")]
        [SerializeField] private Vector2 hipYawLimit = new Vector2(-20f, 60f);
        [SerializeField] private Vector2 hipRollLimit = new Vector2(-120f, 60f);
        [SerializeField] private Vector2 kneeRollLimit = new Vector2(0f, 130f);

        [Header("Optional Bone Overrides")]
        [SerializeField] private Transform rightHip;
        [SerializeField] private Transform rightKnee;

        private Animator animator;
        private Quaternion hipBaseRotation;
        private Quaternion kneeBaseRotation;

        private float hipYaw;
        private float hipRoll;
        private float kneeRoll;

        private void Awake()
        {
            ResolveBones();
            CacheBaseRotations();
        }

        private void LateUpdate()
        {
            if (rightHip == null || rightKnee == null)
            {
                ResolveBones();
                CacheBaseRotations();
            }

            if (rightHip == null || rightKnee == null)
            {
                return;
            }

            Vector2 mousePosition = ReadMousePosition();
            Vector2 origin = useScreenCenterAsOrigin
                ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
                : mouseOriginPixels;

            Vector2 fromOrigin = mousePosition - origin;
            float radius = fromOrigin.magnitude;
            float theta01 = thetaRadiusPixels > 0f ? Mathf.Clamp01(radius / thetaRadiusPixels) : 0f;
            float theta = theta01 * maxThetaDegrees;
            float phiRad = Mathf.Atan2(fromOrigin.y, fromOrigin.x);
            float normalizedY = thetaRadiusPixels > 0f ? Mathf.Clamp(fromOrigin.y / thetaRadiusPixels, -1f, 1f) : 0f;

            hipYaw = theta * Mathf.Cos(phiRad);
            hipRoll = -maxThetaDegrees * normalizedY;

            float kneeInput = 0f;

            if (IsWPressed())
            {
                kneeInput += 1f;
            }

            if (IsSPressed())
            {
                kneeInput -= 1f;
            }

            kneeRoll += kneeInput * kneeSpeed * Time.deltaTime;

            hipYaw = Mathf.Clamp(hipYaw, hipYawLimit.x, hipYawLimit.y);
            hipRoll = Mathf.Clamp(hipRoll, hipRollLimit.x, hipRollLimit.y);
            kneeRoll = Mathf.Clamp(kneeRoll, kneeRollLimit.x, kneeRollLimit.y);

            rightHip.localRotation = hipBaseRotation * Quaternion.Euler(0f, hipYaw, hipRoll);
            rightKnee.localRotation = kneeBaseRotation * Quaternion.Euler(0f, 0f, kneeRoll);
        }

        private static Vector2 ReadMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }

            return Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.mousePosition.x, Input.mousePosition.y);
#else
            return Vector2.zero;
#endif
        }

        private static bool IsWPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.wKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.W);
#else
            return false;
#endif
        }

        private static bool IsSPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.sKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.S);
#else
            return false;
#endif
        }

        private void ResolveBones()
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            Transform searchRoot = animator != null ? animator.transform : transform;

            if (animator != null && animator.isHuman)
            {
                if (rightHip == null)
                {
                    rightHip = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                }

                if (rightKnee == null)
                {
                    rightKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                }
            }

            if (rightHip == null)
            {
                rightHip = FindChildByNames(
                    searchRoot,
                    "Character1_RightUpLeg",
                    "RightUpLeg",
                    "mixamorig:RightUpLeg",
                    "mixamorigRightUpLeg",
                    "J_Bip_R_UpperLeg",
                    "RightUpperLeg",
                    "UpperLeg_R",
                    "R_Thigh",
                    "Bip001 R Thigh"
                );
            }

            if (rightKnee == null)
            {
                rightKnee = FindChildByNames(
                    searchRoot,
                    "Character1_RightLeg",
                    "RightLeg",
                    "mixamorig:RightLeg",
                    "mixamorigRightLeg",
                    "J_Bip_R_LowerLeg",
                    "RightLowerLeg",
                    "LowerLeg_R",
                    "R_Calf",
                    "Bip001 R Calf"
                );
            }

            if (rightHip == null)
            {
                rightHip = FindRightLegByKeywords(searchRoot, true);
            }

            if (rightKnee == null)
            {
                Transform kneeSearchRoot = rightHip != null ? rightHip : searchRoot;
                rightKnee = FindRightLegByKeywords(kneeSearchRoot, false);
            }
        }

        private void CacheBaseRotations()
        {
            if (rightHip != null)
            {
                hipBaseRotation = rightHip.localRotation;
            }

            if (rightKnee != null)
            {
                kneeBaseRotation = rightKnee.localRotation;
            }
        }

        private static Transform FindChildByNames(Transform root, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Transform found = FindDeepChild(root, names[i]);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Transform FindDeepChild(Transform parent, string targetName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == targetName)
                {
                    return child;
                }

                Transform result = FindDeepChild(child, targetName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static Transform FindRightLegByKeywords(Transform root, bool upper)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                string lower = t.name.ToLowerInvariant();

                bool isRight =
                    lower.Contains("right") ||
                    lower.Contains("_r") ||
                    lower.Contains("r_") ||
                    lower.Contains(".r") ||
                    lower.Contains(" r ");

                if (!isRight)
                {
                    continue;
                }

                bool isUpper =
                    lower.Contains("upleg") ||
                    lower.Contains("upperleg") ||
                    lower.Contains("thigh");

                bool isLower =
                    lower.Contains("lowerleg") ||
                    lower.Contains("calf") ||
                    lower.Contains("shin") ||
                    (lower.Contains("leg") && !isUpper);

                if (upper && isUpper)
                {
                    return t;
                }

                if (!upper && isLower)
                {
                    return t;
                }
            }

            return null;
        }
    }

    public static class TrashTitanRightLegInputBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachToTrashTitanIfFound()
        {
            if (Object.FindFirstObjectByType<TrashTitanRightLegInputController>() != null)
            {
                return;
            }

            GameObject target = GameObject.Find("Trash_titan");

            if (target == null)
            {
                Animator[] animators = Object.FindObjectsByType<Animator>(FindObjectsSortMode.None);
                for (int i = 0; i < animators.Length; i++)
                {
                    Animator a = animators[i];
                    if (a == null)
                    {
                        continue;
                    }

                    string lowerName = a.gameObject.name.ToLowerInvariant();
                    if (!lowerName.Contains("trash") && !lowerName.Contains("titan"))
                    {
                        continue;
                    }

                    Transform hip = a.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                    Transform knee = a.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                    if (hip != null && knee != null)
                    {
                        target = a.gameObject;
                        break;
                    }
                }
            }

            if (target != null && target.GetComponent<TrashTitanRightLegInputController>() == null)
            {
                target.AddComponent<TrashTitanRightLegInputController>();
            }
        }
    }
}
