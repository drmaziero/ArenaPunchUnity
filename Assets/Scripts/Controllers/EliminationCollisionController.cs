using System;
using System.Collections;
using Manager;
using UI;
using Unity.Netcode;
using UnityEngine;

namespace Controllers
{
    public class EliminationCollisionController : NetworkBehaviour
    {
#if NOT_SERVER
        private bool IsEliminated = false;
#else
        private NetworkVariable<bool> IsEliminated = new NetworkVariable<bool>(false);
#endif
        
        private void OnCollisionEnter2D(Collision2D other)
        {
#if !NOT_SERVER
            if (!IsServer)
                return;
#endif

            if (other.collider.CompareTag("RingLimit"))
            {
#if NOT_SERVER
                if (!IsEliminated)
                    StartCoroutine(StartingElimination());
#else
                if (!IsEliminated.Value)
                    StartCoroutine(StartingElimination());
#endif
            }
        }

        [ClientRpc]
        private void ShowGameOverClientRpc(string notifyPlayerId)
        {
            if (IsOwner)
            {
                Debug.LogWarning("Show Game Over UI");
                GameOverUI.Instance.Show();
            }
            else
                GameManager.Instance.NotifyPlayerElimination(false,notifyPlayerId);
        }

        private IEnumerator StartingElimination()
        {
#if NOT_SERVER
            IsEliminated = true;
#else
            IsEliminated.Value = true;
#endif
            this.gameObject.GetComponent<PlayerController>().Eliminate();
            yield return new WaitForSeconds(0.25f);
#if !NOT_SERVER
            string attackerPlayerID = GetComponent<PlayerController>().AttackPlayerId.Value;
            ShowGameOverClientRpc(attackerPlayerID);
            NetworkObject.Despawn(true);
            var attackPlayerController = GameManager.Instance.GetPlayerControllerByAuthId(attackerPlayerID);
            if (attackPlayerController != null)
                attackPlayerController.AddCoinsServerRpc(attackerPlayerID, GetComponent<PlayerController>().GetHalfCoins());
#else
            GameManager.Instance.Unregister(gameObject.GetComponent<PlayerController>().MyPlayerId);
            Destroy(this.gameObject);
            if (this.GetComponent<PlayerController>().IsNotServerLocalPlayer)
            {
                GameOverUI.Instance.Show();
            }
            else
                GameManager.Instance.NotifyPlayerElimination();
            
            var attackPlayerController = GameManager.Instance.GetPlayerControllerByAuthId(gameObject.GetComponent<PlayerController>().MyPlayerId);
            if (attackPlayerController != null)
                attackPlayerController.AddCoinsLocal(GetComponent<PlayerController>().GetHalfCoins());
#endif
#if NOT_SERVER
            IsEliminated = false;
#else
            IsEliminated.Value = false;
#endif
            
        }
    }
}