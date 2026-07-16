using System.Collections.Generic;
using Game.Items;
using PrimeTween;
using TMPro;
using UnityEngine;

namespace UI
{
    public class ShopUI : MonoBehaviour
    {
        private Shop _shop;

        [SerializeField] private TextMeshProUGUI _sellAllEstimateText;
        [SerializeField] private TextMeshProUGUI _balanceText;
        [SerializeField] private TextMeshProUGUI _errorText;

        [SerializeField] private ItemCard _itemCardPrefab;

        [SerializeField] private AK.Wwise.Event _sellSfx;

        private List<ItemCard> _itemCards = new();

        private Cart _cachedCart;

        private void Awake()
        {
            // todo thuis really sucks, make global singleton now we're committing to one cart
            _cachedCart = FindAnyObjectByType<Cart>();
        }

        private void OnEnable()
        {
            if (_shop) _shop.OnReceiveBuyResult.AddListener(OnReceiveBuyResult);
        }

        private void OnDisable()
        {
            if (_shop) _shop.OnReceiveBuyResult.RemoveListener(OnReceiveBuyResult);
        }

        private void OnReceiveBuyResult(Item item, PurchaseError result)
        {
            if (result == PurchaseError.None) return;

            _errorText.text = result switch
            {
                PurchaseError.NotEnoughMoney => "You don't have enough coins to purchase this.",
                PurchaseError.AlreadyHoldingObject => "You are already holding an object.",
                _ => "An unknown error occurred :\\"
            };

            Tween.Color(_errorText, Color.red, _errorText.color, 0.5f, Ease.OutCubic);
        }

        public void Build(Shop shop)
        {
            _shop = shop;
            shop.OnReceiveBuyResult.AddListener(OnReceiveBuyResult);

            for (var i = 0; i < shop.PurchasableItems.Count; i++)
            {
                var item = shop.PurchasableItems[i];
                ItemCard itemCard = Instantiate(_itemCardPrefab, transform);
                itemCard.Build(item, i, shop);

                itemCard.WorldFollowUI.TrackingTarget = item.transform;
                itemCard.WorldFollowUI.TrackingOffset = Vector3.up * 0.75f;

                _itemCards.Add(itemCard);
            }
        }

        private void Update()
        {
            // todo event for when estimate changes so we don't have to compute every frame
            _sellAllEstimateText.text = $"You will receive <b>{_shop.EvaluateSellAllPrice(_cachedCart)}</b> coins.";
            _balanceText.text = BankManager.Instance.Balance.ToString();
        }

        public void Leave() => _shop.LeaveShop();

        public void SellAll()
        {
            if (_cachedCart.CarriedItems.Count > 0)
            {
                _sellSfx.Post(gameObject);
                _shop.SellAll();
            }
        }
    }
}