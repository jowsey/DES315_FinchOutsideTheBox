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

        private bool _isActive;
        private CanvasGroup _canvasGroup;

        //Menu makes music go through filter
        public AK.Wwise.RTPC RTPCMenu;
        public float RTPCMenuNum;

        [SerializeField] [Required] private CinemachineInputAxisController _playerCamInput;

        public void Update()
        {
            if( _isActive )
            {
                RTPCMenuNum = 1;
            }
            else
            {
                RTPCMenuNum = 0;
            }
            RTPCMenu.SetGlobalValue(RTPCMenuNum);
        }

        private void OnEnable()
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            _openAction.action.performed += OnOpen;
            OnOpen(false);
            
        }

        private void OnDisable()
        {
            _openAction.action.performed -= OnOpen;
            
        }

        private void OnDestroy()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); // if set by button
            RTPCMenuNum = 0;
        }

        // Wrapper for event listener
        private void OnOpen(InputAction.CallbackContext ctx) => OnOpen(!_isActive);

        private void OnOpen(bool active)
        {
            _isActive = active;

            Cursor.lockState = active ? CursorLockMode.None : CursorLockMode.Locked;
            _playerCamInput.enabled = !active;

            // we don't use SetActive since we want the menu to still receive input events, and being inactive would disable that
            _canvasGroup.alpha = active ? 1 : 0;
            _canvasGroup.interactable = active;
            _canvasGroup.blocksRaycasts = active;
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