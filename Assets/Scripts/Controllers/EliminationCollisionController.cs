using System;
using UI;
using Unity.Netcode;
using UnityEngine;

namespace Controllers
{
    public class EliminationCollisionController : NetworkBehaviour
    {
        private void OnCollisionEnter2D(Collision2D other)
        {
            if (!IsServer)
                return;

            if (other.collider.CompareTag("RingLimit"))
            {
                ulong ownerClientId = OwnerClientId;
                ShowGameOverClientRpc(new ClientRpcParams()
                {
                    Send = new ClientRpcSendParams() { TargetClientIds = new[] { ownerClientId } }
                });
                NetworkObject.Despawn(true);
            }
        }

        [ClientRpc]
        private void ShowGameOverClientRpc(ClientRpcParams rpcParams = default)
        {
            Debug.LogWarning("Show Game Over UI");
            GameOverUI.Instance.Show();
        }
    }
}