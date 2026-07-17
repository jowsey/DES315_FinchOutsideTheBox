using System;
using System.Collections;
using System.Linq;
using Epic.OnlineServices.Lobby;
using Game.Items;
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

        [SerializeField] private LoadingScreen _loadingScreenPrefab;
        private LoadingScreen _loadingScreenInstance;

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

        public void AnimateLoadInto(Action afterAnimateIn, Func<bool> animateOutPredicate)
        {
            _loadingScreenInstance = Instantiate(_loadingScreenPrefab);
            _loadingScreenInstance.OnFinishAnimateIn.AddListener(() =>
            {
                afterAnimateIn.Invoke();
                StartCoroutine(WaitForAnimateOut());
            });

            return;

            IEnumerator WaitForAnimateOut()
            {
                yield return new WaitUntil(animateOutPredicate);
                _loadingScreenInstance.Animate(LoadingScreen.AnimateDirection.Out);
            }
        }

        public void StartHostLoading() => AnimateLoadInto(StartHost, () => PlayerController.LocalPlayer || !NetworkServer.active);
        public void StartClientLoading() => AnimateLoadInto(StartClient, () => PlayerController.LocalPlayer || !NetworkClient.active);

        public override void OnClientConnect()
        {
            base.OnClientConnect();
            OnJoinGame.Invoke();

            if (NetworkServer.active)
            {
                SendClientInfo();
            }
        }

        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged();

            // give some time for ready to settle?
            // seems to fix it. todo, figure out what exactly we're racing and explicitly wait for it
            StartCoroutine(DelaySendClientInfo(0.5f));
            return;

            IEnumerator DelaySendClientInfo(float delay)
            {
                yield return new WaitForSeconds(delay);
                SendClientInfo();
            }
        }

        private void SendClientInfo()
        {
            var clientInfoMessage = new ClientInfoMessage
            {
                PlayerName = SettingsManager.GetSafeName(),
                PlayerUID = SettingsManager.ActiveSettings.UserID
            };

            NetworkClient.Send(clientInfoMessage);
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();
            OnLeaveGame.Invoke();
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // Make player drop treasure if they had one
            var player = conn.identity?.GetComponent<PlayerController>();
            if (player && player.HeldObject)
            {
                player.HeldObject.State = Item.ItemState.Idle;
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

            NetworkServer.SendToReady(new PresenceJoinMessage
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
            if (player.CutscenePlayer) return;

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

        public string GetLobbyJoinCode()
        {
            var options = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = LobbyBrowser.RoomCodeKey };
            EosLobby.ConnectedLobbyDetails.CopyAttributeByKey(ref options, out var roomCodeAttribute);
            if (roomCodeAttribute == null)
            {
                Debug.LogError("Failed to get lobby room code attribute");
                return null;
            }

            var visibility = GetLobbyVisibility();
            if (visibility == "public")
            {
                return roomCodeAttribute?.Data?.Value.AsUtf8.ToString();
            }

            // we use client password instead of server password because if we're in the game, it's
            // guaranteed to match ServerPassword and, crucially, is populated on both client and server
            var password = authenticator.GetComponent<PasswordAuthenticator>().ClientPassword;
            return $"{roomCodeAttribute?.Data?.Value.AsUtf8}.{password}";
        }

        public string GetLobbyVisibility()
        {
            var options = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = LobbyBrowser.VisibilityKey };
            EosLobby.ConnectedLobbyDetails.CopyAttributeByKey(ref options, out var visibilityAttribute);
            if (visibilityAttribute == null)
            {
                Debug.LogError("Failed to get lobby visibility attribute");
                return null;
            }

            return visibilityAttribute?.Data?.Value.AsUtf8.ToString();
        }
    }
}