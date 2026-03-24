using Mirror;
using UnityEngine;

public class PillarFall : NetworkBehaviour
{
    [SerializeField] private NetworkAnimator _animator;
    private bool _activated = false;

    void OnTriggerEnter(Collider other)
    {
        if (authority)
        {
            if (_activated) { return; }

            if (((1 << other.gameObject.layer) & LayerMask.GetMask("Cart", "Player")) != 0)
            {
                _animator.SetTrigger("Fall");
                _activated = true;

            }
        }
    }
}
