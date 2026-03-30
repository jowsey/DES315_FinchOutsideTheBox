using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class CreateLobbyButton : MonoBehaviour
    {
        public string DefaultText { get; private set; }

        [field: SerializeField] [field: Required] public Button Button { get; private set; }
        [field: SerializeField] [field: Required] public TextMeshProUGUI LabelText { get; private set; }

        [field: SerializeField] public string LoadingText { get; private set; } = "Creating...";

        private void OnValidate()
        {
            if (!Button) Button = GetComponentInChildren<Button>();
            if (!LabelText) LabelText = GetComponentInChildren<TextMeshProUGUI>();
        }

        private void Awake()
        {
            DefaultText = LabelText.text;
        }
    }
}