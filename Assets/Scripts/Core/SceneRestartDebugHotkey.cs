using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

[DisallowMultipleComponent]
public class SceneRestartDebugHotkey : MonoBehaviour
{
    private enum ControllerButton
    {
        PrimaryButton = 0,
        SecondaryButton = 1,
        MenuButton = 2,
        TriggerButton = 3,
        GripButton = 4
    }

    [Header("Режим работы")]
    [SerializeField] private bool debugRestartEnabled = true;
    [SerializeField] private bool allowInReleaseBuild = true;

    [Header("Источник ввода")]
    [SerializeField] private InputActionReference restartAction;
    [SerializeField] private XRNode controllerNode = XRNode.RightHand;
    [SerializeField] private ControllerButton controllerButton = ControllerButton.SecondaryButton;

    [Header("Защита от случайного перезапуска")]
    [SerializeField] private float holdDuration = 0.75f;
    [SerializeField] private float restartCooldown = 1f;

    private float holdTimer;
    private float cooldownTimer;
    private bool wasPressedLastFrame;

    private void Start()
    {
        if (!debugRestartEnabled)
        {
            Debug.Log("SceneRestartDebugHotkey is disabled in Inspector.", this);
            return;
        }

        if (!allowInReleaseBuild && !Application.isEditor && !Debug.isDebugBuild)
        {
            Debug.LogWarning("SceneRestartDebugHotkey is disabled in non-development build. Enable 'Allow In Release Build' to use restart on device.", this);
        }

        if (restartAction == null && controllerNode == XRNode.LeftHand && controllerButton == ControllerButton.MenuButton)
        {
            Debug.LogWarning("SceneRestartDebugHotkey uses LeftHand + MenuButton. On many XR devices this button is unavailable. Prefer RightHand + SecondaryButton or assign an Input Action.", this);
        }
    }

    private void OnEnable()
    {
        // Включаем action только для debug-инструмента, если он назначен через Input System.
        if (restartAction != null && restartAction.action != null)
        {
            restartAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (restartAction != null && restartAction.action != null)
        {
            restartAction.action.Disable();
        }
    }

    private void Update()
    {
        // В релизной сборке инструмент по умолчанию не активен.
        if (!debugRestartEnabled || (!allowInReleaseBuild && !Application.isEditor && !Debug.isDebugBuild))
        {
            ResetHoldState();
            return;
        }

        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.unscaledDeltaTime;
        }

        var isPressed = ReadRestartButtonPressed();
        if (!isPressed)
        {
            ResetHoldState();
            return;
        }

        holdTimer = wasPressedLastFrame ? holdTimer + Time.unscaledDeltaTime : 0f;
        wasPressedLastFrame = true;

        // Требуем удержание, чтобы не ломать обычное VR-управление случайным нажатием.
        if (holdTimer < holdDuration || cooldownTimer > 0f)
        {
            return;
        }

        RestartCurrentScene();
    }

    private bool ReadRestartButtonPressed()
    {
        // Если action назначен в инспекторе, используем его как основной источник.
        if (restartAction != null && restartAction.action != null)
        {
            return restartAction.action.IsPressed();
        }

        // Иначе читаем состояние напрямую с XR-контроллера.
        var inputDevice = InputDevices.GetDeviceAtXRNode(controllerNode);
        if (!inputDevice.isValid)
        {
            return false;
        }

        return controllerButton switch
        {
            ControllerButton.PrimaryButton => ReadBooleanFeature(inputDevice, UnityEngine.XR.CommonUsages.primaryButton),
            ControllerButton.SecondaryButton => ReadBooleanFeature(inputDevice, UnityEngine.XR.CommonUsages.secondaryButton),
            ControllerButton.MenuButton => ReadBooleanFeature(inputDevice, UnityEngine.XR.CommonUsages.menuButton),
            ControllerButton.TriggerButton => ReadBooleanFeature(inputDevice, UnityEngine.XR.CommonUsages.triggerButton),
            ControllerButton.GripButton => ReadBooleanFeature(inputDevice, UnityEngine.XR.CommonUsages.gripButton),
            _ => false
        };
    }

    private static bool ReadBooleanFeature(UnityEngine.XR.InputDevice inputDevice, InputFeatureUsage<bool> feature)
    {
        return inputDevice.TryGetFeatureValue(feature, out var isPressed) && isPressed;
    }

    private void RestartCurrentScene()
    {
        var activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            return;
        }

        // Перед debug-перезапуском явно завершаем текущий прогон.
        // Если сценарий уже завершён, менеджер повторно лог не создаст.
        var scenarioManager = ScenarioManager.FindInScene();
        if (scenarioManager != null)
        {
            scenarioManager.FinishScenario("manual_stop");
        }
        else
        {
            Debug.LogWarning("SceneRestartDebugHotkey did not find ScenarioManager before restart.", this);
        }

        Debug.Log($"SceneRestartDebugHotkey restarting scene '{activeScene.name}'.", this);

        cooldownTimer = restartCooldown;
        ResetHoldState();
        SceneManager.LoadScene(activeScene.buildIndex);
    }

    private void ResetHoldState()
    {
        holdTimer = 0f;
        wasPressedLastFrame = false;
    }
}

