using System.Linq;
using Gilzoide.RoundedCorners;
using Mirror;
using PrimeTween;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class TextChatItem : MonoBehaviour
    {
        [SerializeField] [Required] private CanvasGroup _canvasGroup;
        [SerializeField] [Required] private RoundedImage _catFaceIcon;
        [SerializeField] [Required] private TextMeshProUGUI _playerNameText;
        [SerializeField] [Required] private TextMeshProUGUI _messageText;

        [SerializeField] private float _displayDuration = 10f;

        private void Awake()
        {
            _canvasGroup.alpha = 0;
        }

        public void Build(PlayerController player, string message)
        {
            _catFaceIcon.Sprite = PlayerController.LoadedSkins[player.PlayerSkinIndex].Icon;
            _playerNameText.text = player.PlayerName;

            var playerInfos = FindObjectsByType<PlayerController>(FindObjectsSortMode.None)
                .Select(p => (p.PlayerName, PlayerController.LoadedSkins[p.PlayerSkinIndex].AccentColor));

            _messageText.text = message;
            foreach (var i in playerInfos)
            {
                _messageText.text = _messageText.text.Replace($"@{i.PlayerName}", $"<color=#{ColorUtility.ToHtmlStringRGB(i.AccentColor)}><b>@{i.PlayerName}</b></color>");
            }

            var rt = (RectTransform)transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            _playerNameText.ForceMeshUpdate();
            _messageText.ForceMeshUpdate();

            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            const float animationDuration = 0.5f;

            Sequence.Create()
                .Group(Tween.Alpha(_canvasGroup, 1f, animationDuration, Ease.OutCubic))
                .Group(Tween.Scale(rt, Vector3.zero, Vector3.one, animationDuration, Ease.OutBack))
                .ChainDelay(_displayDuration)
                .Chain(Tween.Alpha(_canvasGroup, 0f, animationDuration, Ease.InCubic))
                .Group(Tween.Scale(rt, Vector3.one, Vector3.zero, animationDuration, Ease.InBack))
                .OnComplete(() => Destroy(gameObject), false);
        }
    }
}