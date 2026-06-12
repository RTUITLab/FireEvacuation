using UnityEngine;

[DisallowMultipleComponent]
public class ExitPoint : MonoBehaviour
{
    [SerializeField] private string exitId = "Exit";
    [SerializeField] private bool recalculateOnStart = true;

    public string ExitId => exitId;

    private void Start()
    {
        if (!recalculateOnStart)
        {
            return;
        }

        RecalculateSceneProbePoints();
    }

    [ContextMenu("Recalculate Scene Probe Points")]
    public void RecalculateSceneProbePoints()
    {
        ExitProximityCalculator.RecalculateSceneExitProximity();
    }
}
