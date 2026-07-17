using System.Collections.Generic;
using System.Linq;
using Game.Items;
using Mirror;
using PrimeTween;
using Sirenix.OdinInspector;
using UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AddressableAssets;
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
    private static List<ItemData> _itemRegistry;

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

    [Header("Animation")]
    [SerializeField] private Transform _tipJar;

    [SerializeField] private Transform _telescope;
    [SerializeField] private Transform _hatchLeft;
    [SerializeField] private Transform _hatchRight;
    [SerializeField] private float _tipJarDescendDuration = 0.75f;
    [SerializeField] private float _tipJarDescendHeight = 1.0f;
    [SerializeField, SuffixLabel("degs/s")] private float _telescopeRotateSpeed = 55f;
    [SerializeField] private float _hatchOpenDuration = 1.0f;
    [SerializeField] private float _hatchOpenAngle = 135f;

    private Tween _telescopeRotationTween;
    private bool _hasOpened;

    [Header("Visual Spawning")]
    [SerializeField] private Transform _itemSpawnStart;

    [SerializeField] private Transform _itemSpawnEnd;

    private Transform _uiCanvas;

    [SerializeField] private CanvasGroup[] _hiddenUIElements;

    //Wwise Thangs
    [SerializeField] private AK.Wwise.Event _shopEnter;
    [SerializeField] private AK.Wwise.Event _shopBuy;
    [SerializeField] private AK.Wwise.Event _shopTipJar;
    [SerializeField] private AK.Wwise.Event _shopkeepRadio;

    [SerializeField] private int _numPurchasableItems;

    public SyncList<NetworkIdentity> AvailableItemIdentities = new();

    //For restoring bought items upon respawn
    private Dictionary<Checkpoint, List<NetworkIdentity>> _shopStateAtCheckpoint = new();

    public UnityEvent<Item, PurchaseError> OnReceiveBuyResult { get; private set; } = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadAllItems()
    {
        var handle = Addressables.LoadAssetsAsync<ItemData>("Item");
        var items = handle.WaitForCompletion();
        _itemRegistry = items.ToList();

        Debug.Log($"Loaded {_itemRegistry.Count} shop items");
    }

    private void Awake()
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
        _uiCanvas = GameObject.FindGameObjectWithTag("UICanvas").transform;
    }

    public override void OnStartServer()
    {
        Cart.OnReachCheckpoint.AddListener(SaveShopState);
        Checkpoint.RespawnEvent.AddListener(RestoreShopState);
        SpawnPhysicalItems();

        _telescope.localRotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
        RunNextTelescopeTween();
    }

    public override void OnStartClient()
    {
        _shopkeepRadio.Post(gameObject);
    }

    public override void OnStopServer()
    {
        Cart.OnReachCheckpoint.RemoveListener(SaveShopState);
        Checkpoint.RespawnEvent.RemoveListener(RestoreShopState);
    }

    [Server]
    private void RunNextTelescopeTween()
    {
        Tween.CompleteAll(_telescopeRotationTween);

        var absRot = Random.Range(45f, 180f);
        var sign = Mathf.Sign(Random.Range(-1f, 1f));

        _telescopeRotationTween = Tween.RotationAtSpeed(
            _telescope,
            _telescope.eulerAngles,
            _telescope.eulerAngles + new Vector3(0, sign * absRot, 0),
            _telescopeRotateSpeed,
            Ease.InOutCubic,
            endDelay: Random.Range(1f, 3f)
        ).OnComplete(RunNextTelescopeTween, warnIfTargetDestroyed: false);
    }

    private void Update()
    {
        // Existence of enter prompt implies we're within range, saves a distance check
        if (_enterPromptInstance && _interactAction.action.WasPressedThisFrame())
        {
            EnterShop();
        }
    }

    [Server]
    private void SpawnPhysicalItems()
    {
        AvailableItemIdentities.Clear();
        for (int i = 0; i < _numPurchasableItems; ++i)
        {
            ItemData itemToSpawn = _itemRegistry[Random.Range(0, _itemRegistry.Count)];

            //Calculate position along the line
            //If there's only one item, stick it in the middle. Otherwise, space em out evenly
            float t = (_numPurchasableItems == 1) ? 0.5f : (float)i / (_numPurchasableItems - 1);
            Vector3 spawnPos = Vector3.Lerp(_itemSpawnStart.position, _itemSpawnEnd.position, t);

            //Spawn the networked object and track it
            Item newItem = Instantiate(itemToSpawn.Prefab, spawnPos, _itemSpawnStart.rotation);
            NetworkServer.Spawn(newItem.gameObject);
            newItem.Pickuppable = false;
            AvailableItemIdentities.Add(newItem.netIdentity);
        }
    }

    [Server]
    private void SaveShopState(Checkpoint checkpoint)
    {
        _shopStateAtCheckpoint[checkpoint] = new List<NetworkIdentity>(AvailableItemIdentities);
    }

    [Server]
    private void RestoreShopState(Checkpoint checkpoint)
    {
        if (_shopStateAtCheckpoint.TryGetValue(checkpoint, out var savedItems))
        {
            AvailableItemIdentities = new SyncList<NetworkIdentity>(savedItems); //deep copy (todo maybe not necessary ?)

            //Put the items back on the display rack
            int count = AvailableItemIdentities.Count;
            for (int i = 0; i < count; ++i)
            {
                NetworkIdentity itemIdentity = AvailableItemIdentities[i];
                if (itemIdentity)
                {
                    var item = itemIdentity.GetComponent<Item>();

                    float t = (count == 1) ? 0.5f : (float)i / (count - 1);
                    item.Rb.position = Vector3.Lerp(_itemSpawnStart.position, _itemSpawnEnd.position, t);
                    item.Rb.rotation = _itemSpawnStart.rotation;
                    Physics.SyncTransforms();
                    item.State = Item.ItemState.Idle;
                    item.Pickuppable = false;
                }
            }

            Debug.Log($"Shop restored: display rack reverted at {checkpoint.AreaName}");
        }
    }

    [Button, DisableInEditorMode]
    public void EnterShop()
    {
        //Don't let the player enter the shop if they're on the cart
        if (PlayerController.LocalPlayer.Seat)
        {
            return;
        }

        //Move camera
        _zoomController.OnForceThirdPersonActionStarted();
        _zoomController.OnForceMinThirdPersonRadiusActionStarted();
        _cinemachineCamera.Follow = _cameraLockLocation;
        _cinemachineCamera.LookAt = _cameraLockLocation;
        _orbitalFollow.HorizontalAxis.Value = _cameraLockLocation.eulerAngles.y;
        _orbitalFollow.VerticalAxis.Value = 20; // todo figure out correct dynamic values here

        //Add control blockers
        PlayerController.ControlBlockerFlags flags = PlayerController.ControlBlockerFlags.All;
        flags &= ~PlayerController.ControlBlockerFlags.Respawn;
        PlayerController.AddControlBlockerFlags(this, flags);
        Cursor.lockState = CursorLockMode.None;
        PlayerController.LocalPlayer.ActiveShop = this;

        //Show UI
        if (!_shopUIInstance)
        {
            _shopUIInstance = Instantiate(_shopUIPrefab, _uiCanvas);
            _shopUIInstance.Build(this);
            _shopEnter.Post(gameObject);
        }

        //Hide action UIs & enter prompt
        foreach (CanvasGroup uiElement in _hiddenUIElements)
        {
            Tween.Alpha(uiElement, 0, 0.25f, Ease.OutCubic);
        }

        if (_enterPromptInstance) _enterPromptInstance.gameObject.SetActive(false);
    }

    [Button, DisableInEditorMode]
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

        PlayerController.LocalPlayer.ActiveShop = null;

        //Destroy UI
        if (_shopUIInstance)
        {
            Destroy(_shopUIInstance.gameObject);
            _shopUIInstance = null;
        }

        //Show action UIs & enter prompt
        foreach (CanvasGroup uiElement in _hiddenUIElements)
        {
            Tween.Alpha(uiElement, 1, 0.25f, Ease.OutCubic);
        }

        if (_enterPromptInstance) _enterPromptInstance.gameObject.SetActive(true);
    }

    [Command(requiresAuthority = false)]
    public void CmdTryBuy(int index, NetworkConnectionToClient sender = null)
    {
        //Buncha input validation
        if (index < 0 || index >= AvailableItemIdentities.Count)
        {
            Debug.LogError("Shop.TryBuy() called with index " + index + " which was out of range (_purchasableItems.Count = " + AvailableItemIdentities.Count + ")");
            return;
        }

        if (!AvailableItemIdentities[index])
        {
            Debug.LogError("Shop.TryBuy() called with index " + index + " which was null (has the item already been bought?)");
            return;
        }

        Item itemToBuy = AvailableItemIdentities[index].GetComponent<Item>();
        if (!itemToBuy || itemToBuy.State != Item.ItemState.Idle)
        {
            Debug.LogError("Shop.TryBuy() called with index " + index + " which returned a " + (itemToBuy == null ? "null item" : "non-idle item (name = " + itemToBuy.name + ")"));
            return;
        }

        PlayerController buyer = sender!.identity.GetComponent<PlayerController>();
        if (buyer.HeldObject)
        {
            TargetBuyResult(sender, PurchaseError.AlreadyHoldingObject, itemToBuy, -1);
            return;
        }

        int price = itemToBuy.Data.BuyPrice;
        if (BankManager.Instance.Balance < price)
        {
            TargetBuyResult(sender, PurchaseError.NotEnoughMoney, itemToBuy, price);
            return;
        }

        //Take the money, remove from the display rack, and put it in the player's hands
        BankManager.Instance.Balance -= price;
        AvailableItemIdentities[index] = null;
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
                _shopBuy.Post(gameObject);
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
        // todo maybe * difficulty multiplier
        return cart.CarriedItems.Sum(item => item.Data.SellPrice);
    }

    public void CmdSellAll(Cart cart)
    {
        BankManager.Instance.Balance += EvaluateSellAllPrice(cart); //must be done before removing all the treasure, obviously
        cart.RemoveAllTreasures();
    }

    private void OnTriggerEnter(Collider other)
    {
        // If any player walks in, animate open
        if (!_hasOpened && other.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            _hasOpened = true;
            _tipJar.localPosition = new Vector3(_tipJar.localPosition.x, _tipJar.localPosition.y + _tipJarDescendHeight, _tipJar.localPosition.z);
            Tween.LocalPositionY(
                _tipJar,
                _tipJar.localPosition.y,
                _tipJar.localPosition.y - _tipJarDescendHeight,
                _tipJarDescendDuration,
                Ease.OutBack
            );

            Tween.LocalEulerAngles(
                _hatchLeft,
                _hatchLeft.localRotation.eulerAngles,
                _hatchLeft.localRotation.eulerAngles + new Vector3(0, _hatchOpenAngle, 0),
                _hatchOpenDuration,
                Ease.OutBack
            );
            Tween.LocalEulerAngles(
                _hatchRight,
                _hatchRight.localRotation.eulerAngles,
                _hatchRight.localRotation.eulerAngles - new Vector3(0, _hatchOpenAngle, 0),
                _hatchOpenDuration,
                Ease.OutBack
            );
        }

        if (!_enterPromptInstance && NetworkClient.localPlayer?.gameObject == other.attachedRigidbody?.gameObject)
        {
            //Don't show players Enter prompt if they're on the cart
            if (PlayerController.LocalPlayer?.Seat) return;

            _enterPromptInstance = Instantiate(_enterPromptPrefab, _uiCanvas);
            _enterPromptInstance.Build(InteractPrompt.InteractionType.EnterShop);
            _enterPromptInstance.WorldFollowUI.TrackingTarget = _enterPromptPosition;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (NetworkClient.localPlayer?.gameObject == other.attachedRigidbody?.gameObject && _enterPromptInstance)
        {
            _enterPromptInstance.Destroy();
            _enterPromptInstance = null;
        }
    }

    [Button, DisableInEditorMode] public void BuyIndex0() => CmdTryBuy(0);
    [Button, DisableInEditorMode] public void BuyIndex1() => CmdTryBuy(1);
    [Button, DisableInEditorMode] public void BuyIndex2() => CmdTryBuy(2);
    [Button, DisableInEditorMode] public void DebugSellAllPrice() => Debug.Log("Current sell all price: " + EvaluateSellAllPrice(FindAnyObjectByType<Cart>()) + " juice coins");
    [Button, DisableInEditorMode] public void SellAll() => CmdSellAll(FindAnyObjectByType<Cart>());
    [Button, DisableInEditorMode] public void DebugBalance() => Debug.Log("Balance: " + BankManager.Instance.Balance + " juice coins");
}