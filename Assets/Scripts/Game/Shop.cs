using System.Collections.Generic;
using System.Linq;
using Game;
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
    private static readonly int ShopkeepOnBuyTrigger = Animator.StringToHash("OnBuy");

    public static List<ItemData> ItemRegistry { get; private set; }

    private CinemachineCamera _cinemachineCamera;
    private CinemachineOrbitalFollow _orbitalFollow;
    private CinemachineRotationComposer _rotationComposer;
    private CameraZoomController _zoomController;

    private Vector2 _initialRotationComposerDamping;

    [SerializeField] private InputActionReference _interactAction;

    [Tooltip("The transform that the camera will be moved to when the shop is entered")]
    [SerializeField] private Transform _cameraLockLocation;

    [SerializeField] private Transform _enterPromptPosition;
    [SerializeField] private InteractPrompt _enterPromptPrefab;
    private InteractPrompt _enterPromptInstance;

    [SerializeField] private InteractPrompt.InteractPromptConfiguration _enterPromptConfig;

    [SerializeField] private ShopUI _shopUIPrefab;
    private ShopUI _shopUIInstance;

    [SerializeField] private LayerMask _itemHoverMask;
    private ShopCounterItem _hoveredItem;

    private Camera _camera;

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

    [SerializeField] private NetworkAnimator _shopkeepAnimator;

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

    [SerializeField] private int _maxAvailableItems;

    public readonly SyncList<Item> AvailableItems = new();

    public UnityEvent<Item, PurchaseError> OnReceiveBuyResult { get; private set; } = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadAllItems()
    {
        var handle = Addressables.LoadAssetsAsync<ItemData>("Item");
        var items = handle.WaitForCompletion();
        ItemRegistry = items.ToList();

        Debug.Log($"Loaded {ItemRegistry.Count} shop items");
    }

    private void OnAvailableItemAdded(int index)
    {
        var newItem = AvailableItems[index];
        var counterItem = newItem.gameObject.AddComponent<ShopCounterItem>();
        counterItem.Build(newItem.Data);
    }

    private void OnAvailableItemRemoved(int index, Item removedItem)
    {
        if (removedItem.TryGetComponent(out ShopCounterItem counterItem) && counterItem == _hoveredItem)
        {
            _hoveredItem = null;
        }

        Destroy(removedItem.GetComponent<ShopCounterItem>());
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(_itemSpawnStart.position, _itemSpawnEnd.position);
    }

    private void Awake()
    {
        foreach (CinemachineCamera cam in FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cam.CompareTag("FreeLookCam"))
            {
                _cinemachineCamera = cam;
                _orbitalFollow = _cinemachineCamera.GetComponent<CinemachineOrbitalFollow>();
                _rotationComposer = _cinemachineCamera.GetComponent<CinemachineRotationComposer>();
                break;
            }
        }

        _camera = Camera.main!;
        _zoomController = _camera.GetComponent<CameraZoomController>();
        _uiCanvas = GameObject.FindGameObjectWithTag("UICanvas").transform;
    }

    public override void OnStartServer()
    {
        RespawnTarget.OnBuildRespawnSnapshot.AddListener(OnBuildRespawnSnapshot);
        RespawnTarget.OnRespawn.AddListener(OnRespawn);
        SpawnPhysicalItems();

        _telescope.localRotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
        RunNextTelescopeTween();
    }

    public override void OnStartClient()
    {
        _shopkeepRadio.Post(gameObject);

        AvailableItems.OnAdd += OnAvailableItemAdded;
        AvailableItems.OnRemove += OnAvailableItemRemoved;

        for (var i = 0; i < AvailableItems.Count; i++)
        {
            OnAvailableItemAdded(i);
        }
    }

    public override void OnStopServer()
    {
        RespawnTarget.OnBuildRespawnSnapshot.RemoveListener(OnBuildRespawnSnapshot);
        RespawnTarget.OnRespawn.RemoveListener(OnRespawn);
    }

    public override void OnStopClient()
    {
        _shopkeepRadio.Stop(gameObject);
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

        if (_shopUIInstance)
        {
            var extents = new Vector2(0.1f, 0.1f);
            var mousePos = Mouse.current.position.ReadValue();
            _rotationComposer.Composition.ScreenPosition = new Vector2(
                Mathf.Lerp(extents.x, -extents.x, mousePos.x / Screen.width),
                Mathf.Lerp(-extents.y, extents.y, mousePos.y / Screen.height)
            );
        }
    }

    private void LateUpdate()
    {
        if (!_shopUIInstance) return;

        var ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
        var hitItem = Physics.Raycast(ray, out var hit, 100f, _itemHoverMask, QueryTriggerInteraction.Ignore)
            ? hit.collider.GetComponentInParent<ShopCounterItem>()
            : null;

        if (hitItem == _hoveredItem) return;

        if (_hoveredItem) _hoveredItem.SetSelected(false);
        _hoveredItem = hitItem;
        if (_hoveredItem) _hoveredItem.SetSelected(true);
    }

    [Server]
    private void SpawnPhysicalItems()
    {
        AvailableItems.Clear();

        var equipments = ItemRegistry.Where(i => i.Type == ItemType.Equipment).ToList();

        int cappedItemCount = Mathf.Min(_maxAvailableItems, equipments.Count);
        for (int i = 0; i < cappedItemCount; ++i)
        {
            //If we can fit the entire registry, deterministically spawn one of each, otherwise pick at random
            ItemData itemToSpawn = cappedItemCount >= equipments.Count
                ? equipments[i]
                : equipments[Random.Range(0, equipments.Count)];

            //Calculate position along the line
            //If there's only one item, stick it in the middle. Otherwise, space em out evenly
            float t = (cappedItemCount == 1) ? 0.5f : (float)i / (cappedItemCount - 1);
            Vector3 spawnPos = Vector3.Lerp(_itemSpawnStart.position, _itemSpawnEnd.position, t);

            //Spawn the networked object and track it
            Item newItem = Instantiate(itemToSpawn.Prefab, spawnPos, _itemSpawnStart.rotation);
            newItem.Rb.isKinematic = true; // force immediate kinematic before state change to prevent any possible physics tick
            newItem.State = Item.ItemState.Frozen;
            newItem.transform.localScale = Vector3.one * 0.5f;
            newItem.Pickuppable = false;

            NetworkServer.Spawn(newItem.gameObject);
            AvailableItems.Add(newItem);
        }
    }

    [Server]
    private void OnBuildRespawnSnapshot(RespawnTarget.RespawnSnapshot snapshot)
    {
        snapshot.ShopAvailableItems[this] = AvailableItems.ToList();
    }

    [Server]
    private void OnRespawn(RespawnTarget target)
    {
        AvailableItems.Clear();
        AvailableItems.AddRange(target.Snapshot.ShopAvailableItems[this]);

        //Put the items back on the display rack
        int count = AvailableItems.Count;
        for (int i = 0; i < count; ++i)
        {
            Item item = AvailableItems[i];
            if (item)
            {
                float t = (count == 1) ? 0.5f : (float)i / (count - 1);
                Physics.SyncTransforms();

                item.State = Item.ItemState.Frozen;
                item.Rb.position = Vector3.Lerp(_itemSpawnStart.position, _itemSpawnEnd.position, t);
                item.Rb.rotation = _itemSpawnStart.rotation;
                item.transform.localScale = Vector3.one * 0.5f;
                item.Pickuppable = false;
            }
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

        _orbitalFollow.HorizontalAxis.Value = _cameraLockLocation.localEulerAngles.y;
        _orbitalFollow.VerticalAxis.Value = _cameraLockLocation.localEulerAngles.x;

        _initialRotationComposerDamping = _rotationComposer.Damping;
        _rotationComposer.Damping = Vector2.one * 0.5f;

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

        OnboardingPrompt.EnableDetection = false;

        if (_enterPromptInstance) _enterPromptInstance.gameObject.SetActive(false);
    }

    [Button, DisableInEditorMode]
    public void LeaveShop()
    {
        //Move camera
        _cinemachineCamera.Follow = PlayerController.LocalPlayer.transform;
        _cinemachineCamera.LookAt = PlayerController.LocalPlayer.transform;

        _orbitalFollow.HorizontalAxis.Value = PlayerController.LocalPlayer.transform.eulerAngles.y;

        _rotationComposer.Composition.ScreenPosition.x = 0f;
        _rotationComposer.Damping = _initialRotationComposerDamping;

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

        if (_hoveredItem)
        {
            _hoveredItem.SetSelected(false);
            _hoveredItem = null;
        }

        //Show action UIs & enter prompt
        foreach (CanvasGroup uiElement in _hiddenUIElements)
        {
            Tween.Alpha(uiElement, 1, 0.25f, Ease.OutCubic);
        }

        OnboardingPrompt.EnableDetection = true;

        if (_enterPromptInstance) _enterPromptInstance.gameObject.SetActive(true);
    }

    [Command(requiresAuthority = false)]
    public void CmdTryBuy(int index, NetworkConnectionToClient sender = null)
    {
        //Buncha input validation
        if (index < 0 || index >= AvailableItems.Count)
        {
            Debug.LogError("Shop.TryBuy() called with index " + index + " which was out of range (_purchasableItems.Count = " + AvailableItems.Count + ")");
            return;
        }

        if (!AvailableItems[index])
        {
            Debug.LogError("Shop.TryBuy() called with index " + index + " which was null (has the item already been bought?)");
            return;
        }

        Item itemToBuy = AvailableItems[index].GetComponent<Item>();
        if (!itemToBuy || itemToBuy.State != Item.ItemState.Frozen)
        {
            Debug.LogError("Shop.TryBuy() called with index " + index + " which returned a " + (!itemToBuy ? "null item" : "non-frozen item (name = " + itemToBuy.name + ")"));
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
        AvailableItems[index] = null;
        itemToBuy.Pickuppable = true;
        itemToBuy.transform.localScale = Vector3.one;
        itemToBuy.State = Item.ItemState.Idle;
        itemToBuy.ServerTryPickup(buyer);

        TargetBuyResult(sender, PurchaseError.None, itemToBuy, price);
        _shopkeepAnimator.SetTrigger(ShopkeepOnBuyTrigger);
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
                item.BuySfx?.Post(gameObject);

                LeaveShop();
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

    [Command(requiresAuthority = false)]
    public void CmdSellAll()
    {
        BankManager.Instance.Balance += Cart.Instance.EvaluateTotalItemSellPrice(); //must be done before removing all the treasure, obviously
        Cart.Instance.RemoveAllTreasures();
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
            //Don't show players Enter prompt if they're on the cart or holding something
            if (PlayerController.LocalPlayer?.Seat || PlayerController.LocalPlayer?.HeldObject) return;

            _enterPromptInstance = Instantiate(_enterPromptPrefab, _uiCanvas);
            _enterPromptInstance.Build(_enterPromptConfig);
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
}