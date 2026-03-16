using Gilzoide.RoundedCorners;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class PlayerPresenceItem : MonoBehaviour
    {
        public static Sprite[] SkinIcons;

        [SerializeField] private Color _joinBackground;
        [SerializeField] private Color _leaveBackground;

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RoundedRect _backgroundImage;
        [SerializeField] private RoundedImage _catFaceIcon;
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private string _template = "<b>[[name]]</b> [[activity]]";

        public void Render(string playerName, int skin, PlayerPresenceFeed.PresenceType presenceType)
        {
            _catFaceIcon.Sprite = SkinIcons[skin];
            _backgroundImage.color = presenceType == PlayerPresenceFeed.PresenceType.Join ? _joinBackground : _leaveBackground;
            _label.text = _template
                .Replace("[[name]]", playerName)
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
            SkinIcons ??= Resources.LoadAll<Sprite>("PlayerSkins/Icons");

            _canvasGroup.alpha = 0;
        }
    }
}