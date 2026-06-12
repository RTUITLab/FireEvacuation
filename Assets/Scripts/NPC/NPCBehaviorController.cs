using System;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class NPCBehaviorController : MonoBehaviour
{
    [SerializeField] private NPCState initialState = NPCState.Idle;
    [SerializeField] private NPCState currentState = NPCState.Idle;
    [Header("Dynamic State")]
    [SerializeField] [Range(0f, 1f)] private float currentPanic;
    [SerializeField] [Range(0f, 1f)] private float currentDamage;
    [SerializeField] [Range(0f, 1f)] private float panicCriticalThreshold = 0.8f;
    [SerializeField] [Range(0f, 1f)] private float criticalDamageThreshold = 0.8f;
    [Header("Debug")]
    [SerializeField] private bool applyStartStateOverride;
    [SerializeField] private NPCState startStateOverride = NPCState.MoveToPoint;
    [SerializeField] private NPCState debugStateToApply = NPCState.MoveToPoint;
    [SerializeField] [Range(0f, 1f)] private float debugPanicStep = 0.1f;
    [SerializeField] [Range(0f, 1f)] private float debugDamageStep = 0.1f;

    public event Action<NPCState, NPCState> StateChanged;

    public NPCState CurrentState => currentState;
    public float CurrentPanic => currentPanic;
    public float CurrentDamage => currentDamage;
    public float PanicCriticalThreshold => panicCriticalThreshold;
    public float CriticalDamageThreshold => criticalDamageThreshold;

    private NavMeshAgent navMeshAgent;

    private void Awake()
    {
        if (!TryGetComponent(out navMeshAgent))
        {
            Debug.LogWarning($"NPC {name}: NavMeshAgent is missing. Movement commands will be ignored.", this);
        }

        currentState = initialState;
        ClampDynamicState();
    }

    private void Start()
    {
        if (!applyStartStateOverride)
        {
            return;
        }

        // Временный отладочный переход помогает быстро проверить лог смены состояния в сцене.
        SetState(startStateOverride);
    }

    private void OnValidate()
    {
        ClampDynamicState();
    }

    public void SetState(NPCState newState)
    {
        if (currentState == newState)
        {
            return;
        }

        var oldState = currentState;
        currentState = newState;

        Debug.Log($"NPC {name}: {oldState} -> {newState}", this);
        StateChanged?.Invoke(oldState, newState);
    }

    public void MoveTo(Vector3 position)
    {
        if (navMeshAgent == null)
        {
            Debug.LogWarning($"NPC {name}: MoveTo failed because NavMeshAgent is missing.", this);
            return;
        }

        // При команде движения сразу переключаем NPC в состояние перемещения к точке.
        navMeshAgent.SetDestination(position);
        SetState(NPCState.MoveToPoint);
    }

    public void StopMovement()
    {
        if (navMeshAgent == null)
        {
            Debug.LogWarning($"NPC {name}: StopMovement ignored because NavMeshAgent is missing.", this);
            return;
        }

        navMeshAgent.ResetPath();
    }

    public void AddPanic(float value)
    {
        // Динамическая паника всегда ограничивается диапазоном 0..1.
        currentPanic = Mathf.Clamp01(currentPanic + Mathf.Max(0f, value));
    }

    public void AddDamage(float value)
    {
        // Динамический ущерб всегда ограничивается диапазоном 0..1.
        currentDamage = Mathf.Clamp01(currentDamage + Mathf.Max(0f, value));
    }

    public void ReducePanic(float value)
    {
        // Снижение паники также не должно выводить значение за нижнюю границу.
        currentPanic = Mathf.Clamp01(currentPanic - Mathf.Max(0f, value));
    }

    [ContextMenu("Apply Debug State")]
    public void ApplyDebugState()
    {
        SetState(debugStateToApply);
    }

    [ContextMenu("Reset To Initial State")]
    public void ResetToInitialState()
    {
        SetState(initialState);
    }

    [ContextMenu("Add Debug Panic")]
    public void AddDebugPanic()
    {
        AddPanic(debugPanicStep);
    }

    [ContextMenu("Add Debug Damage")]
    public void AddDebugDamage()
    {
        AddDamage(debugDamageStep);
    }

    [ContextMenu("Reduce Debug Panic")]
    public void ReduceDebugPanic()
    {
        ReducePanic(debugPanicStep);
    }

    private void ClampDynamicState()
    {
        currentPanic = Mathf.Clamp01(currentPanic);
        currentDamage = Mathf.Clamp01(currentDamage);
        panicCriticalThreshold = Mathf.Clamp01(panicCriticalThreshold);
        criticalDamageThreshold = Mathf.Clamp01(criticalDamageThreshold);
        debugPanicStep = Mathf.Clamp01(debugPanicStep);
        debugDamageStep = Mathf.Clamp01(debugDamageStep);
    }
}
