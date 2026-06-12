using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class VictimNPC : MonoBehaviour
{
    private const float LowMovementSmokeDamageMultiplier = 0.5f;
    private const float LowMovementSmokePanicMultiplier = 0.7f;

    public enum VictimState
    {
        Idle = 0,
        Following = 1,
        Dragged = 2,
        Rescued = 3,
        Critical = 4,
        Lost = 5
    }

    [SerializeField] private string npcId;
    [SerializeField] private bool isRescued;
    [SerializeField] private float condition = 1f;
    [SerializeField] [Range(0f, 1f)] private float currentDamage;
    [SerializeField] [Range(0f, 1f)] private float currentPanic;
    [SerializeField] private HazardZone currentHazardZone;
    [SerializeField] private bool isInLowMovement;
    [SerializeField] private NPCParameterSet parameters = new NPCParameterSet();
    [SerializeField] [Range(0f, 1f)] private float criticalDamageThreshold = 0.75f;
    [SerializeField] private VictimState initialState = VictimState.Idle;
    [SerializeField] private Color rescuedColor = new Color(0.24f, 0.85f, 0.32f, 1f);
    [SerializeField] private Color criticalColor = new Color(0.82f, 0.16f, 0.16f, 1f);
    [SerializeField] private Color lostColor = new Color(0.3f, 0.3f, 0.36f, 1f);

    private VictimState currentState;
    private Renderer[] cachedRenderers;
    private bool isCritical;
    private bool isLost;

    public string NpcId => npcId;
    public bool IsRescued => isRescued;
    public float Condition => condition;
    public float CurrentDamage => currentDamage;
    public float CurrentPanic => currentPanic;
    public HazardZone CurrentHazardZone => currentHazardZone;
    public bool IsInLowMovement => isInLowMovement;
    public NPCParameterSet Parameters => parameters;
    public float Weight => parameters.Weight;
    public bool IsCritical => isCritical;
    public bool IsLost => isLost;
    public VictimState CurrentState => currentState;

    private void Awake()
    {
        EnsureNpcId();
        EnsureUniqueNpcId();
        condition = Mathf.Clamp01(condition);
        currentDamage = Mathf.Clamp01(currentDamage);
        currentPanic = Mathf.Clamp01(currentPanic);
        EnsureParameters();
        parameters.ClampAll();
        criticalDamageThreshold = Mathf.Clamp01(criticalDamageThreshold);
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        isLost = !isRescued && currentDamage >= 1f;
        isCritical = !isRescued && !isLost && currentDamage >= criticalDamageThreshold;
        currentState = ResolveStateAfterStatusChange(initialState);

        if (isRescued)
        {
            ApplyColor(rescuedColor);
        }
        else if (isLost)
        {
            ApplyLostPresentation(false);
        }
        else if (isCritical)
        {
            EnterCriticalState(false);
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
        EnsureParameters();
        parameters.ClampAll();
        criticalDamageThreshold = Mathf.Clamp01(criticalDamageThreshold);
        EnsureNpcId();
        EnsureUniqueNpcId();
    }

    public void SetState(VictimState newState)
    {
        if (isRescued && newState != VictimState.Rescued)
        {
            return;
        }

        if (isCritical && newState != VictimState.Dragged && newState != VictimState.Rescued)
        {
            currentState = VictimState.Critical;
            return;
        }

        if (isLost && newState != VictimState.Dragged && newState != VictimState.Rescued)
        {
            currentState = VictimState.Lost;
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
        isCritical = false;
        isLost = false;
        currentState = VictimState.Rescued;
        if (TryGetComponent<NPCBehaviorController>(out var behaviorController))
        {
            // Синхронизируем старую эвакуацию VictimNPC с новым состоянием NPCBehaviorController.
            behaviorController.MarkEvacuated();
        }

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
        condition = Mathf.Clamp01(1f - currentDamage);

        if (!isLost && currentDamage >= 1f)
        {
            MarkLost();
            return;
        }

        if (!isCritical && currentDamage >= criticalDamageThreshold)
        {
            EnterCriticalState();
        }
    }

    public bool MarkLost()
    {
        if (isRescued || isLost)
        {
            return false;
        }

        isLost = true;
        isCritical = false;
        currentDamage = 1f;
        condition = 0f;
        currentState = VictimState.Lost;
        StopSelfMovement();
        ApplyLostPresentation();

        var scenarioManager = ScenarioManager.FindInScene();
        if (scenarioManager != null)
        {
            scenarioManager.NotifyVictimLost(this);
        }

        Debug.Log($"VictimNPC '{npcId}' became lost.", this);
        return true;
    }

    public void SetCurrentHazardZone(HazardZone zone)
    {
        currentHazardZone = zone;
    }

    public void SetLowMovement(bool value)
    {
        isInLowMovement = value;
    }

    public void RefreshStatusColor()
    {
        if (isRescued)
        {
            ApplyColor(rescuedColor);
            return;
        }

        if (isCritical)
        {
            ApplyColor(criticalColor);
            return;
        }

        if (isLost)
        {
            ApplyColor(lostColor);
        }
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

    private void StopSelfMovement()
    {
        if (TryGetComponent<NavMeshAgent>(out var navMeshAgent) && navMeshAgent.enabled)
        {
            navMeshAgent.isStopped = true;
            navMeshAgent.ResetPath();
        }
    }

    private void EnterCriticalState(bool updateColor = true)
    {
        if (isRescued || isLost)
        {
            return;
        }

        isCritical = true;
        if (currentState != VictimState.Dragged)
        {
            // При критическом уроне NPC переходит в недееспособное состояние.
            currentState = VictimState.Critical;
        }

        StopSelfMovement();

        if (updateColor)
        {
            ApplyColor(criticalColor);
        }
    }

    private float GetFearfulness()
    {
        return parameters.Fearfulness;
    }

    private VictimState ResolveStateAfterStatusChange(VictimState fallbackState)
    {
        if (isRescued)
        {
            return VictimState.Rescued;
        }

        if (isLost)
        {
            return VictimState.Lost;
        }

        if (isCritical)
        {
            return VictimState.Critical;
        }

        return fallbackState;
    }

    private void ApplyLostPresentation(bool updateColor = true)
    {
        var currentEulerAngles = transform.rotation.eulerAngles;
        transform.rotation = Quaternion.Euler(90f, currentEulerAngles.y, 0f);

        if (updateColor)
        {
            ApplyColor(lostColor);
        }
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

    private void EnsureParameters()
    {
        if (parameters == null)
        {
            parameters = new NPCParameterSet();
        }
    }

}
