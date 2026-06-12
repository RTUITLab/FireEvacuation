using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerSpeedModifier : MonoBehaviour
{
    [SerializeField] private MonoBehaviour movementProvider;
    [SerializeField] [Min(0.05f)] private float minimumAllowedMoveSpeed = 0.1f;

    private readonly Dictionary<Object, float> activeMultipliers = new();

    private PropertyInfo moveSpeedProperty;
    private FieldInfo moveSpeedField;
    private float baseMoveSpeed = 1f;
    private bool hasCapturedBaseMoveSpeed;
    private bool hasWarnedMissingProvider;
    private bool hasWarnedMissingAccessor;

    public float EffectiveMultiplier => GetEffectiveMultiplier();

    private void Awake()
    {
        TryResolveMovementProvider();
    }

    private void OnDisable()
    {
        RestoreBaseMoveSpeed();
        activeMultipliers.Clear();
    }

    public bool ApplyModifier(Object source, float speedMultiplier)
    {
        if (source == null)
        {
            return false;
        }

        if (!TryResolveMovementProvider())
        {
            return false;
        }

        if (!hasCapturedBaseMoveSpeed)
        {
            baseMoveSpeed = ReadMoveSpeed();
            hasCapturedBaseMoveSpeed = true;
        }

        activeMultipliers[source] = Mathf.Clamp(speedMultiplier, 0.01f, 1f);
        ApplyEffectiveMoveSpeed();
        return true;
    }

    public void RemoveModifier(Object source)
    {
        if (source == null || !activeMultipliers.Remove(source))
        {
            return;
        }

        if (activeMultipliers.Count == 0)
        {
            RestoreBaseMoveSpeed();
            return;
        }

        if (TryResolveMovementProvider())
        {
            ApplyEffectiveMoveSpeed();
        }
    }

    public static PlayerSpeedModifier GetOrCreateInScene()
    {
        var existingModifier = FindAnyObjectByType<PlayerSpeedModifier>(FindObjectsInactive.Include);
        if (existingModifier != null)
        {
            return existingModifier;
        }

        var movementProviderInScene = FindBestMovementProvider();
        if (movementProviderInScene == null)
        {
            return null;
        }

        var createdModifier = movementProviderInScene.gameObject.AddComponent<PlayerSpeedModifier>();
        createdModifier.SetMovementProvider(movementProviderInScene);
        return createdModifier;
    }

    private void SetMovementProvider(MonoBehaviour provider)
    {
        movementProvider = provider;
        moveSpeedProperty = null;
        moveSpeedField = null;
        hasCapturedBaseMoveSpeed = false;
    }

    private bool TryResolveMovementProvider()
    {
        if (movementProvider == null)
        {
            movementProvider = FindBestMovementProvider();
        }

        if (movementProvider == null)
        {
            if (!hasWarnedMissingProvider)
            {
                Debug.LogWarning("PlayerSpeedModifier could not find an XR movement provider automatically. Assign one in the inspector.", this);
                hasWarnedMissingProvider = true;
            }

            return false;
        }

        if (HasMoveSpeedAccessor(movementProvider.GetType()))
        {
            CacheMoveSpeedAccessor(movementProvider.GetType());
            return true;
        }

        if (!hasWarnedMissingAccessor)
        {
            Debug.LogWarning($"PlayerSpeedModifier found '{movementProvider.GetType().Name}', but it does not expose a writable move speed. Assign a compatible provider manually.", this);
            hasWarnedMissingAccessor = true;
        }

        return false;
    }

    private void ApplyEffectiveMoveSpeed()
    {
        var targetMoveSpeed = Mathf.Max(minimumAllowedMoveSpeed, baseMoveSpeed * GetEffectiveMultiplier());
        WriteMoveSpeed(targetMoveSpeed);
    }

    private void RestoreBaseMoveSpeed()
    {
        if (!hasCapturedBaseMoveSpeed || movementProvider == null)
        {
            return;
        }

        if (TryResolveMovementProvider())
        {
            WriteMoveSpeed(baseMoveSpeed);
        }

        hasCapturedBaseMoveSpeed = false;
    }

    private float GetEffectiveMultiplier()
    {
        var multiplier = 1f;
        foreach (var activeMultiplier in activeMultipliers.Values)
        {
            multiplier = Mathf.Min(multiplier, activeMultiplier);
        }

        return multiplier;
    }

    private float ReadMoveSpeed()
    {
        if (moveSpeedProperty != null)
        {
            return (float)moveSpeedProperty.GetValue(movementProvider);
        }

        return (float)moveSpeedField.GetValue(movementProvider);
    }

    private void WriteMoveSpeed(float moveSpeed)
    {
        if (moveSpeedProperty != null)
        {
            moveSpeedProperty.SetValue(movementProvider, moveSpeed);
            return;
        }

        moveSpeedField.SetValue(movementProvider, moveSpeed);
    }

    private void CacheMoveSpeedAccessor(System.Type providerType)
    {
        if (moveSpeedProperty != null || moveSpeedField != null)
        {
            return;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var candidateProperty = providerType.GetProperty("moveSpeed", flags);
        if (candidateProperty != null &&
            candidateProperty.PropertyType == typeof(float) &&
            candidateProperty.CanRead &&
            candidateProperty.CanWrite)
        {
            moveSpeedProperty = candidateProperty;
            return;
        }

        var candidateField = providerType.GetField("m_MoveSpeed", flags) ?? providerType.GetField("moveSpeed", flags);
        if (candidateField != null && candidateField.FieldType == typeof(float))
        {
            moveSpeedField = candidateField;
        }
    }

    private static bool HasMoveSpeedAccessor(System.Type providerType)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var candidateProperty = providerType.GetProperty("moveSpeed", flags);
        if (candidateProperty != null &&
            candidateProperty.PropertyType == typeof(float) &&
            candidateProperty.CanRead &&
            candidateProperty.CanWrite)
        {
            return true;
        }

        var candidateField = providerType.GetField("m_MoveSpeed", flags) ?? providerType.GetField("moveSpeed", flags);
        return candidateField != null && candidateField.FieldType == typeof(float);
    }

    private static MonoBehaviour FindBestMovementProvider()
    {
        MonoBehaviour fallbackCandidate = null;
        var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
        for (var index = 0; index < behaviours.Length; index++)
        {
            var candidate = behaviours[index];
            if (candidate == null)
            {
                continue;
            }

            var candidateType = candidate.GetType();
            if (!HasMoveSpeedAccessor(candidateType))
            {
                continue;
            }

            var typeName = candidateType.Name;
            var typeNamespace = candidateType.Namespace ?? string.Empty;
            if (typeName.Contains("MoveProvider") ||
                typeName.Contains("ContinuousMoveProvider") ||
                typeNamespace.Contains("XR.Interaction.Toolkit"))
            {
                return candidate;
            }

            fallbackCandidate ??= candidate;
        }

        return fallbackCandidate;
    }
}
