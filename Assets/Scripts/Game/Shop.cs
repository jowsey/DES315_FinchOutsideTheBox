using Sirenix.OdinInspector;
using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.VisualScripting;
using UnityEngine;

public class Shop : SerializedMonoBehaviour //for _itemPrefabs serialisation (@jowsey is there a better way of doing this ?)
{
    private CinemachineCamera _cinemachineCamera;
    private CinemachineOrbitalFollow _orbitalFollow;
    private CameraZoomController _zoomController;
    [Tooltip("The transform that the camera will be moved to when the shop is entered")]
    [SerializeField] private Transform _cameraLockLocation;

    [Header("Visual Spawning")]
    [SerializeField] private Transform _itemSpawnStart;
    [SerializeField] private Transform _itemSpawnEnd;
    [Tooltip("Map from each ItemType to the visual prefab that will be spawned")]
    [SerializeField] private Dictionary<ItemType, GameObject> _itemPrefabs = new();
    private List<GameObject> _spawnedVisuals = new List<GameObject>();

    [SerializeField] private int _numPurchasableItems;
    private List<ItemType> _purchasableItems = new List<ItemType>();
    public List<ItemType> PurchasableItems
    {
        get => _purchasableItems;
        private set
        {
            _purchasableItems = value;
            RefreshVisuals();
        }
    }
    [SerializeField] private EconomySettings _economySettings;

    //For restoring bought items upon respawn
    private Dictionary<Checkpoint, List<ItemType>> _shopStateAtCheckpoint = new Dictionary<Checkpoint, List<ItemType>>();


    void Awake()
    {
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

    private void Start()
    {
        Cart.OnReachCheckpoint.AddListener(SaveShopState);
        Checkpoint.RespawnEvent.AddListener(RestoreShopState);
    }

    private void OnDestroy()
    {
        Cart.OnReachCheckpoint.RemoveListener(SaveShopState);
        Checkpoint.RespawnEvent.RemoveListener(RestoreShopState);
    }

    private void RefreshVisuals()
    {
        if (!Application.isPlaying) { return; }

        //Destroy the old visuals
        foreach (GameObject visual in _spawnedVisuals)
        {
            if (visual != null) { Destroy(visual); }
        }
        _spawnedVisuals.Clear();
        if (_purchasableItems == null || _purchasableItems.Count == 0 || _itemSpawnStart == null || _itemSpawnEnd == null) { return; }

        //Instantiate the new visuals evenly spaced between _itemSpawnStart and _itemSpawnEnd
        int count = _purchasableItems.Count;
        for (int i = 0; i < count; ++i)
        {
            ItemType itemToSpawn = _purchasableItems[i];
            if (_itemPrefabs.TryGetValue(itemToSpawn, out GameObject prefab))
            {
                //Calculate position along the line
                //If there's only 1 item, stick it in the middle. Otherwise, space em out evenly.
                float t = (count == 1) ? 0.5f : (float)i / (count - 1);
                Vector3 spawnPos = Vector3.Lerp(_itemSpawnStart.position, _itemSpawnEnd.position, t);
                GameObject newVisual = Instantiate(prefab, spawnPos, _itemSpawnStart.rotation, this.transform);
                _spawnedVisuals.Add(newVisual);
            }
            else
            {
                Debug.LogWarning($"Shop is trying to spawn {itemToSpawn} but it's not set in _itemPrefabs");
            }
        }
    }

    private void SaveShopState(Checkpoint checkpoint)
    {
        _shopStateAtCheckpoint[checkpoint] = new List<ItemType>(PurchasableItems);
    }

    private void RestoreShopState(Checkpoint checkpoint)
    {
        if (_shopStateAtCheckpoint.TryGetValue(checkpoint, out List<ItemType> savedItems))
        {
            PurchasableItems = new List<ItemType>(savedItems); //deep copy (todo maybe not necessary ?)
            Debug.Log($"Shop restored: inventory reverted at {checkpoint.AreaName}");
        }
    }

    private void OnValidate()
    {
        _numPurchasableItems = Mathf.Clamp(_numPurchasableItems, 0, (int)ItemType.NUM_TYPES);
        if (PurchasableItems.Count != _numPurchasableItems)
        {
            RandomiseItems();
        }
    }

    [Button]
    private void RandomiseItems()
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
        PurchasableItems = newItems;
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

    //Returns false if purchase was unsuccessful due to a lack of balance
    public bool TryBuy(int index)
    {
        int price = _economySettings.ItemBuyPrices[PurchasableItems[index]];
        if (price > BankManager.Instance.Balance) { return false; }

        BankManager.Instance.CmdSubtractFromBalance(price);
        _purchasableItems.RemoveAt(index);
        RefreshVisuals();

        return true;
    }

    //wasnt sure which one u wanted mush

    //Returns false if purchase was unsuccessful due to a lack of balance
    public bool TryBuy(ItemType item)
    {
        int price = _economySettings.ItemBuyPrices[item];
        if (price > BankManager.Instance.Balance) { return false; }
        
        BankManager.Instance.CmdSubtractFromBalance(price);
        _purchasableItems.Remove(item);
        RefreshVisuals();

        return true;
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
        cart.CmdRemoveAllTreasures();
        BankManager.Instance.CmdAddToBalance(EvaluateSellAllPrice(cart));
    }


    //placeholder (@jowsey do yo shit)
    [Button] public void BuyIndex0() => TryBuy(0);
    [Button] public void BuyIndex1() => TryBuy(1);
    [Button] public void BuyIndex2() => TryBuy(2);
    [Button] public void DebugSellAllPrice() => Debug.Log(EvaluateSellAllPrice(FindAnyObjectByType<Cart>()));
    [Button] public void SellAll() => SellAll(FindAnyObjectByType<Cart>());
}
