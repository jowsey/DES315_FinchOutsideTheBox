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
        [ReadOnly] public Transform LinkedCartTransform;

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

        private void OnStoredItemChanged(Item oldValue, Item newValue)
        {
            _emptySack.SetActive(!newValue);
            _fullSack.SetActive(newValue);

            _sackInOut.Post(gameObject);
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
                LinkedCartTransform.gameObject.SetActive(false);
                NetworkServer.Destroy(gameObject);
            }
        }
    }
}