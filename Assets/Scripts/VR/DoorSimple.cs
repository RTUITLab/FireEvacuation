using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class DoorSimple : XRBaseInteractable
{
    [Header("Ссылки")]
    [SerializeField] private Transform doorVisual;

    [Header("Ограничения поворота")]
    [SerializeField] private float closedAngle = 0f;
    [SerializeField] private float minAngle = 0f;
    [SerializeField] private float maxAngle = 95f;

    [Header("Управление рукой")]
    [SerializeField] private float maxGrabDistance = 0.2f;
    [SerializeField] private float releaseDistance = 0.35f;
    [SerializeField] private float grabDeadZoneRadius = 0.08f;
    [SerializeField] private float handAngleMultiplier = 1f;
    [SerializeField] private bool invertHandDirection;

    [Header("Скорость двери")]
    [SerializeField] private float maxDegreesPerSecond = 140f;
    [SerializeField] private float closeSnapThreshold = 2f;

    [Header("Проверка препятствий")]
    [SerializeField] private LayerMask blockingLayers = ~0;
    [SerializeField] private float collisionPadding = 0.01f;
    [SerializeField] private Collider[] ignoredColliders;

    [Header("Опции")]
    [SerializeField] private bool disableColliderWhenFullyOpen;

    private const int OverlapBufferSize = 16;

    private readonly Collider[] overlapHits = new Collider[OverlapBufferSize];

    private Collider doorCollider;
    private Transform activeInteractorTransform;
    private Component activeInteractorComponent;
    private float currentAngle;
    private float grabStartInteractorAngle;
    private float grabStartDoorAngle;

    private Transform DoorTransform => doorVisual != null ? doorVisual : transform;

    public float CurrentAngle => currentAngle;
    public bool IsOpen => Mathf.Abs(Mathf.DeltaAngle(currentAngle, maxAngle)) < 0.01f;

    protected override void Awake()
    {
        base.Awake();
        doorCollider = GetComponent<Collider>();
        currentAngle = NormalizeAngle(closedAngle);
        ApplyAngleInstant(currentAngle);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
    }

    protected override void OnDisable()
    {
        activeInteractorTransform = null;
        activeInteractorComponent = null;
        base.OnDisable();
    }

    private void OnValidate()
    {
        maxGrabDistance = Mathf.Max(0.01f, maxGrabDistance);
        releaseDistance = Mathf.Max(maxGrabDistance, releaseDistance);
        grabDeadZoneRadius = Mathf.Max(0.01f, grabDeadZoneRadius);
        maxDegreesPerSecond = Mathf.Max(1f, maxDegreesPerSecond);
        closeSnapThreshold = Mathf.Max(0f, closeSnapThreshold);
        handAngleMultiplier = Mathf.Max(0.01f, handAngleMultiplier);
        collisionPadding = Mathf.Clamp(collisionPadding, 0f, 0.05f);

        if (!Application.isPlaying)
        {
            currentAngle = ClampDoorAngle(currentAngle == 0f ? closedAngle : currentAngle);
            ApplyAngleInstant(currentAngle);
        }
    }

    public override bool IsHoverableBy(IXRHoverInteractor interactor)
    {
        return base.IsHoverableBy(interactor) && IsInteractorAllowed(interactor);
    }

    public override bool IsSelectableBy(IXRSelectInteractor interactor)
    {
        return base.IsSelectableBy(interactor) && IsInteractorAllowed(interactor);
    }

    public override Transform GetAttachTransform(IXRInteractor interactor)
    {
        return DoorTransform;
    }

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
    {
        base.ProcessInteractable(updatePhase);

        if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic || !isSelected)
        {
            return;
        }

        if (activeInteractorTransform == null)
        {
            return;
        }

        if (!IsStillWithinReach(activeInteractorTransform.position))
        {
            ForceDeselect(firstInteractorSelecting as IXRSelectInteractor);
            return;
        }

        var desiredAngle = CalculateDesiredAngle(activeInteractorTransform.position);
        MoveDoorTowards(desiredAngle, Time.deltaTime);
    }

    [ContextMenu("Открыть дверь")]
    public void OpenDoor()
    {
        MoveDoorTowards(maxAngle, 999f);
    }

    [ContextMenu("Закрыть дверь")]
    public void CloseDoor()
    {
        MoveDoorTowards(closedAngle, 999f);
    }

    [ContextMenu("Переключить дверь")]
    public void ToggleDoor()
    {
        var targetAngle = Mathf.Abs(Mathf.DeltaAngle(currentAngle, closedAngle)) <= Mathf.Abs(Mathf.DeltaAngle(currentAngle, maxAngle))
            ? maxAngle
            : closedAngle;

        MoveDoorTowards(targetAngle, 999f);
    }

    [ContextMenu("Выключить коллайдер двери")]
    public void DisableDoorCollider()
    {
        SetDoorColliderEnabled(false);
    }

    [ContextMenu("Включить коллайдер двери")]
    public void EnableDoorCollider()
    {
        SetDoorColliderEnabled(true);
    }

    public void SetDoorColliderEnabled(bool enabled)
    {
        if (doorCollider != null)
        {
            doorCollider.enabled = enabled;
        }
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);

        activeInteractorComponent = args.interactorObject as Component;
        activeInteractorTransform = ResolveInteractorTransform(args.interactorObject);
        if (activeInteractorTransform == null)
        {
            ForceDeselect(args.interactorObject);
            return;
        }

        if (!IsStillWithinReach(activeInteractorTransform.position))
        {
            ForceDeselect(args.interactorObject);
            return;
        }

        grabStartInteractorAngle = GetInteractorAngle(activeInteractorTransform.position);
        grabStartDoorAngle = currentAngle;
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);

        activeInteractorTransform = null;
        activeInteractorComponent = null;

        if (Mathf.Abs(Mathf.DeltaAngle(currentAngle, closedAngle)) <= closeSnapThreshold)
        {
            ApplyAngleInstant(closedAngle);
        }
    }

    private bool IsInteractorAllowed(IXRInteractor interactor)
    {
        if (interactor is XRRayInteractor)
        {
            return false;
        }

        var interactorTransform = ResolveInteractorTransform(interactor);
        if (interactorTransform == null)
        {
            return false;
        }

        return IsStillWithinGrabDistance(interactorTransform.position);
    }

    private Transform ResolveInteractorTransform(object interactorObject)
    {
        if (interactorObject is IXRInteractor interactor)
        {
            var attachTransform = interactor.GetAttachTransform(this);
            if (attachTransform != null)
            {
                return attachTransform;
            }
        }

        return interactorObject as Component != null ? ((Component)interactorObject).transform : null;
    }

    private bool IsStillWithinGrabDistance(Vector3 worldPosition)
    {
        if (doorCollider == null || !doorCollider.enabled)
        {
            return false;
        }

        var closestPoint = doorCollider.ClosestPoint(worldPosition);
        return Vector3.Distance(closestPoint, worldPosition) <= maxGrabDistance;
    }

    private bool IsStillWithinReach(Vector3 worldPosition)
    {
        if (doorCollider == null || !doorCollider.enabled)
        {
            return false;
        }

        var closestPoint = doorCollider.ClosestPoint(worldPosition);
        return Vector3.Distance(closestPoint, worldPosition) <= releaseDistance;
    }

    private float CalculateDesiredAngle(Vector3 handWorldPosition)
    {
        var currentInteractorAngle = GetInteractorAngle(handWorldPosition);
        var delta = Mathf.DeltaAngle(grabStartInteractorAngle, currentInteractorAngle);
        if (invertHandDirection)
        {
            delta = -delta;
        }

        var targetAngle = grabStartDoorAngle + delta * handAngleMultiplier;
        return ClampDoorAngle(targetAngle);
    }

    private float GetInteractorAngle(Vector3 handWorldPosition)
    {
        var door = DoorTransform;
        var localOffset = door.InverseTransformPoint(handWorldPosition);
        localOffset.y = 0f;

        if (localOffset.sqrMagnitude < grabDeadZoneRadius * grabDeadZoneRadius)
        {
            return grabStartInteractorAngle;
        }

        return Mathf.Atan2(localOffset.x, localOffset.z) * Mathf.Rad2Deg;
    }

    private void MoveDoorTowards(float targetAngle, float deltaTime)
    {
        targetAngle = ClampDoorAngle(targetAngle);
        var maxStep = maxDegreesPerSecond * Mathf.Max(deltaTime, Time.deltaTime);
        var nextAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, maxStep);

        if (Mathf.Approximately(nextAngle, currentAngle))
        {
            return;
        }

        if (!CanRotateToAngle(nextAngle))
        {
            return;
        }

        ApplyAngleInstant(nextAngle);
    }

    private bool CanRotateToAngle(float targetAngle)
    {
        if (doorCollider == null || !doorCollider.enabled)
        {
            return true;
        }

        if (doorCollider is BoxCollider boxCollider)
        {
            return !HasBlockingOverlapBox(boxCollider, targetAngle);
        }

        if (doorCollider is SphereCollider sphereCollider)
        {
            return !HasBlockingOverlapSphere(sphereCollider, targetAngle);
        }

        if (doorCollider is CapsuleCollider capsuleCollider)
        {
            return !HasBlockingOverlapCapsule(capsuleCollider, targetAngle);
        }

        return !HasBlockingOverlapBounds(targetAngle);
    }

    private bool HasBlockingOverlapBox(BoxCollider boxCollider, float targetAngle)
    {
        var worldRotation = GetWorldRotation(targetAngle);
        var scaledCenter = Vector3.Scale(boxCollider.center, AbsVector(DoorTransform.lossyScale));
        var worldCenter = DoorTransform.position + worldRotation * scaledCenter;
        var halfExtents = Vector3.Scale(boxCollider.size * 0.5f, AbsVector(DoorTransform.lossyScale));
        halfExtents = Vector3.Max(Vector3.zero, halfExtents - Vector3.one * collisionPadding);

        var hitCount = Physics.OverlapBoxNonAlloc(
            worldCenter,
            halfExtents,
            overlapHits,
            worldRotation,
            blockingLayers,
            QueryTriggerInteraction.Ignore);

        return ContainsBlockingCollider(hitCount);
    }

    private bool HasBlockingOverlapSphere(SphereCollider sphereCollider, float targetAngle)
    {
        var worldRotation = GetWorldRotation(targetAngle);
        var scaledCenter = Vector3.Scale(sphereCollider.center, AbsVector(DoorTransform.lossyScale));
        var worldCenter = DoorTransform.position + worldRotation * scaledCenter;
        var maxScale = Mathf.Max(Mathf.Abs(DoorTransform.lossyScale.x), Mathf.Abs(DoorTransform.lossyScale.y), Mathf.Abs(DoorTransform.lossyScale.z));
        var radius = Mathf.Max(0f, sphereCollider.radius * maxScale - collisionPadding);

        var hitCount = Physics.OverlapSphereNonAlloc(
            worldCenter,
            radius,
            overlapHits,
            blockingLayers,
            QueryTriggerInteraction.Ignore);

        return ContainsBlockingCollider(hitCount);
    }

    private bool HasBlockingOverlapCapsule(CapsuleCollider capsuleCollider, float targetAngle)
    {
        var worldRotation = GetWorldRotation(targetAngle);
        var lossyScale = AbsVector(DoorTransform.lossyScale);
        var scaledCenter = Vector3.Scale(capsuleCollider.center, lossyScale);
        var worldCenter = DoorTransform.position + worldRotation * scaledCenter;

        var direction = GetCapsuleDirection(capsuleCollider.direction);
        direction = worldRotation * direction;

        var radiusScale = capsuleCollider.direction switch
        {
            0 => Mathf.Max(lossyScale.y, lossyScale.z),
            1 => Mathf.Max(lossyScale.x, lossyScale.z),
            _ => Mathf.Max(lossyScale.x, lossyScale.y)
        };

        var heightScale = capsuleCollider.direction switch
        {
            0 => lossyScale.x,
            1 => lossyScale.y,
            _ => lossyScale.z
        };

        var radius = Mathf.Max(0f, capsuleCollider.radius * radiusScale - collisionPadding);
        var halfHeight = Mathf.Max(radius, capsuleCollider.height * heightScale * 0.5f);
        var segmentOffset = Mathf.Max(0f, halfHeight - radius);
        var pointA = worldCenter + direction * segmentOffset;
        var pointB = worldCenter - direction * segmentOffset;

        var hitCount = Physics.OverlapCapsuleNonAlloc(
            pointA,
            pointB,
            radius,
            overlapHits,
            blockingLayers,
            QueryTriggerInteraction.Ignore);

        return ContainsBlockingCollider(hitCount);
    }

    private bool HasBlockingOverlapBounds(float targetAngle)
    {
        var bounds = doorCollider.bounds;
        var hitCount = Physics.OverlapBoxNonAlloc(
            bounds.center,
            Vector3.Max(Vector3.zero, bounds.extents - Vector3.one * collisionPadding),
            overlapHits,
            GetWorldRotation(targetAngle),
            blockingLayers,
            QueryTriggerInteraction.Ignore);

        return ContainsBlockingCollider(hitCount);
    }

    private bool ContainsBlockingCollider(int hitCount)
    {
        for (var index = 0; index < hitCount; index++)
        {
            var hitCollider = overlapHits[index];
            overlapHits[index] = null;

            if (hitCollider == null || hitCollider == doorCollider)
            {
                continue;
            }

            if (hitCollider.transform.IsChildOf(DoorTransform))
            {
                continue;
            }

            if (IsIgnoredCollider(hitCollider))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool IsIgnoredCollider(Collider candidate)
    {
        if (candidate == null)
        {
            return true;
        }

        if (ignoredColliders != null)
        {
            for (var index = 0; index < ignoredColliders.Length; index++)
            {
                if (ignoredColliders[index] == candidate)
                {
                    return true;
                }
            }
        }

        if (activeInteractorComponent != null)
        {
            var interactorTransform = activeInteractorComponent.transform;
            if (candidate.transform == interactorTransform ||
                candidate.transform.IsChildOf(interactorTransform) ||
                interactorTransform.IsChildOf(candidate.transform))
            {
                return true;
            }
        }

        return false;
    }

    private Quaternion GetWorldRotation(float targetAngle)
    {
        var parentRotation = DoorTransform.parent != null ? DoorTransform.parent.rotation : Quaternion.identity;
        return parentRotation * Quaternion.Euler(0f, targetAngle, 0f);
    }

    private float ClampDoorAngle(float angle)
    {
        var lower = Mathf.Min(minAngle, maxAngle);
        var upper = Mathf.Max(minAngle, maxAngle);
        return Mathf.Clamp(angle, lower, upper);
    }

    private void ApplyAngleInstant(float targetAngle)
    {
        currentAngle = ClampDoorAngle(targetAngle);
        var localEuler = DoorTransform.localEulerAngles;
        localEuler.y = NormalizeAngle(currentAngle);
        DoorTransform.localEulerAngles = localEuler;

        if (disableColliderWhenFullyOpen && doorCollider != null)
        {
            doorCollider.enabled = Mathf.Abs(Mathf.DeltaAngle(currentAngle, maxAngle)) > 0.5f;
        }
    }

    private void ForceDeselect(IXRSelectInteractor interactor)
    {
        if (interactor == null || interactionManager == null)
        {
            return;
        }

        interactionManager.SelectExit(interactor, this);
    }

    private static Vector3 AbsVector(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private static Vector3 GetCapsuleDirection(int axis)
    {
        return axis switch
        {
            0 => Vector3.right,
            1 => Vector3.up,
            _ => Vector3.forward
        };
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle < 0f)
        {
            angle += 360f;
        }

        return angle;
    }
}
