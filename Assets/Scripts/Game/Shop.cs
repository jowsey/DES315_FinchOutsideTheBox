using Mirror;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public enum PurchaseError
{
    None,
    NotEnoughMoney,
    AlreadyHoldingObject,
}

public class Shop : NetworkBehaviour
{
    private CinemachineCamera _cinemachineCamera;
    private CinemachineOrbitalFollow _orbitalFollow;
    private CameraZoomController _zoomController;
    [Tooltip("The transform that the camera will be moved to when the shop is entered")]
    [SerializeField] private Transform _cameraLockLocation;

    [Header("Visual Spawning")]
    [SerializeField] private Transform _itemSpawnStart;
    [SerializeField] private Transform _itemSpawnEnd;

    //Because Dictionary isn't serialisable by default (and we can't inherit from SerializedMonoBehaviour since we're inheriting from NetworkBehaviour) (@jowsey is there a better way of doing this ?)
    [System.Serializable]
    private struct ItemPrefabMapping
    {
        public ItemType ItemType;
        public GameObject Prefab;
    }
    [Tooltip("Map from each ItemType to the visual prefab that will be spawned")]
    [SerializeField] private List<ItemPrefabMapping> _itemPrefabsList = new();
    [SerializeField] private Dictionary<ItemType, GameObject> _itemPrefabs = new();

    [SerializeField] private int _numPurchasableItems;
    private List<Item> _purchasableItems = new List<Item>();
    [SerializeField] private EconomySettings _economySettings;

    [Tooltip("The types of items that will be spawned on the shelf when the game starts.")]
    [SerializeField] private List<ItemType> _plannedItemTypes = new List<ItemType>();

    //For restoring bought items upon respawn
    private Dictionary<Checkpoint, List<Item>> _shopStateAtCheckpoint = new();


    private void SyncItemPrefabsDictionary()
    {
        _itemPrefabs.Clear();
        if (_itemPrefabsList == null) { return; }
        foreach (ItemPrefabMapping mapping in _itemPrefabsList)
        {
            _itemPrefabs[mapping.ItemType] = mapping.Prefab;
        }
    }

