using System.Linq;
using Game.Items;
using PrimeTween;
using TMPro;
using UnityEngine;

namespace UI.Shop
{
    public class ShopUI : MonoBehaviour
    {
        private Game.Shop _shop;

        [SerializeField] private TextMeshProUGUI _sellAllEstimateText;
        [SerializeField] private TextMeshProUGUI _balanceText;
        [SerializeField] private TextMeshProUGUI _errorText;

        [SerializeField] private ItemInfoCard _itemCardPrefab;

        [SerializeField] private AK.Wwise.Event _sellSfx;

        private void OnEnable()
        {
            if (_shop) _shop.OnReceiveBuyResult.AddListener(OnReceiveBuyResult);
        }

        private void OnDisable()
        {
            if (_shop) _shop.OnReceiveBuyResult.RemoveListener(OnReceiveBuyResult);
        }

        private void OnReceiveBuyResult(Item item, Game.Shop.PurchaseError result)
        {
            if (result == Game.Shop.PurchaseError.None) return;

            _errorText.text = result switch
            {
                Game.Shop.PurchaseError.NotEnoughMoney => "You don't have enough coins to purchase this.",
                Game.Shop.PurchaseError.AlreadyHoldingObject => "You are already holding an object.",
                _ => "An unknown error occurred :\\"
            };

            Tween.Color(_errorText, Color.red, _errorText.color, 0.5f, Ease.OutCubic);
        }

        public void Build(Game.Shop shop)
        {
            _shop = shop;
            shop.OnReceiveBuyResult.AddListener(OnReceiveBuyResult);

            foreach (var counterItem in shop.AvailableItems.Select(i => i.GetComponent<ShopCounterItem>()).Append(shop.SackItem))
            {
                if (!counterItem) continue;

                var itemCard = Instantiate(_itemCardPrefab, transform);
                itemCard.Build(counterItem.ItemData, ItemInfoCard.ItemInfoCardPriceDisplay.BuyPrice);

                itemCard.WorldFollowUI.TrackingTarget = counterItem.transform;
                ((RectTransform)itemCard.transform).pivot = new Vector2(0, 0.5f);
                itemCard.WorldFollowUI.UIPositionOffset = new Vector2(48, 0);

                itemCard.gameObject.SetActive(false);

                counterItem.OnSelectedChange.AddListener(itemCard.gameObject.SetActive);
            }
        }

        private void Update()
        {
            _sellAllEstimateText.text = $"You will receive <b>{Cart.Instance.ExpectedTotalItemSellPrice}</b> coins.";
            _balanceText.text = BankManager.Instance.Balance.ToString();
        }

        public void Leave() => _shop.LeaveShop();

        public void SellAll()
        {
            if (Cart.Instance.ExpectedTotalItemSellPrice > 0)
            {
                _sellSfx.Post(gameObject);
                _shop.CmdSellAll();
            }
        }
    }
}