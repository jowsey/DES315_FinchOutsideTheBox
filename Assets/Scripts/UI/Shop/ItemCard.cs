using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    public class ItemCard : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [field: SerializeField] public WorldFollowUI WorldFollowUI { get; private set; }

        [SerializeField] private CanvasGroup _hoverCta;

        [SerializeField] private Outline _outline;
        [SerializeField] private Color _hoveredOutline = Color.white;
        private Color _initialOutline;

        [SerializeField] private TextMeshProUGUI _itemNameText;
        [SerializeField] private TextMeshProUGUI _itemDescriptionText;
        [SerializeField] private TextMeshProUGUI _itemPriceText;

        [SerializeField] private RectTransform _contentRectTransform;
        private float _initialHeight;

        [SerializeField] [Min(0)] private float _transitionDuration = 0.25f;

        private Shop _shop;
        private int _shopIndex;

        private void OnValidate()
        {
            if (!WorldFollowUI) WorldFollowUI = GetComponent<WorldFollowUI>();
            if (!_outline) _outline = GetComponent<Outline>();
        }

        private void OnEnable()
        {
            _initialHeight = ((RectTransform)transform).sizeDelta.y;
            _initialOutline = _outline.effectColor;
        }

        public void Build(Item item, int index, Shop shop)
        {
            var stylisedName = item.Type.ToString(); // todo actual name
            name = $"ItemCard: {stylisedName}";

            _itemNameText.text = stylisedName;
            _itemDescriptionText.text = $"This is a {item.Type.ToString().ToLower()}. Here is a placeholder description."; // todo actual description
            _itemPriceText.text = $"Purchase for <b>{shop.EconomySettings.ItemBuyPrices[item.Type]}</b> coins";

            _shop = shop;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _shop.CmdTryBuy(_shopIndex);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            var rt = (RectTransform)transform;
            Tween.UISizeDelta(rt, new Vector2(rt.sizeDelta.x, _contentRectTransform.sizeDelta.y), _transitionDuration, Ease.OutCubic);
            Tween.Scale(rt, Vector3.one * 1.05f, _transitionDuration, Ease.OutCubic);
            Tween.Alpha(_hoverCta, 0, _transitionDuration, Ease.OutCubic);

            _outline.effectColor = _hoveredOutline;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            var rt = (RectTransform)transform;
            Tween.UISizeDelta(rt, new Vector2(rt.sizeDelta.x, _initialHeight), _transitionDuration, Ease.OutCubic);
            Tween.Scale(rt, Vector3.one, _transitionDuration, Ease.OutCubic);
            Tween.Alpha(_hoverCta, 1, _transitionDuration, Ease.OutCubic);

            _outline.effectColor = _initialOutline;
        }
    }
}