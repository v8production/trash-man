using UnityEngine;

public sealed class TitanRigManager
{
    private static readonly TitanRigManager instance = new TitanRigManager();

    private TitanRigRuntime runtime;

    public static TitanRigManager Instance => instance;

    public TitanRigRuntime Runtime => runtime;
    public bool IsBound => runtime != null;
    public GameObject RuntimeGameObject => runtime != null ? runtime.gameObject : null;
    public Transform MovementRoot => runtime != null ? runtime.MovementRoot : null;
    public Transform Spine => runtime != null ? runtime.Spine : null;
    public float WaistYaw => runtime != null ? runtime.WaistYaw : 0f;

    private TitanRigManager()
    {
    }

    public bool EnsureBoundFromScene()
    {
        if (runtime != null)
        {
            return true;
        }

        runtime = Object.FindAnyObjectByType<TitanRigRuntime>();
        return runtime != null;
    }

    public void Init()
    {
        if (EnsureBoundFromScene())
        {
            runtime.Init();
        }
    }

    public bool TryGetRuntime(out TitanRigRuntime value)
    {
        if (runtime == null)
        {
            EnsureBoundFromScene();
        }

        value = runtime;
        return value != null;
    }

    public bool EnsureBoundTo(GameObject target)
    {
        if (target == null)
        {
            return EnsureBoundFromScene();
        }

        if (runtime != null && runtime.gameObject == target)
        {
            return true;
        }

        if (runtime != null && runtime.gameObject != target)
        {
            Debug.LogWarning($"[TitanRigManager] Rebinding runtime from '{runtime.gameObject.name}' to '{target.name}'.");
        }

        TitanRigRuntime found = target.GetComponent<TitanRigRuntime>();
        if (found == null)
        {
            found = target.AddComponent<TitanRigRuntime>();
        }

        runtime = found;
        return runtime != null;
    }

    public void Bind(TitanRigRuntime value)
    {
        runtime = value;
        runtime?.Init();
    }

    public void UnbindIfOwner(TitanRigRuntime owner)
    {
        if (owner != null && runtime == owner)
        {
            runtime = null;
        }
    }

    public void Clear()
    {
        if (runtime != null)
        {
            runtime.Clear();
        }

        runtime = null;
    }

    public bool EnsureReady()
    {
        return (runtime != null || EnsureBoundFromScene()) && runtime.EnsureReady();
    }

    public void SetWaistYaw(float value)
    {
        if (!EnsureBoundFromScene())
        {
            return;
        }

        runtime.SetWaistYaw(value);
    }

    public TitanArmControlState GetArmState(bool left)
    {
        if (!EnsureBoundFromScene())
        {
            return default;
        }

        return runtime.GetArmState(left);
    }

    public void SetArmState(bool left, TitanArmControlState state)
    {
        if (!EnsureBoundFromScene())
        {
            return;
        }

        runtime.SetArmState(left, state);
    }

    public TitanLegControlState GetLegState(bool left)
    {
        if (!EnsureBoundFromScene())
        {
            return default;
        }

        return runtime.GetLegState(left);
    }

    public void SetLegState(bool left, TitanLegControlState state)
    {
        if (!EnsureBoundFromScene())
        {
            return;
        }

        runtime.SetLegState(left, state);
    }

    public void ApplyBodyPose()
    {
        if (!EnsureBoundFromScene())
        {
            return;
        }

        runtime.ApplyBodyPose();
    }

    public void ApplyArmPose(bool left)
    {
        if (!EnsureBoundFromScene())
        {
            return;
        }

        runtime.ApplyArmPose(left);
    }

    public void ApplyLegPose(bool left)
    {
        if (!EnsureBoundFromScene())
        {
            return;
        }

        runtime.ApplyLegPose(left);
    }

    public bool TryGetPoseSnapshot(out TitanRigPoseSnapshot snapshot)
    {
        snapshot = default;
        return EnsureBoundFromScene() && runtime.TryGetPoseSnapshot(out snapshot);
    }

    public void ApplyPoseSnapshot(in TitanRigPoseSnapshot snapshot)
    {
        if (!EnsureBoundFromScene())
        {
            return;
        }

        runtime.ApplyPoseSnapshot(snapshot);
    }
}

[System.Serializable]
public struct TitanRigPoseSnapshot
{
    public Vector3 RootPosition;
    public Quaternion RootRotation;

