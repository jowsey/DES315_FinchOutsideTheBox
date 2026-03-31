using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Collections;
using UnityEngine;

namespace Util
{
    [InfoBox("This object will act as though it has infinite mass when pushed by a player.")]
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerImmovable : MonoBehaviour
    {
        private static readonly Dictionary<int, PlayerImmovable> _immovableIds = new();
        private static bool _contactListenerActive;

        private Rigidbody _rb;

        private void OnEnable()
        {
            _rb = GetComponent<Rigidbody>();

            foreach (var col in GetComponentsInChildren<Collider>())
                col.hasModifiableContacts = true;

            _immovableIds[_rb.GetInstanceID()] = this;

            if (!_contactListenerActive)
            {
                Physics.ContactModifyEvent += OnModifyContacts;
                _contactListenerActive = true;
            }
        }

        private void OnDisable()
        {
            _immovableIds.Remove(_rb.GetInstanceID());
            if (_immovableIds.Count == 0)
            {
                Physics.ContactModifyEvent -= OnModifyContacts;
                _contactListenerActive = false;
            }
        }


        private static void OnModifyContacts(PhysicsScene scene, NativeArray<ModifiableContactPair> pairs)
        {
            for (var i = 0; i < pairs.Length; i++)
            {
                var pair = pairs[i];

                // If collision is between an Immovable and a Player
                if (_immovableIds.ContainsKey(pair.bodyInstanceID) && PlayerController.IsPlayerRb(pair.otherBodyInstanceID))
                {
                    // from perspective of Immovable (us) colliding with Player (them)
                    var mp = pair.massProperties;
                    mp.inverseMassScale = 0f; // make us act as if infinitely heavy
                    mp.inverseInertiaScale = 0f;
                    pair.massProperties = mp;
                }
                else if (_immovableIds.ContainsKey(pair.otherBodyInstanceID) && PlayerController.IsPlayerRb(pair.bodyInstanceID))
                {
                    // from perspective of Player (us) colliding with Immovable (them)
                    var mp = pair.massProperties;
                    mp.otherInverseMassScale = 0f; // make them act as if infinitely heavy
                    mp.otherInverseInertiaScale = 0f;
                    pair.massProperties = mp;
                }
            }
        }
    }
}