using Gilzoide.RoundedCorners;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class PlayerPresenceItem : MonoBehaviour
    {
        [SerializeField] private Sprite _greenCatFace;
        [SerializeField] private Sprite _blueCatFace;

        [SerializeField] private Color _joinColour;
        [SerializeField] private Color _leaveColour;

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RoundedRect _backgroundImage;
        [SerializeField] private RoundedImage _catFaceIcon;
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private string _template = "<b>[[name]]</b> [[activity]]";

        public void Render(string playerName, PlayerPresenceFeed.CatSkin skin, PlayerPresenceFeed.PresenceType presenceType)
        {
            _catFaceIcon.Sprite = skin == PlayerPresenceFeed.CatSkin.Green ? _greenCatFace : _blueCatFace;
            _backgroundImage.color = presenceType == PlayerPresenceFeed.PresenceType.Join ? _joinColour : _leaveColour;
            _label.text = _template
                .Replace("[[name]]", playerName)
                .Replace("[[activity]]", presenceType == PlayerPresenceFeed.PresenceType.Join ? "joined" : "left");
            
            var rt = (RectTransform)transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            Sequence.Create()
                .Group(Tween.Alpha(_canvasGroup, 0f, 1f, 1f, Ease.OutCubic))
                .Group(Tween.UIAnchoredPositionX(rt, rt.anchoredPosition.x - rt.sizeDelta.x, rt.anchoredPosition.x, 1f, Ease.OutCubic))
                .ChainDelay(2f)
                .Chain(Tween.Alpha(_canvasGroup, 1f, 0f, 1f, Ease.InCubic))
                .Group(Tween.UIAnchoredPositionX(rt, rt.anchoredPosition.x, rt.anchoredPosition.x - rt.sizeDelta.x, 1f, Ease.InCubic))
                .OnComplete(() => Destroy(gameObject), false);
        }

        private void Awake()
        {
            if (!_greenCatFace) _greenCatFace = Resources.Load<Sprite>("UI/GreenCatFace");
            if (!_blueCatFace) _blueCatFace = Resources.Load<Sprite>("UI/BlueCatFace");
        }
    }
}