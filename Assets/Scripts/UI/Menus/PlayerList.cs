using UnityEngine;

namespace UI
{
    public class PlayerList : MonoBehaviour
    {
        [SerializeField] private PlayerListItem _playerListItemPrefab;

        private void Awake()
        {
            PlayerController.OnPlayerReady.AddListener(OnPlayerJoin);
        }

        private void OnPlayerJoin(PlayerController player)
        {
            if (player.isLocalPlayer) return;

            var item = Instantiate(_playerListItemPrefab, transform);
            item.Build(player);
        }
    }
}