using UnityEngine;
using UnityEngine.InputSystem;

namespace Overdraft.Player
{
    /// <summary>Which device the player touched most recently. Drives prompt glyphs only.</summary>
    public enum InputDeviceKind
    {
        KeyboardMouse = 0,
        Gamepad = 1
    }

    /// <summary>
    /// Reads the Gameplay action map in Update and holds the result for FixedUpdate to consume
    /// (docs/01 §2). Keyboard+mouse and gamepad are live simultaneously with no mode toggle and
    /// no settings switch — C6 and FR-541 are not negotiable, so there is deliberately no
    /// "input mode" anywhere in this class.
    /// </summary>
    public sealed class PlayerInputRouter : MonoBehaviour
    {
        [SerializeField] InputActionAsset actions;

        InputActionMap gameplayMap;
        InputAction moveAction;
        InputAction aimAction;
        InputAction dashAction;
        InputAction bankAction;
        InputAction pauseAction;

        Camera cam;
        Plane arenaPlane;

        Vector2 move;
        Vector3 aimDirection = Vector3.forward;
        bool dashBuffered;
        bool bankHeld;
        bool pausePressed;

        public Vector2 Move => move;
        public Vector3 AimDirection => aimDirection;
        public bool BankHeld => bankHeld;
        public InputDeviceKind LastDevice { get; private set; } = InputDeviceKind.KeyboardMouse;

        /// <summary>True when a controller vanished mid-run and the game must pause (FR-546).</summary>
        public bool DeviceLost { get; private set; }

        void Awake()
        {
            cam = Camera.main;   // resolved once in Awake; never touched in Update (CLAUDE.md §4)
            arenaPlane = new Plane(Vector3.up, Vector3.zero);

            gameplayMap = actions.FindActionMap("Gameplay", true);
            moveAction = gameplayMap.FindAction("Move", true);
            aimAction = gameplayMap.FindAction("Aim", true);
            dashAction = gameplayMap.FindAction("Dash", true);
            bankAction = gameplayMap.FindAction("Bank", true);
            pauseAction = gameplayMap.FindAction("Pause", true);
        }

        void OnEnable()
        {
            gameplayMap.Enable();
            InputSystem.onActionChange += OnActionChange;
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        void OnDisable()
        {
            InputSystem.onActionChange -= OnActionChange;
            InputSystem.onDeviceChange -= OnDeviceChange;
            gameplayMap.Disable();
        }

        void Update()
        {
            move = moveAction.ReadValue<Vector2>();
            if (move.sqrMagnitude > 1f) { move.Normalize(); }

            bankHeld = bankAction.IsPressed();

            if (dashAction.WasPressedThisFrame()) { dashBuffered = true; }
            if (pauseAction.WasPressedThisFrame()) { pausePressed = true; }

            UpdateAim();
        }

        /// <summary>One-shot read. Buffered in Update, consumed once by the fixed step.</summary>
        public bool ConsumeDash()
        {
            bool value = dashBuffered;
            dashBuffered = false;
            return value;
        }

        public bool ConsumePause()
        {
            bool value = pausePressed;
            pausePressed = false;
            return value;
        }

        public void ClearBuffers()
        {
            dashBuffered = false;
            pausePressed = false;
            bankHeld = false;
            move = Vector2.zero;
        }

        public void AcknowledgeDeviceLoss()
        {
            DeviceLost = false;
        }

        void UpdateAim()
        {
            Vector2 raw = aimAction.ReadValue<Vector2>();

            if (LastDevice == InputDeviceKind.Gamepad)
            {
                // Right stick: a direction. The deadzone lives on the binding's stick processor,
                // so anything that survives to here is intentional input. Below it the last aim
                // is held rather than snapping forward, so releasing the stick does not swing
                // the weapon across the arena.
                if (raw.sqrMagnitude > 1e-6f)
                {
                    aimDirection = new Vector3(raw.x, 0f, raw.y).normalized;
                }
                return;
            }

            // Mouse: an absolute screen position projected onto the arena plane (FR-102).
            // Screen delta would drift and would break at different resolutions.
            if (cam == null) { return; }

            Ray ray = cam.ScreenPointToRay(new Vector3(raw.x, raw.y, 0f));
            if (!arenaPlane.Raycast(ray, out float distance)) { return; }

            Vector3 point = ray.GetPoint(distance);
            Vector3 toPoint = point - transform.position;
            toPoint.y = 0f;

            if (toPoint.sqrMagnitude > 1e-6f)
            {
                aimDirection = toPoint.normalized;
            }
        }

        void OnActionChange(object obj, InputActionChange change)
        {
            if (change != InputActionChange.ActionPerformed) { return; }

            InputAction action = obj as InputAction;
            InputDevice device = action?.activeControl?.device;
            if (device == null) { return; }

            LastDevice = device is Gamepad ? InputDeviceKind.Gamepad : InputDeviceKind.KeyboardMouse;
        }

        void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change != InputDeviceChange.Removed && change != InputDeviceChange.Disconnected) { return; }
            if (device is not Gamepad) { return; }
            if (LastDevice != InputDeviceKind.Gamepad) { return; }

            // The player was on a pad and it just vanished. Never soft-lock (QA cases 14, 15).
            DeviceLost = true;
            bankHeld = false;
            move = Vector2.zero;
        }
    }
}
