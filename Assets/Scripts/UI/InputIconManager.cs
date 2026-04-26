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

        public void Start()
        {
            InputDeviceManager.InputTypeChanged.AddListener(OnInputTypeChanged);
        }

        public void OnDestroy()
        {
            InputDeviceManager.InputTypeChanged.RemoveListener(OnInputTypeChanged);
        }

        private void OnInputTypeChanged()
        {
            //Debug.Log(InputDeviceManager.CurrentInputType);
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
        }
    }
}