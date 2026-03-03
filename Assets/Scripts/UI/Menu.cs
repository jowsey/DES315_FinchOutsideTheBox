using System.Collections;
using EpicTransport;
using kcp2k;
using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI
{
    public class Menu : MonoBehaviour
    {
        private NetworkManager _networkManager;

        [SerializeField] [Required] private EosTransport _eosTransportPrefab;
        [SerializeField] [Required] private KcpTransport _kcpTransportPrefab;

        [SerializeField] [Required] private LobbyBrowser _lobbyBrowser;
        [SerializeField] [Required] private MainMenuButton _settingsButton;
        [SerializeField] [Required] private SettingsManager _settings;

        [SerializeField] [Required] private InputActionReference _altMoveAction;
        [SerializeField] [Required] private InputActionReference _altJumpAction;


        private void Awake()
        {
            _networkManager = FindAnyObjectByType<NetworkManager>(FindObjectsInactive.Include);
            _networkManager.gameObject.SetActive(false);
            _settings.gameObject.SetActive(false);

            // Clean up old transports if we're coming back from the game
            var transport = NetworkManager.singleton?.transport;
            if (transport is EosTransport eosTransport)
            {
                // Leave Epic lobby if we're in one
                eosTransport.GetComponent<EOSLobby>().LeaveLobby();
            }
            else if (transport is KcpTransport kcpTransport)
            {
                // not sure if we need to do anything here?   
            }
            Destroy(transport?.gameObject);

            Cursor.lockState = CursorLockMode.None;
            
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

            if (_networkManager.transport != null)
            {
                // we currently offload to EOS built-in UI. eventually this will have proper state
                Debug.Log("Existing transport found");
                return;
            }

            var transport = Instantiate(_eosTransportPrefab);
            transport.GetComponent<EOSLobbyHUD>().manager = _networkManager;
            DontDestroyOnLoad(transport);
            _networkManager.transport = transport;
            _networkManager.gameObject.SetActive(true);

            // var eosLobby = transport.GetComponent<EOSLobby>();
            // _lobbyBrowser.EosLobby = eosLobby;
            // _lobbyBrowser.gameObject.SetActive(true);
        }

        private void InitLocal()
        {
            var transport = Instantiate(_kcpTransportPrefab);
            DontDestroyOnLoad(transport);
            _networkManager.transport = transport;
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

        public void Settings()
        {
            _settings.gameObject.SetActive(!_settings.gameObject.activeSelf);
            _settingsButton.SetActive(_settings.gameObject.activeSelf);
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
    }
}