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
            _canvasGroup = GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 1;
        }

        private void Update()
        {
            if (_canvasGroup.alpha > 0)
            {
                _canvasGroup.alpha -= Time.deltaTime;
                return;
            }

            Destroy(gameObject);
        }
    }
}