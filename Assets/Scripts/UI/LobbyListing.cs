using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class LobbyListing : MonoBehaviour
    {
        public string DefaultText { get; private set; }

        [field: SerializeField] public TextMeshProUGUI LobbyNameText { get; private set; }
        [field: SerializeField] public TextMeshProUGUI MetadataText { get; private set; }
        [field: SerializeField] public Button JoinButton { get; private set; }

        public TextMeshProUGUI JoinButtonText { get; private set; }

        [field: SerializeField] public string JoiningText { get; private set; } = "Joining...";

        private void Awake()
        {
            JoinButtonText = JoinButton.GetComponentInChildren<TextMeshProUGUI>();
            DefaultText = JoinButtonText.text;
        }
    }
}