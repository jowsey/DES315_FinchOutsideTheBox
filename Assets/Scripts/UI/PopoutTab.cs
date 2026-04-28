using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI
{
    public class PopoutTab : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] [Required] private RectTransform _hiddenTab;
        [SerializeField] [Required] private CanvasGroup _hiddenGroup;

        private void Start()
        {
            _hiddenGroup.interactable = false;
            _hiddenGroup.blocksRaycasts = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            var rt = (RectTransform)transform;
            Tween.UIAnchoredPositionX(rt, _hiddenTab.sizeDelta.x, 0.25f, Ease.OutCubic);

            _hiddenGroup.interactable = true;
            _hiddenGroup.blocksRaycasts = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            var rt = (RectTransform)transform;
            Tween.UIAnchoredPositionX(rt, 0, 0.25f, Ease.InCubic);

            _hiddenGroup.interactable = false;
            _hiddenGroup.blocksRaycasts = false;
        }
    }
}