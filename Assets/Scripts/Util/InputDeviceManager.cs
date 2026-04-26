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
        private static InputType _inputTypeLastFrame;

        public static UnityEvent InputTypeChanged = new();


        public static bool IsGamepadActive()
        {
            Gamepad gamepad = Gamepad.current;
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            double lastGamepadTime = gamepad != null ? gamepad.lastUpdateTime : 0;
            double lastKeyboardTime = keyboard != null ? keyboard.lastUpdateTime : 0;
            double lastMouseTime = mouse != null ? mouse.lastUpdateTime : 0;

            //If gamepad has never been touched, default to keyboard
            if (lastGamepadTime == 0) { return false; }

            return (lastGamepadTime > lastKeyboardTime && lastGamepadTime > lastMouseTime);
        }

        private void Start()
        {
            DontDestroyOnLoad(this);

            CurrentInputType = InputType.KeyboardMouse;
            _inputTypeLastFrame = InputType.KeyboardMouse;
        }

        void Update()
        {
            Debug.Log(IsGamepadActive());

            //Get the current input type
            if (!IsGamepadActive() || Gamepad.current == null)
            {
                //Keyboard and mouse
                CurrentInputType = InputType.KeyboardMouse;
                //Debug.Log("defaulting to: keyboard");
            }
            else
            {
                //Gamepad - which kind?
                if (Gamepad.current is DualShockGamepad) { CurrentInputType = InputType.Playstation; }
                else if (Gamepad.current.displayName.Contains("Switch")) { CurrentInputType = InputType.Switch; }
                else { CurrentInputType = InputType.Xbox; } //just default to xbox
                //Debug.Log("it's a: " + Gamepad.current.displayName);
            }

            if (CurrentInputType != _inputTypeLastFrame)
            {
                InputTypeChanged.Invoke();
            }

            _inputTypeLastFrame = CurrentInputType;
        }
    }
}