    void Awake()
    {
        SyncItemPrefabsDictionary();
        foreach (CinemachineCamera cam in FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cam.CompareTag("FreeLookCam"))
            {
                _cinemachineCamera = cam;
                _orbitalFollow = _cinemachineCamera.GetComponent<CinemachineOrbitalFollow>();
                break;
            }
        }
        _zoomController = Camera.main.GetComponent<CameraZoomController>();
    }

    public override void OnStartServer()
    {
        Cart.OnReachCheckpoint.AddListener(SaveShopState);
        Checkpoint.RespawnEvent.AddListener(RestoreShopState);
        SpawnPhysicalItems();
    }

    public override void OnStopServer()
    {
        Cart.OnReachCheckpoint.RemoveListener(SaveShopState);
        Checkpoint.RespawnEvent.RemoveListener(RestoreShopState);
    }

    [Server]
    private void SpawnPhysicalItems()
    {
        _purchasableItems.Clear();
        int count = _plannedItemTypes.Count;
        for (int i = 0; i < count; ++i)
        {
            ItemType itemToSpawn = _plannedItemTypes[i];
            if (_itemPrefabs.TryGetValue(itemToSpawn, out GameObject prefab))
            {
                //Calculate position along the line
                //If there's only one item, stick it in the middle. Otherwise, space em out evenly
                float t = (count == 1) ? 0.5f : (float)i / (count - 1);
                Vector3 spawnPos = Vector3.Lerp(_itemSpawnStart.position, _itemSpawnEnd.position, t);

                //Spawn the networked object and track it
                GameObject newVisual = Instantiate(prefab, spawnPos, _itemSpawnStart.rotation);
                NetworkServer.Spawn(newVisual);
                Item item = newVisual.GetComponent<Item>();
                item.Pickuppable = false;
                _purchasableItems.Add(item);
            }
            else
            {
                Debug.LogWarning($"Shop is trying to spawn {itemToSpawn} but it's not set in _itemPrefabsList");
            }
        }
    }

    [Server]
    private void SaveShopState(Checkpoint checkpoint)
    {
        _shopStateAtCheckpoint[checkpoint] = new List<Item>(_purchasableItems);
    }

    [Server]
    private void RestoreShopState(Checkpoint checkpoint)
    {
        if (_shopStateAtCheckpoint.TryGetValue(checkpoint, out List<Item> savedItems))
        {
            _purchasableItems = new List<Item>(savedItems); //deep copy (todo maybe not necessary ?)

            //Put the items back on the display rack
            int count = _purchasableItems.Count;
            for (int i = 0; i < count; ++i)
            {
                Item item = _purchasableItems[i];
                if (item != null)
                {
                    float t = (count == 1) ? 0.5f : (float)i / (count - 1);
                    item.Rb.position = Vector3.Lerp(_itemSpawnStart.position, _itemSpawnEnd.position, t);
                    item.Rb.rotation = _itemSpawnStart.rotation;
                    Physics.SyncTransforms();
                    item.State = Holdable.HoldableState.Idle;
                    item.Pickuppable = false;
                }
            }
            Debug.Log($"Shop restored: display rack reverted at {checkpoint.AreaName}");
        }
    }

    private void OnValidate()
    {
        SyncItemPrefabsDictionary();
        _numPurchasableItems = Mathf.Clamp(_numPurchasableItems, 0, (int)ItemType.NUM_TYPES);
        if (_plannedItemTypes.Count != _numPurchasableItems)
        {
            RandomisePlannedItems();
        }
    }

    [Button]
    private void RandomisePlannedItems()
    {
        List<ItemType> newItems = new List<ItemType>();
        for (int i = 0; i < _numPurchasableItems; ++i)
        {
            ItemType item;
            do
            {
                item = (ItemType)Random.Range(0, (int)ItemType.NUM_TYPES);
            } while (newItems.Contains(item));
            newItems.Add(item);
        }
        _plannedItemTypes = newItems;
    }

    [Button]
    public void EnterShop()
    {
        //Move camera
        _zoomController.OnForceThirdPersonActionStarted();
        _zoomController.OnForceMinThirdPersonRadiusActionStarted();
        _cinemachineCamera.Follow = _cameraLockLocation;
        _cinemachineCamera.LookAt = _cameraLockLocation;
        _orbitalFollow.HorizontalAxis.Value = _cameraLockLocation.eulerAngles.y;

        //Add control blockers
        PlayerController.ControlBlockerFlags flags = PlayerController.ControlBlockerFlags.All;
        flags &= ~PlayerController.ControlBlockerFlags.Pause;
        flags &= ~PlayerController.ControlBlockerFlags.ToggleTextChat;
        //todo: do we let players respawn if they're in the shop? i feel like it would introduce a loooot of edge cases like if they're in the middle of stuff
        PlayerController.AddControlBlockerFlags(this, flags);
    }

    [Button]
    public void LeaveShop()
    {
        //Move camera
        _cinemachineCamera.Follow = PlayerController.LocalPlayer.transform;
        _cinemachineCamera.LookAt = PlayerController.LocalPlayer.transform;
        _orbitalFollow.HorizontalAxis.Value = PlayerController.LocalPlayer.transform.eulerAngles.y;
        _zoomController.OnRestorePreActionFirstPersonState();
        _zoomController.OnRestorePreActionThirdPersonRadiusState();

        //Remove control blockers
        PlayerController.RemoveAllControlBlockerFlags(this);
    }

    private void TryBuy(int index)
    {
        CmdTryBuy(index);
    }

    [Command(requiresAuthority = false)]
    private void CmdTryBuy(int index, NetworkConnectionToClient sender = null)
    {
        //Buncha input validation
        if (index < 0 || index >= _purchasableItems.Count)
        {
            Debug.LogError("Shop.TryBuy() called with index " + index + " which was out of range (_purchasableItems.Count = " + _purchasableItems.Count + ")");
            return;
        }
        Item itemToBuy = _purchasableItems[index];
        if (itemToBuy == null || itemToBuy.State != Holdable.HoldableState.Idle)
        {
            Debug.LogError("Shop.TryBuy() called with index " + index + " which returned a " + (itemToBuy == null ? "null item" : "non-idle item (name = " + itemToBuy.name + ")"));
            return;
        }
        PlayerController buyer = sender.identity.GetComponent<PlayerController>();
        if (buyer.HeldObject != null)
        {
            TargetBuyResult(sender, PurchaseError.AlreadyHoldingObject, itemToBuy, -1);
        }
        int price = _economySettings.ItemBuyPrices[itemToBuy.Type];
        if (BankManager.Instance.Balance < price)
        {
            TargetBuyResult(sender, PurchaseError.NotEnoughMoney, itemToBuy, price);
            return;
        }

        //Take the money, remove from the display rack, and put it in the player's hands
        BankManager.Instance.CmdSubtractFromBalance(price);
        _purchasableItems.RemoveAt(index);
        itemToBuy.Pickuppable = true;
        itemToBuy.ServerTryPickup(buyer);
        TargetBuyResult(sender, PurchaseError.None, itemToBuy, price);
    }

    [TargetRpc]
    private void TargetBuyResult(NetworkConnection target, PurchaseError err, Item item, int price)
    {
        //@jowsey this gets called after the client tries to buy something, feel free to add some ui stuff in here for a successful / unsuccessful purchase

        //placeholder:
        switch (err)
        {
        case PurchaseError.None:
        {
            Debug.Log("Successfully purchased " + item.name + " for " + price + " juice coins");
            break;
        }
        case PurchaseError.NotEnoughMoney:
        {
            Debug.Log("Failed to purchase " + item.name + " (price = " + price + ", balance = " + BankManager.Instance.Balance + ")");
            break;
        }
        case PurchaseError.AlreadyHoldingObject:
        {
            Debug.Log("Failed to purchase " + item.name + " (already holding an object)");
            break;
        }
        }
    }

    //Returns the sell price of all treasures in the cart (abstracted out of SellAll() for ui purposes) (@jowsey lmk if u need smth different here)
    public int EvaluateSellAllPrice(Cart cart)
    {
        int sellPrice = 0;
        for (TreasureType type = (TreasureType)0; type < TreasureType.NUM_TYPES; ++type)
        {
            if (cart.CarriedTreasureCounts.ContainsKey(type))
            {
                sellPrice += cart.CarriedTreasureCounts[type] * _economySettings.TreasureSellPrices[type];
            }
        }
        return sellPrice;
    }

    public void SellAll(Cart cart)
    {
        BankManager.Instance.CmdAddToBalance(EvaluateSellAllPrice(cart)); //must be done before removing all the treasure, obviously
        cart.CmdRemoveAllTreasures();
    }


    //placeholder (@jowsey do yo shit)
    [Button] public void BuyIndex0() => TryBuy(0);
    [Button] public void BuyIndex1() => TryBuy(1);
    [Button] public void BuyIndex2() => TryBuy(2);
    [Button] public void DebugSellAllPrice() => Debug.Log("Current sell all price: " + EvaluateSellAllPrice(FindAnyObjectByType<Cart>()) + " juice coins");
    [Button] public void SellAll() => SellAll(FindAnyObjectByType<Cart>());
    [Button] public void DebugBalance() => Debug.Log("Balance: " + BankManager.Instance.Balance + " juice coins");
}