    public bool HasLeftShoulder;
    public Quaternion LeftShoulderRotation;
    public bool HasLeftElbow;
    public Quaternion LeftElbowRotation;

    public bool HasRightShoulder;
    public Quaternion RightShoulderRotation;
    public bool HasRightElbow;
    public Quaternion RightElbowRotation;

    public bool HasLeftHip;
    public Quaternion LeftHipRotation;
    public bool HasLeftKnee;
    public Quaternion LeftKneeRotation;

    public bool HasRightHip;
    public Quaternion RightHipRotation;
    public bool HasRightKnee;
    public Quaternion RightKneeRotation;

    public bool HasSpine;
    public Quaternion SpineRotation;
}

[System.Serializable]
public struct TitanArmControlState
{
    public float ShoulderYaw;
    public float ShoulderPitch;
    public float ElbowPitch;
}

[System.Serializable]
public struct TitanLegControlState
{
    public float HipYaw;
    public float HipRoll;
    public float KneeRoll;
}

public sealed class TitanRigRuntime : MonoBehaviour
{
    [Header("Optional Bone Overrides")]
    [SerializeField] private Transform mechaRoot;
    [SerializeField] private Transform leftShoulder;
    [SerializeField] private Transform leftElbow;
    [SerializeField] private Transform rightShoulder;
    [SerializeField] private Transform rightElbow;
    [SerializeField] private Transform leftHip;
    [SerializeField] private Transform leftKnee;
    [SerializeField] private Transform rightHip;
    [SerializeField] private Transform rightKnee;
    [SerializeField] private Transform spine;

    [Header("Body")]
    [SerializeField] private float waistYaw;

    [Header("Arm States")]
    [SerializeField] private TitanArmControlState leftArm;
    [SerializeField] private TitanArmControlState rightArm;

    [Header("Leg States")]
    [SerializeField] private TitanLegControlState leftLeg;
    [SerializeField] private TitanLegControlState rightLeg;

    private Animator animator;

    private Quaternion leftShoulderBaseRotation;
    private Quaternion leftElbowBaseRotation;
    private Quaternion rightShoulderBaseRotation;
    private Quaternion rightElbowBaseRotation;
    private Quaternion leftHipBaseRotation;
    private Quaternion leftKneeBaseRotation;
    private Quaternion rightHipBaseRotation;
    private Quaternion rightKneeBaseRotation;
    private Quaternion spineBaseRotation;

    private bool warnedMissingBones;
    private bool loggedResolvedBones;
    private bool basePoseInitialized;

    public Transform MovementRoot => mechaRoot != null ? mechaRoot : transform;
    public Transform Spine => spine;
    public float WaistYaw => waistYaw;

    public void Init()
    {
        ResolveAndCacheIfNeeded(forceCache: true);
    }

    public void Clear()
    {
        warnedMissingBones = false;
        loggedResolvedBones = false;
        basePoseInitialized = false;
    }

    public bool EnsureReady()
    {
        if (!basePoseInitialized || !HasAnyDrivenBone())
        {
            ResolveAndCacheIfNeeded(forceCache: false);
        }

        bool hasAnyDrivenBone = HasAnyDrivenBone();

        if (!hasAnyDrivenBone && !warnedMissingBones)
        {
            warnedMissingBones = true;
            Debug.LogWarning("[TitanRigManager] Could not resolve any controllable bones. Assign bone overrides on Titan.", this);
        }

        if (hasAnyDrivenBone && !loggedResolvedBones)
        {
            loggedResolvedBones = true;
            Debug.Log($"[TitanRigManager] Resolved bones - LS:{NameOrNone(leftShoulder)} LE:{NameOrNone(leftElbow)} RS:{NameOrNone(rightShoulder)} RE:{NameOrNone(rightElbow)} LH:{NameOrNone(leftHip)} LK:{NameOrNone(leftKnee)} RH:{NameOrNone(rightHip)} RK:{NameOrNone(rightKnee)} SP:{NameOrNone(spine)}", this);
        }

        return hasAnyDrivenBone;
    }

    public void SetWaistYaw(float value)
    {
        waistYaw = value;
    }

    public TitanArmControlState GetArmState(bool left)
    {
        return left ? leftArm : rightArm;
    }

