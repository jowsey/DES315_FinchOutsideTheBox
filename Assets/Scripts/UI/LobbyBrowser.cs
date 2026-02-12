using System.Collections.Generic;
using Epic.OnlineServices.Lobby;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI
{
    public class LobbyBrowser : MonoBehaviour
    {
        [SerializeField] [Required] private LobbyListing _lobbyListingPrefab;

        [SerializeField] [Required] private Transform _lobbyListContainer;

        [ReadOnly] public EOSLobby EosLobby;

        [SerializeField] [Required] private TMP_InputField _lobbyNameField;
        [SerializeField] [Required] private Button _createLobbyButton;

        private void Start()
        {
            _createLobbyButton.onClick.AddListener(TryCreateLobby);

            EosLobby.CreateLobbySucceeded += CreateLobbySucceeded;
            EosLobby.CreateLobbyFailed += CreateLobbyFailed;

            EosLobby.FindLobbiesSucceeded += FindLobbiesSucceeded;
            EosLobby.FindLobbiesFailed += FindLobbiesFailed;

            EosLobby.JoinLobbySucceeded += JoinLobbySucceeded;
            EosLobby.JoinLobbyFailed += JoinLobbyFailed;
        }

        private void OnDestroy()
        {
            EosLobby.CreateLobbySucceeded -= CreateLobbySucceeded;
        }

        private void TryCreateLobby()
        {
            if (string.IsNullOrEmpty(_lobbyNameField.text))
            {
                Debug.LogWarning("Lobby name cannot be empty");
                return;
            }

            var lobbyAttributes = new AttributeData[]
            {
                new()
                {
                    Key = "lobby_name",
                    Value = _lobbyNameField.text
                },
                new()
                {
                    Key = "max_players",
                    Value = 2
                }
            };

            EosLobby.CreateLobby(2, LobbyPermissionLevel.Publicadvertised, false, lobbyAttributes);
        }

        private void CreateLobbySucceeded(List<Attribute> attributes)
        {
            SceneManager.LoadScene("Game");
        }

        private void CreateLobbyFailed(string error)
        {
            Debug.LogError($"Failed to create lobby: {error}");
        }

        private void FindLobbiesSucceeded(List<LobbyDetails> lobbies)
        {
            foreach (Transform child in _lobbyListContainer) Destroy(child.gameObject);

            foreach (var lobbyDetails in lobbies)
            {
                var listing = Instantiate(_lobbyListingPrefab, transform);

                var lobbyNameKey = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = "lobby_name" };

                var memberCountOptions = new LobbyDetailsGetMemberCountOptions();
                var memberCount = lobbyDetails.GetMemberCount(ref memberCountOptions);

                // todo free memory
                lobbyDetails.CopyAttributeByKey(ref lobbyNameKey, out var lobbyNameAttribute);

                listing.LobbyNameText.text = lobbyNameAttribute.ToString();
                listing.PlayerCountText.text = $"{memberCount}/2";
                listing.JoinButton.onClick.AddListener(() => EosLobby.JoinLobby(lobbyDetails));
            }
        }

        private void FindLobbiesFailed(string error)
        {
            Debug.LogError($"Failed to find lobbies: {error}");
        }

        private void JoinLobbySucceeded(List<Attribute> attributes)
        {
            SceneManager.LoadScene("Game");
            
            // todo look into all this shit
        }

        private void JoinLobbyFailed(string error)
        {
            Debug.LogError($"Failed to join lobby: {error}");
        }
    }
}