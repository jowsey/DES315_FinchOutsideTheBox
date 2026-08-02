using Game.Items;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    [RequireComponent(typeof(WorldFollowUI))]
    public class ItemInfoCard : MonoBehaviour
    {
        public enum ItemInfoCardPriceDisplay
        {
            None,
            BuyPrice,
            SellPrice
        }
        
        [field: SerializeField] public WorldFollowUI WorldFollowUI { get; private set; }

        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private TextMeshProUGUI _priceText;

        [SerializeField] private Image[] _rarityIcons;

        [SerializeField] private float _rarityIconTransitionDurationPerTier = 0.25f;
        [SerializeField] private float _transitionDuration = 0.25f;

        private void OnValidate()
        {
            if (!WorldFollowUI) WorldFollowUI = GetComponent<WorldFollowUI>();
        }

        private void OnEnable()
        {
            transform.localScale = Vector3.zero;
            Tween.Scale(transform, Vector3.one, _transitionDuration, Ease.OutCubic);
        }

        public void Build(ItemData data, ItemInfoCardPriceDisplay priceDisplay)
        {
            _nameText.text = data.ItemName;
            _descriptionText.text = data.Description;
            _priceText.text = priceDisplay switch
            {
                ItemInfoCardPriceDisplay.BuyPrice => $"Buy for <b>{data.BuyPrice}</b> coins",
                ItemInfoCardPriceDisplay.SellPrice => $"Sell for <b>{data.SellPrice}</b> coins",
                _ => _priceText.text
            };

            for (var i = 0; i < _rarityIcons.Length; i++)
            {
                var icon = _rarityIcons[i];
                icon.enabled = false;

                if (i > (int)data.Rarity) continue;

                PrimeTweenConfig.warnZeroDuration = false;
                Tween.Delay(_rarityIconTransitionDurationPerTier * 0.5f * i).OnComplete(() =>
                {
                    icon.enabled = true;
                    Tween.Alpha(icon, 0f, 1f, _rarityIconTransitionDurationPerTier, Ease.OutCubic);
                    Tween.Scale(icon.transform, Vector3.one * 1.5f, Vector3.one, _rarityIconTransitionDurationPerTier, Ease.OutCubic);
                });
                PrimeTweenConfig.warnZeroDuration = true;
            }
        }

        public void Destroy()
        {
            Tween.Scale(transform, Vector3.one, Vector3.zero, _transitionDuration, Ease.InBack)
                .OnComplete(() => Destroy(gameObject));
        }
    }
}