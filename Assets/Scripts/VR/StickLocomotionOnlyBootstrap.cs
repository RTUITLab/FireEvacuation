using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

public static class StickLocomotionOnlyBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void DisableGrabMoveProviders()
    {
        DisableProvidersOfType<GrabMoveProvider>();
        DisableProvidersOfType<TwoHandedGrabMoveProvider>();
    }

    private static void DisableProvidersOfType<T>() where T : Behaviour
    {
        var providers = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var index = 0; index < providers.Length; index++)
        {
            var provider = providers[index];
            if (provider == null || !provider.enabled)
            {
                continue;
            }

            provider.enabled = false;
        }
    }
}