    public void SetArmState(bool left, TitanArmControlState state)
    {
        if (left)
        {
            leftArm = state;
            return;
        }

        rightArm = state;
    }

    public TitanLegControlState GetLegState(bool left)
    {
        return left ? leftLeg : rightLeg;
    }

    public void SetLegState(bool left, TitanLegControlState state)
    {
        if (left)
        {
            leftLeg = state;
            return;
        }

        rightLeg = state;
    }

    public void ApplyBodyPose()
    {
        if (!EnsureReady())
        {
            return;
        }

        ApplySpine(0f, waistYaw, 0f);
    }

    public void ApplyArmPose(bool left)
    {
        if (!EnsureReady())
        {
            return;
        }

        TitanArmControlState state = left ? leftArm : rightArm;
        if (left)
        {
            ApplyLeftArm(state.ShoulderPitch, state.ShoulderYaw, state.ElbowPitch);
            return;
        }

        ApplyRightArm(state.ShoulderPitch, state.ShoulderYaw, state.ElbowPitch);
    }

    public void ApplyLegPose(bool left)
    {
        if (!EnsureReady())
        {
            return;
        }

        TitanLegControlState state = left ? leftLeg : rightLeg;
        if (left)
        {
            ApplyLeftLeg(state.HipYaw, state.HipRoll, state.KneeRoll);
            return;
        }

        ApplyRightLeg(state.HipYaw, state.HipRoll, state.KneeRoll);
    }

    public void ApplyLeftArm(float shoulderPitch, float shoulderYaw, float elbowPitch)
    {
        if (leftShoulder != null)
        {
            leftShoulder.localRotation = leftShoulderBaseRotation * Quaternion.Euler(shoulderPitch, 0f, shoulderYaw);
        }

        if (leftElbow != null)
        {
            leftElbow.localRotation = leftElbowBaseRotation * Quaternion.Euler(0f, elbowPitch, 0f);
        }
    }

    public void ApplyRightArm(float shoulderPitch, float shoulderYaw, float elbowPitch)
    {
        if (rightShoulder != null)
        {
            rightShoulder.localRotation = rightShoulderBaseRotation * Quaternion.Euler(shoulderPitch, 0f, shoulderYaw);
        }

        if (rightElbow != null)
        {
            rightElbow.localRotation = rightElbowBaseRotation * Quaternion.Euler(0f, elbowPitch, 0f);
        }
    }

    public void ApplyLeftLeg(float hipYaw, float hipRoll, float kneeRoll)
    {
        if (leftHip != null)
        {
            leftHip.localRotation = leftHipBaseRotation * Quaternion.Euler(0f, hipYaw, hipRoll);
        }

        if (leftKnee != null)
        {
            leftKnee.localRotation = leftKneeBaseRotation * Quaternion.Euler(0f, 0f, kneeRoll);
        }
    }

    public void ApplyRightLeg(float hipYaw, float hipRoll, float kneeRoll)
    {
        if (rightHip != null)
        {
            rightHip.localRotation = rightHipBaseRotation * Quaternion.Euler(0f, hipYaw, hipRoll);
        }

        if (rightKnee != null)
        {
            rightKnee.localRotation = rightKneeBaseRotation * Quaternion.Euler(0f, 0f, kneeRoll);
        }
    }

    public void ApplySpine(float yaw, float pitch = 0f, float roll = 0f)
    {
        if (spine != null)
        {
            spine.localRotation = spineBaseRotation * Quaternion.Euler(pitch, yaw, roll);
        }
    }

