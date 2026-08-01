using Game.Items;
using Mirror;
using UnityEngine;

namespace Game
{
    public class UpgradeSack : NetworkBehaviour
    {
        [SerializeField] private GameObject _emptySack;
        [SerializeField] private GameObject _fullSack;

        [field: SyncVar(hook = nameof(OnStoredItemChanged))] public Item StoredItem { get; private set; }

        private void OnStoredItemChanged(Item oldValue, Item newValue)
        {
            _emptySack.SetActive(!newValue);
            _fullSack.SetActive(newValue);
        }
    }
}