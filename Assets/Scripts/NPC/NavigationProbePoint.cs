using UnityEngine;

[DisallowMultipleComponent]
[ExecuteAlways]
public class NavigationProbePoint : MonoBehaviour
{
    [SerializeField] private string pointId;
    [SerializeField] private Vector3 position;
    [SerializeField] private string zoneId;
    [SerializeField] [Range(0f, 1f)] private float exitProximity;
    [SerializeField] [Range(0f, 1f)] private float pointDanger;
    [SerializeField] private bool isBlocked;
    [SerializeField] private float distanceToNPC;
    [SerializeField] private float distanceToPlayer;
    [SerializeField] [Range(0f, 1f)] private float rescuerProximity;
    [SerializeField] [Range(0f, 1f)] private float commandTargetProximity;
    [SerializeField] private bool visibleForNPC;

    public string PointId => pointId;
    public Vector3 Position => position;
    public string ZoneId => zoneId;
    public float ExitProximity => exitProximity;
    public float PointDanger => pointDanger;
    public bool IsBlocked => isBlocked;
    public float DistanceToNPC => distanceToNPC;
    public float DistanceToPlayer => distanceToPlayer;
    public float RescuerProximity => rescuerProximity;
    public float CommandTargetProximity => commandTargetProximity;
    public bool VisibleForNPC => visibleForNPC;

    private void Reset()
    {
        RefreshPosition();
        ResetDynamicValues();
    }

    private void OnEnable()
    {
        RefreshPosition();
    }

    private void OnValidate()
    {
        RefreshPosition();
        ClampNormalizedValues();
    }

    private void Update()
    {
        RefreshPosition();
    }

    [ContextMenu("Reset Dynamic Values")]
    public void ResetDynamicValues()
    {
        // Динамические показатели точки сбрасываются перед новым проходом оценки навигации.
        pointDanger = 0f;
        isBlocked = false;
        distanceToNPC = 0f;
        distanceToPlayer = 0f;
        rescuerProximity = 0f;
        commandTargetProximity = 0f;
        visibleForNPC = false;
    }

    public void SetExitProximity(float value)
    {
        exitProximity = Mathf.Clamp01(value);
    }

    public void SetDangerState(float dangerValue, bool blocked)
    {
        pointDanger = Mathf.Clamp01(dangerValue);
        isBlocked = blocked;
    }

    public void SetNpcObservation(float npcDistance, bool isVisible)
    {
        distanceToNPC = Mathf.Max(0f, npcDistance);
        visibleForNPC = isVisible;
    }

    private void RefreshPosition()
    {
        position = transform.position;
    }

    private void ClampNormalizedValues()
    {
        exitProximity = Mathf.Clamp01(exitProximity);
        pointDanger = Mathf.Clamp01(pointDanger);
        rescuerProximity = Mathf.Clamp01(rescuerProximity);
        commandTargetProximity = Mathf.Clamp01(commandTargetProximity);
        distanceToNPC = Mathf.Max(0f, distanceToNPC);
        distanceToPlayer = Mathf.Max(0f, distanceToPlayer);
    }
}
