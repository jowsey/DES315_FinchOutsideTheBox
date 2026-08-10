using UnityEngine;
using UnityEngine.EventSystems;

namespace Util
{
    public class MainMenuMouse : MonoBehaviour, IPointerClickHandler
    {
        private static readonly int Run = Animator.StringToHash("Run");

        [SerializeField] private Animator _animator;

        public void OnPointerClick(PointerEventData eventData)
        {
            _animator.SetTrigger(Run);
        }
    }
}