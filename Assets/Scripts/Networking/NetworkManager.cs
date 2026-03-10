using Mirror;
using UI;
using UnityEngine;

namespace Networking
{
    public struct PlayerJoinMessage : NetworkMessage
    {
        public string PlayerName;
    }

    public class NetworkManager : Mirror.NetworkManager
    {
        private int _nextPlayerIndex;
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            _nextPlayerIndex = 0;
            
            NetworkServer.RegisterHandler<PlayerJoinMessage>(OnPlayerJoinMessage);
        }

        public override void OnClientConnect()
        {
            if (!NetworkClient.ready) NetworkClient.Ready();
            
            var chosenName = string.IsNullOrWhiteSpace(SettingsManager.ActiveSettings.PlayerName)
                ? SettingsManager.GetRandomName()
                : SettingsManager.ActiveSettings.PlayerName;
            
            NetworkClient.Send(new PlayerJoinMessage
            {
                PlayerName = chosenName
            });
        }

        private void OnPlayerJoinMessage(NetworkConnectionToClient conn, PlayerJoinMessage msg)
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
            
            NetworkServer.AddPlayerForConnection(conn, player.gameObject);
        }
    }
}