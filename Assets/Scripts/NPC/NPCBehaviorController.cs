using System;
using UnityEngine;

[DisallowMultipleComponent]
public class NPCBehaviorController : MonoBehaviour
{
    [SerializeField] private NPCState initialState = NPCState.Idle;
    [SerializeField] private NPCState currentState = NPCState.Idle;
    [Header("Debug")]
    [SerializeField] private bool applyStartStateOverride;
    [SerializeField] private NPCState startStateOverride = NPCState.MoveToPoint;
    [SerializeField] private NPCState debugStateToApply = NPCState.MoveToPoint;

    public event Action<NPCState, NPCState> StateChanged;

    public NPCState CurrentState => currentState;

    private void Awake()
    {
        currentState = initialState;
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
}
