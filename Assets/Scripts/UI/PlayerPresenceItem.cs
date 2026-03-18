using Gilzoide.RoundedCorners;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class PlayerPresenceItem : MonoBehaviour
    {
        [SerializeField] private Color _joinBackground;
        [SerializeField] private Color _leaveBackground;

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RoundedRect _backgroundImage;
        [SerializeField] private RoundedImage _catFaceIcon;
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private string _template = "<b>[[name]]</b> [[activity]]";

        public void Build(PlayerController player, PlayerPresenceFeed.PresenceType presenceType)
        {
            _catFaceIcon.Sprite = PlayerController.SkinIcons[player.PlayerSkinIndex];
            _backgroundImage.color = presenceType == PlayerPresenceFeed.PresenceType.Join ? _joinBackground : _leaveBackground;
            _label.text = _template
                .Replace("[[name]]", player.PlayerName)
                .Replace("[[activity]]", presenceType == PlayerPresenceFeed.PresenceType.Join ? "joined" : "left");
            
            var rt = (RectTransform)transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            
            Sequence.Create()
                .Group(Tween.Alpha(_canvasGroup, 1f, 1f, Ease.OutCubic))
                .Group(Tween.UIAnchoredPositionX(rt, rt.anchoredPosition.x - rt.sizeDelta.x, rt.anchoredPosition.x, 1f, Ease.OutCubic))
                .ChainDelay(2f)
                .Chain(Tween.Alpha(_canvasGroup, 0f, 1f, Ease.InCubic))
                .Group(Tween.UIAnchoredPositionX(rt, rt.anchoredPosition.x, rt.anchoredPosition.x - rt.sizeDelta.x, 1f, Ease.InCubic))
                .OnComplete(() => Destroy(gameObject), false);
        }

        private void Awake()
        {
            _canvasGroup.alpha = 0;
        }
    }
}