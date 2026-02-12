using UnityEngine;

namespace UI
{
    public class Menu : MonoBehaviour
    {
        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void PlayOnline()
        {
            Debug.Log("playing online yay");
        }

        public void PlayLocal()
        {
            Debug.Log("playing locally die");
        }
    }
}