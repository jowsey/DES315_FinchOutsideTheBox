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

        public Rigidbody Rb { get; private set; }
        public ConfigurableJoint Joint { get; private set; }

        private void Awake()
        {
            Rb = GetComponent<Rigidbody>();
            Joint = GetComponent<ConfigurableJoint>();

            _emptySack.SetActive(true);
            _fullSack.SetActive(false);
        }

        private void OnStoredItemChanged(Item oldValue, Item newValue)
        {
            _emptySack.SetActive(!newValue);
            _fullSack.SetActive(newValue);
        }
    }
}