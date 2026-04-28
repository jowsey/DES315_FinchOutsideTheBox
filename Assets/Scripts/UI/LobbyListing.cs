using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class LobbyListing : MonoBehaviour
    {
        [field: SerializeField] public TextMeshProUGUI LobbyNameText { get; private set; }
        [field: SerializeField] public TextMeshProUGUI MetadataText { get; private set; }
        [field: SerializeField] public LoadingButton JoinButton { get; private set; }
    }
}