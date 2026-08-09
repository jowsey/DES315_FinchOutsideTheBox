using System.Collections;
using EpicTransport;
using kcp2k;
using Mirror;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UI
{
    public class Menu : MonoBehaviour
    {
        [SerializeField] [Required] private Networking.NetworkManager _networkManager;

        [SerializeField] [Required] private EosTransport _eosTransportPrefab;
        [SerializeField] [Required] private KcpTransport _kcpTransportPrefab;

        private static EosTransport _eosTransport;
        private static KcpTransport _kcpTransport;

        [SerializeField] [Required] private CanvasGroup _mainCanvasGroup;
        [SerializeField] [Required] private LobbyBrowser _lobbyBrowser;
        [SerializeField] [Required] private SettingsManager _settingsMenu;
        [SerializeField] [Required] private Credits _creditsPrefab;

        private Credits _creditsInstance;
        private Tween _creditsFadeTween;

        [SerializeField] [Required] private MainMenuButton _lobbyBrowserButton;
        [SerializeField] [Required] private MainMenuButton _settingsButton;
        [SerializeField] [Required] private MainMenuButton _creditsButton;

        [SerializeField] [Required] private InputActionReference _skipCreditsAction;

        private void ResetTransports()
        {
            var transport = NetworkManager.singleton?.transport;
            if (transport is EosTransport eosTransport)
            {
                // Leave Epic lobby if we're in one
                var eosLobby = eosTransport.GetComponent<EOSLobby>();
                if (eosLobby.ConnectedToLobby)
                {
                    eosLobby.LeaveLobby();
                }
            }
            else if (transport is KcpTransport kcpTransport)
            {
                // not sure if we need to do anything here?
            }

            _networkManager.transport = null;
            Transport.active = null;
            _networkManager.gameObject.SetActive(false);
        }

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            PlayerController.ClearAllControlBlockerFlags();

            // Clean up previously-used transport if we're coming back from the game
            ResetTransports();

            // Set up new transports if none exist
            if (!_eosTransport)
            {
                _eosTransport = Instantiate(_eosTransportPrefab);
                DontDestroyOnLoad(_eosTransport);
            }

            if (!_kcpTransport)
            {
                _kcpTransport = Instantiate(_kcpTransportPrefab);
                DontDestroyOnLoad(_kcpTransport);
            }

            _lobbyBrowser.gameObject.SetActive(false);
            _lobbyBrowser.EosLobby = _eosTransport.GetComponent<EOSLobby>();

            _settingsMenu.LoadFromDisk();
            _settingsMenu.gameObject.SetActive(false);

            _skipCreditsAction.action.performed += SkipCredits;
        }

        private IEnumerator Start()
        {
            _lobbyBrowserButton.Button.interactable = false;
            yield return new WaitUntil(() => EOSSDKComponent.LocalUserProductId != null);
            _lobbyBrowserButton.Button.interactable = true;
        }

        private void OnDestroy()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); // if set by button
            _skipCreditsAction.action.performed -= SkipCredits;
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // override for button
        public void ToggleLobbyBrowser() => ToggleLobbyBrowser(false);

        private void ToggleLobbyBrowser(bool force)
        {
            if (_settingsButton.ForcedActive)
            {
                ToggleSettingsMenu();
            }

            var lbrt = (RectTransform)_lobbyBrowser.transform;
            if (Tween.GetTweensCount(lbrt) > 0 && !force) return; // be patient DAMN

            var active = _lobbyBrowser.gameObject.activeSelf;

            _lobbyBrowserButton.SetForcedActive(!active);

            if (!active)
            {
                _networkManager.transport = _eosTransport;
                Transport.active = _eosTransport;
                _networkManager.gameObject.SetActive(true);

                Tween.CompleteAll(lbrt);
                _lobbyBrowser.gameObject.SetActive(true);
                Tween.UIAnchoredPositionY(lbrt, -lbrt.sizeDelta.y, 0, 0.75f, Ease.OutCubic);
            }
            else
            {
                ResetTransports();

                Tween.CompleteAll(lbrt);
                Tween.UIAnchoredPositionY(lbrt, 0, -lbrt.sizeDelta.y, 0.75f, Ease.InCubic)
                    .OnComplete(() => _lobbyBrowser.gameObject.SetActive(false));
            }
        }

        private void InitLocal()
        {
            ResetTransports(); // disables the EOS lobby UI and whatnot if they opened it. overkill but no reason not to

            _networkManager.transport = _kcpTransport;
            Transport.active = _kcpTransport;
            _networkManager.gameObject.SetActive(true);
        }

        public void HostLocal()
        {
            Debug.Log("Hosting new local game");
            InitLocal();

            _networkManager.StartHostLoading();
        }

        public void JoinLocal()
        {
            Debug.Log("Joining local game");
            InitLocal();

            _networkManager.StartClientLoading();
        }

        public void ToggleSettingsMenu()
        {
            // todo ideally we have a proper extensible Pages system
            if (_lobbyBrowserButton.ForcedActive)
            {
                ToggleLobbyBrowser(true);
            }

            _settingsMenu.gameObject.SetActive(!_settingsMenu.gameObject.activeSelf);
            _settingsButton.SetForcedActive(_settingsMenu.gameObject.activeSelf);
        }

        public void RollCredits()
        {
            Tween.Alpha(_mainCanvasGroup, 0f, 1f, Ease.InCubic);
            _mainCanvasGroup.interactable = false;

            _creditsInstance = Instantiate(_creditsPrefab, transform.parent);
            _creditsInstance.OnCreditsFinished.AddListener(() =>
            {
                _mainCanvasGroup.interactable = true;
                Tween.Alpha(_mainCanvasGroup, 1f, 1f, Ease.OutCubic);
            });
        }

        private void SkipCredits(InputAction.CallbackContext ctx)
        {
            if (!_creditsInstance || _creditsFadeTween.isAlive) return;

            _creditsFadeTween = Tween.Alpha(_creditsInstance.CanvasGroup, 0f, 1f, Ease.InCubic)
                .OnComplete(() => Destroy(_creditsInstance.gameObject), warnIfTargetDestroyed: false);
        }

        public void OpenURL(string url) => Application.OpenURL(url);
    }
}