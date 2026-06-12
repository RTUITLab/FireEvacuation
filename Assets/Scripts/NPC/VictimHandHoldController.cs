using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
[RequireComponent(typeof(VictimNPC))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(XRGrabInteractable))]
public class VictimHandHoldController : MonoBehaviour
{
    private const float DefaultPlayerSpeedMultiplierWhileHolding = 0.65f;
    private const float DefaultMovementSpeedInfluence = 0.35f;
    private const float MinimumPlayerSpeedMultiplier = 0.35f;
    private const int MinimumHandsForLostVictim = 2;
    private const float LostVictimMinimumSpeedMultiplier = 0.35f;

    [SerializeField] private float leashSlackDistance = 0.35f;
    [SerializeField] private float maxHandHoldSpeed = 1.6f;
    [SerializeField] private float maxGripDistance = 0.85f;
    [SerializeField] private float minDistanceToHand = 0.15f;
    [SerializeField] private float smoothingTime = 0.08f;
    [SerializeField] private float playerSpeedMultiplierWhileHolding = DefaultPlayerSpeedMultiplierWhileHolding;
    [SerializeField] private float movementSpeedInfluence = DefaultMovementSpeedInfluence;
    [SerializeField] private PlayerSpeedModifier playerSpeedModifier;
    [SerializeField] private Color idleColor = new Color(0.95f, 0.58f, 0.18f, 1f);
    [SerializeField] private Color hoverColor = new Color(1f, 0.82f, 0.28f, 1f);
    [SerializeField] private Color holdColor = new Color(1f, 0.33f, 0.16f, 1f);

    private VictimNPC victimNpc;
    private Rigidbody body;
    private Collider gripCollider;
    private XRGrabInteractable grabInteractable;
    private Renderer[] renderers;
    private readonly List<Transform> activeHandTransforms = new List<Transform>();
    private Vector3 localGripOffset;
    private Vector3 smoothingVelocity;
    private float lockedGroundY;
    private int hoverCount;
    private bool isHeld;
    private bool hasWarnedMissingPlayerSpeedModifier;

    public bool IsHeld => isHeld;

    private void Awake()
    {
        NormalizeSettings();
        victimNpc = GetComponent<VictimNPC>();
        body = GetComponent<Rigidbody>();
        gripCollider = GetComponent<Collider>();
        grabInteractable = GetComponent<XRGrabInteractable>();
        renderers = GetComponentsInChildren<Renderer>(true);

        ConfigurePhysics();
        UpdateFeedbackColor();
    }

    private void OnEnable()
    {
        grabInteractable.hoverEntered.AddListener(OnHoverEntered);
        grabInteractable.hoverExited.AddListener(OnHoverExited);
        grabInteractable.selectEntered.AddListener(OnSelectEntered);
        grabInteractable.selectExited.AddListener(OnSelectExited);
    }

    private void OnDisable()
    {
        grabInteractable.hoverEntered.RemoveListener(OnHoverEntered);
        grabInteractable.hoverExited.RemoveListener(OnHoverExited);
        grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
        grabInteractable.selectExited.RemoveListener(OnSelectExited);

        ForceRelease();
    }

    private void OnValidate()
    {
        NormalizeSettings();
    }

    private void FixedUpdate()
    {
        if (!isHeld || victimNpc.IsRescued)
        {
            return;
        }

        if (!TryGetReferenceHandPosition(out var referenceHandPosition))
        {
            KeepBodyPose();
            return;
        }

        if (victimNpc.IsLost && activeHandTransforms.Count < MinimumHandsForLostVictim)
        {
            KeepBodyPose();
            return;
        }

        var planarHandPosition = new Vector3(referenceHandPosition.x, lockedGroundY, referenceHandPosition.z);
        var currentGripPoint = body.position + localGripOffset;
        currentGripPoint.y = lockedGroundY;

        var deltaToHand = planarHandPosition - currentGripPoint;
        var currentDistance = deltaToHand.magnitude;
        if (currentDistance <= leashSlackDistance)
        {
            KeepBodyPose();
            return;
        }

        var desiredPosition = body.position + (deltaToHand / currentDistance) * (currentDistance - leashSlackDistance);
        desiredPosition.y = lockedGroundY;

        var planarGap = desiredPosition - planarHandPosition;
        planarGap.y = 0f;
        if (planarGap.sqrMagnitude < minDistanceToHand * minDistanceToHand)
        {
            if (planarGap.sqrMagnitude < 0.0001f)
            {
                planarGap = transform.forward.sqrMagnitude > 0.0001f ? transform.forward : Vector3.back;
                planarGap.y = 0f;
            }

            desiredPosition = planarHandPosition + planarGap.normalized * minDistanceToHand;
            desiredPosition.y = lockedGroundY;
        }

        var nextPosition = Vector3.SmoothDamp(
            body.position,
            desiredPosition,
            ref smoothingVelocity,
            smoothingTime,
            GetCurrentMaxHandHoldSpeed(),
            Time.fixedDeltaTime);

        nextPosition.y = lockedGroundY;
        body.MovePosition(nextPosition);
        KeepBodyPose();
    }