    public bool TryGetPoseSnapshot(out TitanRigPoseSnapshot snapshot)
    {
        snapshot = default;
        if (!EnsureReady())
        {
            return false;
        }

        Transform movementRoot = MovementRoot;
        snapshot.RootPosition = movementRoot.position;
        snapshot.RootRotation = movementRoot.rotation;

        snapshot.HasLeftShoulder = leftShoulder != null;
        if (snapshot.HasLeftShoulder)
        {
            snapshot.LeftShoulderRotation = leftShoulder.localRotation;
        }

        snapshot.HasLeftElbow = leftElbow != null;
        if (snapshot.HasLeftElbow)
        {
            snapshot.LeftElbowRotation = leftElbow.localRotation;
        }

        snapshot.HasRightShoulder = rightShoulder != null;
        if (snapshot.HasRightShoulder)
        {
            snapshot.RightShoulderRotation = rightShoulder.localRotation;
        }

        snapshot.HasRightElbow = rightElbow != null;
        if (snapshot.HasRightElbow)
        {
            snapshot.RightElbowRotation = rightElbow.localRotation;
        }

        snapshot.HasLeftHip = leftHip != null;
        if (snapshot.HasLeftHip)
        {
            snapshot.LeftHipRotation = leftHip.localRotation;
        }

        snapshot.HasLeftKnee = leftKnee != null;
        if (snapshot.HasLeftKnee)
        {
            snapshot.LeftKneeRotation = leftKnee.localRotation;
        }

        snapshot.HasRightHip = rightHip != null;
        if (snapshot.HasRightHip)
        {
            snapshot.RightHipRotation = rightHip.localRotation;
        }

        snapshot.HasRightKnee = rightKnee != null;
        if (snapshot.HasRightKnee)
        {
            snapshot.RightKneeRotation = rightKnee.localRotation;
        }

        snapshot.HasSpine = spine != null;
        if (snapshot.HasSpine)
        {
            snapshot.SpineRotation = spine.localRotation;
        }

        return true;
    }

    public void ApplyPoseSnapshot(in TitanRigPoseSnapshot snapshot)
    {
        if (!EnsureReady())
        {
            return;
        }

        Transform movementRoot = MovementRoot;
        movementRoot.position = snapshot.RootPosition;
        movementRoot.rotation = snapshot.RootRotation;

        if (snapshot.HasLeftShoulder && leftShoulder != null)
        {
            leftShoulder.localRotation = snapshot.LeftShoulderRotation;
        }

        if (snapshot.HasLeftElbow && leftElbow != null)
        {
            leftElbow.localRotation = snapshot.LeftElbowRotation;
        }

        if (snapshot.HasRightShoulder && rightShoulder != null)
        {
            rightShoulder.localRotation = snapshot.RightShoulderRotation;
        }

        if (snapshot.HasRightElbow && rightElbow != null)
        {
            rightElbow.localRotation = snapshot.RightElbowRotation;
        }

        if (snapshot.HasLeftHip && leftHip != null)
        {
            leftHip.localRotation = snapshot.LeftHipRotation;
        }

        if (snapshot.HasLeftKnee && leftKnee != null)
        {
            leftKnee.localRotation = snapshot.LeftKneeRotation;
        }

        if (snapshot.HasRightHip && rightHip != null)
        {
            rightHip.localRotation = snapshot.RightHipRotation;
        }

        if (snapshot.HasRightKnee && rightKnee != null)
        {
            rightKnee.localRotation = snapshot.RightKneeRotation;
        }

        if (snapshot.HasSpine && spine != null)
        {
            spine.localRotation = snapshot.SpineRotation;
        }
    }

    private void ResolveAndCacheIfNeeded(bool forceCache)
    {
        int before = ComputeBoneSignature();
        ResolveBones();
        int after = ComputeBoneSignature();

        if (forceCache || !basePoseInitialized || before != after)
        {
            CacheBaseRotations();
            basePoseInitialized = true;
        }
    }

