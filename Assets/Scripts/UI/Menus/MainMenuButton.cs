using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    [RequireComponent(typeof(Button), typeof(TextMeshProUGUI))]
    public class MainMenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public AK.Wwise.Event buttonSfx;

        public Button Button { get; private set; }
        private TextMeshProUGUI _text;

        private Color _originalColor;
        [SerializeField] private Color _highlightColor = Color.hotPink;

        private static Texture2D _highlightCursor;
        public bool Active;

        private void Awake()
        {
            if (!_highlightCursor) _highlightCursor = Resources.Load<Texture2D>("UI/paw");

            Button = GetComponent<Button>();
            _text = GetComponent<TextMeshProUGUI>();
            _originalColor = _text.color;


            if (Active)
            {
                _text.color = _highlightColor;
                _text.fontStyle |= FontStyles.Bold;
            }
        }

        public void SetActive(bool val)
        {
            Active = val;
            if (Active)
            {
                _text.color = _highlightColor;
                _text.fontStyle |= FontStyles.Bold;
            }
            else
            {
                _text.color = _originalColor;
                _text.fontStyle &= ~FontStyles.Bold;
            }
        }

        private void Highlight()
        {
            if (!Button.interactable) { return; }
            if (!Active)
            {
                _text.color = _highlightColor;
                _text.fontStyle |= FontStyles.Bold;
                
            }
        }
        private void Unhighlight()
        {
            if (!Active)
            {
                _text.color = _originalColor;
                _text.fontStyle &= ~FontStyles.Bold;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
            Cursor.SetCursor(_highlightCursor, new Vector2(_highlightCursor.width / 2f, _highlightCursor.height / 2f), CursorMode.Auto);
            Highlight();
            buttonSfx.Post(gameObject);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            Unhighlight();
        }

        public void OnSelect(BaseEventData eventData)
        {
            Highlight();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            Unhighlight();
        }
    }
}