    public void ForceRelease()
    {
        EndHold(true);
    }

    private void ConfigurePhysics()
    {
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (victimNpc.IsRescued)
        {
            return;
        }

        hoverCount++;
        UpdateFeedbackColor();
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        hoverCount = Mathf.Max(0, hoverCount - 1);
        UpdateFeedbackColor();
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (victimNpc.IsRescued)
        {
            ForceDeselect(args.interactorObject);
            return;
        }

        var attachTransform = args.interactorObject.GetAttachTransform(grabInteractable);
        if (attachTransform == null && args.interactorObject is Component interactorComponent)
        {
            attachTransform = interactorComponent.transform;
        }

        if (attachTransform == null)
        {
            return;
        }

        if (!victimNpc.IsLost && activeHandTransforms.Count > 0)
        {
            ForceDeselect(args.interactorObject);
            return;
        }

        var gripStartPosition = gripCollider.ClosestPoint(attachTransform.position);
        if (Vector3.Distance(gripStartPosition, attachTransform.position) > maxGripDistance)
        {
            ForceDeselect(args.interactorObject);
            return;
        }

        if (!activeHandTransforms.Contains(attachTransform))
        {
            activeHandTransforms.Add(attachTransform);
        }

        lockedGroundY = body.position.y;
        RecalculateGripOffset();
        smoothingVelocity = Vector3.zero;
        isHeld = activeHandTransforms.Count > 0;

        victimNpc.SetState(VictimNPC.VictimState.Dragged);
        ApplyPlayerSpeedModifier();
        UpdateFeedbackColor();
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        var attachTransform = args.interactorObject.GetAttachTransform(grabInteractable);
        if (attachTransform == null && args.interactorObject is Component interactorComponent)
        {
            attachTransform = interactorComponent.transform;
        }

        if (attachTransform != null)
        {
            activeHandTransforms.Remove(attachTransform);
        }

        if (activeHandTransforms.Count == 0)
        {
            EndHold(true);
            return;
        }

        isHeld = true;
        lockedGroundY = body.position.y;
        RecalculateGripOffset();
        smoothingVelocity = Vector3.zero;
        ApplyPlayerSpeedModifier();
        UpdateFeedbackColor();
    }

    private void EndHold(bool restoreIdleState)
    {
        RemovePlayerSpeedModifier();

        if (!isHeld)
        {
            UpdateFeedbackColor();
            return;
        }

        isHeld = false;
        activeHandTransforms.Clear();
        smoothingVelocity = Vector3.zero;

        if (restoreIdleState && !victimNpc.IsRescued)
        {
            // После отпускания NPC возвращается в обычное состояние или остаётся недееспособным.
            victimNpc.SetState(victimNpc.IsLost ? VictimNPC.VictimState.Lost : VictimNPC.VictimState.Idle);
        }

        UpdateFeedbackColor();
      //  Debug.Log($"Victim hold ended for '{victimNpc.NpcId}'.", this);
    }

    private void NormalizeSettings()
    {
        if (playerSpeedMultiplierWhileHolding <= 0f)
        {
            playerSpeedMultiplierWhileHolding = DefaultPlayerSpeedMultiplierWhileHolding;
        }

        if (movementSpeedInfluence <= 0f)
        {
            movementSpeedInfluence = DefaultMovementSpeedInfluence;
        }

        leashSlackDistance = Mathf.Max(0f, leashSlackDistance);
        maxHandHoldSpeed = Mathf.Max(0.1f, maxHandHoldSpeed);
        maxGripDistance = Mathf.Max(0.05f, maxGripDistance);
        minDistanceToHand = Mathf.Max(0f, minDistanceToHand);
        smoothingTime = Mathf.Max(0.01f, smoothingTime);
        playerSpeedMultiplierWhileHolding = Mathf.Clamp(playerSpeedMultiplierWhileHolding, MinimumPlayerSpeedMultiplier, 1f);
        movementSpeedInfluence = Mathf.Clamp01(movementSpeedInfluence);
    }

