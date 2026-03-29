using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Epic.OnlineServices.Lobby;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NetworkManager = Mirror.NetworkManager;

namespace UI
{
    public class LobbyBrowser : MonoBehaviour
    {
        [SerializeField] [Required] private LobbyListing _lobbyListingPrefab;
        [SerializeField] [Required] private Transform _lobbyListContainer;

        [SerializeField] [Required] private TMP_InputField _lobbyNameField;
        [SerializeField] [Required] private Button _createLobbyButton;

        [SerializeField] [Required] private GameObject _emptyListNotice;
        [SerializeField] [Required] private GameObject _refreshNotice;

        [ReadOnly] public EOSLobby EosLobby;

        private const string LobbyNameKey = "lobbyName";
        private const string OwnerNameKey = "ownerName";
        private const string GameVersionKey = "gameVersion";

        private const string DefaultLobbyName = "Unnamed Lobby";
        private const string DefaultOwnerName = "???";
        private const string DefaultGameVersion = "?.?.?";

        private void OnEnable()
        {
            _createLobbyButton.onClick.AddListener(TryCreateLobby);

            EosLobby.CreateLobbySucceeded += CreateLobbySucceeded;
            EosLobby.CreateLobbyFailed += CreateLobbyFailed;

            EosLobby.FindLobbiesSucceeded += FindLobbiesSucceeded;
            EosLobby.FindLobbiesFailed += FindLobbiesFailed;

            EosLobby.JoinLobbySucceeded += JoinLobbySucceeded;
            EosLobby.JoinLobbyFailed += JoinLobbyFailed;

            EosLobby.LeaveLobbySucceeded += LeaveLobbySucceeded;
            EosLobby.LeaveLobbyFailed += LeaveLobbyFailed;

            StartCoroutine(RefreshLobbyInterval());
        }

        private IEnumerator RefreshLobbyInterval()
        {
            while (this && enabled)
            {
                EosLobby.FindLobbies();
                yield return new WaitForSeconds(5f);
            }
        }

        private void OnDisable()
        {
            _createLobbyButton.onClick.RemoveListener(TryCreateLobby);

            EosLobby.CreateLobbySucceeded -= CreateLobbySucceeded;
            EosLobby.CreateLobbyFailed -= CreateLobbyFailed;

            EosLobby.FindLobbiesSucceeded -= FindLobbiesSucceeded;
            EosLobby.FindLobbiesFailed -= FindLobbiesFailed;

            EosLobby.JoinLobbySucceeded -= JoinLobbySucceeded;
            EosLobby.JoinLobbyFailed -= JoinLobbyFailed;

            EosLobby.LeaveLobbySucceeded -= LeaveLobbySucceeded;
            EosLobby.LeaveLobbyFailed -= LeaveLobbyFailed;
        }

        private void TryCreateLobby()
        {
            if (string.IsNullOrEmpty(_lobbyNameField.text))
            {
                Debug.LogWarning("Lobby name cannot be empty");
                return;
            }

            EosLobby.CreateLobby(
                (uint)NetworkManager.singleton.maxConnections,
                LobbyPermissionLevel.Publicadvertised,
                false,
                new AttributeData[]
                {
                    new()
                    {
                        Key = LobbyNameKey,
                        Value = _lobbyNameField.text
                    },
                    new()
                    {
                        Key = OwnerNameKey,
                        Value = SettingsManager.GetSafeName()
                    },
                    new()
                    {
                        Key = GameVersionKey,
                        Value = Application.version
                    }
                }
            );
        }

        private void CreateLobbySucceeded(List<Attribute> attributes)
        {
            NetworkManager.singleton.StartHost();
        }

        private void CreateLobbyFailed(string error)
        {
            Debug.LogError($"Failed to create lobby: {error}");
        }

        private void FindLobbiesSucceeded(List<LobbyDetails> lobbies)
        {
            foreach (var listing in _lobbyListContainer.GetComponentsInChildren<LobbyListing>()) Destroy(listing.gameObject);

            var lobbiesNameSorted = lobbies.OrderBy(lobbyDetails =>
            {
                var lobbyNameOptions = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = LobbyNameKey };
                lobbyDetails.CopyAttributeByKey(ref lobbyNameOptions, out var lobbyNameAttribute);
                return lobbyNameAttribute.HasValue ? lobbyNameAttribute.Value.Data?.Value.AsUtf8.ToString() : DefaultLobbyName;
            });

            foreach (var lobbyDetails in lobbiesNameSorted)
            {
                var listing = Instantiate(_lobbyListingPrefab, _lobbyListContainer);

                var lobbyNameOptions = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = LobbyNameKey };
                lobbyDetails.CopyAttributeByKey(ref lobbyNameOptions, out var lobbyNameAttribute);
                var lobbyName = lobbyNameAttribute.HasValue ? lobbyNameAttribute.Value.Data?.Value.AsUtf8.ToString() : DefaultLobbyName;

                var ownerNameOptions = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = OwnerNameKey };
                lobbyDetails.CopyAttributeByKey(ref ownerNameOptions, out var ownerNameAttribute);
                var ownerName = ownerNameAttribute.HasValue ? ownerNameAttribute.Value.Data?.Value.AsUtf8.ToString() : DefaultOwnerName;

                var gameVersionOptions = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = GameVersionKey };
                lobbyDetails.CopyAttributeByKey(ref gameVersionOptions, out var gameVersionAttribute);
                var lobbyGameVersion = gameVersionAttribute.HasValue ? gameVersionAttribute.Value.Data?.Value.AsUtf8.ToString() : DefaultGameVersion;

                var memberCountOptions = new LobbyDetailsGetMemberCountOptions();
                var memberCount = lobbyDetails.GetMemberCount(ref memberCountOptions);

                var copyInfoOptions = new LobbyDetailsCopyInfoOptions();
                lobbyDetails.CopyInfo(ref copyInfoOptions, out var lobbyInfo);

                listing.LobbyNameText.text = lobbyName;
                listing.MetadataText.text = $"{memberCount}/{lobbyInfo.Value.MaxMembers} players" +
                                            $" <color=#999>–</color> " +
                                            $"Created by <b>{ownerName}</b>" +
                                            $" <color=#999>–</color> " +
                                            $"<color={(lobbyGameVersion == Application.version ? "white" : "red")}>v{lobbyGameVersion}</color>";
                listing.JoinButton.onClick.AddListener(() => EosLobby.JoinLobby(lobbyDetails));
            }

            _emptyListNotice.SetActive(lobbies.Count == 0);
            _refreshNotice.transform.SetAsLastSibling();
        }

        private void FindLobbiesFailed(string error)
        {
            Debug.LogError($"Failed to find lobbies: {error}");
        }

        private void JoinLobbySucceeded(List<Attribute> attributes)
        {
            var hostAttribute = attributes.Find(a => a.Data.HasValue && a.Data.Value.Key == EOSLobby.hostAddressKey);
            var hostAddress = hostAttribute.Data.Value.Value.AsUtf8;

            NetworkManager.singleton.networkAddress = hostAddress;
            NetworkManager.singleton.StartClient();
        }

        private void JoinLobbyFailed(string error)
        {
            Debug.LogError($"Failed to join lobby: {error}");
        }

        private void LeaveLobbySucceeded()
        {
            NetworkManager.singleton.StopHost();
            NetworkManager.singleton.StopClient();
        }

        private void LeaveLobbyFailed(string error)
        {
            Debug.LogError($"Failed to leave lobby: {error}");
        }
    }
}