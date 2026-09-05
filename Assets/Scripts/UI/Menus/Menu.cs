using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EpicTransport;
using kcp2k;
using Mirror;
using Mirror.Discovery;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UI
{
    public class Menu : MonoBehaviour
    {
        public struct TimedServerResponse
        {
            public ServerResponse Response;
            public float TimeReceived;
        }

        [SerializeField] [Required] private Networking.NetworkManager _networkManager;

        [SerializeField] [Required] private EosTransport _eosTransportPrefab;
        [SerializeField] [Required] private KcpTransport _kcpTransportPrefab;

        private static EosTransport _eosTransport;
        private static KcpTransport _kcpTransport;

        [SerializeField] private NetworkDiscovery _networkDiscovery;

        public static readonly Dictionary<long, TimedServerResponse> DiscoveredServers = new();

        private const float ServerPruneInterval = 5f;

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
            HintPrompt.HasShown = new HintPrompt.TutorialPromptShownStates();

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

            _networkDiscovery = _kcpTransport.GetComponent<NetworkDiscovery>();
            _networkDiscovery.OnServerFound.AddListener(OnServerFound);

            _lobbyBrowser.gameObject.SetActive(false);
            _lobbyBrowser.EosLobby = _eosTransport.GetComponent<EOSLobby>();

            _settingsMenu.LoadFromDisk();
            _settingsMenu.gameObject.SetActive(false);

            _skipCreditsAction.action.performed += SkipCredits;
        }

        private IEnumerator Start()
        {
            DiscoveredServers.Clear();
            _networkDiscovery.StartDiscovery();

            _lobbyBrowserButton.Button.interactable = false;
            yield return new WaitUntil(() => EOSSDKComponent.LocalUserProductId != null);
            _lobbyBrowserButton.Button.interactable = true;

            InvokeRepeating(nameof(PruneOldServers), ServerPruneInterval, ServerPruneInterval);
        }

        private void OnDestroy()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); // if set by button
            _skipCreditsAction.action.performed -= SkipCredits;

            _networkDiscovery.OnServerFound.RemoveListener(OnServerFound);
            CancelInvoke(nameof(PruneOldServers));
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

            DiscoveredServers.Clear();
            _networkDiscovery.AdvertiseServer();
        }

        public void JoinLocal()
        {
            Debug.Log("Joining local game");
            InitLocal();

            // todo list in main lobby browser
            TimedServerResponse? lastServer = DiscoveredServers.Values.Count > 0 ? DiscoveredServers.Values.First() : null;
            if (lastServer == null)
            {
                Debug.LogError("No local server found to join!");
                return;
            }

            _networkDiscovery.StopDiscovery();
            _networkManager.StartClientLoading(lastServer.Value.Response.uri);
        }

        private void OnServerFound(ServerResponse info)
        {
            // Debug.Log("Received server response from: " + info.EndPoint.Address);

            var timedInfo = new TimedServerResponse
            {
                Response = info,
                TimeReceived = Time.time
            };

            if (DiscoveredServers.TryAdd(info.serverId, timedInfo))
            {
                Debug.Log($"Discovered new server at: {info.EndPoint.Address}");
            }
            else
            {
                // Debug.Log("Server already known, updating info");
                DiscoveredServers[info.serverId] = timedInfo;
            }
        }

        private void PruneOldServers()
        {
            var now = Time.time;
            var oldServers = DiscoveredServers.Where(kvp => now - kvp.Value.TimeReceived > ServerPruneInterval).Select(kvp => kvp.Key).ToList();
            foreach (var serverId in oldServers)
            {
                // Debug.Log($"Removed old server with ID: {serverId}");
                DiscoveredServers.Remove(serverId);
            }
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