    private void ApplyPlayerSpeedModifier()
    {
        var speedModifier = ResolvePlayerSpeedModifier();
        if (speedModifier == null)
        {
            if (!hasWarnedMissingPlayerSpeedModifier)
            {
             //   Debug.LogWarning("VictimHandHoldController could not find PlayerSpeedModifier or XR movement provider. Assign PlayerSpeedModifier manually if slowdown is required.", this);
                hasWarnedMissingPlayerSpeedModifier = true;
            }

            return;
        }

        speedModifier.ApplyModifier(this, GetEffectivePlayerSpeedMultiplier());
    }

    private void RemovePlayerSpeedModifier()
    {
        if (playerSpeedModifier == null)
        {
            return;
        }

        playerSpeedModifier.RemoveModifier(this);
    }

    private PlayerSpeedModifier ResolvePlayerSpeedModifier()
    {
        if (playerSpeedModifier != null)
        {
            return playerSpeedModifier;
        }

        playerSpeedModifier = PlayerSpeedModifier.GetOrCreateInScene();
        return playerSpeedModifier;
    }

    private float GetEffectivePlayerSpeedMultiplier()
    {
        if (victimNpc != null && victimNpc.IsLost)
        {
            var lostDragSpeedMultiplier = Mathf.Lerp(1f, LostVictimMinimumSpeedMultiplier, victimNpc.Weight);
            return Mathf.Clamp(lostDragSpeedMultiplier, MinimumPlayerSpeedMultiplier, 1f);
        }

        var influenceMultiplier = 1f - movementSpeedInfluence;
        return Mathf.Clamp(
            Mathf.Min(playerSpeedMultiplierWhileHolding, influenceMultiplier),
            MinimumPlayerSpeedMultiplier,
            1f);
    }

    private void ForceDeselect(IXRSelectInteractor interactor)
    {
        var interactionManager = grabInteractable.interactionManager;
        if (interactionManager != null)
        {
            interactionManager.SelectExit(interactor, grabInteractable);
        }
    }

    private void KeepBodyPose()
    {
        var currentEulerAngles = body.rotation.eulerAngles;
        var targetRotation = victimNpc != null && victimNpc.IsLost
            ? Quaternion.Euler(90f, currentEulerAngles.y, 0f)
            : Quaternion.Euler(0f, currentEulerAngles.y, 0f);

        body.MoveRotation(targetRotation);
    }

    private void UpdateFeedbackColor()
    {
        if (victimNpc != null && victimNpc.IsRescued)
        {
            victimNpc.RefreshStatusColor();
            return;
        }

        if (victimNpc != null && victimNpc.IsLost && !isHeld && hoverCount <= 0)
        {
            victimNpc.RefreshStatusColor();
            return;
        }

        if (victimNpc != null && victimNpc.IsCritical && !isHeld && hoverCount <= 0)
        {
            victimNpc.RefreshStatusColor();
            return;
        }

        var targetColor = idleColor;
        if (isHeld)
        {
            targetColor = holdColor;
        }
        else if (hoverCount > 0)
        {
            targetColor = hoverColor;
        }

        ApplyColor(targetColor);
    }

    private void ApplyColor(Color color)
    {
        if (renderers == null)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        for (var index = 0; index < renderers.Length; index++)
        {
            var rendererComponent = renderers[index];
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

    private bool TryGetReferenceHandPosition(out Vector3 referencePosition)
    {
        referencePosition = default;
        if (activeHandTransforms.Count == 0)
        {
            return false;
        }

        if (!victimNpc.IsLost)
        {
            referencePosition = activeHandTransforms[0].position;
            return true;
        }

        var validHands = 0;
        var accumulatedPosition = Vector3.zero;
        for (var index = 0; index < activeHandTransforms.Count; index++)
        {
            var handTransform = activeHandTransforms[index];
            if (handTransform == null)
            {
                continue;
            }

            accumulatedPosition += handTransform.position;
            validHands++;
        }

        if (validHands == 0)
        {
            return false;
        }

        referencePosition = accumulatedPosition / validHands;
        return true;
    }

    private void RecalculateGripOffset()
    {
        if (!TryGetReferenceHandPosition(out var referencePosition))
        {
            localGripOffset = Vector3.zero;
            return;
        }

        var gripStartPosition = gripCollider.ClosestPoint(referencePosition);
        localGripOffset = transform.InverseTransformPoint(gripStartPosition);
        localGripOffset.y = 0f;
    }

    private float GetCurrentMaxHandHoldSpeed()
    {
        if (victimNpc == null || !victimNpc.IsLost)
        {
            return maxHandHoldSpeed;
        }

        return Mathf.Max(0.1f, maxHandHoldSpeed * Mathf.Lerp(1f, LostVictimMinimumSpeedMultiplier, victimNpc.Weight));
    }
}
