using UnityEngine;
using UnityEngine.Events;
using Util;
using NetworkManager = Mirror.NetworkManager;

namespace UI
{
    public class PlayerPresenceFeed : MonoBehaviour
    {
        public enum PresenceType
        {
            Join,
            Leave
        }
        
        public static readonly UnityEvent<PlayerController> OnPlayerJoin = new();
        public static readonly UnityEvent<PlayerController> OnPlayerLeave = new();

        [SerializeField] private PlayerPresenceItem _playerPresenceItemPrefab;

        private void OnEnable()
        {
            if (!NetworkManager.singleton && !FindAnyObjectByType<EnsureNetworked>())
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

        private void OnPlayerJoinListener(PlayerController player)
        {
            var item = Instantiate(_playerPresenceItemPrefab, transform);
            item.Build(player, PresenceType.Join);
        }

        private void OnPlayerLeaveListener(PlayerController player)
        {
            var item = Instantiate(_playerPresenceItemPrefab, transform);
            item.Build(player, PresenceType.Leave);
        }
    }
}