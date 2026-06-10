using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class VictimNPC : MonoBehaviour
{
    private const float DefaultFearfulness = 0.5f;
    private const float LowMovementSmokeDamageMultiplier = 0.5f;
    private const float LowMovementSmokePanicMultiplier = 0.7f;

    public enum VictimState
    {
        Idle = 0,
        Following = 1,
        Dragged = 2,
        Rescued = 3
    }

    [SerializeField] private string npcId;
    [SerializeField] private bool isRescued;
    [SerializeField] private float condition = 1f;
    [SerializeField] [Range(0f, 1f)] private float currentDamage;
    [SerializeField] [Range(0f, 1f)] private float currentPanic;
    [SerializeField] private HazardZone currentHazardZone;
    [SerializeField] private bool isInLowMovement;
    [SerializeField] private VictimState initialState = VictimState.Idle;
    [SerializeField] private Color rescuedColor = new Color(0.24f, 0.85f, 0.32f, 1f);

    private VictimState currentState;
    private Renderer[] cachedRenderers;

    public string NpcId => npcId;
    public bool IsRescued => isRescued;
    public float Condition => condition;
    public float CurrentDamage => currentDamage;
    public float CurrentPanic => currentPanic;
    public HazardZone CurrentHazardZone => currentHazardZone;
    public bool IsInLowMovement => isInLowMovement;
    public VictimState CurrentState => currentState;

    private void Awake()
    {
        EnsureNpcId();
        EnsureUniqueNpcId();
        condition = Mathf.Clamp01(condition);
        currentDamage = Mathf.Clamp01(currentDamage);
        currentPanic = Mathf.Clamp01(currentPanic);
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        currentState = isRescued ? VictimState.Rescued : initialState;

        if (isRescued)
        {
            ApplyColor(rescuedColor);
        }
    }

    private void OnEnable()
    {
        RegisterWithScenarioManager();
    }

    private void OnValidate()
    {
        condition = Mathf.Clamp01(condition);
        currentDamage = Mathf.Clamp01(currentDamage);
        currentPanic = Mathf.Clamp01(currentPanic);
        EnsureNpcId();
        EnsureUniqueNpcId();
    }

    public void SetState(VictimState newState)
    {
        if (isRescued && newState != VictimState.Rescued)
        {
            return;
        }

        currentState = isRescued ? VictimState.Rescued : newState;
    }

    public bool MarkRescued()
    {
        if (isRescued)
        {
            return false;
        }

        isRescued = true;
        currentState = VictimState.Rescued;
        DisableActiveBehaviour();
        ApplyColor(rescuedColor);

        Debug.Log($"VictimNPC '{npcId}' rescued.", this);
        return true;
    }

    public void ApplyHazardEffect(HazardZone zone, float deltaTime)
    {
        if (zone == null || deltaTime <= 0f || isRescued)
        {
            return;
        }

        currentHazardZone = zone;

        var damageDelta = zone.GetDamageRate() * deltaTime;
        var panicDelta = zone.GetPanicRate() * GetFearfulness() * deltaTime;

        if (zone.GetHazardType() == HazardZone.HazardType.Smoke && isInLowMovement)
        {
            damageDelta *= LowMovementSmokeDamageMultiplier;
            panicDelta *= LowMovementSmokePanicMultiplier;
        }

        currentDamage = Mathf.Clamp01(currentDamage + damageDelta);
        currentPanic = Mathf.Clamp01(currentPanic + panicDelta);
    }

    public void SetCurrentHazardZone(HazardZone zone)
    {
        currentHazardZone = zone;
    }

    public void SetLowMovement(bool value)
    {
        isInLowMovement = value;
    }

    private void EnsureNpcId()
    {
        if (!string.IsNullOrWhiteSpace(npcId))
        {
            return;
        }

        npcId = $"{gameObject.name}_{Guid.NewGuid():N}";
    }

    private void EnsureUniqueNpcId()
    {
        if (!gameObject.scene.IsValid() || string.IsNullOrWhiteSpace(npcId))
        {
            return;
        }

        var victims = FindObjectsByType<VictimNPC>(FindObjectsInactive.Include);
        for (var index = 0; index < victims.Length; index++)
        {
            var otherVictim = victims[index];
            if (otherVictim == null || otherVictim == this)
            {
                continue;
            }

            if (!string.Equals(otherVictim.npcId, npcId, StringComparison.Ordinal))
            {
                continue;
            }

            npcId = $"{gameObject.name}_{Guid.NewGuid():N}";
            return;
        }
    }

    private void RegisterWithScenarioManager()
    {
        var scenarioManager = ScenarioManager.FindInScene();
        if (scenarioManager == null)
        {
            return;
        }

        scenarioManager.RegisterVictim(this);
    }

    private void DisableActiveBehaviour()
    {
        if (TryGetComponent<VictimHandHoldController>(out var handHoldController))
        {
            handHoldController.ForceRelease();
            handHoldController.enabled = false;
        }

        if (TryGetComponent<XRGrabInteractable>(out var grabInteractable))
        {
            grabInteractable.enabled = false;
        }

        if (TryGetComponent<NavMeshAgent>(out var navMeshAgent))
        {
            navMeshAgent.enabled = false;
        }

        if (TryGetComponent<Rigidbody>(out var body))
        {
            body.isKinematic = true;
            body.useGravity = false;
        }
    }

    private float GetFearfulness()
    {
        // TODO: replace with VictimParameters.Fearfulness when NPC parameter data is introduced.
        return DefaultFearfulness;
    }

    private void ApplyColor(Color color)
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
        }

        for (var index = 0; index < cachedRenderers.Length; index++)
        {
            var rendererComponent = cachedRenderers[index];
            if (rendererComponent == null)
            {
                continue;
            }

            var material = rendererComponent.material;
            if (material == null)
            {
                continue;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }
    }
}
