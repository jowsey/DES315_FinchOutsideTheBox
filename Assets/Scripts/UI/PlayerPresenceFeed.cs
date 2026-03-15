using Networking;
using UnityEngine;
using UnityEngine.Events;
using Util;

namespace UI
{
    public class PlayerPresenceFeed : MonoBehaviour
    {
        public enum PresenceType
        {
            Join,
            Leave
        }

        public enum CatSkin
        {
            Green,
            Blue
        }
        
        public static readonly UnityEvent<string, CatSkin> OnPlayerJoin = new();
        public static readonly UnityEvent<string, CatSkin> OnPlayerLeave = new();

        [SerializeField] private PlayerPresenceItem _playerPresenceItemPrefab;
        
        private NetworkManager _networkManager;

        private void OnEnable()
        {
            _networkManager = FindAnyObjectByType<NetworkManager>();
            
            if (!_networkManager && !FindAnyObjectByType<EnsureNetworked>())
            {
                Debug.LogWarning("No NetworkManager found");
                return;
            }
            
            OnPlayerJoin.AddListener(OnPlayerJoinListener);
            OnPlayerLeave.AddListener(OnPlayerLeaveListener);
        }

        private void OnDisable()
        {
            OnPlayerJoin.RemoveListener(OnPlayerJoinListener);
            OnPlayerLeave.RemoveListener(OnPlayerLeaveListener);
        }

        private void OnPlayerJoinListener(string playerName, CatSkin catSkin)
        {
            var item = Instantiate(_playerPresenceItemPrefab, transform);
            item.Render(playerName, catSkin, PresenceType.Join);
        }

        private void OnPlayerLeaveListener(string playerName, CatSkin catSkin)
        {
            var item = Instantiate(_playerPresenceItemPrefab, transform);
            item.Render(playerName, catSkin, PresenceType.Leave);
        }
    }
}