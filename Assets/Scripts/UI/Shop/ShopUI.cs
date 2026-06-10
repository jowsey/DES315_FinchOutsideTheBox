using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace UI
{
    public class ShopUI : MonoBehaviour
    {
        private Shop _shop;

        [SerializeField] private TextMeshProUGUI _sellAllEstimateText;
        [SerializeField] private TextMeshProUGUI _balanceText;

        [SerializeField] private ItemCard _itemCardPrefab;

        private List<ItemCard> _itemCards = new();

        private Cart _cachedCart;

        private void Awake()
        {
            // todo thuis really sucks, make global singleton now we're committing to one cart
            _cachedCart = FindAnyObjectByType<Cart>();
        }

        public void Build(Shop shop)
        {
            _shop = shop;

            for (var i = 0; i < shop.PurchasableItems.Count; i++)
            {
                var item = shop.PurchasableItems[i];
                ItemCard itemCard = Instantiate(_itemCardPrefab, transform);
                itemCard.Build(item, i, shop);

                itemCard.WorldFollowUI.TrackingTarget = item.transform;
                itemCard.WorldFollowUI.TrackingOffset = Vector3.up * 1.25f;

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
        public void SellAll() => _shop.SellAll();
    }
}