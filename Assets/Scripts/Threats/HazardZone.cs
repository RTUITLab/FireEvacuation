using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class HazardZone : MonoBehaviour
{
    public enum HazardType
    {
        None = 0,
        Smoke = 1,
        Fire = 2
    }

    [SerializeField] private HazardType hazardType = HazardType.None;
    [SerializeField] [Range(0f, 1f)] private float hazardLevel;
    [SerializeField] private float damageRate;
    [SerializeField] private float panicRate;
    [SerializeField] private float pathDangerWeight = 1f;
    [SerializeField] private bool affectsNPC = true;
    [SerializeField] private bool affectsPlayer;
    [SerializeField] private Color smokeGizmoColor = new Color(0.55f, 0.55f, 0.55f, 0.2f);
    [SerializeField] private Color fireGizmoColor = new Color(1f, 0.35f, 0.1f, 0.2f);
    [SerializeField] private Color neutralGizmoColor = new Color(1f, 0.92f, 0.16f, 0.2f);

    private Collider hazardCollider;
    private bool isActiveHazard = true;

    public bool AffectsNPC => affectsNPC;
    public bool AffectsPlayer => affectsPlayer;
    public bool IsActiveHazard => isActiveHazard;

    private void Reset()
    {
        EnsureTriggerCollider();
        ClampSerializedValues();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
        ClampSerializedValues();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
        ClampSerializedValues();
    }

    public HazardType GetHazardType()
    {
        return hazardType;
    }

    public float GetHazardLevel()
    {
        return isActiveHazard ? hazardLevel : 0f;
    }

    public float GetDamageRate()
    {
        return isActiveHazard ? damageRate * hazardLevel : 0f;
    }

    public float GetPanicRate()
    {
        return isActiveHazard ? panicRate * hazardLevel : 0f;
    }

    public float GetPathDangerContribution()
    {
        return isActiveHazard ? hazardLevel * pathDangerWeight : 0f;
    }

    public void SetHazardLevel(float value)
    {
        hazardLevel = Mathf.Clamp01(value);
    }

    public void SetActiveHazard(bool value)
    {
        isActiveHazard = value;
    }

    private void EnsureTriggerCollider()
    {
        hazardCollider = GetComponent<Collider>();
        if (hazardCollider != null && !hazardCollider.isTrigger)
        {
            Debug.LogWarning($"HazardZone '{name}' requires Collider.isTrigger enabled. Enabling automatically.", this);
            hazardCollider.isTrigger = true;
        }
    }

    private void ClampSerializedValues()
    {
        hazardLevel = Mathf.Clamp01(hazardLevel);
    }

    private void OnDrawGizmosSelected()
    {
        var zoneCollider = GetComponent<Collider>();
        if (zoneCollider == null)
        {
            return;
        }

        var gizmoColor = ResolveGizmoColor();
        Gizmos.color = gizmoColor;
        Gizmos.matrix = transform.localToWorldMatrix;

        if (zoneCollider is BoxCollider boxCollider)
        {
            Gizmos.DrawCube(boxCollider.center, boxCollider.size);
            Gizmos.color = ToWireColor(gizmoColor);
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
            return;
        }

        if (zoneCollider is SphereCollider sphereCollider)
        {
            Gizmos.DrawSphere(sphereCollider.center, sphereCollider.radius);
            Gizmos.color = ToWireColor(gizmoColor);
            Gizmos.DrawWireSphere(sphereCollider.center, sphereCollider.radius);
            return;
        }

        if (zoneCollider is CapsuleCollider capsuleCollider)
        {
            DrawCapsuleGizmos(capsuleCollider, gizmoColor);
            return;
        }

        var bounds = zoneCollider.bounds;
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.DrawCube(bounds.center, bounds.size);
        Gizmos.color = ToWireColor(gizmoColor);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }

    private Color ResolveGizmoColor()
    {
        var baseColor = hazardType switch
        {
            HazardType.Smoke => smokeGizmoColor,
            HazardType.Fire => fireGizmoColor,
            _ => neutralGizmoColor
        };

        baseColor.a = Mathf.Lerp(0.1f, 0.35f, hazardLevel);
        return baseColor;
    }

    private static Color ToWireColor(Color fillColor)
    {
        return new Color(fillColor.r, fillColor.g, fillColor.b, 1f);
    }

    private void DrawCapsuleGizmos(CapsuleCollider capsuleCollider, Color gizmoColor)
    {
        var axis = capsuleCollider.direction switch
        {
            0 => Vector3.right,
            1 => Vector3.up,
            _ => Vector3.forward
        };

        var center = capsuleCollider.center;
        var radius = capsuleCollider.radius;
        var cylinderLength = Mathf.Max(0f, capsuleCollider.height * 0.5f - radius);
        var offset = axis * cylinderLength;

        Gizmos.DrawSphere(center + offset, radius);
        Gizmos.DrawSphere(center - offset, radius);

        Gizmos.color = ToWireColor(gizmoColor);
        Gizmos.DrawWireSphere(center + offset, radius);
        Gizmos.DrawWireSphere(center - offset, radius);
    }
}
