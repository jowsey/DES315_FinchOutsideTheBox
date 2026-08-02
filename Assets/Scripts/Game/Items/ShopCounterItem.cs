using UnityEngine;
using Util;

namespace Game.Items
{
    [RequireComponent(typeof(Item), typeof(OutlineTarget))]
    public class ShopCounterItem : MonoBehaviour
    {
        private ItemData _itemData;
        private OutlineTarget _outline;

        private readonly Color _standardColour = Color.whiteSmoke;
        private readonly Color _validColour = Color.lightGreen;
        private readonly Color _invalidColour = Color.softRed;

        private bool _selected;

        public void Build(ItemData data)
        {
            _itemData = data;
        }

        private void Awake()
        {
            _outline = GetComponent<OutlineTarget>();
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            _outline.WidthFactor = selected ? 1f : 0.5f;
            if (!selected) _outline.Colour = _standardColour;
        }

        private void Update()
        {
            if (!_selected) return;

            _outline.Colour = BankManager.Instance.Balance >= _itemData.BuyPrice
                ? _validColour
                : _invalidColour;
        }

        private void OnDestroy()
        {
            Destroy(_outline);
        }
    }
}