using UnityEngine;

namespace UI
{
    public class LoadingIndicator : MonoBehaviour
    {
        public LoadingIndicator Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance)
            {
                Debug.LogWarning("Loading indicator already exists and you're making another. Are you Stupid?");
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }
    }
}