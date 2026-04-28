using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Util;
using NetworkManager = Networking.NetworkManager;

namespace UI
{
    public class LobbyBrowser : MonoBehaviour
    {
        [SerializeField] [Required] private LobbyListing _lobbyListingPrefab;
        [SerializeField] [Required] private Transform _lobbyListContainer;

        [SerializeField] [Required] private TMP_InputField _lobbyNameField;
        [SerializeField] [Required] private LoadingButton _createLobbyButton;
        [SerializeField] [Required] private Toggle _lobbyPublicToggle;

        [SerializeField] [Required] private TMP_InputField _lobbyCodeField;
        [SerializeField] [Required] private LoadingButton _joinByCodeButton;

        [SerializeField] [Required] private GameObject _emptyListNotice;
        [SerializeField] [Required] private GameObject _refreshNotice;

        [ReadOnly] public EOSLobby EosLobby;

        public const string LobbyNameKey = "lobbyName";
        public const string OwnerNameKey = "ownerName";
        public const string GameVersionKey = "gameVersion";
        public const string RoomCodeKey = "roomCode";
        public const string VisibilityKey = "visibility";

        public const string DefaultLobbyName = "Unnamed Lobby";
        public const string DefaultOwnerName = "???";
        public const string DefaultGameVersion = "?.?.?";

        private string _activeSearchAttemptCode;
        private LobbyListing _activeJoinAttempt;

        private void OnEnable()
        {
            _createLobbyButton.Button.onClick.AddListener(TryCreateLobby);
            _joinByCodeButton.Button.onClick.AddListener(TryJoinLobby);
            _lobbyNameField.onSubmit.AddListener(TryCreateLobby);
            _lobbyCodeField.onSubmit.AddListener(TryJoinLobby);

            EosLobby.CreateLobbySucceeded += CreateLobbySucceeded;
            EosLobby.CreateLobbyFailed += CreateLobbyFailed;

            EosLobby.FindLobbiesSucceeded += FindLobbiesSucceeded;
            EosLobby.FindLobbiesFailed += FindLobbiesFailed;

            EosLobby.JoinLobbySucceeded += JoinLobbySucceeded;
            EosLobby.JoinLobbyFailed += JoinLobbyFailed;

            EosLobby.LeaveLobbySucceeded += LeaveLobbySucceeded;
            EosLobby.LeaveLobbyFailed += LeaveLobbyFailed;

            ClearLobbyListings();
            StartCoroutine(RefreshLobbyInterval());
        }

        private IEnumerator RefreshLobbyInterval()
        {
            var publicSearchOptions = new[]
            {
                new LobbySearchSetParameterOptions
                {
                    ComparisonOp = ComparisonOp.Notequal,
                    Parameter = new AttributeData
                    {
                        Key = VisibilityKey,
                        Value = "protected"
                    }
                }
            };

            while (this && enabled)
            {
                if (!_activeJoinAttempt && _activeSearchAttemptCode == null) EosLobby.FindLobbies(lobbySearchSetParameterOptions: publicSearchOptions);
                yield return new WaitForSeconds(3f); // SessionSearch rate limit is 30/min, so anything over 2s should be chill
            }
        }

        private void OnDisable()
        {
            StopCoroutine(RefreshLobbyInterval());

            _createLobbyButton.Button.onClick.RemoveListener(TryCreateLobby);
            _joinByCodeButton.Button.onClick.RemoveListener(TryJoinLobby);
            _lobbyNameField.onSubmit.RemoveListener(TryCreateLobby);
            _lobbyCodeField.onSubmit.RemoveListener(TryJoinLobby);

            EosLobby.CreateLobbySucceeded -= CreateLobbySucceeded;
            EosLobby.CreateLobbyFailed -= CreateLobbyFailed;

            EosLobby.FindLobbiesSucceeded -= FindLobbiesSucceeded;
            EosLobby.FindLobbiesFailed -= FindLobbiesFailed;

            EosLobby.JoinLobbySucceeded -= JoinLobbySucceeded;
            EosLobby.JoinLobbyFailed -= JoinLobbyFailed;

            EosLobby.LeaveLobbySucceeded -= LeaveLobbySucceeded;
            EosLobby.LeaveLobbyFailed -= LeaveLobbyFailed;
        }

        private void TryJoinLobby(string _) => TryJoinLobby();
        private void TryCreateLobby(string _) => TryCreateLobby();

