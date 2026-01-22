using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;


public class Grappler : MonoBehaviour
{
    [Tooltip("Camera to use for raycasting")]
    [SerializeField] [Required] private Camera cam;
    
    [Tooltip("Layer of objects that can be grappled to")]
    [SerializeField] private LayerMask grappleableLayer;

    [Tooltip("Grappling hook object to be enabled/disabled (must already exist in the scene)")]
    [SerializeField] [Required] private GameObject grapplingHook;


    [Header("Input")]
    [Tooltip("Boolean input action used to grapple")]
    [SerializeField] [Required] private InputActionReference grappleInputAction;


    [Header("Settings")]
    [Tooltip("Maximum range at which the player can grapple on to objects")]
    [SerializeField] private float range;

    [Tooltip("Speed of grappling hook in ms^-1")]
    [SerializeField] private float speed;


    private Transform highlightedObject = null;


    void Update()
    {
        //Raycasting
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, range, grappleableLayer))
        {
            hit.transform.GetComponent<MeshRenderer>().material.color = Color.red; //todo: temp, replace with a cool shader
            highlightedObject = hit.transform;
        }
        else
        {
            if (highlightedObject != null)
            {
                highlightedObject.GetComponent<MeshRenderer>().material.color = Color.green; //todo: temp, replace with a cool shader
                highlightedObject = null;
            }
        }

        //Grapple
        if (highlightedObject != null && grappleInputAction.action.ReadValue<bool>())
        {
            grapplingHook.SetActive(true);
            //todo: jowsey
        }
    }
}
