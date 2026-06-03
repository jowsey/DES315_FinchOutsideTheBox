using Sirenix.OdinInspector;
using UnityEngine;

namespace Util
{
    [InfoBox("Stops all Wwise audio when destroyed")]
    public class StopAllAudioOnDestroy : MonoBehaviour
    {
        private void OnDestroy()
        {
            AkUnitySoundEngine.StopAll();
        }
    }
}