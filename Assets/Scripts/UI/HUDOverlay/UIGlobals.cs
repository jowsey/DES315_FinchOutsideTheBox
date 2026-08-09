using UnityEngine;

namespace UI
{
    public class UIGlobals : MonoBehaviour
    {
        public static Canvas MainCanvas { get; private set; }

        private void Awake()
        {
            MainCanvas = GetComponent<Canvas>();
        }
    }
}