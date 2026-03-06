using Sirenix.OdinInspector;
using UnityEngine;

namespace Util
{
    [RequireComponent(typeof(Animator))]
    [InfoBox("Apply to a player to offset its initial idle animation by a random amount.")]
    public class IdleAnimationOffset : MonoBehaviour
    {
        private void Awake()
        {
            var animator = GetComponent<Animator>();
            animator.Play("Idle", 0, Random.value);
            animator.Update(0);
        }
    }
}