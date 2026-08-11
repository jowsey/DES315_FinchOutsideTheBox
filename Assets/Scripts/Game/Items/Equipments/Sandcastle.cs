using System;
using Mirror;
using UnityEngine;
using ReadOnlyAttribute = Sirenix.OdinInspector.ReadOnlyAttribute;

namespace Game.Items.Equipments
{
    public class Sandcastle : RespawnTarget
    {
        [ReadOnly] [SyncVar] public Checkpoint Parent;

        public override void OnStartServer()
        {
            base.OnStartServer();

            var currentCheckpoint = Cart.Instance.CurrentRespawnTarget switch
            {
                Checkpoint cp => cp,
                Sandcastle sc => sc.Parent,
                _ => null
            };

            Parent = currentCheckpoint;
            currentCheckpoint?.Sandcastles.Add(this);
            Cart.Instance.SetActiveRespawnTarget(this);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Sync up ground sand materials if eligible
            if (Physics.Raycast(new Ray(transform.position + Vector3.up * 0.5f, Vector3.down), out var hit, 1f, ~(1 << gameObject.layer)))
            {
                var groundRenderer = hit.collider.gameObject.GetComponent<Renderer>();
                if (groundRenderer.sharedMaterial.name.StartsWith("Sand_", StringComparison.Ordinal))
                {
                    var localRenderers = GetComponentsInChildren<Renderer>();
                    foreach (var rend in localRenderers)
                    {
                        if (rend.sharedMaterial.name.StartsWith("Sand_", StringComparison.Ordinal))
                        {
                            rend.sharedMaterial = groundRenderer.sharedMaterial;
                        }
                    }
                }
            }
        }
    }
}