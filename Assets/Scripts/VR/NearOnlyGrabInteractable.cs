using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
public class NearOnlyGrabInteractable : XRGrabInteractable
{
    [SerializeField] private float maxSelectDistance = 0.85f;

    public override bool IsHoverableBy(IXRHoverInteractor interactor)
    {
        return base.IsHoverableBy(interactor) && IsNearInteractor(interactor);
    }

    public override bool IsSelectableBy(IXRSelectInteractor interactor)
    {
        return base.IsSelectableBy(interactor) && IsNearInteractor(interactor);
    }

    private bool IsNearInteractor(object interactorObject)
    {
        if (interactorObject is XRRayInteractor)
        {
            return false;
        }

        if (interactorObject is not IXRInteractor interactor)
        {
            return false;
        }

        var attachTransform = interactor.GetAttachTransform(this);
        if (attachTransform == null && interactorObject is Component interactorComponent)
        {
            attachTransform = interactorComponent.transform;
        }

        if (attachTransform == null)
        {
            return false;
        }

        var closestPoint = GetClosestPoint(attachTransform.position);
        return Vector3.Distance(closestPoint, attachTransform.position) <= maxSelectDistance;
    }

    private Vector3 GetClosestPoint(Vector3 worldPoint)
    {
        if (colliders == null || colliders.Count == 0)
        {
            return transform.position;
        }

        var closestPoint = transform.position;
        var closestDistance = float.MaxValue;

        for (var index = 0; index < colliders.Count; index++)
        {
            var candidateCollider = colliders[index];
            if (candidateCollider == null || !candidateCollider.enabled)
            {
                continue;
            }

            var candidatePoint = candidateCollider.ClosestPoint(worldPoint);
            var candidateDistance = (candidatePoint - worldPoint).sqrMagnitude;
            if (candidateDistance < closestDistance)
            {
                closestDistance = candidateDistance;
                closestPoint = candidatePoint;
            }
        }

        return closestPoint;
    }
}
