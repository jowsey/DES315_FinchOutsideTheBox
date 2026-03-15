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
        
        public static readonly UnityEvent<string, int> OnPlayerJoin = new();
        public static readonly UnityEvent<string, int> OnPlayerLeave = new();

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

        private void OnPlayerJoinListener(string playerName, int skin)
        {
            var item = Instantiate(_playerPresenceItemPrefab, transform);
            item.Render(playerName, skin, PresenceType.Join);
        }

        private void OnPlayerLeaveListener(string playerName, int skin)
        {
            var item = Instantiate(_playerPresenceItemPrefab, transform);
            item.Render(playerName, skin, PresenceType.Leave);
        }
    }
}