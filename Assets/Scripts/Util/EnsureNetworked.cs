using UnityEngine;
using UnityEngine.SceneManagement;

namespace Util
{
    public class EnsureNetworked : MonoBehaviour
    {
        private void Awake()
        {
            if (!FindAnyObjectByType<Mirror.NetworkManager>())
            {
                SceneManager.LoadScene("Menu");
            }
        }
    }
}
