using System.Collections.Generic;
using System.Linq;
using Mirror;
using UI;
using UnityEngine;

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
        private int _nextPlayerIndex;

        // this is, of course, terribly insecure, given it's trusting the client to
        // be honest about its hardware ID, but I imagine 99% of people won't bother to
        // spoof it, so i think it functions as a good enough deterrent
        private readonly HashSet<string> _bannedPlayerUids = new();

        public void BanPlayer(PlayerController player)
        {
            _bannedPlayerUids.Add(player.PlayerUID);
            player.connectionToClient.Disconnect();
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

            var chosenName = string.IsNullOrWhiteSpace(SettingsManager.ActiveSettings.PlayerName)
                ? SettingsManager.GetRandomName()
                : SettingsManager.ActiveSettings.PlayerName;

            NetworkClient.Send(new ClientInfoMessage
            {
                PlayerName = chosenName,
                PlayerUID = SettingsManager.ActiveSettings.UserID
            });
        }

        public override Transform GetStartPosition()
        {
            // todo eventually this should first assign the player to a cart, and then get *that* cart's checkpoints
            // todo also it should cache this on reaching checkpoint instead of re-running on every join
            var cart = FindAnyObjectByType<Cart>();
            var activeCheckpoint = cart.Checkpoints[Mathf.Clamp(cart.CurrentCheckpointIndex, 0, cart.Checkpoints.Count - 1)]; // clamp because it starts at -1
            startPositions = activeCheckpoint.playerRespawnLocalTransforms.ToList();

            return base.GetStartPosition();
        }

        private void OnClientInfoMessage(NetworkConnectionToClient conn, ClientInfoMessage msg)
        {
            if (_bannedPlayerUids.Contains(msg.PlayerUID))
            {
                Debug.Log($"Client {conn.connectionId} is banned. Rejecting.");
                conn.Disconnect();
                return;
            }

            if (conn.identity)
            {
                Debug.LogWarning($"Client {conn.connectionId} sent PlayerJoinMessage but has already joined");
                return;
            }

            var startPos = GetStartPosition();

            var player = Instantiate(playerPrefab, startPos.position, startPos.rotation).GetComponent<PlayerController>();
            player.PlayerIndex = _nextPlayerIndex++;
            player.PlayerName = msg.PlayerName;
            player.PlayerSkinIndex = player.PlayerIndex % PlayerController.SkinMaterials.Length; // round-robin
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

            var player = NetworkClient.spawned[msg.PlayerNetId].GetComponent<PlayerController>();
            PlayerPresenceFeed.OnPlayerJoin.Invoke(player);
        }
    }
}