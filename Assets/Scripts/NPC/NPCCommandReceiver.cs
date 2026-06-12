using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NPCBehaviorController))]
public class NPCCommandReceiver : MonoBehaviour
{
    [Serializable]
    private struct NPCCommandModel
    {
        public NPCCommandType Type;
        public bool HasTargetPosition;
        public Vector3 TargetPosition;
        public float ReceivedAtTime;
        public float ActivationTime;
    }

    [Header("Reaction Delay")]
    [SerializeField] [Min(0f)] private float minReactionDelay = 0.15f;
    [SerializeField] [Min(0f)] private float maxExtraReactionDelay = 1f;

    [Header("Runtime")]
    [SerializeField] private bool hasPendingCommand;
    [SerializeField] private NPCCommandModel pendingCommand;
    [SerializeField] private bool hasActiveCommand;
    [SerializeField] private NPCCommandModel activeCommand;

    [Header("Debug")]
    [SerializeField] private NPCCommandType debugCommandType = NPCCommandType.FollowPlayer;
    [SerializeField] private Transform debugTargetTransform;

    private NPCBehaviorController behaviorController;
    private VictimNPC victimNpc;

    public bool HasPendingCommand => hasPendingCommand;
    public bool HasActiveCommand => hasActiveCommand;
    public NPCCommandType? ActiveCommandType => hasActiveCommand ? activeCommand.Type : null;

    private void Awake()
    {
        TryGetComponent(out behaviorController);
        TryGetComponent(out victimNpc);
    }

    private void OnValidate()
    {
        minReactionDelay = Mathf.Max(0f, minReactionDelay);
        maxExtraReactionDelay = Mathf.Max(0f, maxExtraReactionDelay);
    }

    private void Update()
    {
        if (!Application.isPlaying || !hasPendingCommand)
        {
            return;
        }

        if (Time.time < pendingCommand.ActivationTime)
        {
            return;
        }

        ActivatePendingCommand();
    }

    public void ReceiveCommand(NPCCommandType type, Vector3? targetPosition = null)
    {
        if (behaviorController == null)
        {
            Debug.LogWarning($"NPC {name}: command {type} ignored because NPCBehaviorController is missing.", this);
            return;
        }

        if (behaviorController.CurrentState == NPCState.Evacuated
            || behaviorController.CurrentState == NPCState.Incapacitated
            || behaviorController.CurrentState == NPCState.Chaotic)
        {
            Debug.Log($"NPC {name}: command {type} ignored in state {behaviorController.CurrentState}.", this);
            return;
        }

        var reactionDelay = CalculateReactionDelay();
        pendingCommand = new NPCCommandModel
        {
            Type = type,
            HasTargetPosition = targetPosition.HasValue,
            TargetPosition = targetPosition ?? Vector3.zero,
            ReceivedAtTime = Time.time,
            ActivationTime = Time.time + reactionDelay
        };

        // Повторная команда заменяет ожидающую, чтобы эффект не накапливался бесконечно.
        hasPendingCommand = true;

        Debug.Log(
            $"NPC {name}: received command {type}, reaction delay {reactionDelay:F2}s, activates at {pendingCommand.ActivationTime:F2}.",
            this);
    }

    [ContextMenu("Receive Debug Command")]
    public void ReceiveDebugCommand()
    {
        var targetPosition = debugTargetTransform != null ? debugTargetTransform.position : (Vector3?)null;
        ReceiveCommand(debugCommandType, targetPosition);
    }

    private float CalculateReactionDelay()
    {
        var commandReactionDelay = victimNpc != null && victimNpc.Parameters != null
            ? victimNpc.Parameters.CommandReactionDelay
            : 0f;

        // Задержка реакции берётся из базового минимума и коэффициента CommandReactionDelay у NPC.
        return minReactionDelay + Mathf.Clamp01(commandReactionDelay) * maxExtraReactionDelay;
    }

    private void ActivatePendingCommand()
    {
        activeCommand = pendingCommand;
        hasActiveCommand = true;
        hasPendingCommand = false;

        ApplyCommand(activeCommand);

        Debug.Log(
            $"NPC {name}: activated command {activeCommand.Type} after {(activeCommand.ActivationTime - activeCommand.ReceivedAtTime):F2}s.",
            this);
    }

    private void ApplyCommand(NPCCommandModel command)
    {
        Vector3? targetPosition = command.HasTargetPosition ? command.TargetPosition : null;

        if (command.Type == NPCCommandType.GoThere && !targetPosition.HasValue)
        {
            Debug.LogWarning($"NPC {name}: GoThere command has no target position.", this);
        }

        // После задержки команда становится постоянным локальным модификатором выбора точек для этого NPC.
        behaviorController.ApplyCommandOverride(command.Type, targetPosition);
    }
}
