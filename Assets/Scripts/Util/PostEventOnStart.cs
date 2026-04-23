using UnityEngine;

namespace Util
{
    public class PostEventOnStart : MonoBehaviour
    {
        [SerializeField] private AK.Wwise.Event _event;

        private void Start()
        {
            _event.Post(gameObject);
        }
    }
}