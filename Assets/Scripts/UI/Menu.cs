using System;
using EpicTransport;
using kcp2k;
using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;

namespace UI
{
    public class Menu : MonoBehaviour
    {
        private NetworkManager _networkManager;

        [SerializeField] [Required] private EosTransport _eosTransportPrefab;
        [SerializeField] [Required] private KcpTransport _kcpTransportPrefab;

        [SerializeField] [Required] private LobbyBrowser _lobbyBrowser;

        private void Awake()
        {
            _networkManager = FindAnyObjectByType<NetworkManager>(FindObjectsInactive.Include);
            _networkManager.gameObject.SetActive(false);
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
    }
}