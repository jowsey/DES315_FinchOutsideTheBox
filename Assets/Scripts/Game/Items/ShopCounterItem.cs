using UnityEngine;
using UnityEngine.Events;
using Util;

namespace Game.Items
{
    [RequireComponent(typeof(OutlineTarget))]
    public class ShopCounterItem : MonoBehaviour
    {
        private readonly Color _standardColour = Color.whiteSmoke;
        private readonly Color _validColour = Color.lightGreen;
        private readonly Color _invalidColour = Color.softRed;

        public ItemData ItemData;
        
        public UnityEvent<bool> OnSelectedChange = new();
        
        public OutlineTarget Outline { get; private set; }
        private bool _selected;

        private void Awake()
        {
            Outline = GetComponent<OutlineTarget>();
        }

        private void OnEnable()
        {
            Outline.enabled = true;
        }

        private void OnDisable()
        {
            Outline.enabled = false;
        }

        public void SetSelected(bool selected)
        {
            Outline.WidthFactor = selected ? 1f : 0.35f;
            if (!selected) Outline.Colour = _standardColour;

            _selected = selected;
            OnSelectedChange?.Invoke(selected);
        }

        private void Update()
        {
            if (!_selected) return;

            Outline.Colour = BankManager.Instance.Balance >= ItemData.BuyPrice
                ? _validColour
                : _invalidColour;
        }
    }
}