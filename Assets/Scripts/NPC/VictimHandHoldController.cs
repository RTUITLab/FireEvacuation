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
    private Transform activeHandTransform;
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
        if (!isHeld || activeHandTransform == null || victimNpc.IsRescued)
        {
            return;
        }

        var handPosition = activeHandTransform.position;
        var planarHandPosition = new Vector3(handPosition.x, lockedGroundY, handPosition.z);
        var currentGripPoint = body.position + localGripOffset;
        currentGripPoint.y = lockedGroundY;

        var deltaToHand = planarHandPosition - currentGripPoint;
        var currentDistance = deltaToHand.magnitude;
        if (currentDistance <= leashSlackDistance)
        {
            KeepUpright();
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
            maxHandHoldSpeed,
            Time.fixedDeltaTime);

        nextPosition.y = lockedGroundY;
        body.MovePosition(nextPosition);
        KeepUpright();
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

        var gripStartPosition = gripCollider.ClosestPoint(attachTransform.position);
        if (Vector3.Distance(gripStartPosition, attachTransform.position) > maxGripDistance)
        {
            ForceDeselect(args.interactorObject);
            return;
        }

        activeHandTransform = attachTransform;
        lockedGroundY = body.position.y;
        localGripOffset = transform.InverseTransformPoint(gripStartPosition);
        localGripOffset.y = 0f;
        smoothingVelocity = Vector3.zero;
        isHeld = true;

        victimNpc.SetState(VictimNPC.VictimState.Dragged);
        ApplyPlayerSpeedModifier();
        UpdateFeedbackColor();
        Debug.Log($"Victim hold started for '{victimNpc.NpcId}'.", this);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        EndHold(true);
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
        activeHandTransform = null;
        smoothingVelocity = Vector3.zero;

        if (restoreIdleState && !victimNpc.IsRescued)
        {
            victimNpc.SetState(VictimNPC.VictimState.Idle);
        }

        UpdateFeedbackColor();
        Debug.Log($"Victim hold ended for '{victimNpc.NpcId}'.", this);
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
                Debug.LogWarning("VictimHandHoldController could not find PlayerSpeedModifier or XR movement provider. Assign PlayerSpeedModifier manually if slowdown is required.", this);
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

    private void KeepUpright()
    {
        var currentEulerAngles = body.rotation.eulerAngles;
        body.MoveRotation(Quaternion.Euler(0f, currentEulerAngles.y, 0f));
    }

    private void UpdateFeedbackColor()
    {
        if (victimNpc != null && victimNpc.IsRescued)
        {
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
}
