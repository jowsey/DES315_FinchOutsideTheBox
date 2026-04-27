using UnityEngine;
using UnityEngine.UI;
using Util;

namespace UI
{
    public class InputIconManager : MonoBehaviour
    {
        [Tooltip("The image to be replaced")]
        [SerializeField] private Image _image;
        
        [Header("Icons")]
        [SerializeField] private Sprite _keyboardMouseSprite;
        [SerializeField] private Sprite _playstationSprite;
        [SerializeField] private Sprite _switchSprite;
        [SerializeField] private Sprite _xboxSprite;

        private InputDeviceManager.InputType _currentlyDisplayedInputType;

        public void Start()
        {
            _currentlyDisplayedInputType = InputDeviceManager.InputType.KeyboardMouse;
            CheckForInputTypeChange();
        }

        private void Update()
        {
            CheckForInputTypeChange();
        }

        private void CheckForInputTypeChange()
        {
            if (_currentlyDisplayedInputType != InputDeviceManager.CurrentInputType)
            {
                switch (InputDeviceManager.CurrentInputType)
                {
                    case
                        InputDeviceManager.InputType.KeyboardMouse:
                        _image.sprite = _keyboardMouseSprite;
                        break;
                    case InputDeviceManager.InputType.Switch:
                        _image.sprite = _switchSprite;
                        break;
                    case InputDeviceManager.InputType.Playstation:
                        _image.sprite = _playstationSprite;
                        break;
                    default: //fallback (xbox for now)
                        _image.sprite = _xboxSprite;
                        break;

                }
                _currentlyDisplayedInputType = InputDeviceManager.CurrentInputType;
            }
        }
    }
}