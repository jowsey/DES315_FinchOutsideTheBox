using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
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
        private static bool _registeredStaticDeviceHandler;

        private void OnValidate()
        {
            if (!_image) _image = GetComponent<Image>();
        }

        public void Start()
        {
            if (!_registeredStaticDeviceHandler)
            {
                InputSystem.onActionChange += TrackActionDeviceChanged;
                _registeredStaticDeviceHandler = true;
            }

            UpdateIcons();
        }

        private void OnEnable()
        {
            InputSystem.onActionChange += OnActionChange;
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

            _image.sprite = null;
            if (_iconHandle.IsValid()) Addressables.Release(_iconHandle);

            var activeControl = _actionRef.action.controls.FirstOrDefault(control => control.device == _lastActiveDevice);
            activeControl ??= _actionRef.action.controls.Count > 0 ? _actionRef.action.controls[0] : null;
            if (activeControl == null) return;

            _iconHandle = Addressables.LoadAssetAsync<Sprite>("InputIcon/" + activeControl.name);
            _iconHandle.WaitForCompletion();

            if (_iconHandle.Status == AsyncOperationStatus.Succeeded)
            {
                _image.sprite = _iconHandle.Result;
            }
            else
            {
                Debug.LogWarning($"Input icon 'InputIcon/{activeControl.name}' for {_actionRef.action.name} not found in Addressables!");
                Addressables.Release(_iconHandle);
            }
        }

        public void SetAction(InputActionReference action)
        {
            _actionRef = action;
            UpdateIcons();
        }
    }
}