        private void TryJoinLobby()
        {
            if (string.IsNullOrEmpty(_lobbyCodeField.text))
            {
                Debug.LogWarning("Lobby code cannot be empty");
                return;
            }

            // todo this is horrible
            var split = _lobbyCodeField.text.Split('.');

            var searchCode = split[0];
            _activeSearchAttemptCode = searchCode;

            if (split.Length > 1)
            {
                var password = split[1];
                NetworkManager.singleton.authenticator.GetComponent<Networking.PasswordAuthenticator>().ClientPassword = password;
            }

            EosLobby.FindLobbies(1, new[]
            {
                new LobbySearchSetParameterOptions
                {
                    ComparisonOp = ComparisonOp.Equal,
                    Parameter = new AttributeData
                    {
                        Key = RoomCodeKey,
                        Value = searchCode
                    }
                }
            });

            GloballyLockedButton.AddLockSource(this);
            _joinByCodeButton.SetLoading(true);
        }

        private void TryCreateLobby()
        {
            if (string.IsNullOrEmpty(_lobbyNameField.text))
            {
                Debug.LogWarning("Lobby name cannot be empty");
                return;
            }

            var authenticator = NetworkManager.singleton.authenticator.GetComponent<Networking.PasswordAuthenticator>();
            if (!_lobbyPublicToggle.isOn)
            {
                var password = Base64Url.Generate(8);
                authenticator.ServerPassword = password;
                authenticator.ClientPassword = password;
            }
            else
            {
                authenticator.ServerPassword = null;
                authenticator.ClientPassword = null;
            }

            var roomCode = Base64Url.Generate(6);
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
                    },
                    new()
                    {
                        Key = RoomCodeKey,
                        Value = roomCode
                    },
                    new()
                    {
                        Key = VisibilityKey,
                        Value = _lobbyPublicToggle.isOn ? "public" : "protected"
                    }
                }
            );

            GloballyLockedButton.AddLockSource(this);
            _createLobbyButton.SetLoading(true);
        }

        private void ClearLobbyListings()
        {
            foreach (var listing in _lobbyListContainer.GetComponentsInChildren<LobbyListing>())
                Destroy(listing.gameObject);

            _emptyListNotice.SetActive(true);
        }

        private void CreateLobbySucceeded(List<Attribute> attributes)
        {
            NetworkManager.singleton.StartHost();

            GloballyLockedButton.RemoveLockSource(this); // paired with TryCreateLobby
        }

        private void CreateLobbyFailed(string error)
        {
            Debug.LogError($"Failed to create lobby: {error}");

            GloballyLockedButton.RemoveLockSource(this); // paired with TryCreateLobby
        }

        private void FindLobbiesSucceeded(List<LobbyDetails> lobbies)
        {
            ClearLobbyListings();

            // active search pre-pass
            if (_activeSearchAttemptCode != null)
            {
                foreach (var lobbyDetails in lobbies)
                {
                    var roomCodeOptions = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = RoomCodeKey };
                    lobbyDetails.CopyAttributeByKey(ref roomCodeOptions, out var roomCodeAttribute);
                    var roomCode = roomCodeAttribute?.Data?.Value.AsUtf8.ToString();

                    if (roomCode == _activeSearchAttemptCode)
                    {
                        EosLobby.JoinLobby(lobbyDetails);
                        _activeSearchAttemptCode = null;
                        return;
                    }
                }
            }

            _activeSearchAttemptCode = null;

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

                listing.JoinButton.onClick.AddListener(() =>
                {
                    _activeJoinAttempt = listing;
                    listing.JoinButtonText.text = listing.JoiningText;
                    GloballyLockedButton.AddLockSource(this);

                    EosLobby.JoinLobby(lobbyDetails);
                });
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
            var hostAddress = hostAttribute.Data?.Value.AsUtf8;

            NetworkManager.singleton.networkAddress = hostAddress;
            NetworkManager.singleton.StartClient();

            _activeJoinAttempt.JoinButtonText.text = _activeJoinAttempt.DefaultText;
            _activeJoinAttempt = null;

            GloballyLockedButton.RemoveLockSource(this); // paired with listing join button + TryJoinLobby
        }

        private void JoinLobbyFailed(string error)
        {
            Debug.LogError($"Failed to join lobby: {error}");

            _activeJoinAttempt.JoinButtonText.text = _activeJoinAttempt.DefaultText;
            _activeJoinAttempt = null;

            GloballyLockedButton.RemoveLockSource(this); // paired with listing join button + TryJoinLobby
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