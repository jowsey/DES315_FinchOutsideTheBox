using System.Linq;
using Mirror;
using UI;
using UnityEngine;

namespace Networking
{
    public struct ClientInfoMessage : NetworkMessage
    {
        public string PlayerName;
    }

    public struct PresenceFeedMessage : NetworkMessage
    {
        public uint PlayerNetId;
        public string PlayerName;
        public PlayerPresenceFeed.CatSkin Skin;
        public PlayerPresenceFeed.PresenceType PresenceType;
    }

    public class NetworkManager : Mirror.NetworkManager
    {
        private int _nextPlayerIndex;

        public override void OnStartServer()
        {
            base.OnStartServer();

            _nextPlayerIndex = 0;

            NetworkServer.RegisterHandler<ClientInfoMessage>(OnClientInfoMessage);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            NetworkClient.RegisterHandler<PresenceFeedMessage>(OnPresenceFeedMessage);
        }

        public override void OnClientConnect()
        {
            if (!NetworkClient.ready) NetworkClient.Ready();

            var chosenName = string.IsNullOrWhiteSpace(SettingsManager.ActiveSettings.PlayerName)
                ? SettingsManager.GetRandomName()
                : SettingsManager.ActiveSettings.PlayerName;

            NetworkClient.Send(new ClientInfoMessage
            {
                PlayerName = chosenName
            });
        }

        private void OnClientInfoMessage(NetworkConnectionToClient conn, ClientInfoMessage msg)
        {
            if (conn.identity)
            {
                Debug.LogWarning($"Client {conn.connectionId} sent PlayerJoinMessage but has already joined");
                return;
            }

            // todo eventually this should first assign the player to a cart, and then get *that* cart's checkpoints
            // todo also it should cache this on reaching checkpoint instead of re-running on every join
            var cart = FindAnyObjectByType<Cart>();
            var activeCheckpoint = cart.Checkpoints[Mathf.Clamp(cart.CurrentCheckpointIndex, 0, cart.Checkpoints.Count - 1)]; // clamp because it starts at -1
            startPositions = activeCheckpoint.playerRespawnLocalTransforms.ToList();

            var startPos = GetStartPosition();

            var player = Instantiate(playerPrefab, startPos.position, startPos.rotation).GetComponent<PlayerController>();
            player.PlayerIndex = _nextPlayerIndex++;
            player.PlayerName = msg.PlayerName;
            player.PlayerSkin = player.PlayerIndex == 0 ? PlayerPresenceFeed.CatSkin.Green : PlayerPresenceFeed.CatSkin.Blue;

            NetworkServer.AddPlayerForConnection(conn, player.gameObject);

            NetworkServer.SendToAll(new PresenceFeedMessage
            {
                PlayerNetId = conn.identity.netId,
                PlayerName = player.PlayerName,
                Skin = player.PlayerSkin,
                PresenceType = PlayerPresenceFeed.PresenceType.Join
            });
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (conn.identity.TryGetComponent(out PlayerController player))
            {
                NetworkServer.SendToAll(new PresenceFeedMessage
                {
                    PlayerNetId = conn.identity.netId,
                    PlayerName = player.PlayerName,
                    Skin = player.PlayerSkin,
                    PresenceType = PlayerPresenceFeed.PresenceType.Leave
                });
            }

            base.OnServerDisconnect(conn);
        }

        private void OnPresenceFeedMessage(PresenceFeedMessage msg)
        {
            // No need to report on our own presence
            if (msg.PlayerNetId == NetworkClient.connection.identity.netId) return;

            if (msg.PresenceType == PlayerPresenceFeed.PresenceType.Join)
                PlayerPresenceFeed.OnPlayerJoin.Invoke(msg.PlayerName, msg.Skin);
            else if (msg.PresenceType == PlayerPresenceFeed.PresenceType.Leave)
                PlayerPresenceFeed.OnPlayerLeave.Invoke(msg.PlayerName, msg.Skin);
        }
    }
}