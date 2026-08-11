using System.Collections.Generic;
using PrimeTween;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace Util
{
    [RequireComponent(typeof(CanvasGroup))]
    public class GloballyHiddenGroup : MonoBehaviour
    {
        private static readonly HashSet<Object> _hideSources = new();

        public static bool Hidden => _hideSources.Count > 0;

        public static void AddHideSource(Object source)
        {
            _hideSources.Add(source);
            if (_hideSources.Count == 1) _onHide.Invoke();
        }

        public static void RemoveHideSource(Object source)
        {
            _hideSources.Remove(source);
            if (_hideSources.Count == 0) _onShow.Invoke();
        }

        private static readonly UnityEvent _onHide = new();
        private static readonly UnityEvent _onShow = new();

        private CanvasGroup _canvasGroup;

        private void OnEnable()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _onHide.AddListener(Hide);
            _onShow.AddListener(Show);
        }

        private void OnDisable()
        {
            _onHide.RemoveListener(Hide);
            _onShow.RemoveListener(Show);
        }

        private void Hide()
        {
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            Tween.Alpha(_canvasGroup, 0f, 0.5f, Ease.OutCubic);
        }

        private void Show()
        {
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
            Tween.Alpha(_canvasGroup, 1f, 0.5f, Ease.OutCubic);
        }
    }
}