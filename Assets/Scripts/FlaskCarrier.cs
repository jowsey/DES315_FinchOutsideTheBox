using UnityEngine;

public class FlaskCarrier : MonoBehaviour
{
    public Transform FlaskPutdownTarget;
    [SerializeField] private Cart _cart;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Flask"))
        {
            Flask flask = GetComponent<Flask>();
            flask.Smashable = true;
            _cart.CarriedFlasks.Add(flask);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Flask"))
        {
            _cart.CarriedFlasks.Remove(other.GetComponent<Flask>());
        }
    }
}