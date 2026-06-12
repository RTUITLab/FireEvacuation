using System;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class NPCBehaviorController : MonoBehaviour
{
    [SerializeField] private NPCState initialState = NPCState.Idle;
    [SerializeField] private NPCState currentState = NPCState.Idle;

    [Header("Movement")]
    [SerializeField] [Min(0.05f)] private float maxSpeed = 1.75f;
    [SerializeField] [Min(0.05f)] private float minimumSafeMoveSpeed = 0.15f;
    [SerializeField] private float actualSpeed = 1.75f;

    [Header("Dynamic State")]
    [SerializeField] [Range(0f, 1f)] private float currentPanic;
    [SerializeField] [Range(0f, 1f)] private float currentDamage;
    [SerializeField] [Range(0f, 1f)] private float panicCriticalThreshold = 0.8f;
    [SerializeField] [Range(0f, 1f)] private float criticalDamageThreshold = 0.8f;

    [Header("Debug")]
    [SerializeField] private bool applyStartStateOverride;
    [SerializeField] private NPCState startStateOverride = NPCState.MoveToPoint;
    [SerializeField] private NPCState debugStateToApply = NPCState.MoveToPoint;
    [SerializeField] private Transform debugMoveTarget;
    [SerializeField] [Range(0f, 1f)] private float debugPanicStep = 0.1f;
    [SerializeField] [Range(0f, 1f)] private float debugDamageStep = 0.1f;

    public event Action<NPCState, NPCState> StateChanged;

    public NPCState CurrentState => currentState;
    public float CurrentPanic => currentPanic;
    public float CurrentDamage => currentDamage;
    public float PanicCriticalThreshold => panicCriticalThreshold;
    public float CriticalDamageThreshold => criticalDamageThreshold;
    public float ActualSpeed => actualSpeed;

    private NavMeshAgent navMeshAgent;
    private VictimNPC victimNpc;
    private bool isEvacuated;

    private void Awake()
    {
        if (!TryGetComponent(out navMeshAgent))
        {
            Debug.LogWarning($"NPC {name}: NavMeshAgent is missing. Movement commands will be ignored.", this);
        }

        TryGetComponent(out victimNpc);

        currentState = initialState;
        ClampDynamicState();
        ApplyMovementSpeed();
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
        TryGetComponent(out navMeshAgent);
        TryGetComponent(out victimNpc);
        ApplyMovementSpeed();
    }

    private void Update()
    {
        if (isEvacuated || navMeshAgent == null || !navMeshAgent.enabled || currentState != NPCState.MoveToPoint)
        {
            return;
        }

        if (navMeshAgent.pathPending)
        {
            return;
        }

        if (navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance)
        {
            return;
        }

        if (navMeshAgent.hasPath && navMeshAgent.velocity.sqrMagnitude > 0.0001f)
        {
            return;
        }

        // Когда агент дошел до цели, останавливаем его и возвращаем NPC в состояние ожидания.
        StopMovement();
        SetState(NPCState.Idle);
    }

    public void SetState(NPCState newState)
    {
        if (isEvacuated && newState != NPCState.Evacuated)
        {
            return;
        }

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
        if (isEvacuated)
        {
            return;
        }

        if (navMeshAgent == null || !navMeshAgent.enabled)
        {
            Debug.LogWarning($"NPC {name}: MoveTo failed because NavMeshAgent is missing or disabled.", this);
            return;
        }

        // При команде движения сразу переключаем NPC в состояние перемещения к точке.
        ApplyMovementSpeed();
        navMeshAgent.isStopped = false;
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

        if (!navMeshAgent.enabled)
        {
            return;
        }

        navMeshAgent.isStopped = true;
        navMeshAgent.ResetPath();
    }

    public void AddPanic(float value)
    {
        if (isEvacuated)
        {
            return;
        }

        // Динамическая паника всегда ограничивается диапазоном 0..1.
        currentPanic = Mathf.Clamp01(currentPanic + Mathf.Max(0f, value));
    }

    public void AddDamage(float value)
    {
        if (isEvacuated)
        {
            return;
        }

        // Динамический ущерб всегда ограничивается диапазоном 0..1.
        currentDamage = Mathf.Clamp01(currentDamage + Mathf.Max(0f, value));
    }

    public void ReducePanic(float value)
    {
        if (isEvacuated)
        {
            return;
        }

        // Снижение паники также не должно выводить значение за нижнюю границу.
        currentPanic = Mathf.Clamp01(currentPanic - Mathf.Max(0f, value));
    }

    public void MarkEvacuated()
    {
        if (isEvacuated)
        {
            return;
        }

        // После эвакуации NPC фиксируется в финальном состоянии и больше не принимает новые команды.
        isEvacuated = true;
        StopMovement();
        SetState(NPCState.Evacuated);
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

    [ContextMenu("Move To Debug Target")]
    public void MoveToDebugTarget()
    {
        if (debugMoveTarget == null)
        {
            Debug.LogWarning($"NPC {name}: Debug move target is not assigned.", this);
            return;
        }

        // Отладочный вызов использует позицию выбранного объекта как целевую точку для NavMeshAgent.
        MoveTo(debugMoveTarget.position);
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
        maxSpeed = Mathf.Max(0.05f, maxSpeed);
        minimumSafeMoveSpeed = Mathf.Clamp(minimumSafeMoveSpeed, 0.05f, maxSpeed);
    }

    private void ApplyMovementSpeed()
    {
        var moveSpeedFactor = 1f;

        if (victimNpc != null && victimNpc.Parameters != null)
        {
            moveSpeedFactor = Mathf.Clamp01(victimNpc.Parameters.MoveSpeed);
        }

        // Фактическая скорость строится от MaxSpeed и коэффициента MoveSpeed из параметров NPC.
        actualSpeed = Mathf.Clamp(maxSpeed * moveSpeedFactor, minimumSafeMoveSpeed, maxSpeed);

        if (navMeshAgent != null)
        {
            navMeshAgent.speed = actualSpeed;
        }
    }
}
