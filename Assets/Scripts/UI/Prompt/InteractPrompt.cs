using System;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class InteractPrompt : MonoBehaviour
    {
        public enum InteractionType
        {
            PickUp,
            PutDown,
            EnterShop
        }

        [field: SerializeField] public WorldFollowUI WorldFollowUI { get; private set; }
        [SerializeField] private TextMeshProUGUI _promptLabel;

        [SerializeField] private InteractionType _interactionType;

        [SerializeField] private string _pickUpText = "Pick up";
        [SerializeField] private string _putDownText = "Put down";
        [SerializeField] private string _enterShopText = "Enter shop";

        [SerializeField] private float _transitionDuration = 0.25f;

        private void OnValidate()
        {
            if (!_promptLabel) _promptLabel = GetComponentInChildren<TextMeshProUGUI>();
            if (!WorldFollowUI) WorldFollowUI = GetComponent<WorldFollowUI>();
            
            Build(_interactionType);
        }

        private void OnEnable()
        {
            transform.localScale = Vector3.zero;
            Tween.Scale(transform, Vector3.one, _transitionDuration, Ease.OutCubic);
        }

        public void Build(InteractionType interactionType)
        {
            _promptLabel.text = interactionType switch
            {
                InteractionType.PickUp => _pickUpText,
                InteractionType.PutDown => _putDownText,
                InteractionType.EnterShop => _enterShopText,
                _ => throw new ArgumentOutOfRangeException(nameof(interactionType), interactionType, null)
            };

            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
        }

        public void Destroy()
        {
            Tween.Scale(transform, Vector3.one, Vector3.zero, _transitionDuration, Ease.InBack)
                .OnComplete(() => Destroy(gameObject));
        }
    }
}