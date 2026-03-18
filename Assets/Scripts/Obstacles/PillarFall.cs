using UnityEngine;

public class PillarFall : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    private bool _activated = false;

    void OnTriggerEnter(Collider other)
    {
        if (_activated) { return; }

        if (((1 << other.gameObject.layer) & LayerMask.GetMask("Cart", "Player")) != 0)
        {
            _animator.SetTrigger("Fall");
            _activated = true;

        }
    }
}
