using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.XInput;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace UI
{
    public class InputIconManager : MonoBehaviour
    {
        [Tooltip("The image to be replaced")]
        [SerializeField] private Image _image;

        [Header("Icons")]
        [SerializeField] private InputActionReference _actionRef;

        private AsyncOperationHandle<Sprite> _iconHandle;

        private static InputDevice _lastActiveDevice;

        private void OnValidate()
        {
            if (!_image) _image = GetComponent<Image>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterStaticDeviceHandler()
        {
            InputSystem.onActionChange += TrackActionDeviceChanged;
        }

        private void OnEnable()
        {
            InputSystem.onActionChange += OnActionChange;
            UpdateIcons();
        }

        private void OnDisable()
        {
            InputSystem.onActionChange -= OnActionChange;
        }

        private static void TrackActionDeviceChanged(object obj, InputActionChange change)
        {
            if (change != InputActionChange.ActionStarted) return;

            var action = (InputAction)obj;
            var control = action.activeControl;
            if (control != null) _lastActiveDevice = control.device;
        }

        private void OnDestroy()
        {
            if (_iconHandle.IsValid()) Addressables.Release(_iconHandle);
        }

        private void OnActionChange(object obj, InputActionChange change)
        {
            if (change != InputActionChange.ActionStarted) return;

            var action = (InputAction)obj;
            var control = action.activeControl;
            if (control != null) UpdateIcons();
        }

        private void UpdateIcons()
        {
            if (!_actionRef) return;

            if (_iconHandle.IsValid()) Addressables.Release(_iconHandle);

            var isControlDeviceNative = true;
            var activeControl = _actionRef.action.controls.FirstOrDefault(control => control.device == _lastActiveDevice);
            if (activeControl == null)
            {
                isControlDeviceNative = false;
                activeControl = _actionRef.action.controls.Count > 0 ? _actionRef.action.controls[0] : null;
            }

            if (activeControl == null) return;

            var inputPath = activeControl.path;

            if (isControlDeviceNative && _lastActiveDevice is Gamepad)
            {
                inputPath = inputPath.Replace(inputPath.Split('/')[1], _lastActiveDevice is DualShockGamepad ? "PlayStation" : "Xbox");
            }

            var assetPath = $"InputIcon{inputPath}";

            _iconHandle = Addressables.LoadAssetAsync<Sprite>(assetPath);
            _iconHandle.Completed += handle =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    if (_image) _image.sprite = handle.Result;
                }
                else
                {
                    Debug.LogWarning($"Input icon '{assetPath}' for {_actionRef.action.name} not found in Addressables!");
                }
            };
        }

        private void LateUpdate()
        {
            if (_iconHandle.IsValid() && !_iconHandle.IsDone)
            {
                _iconHandle.WaitForCompletion();
            }
        }
        
        public void SetAction(InputActionReference action)
        {
            _actionRef = action;
            UpdateIcons();
        }
    }
}