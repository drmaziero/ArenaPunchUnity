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

        public NetworkVariable<bool> IsAttacking = new NetworkVariable<bool>(false);
        public NetworkVariable<bool> IsHit = new NetworkVariable<bool>(false);
        public NetworkVariable<bool> IsDead = new NetworkVariable<bool>(false);
        private NetworkVariable<bool> IsFlipping = new NetworkVariable<bool>(false);
        private Vector2 ServerMovementInput { get; set; }
        private Vector2 FacingDirection { get; set; }
        private Rigidbody2D Rigidbody { get;  set; }
        private NetworkVariable<int> PlayerCounter { get; set; } = new NetworkVariable<int>(0);
        private NetworkVariable<float> PlayerCoins { get; set; } = new NetworkVariable<float>(10.0f);
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
            
            if (!IsServer)
                return;

            if (IsHit.Value)
            {
                CharacterAnimator.SetBool(IsMoving,false);
                return;
            }

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
                    IsFlipping.Value = false;
                    
                }
                else if (FacingDirection.x < 0)
                {
                    IsFlipping.Value = true;
                    
                }
            }
            else
                CharacterAnimator.SetBool(IsMoving,false);
        }

        private void ApplyFlipping()
        {
            if (IsFlipping.Value)
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

        [ServerRpc]
        public void SendAttackServerRpc(Vector3 direction)
        {
            if (!IsServer)
                return;

            if (!CanAttack)
                return;

            Vector3 normalizeDirection = direction.sqrMagnitude > 0.0001f ? direction : FacingDirection;
            FacingDirection = normalizeDirection;
            StartCoroutine(PerformAttack());
        }

        private IEnumerator PerformAttack()
        {
            CanAttack = false;
            IsAttacking.Value = true;

            GloveAnimator.Play("Attack");
            yield return new WaitForSeconds(AttackDuration);
            IsAttacking.Value = false;
            yield return new WaitForSeconds(AttackCooldown);
            CanAttack = true;
        }

        
        
        public void ApplyPush(Vector2 direction)
        {
            Rigidbody.AddForce(direction * PushForce, ForceMode2D.Impulse);
        }

        public void GetHit()
        {
            if (IsHit.Value)
                return;

            StartCoroutine(PerformHit());
        }

        private IEnumerator PerformHit()
        {
            IsHit.Value = true;
            //CharacterAnimator.Play("GetHit");
            yield return new WaitForSeconds(0.5f);
            IsHit.Value = false;
        }

        public void Death()
        {
            if (IsDead.Value)
                return;

            IsDead.Value = true;
            CharacterAnimator.Play("Death");
        }
    }
}