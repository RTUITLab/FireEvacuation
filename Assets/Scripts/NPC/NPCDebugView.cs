using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NPCBehaviorController))]
public class NPCDebugView : MonoBehaviour
{
    [Header("Colors")]
    [SerializeField] private Color idleColor = new Color(0.82f, 0.82f, 0.82f, 1f);
    [SerializeField] private Color moveToPointColor = new Color(0.26f, 0.69f, 0.98f, 1f);
    [SerializeField] private Color followPlayerColor = new Color(0.24f, 0.85f, 0.32f, 1f);
    [SerializeField] private Color assistedByPlayerColor = new Color(1f, 0.66f, 0.18f, 1f);
    [SerializeField] private Color chaoticColor = new Color(0.92f, 0.26f, 0.2f, 1f);
    [SerializeField] private Color incapacitatedColor = new Color(0.56f, 0.34f, 0.18f, 1f);
    [SerializeField] private Color evacuatedColor = new Color(0.18f, 0.94f, 0.78f, 1f);
    [Header("Label")]
    [SerializeField] private bool createWorldLabel = true;
    [SerializeField] private GameObject labelPrefab;
    [SerializeField] private Vector3 labelOffset = new Vector3(0f, 1.4f, 0f);

    private NPCBehaviorController behaviorController;
    private Renderer[] targetRenderers;
    private TMP_Text labelText;
    private Transform labelTransform;

    private void Awake()
    {
        behaviorController = GetComponent<NPCBehaviorController>();
        targetRenderers = GetComponentsInChildren<Renderer>(true);
        TryCreateWorldLabel();
        RefreshView();
    }

    private void OnEnable()
    {
        if (behaviorController != null)
        {
            behaviorController.StateChanged += OnStateChanged;
        }

        RefreshView();
    }

    private void OnDisable()
    {
        if (behaviorController != null)
        {
            behaviorController.StateChanged -= OnStateChanged;
        }
    }

    private void LateUpdate()
    {
        if (labelTransform == null)
        {
            return;
        }

        labelTransform.position = transform.position + labelOffset;
    }

    private void OnStateChanged(NPCState oldState, NPCState newState)
    {
        // При смене состояния сразу обновляем цвет и текст отладочного отображения.
        RefreshView();
    }

    private void RefreshView()
    {
        if (behaviorController == null)
        {
            return;
        }

        var stateColor = GetStateColor(behaviorController.CurrentState);
        ApplyColor(stateColor);
        UpdateLabelText(behaviorController.CurrentState);
    }

    private void ApplyColor(Color targetColor)
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            targetRenderers = GetComponentsInChildren<Renderer>(true);
        }

        for (var index = 0; index < targetRenderers.Length; index++)
        {
            var rendererComponent = targetRenderers[index];
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
                material.SetColor("_BaseColor", targetColor);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", targetColor);
            }
        }
    }

    private void UpdateLabelText(NPCState state)
    {
        if (labelText == null)
        {
            return;
        }

        labelText.text = $"NPC {name}\n{state}";
    }

    private void TryCreateWorldLabel()
    {
        if (!createWorldLabel || labelPrefab == null)
        {
            return;
        }

        if (labelTransform != null)
        {
            return;
        }

        // Лейбл создаётся из prefab, чтобы его размер и внешний вид можно было настраивать в инспекторе.
        var labelObject = Instantiate(labelPrefab, transform);
        labelObject.name = $"{labelPrefab.name} (Runtime)";
        labelTransform = labelObject.transform;
        labelTransform.localPosition = labelOffset;
        labelTransform.localRotation = Quaternion.identity;

        labelText = labelObject.GetComponentInChildren<TMP_Text>(true);
    }

    private Color GetStateColor(NPCState state)
    {
        return state switch
        {
            NPCState.Idle => idleColor,
            NPCState.MoveToPoint => moveToPointColor,
            NPCState.FollowPlayer => followPlayerColor,
            NPCState.AssistedByPlayer => assistedByPlayerColor,
            NPCState.Chaotic => chaoticColor,
            NPCState.Incapacitated => incapacitatedColor,
            NPCState.Evacuated => evacuatedColor,
            _ => idleColor
        };
    }
}
