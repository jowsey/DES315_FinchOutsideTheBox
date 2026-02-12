using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    [RequireComponent(typeof(Button), typeof(TextMeshProUGUI))]
    public class MainMenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private TextMeshProUGUI _text;

        private Color _originalColor;
        [SerializeField] private Color _highlightColor = Color.hotPink;

        private static Texture2D _highlightCursor;

        private void Awake()
        {
            if (!_highlightCursor) _highlightCursor = Resources.Load<Texture2D>("UI/paw");

            _text = GetComponent<TextMeshProUGUI>();
            _originalColor = _text.color;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _text.color = _highlightColor;
            _text.fontStyle |= FontStyles.Bold;

            Cursor.SetCursor(_highlightCursor, new Vector2(_highlightCursor.width / 2f, _highlightCursor.height / 2f), CursorMode.Auto);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _text.color = _originalColor;
            _text.fontStyle &= ~FontStyles.Bold;

            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }
}