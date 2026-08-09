using System;
using Game.Items;
using Mirror;
using UnityEngine;
using ReadOnlyAttribute = Sirenix.OdinInspector.ReadOnlyAttribute;

namespace Game
{
    public class UpgradeSack : NetworkBehaviour
    {
        [SerializeField] private GameObject _emptySack;
        [SerializeField] private GameObject _fullSack;

        [SerializeField] public AK.Wwise.Event _sackInOut;

        [field: SyncVar(hook = nameof(OnStoredItemChanged))] public Item StoredItem;
        [ReadOnly, NonSerialized] public Transform CartPositionTransform;

        public Rigidbody Rb { get; private set; }
        public ConfigurableJoint Joint { get; private set; }
        [field: SerializeField] public Transform StorePosition { get; private set; }

        private void Awake()
        {
            Rb = GetComponent<Rigidbody>();
            Joint = GetComponent<ConfigurableJoint>();

            _emptySack.SetActive(true);
            _fullSack.SetActive(false);
        }

        private void OnStoredItemChanged(Item oldItem, Item newItem)
        {
            _emptySack.SetActive(!newItem);
            _fullSack.SetActive(newItem);

            _sackInOut.Post(gameObject);

            if (isServer)
            {
                Cart.Instance.ReevaluateTotalItemSellPrice();

                const float sackItemScaleFactor = 0.8f;
                if (oldItem) oldItem.transform.localScale = Vector3.one;
                if (newItem) newItem.transform.localScale = Vector3.one * newItem.GlobalDisplayScale * sackItemScaleFactor;
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            RespawnTarget.OnRespawn.AddListener(OnRespawn);
            RespawnTarget.OnBuildRespawnSnapshot.AddListener(OnBuildRespawnSnapshot);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            RespawnTarget.OnRespawn.RemoveListener(OnRespawn);
            RespawnTarget.OnBuildRespawnSnapshot.RemoveListener(OnBuildRespawnSnapshot);
        }

        private void OnBuildRespawnSnapshot(RespawnTarget.RespawnSnapshot snapshot)
        {
            snapshot.SackStoredItems[this] = StoredItem;
        }

        private void OnRespawn(RespawnTarget target)
        {
            if (!target.Snapshot.SackStoredItems.ContainsKey(this))
            {
                Cart.Instance.Sacks.Remove(this);
                CartPositionTransform.gameObject.SetActive(false);
                NetworkServer.Destroy(gameObject);
            }
        }
    }
}