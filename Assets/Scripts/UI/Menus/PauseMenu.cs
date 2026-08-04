using Mirror;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private InputActionReference _openAction;

        [SerializeField] private RectTransform _hostLeaveDisbandWarning;
        [SerializeField] private TextMeshProUGUI _lobbyIdText;

        private bool _isActive;
        private CanvasGroup _canvasGroup;

        [SerializeField] private CanvasGroup[] _hiddenOnOpen;

        public AK.Wwise.RTPC RTPCMenuOnOff;

        private void OnEnable()
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            _openAction.action.performed += OnOpen;
            OnOpen(false);

            if (!NetworkServer.active)
            {
                _hostLeaveDisbandWarning.gameObject.SetActive(false);
            }

            var isOnline = Networking.NetworkManager.singleton?.EosLobby?.ConnectedToLobby == true;

            _lobbyIdText.gameObject.SetActive(isOnline);
            if (isOnline)
            {
                var visibility = Networking.NetworkManager.singleton.GetLobbyVisibility();
                var joinCode = Networking.NetworkManager.singleton.GetLobbyJoinCode();

                var formattedJoinCode = visibility == "public"
                    ? joinCode
                    : new string('*', joinCode.Length);

                _lobbyIdText.text = $"<b>Join code</b>: {formattedJoinCode}\n" +
                                    $"This lobby is <b>{visibility}</b>.";
            }
        }

        public void CopyLobbyId()
        {
            if (!NetworkClient.active) return;

            var joinCode = Networking.NetworkManager.singleton.GetLobbyJoinCode();
            GUIUtility.systemCopyBuffer = joinCode;
        }

        private void OnDisable()
        {
            _openAction.action.performed -= OnOpen;
            OnOpen(false);
        }

        private void OnDestroy()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); // if set by button
        }

        // Wrapper for event listener
        private void OnOpen(InputAction.CallbackContext ctx)
        {
            if (!PlayerController.ControlEnabled(PlayerController.ControlBlockerFlags.Pause) && !_isActive) return;
            OnOpen(!_isActive);
        }

        private void OnOpen(bool active)
        {
            if (active && !PlayerController.LocalPlayer) return; // don't open until client has finished joining
            
            _isActive = active;

            Cursor.lockState = active ? CursorLockMode.None : CursorLockMode.Locked;
            if (active)
            {
                PlayerController.ControlBlockerFlags controllerBlockerFlags = PlayerController.ControlBlockerFlags.All;
                controllerBlockerFlags &= ~PlayerController.ControlBlockerFlags.Pause;
                PlayerController.AddControlBlockerFlags(this, controllerBlockerFlags);
            }
            else
            {
                PlayerController.RemoveAllControlBlockerFlags(this);
            }

            // we don't use SetActive since we want the menu to still receive input events, and being inactive would disable that
            _canvasGroup.alpha = active ? 1 : 0;
            _canvasGroup.interactable = active;
            _canvasGroup.blocksRaycasts = active;

            // bring to front
            if (active) transform.SetAsLastSibling();

            // set wwise rtpc
            RTPCMenuOnOff.SetGlobalValue(active ? 1 : 0);

            // hide
            foreach (var group in _hiddenOnOpen) Tween.Alpha(group, _isActive ? 0f : 1f, 0.5f, Ease.OutCubic);
        }

        public void ReturnToCart() => PlayerController.LocalPlayer.CmdReturnToCart();

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