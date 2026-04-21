using UnityEngine;

public class TitanController : MonoBehaviour
{
    [Header("Auto Attach")]
    [SerializeField] private bool attachControllersOnAwake = true;

    [Header("References")]
    [SerializeField] private TitanRigRuntime rigRuntime;
    [SerializeField] private TitanBodyRoleController bodyController;
    [SerializeField] private TitanLeftArmRoleController leftArmController;
    [SerializeField] private TitanRightArmRoleController rightArmController;
    [SerializeField] private TitanLeftLegRoleController leftLegController;
    [SerializeField] private TitanRightLegRoleController rightLegController;

    private void Awake()
    {
        if (attachControllersOnAwake)
        {
            EnsureInitialized();
        }
    }

    public void EnsureInitialized()
    {
        rigRuntime = EnsureComponent(rigRuntime);
        Managers.TitanRig.Bind(rigRuntime);

        bodyController = EnsureComponent(bodyController);
        leftArmController = EnsureComponent(leftArmController);
        rightArmController = EnsureComponent(rightArmController);
        leftLegController = EnsureComponent(leftLegController);
        rightLegController = EnsureComponent(rightLegController);
    }

    private T EnsureComponent<T>(T existing) where T : Component
    {
        if (existing != null)
        {
            return existing;
        }

        T found = GetComponent<T>();
        if (found != null)
        {
            return found;
        }

        return gameObject.AddComponent<T>();
    }
}
