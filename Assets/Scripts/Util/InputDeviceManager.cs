using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

namespace Util
{
    public class InputDeviceManager : MonoBehaviour
    {
        public enum InputType
        {
            KeyboardMouse,
            Playstation,
            Switch,
            Xbox,
        }

        public static InputType CurrentInputType { get; private set; }

        private static double _lastGamepadTime;
        private static double _lastKBMTime;
        private const float _analogueDeadzone = 0.2f;

        public static bool IsGamepadActive()
        {
            return (_lastGamepadTime > _lastKBMTime);
        }

        private void Start()
        {
            DontDestroyOnLoad(this);

            CurrentInputType = InputType.KeyboardMouse;

            //Default to keyboard
            _lastKBMTime = 1;
            _lastGamepadTime = 0;
        }

        void Update()
        {
            DetectKeyboardMouseInput();
            DetectGamepadInput();

            if (!IsGamepadActive() || Gamepad.current == null)
            {
                CurrentInputType = InputType.KeyboardMouse;
                Cursor.visible = true;
            }
            else
            {
                if (Gamepad.current is DualShockGamepad) { CurrentInputType = InputType.Playstation; }
                else if (Gamepad.current.description.manufacturer == "Nintendo") { CurrentInputType = InputType.Switch; }
                else { CurrentInputType = InputType.Xbox; }
                Cursor.visible = false;
            }
        }

        private static void DetectKeyboardMouseInput()
        {
            bool keyboardActive = (Keyboard.current != null && Keyboard.current.anyKey.isPressed);
            bool mouseActive = (Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed || Mouse.current.middleButton.isPressed || Mouse.current.delta.ReadValue().magnitude > 1.0f));
            if (keyboardActive || mouseActive)
            {
                _lastKBMTime = Time.time;
            }
        }

        private static void DetectGamepadInput()
        {
            Gamepad gp = Gamepad.current;
            if (gp == null) { return; }

            bool anyButton = gp.buttonSouth.isPressed || gp.buttonNorth.isPressed ||
                             gp.buttonEast.isPressed || gp.buttonWest.isPressed ||
                             gp.leftShoulder.isPressed || gp.rightShoulder.isPressed ||
                             gp.startButton.isPressed || gp.selectButton.isPressed ||
                             gp.leftStickButton.isPressed || gp.rightStickButton.isPressed ||
                             gp.dpad.up.isPressed || gp.dpad.down.isPressed ||
                             gp.dpad.left.isPressed || gp.dpad.right.isPressed;

            bool anyAxis = gp.leftStick.value.magnitude > _analogueDeadzone ||
                           gp.rightStick.value.magnitude > _analogueDeadzone ||
                           gp.leftTrigger.value > _analogueDeadzone ||
                           gp.rightTrigger.value > _analogueDeadzone;

            if (anyButton || anyAxis)
            {
                _lastGamepadTime = Time.time;
            }
        }
    }
}