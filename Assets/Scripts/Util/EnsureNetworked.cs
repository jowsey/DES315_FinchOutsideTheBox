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
            else
            {
                // https://tenor.com/view/job-is-done-gif-6327344616414725502
                Destroy(gameObject);
            }
        }
    }
}