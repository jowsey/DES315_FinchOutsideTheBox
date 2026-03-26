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
        [SerializeField] [Required] private NetworkManager _networkManager;

        [SerializeField] [Required] private EosTransport _eosTransportPrefab;
        [SerializeField] [Required] private KcpTransport _kcpTransportPrefab;

        private static EosTransport _eosTransport;
        private static KcpTransport _kcpTransport;

        [SerializeField] [Required] private LobbyBrowser _lobbyBrowser;
        [SerializeField] [Required] private SettingsManager _settingsMenu;

        [SerializeField] [Required] private MainMenuButton _lobbyBrowserButton;
        [SerializeField] [Required] private MainMenuButton _settingsButton;

        [SerializeField] [Required] private InputActionReference _altMoveAction;
        [SerializeField] [Required] private InputActionReference _altJumpAction;

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
            if (_settingsButton.Active)
            {
                ToggleSettingsMenu();
            }

            var lbrt = (RectTransform)_lobbyBrowser.transform;
            if (Tween.GetTweensCount(lbrt) > 0 && !force) return; // be patient DAMN

            var active = _lobbyBrowser.gameObject.activeSelf;
            
            _lobbyBrowserButton.SetActive(!active);

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

            _networkManager.StartHost();
        }

        public void JoinLocal()
        {
            Debug.Log("Joining local game");
            InitLocal();

            _networkManager.StartClient();
        }

        public void HostLocal2Player()
        {
            InitLocal();
            _networkManager.StartHost();

            _networkManager.StartCoroutine(Routine());
            return;

            IEnumerator Routine()
            {
                yield return new WaitUntil(() => NetworkServer.localConnection?.isReady == true);

                // manually spawn 2nd player with server authority
                var spawnPoint = FindAnyObjectByType<Networking.NetworkManager>().GetStartPosition();

                var otherPlayer = Instantiate(_networkManager.playerPrefab, spawnPoint.transform.position, spawnPoint.transform.rotation);
                var otherPlayerController = otherPlayer.GetComponent<PlayerController>();
                otherPlayerController.MoveAction = _altMoveAction;
                otherPlayerController.JumpAction = _altJumpAction;

                NetworkServer.ReplacePlayerForConnection(
                    NetworkServer.localConnection,
                    otherPlayer,
                    ReplacePlayerOptions.KeepAuthority
                );
            }
        }

        public void ToggleSettingsMenu()
        {
            // todo ideally we have a proper extensible Pages system
            if (_lobbyBrowserButton.Active)
            {
                ToggleLobbyBrowser(true);
            }

            _settingsMenu.gameObject.SetActive(!_settingsMenu.gameObject.activeSelf);
            _settingsButton.SetActive(_settingsMenu.gameObject.activeSelf);
        }

        public void OpenBluesky() => Application.OpenURL("https://finchoutsidethebox.bsky.social");
        public void OpenInstagram() => Application.OpenURL("https://instagram.com/finchoutsidethebox");
        public void OpenItch() => Application.OpenURL("https://fotb.itch.io");
    }
}