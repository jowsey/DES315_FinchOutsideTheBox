using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;

namespace UI
{
    [RequireComponent(typeof(CanvasGroup))]
    [InfoBox("Will set the Canvas Group's alpha to 1 on start, then animate it to zero, then destroy itself.")]
    public class FadeFromBlack : MonoBehaviour
    {
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            // If we're on program frame 1 (i.e. we Didn't come from the splash screen), skip fading in
            if (Time.time == 0)
            {
                Debug.Log("Splash didn't play, skipping fade from black.");
                Destroy(gameObject);
                return;
            }

            _canvasGroup = GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 1;

            Tween.Alpha(_canvasGroup, 0, 2f, Ease.InSine)
                .OnComplete(() => Destroy(gameObject));
        }
    }
}