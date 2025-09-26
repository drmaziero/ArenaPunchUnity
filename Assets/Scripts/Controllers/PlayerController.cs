using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Manager;
using UI;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Controllers
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : NetworkBehaviour
    {
        [field: SerializeField]
        private float Speed { get; set; }

        [field: SerializeField] 
        private float AttackDuration { get; set; } = 0.5f;

        [field: SerializeField] 
        private float AttackCooldown { get; set; } = 1.0f;

        [field: SerializeField] 
        private float AttackVFXDuration { get; set; } = 0.3f;
        [field: SerializeField] 
        private float EliminateVFXDuration { get; set; } = 0.3f;

        [field: SerializeField] 
        private float PushForce { get; set; } = 10.0f;
        [field: SerializeField]
        private Animator CharacterAnimator { get; set; }
        [field: SerializeField]
        private SpriteRenderer CharacterSpriteRenderer { get; set; }
        
        [field: SerializeField]
        private Animator GloveAnimator { get; set; }
        
        [field: SerializeField]
        private List<SpriteRenderer> GloveSpriteRenderer { get; set; }
        
        [field: SerializeField]
        private SpriteRenderer HitVFXSpriteRender { get; set; }
        
        [field: SerializeField]
        private List<Transform> GlovesParent { get; set; }
        
        [field: SerializeField]
        private GameObject LocalPlayerMarked { get; set; }
        
        [field: SerializeField]
        private SpriteRenderer EliminationVFXSpriteRenderer { get; set; }

        [field: Header("Local Player")]
        [field: SerializeField]
        public bool IsNotServerLocalPlayer { get; private set; } = false;

#if NOT_SERVER
        public bool IsAttacking = false;
        public bool IsHit = false;
        public bool IsDead = false;
        private bool IsFlipping = false;
        public int PlayerCounter { get; private set; } = 0;
        public int Coins { get; private set; } = 10;
        public string MyPlayerId { get; set; }
        public string AttackPlayerId { get; set; }
#else
        public NetworkVariable<bool> IsAttacking = new NetworkVariable<bool>(false);
        public NetworkVariable<bool> IsHit = new NetworkVariable<bool>(false);
        public NetworkVariable<bool> IsDead = new NetworkVariable<bool>(false);
        private NetworkVariable<bool> IsFlipping = new NetworkVariable<bool>(false);
        private NetworkVariable<int> PlayerCounter { get; set; } = new NetworkVariable<int>(0);
        public NetworkVariable<string> AttackPlayerId { get; private set; } = new NetworkVariable<string>();
        private NetworkVariable<int> Coins { get; set; } = new NetworkVariable<int>(10);
#endif
        private Vector2 ServerMovementInput { get; set; }
        private Vector2 FacingDirection { get; set; }
        private Rigidbody2D Rigidbody { get;  set; }
       
        private const int PlayersToEnableEscape = 2;
        
        private const string IsMoving = "IsMoving";
       
       private bool CanAttack { get; set; }
      

        private void Start()
        {
            ServerMovementInput = Vector2.zero;
            FacingDirection = Vector2.zero;

            CanAttack = true;
            Rigidbody = GetComponent<Rigidbody2D>();
            
#if NOT_SERVER
            if (IsNotServerLocalPlayer)
                LocalPlayerMarked.SetActive(true);
            
            CoinsCounterUI.Instance.UpdateTotalCoins(Coins);
#else
            if (IsLocalPlayer)
                LocalPlayerMarked.SetActive(true);

            Coins.OnValueChanged += (oldValue, newValue) =>
            {
               CoinsCounterUI.Instance.UpdateTotalCoins(newValue);
               CoinsCounterUI.Instance.UpdateAddCoinsAmount(newValue - oldValue);
            };

            CoinsCounterUI.Instance.UpdateTotalCoins(Coins.Value);
#endif
        }

        protected override void OnNetworkPostSpawn()
        {
            if (IsServer)
            {
                string playerId = AuthenticationService.Instance.PlayerId;
                GameManager.Instance.Register(this,playerId);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                string playerId = AuthenticationService.Instance.PlayerId;
                GameManager.Instance.Unregister(playerId);
            }
        }

        public void FixedUpdate()
        {
            ApplyFlipping();

#if !NOT_SERVER
            if (!IsServer)
                return;

            if (IsHit.Value)
            {
                CharacterAnimator.SetBool(IsMoving,false);
                return;
            }
#else
            if (IsHit)
            {
                CharacterAnimator.SetBool(IsMoving,false);
                return;
            }
#endif
            Rigidbody.velocity = ServerMovementInput * Speed;

            if (ServerMovementInput.sqrMagnitude > 0.00001f)
                FacingDirection = ServerMovementInput.normalized;

            CheckMovementAnimation(); 
           
        }

        private void CheckMovementAnimation()
        {
            if (Rigidbody.velocity.sqrMagnitude > 0.01f)
            {
                CharacterAnimator.SetBool(IsMoving,true);
                if (FacingDirection.x > 0)
                {
#if NOT_SERVER
                    IsFlipping = false;     
#else
                    IsFlipping.Value = false;
#endif
                }
                else if (FacingDirection.x < 0)
                {
#if NOT_SERVER
                    IsFlipping = true;     
#else
                   IsFlipping.Value = true;
#endif
                }
            }
            else
                CharacterAnimator.SetBool(IsMoving,false);
        }

        private void ApplyFlipping()
        {
            bool useFlipping;
#if NOT_SERVER
            useFlipping = IsFlipping;
#else
            useFlipping = IsFlipping.Value;
#endif
            if (useFlipping)
            {
                CharacterSpriteRenderer.flipX = true;
                foreach (var spriteRenderer in GloveSpriteRenderer)
                {
                    spriteRenderer.flipX = true;
                }

                HitVFXSpriteRender.flipX = false;
                EliminationVFXSpriteRenderer.flipX = true;

                GlovesParent[0].localPosition = new Vector3(-0.6f, 0.0f, 0.0f);
                GlovesParent[1].localPosition = new Vector3(-1.2f, 0.0f, 0.0f);
            }
            else
            {
                CharacterSpriteRenderer.flipX = false;
                foreach (var spriteRenderer in GloveSpriteRenderer)
                {
                    spriteRenderer.flipX = false;
                }
                
                HitVFXSpriteRender.flipX = true;
                EliminationVFXSpriteRenderer.flipX = false;
                    
                foreach (var gloveTransform in GlovesParent)
                {
                    gloveTransform.localPosition = Vector3.zero;
                }
            }
        }

        

        [ServerRpc]
        public void SendMovementInputServerRpc(Vector2 input)
        {
            if (!IsServer)
                return;
            
            ServerMovementInput = input;
        }

        public void MovementInputLocal(Vector2 input)
        {
            ServerMovementInput = input;
        }

        [ServerRpc]
        public void SendAttackServerRpc(Vector3 direction)
        {
            if (!IsServer)
                return;

            HandleAttack(direction);
        }

        public void HandleAttack(Vector3 direction)
        {
            if (!CanAttack)
                return;

            Vector3 normalizeDirection = direction.sqrMagnitude > 0.0001f ? direction : FacingDirection;
            FacingDirection = normalizeDirection;
            StartCoroutine(PerformAttack());
        }

        private IEnumerator PerformAttack()
        {
            CanAttack = false;
#if NOT_SERVER
            IsAttacking = true;
#else
            IsAttacking.Value = true;
#endif
            GloveAnimator.Play("Attack");
            yield return new WaitForSeconds(AttackDuration);
#if NOT_SERVER
            IsAttacking = false;
#else
             IsAttacking.Value = false;
#endif
            yield return new WaitForSeconds(AttackCooldown);
            CanAttack = true;
        }

        
        
        public void ApplyPush(Vector2 direction)
        {
            Rigidbody.AddForce(direction.normalized * PushForce, ForceMode2D.Impulse);
        }

        public void GetHit(string attackPlayerID = "")
        {
#if NOT_SERVER
            if (IsHit)
                return;

            AttackPlayerId = attackPlayerID;
#else
            if (IsHit.Value)
                return;

            AttackPlayerId.Value = attackPlayerID;
#endif
            StartCoroutine(PerformHit());
        }

        private IEnumerator PerformHit()
        {
#if NOT_SERVER
            IsHit = true;
            yield return new WaitForSeconds(0.5f);
            IsHit = false;
#else
            IsHit.Value = true;
            yield return new WaitForSeconds(0.5f);
            IsHit.Value = false;
            AttackPlayerId.Value = string.Empty;
#endif
        }

        public void ApplyHitVFX()
        {
            StartCoroutine(PerformHitVFX());
        }

        private IEnumerator PerformHitVFX()
        {
            HitVFXSpriteRender.gameObject.SetActive(true);
            yield return new WaitForSeconds(AttackVFXDuration);
            HitVFXSpriteRender.gameObject.SetActive(false);
        }


        public void Eliminate()
        {
            StartCoroutine(PerformEliminate());
        }

        private IEnumerator PerformEliminate()
        {
            EliminationVFXSpriteRenderer.gameObject.SetActive(true);
            yield return new WaitForSeconds(EliminateVFXDuration);
            EliminationVFXSpriteRenderer.gameObject.SetActive(false);
        }

        public string GetPlayerId()
        {
#if NOT_SERVER
            return MyPlayerId;
#endif
            return AuthenticationService.Instance.PlayerId;
        }

        public void AddCoinsLocal(int amountCoins)
        {
#if NOT_SERVER
            Debug.Log($"Add Coins Local: {amountCoins}");
            Coins += amountCoins;
            CoinsCounterUI.Instance.UpdateTotalCoins(Coins);
            CoinsCounterUI.Instance.UpdateAddCoinsAmount(amountCoins);
            EndGameUI.Instance.UpdateCoins(Coins);
#endif
        }

        [ServerRpc(RequireOwnership = false)]
        public void AddCoinsServerRpc(string playerId, int amountCoins)
        {
#if !NOT_SERVER
            var target = GameManager.Instance.GetPlayerControllerByAuthId(playerId);
            if (target != null)
            {
                target.Coins.Value += amountCoins;
                EndGameUI.Instance.UpdateCoins(target.Coins.Value);
            }
#endif
        }
        
        public int GetHalfCoins()
        {
#if !NOT_SERVER
            return Mathf.FloorToInt(Coins.Value / 2);
#else
           return Mathf.FloorToInt(Coins / 2);
#endif
        }
        
    }
}