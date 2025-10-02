using System;
using System.Collections;
using Manager;
using UI;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
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
        private void ShowGameOverClientRpc(FixedString128Bytes notifyPlayerId)
        {
#if !NOT_SERVER
            if (IsOwner)
            {
                Debug.LogWarning("Show Game Over UI");
                EndGameUI.Instance.Show();
            }
#endif
            if (IsClient)
            {
                if (notifyPlayerId == AuthenticationService.Instance.PlayerId)
                    EndGameUI.Instance.Show();
            }
            
        }

        private IEnumerator StartingElimination()
        {
#if NOT_SERVER
            IsEliminated = true;
#else
            IsEliminated.Value = true;
#endif
           
#if !NOT_SERVER
            if (IsServer)
            {
                Debug.Log("Elimination Player");
                var eliminatedPlayerController = GetComponent<PlayerController>();
                FixedString128Bytes attackerPlayerID = string.IsNullOrEmpty(eliminatedPlayerController.AttackPlayerId.Value.ToString()) ? "" : eliminatedPlayerController.AttackPlayerId.Value;
                string eliminatedPlayerID = eliminatedPlayerController.GetPlayerId();
                
                this.gameObject.GetComponent<PlayerController>().EliminateClientRpc(eliminatedPlayerID);
            }
            
            yield return new WaitForSeconds(0.5f);
            /*
            NetworkObject.Despawn(true);
            ShowGameOverClientRpc(GetComponent<PlayerController>().GetPlayerId());

            var attackPlayerController = GameManager.Instance.GetPlayerControllerByAuthId(attackerPlayerID);
            if (attackPlayerController != null)
                attackPlayerController.AddCoinsServerRpc(attackerPlayerID, GetComponent<PlayerController>().GetHalfCoins());
            GetComponent<PlayerController>().LoseCoinsServerRpc(AuthenticationService.Instance.PlayerId);
            GameManager.Instance.UpdateOrCreatePlayerElimination(attackerPlayerID);
            */
#else
            this.gameObject.GetComponent<PlayerController>().Eliminate();
            yield return new WaitForSeconds(0.25f);
            Debug.Log($"Eliminate Player by: {gameObject.GetComponent<PlayerController>().AttackPlayerId}");
            GameManager.Instance.Unregister(gameObject.GetComponent<PlayerController>().MyPlayerId);
            Destroy(this.gameObject);
            if (this.GetComponent<PlayerController>().IsNotServerLocalPlayer)
            {
                EndGameUI.Instance.Show();
            }
            else
                GameManager.Instance.NotifyPlayerElimination();
            
            var attackPlayerController = GameManager.Instance.GetPlayerControllerByAuthId(gameObject.GetComponent<PlayerController>().AttackPlayerId);
            if (attackPlayerController != null)
                attackPlayerController.AddCoinsLocal(GetComponent<PlayerController>().GetHalfCoins());
            GetComponent<PlayerController>().LoseCoinsLocal();
#endif
           
#if NOT_SERVER
            IsEliminated = false;
#else
            IsEliminated.Value = false;
#endif
            
        }
    }
}