    private void ResolveBones()
    {
        mechaRoot ??= transform;

        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        Transform searchRoot = animator != null ? animator.transform : transform;

        if (animator != null && animator.isHuman)
        {
            leftShoulder ??= animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftElbow ??= animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            rightShoulder ??= animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightElbow ??= animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            leftHip ??= animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            leftKnee ??= animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            rightHip ??= animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            rightKnee ??= animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            spine ??= animator.GetBoneTransform(HumanBodyBones.Chest);
            spine ??= animator.GetBoneTransform(HumanBodyBones.UpperChest);
            spine ??= animator.GetBoneTransform(HumanBodyBones.Spine);
        }

        leftShoulder ??= FindChildByNames(searchRoot,
            "Character1_LeftShoulder", "Character1_LeftArm", "LeftShoulder", "LeftArm",
            "mixamorig:LeftShoulder", "mixamorig:LeftArm", "mixamorigLeftShoulder", "mixamorigLeftArm",
            "J_Bip_L_UpperArm", "LeftUpperArm", "UpperArm_L", "L_UpperArm", "Bip001 L Clavicle", "Bip001 L UpperArm");

        leftElbow ??= FindChildByNames(searchRoot,
            "Character1_LeftForeArm", "Character1_LeftLowerArm", "LeftForeArm", "LeftLowerArm", "L_Arm_Lower",
            "mixamorig:LeftForeArm", "mixamorigLeftForeArm", "J_Bip_L_LowerArm", "LeftLowerArm",
            "LowerArm_L", "L_ForeArm", "Bip001 L Forearm");

        rightShoulder ??= FindChildByNames(searchRoot,
            "Character1_RightShoulder", "Character1_RightArm", "RightShoulder", "RightArm",
            "mixamorig:RightShoulder", "mixamorig:RightArm", "mixamorigRightShoulder", "mixamorigRightArm",
            "J_Bip_R_UpperArm", "RightUpperArm", "UpperArm_R", "R_UpperArm", "Bip001 R Clavicle", "Bip001 R UpperArm");

        rightElbow ??= FindChildByNames(searchRoot,
            "Character1_RightForeArm", "Character1_RightLowerArm", "RightForeArm", "RightLowerArm", "R_Arm_Lower",
            "mixamorig:RightForeArm", "mixamorigRightForeArm", "J_Bip_R_LowerArm", "RightLowerArm",
            "LowerArm_R", "R_ForeArm", "Bip001 R Forearm");

        leftHip ??= FindChildByNames(searchRoot,
            "Character1_LeftUpLeg", "LeftUpLeg", "mixamorig:LeftUpLeg", "mixamorigLeftUpLeg",
            "J_Bip_L_UpperLeg", "LeftUpperLeg", "UpperLeg_L", "L_Thigh", "Bip001 L Thigh");

        leftKnee ??= FindChildByNames(searchRoot,
            "Character1_LeftLeg", "LeftLeg", "mixamorig:LeftLeg", "mixamorigLeftLeg",
            "J_Bip_L_LowerLeg", "LeftLowerLeg", "LowerLeg_L", "L_Calf", "Bip001 L Calf");

        rightHip ??= FindChildByNames(searchRoot,
            "Character1_RightUpLeg", "RightUpLeg", "mixamorig:RightUpLeg", "mixamorigRightUpLeg",
            "J_Bip_R_UpperLeg", "RightUpperLeg", "UpperLeg_R", "R_Thigh", "Bip001 R Thigh");

        rightKnee ??= FindChildByNames(searchRoot,
            "Character1_RightLeg", "RightLeg", "mixamorig:RightLeg", "mixamorigRightLeg",
            "J_Bip_R_LowerLeg", "RightLowerLeg", "LowerLeg_R", "R_Calf", "Bip001 R Calf");

        spine ??= FindChildByNames(searchRoot,
            "Character1_Chest", "Character1_UpperChest", "Character1_Spine",
            "UpperChest", "Chest", "Spine",
            "mixamorig:UpperChest", "mixamorig:Chest", "mixamorig:Spine",
            "mixamorigUpperChest", "mixamorigChest", "mixamorigSpine",
            "J_Bip_C_Chest", "J_Bip_C_Spine", "Bip001 Spine1", "Bip001 Spine");

        leftShoulder ??= FindByKeywords(searchRoot, true, "shoulder", "upperarm", "arm", "clavicle");
        leftElbow ??= FindByKeywords(leftShoulder != null ? leftShoulder : searchRoot, true, "lowerarm", "forearm", "elbow");
        rightShoulder ??= FindByKeywords(searchRoot, false, "shoulder", "upperarm", "arm", "clavicle");
        rightElbow ??= FindByKeywords(rightShoulder != null ? rightShoulder : searchRoot, false, "lowerarm", "forearm", "elbow");

        leftHip ??= FindByKeywords(searchRoot, true, "upleg", "upperleg", "thigh", "leg");
        leftKnee ??= FindByKeywords(leftHip != null ? leftHip : searchRoot, true, "lowerleg", "calf", "shin", "leg");
        rightHip ??= FindByKeywords(searchRoot, false, "upleg", "upperleg", "thigh", "leg");
        rightKnee ??= FindByKeywords(rightHip != null ? rightHip : searchRoot, false, "lowerleg", "calf", "shin", "leg");
        spine ??= FindByCenterKeywords(searchRoot, "upperchest", "chest", "spine", "torso", "waist");
    }

