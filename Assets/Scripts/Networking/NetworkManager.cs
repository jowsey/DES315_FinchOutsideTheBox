using System.Linq;
using Epic.OnlineServices.Lobby;
using Mirror;
using UI;
using UnityEngine;
using UnityEngine.Events;

namespace Networking
{
    public struct ClientInfoMessage : NetworkMessage
    {
        public string PlayerName;
        public string PlayerUID;
    }

    public struct PresenceJoinMessage : NetworkMessage
    {
        public uint PlayerNetId;
    }

    public class NetworkManager : Mirror.NetworkManager
    {
        public new static NetworkManager singleton => (NetworkManager)Mirror.NetworkManager.singleton;

        public static readonly UnityEvent OnJoinGame = new();
        public static readonly UnityEvent OnLeaveGame = new();

        private int _nextPlayerIndex;

        public EOSLobby EosLobby { get; private set; }

        public override void Start()
        {
            base.Start();

            EosLobby = FindAnyObjectByType<EOSLobby>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            _nextPlayerIndex = 0;

            NetworkServer.RegisterHandler<ClientInfoMessage>(OnClientInfoMessage);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            NetworkClient.RegisterHandler<PresenceJoinMessage>(OnPresenceJoinMessage);
        }

        public override void OnClientConnect()
        {
            if (!NetworkClient.ready) NetworkClient.Ready();

            NetworkClient.Send(new ClientInfoMessage
            {
                PlayerName = SettingsManager.GetSafeName(),
                PlayerUID = SettingsManager.ActiveSettings.UserID
            });

            OnJoinGame.Invoke();
        }

        public override void OnClientDisconnect()
        {
            OnLeaveGame.Invoke();
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // Make player drop flask if they had one
            var player = conn.identity?.GetComponent<PlayerController>();
            if (player && player.HeldFlask)
            {
                player.HeldFlask.State = Flask.FlaskState.Idle;
            }

            base.OnServerDisconnect(conn);
        }

        public override Transform GetStartPosition()
        {
            // todo eventually this should first assign the player to a cart, and then get *that* cart's checkpoints
            // todo also it should cache this on reaching checkpoint instead of re-running on every join
            var cart = FindAnyObjectByType<Cart>();
            var activeCheckpoint = cart.Checkpoints[cart.CurrentCheckpointIndex];
            startPositions = activeCheckpoint.playerRespawnLocalTransforms.ToList();

            return base.GetStartPosition();
        }

        private void OnClientInfoMessage(NetworkConnectionToClient conn, ClientInfoMessage msg)
        {
            if (conn.identity)
            {
                Debug.LogWarning($"Client {conn.connectionId} sent PlayerJoinMessage but has already joined");
                return;
            }

            var startPos = GetStartPosition();

            var player = Instantiate(playerPrefab, startPos.position, startPos.rotation).GetComponent<PlayerController>();
            player.PlayerIndex = _nextPlayerIndex++;
            player.PlayerName = msg.PlayerName;
            player.PlayerSkinIndex = player.PlayerIndex % PlayerController.LoadedSkins.Length; // round-robin
            player.PlayerUID = msg.PlayerUID;

            NetworkServer.AddPlayerForConnection(conn, player.gameObject);

            NetworkServer.SendToAll(new PresenceJoinMessage
            {
                PlayerNetId = conn.identity.netId,
            });
        }

        private void OnPresenceJoinMessage(PresenceJoinMessage msg)
        {
            // No need to report on our own presence
            if (msg.PlayerNetId == NetworkClient.connection.identity.netId) return;

            //Also don't need to report the cutscene players
            PlayerController player = NetworkClient.spawned[msg.PlayerNetId].GetComponent<PlayerController>();
            if (player.CutscenePlayer) { return; }

            PlayerPresenceFeed.OnPlayerJoin.Invoke(player);
        }

        public uint GetLobbyPlayerCount()
        {
            var options = new LobbyDetailsGetMemberCountOptions();
            return EosLobby.ConnectedLobbyDetails.GetMemberCount(ref options);
        }

        public uint GetLobbyMaxPlayerCount()
        {
            var options = new LobbyDetailsCopyInfoOptions();
            EosLobby.ConnectedLobbyDetails.CopyInfo(ref options, out var lobbyInfo);
            return lobbyInfo?.MaxMembers ?? 0;
        }
    }
}