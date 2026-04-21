using AK.Wwise;
using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Util
{
    [InfoBox("The selected state will be set when only the <b>local player</b> enters the bounds.")]
    public class LocalPlayerStateArea : MonoBehaviour
    {
        [SerializeField] private State _state;

        private void OnTriggerEnter(Collider other)
        {
            if (NetworkClient.localPlayer?.gameObject != other.attachedRigidbody.gameObject) return;
            SetValue();
        }

        //For cutscene testing
        public void SetValue()
        {
            _state.SetValue();
        }
    }
}