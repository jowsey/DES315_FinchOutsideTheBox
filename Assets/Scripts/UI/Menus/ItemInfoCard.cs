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
        public enum SubtextDisplayType
        {
            None,
            BuyPrice,
            SellPrice,
            BuySpeculate,
            UsageHint
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
            SetVisible(true);
        }

        public void SetVisible(bool visible)
        {
            Tween.Scale(transform, visible ? Vector3.one : Vector3.zero, _transitionDuration, Ease.OutCubic);
        }

        public void Build(ItemData data, SubtextDisplayType subtextDisplayType)
        {
            _nameText.text = data.ItemName;
            _descriptionText.text = data.Description;
            _priceText.text = subtextDisplayType switch
            {
                SubtextDisplayType.None => string.Empty,
                SubtextDisplayType.BuyPrice => $"<size=150%>Buy for <b>{data.BuyPrice}</b> coins",
                SubtextDisplayType.SellPrice => $"<size=150%>Sell for <b>{data.SellPrice}</b> coins",
                SubtextDisplayType.BuySpeculate => "Could be worth something in the shop...",
                SubtextDisplayType.UsageHint => "Items can be used to overcome tough obstacles.",
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