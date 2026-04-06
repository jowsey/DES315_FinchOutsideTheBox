using Discord.Sdk;
using Networking;
using Sirenix.OdinInspector;
using UI;
using UnityEngine;
using Client = Discord.Sdk.Client;

namespace Util
{
    public class DiscordManager : MonoBehaviour
    {
        private enum PresenceMode
        {
            Menu,
            InGame
        }

        public static DiscordManager Instance { get; private set; }

        [SerializeField] private ulong _clientId;
        [ShowInInspector] private Client.Status _status => _client?.GetStatus() ?? Client.Status.Disconnected;

        private Client _client;
        private PresenceMode _presenceMode;

        private ulong _joinGameEpoch;

        // EOS updates slow as shit so we keep our own count. should never desync on paper but todo maybe have the server be the source of truth
        private int _lobbyPlayerCount;
        private int _lobbyMaxPlayerCount;

        private void Awake()
        {
            if (Instance)
            {
                Destroy(gameObject);
                Debug.LogWarning("Multiple DiscordManagers, deleting new one");
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            _client = new Client();
            _client.SetStatusChangedCallback(OnStatusChanged);
            _client.AddLogCallback(OnLog, LoggingSeverity.Info);

            _client.SetApplicationId(_clientId);
            RebuildPresence();

            NetworkManager.OnJoinGame.AddListener(OnJoinGame);
            NetworkManager.OnLeaveGame.AddListener(OnLeaveGame);
            PlayerPresenceFeed.OnPlayerJoin.AddListener(OnPlayerJoin);
            PlayerPresenceFeed.OnPlayerLeave.AddListener(OnPlayerLeave);
            Cart.OnReachCheckpoint.AddListener(OnReachCheckpoint);
        }

        private void OnDestroy()
        {
            NetworkManager.OnJoinGame.RemoveListener(OnJoinGame);
            NetworkManager.OnLeaveGame.RemoveListener(OnLeaveGame);
            PlayerPresenceFeed.OnPlayerJoin.RemoveListener(OnPlayerJoin);
            PlayerPresenceFeed.OnPlayerLeave.RemoveListener(OnPlayerLeave);
            Cart.OnReachCheckpoint.RemoveListener(OnReachCheckpoint);

            _client?.Dispose();
        }

        public void RebuildPresence()
        {
            var activity = new Activity();
            activity.SetType(ActivityTypes.Playing);

            switch (_presenceMode)
            {
                case PresenceMode.Menu:
                {
                    activity.SetState("In the menu");
                    break;
                }
                case PresenceMode.InGame:
                {
                    activity.SetState("In a game");

                    var timestamps = new ActivityTimestamps();
                    timestamps.SetStart(_joinGameEpoch);
                    activity.SetTimestamps(timestamps);

                    var cart = FindAnyObjectByType<Cart>();
                    var activeCheckpointName = cart.Checkpoints[cart.CurrentCheckpointIndex].AreaName;
                    activity.SetDetails($"Exploring {activeCheckpointName}");

                    if (NetworkManager.singleton.EosLobby.ConnectedToLobby)
                    {
                        var party = new ActivityParty();
                        party.SetId(NetworkManager.singleton.EosLobby.GetCurrentLobbyId());
                        party.SetCurrentSize(_lobbyPlayerCount);
                        party.SetMaxSize(_lobbyMaxPlayerCount);
                        activity.SetParty(party);
                    }

                    break;
                }
            }

            SetActivity(activity);
        }

        private void SetActivity(Activity activity)
        {
            _client.UpdateRichPresence(activity, result => Debug.Log($"Discord: Updated presence ({result.Type()})"));
        }

        private void OnJoinGame()
        {
            _presenceMode = PresenceMode.InGame;
            _joinGameEpoch = (ulong)System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (NetworkManager.singleton.EosLobby.ConnectedToLobby)
            {
                _lobbyPlayerCount = (int)NetworkManager.singleton.GetLobbyPlayerCount();
                _lobbyMaxPlayerCount = (int)NetworkManager.singleton.GetLobbyMaxPlayerCount();

                // Debug.Log($"Join game: connected to lobby ({_lobbyPlayerCount}/{_lobbyMaxPlayerCount})");
            }

            RebuildPresence();
        }

        private void OnLeaveGame()
        {
            _presenceMode = PresenceMode.Menu;
            RebuildPresence();
        }

        private void OnPlayerJoin(PlayerController player)
        {
            if (_presenceMode != PresenceMode.InGame) return;
            _lobbyPlayerCount++;
            RebuildPresence();
        }

        private void OnPlayerLeave(PlayerController player)
        {
            if (_presenceMode != PresenceMode.InGame) return;
            _lobbyPlayerCount--;
            RebuildPresence();
        }

        private void OnReachCheckpoint(Checkpoint checkpoint)
        {
            if (_presenceMode != PresenceMode.InGame) return;
            RebuildPresence();
        }

        private void OnStatusChanged(Client.Status status, Client.Error error, int errorDetail)
        {
            Debug.Log($"Discord status changed: {status}");
            if (error != Client.Error.None)
            {
                Debug.LogError($"Status error: {error}, code: {errorDetail}");
            }
        }

        private void OnLog(string message, LoggingSeverity severity)
        {
            switch (severity)
            {
                case LoggingSeverity.Error:
                    Debug.LogError(message);
                    break;
                case LoggingSeverity.Warning:
                    Debug.LogWarning(message);
                    break;
                default:
                    Debug.Log(message);
                    break;
            }
        }
    }
}