    private bool HasAnyDrivenBone()
    {
        return
            leftShoulder != null ||
            leftElbow != null ||
            rightShoulder != null ||
            rightElbow != null ||
            leftHip != null ||
            leftKnee != null ||
            rightHip != null ||
            rightKnee != null ||
            spine != null;
    }

    private static Transform FindByCenterKeywords(Transform root, params string[] keywords)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform current = all[i];
            string lower = current.name.ToLowerInvariant();

            if (IsRejectedBoneName(lower))
            {
                continue;
            }

            for (int k = 0; k < keywords.Length; k++)
            {
                if (lower.Contains(keywords[k]))
                {
                    return current;
                }
            }
        }

        return null;
    }

    private static Transform FindByKeywords(Transform root, bool isLeft, params string[] keywords)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform current = all[i];
            string lower = current.name.ToLowerInvariant();

            if (IsRejectedBoneName(lower))
            {
                continue;
            }

            if (!IsExpectedSide(lower, isLeft))
            {
                continue;
            }

            for (int k = 0; k < keywords.Length; k++)
            {
                if (lower.Contains(keywords[k]))
                {
                    return current;
                }
            }
        }

        return null;
    }

    private static bool IsExpectedSide(string lower, bool isLeft)
    {
        bool hasLeft =
            lower.Contains("left") ||
            lower.Contains("_l") ||
            lower.Contains("l_") ||
            lower.Contains(".l") ||
            lower.Contains(" l ");

        bool hasRight =
            lower.Contains("right") ||
            lower.Contains("_r") ||
            lower.Contains("r_") ||
            lower.Contains(".r") ||
            lower.Contains(" r ");

        if (isLeft)
        {
            return hasLeft && !hasRight;
        }

        return hasRight && !hasLeft;
    }

    private static bool IsRejectedBoneName(string lowerName)
    {
        return
            lowerName.Contains("nub") ||
            lowerName.Contains("finger") ||
            lowerName.Contains("thumb") ||
            lowerName.Contains("index") ||
            lowerName.Contains("middle") ||
            lowerName.Contains("ring") ||
            lowerName.Contains("pinky") ||
            lowerName.Contains("toe") ||
            lowerName.Contains("head") ||
            lowerName.Contains("neck") ||
            lowerName.Contains("jaw") ||
            lowerName.Contains("eye");
    }

    private static Transform FindChildByNames(Transform root, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform found = FindDeepChildExact(root, names[i]);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Transform FindDeepChildExact(Transform parent, string targetName)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (string.Equals(child.name, targetName, System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            Transform result = FindDeepChildExact(child, targetName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private int ComputeBoneSignature()
    {
        unchecked
        {
            int signature = 17;
            signature = (signature * 23) + GetId(leftShoulder);
            signature = (signature * 23) + GetId(leftElbow);
            signature = (signature * 23) + GetId(rightShoulder);
            signature = (signature * 23) + GetId(rightElbow);
            signature = (signature * 23) + GetId(leftHip);
            signature = (signature * 23) + GetId(leftKnee);
            signature = (signature * 23) + GetId(rightHip);
            signature = (signature * 23) + GetId(rightKnee);
            signature = (signature * 23) + GetId(spine);
            return signature;
        }
    }

    private static int GetId(Transform value)
    {
        return value != null ? value.GetEntityId().GetHashCode() : 0;
    }

    private static string NameOrNone(Transform value)
    {
        return value != null ? value.name : "None";
    }

    private void CacheBaseRotations()
    {
        if (leftShoulder != null)
        {
            leftShoulderBaseRotation = leftShoulder.localRotation;
        }

        if (leftElbow != null)
        {
            leftElbowBaseRotation = leftElbow.localRotation;
        }

        if (rightShoulder != null)
        {
            rightShoulderBaseRotation = rightShoulder.localRotation;
        }

        if (rightElbow != null)
        {
            rightElbowBaseRotation = rightElbow.localRotation;
        }

        if (leftHip != null)
        {
            leftHipBaseRotation = leftHip.localRotation;
        }

        if (leftKnee != null)
        {
            leftKneeBaseRotation = leftKnee.localRotation;
        }

        if (rightHip != null)
        {
            rightHipBaseRotation = rightHip.localRotation;
        }

        if (rightKnee != null)
        {
            rightKneeBaseRotation = rightKnee.localRotation;
        }

        if (spine != null)
        {
            spineBaseRotation = spine.localRotation;
        }
    }
}
