using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UI;
using Unity.Collections;
using Unity.Netcode;
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
        private List<Transform> GlovesParent { get; set; }

#if NOT_SERVER
        public bool IsAttacking = false;
        public bool IsHit = false;
        public bool IsDead = false;
        private bool IsFlipping = false;
        private int PlayerCounter { get; set; } = 0;
        private float PlayerCoins { get; set; } = 10.0f;
#else
        public NetworkVariable<bool> IsAttacking = new NetworkVariable<bool>(false);
        public NetworkVariable<bool> IsHit = new NetworkVariable<bool>(false);
        public NetworkVariable<bool> IsDead = new NetworkVariable<bool>(false);
        private NetworkVariable<bool> IsFlipping = new NetworkVariable<bool>(false);
        private NetworkVariable<int> PlayerCounter { get; set; } = new NetworkVariable<int>(0);
        private NetworkVariable<float> PlayerCoins { get; set; } = new NetworkVariable<float>(10.0f);
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
            //PlayerCounterUI.Instance.UpdateCounter(PlayersToEnableEscape - PlayerCounter.Value);
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
            Rigidbody.AddForce(direction * PushForce, ForceMode2D.Impulse);
        }

        public void GetHit()
        {
#if NOT_SERVER
            if (IsHit)
                return;
#else
            if (IsHit.Value)
                return;
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
#endif
        }

        /*
        public void Death()
        {
            if (IsDead.Value)
                return;

            IsDead.Value = true;
            CharacterAnimator.Play("Death");
        }
        */
    }
}