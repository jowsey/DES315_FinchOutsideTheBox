using System;
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
            PutDown
        }

        [field: SerializeField] public WorldFollowUI WorldFollowUI { get; private set; }
        [SerializeField] private TextMeshProUGUI _promptLabel;

        [SerializeField] private InteractionType _interactionType;

        [SerializeField] private string _pickUpText = "Pick up";
        [SerializeField] private string _putDownText = "Put down";

        private void OnValidate()
        {
            if (!_promptLabel) _promptLabel = GetComponentInChildren<TextMeshProUGUI>();
            if (!WorldFollowUI) WorldFollowUI = GetComponent<WorldFollowUI>();

            Build(_interactionType);
        }

        public void Build(InteractionType interactionType)
        {
            _promptLabel.text = interactionType switch
            {
                InteractionType.PickUp => _pickUpText,
                InteractionType.PutDown => _putDownText,
                _ => throw new ArgumentOutOfRangeException(nameof(interactionType), interactionType, null)
            };

            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
        }
    }
}