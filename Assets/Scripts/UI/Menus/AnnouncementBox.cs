using System;
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

        [SerializeField] private TextMeshProUGUI _versionText;

        private string _itemLink;
        private float _standardAnchoredY;

        private Texture2D _downloadedTexture;
        private Sprite _generatedSprite;

        public void OpenLink() => Application.OpenURL(_itemLink);

        private const string SemverPattern =
            @"(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+(?<buildmetadata>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?";

        // true if A is higher (newer) than B
        private static bool CompareSemver(Match a, Match b)
        {
            // official semver regex. good grief
            if (!a.Success)
            {
                Debug.LogWarning($"Failed to parse semver string: '{a}'");
                return false;
            }

            if (!b.Success)
            {
                Debug.LogWarning($"Failed to parse semver string: '{b}'");
                return false;
            }

            // we only care about major/minor/patch, because no fancier version will ever be published (hopefully)
            var aMajor = int.Parse(a.Groups["major"].Value);
            var bMajor = int.Parse(b.Groups["major"].Value);
            if (aMajor != bMajor) return aMajor > bMajor;

            var aMinor = int.Parse(a.Groups["minor"].Value);
            var bMinor = int.Parse(b.Groups["minor"].Value);
            if (aMinor != bMinor) return aMinor > bMinor;

            var aPatch = int.Parse(a.Groups["patch"].Value);
            var bPatch = int.Parse(b.Groups["patch"].Value);
            if (aPatch != bPatch) return aPatch > bPatch;

            return false;
        }

        private async Awaitable<bool> AttemptPostDownload()
        {
            using var req = UnityWebRequest.Get(_rssFeedUrl);
            await req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var rssFeed = new System.Xml.XmlDocument();
                rssFeed.LoadXml(req.downloadHandler.text);

                var items = rssFeed.SelectNodes("//item");
                if (items == null) return false;
                if (items.Count == 0) return false;

                var localVersionMatch = Regex.Match(Application.version, SemverPattern);

                // Find first post with title matching semver
                for (var i = 0; i < items.Count; i++)
                {
                    var itemNode = items[i];

                    var title = itemNode?.SelectSingleNode("title");
                    if (title == null) continue;

                    var text = title.InnerText;
                    var semverMatch = Regex.Match(text, SemverPattern);
                    if (semverMatch.Success && CompareSemver(semverMatch, localVersionMatch))
                    {
                        // Found a newer version post
                        _versionText.text += $"\n<size=50%>New version available: <b>v{semverMatch.Value}</b>";
                        break;
                    }
                }

                // Display latest post
                var latestItemNode = items![0];
                if (latestItemNode == null) return false;

                var titleNode = latestItemNode.SelectSingleNode("title");
                var linkNode = latestItemNode.SelectSingleNode("link");
                var descriptionNode = latestItemNode.SelectSingleNode("description");

                var titleText = titleNode?.InnerText ?? "";
                var linkText = linkNode?.InnerText ?? "";
                var descText = descriptionNode?.InnerText ?? "";

                _itemLink = linkText;

                var descMatch = Regex.Match(descText, "<p>(.*?)</p>");
                var extractedDesc = descMatch.Groups[1].Value;

                _titleText.text = titleText;
                _bodyText.text = extractedDesc;

                var imgMatch = Regex.Match(descText, "<img[^>]+src=\"([^\"]+)\"");
                var imageUrl = imgMatch.Success ? imgMatch.Groups[1].Value : null;

                if (imageUrl == null)
                {
                    _heroImage.enabled = false;
                }
                else
                {
                    using var imageReq = UnityWebRequestTexture.GetTexture(imageUrl);
                    await imageReq.SendWebRequest();

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
                return false;
            }

            return true;
        }

        private async void Start()
        {
            try
            {
                // hide until ready
                _standardAnchoredY = _rt.anchoredPosition.y;
                _rt.anchoredPosition = new Vector2(_rt.anchoredPosition.x, _standardAnchoredY - _rt.sizeDelta.y);

                var success = false;
                var triesRemaining = 3;
                while (!success && triesRemaining-- > 0)
                {
                    await Awaitable.WaitForSecondsAsync(1f);
                    success = await AttemptPostDownload();
                }

                // appear on success
                if (success) _ = Tween.UIAnchoredPositionY(_rt, _standardAnchoredY, 1f, Ease.OutBack);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error while fetching post: {e}");
            }
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