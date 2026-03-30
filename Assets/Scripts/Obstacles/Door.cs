using UnityEngine;
using Mirror;

public class Door : NetworkBehaviour
{
    [SerializeField] private NetworkAnimator _animator;

    public void Open()
    {
        //if (netIdentity == null) { netIdentity = GetComponent<NetworkIdentity>(); }

        if (authority)
        {
            _animator.SetTrigger("Open");
        }
    }
}
