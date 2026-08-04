using System.Collections.Generic;
using System.Linq;
using Game.Items;
using Mirror;
using PrimeTween;
using Sirenix.OdinInspector;
using UI;
using UI.Shop;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace Game
{
    public class Shop : NetworkBehaviour
    {
        public enum PurchaseError
        {
            None,
            NotEnoughMoney,
            AlreadyHoldingObject,
        }

        private static readonly int ShopkeepOnBuyTrigger = Animator.StringToHash("OnBuy");

        public static List<ItemData> ItemRegistry { get; private set; }

        private CinemachineCamera _cinemachineCamera;
        private CinemachineOrbitalFollow _orbitalFollow;
        private CinemachineRotationComposer _rotationComposer;
        private CameraZoomController _zoomController;

        private Vector2 _initialRotationComposerDamping;

        [SerializeField] private InputActionReference _interactAction;
        [SerializeField] private InputActionReference _buyAction;

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

        [field: SerializeField] public ShopCounterItem SackItem { get; private set; }

        private Camera _camera;

        [Header("Animation")] [SerializeField] private Transform _tipJar;
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

        [Header("Item Spawning")] [SerializeField] private Transform _itemSpawnStart;
        [SerializeField] private Transform _itemSpawnEnd;
        [SerializeField] private int _maxAvailableItems;

        private Transform _uiCanvas;
        [SerializeField] private CanvasGroup[] _hiddenUIElements;

        //Wwise Thangs
        [SerializeField] private AK.Wwise.Event _shopEnter;
        [SerializeField] private AK.Wwise.Event _shopBuy;
        [SerializeField] private AK.Wwise.Event _shopTipJar;
        [SerializeField] private AK.Wwise.Event _shopkeepRadio;
        [SerializeField] private AK.Wwise.Event _itemHoverOverSFX;

        public readonly SyncList<Item> AvailableItems = new();

        public readonly UnityEvent<ItemData, PurchaseError> OnReceiveBuyResult = new();
        private static readonly UnityEvent<bool> OnGlobalSackAvailabilityChange = new();

        [SyncVar(hook = nameof(OnSackAvailabilityChanged))] private bool _sackAvailable = true;

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
            if (!newItem) return; // will be null if already bought on join

            var counterItem = newItem.gameObject.GetComponent<ShopCounterItem>() ?? newItem.gameObject.AddComponent<ShopCounterItem>();
            counterItem.enabled = true;
            counterItem.ItemData = newItem.Data;
            counterItem.SetSelected(false);
        }

        private void OnAvailableItemChanged(int index, Item oldValue)
        {
            var newValue = AvailableItems[index];
            if (!newValue && oldValue.TryGetComponent(out ShopCounterItem counterItem))
            {
                counterItem.Outline.enabled = false;
                if (counterItem == _hoveredItem) _hoveredItem = null;
            }
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

            _camera = FindAnyObjectByType<Camera>(FindObjectsInactive.Include);
            _zoomController = _camera.GetComponent<CameraZoomController>();
            _uiCanvas = GameObject.FindGameObjectWithTag("UICanvas").transform;
        }

        public override void OnStartServer()
        {
            RespawnTarget.OnBuildRespawnSnapshot.AddListener(OnBuildRespawnSnapshot);
            RespawnTarget.OnRespawn.AddListener(OnRespawn);
            RespawnTarget.OnPostRespawn.AddListener(OnPostRespawn);
            OnGlobalSackAvailabilityChange.AddListener(OnGlobalSackAvailabilityChanged);
            SpawnPhysicalItems();

            _telescope.localRotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            RunNextTelescopeTween();
        }

        public override void OnStartClient()
        {
            _shopkeepRadio.Post(gameObject);

            AvailableItems.OnAdd += OnAvailableItemAdded;
            AvailableItems.OnSet += OnAvailableItemChanged;

            for (var i = 0; i < AvailableItems.Count; i++)
            {
                OnAvailableItemAdded(i);
            }
        }

        public override void OnStopServer()
        {
            RespawnTarget.OnBuildRespawnSnapshot.RemoveListener(OnBuildRespawnSnapshot);
            RespawnTarget.OnRespawn.RemoveListener(OnRespawn);
            RespawnTarget.OnPostRespawn.RemoveListener(OnPostRespawn);
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
                var extents = new Vector2(0.05f, 0.075f);
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

            if (hitItem && _buyAction.action.WasPressedThisFrame())
            {
                if (hitItem == SackItem)
                {
                    CmdTryBuySack();
                }
                else
                {
                    var index = AvailableItems.IndexOf(hitItem.GetComponent<Item>());
                    if (index != -1) CmdTryBuy(index);
                }
            }

            if (hitItem == _hoveredItem) return;

            if (_hoveredItem) _hoveredItem.SetSelected(false);
            _hoveredItem = hitItem;
            if (_hoveredItem)
            {
                _hoveredItem.SetSelected(true);
                _itemHoverOverSFX.Post(gameObject);
            }
        }

        [Command(requiresAuthority = false)]
        private void CmdTryBuySack(NetworkConnectionToClient sender = null)
        {
            if (BankManager.Instance.Balance < SackItem.ItemData.BuyPrice)
            {
                TargetBuyResult(sender, PurchaseError.NotEnoughMoney, SackItem.ItemData);
                return;
            }

            var nextSackPosition = Cart.Instance.SackPositions.FirstOrDefault(s => !s.gameObject.activeSelf);
            if (!nextSackPosition) return;

            // Hide on counter if buying final sack
            if (nextSackPosition == Cart.Instance.SackPositions[^1])
            {
                // event invokes on server -> all shop instances on server set a syncvar -> all clients update based on syncvar changing
                // ^ building around syncvars because they run on client late join. not ideal but
                OnGlobalSackAvailabilityChange.Invoke(false);
            }

            BankManager.Instance.Balance -= SackItem.ItemData.BuyPrice;
            nextSackPosition.gameObject.SetActive(true);

            var newSack = Instantiate(Cart.Instance.SackPrefab, nextSackPosition.position, nextSackPosition.rotation);
            newSack.Joint.connectedBody = Cart.Instance.Rb;
            newSack.Joint.connectedAnchor = Cart.Instance.transform.InverseTransformPoint(nextSackPosition.position);
            newSack.CartPositionTransform = nextSackPosition;
            newSack.transform.SetParent(nextSackPosition, worldPositionStays: true);
            NetworkServer.Spawn(newSack.gameObject);

            Cart.Instance.Sacks.Add(newSack);

            _shopkeepAnimator.SetTrigger(ShopkeepOnBuyTrigger);
        }

        [Server]
        private void OnGlobalSackAvailabilityChanged(bool value)
        {
            // todo this is a dumb stupid way of propagating a global value
            _sackAvailable = value;
        }

        private void OnSackAvailabilityChanged(bool oldValue, bool newValue)
        {
            SackItem.gameObject.SetActive(newValue);
        }

        [Server]
        private void SpawnPhysicalItems()
        {
            AvailableItems.Clear();

            var allEquipment = ItemRegistry.Where(i => i.Type == ItemType.Equipment && i.Prefab).ToList();
            var cappedItemCount = Mathf.Min(_maxAvailableItems, allEquipment.Count);
            var itemsToSpawn = allEquipment.OrderBy(_ => Random.value).Take(cappedItemCount).ToList();

            for (var i = 0; i < cappedItemCount; ++i)
            {
                var itemToSpawn = itemsToSpawn[i];

                var t = cappedItemCount == 1 ? 0.5f : (float)i / (cappedItemCount - 1);
                var spawnPos = Vector3.Lerp(_itemSpawnStart.position, _itemSpawnEnd.position, t);

                var newItem = Instantiate(itemToSpawn.Prefab, spawnPos, _itemSpawnStart.rotation);
                newItem.Rb.isKinematic = true; // force immediate kinematic before state change to prevent any possible physics tick
                newItem.StateData = new Item.FrozenStateData();
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

            // Put the items back on the counter
            var count = AvailableItems.Count;
            for (var i = 0; i < count; ++i)
            {
                var item = AvailableItems[i];
                if (!item) continue;

                var t = count == 1 ? 0.5f : (float)i / (count - 1);
                Physics.SyncTransforms();

                item.StateData = new Item.FrozenStateData();
                item.Rb.position = Vector3.Lerp(_itemSpawnStart.position, _itemSpawnEnd.position, t);
                item.Rb.rotation = _itemSpawnStart.rotation;
                item.transform.localScale = Vector3.one * 0.5f;
                item.Pickuppable = false;
            }
        }

        [Server]
        private void OnPostRespawn(RespawnTarget target)
        {
            OnGlobalSackAvailabilityChange.Invoke(Cart.Instance.SackPositions.Any(s => !s.gameObject.activeSelf));
        }

        [Button, DisableInEditorMode]
        public void EnterShop()
        {
            //Don't let the player enter the shop if they're on the cart
            if (PlayerController.LocalPlayer.Seat) return;

            //Move camera
            _zoomController.OnForceThirdPersonActionStarted();
            _zoomController.OnForceMinThirdPersonRadiusActionStarted();
            _cinemachineCamera.Follow = _cameraLockLocation;
            _cinemachineCamera.LookAt = _cameraLockLocation;

            _orbitalFollow.HorizontalAxis.Value = _cameraLockLocation.eulerAngles.y;
            _orbitalFollow.VerticalAxis.Value = _cameraLockLocation.eulerAngles.x;

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

            // Enable item outlines
            SackItem.Outline.enabled = true;
            foreach (var item in AvailableItems)
            {
                if (!item) continue;
                var counterItem = item.GetComponent<ShopCounterItem>();
                if (counterItem) counterItem.Outline.enabled = true;
            }

            //Hide action UIs & enter prompt
            foreach (CanvasGroup uiElement in _hiddenUIElements)
            {
                Tween.Alpha(uiElement, 0, 0.25f, Ease.OutCubic);
            }

            OnboardingPrompt.EnableDetection = false;
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

            // Disable item outlines
            if (_hoveredItem)
            {
                _hoveredItem.SetSelected(false);
                _hoveredItem = null;
            }

            SackItem.Outline.enabled = false;
            foreach (var item in AvailableItems)
            {
                if (!item) continue;
                var counterItem = item.GetComponent<ShopCounterItem>();
                if (counterItem) counterItem.Outline.enabled = false;
            }

            //Show action UIs & enter prompt
            foreach (CanvasGroup uiElement in _hiddenUIElements)
            {
                Tween.Alpha(uiElement, 1, 0.25f, Ease.OutCubic);
            }

            OnboardingPrompt.EnableDetection = true;
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

            Item itemToBuy = AvailableItems[index];
            if (!itemToBuy || itemToBuy.StateData is not Item.FrozenStateData)
            {
                Debug.LogError("Shop.TryBuy() called with index " + index + " which returned a " + (!itemToBuy ? "null item" : "non-frozen item (name = " + itemToBuy.name + ")"));
                return;
            }

            PlayerController buyer = sender!.identity.GetComponent<PlayerController>();
            if (buyer.HeldObject)
            {
                TargetBuyResult(sender, PurchaseError.AlreadyHoldingObject, itemToBuy.Data);
                return;
            }

            int price = itemToBuy.Data.BuyPrice;
            if (BankManager.Instance.Balance < price)
            {
                TargetBuyResult(sender, PurchaseError.NotEnoughMoney, itemToBuy.Data);
                return;
            }

            //Take the money, remove from the display rack, and put it in the player's hands
            BankManager.Instance.Balance -= price;
            AvailableItems[index] = null;
            itemToBuy.Pickuppable = true;
            itemToBuy.transform.localScale = Vector3.one;
            itemToBuy.StateData = new Item.IdleStateData();
            itemToBuy.ServerTryPickup(buyer);

            var counterItem = itemToBuy.GetComponent<ShopCounterItem>();
            counterItem.enabled = false;

            TargetBuyResult(sender, PurchaseError.None, itemToBuy.Data);
            _shopkeepAnimator.SetTrigger(ShopkeepOnBuyTrigger);
        }

        [TargetRpc]
        private void TargetBuyResult(NetworkConnection target, PurchaseError err, ItemData item)
        {
            OnReceiveBuyResult.Invoke(item, err);

            switch (err)
            {
                case PurchaseError.None:
                {
                    _shopBuy.Post(gameObject);
                    item.BuySfx?.Post(gameObject);

                    LeaveShop();
                    break;
                }
                case PurchaseError.NotEnoughMoney:
                {
                    Debug.Log($"Failed to purchase {item.name} (price = {item.BuyPrice}, balance = {BankManager.Instance.Balance})");
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
            var cart = Cart.Instance;

            BankManager.Instance.Balance += cart.TotalItemSellPrice;

            var sackTreasures = cart.Sacks.Select(s => s.StoredItem).OfType<Treasure>().ToList();
            var carriedTreasures = cart.CarriedItems.OfType<Treasure>().ToList();

            foreach (var treasure in sackTreasures)
            {
                // transitioning out of the sack state will automatically un-store it
                treasure.StateData = new Item.InactiveStateData();
            }

            foreach (var treasure in carriedTreasures)
            {
                treasure.StateData = new Item.InactiveStateData();
                cart.RemoveCarriedItem(treasure);
            }
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
        }

        private void OnTriggerStay(Collider other)
        {
            var canShowEnterPrompt = !PlayerController.LocalPlayer?.Seat
                                     && !PlayerController.LocalPlayer?.HeldObject
                                     && !PlayerController.LocalPlayer?.ActiveShop
                                     && !InteractDetection.TargetedTransform
                                     && PlayerController.ControlEnabled(PlayerController.ControlBlockerFlags.Interact);

            var localPlayer = NetworkClient.localPlayer?.gameObject == other.attachedRigidbody?.gameObject;

            if (!_enterPromptInstance && localPlayer && canShowEnterPrompt)
            {
                _enterPromptInstance = Instantiate(_enterPromptPrefab, _uiCanvas);
                _enterPromptInstance.Build(_enterPromptConfig);
                _enterPromptInstance.WorldFollowUI.TrackingTarget = _enterPromptPosition;
            }
            else if (_enterPromptInstance && localPlayer && !canShowEnterPrompt)
            {
                _enterPromptInstance.Destroy();
                _enterPromptInstance = null;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var localPlayer = NetworkClient.localPlayer?.gameObject == other.attachedRigidbody?.gameObject;
            if (_enterPromptInstance && localPlayer)
            {
                _enterPromptInstance.Destroy();
                _enterPromptInstance = null;
            }
        }
    }
}