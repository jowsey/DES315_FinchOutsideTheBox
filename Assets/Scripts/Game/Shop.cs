using Mirror;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using PrimeTween;
using UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

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

    [SerializeField] private InputActionReference _interactAction;

    [Tooltip("The transform that the camera will be moved to when the shop is entered")]
    [SerializeField] private Transform _cameraLockLocation;

    [SerializeField] private Transform _enterPromptPosition;
    [SerializeField] private InteractPrompt _enterPromptPrefab;
    private InteractPrompt _enterPromptInstance;

    [SerializeField] private ShopUI _shopUIPrefab;
    private ShopUI _shopUIInstance;

    [Header("Visual Spawning")]
    [SerializeField] private Transform _itemSpawnStart;

    [SerializeField] private Transform _itemSpawnEnd;
    
    private Transform _uiCanvas;

    [SerializeField] private CanvasGroup[] _hiddenUIElements;

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
    public List<Item> PurchasableItems { get; private set; } = new();
    [field: SerializeField] public EconomySettings EconomySettings { get; private set; }

    [Tooltip("The types of items that will be spawned on the shelf when the game starts.")]
    [SerializeField] private List<ItemType> _plannedItemTypes = new List<ItemType>();

    //For restoring bought items upon respawn
    private Dictionary<Checkpoint, List<Item>> _shopStateAtCheckpoint = new();
    
    public UnityEvent<Item, PurchaseError> OnReceiveBuyResult { get; private set; } = new();
    
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
        _uiCanvas = GameObject.FindGameObjectWithTag("UICanvas").transform;
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

    private void Update()
    {
        if (_enterPromptInstance && _interactAction.action.WasPressedThisFrame())
        {
            Destroy(_enterPromptInstance.gameObject);
            _enterPromptInstance = null;
            
            EnterShop();
        }
    }

    [Server]
    private void SpawnPhysicalItems()
    {
        PurchasableItems.Clear();
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
                PurchasableItems.Add(item);
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
        _shopStateAtCheckpoint[checkpoint] = new List<Item>(PurchasableItems);
    }

    [Server]
    private void RestoreShopState(Checkpoint checkpoint)
    {
        if (_shopStateAtCheckpoint.TryGetValue(checkpoint, out List<Item> savedItems))
        {
            PurchasableItems = new List<Item>(savedItems); //deep copy (todo maybe not necessary ?)

            //Put the items back on the display rack
            int count = PurchasableItems.Count;
            for (int i = 0; i < count; ++i)
            {
                Item item = PurchasableItems[i];
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
        _orbitalFollow.VerticalAxis.Value = 20; // todo figure out correct dynamic values here

        //Add control blockers
        PlayerController.ControlBlockerFlags flags = PlayerController.ControlBlockerFlags.All;
        flags &= ~PlayerController.ControlBlockerFlags.Pause;
        flags &= ~PlayerController.ControlBlockerFlags.ToggleTextChat;
        //todo: do we let players respawn if they're in the shop? i feel like it would introduce a loooot of edge cases like if they're in the middle of stuff
        PlayerController.AddControlBlockerFlags(this, flags);
        Cursor.lockState = CursorLockMode.None;
        
        //Show UI
        if (!_shopUIInstance)
        {
            _shopUIInstance = Instantiate(_shopUIPrefab, _uiCanvas);
            _shopUIInstance.Build(this);
        }
        
        //Hide action UIs
        foreach (CanvasGroup uiElement in _hiddenUIElements)
        {
            Tween.Alpha(uiElement, 0, 0.25f, Ease.OutCubic);
        }
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
        Cursor.lockState = CursorLockMode.Locked;
        
        //Destroy UI
        if (_shopUIInstance)
        {
            Destroy(_shopUIInstance.gameObject);
            _shopUIInstance = null;
        }
        
        //Show action UIs
        foreach (CanvasGroup uiElement in _hiddenUIElements)
        {
            Tween.Alpha(uiElement, 1, 0.25f, Ease.OutCubic);
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdTryBuy(int index, NetworkConnectionToClient sender = null)
    {
        //Buncha input validation
        if (index < 0 || index >= PurchasableItems.Count)
        {
            Debug.LogError("Shop.TryBuy() called with index " + index + " which was out of range (_purchasableItems.Count = " + PurchasableItems.Count + ")");
            return;
        }
        Item itemToBuy = PurchasableItems[index];
        if (itemToBuy == null || itemToBuy.State != Holdable.HoldableState.Idle)
        {
            Debug.LogError("Shop.TryBuy() called with index " + index + " which returned a " + (itemToBuy == null ? "null item" : "non-idle item (name = " + itemToBuy.name + ")"));
            return;
        }
        PlayerController buyer = sender.identity.GetComponent<PlayerController>();
        if (buyer.HeldObject != null)
        {
            TargetBuyResult(sender, PurchaseError.AlreadyHoldingObject, itemToBuy, -1);
            return;
        }
        int price = EconomySettings.ItemBuyPrices[itemToBuy.Type];
        if (BankManager.Instance.Balance < price)
        {
            TargetBuyResult(sender, PurchaseError.NotEnoughMoney, itemToBuy, price);
            return;
        }

        //Take the money, remove from the display rack, and put it in the player's hands
        BankManager.Instance.Balance -= price;
        PurchasableItems.RemoveAt(index);
        itemToBuy.Pickuppable = true;
        itemToBuy.ServerTryPickup(buyer);
        TargetBuyResult(sender, PurchaseError.None, itemToBuy, price);
    }

    [TargetRpc]
    private void TargetBuyResult(NetworkConnection target, PurchaseError err, Item item, int price)
    {
        OnReceiveBuyResult.Invoke(item, err);
        
        switch (err)
        {
            case PurchaseError.None:
            {
                Debug.Log($"Successfully purchased {item.name} (price = {price})");
                break;
            }
            case PurchaseError.NotEnoughMoney:
            {
                Debug.Log($"Failed to purchase {item.name} (price = {price}, balance = {BankManager.Instance.Balance})");
                break;
            }
            case PurchaseError.AlreadyHoldingObject:
            {
                Debug.Log($"Failed to purchase {item.name} (already holding an object)");
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
                sellPrice += cart.CarriedTreasureCounts[type] * EconomySettings.TreasureSellPrices[type];
            }
        }
        return sellPrice;
    }

    public void CmdSellAll(Cart cart)
    {
        BankManager.Instance.Balance += EvaluateSellAllPrice(cart); //must be done before removing all the treasure, obviously
        cart.RemoveAllTreasures();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (NetworkClient.localPlayer?.gameObject == other.attachedRigidbody?.gameObject && !_enterPromptInstance)
        {
            _enterPromptInstance = Instantiate(_enterPromptPrefab, _uiCanvas);
            _enterPromptInstance.Build(InteractPrompt.InteractionType.EnterShop);
            _enterPromptInstance.WorldFollowUI.TrackingTarget = _enterPromptPosition;
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (NetworkClient.localPlayer?.gameObject == other.attachedRigidbody?.gameObject && _enterPromptInstance)
        {
            Destroy(_enterPromptInstance.gameObject);
            _enterPromptInstance = null;
        }
    }

    //placeholder (@jowsey do yo shit)
    [Button] public void BuyIndex0() => CmdTryBuy(0);
    [Button] public void BuyIndex1() => CmdTryBuy(1);
    [Button] public void BuyIndex2() => CmdTryBuy(2);
    [Button] public void DebugSellAllPrice() => Debug.Log("Current sell all price: " + EvaluateSellAllPrice(FindAnyObjectByType<Cart>()) + " juice coins");
    [Button] public void SellAll() => CmdSellAll(FindAnyObjectByType<Cart>());
    [Button] public void DebugBalance() => Debug.Log("Balance: " + BankManager.Instance.Balance + " juice coins");
}