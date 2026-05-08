using Unity.VisualScripting;
using UnityEngine;

public sealed class TitanDrillController : MonoBehaviour
{
    [SerializeField] private float spinDegreesPerSecond = 1440f;
    [SerializeField] private Vector3 localSpinAxis = Vector3.right;

    private TitanController titanController;
    private Quaternion drillBaseLocalRotation;
    private bool hasDrillBaseLocalRotation;
    private float spinDegrees;

    private void Awake()
    {
        titanController = gameObject.GetOrAddComponent<TitanController>();
    }

    private void Update()
    {
        bool active = titanController.LeftDrillActive;
        if (!active)
            return;

        RotateDrill(Time.deltaTime);
    }

    private void RotateDrill(float deltaTime)
    {
        Transform drill = Managers.TitanRig.Drill;
        if (drill == null)
            return;

        if (!hasDrillBaseLocalRotation)
        {
            drillBaseLocalRotation = drill.localRotation;
            hasDrillBaseLocalRotation = true;
        }

        spinDegrees += spinDegreesPerSecond * deltaTime;
        drill.localRotation = drillBaseLocalRotation * Quaternion.AngleAxis(spinDegrees, localSpinAxis.normalized);
    }
}
