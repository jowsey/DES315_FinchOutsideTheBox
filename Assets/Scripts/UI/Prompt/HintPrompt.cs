using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;

namespace UI
{
    public class HintPrompt : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float TransitionDuration = 0.75f;
        private static HintPrompt _prefab;

        public class HintPromptData
        {
            public string Title;
            public string Description;
            public float ShowDuration = 15f;
        }

        public class TutorialPromptShownStates
        {
            public bool CaravanControls;
            public bool PickupTreasure;
            public bool ReachCheckpoint;
            public bool Shop;
            public bool BalanceBeam;
            public bool PressurePlate;
        }

        public static TutorialPromptShownStates HasShown = new();

        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _bodyText;

        [SerializeField] private RectTransform _timerBarBackground;
        [SerializeField] private RectTransform _timerBar;

        [SerializeField] private RectTransform _catIcon;

        [SerializeField] private CanvasGroup _canvasGroup;

        private HintPromptData _data;

        private float _startTime;
        private bool _done;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static async void LoadPrefabAsset()
        {
            var handle = Addressables.LoadAssetAsync<GameObject>("UI/HintPrompt");
            await handle.Task;

            _prefab = handle.Result.GetComponent<HintPrompt>();
        }

        public static void RequestNew(HintPromptData data)
        {
            if (SettingsManager.ActiveSettings.HideTutorialPrompts) return;
            var instance = Instantiate(_prefab, UIGlobals.MainCanvas.transform);
            instance.Build(data);
        }

        public void Build(HintPromptData data)
        {
            _data = data;

            _titleText.text = data.Title;
            _bodyText.text = data.Description;

            _startTime = Time.time;

            transform.localScale = Vector3.zero;
            Tween.Scale(transform, Vector3.one, TransitionDuration, Ease.OutBack);

            Tween.ScaleY(_catIcon, 1.05f, 0.5f, Ease.OutSine, -1, CycleMode.Rewind);
        }

        private void Update()
        {
            if (_done || _data == null) return;

            var maxSize = _timerBarBackground.rect.width;
            var elapsed = Time.time - _startTime;
            var progress = Mathf.Clamp01(elapsed / _data.ShowDuration);
            var newRightOffset = maxSize * (1f - progress);

            _timerBar.offsetMax = new Vector2(-newRightOffset, _timerBar.offsetMax.y);

            if (progress >= 1f) Destroy();
        }

        private void Destroy()
        {
            _done = true;
            Tween.Scale(transform, Vector3.zero, TransitionDuration, Ease.InBack)
                .OnComplete(() => Destroy(gameObject));
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Tween.Alpha(_canvasGroup, 0.15f, 0.25f, Ease.OutCubic);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Tween.Alpha(_canvasGroup, 1f, 0.25f, Ease.OutCubic);
        }
    }
}