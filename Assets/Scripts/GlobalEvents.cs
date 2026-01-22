using System;
using UnityEngine;
using UnityEngine.Events;

//Static class for global access to events (note: C# actions, not unity events - this is more efficient)
//Call EventManager.[event name] += foo; and EventManager.[event name].Invoke(params...);
//(^where foo is a function with the signature void foo(params...);)
//Note: c# actions aren't automatically unsubscribed upon destruction, make sure to manually unsubscribe with EventManager.[event name] -= foo; to avoid mem leak
//Subscription and unsubscription will usually be handled in OnEnable() and OnDisable() respectively
public static class GlobalEvents
{
    //Triggered when the player has initiated a grapple to a GameObject (todo: better if GameObject or Transform?)
    //todo: maybe unnecessary
    public static Action<GameObject> OnGrapple;

    public static UnityEvent OnGrappleHover = new();
    public static UnityEvent OnGrappleHoverEnd = new();
}