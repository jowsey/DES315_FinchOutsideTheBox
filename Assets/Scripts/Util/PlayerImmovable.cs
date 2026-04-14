using System.Collections.Generic;
using System.Linq;
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
        private Collider[] _colliders;

        private void OnEnable()
        {
            _rb = GetComponent<Rigidbody>();
            _colliders = GetComponentsInChildren<Collider>().Where(c => c.attachedRigidbody == _rb).ToArray();

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

        private void FixedUpdate()
        {
            // Don't use contacts if body is kinematic
            foreach (var col in _colliders)
                col.hasModifiableContacts = !_rb.isKinematic;
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