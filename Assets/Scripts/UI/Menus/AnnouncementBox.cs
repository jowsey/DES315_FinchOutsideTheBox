using System;
using System.Collections;
using System.Text.RegularExpressions;
using Gilzoide.RoundedCorners;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace UI.Menus
{
    public class AnnouncementBox : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float TransitionDuration = 0.1f;
        private RectTransform _rt => (RectTransform)transform;

        [SerializeField] private string _rssFeedUrl = "https://fotb.itch.io/loose-juice/devlog.rss";

        [SerializeField] private Graphic[] _backgroundGraphics;
        [SerializeField] private RoundedImage _heroImage;

        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _bodyText;
        [SerializeField] private TextMeshProUGUI _ctaText;
        [SerializeField] private Image _ctaIcon;

        [SerializeField] private Color _lightColour;
        [SerializeField] private Color _darkColour;

        private string _itemLink;
        private float _standardAnchoredY;

        private Texture2D _downloadedTexture;
        private Sprite _generatedSprite;

        public void OpenLink() => Application.OpenURL(_itemLink);

        private IEnumerator Start()
        {
            // hide until ready
            _standardAnchoredY = _rt.anchoredPosition.y;
            _rt.anchoredPosition = new Vector2(_rt.anchoredPosition.x, _standardAnchoredY - _rt.sizeDelta.y);

            using var req = UnityWebRequest.Get(_rssFeedUrl);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var rssFeed = new System.Xml.XmlDocument();
                rssFeed.LoadXml(req.downloadHandler.text);

                var latestItemNode = rssFeed.SelectSingleNode("//item");

                if (latestItemNode == null) throw new System.Exception("No <item> found in RSS feed");

                var titleNode = latestItemNode.SelectSingleNode("title");
                var linkNode = latestItemNode.SelectSingleNode("link");
                var descriptionNode = latestItemNode.SelectSingleNode("description");

                var titleText = titleNode?.InnerText ?? "";
                var linkText = linkNode?.InnerText ?? "";
                var descText = descriptionNode?.InnerText ?? "";

                _itemLink = linkText;

                var descMatch = Regex.Match(descText, "<p>(.*?)</p>");
                var extractedDesc = descMatch.Groups[1].Value;

                if (titleNode != null) _titleText.text = titleText;
                if (descriptionNode != null) _bodyText.text = extractedDesc;

                var imgMatch = Regex.Match(descText, "<img[^>]+src=\"([^\"]+)\"");
                var imageUrl = imgMatch.Success ? imgMatch.Groups[1].Value : null;

                if (imageUrl == null)
                {
                    _heroImage.enabled = false;
                }
                else
                {
                    using var imageReq = UnityWebRequestTexture.GetTexture(imageUrl);
                    yield return imageReq.SendWebRequest();

                    if (imageReq.result == UnityWebRequest.Result.Success)
                    {
                        _downloadedTexture = DownloadHandlerTexture.GetContent(imageReq);
                        _generatedSprite = Sprite.Create(_downloadedTexture, new Rect(0, 0, _downloadedTexture.width, _downloadedTexture.height), Vector2.zero);
                        _heroImage.Sprite = _generatedSprite;
                    }
                    else
                    {
                        Debug.LogError($"Failed to fetch post image: {imageReq.error}");
                        _heroImage.enabled = false;
                    }
                }
            }
            else
            {
                Debug.LogError($"Failed to fetch RSS feed: {req.error}");
                yield break;
            }

            // appear
            Tween.UIAnchoredPositionY(_rt, _standardAnchoredY, 1f, Ease.OutBack);
        }

        private void OnDestroy()
        {
            if (_downloadedTexture) Destroy(_downloadedTexture);
            if (_generatedSprite) Destroy(_generatedSprite);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Cursor.SetCursor(MainMenuButton.HighlightCursor, new Vector2(MainMenuButton.HighlightCursor.width / 2f, MainMenuButton.HighlightCursor.height / 2f), CursorMode.Auto);

            foreach (var graphic in _backgroundGraphics) Tween.Color(graphic, _darkColour, TransitionDuration, Ease.OutCubic);
            Tween.Color(_titleText, _lightColour, TransitionDuration, Ease.OutCubic);
            Tween.Color(_bodyText, _lightColour, TransitionDuration, Ease.OutCubic);
            Tween.Color(_ctaText, _lightColour, TransitionDuration, Ease.OutCubic);
            Tween.Color(_ctaIcon, _lightColour, TransitionDuration, Ease.OutCubic);

            Tween.UIAnchoredPositionY(_rt, _standardAnchoredY + 8f, TransitionDuration, Ease.OutCubic);

            _ctaText.fontStyle |= FontStyles.Underline;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

            foreach (var graphic in _backgroundGraphics) Tween.Color(graphic, _lightColour, TransitionDuration, Ease.OutCubic);
            Tween.Color(_titleText, _darkColour, TransitionDuration, Ease.OutCubic);
            Tween.Color(_bodyText, _darkColour, TransitionDuration, Ease.OutCubic);
            Tween.Color(_ctaText, _darkColour, TransitionDuration, Ease.OutCubic);
            Tween.Color(_ctaIcon, _darkColour, TransitionDuration, Ease.OutCubic);

            Tween.UIAnchoredPositionY(_rt, _standardAnchoredY, TransitionDuration, Ease.OutCubic);

            _ctaText.fontStyle &= ~FontStyles.Underline;
        }
    }
}