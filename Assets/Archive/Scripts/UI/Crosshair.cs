using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    [RequireComponent(typeof(Image))]
    public class Crosshair : MonoBehaviour
    {
        private Image _crosshairImage;

        [SerializeField] [Required] private Sprite _defaultCrosshair;
        [SerializeField] [Required] private Sprite _grappleCrosshair;

        private void Awake()
        {
            _crosshairImage = GetComponent<Image>();
        }

        private void OnEnable()
        {
            GlobalEvents.OnGrappleHover.AddListener(SetGrappleCrosshair);
            GlobalEvents.OnGrappleHoverEnd.AddListener(SetDefaultCrosshair);
        }

        private void OnDisable()
        {
            GlobalEvents.OnGrappleHover.RemoveListener(SetGrappleCrosshair);
            GlobalEvents.OnGrappleHoverEnd.RemoveListener(SetDefaultCrosshair);
        }

        private void SetGrappleCrosshair()
        {
            _crosshairImage.sprite = _grappleCrosshair;
        }

        private void SetDefaultCrosshair()
        {
            _crosshairImage.sprite = _defaultCrosshair;
        }
    }
}