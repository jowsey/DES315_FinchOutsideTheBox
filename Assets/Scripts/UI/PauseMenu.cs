using Mirror;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private InputActionReference _openAction;

        [SerializeField] private RectTransform _hostLeaveDisbandWarning;

        private bool _isActive;
        private CanvasGroup _canvasGroup;

        public AK.Wwise.RTPC RTPCMenuOnOff;

        [SerializeField] [Required] private CinemachineInputAxisController _playerCamInput;
        [SerializeField] [Required] private CameraController _playerCamController;

        private void OnEnable()
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            _openAction.action.performed += OnOpen;
            OnOpen(false);

            if (!NetworkServer.active)
            {
                _hostLeaveDisbandWarning.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            _openAction.action.performed -= OnOpen;
        }

        private void OnDestroy()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); // if set by button
            RTPCMenuOnOff.SetGlobalValue(0);
        }

        // Wrapper for event listener
        private void OnOpen(InputAction.CallbackContext ctx) => OnOpen(!_isActive);

        private void OnOpen(bool active)
        {
            _isActive = active;

            Cursor.lockState = active ? CursorLockMode.None : CursorLockMode.Locked;
            _playerCamInput.enabled = !active;
            _playerCamController.enabled = !active;

            // we don't use SetActive since we want the menu to still receive input events, and being inactive would disable that
            _canvasGroup.alpha = active ? 1 : 0;
            _canvasGroup.interactable = active;
            _canvasGroup.blocksRaycasts = active;

            // bring to front
            if (active) transform.SetAsLastSibling();

            // set wwise rtpc
            RTPCMenuOnOff.SetGlobalValue(active ? 1 : 0);
        }

        public void ReturnToCart()
        {
            var player = NetworkClient.localPlayer.GetComponent<PlayerController>();
            player.ReturnToCart();
        }

        public void QuitToMenu()
        {
            if (NetworkServer.active)
            {
                NetworkManager.singleton.StopHost();
            }
            else if (NetworkClient.active)
            {
                NetworkManager.singleton.StopClient();
            }
        }

        public void QuitToDesktop()
        {
            // NetworkManager auto-disconnects on application quit

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}