using System.Collections;
using EpicTransport;
using kcp2k;
using Mirror;
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

        // [SerializeField] [Required] private LobbyBrowser _lobbyBrowser;
        [SerializeField] [Required] private MainMenuButton _settingsButton;
        [SerializeField] [Required] private SettingsManager _settings;

        [SerializeField] [Required] private InputActionReference _altMoveAction;
        [SerializeField] [Required] private InputActionReference _altJumpAction;

        private void Awake()
        {
            _settings.gameObject.SetActive(false);
            Cursor.lockState = CursorLockMode.None;
            
            // Clean up previously-used transport if we're coming back from the game
            var transport = NetworkManager.singleton?.transport;
            if (transport is EosTransport eosTransport)
            {
                // Leave Epic lobby if we're in one
                eosTransport.GetComponent<EOSLobby>().LeaveLobby();
                eosTransport.GetComponent<EOSLobbyHUD>().enabled = false;
            }
            else if (transport is KcpTransport kcpTransport)
            {
                // not sure if we need to do anything here?
            }

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

            // Reset player IDs before going into a new game
            PlayerController.NextPlayerNetworkId = 0;
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

        public void PlayOnline()
        {
            Debug.Log("Playing online");

            var eosLobbyHUD = _eosTransport.GetComponent<EOSLobbyHUD>();
            eosLobbyHUD.manager = _networkManager;
            eosLobbyHUD.enabled = true;

            _networkManager.transport = _eosTransport;
            _networkManager.gameObject.SetActive(true);
        }

        private void InitLocal()
        {
            _networkManager.transport = _kcpTransport;
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
                var spawnPoint = FindAnyObjectByType<NetworkStartPosition>();
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

        public void Settings()
        {
            _settings.gameObject.SetActive(!_settings.gameObject.activeSelf);
            _settingsButton.SetActive(_settings.gameObject.activeSelf);
        }

        public void OpenBluesky() => Application.OpenURL("https://finchoutsidethebox.bsky.social");
        public void OpenInstagram() => Application.OpenURL("https://instagram.com/finchoutsidethebox");
        public void OpenItch() => Application.OpenURL("https://fotb.itch.io");
    }
}