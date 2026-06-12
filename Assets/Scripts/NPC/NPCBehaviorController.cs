using System;
using System.Collections.Generic;
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
    [SerializeField] [Range(0f, 1f)] private float chaoticMinTravelDistance = 0.25f;
    [SerializeField] [Min(0f)] private float minChaoticDuration = 1.5f;
    [SerializeField] [Min(0f)] private float maxExtraChaoticDuration = 3f;
    [SerializeField] private float currentChaoticEndTime;

    [Header("Probe Search")]
    [SerializeField] [Min(0.5f)] private float probeSearchRadius = 8f;
    [SerializeField] [Min(0.5f)] private float viewRadius = 5f;
    [SerializeField] private LayerMask visibilityObstacleMask = ~0;
    [SerializeField] private int lastCandidateCount;
    [SerializeField] private int lastVisibleCandidateCount;

    [Header("Point Scoring")]
    [SerializeField] [Range(0f, 1f)] private float orientationWeight = 1f;
    [SerializeField] [Range(0f, 1f)] private float dangerWeight = 1f;
    [SerializeField] [Range(0f, 1f)] private float distanceWeight = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float rescuerWeight = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float commandWeight = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float stayPointBonus = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float stopCommandBonus = 0.25f;
    [SerializeField] private float bestPointDesirability = float.NegativeInfinity;
    [SerializeField] private float currentPositionDesirability = float.NegativeInfinity;

    [Header("Decision Loop")]
    [SerializeField] [Min(0.05f)] private float decisionUpdateInterval = 0.5f;
    [SerializeField] [Min(0f)] private float switchTargetThreshold = 0.05f;
    [SerializeField] private NavigationProbePoint currentTargetPoint;
    [SerializeField] private float currentTargetDesirability = float.NegativeInfinity;

    [Header("Command Override")]
    [SerializeField] private bool hasActiveCommandOverride;
    [SerializeField] private NPCCommandType activeCommandType = NPCCommandType.Stop;
    [SerializeField] private Vector3 activeCommandTargetPosition;
    [SerializeField] private bool activeCommandHasTargetPosition;

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
    public float ProbeSearchRadius => probeSearchRadius;
    public float ViewRadius => viewRadius;
    public float BestPointDesirability => bestPointDesirability;
    public float CurrentPositionDesirability => currentPositionDesirability;
    public bool HasActiveCommandOverride => hasActiveCommandOverride;
    public NPCCommandType ActiveCommandType => activeCommandType;

    private NavMeshAgent navMeshAgent;
    private VictimNPC victimNpc;
    private bool isEvacuated;
    private float nextDecisionUpdateTime;
    private readonly List<NavigationProbePoint> lastObservedProbePoints = new List<NavigationProbePoint>();

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
        RefreshVisibleProbeCandidates();
    }

    private void Start()
    {
        if (!applyStartStateOverride)
        {
            return;
        }

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
        RefreshVisibleProbeCandidates();
        UpdateCriticalStateTransitions();

        if (!isEvacuated
            && (currentState == NPCState.Idle
                || currentState == NPCState.MoveToPoint
                || currentState == NPCState.FollowPlayer))
        {
            TryRunDecisionLoop();
        }

        if (isEvacuated
            || navMeshAgent == null
            || !navMeshAgent.enabled
            || (currentState != NPCState.MoveToPoint
                && currentState != NPCState.FollowPlayer
                && currentState != NPCState.Chaotic))
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

        if (currentState == NPCState.Chaotic)
        {
            // В хаотичном состоянии после достижения точки сразу выбираем следующую.
            MoveToChaoticPoint();
            return;
        }

        // Когда агент дошёл до цели, останавливаем его и возвращаем NPC в ожидание.
        StopMovement();
        currentTargetPoint = null;
        currentTargetDesirability = currentPositionDesirability;
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
        MoveTo(position, NPCState.MoveToPoint);
    }

    public void MoveTo(Vector3 position, NPCState movementState)
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

        // При команде движения сразу переключаем NPC в нужное состояние перемещения к точке.
        ApplyMovementSpeed();
        navMeshAgent.isStopped = false;
        navMeshAgent.SetDestination(position);
        SetState(movementState);
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

        // Динамическая паника всегда остаётся в диапазоне 0..1.
        currentPanic = Mathf.Clamp01(currentPanic + Mathf.Max(0f, value));
    }

    public void AddDamage(float value)
    {
        if (isEvacuated)
        {
            return;
        }

        // Динамический ущерб всегда остаётся в диапазоне 0..1.
        currentDamage = Mathf.Clamp01(currentDamage + Mathf.Max(0f, value));
    }

    public void ReducePanic(float value)
    {
        if (isEvacuated)
        {
            return;
        }

        // Снижение паники не должно уходить ниже нуля.
        currentPanic = Mathf.Clamp01(currentPanic - Mathf.Max(0f, value));
    }

    public void MarkEvacuated()
    {
        if (isEvacuated)
        {
            return;
        }

        // После эвакуации NPC фиксируется в финальном состоянии и больше не принимает команды.
        isEvacuated = true;
        currentTargetPoint = null;
        currentTargetDesirability = float.NegativeInfinity;
        hasActiveCommandOverride = false;
        activeCommandHasTargetPosition = false;
        currentChaoticEndTime = 0f;
        StopMovement();
        SetState(NPCState.Evacuated);
    }

    public void ApplyCommandOverride(NPCCommandType commandType, Vector3? targetPosition = null)
    {
        if (isEvacuated)
        {
            return;
        }

        hasActiveCommandOverride = true;
        activeCommandType = commandType;
        activeCommandHasTargetPosition = targetPosition.HasValue;
        activeCommandTargetPosition = targetPosition ?? transform.position;
        currentTargetPoint = null;
        currentTargetDesirability = float.NegativeInfinity;

        if (victimNpc != null)
        {
            victimNpc.SetLowMovement(commandType == NPCCommandType.LowMovement);
        }

        // Новая команда перезаписывает прошлую и сразу обновляет выбор точек только у этого NPC.
        RefreshVisibleProbeCandidates();
        EvaluateBestPointDecision();
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

    [ContextMenu("Refresh Visible Probe Candidates")]
    public void RefreshVisibleProbeCandidates()
    {
        ClearPreviousObservedProbePoints();

        var probePoints = FindObjectsByType<NavigationProbePoint>(FindObjectsInactive.Include);
        var candidateCount = 1;
        var visibleCandidateCount = 1;
        var origin = GetViewOrigin();
        bestPointDesirability = float.NegativeInfinity;

        for (var index = 0; index < probePoints.Length; index++)
        {
            var probePoint = probePoints[index];
            if (probePoint == null)
            {
                continue;
            }

            var distanceToProbe = Vector3.Distance(origin, probePoint.transform.position);
            if (distanceToProbe > probeSearchRadius)
            {
                continue;
            }

            var isVisible = distanceToProbe <= viewRadius && !HasVisibilityObstacle(origin, probePoint.transform.position);
            probePoint.SetNpcObservation(distanceToProbe, isVisible);
            lastObservedProbePoints.Add(probePoint);
            candidateCount++;

            if (isVisible)
            {
                visibleCandidateCount++;
            }

            bestPointDesirability = Mathf.Max(bestPointDesirability, ScorePoint(probePoint));
        }

        // Текущая позиция NPC всегда рассматривается как кандидат остаться на месте.
        currentPositionDesirability = ScoreCurrentPosition();
        bestPointDesirability = Mathf.Max(bestPointDesirability, currentPositionDesirability);
        lastCandidateCount = candidateCount;
        lastVisibleCandidateCount = visibleCandidateCount;
    }

    public float ScorePoint(NavigationProbePoint point)
    {
        if (point == null)
        {
            return float.NegativeInfinity;
        }

        if (point.IsBlocked)
        {
            return float.NegativeInfinity;
        }

        var parameters = victimNpc != null ? victimNpc.Parameters : null;
        var spatialOrientation = parameters != null ? parameters.SpatialOrientation : 1f;
        var dangerAvoidance = parameters != null ? parameters.DangerAvoidance : 1f;
        var trustToRescuer = parameters != null ? parameters.TrustToRescuer : 1f;
        var distanceToPoint = Mathf.Clamp01(point.DistanceToNPC / Mathf.Max(probeSearchRadius, 0.001f));
        var effectiveDistanceWeight = GetEffectiveDistanceWeight(trustToRescuer);
        var commandBonus = EvaluateCommandPointBonus(point.Position, trustToRescuer);

        // Формула оценивает выгодность точки по выходу, риску, расстоянию и активной команде.
        return point.ExitProximity * spatialOrientation * orientationWeight
            - point.PointDanger * dangerAvoidance * dangerWeight
            - distanceToPoint * effectiveDistanceWeight
            + point.RescuerProximity * trustToRescuer * rescuerWeight
            + point.CommandTargetProximity * commandWeight
            + commandBonus;
    }

    [ContextMenu("Run Decision Tick")]
    public void RunDecisionTick()
    {
        RefreshVisibleProbeCandidates();
        EvaluateBestPointDecision();
    }

    private void ClampDynamicState()
    {
        currentPanic = Mathf.Clamp01(currentPanic);
        currentDamage = Mathf.Clamp01(currentDamage);
        panicCriticalThreshold = Mathf.Clamp01(panicCriticalThreshold);
        criticalDamageThreshold = Mathf.Clamp01(criticalDamageThreshold);
        chaoticMinTravelDistance = Mathf.Clamp01(chaoticMinTravelDistance);
        debugPanicStep = Mathf.Clamp01(debugPanicStep);
        debugDamageStep = Mathf.Clamp01(debugDamageStep);
        maxSpeed = Mathf.Max(0.05f, maxSpeed);
        minimumSafeMoveSpeed = Mathf.Clamp(minimumSafeMoveSpeed, 0.05f, maxSpeed);
        minChaoticDuration = Mathf.Max(0f, minChaoticDuration);
        maxExtraChaoticDuration = Mathf.Max(0f, maxExtraChaoticDuration);
        probeSearchRadius = Mathf.Max(0.5f, probeSearchRadius);
        viewRadius = Mathf.Clamp(viewRadius, 0.5f, probeSearchRadius);
        orientationWeight = Mathf.Clamp01(orientationWeight);
        dangerWeight = Mathf.Clamp01(dangerWeight);
        distanceWeight = Mathf.Clamp01(distanceWeight);
        rescuerWeight = Mathf.Clamp01(rescuerWeight);
        commandWeight = Mathf.Clamp01(commandWeight);
        stayPointBonus = Mathf.Clamp01(stayPointBonus);
        stopCommandBonus = Mathf.Clamp01(stopCommandBonus);
        decisionUpdateInterval = Mathf.Max(0.05f, decisionUpdateInterval);
        switchTargetThreshold = Mathf.Max(0f, switchTargetThreshold);
    }

    private void ApplyMovementSpeed()
    {
        var moveSpeedFactor = 1f;

        if (victimNpc != null && victimNpc.Parameters != null)
        {
            moveSpeedFactor = Mathf.Clamp01(victimNpc.Parameters.MoveSpeed);
        }

        // Фактическая скорость строится от MaxSpeed и коэффициента MoveSpeed у NPC.
        actualSpeed = Mathf.Clamp(maxSpeed * moveSpeedFactor, minimumSafeMoveSpeed, maxSpeed);

        if (navMeshAgent != null)
        {
            navMeshAgent.speed = actualSpeed;
        }
    }

    private void ClearPreviousObservedProbePoints()
    {
        for (var index = 0; index < lastObservedProbePoints.Count; index++)
        {
            var probePoint = lastObservedProbePoints[index];
            if (probePoint == null)
            {
                continue;
            }

            probePoint.SetNpcObservation(0f, false);
        }

        lastObservedProbePoints.Clear();
    }

    private Vector3 GetViewOrigin()
    {
        if (TryGetComponent<Collider>(out var bodyCollider))
        {
            return bodyCollider.bounds.center;
        }

        return transform.position;
    }

    private bool HasVisibilityObstacle(Vector3 origin, Vector3 targetPosition)
    {
        var direction = targetPosition - origin;
        var distance = direction.magnitude;
        if (distance <= 0.001f)
        {
            return false;
        }

        return Physics.Raycast(
            origin,
            direction.normalized,
            distance,
            visibilityObstacleMask,
            QueryTriggerInteraction.Ignore);
    }

    private float ScoreCurrentPosition()
    {
        return stayPointBonus;
    }

    private void TryRunDecisionLoop()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (Time.time < nextDecisionUpdateTime)
        {
            return;
        }

        nextDecisionUpdateTime = Time.time + decisionUpdateInterval;
        EvaluateBestPointDecision();
    }

    private void UpdateCriticalStateTransitions()
    {
        if (isEvacuated || !Application.isPlaying)
        {
            return;
        }

        if (currentState == NPCState.Chaotic)
        {
            if (Time.time >= currentChaoticEndTime)
            {
                // После завершения хаотичного состояния NPC возвращается к обычной логике.
                currentTargetPoint = null;
                currentTargetDesirability = currentPositionDesirability;
                currentChaoticEndTime = 0f;
                currentPanic = 0f;
                StopMovement();
                SetState(NPCState.Idle);
            }

            return;
        }

        if (currentPanic >= panicCriticalThreshold)
        {
            EnterChaoticState();
        }
    }

    private void EvaluateBestPointDecision()
    {
        NavigationProbePoint bestPoint = null;
        var bestDesirability = currentPositionDesirability;

        for (var index = 0; index < lastObservedProbePoints.Count; index++)
        {
            var probePoint = lastObservedProbePoints[index];
            if (probePoint == null || !probePoint.VisibleForNPC)
            {
                continue;
            }

            var desirability = ScorePoint(probePoint);
            if (desirability <= bestDesirability)
            {
                continue;
            }

            bestDesirability = desirability;
            bestPoint = probePoint;
        }

        if (bestPoint == null)
        {
            // Если выгоднее остаться на месте, NPC не должен продолжать лишнее движение.
            currentTargetPoint = null;
            currentTargetDesirability = currentPositionDesirability;

            if (currentState == NPCState.MoveToPoint || currentState == NPCState.FollowPlayer)
            {
                StopMovement();
                SetState(NPCState.Idle);
            }

            return;
        }

        if (currentTargetPoint == bestPoint)
        {
            currentTargetDesirability = bestDesirability;
            return;
        }

        var activeTargetDesirability = currentTargetPoint != null ? currentTargetDesirability : currentPositionDesirability;
        if (bestDesirability <= activeTargetDesirability + switchTargetThreshold)
        {
            return;
        }

        // Переключаем цель только когда новая точка заметно лучше текущей.
        currentTargetPoint = bestPoint;
        currentTargetDesirability = bestDesirability;
        MoveTo(bestPoint.Position, ResolveMovementStateForCurrentCommand());
    }

    private void EnterChaoticState()
    {
        hasActiveCommandOverride = false;
        activeCommandHasTargetPosition = false;
        currentTargetPoint = null;
        currentTargetDesirability = float.NegativeInfinity;
        currentChaoticEndTime = Time.time + GetChaoticDuration();
        SetState(NPCState.Chaotic);
        MoveToChaoticPoint();
    }

    private float GetChaoticDuration()
    {
        var chaoticBehaviorDuration = victimNpc != null && victimNpc.Parameters != null
            ? victimNpc.Parameters.ChaoticBehaviorDuration
            : 0f;

        // Длительность хаотичного состояния складывается из базового минимума и коэффициента NPC.
        return minChaoticDuration + Mathf.Clamp01(chaoticBehaviorDuration) * maxExtraChaoticDuration;
    }

    private void MoveToChaoticPoint()
    {
        var selectedPoint = SelectChaoticPoint();
        if (selectedPoint == null)
        {
            StopMovement();
            return;
        }

        currentTargetPoint = selectedPoint;
        currentTargetDesirability = float.NegativeInfinity;
        MoveTo(selectedPoint.Position, NPCState.Chaotic);
    }

    private NavigationProbePoint SelectChaoticPoint()
    {
        var visibleUnblockedPoints = new List<NavigationProbePoint>();
        NavigationProbePoint nearestUnblockedPoint = null;
        var nearestDistance = float.MaxValue;
        var minimumChaoticPointDistance = Mathf.Max(
            navMeshAgent != null ? navMeshAgent.stoppingDistance + 0.1f : 0.1f,
            probeSearchRadius * chaoticMinTravelDistance);

        for (var index = 0; index < lastObservedProbePoints.Count; index++)
        {
            var probePoint = lastObservedProbePoints[index];
            if (probePoint == null || probePoint.IsBlocked)
            {
                continue;
            }

            if (probePoint.DistanceToNPC <= minimumChaoticPointDistance)
            {
                continue;
            }

            if (probePoint.VisibleForNPC)
            {
                visibleUnblockedPoints.Add(probePoint);
            }

            if (probePoint.DistanceToNPC < nearestDistance)
            {
                nearestDistance = probePoint.DistanceToNPC;
                nearestUnblockedPoint = probePoint;
            }
        }

        if (visibleUnblockedPoints.Count > 0)
        {
            // В хаотичном состоянии NPC выбирает случайную видимую незаблокированную точку.
            var randomIndex = UnityEngine.Random.Range(0, visibleUnblockedPoints.Count);
            return visibleUnblockedPoints[randomIndex];
        }

        // Если видимых точек нет, выбираем ближайшую незаблокированную.
        return nearestUnblockedPoint;
    }

    private float EvaluateCommandPointBonus(Vector3 pointPosition, float trustToRescuer)
    {
        if (!hasActiveCommandOverride)
        {
            return 0f;
        }

        switch (activeCommandType)
        {
            case NPCCommandType.FollowPlayer:
                if (!activeCommandHasTargetPosition)
                {
                    return 0f;
                }

                return CalculateTargetProximity(pointPosition, activeCommandTargetPosition)
                    * trustToRescuer
                    * commandWeight;
            case NPCCommandType.Stop:
                return 0f;
            case NPCCommandType.GoThere:
                if (!activeCommandHasTargetPosition)
                {
                    return 0f;
                }

                // Команда GoThere усиливает точки рядом с целью, пока другая точка не станет заметно лучше.
                return CalculateTargetProximity(pointPosition, activeCommandTargetPosition)
                    * commandWeight
                    * trustToRescuer;
            case NPCCommandType.LowMovement:
                return 0f;
            default:
                return 0f;
        }
    }

    private float CalculateTargetProximity(Vector3 pointPosition, Vector3 targetPosition)
    {
        var distance = Vector3.Distance(pointPosition, targetPosition);
        var normalizedDistance = Mathf.Clamp01(distance / Mathf.Max(probeSearchRadius, 0.001f));
        return 1f - normalizedDistance;
    }

    private float GetEffectiveDistanceWeight(float trustToRescuer)
    {
        if (!hasActiveCommandOverride || activeCommandType != NPCCommandType.Stop)
        {
            return distanceWeight;
        }

        // При команде Stop усиливаем влияние штрафа за расстояние, не меняя само базовое DistanceWeight.
        return distanceWeight + stopCommandBonus * trustToRescuer;
    }

    private NPCState ResolveMovementStateForCurrentCommand()
    {
        if (hasActiveCommandOverride && activeCommandType == NPCCommandType.FollowPlayer)
        {
            return NPCState.FollowPlayer;
        }

        return NPCState.MoveToPoint;
    }
}
