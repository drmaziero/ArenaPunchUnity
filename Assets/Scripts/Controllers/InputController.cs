using System;
using System.Threading.Tasks;
using UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Controllers
{
    [RequireComponent(typeof(PlayerController))]
    public class InputController : NetworkBehaviour
    {
        [field: FormerlySerializedAs("<PlayerInputsAction>k__BackingField")]
        [field: SerializeField]
        private InputActionAsset PlayerInputActions { get; set; }

        private InputAction MoveAction { get; set; }
        private InputAction AttackAction { get; set; }
        private Vector3 LastNonZeroDirection { get; set; }
        private PlayerController PlayerController { get; set; }
        
        
        private void Awake()
        {
            LastNonZeroDirection = Vector3.forward;
            PlayerController = GetComponent<PlayerController>();

            if (PlayerInputActions == null)
            {
                Debug.LogError("Player Input Actions is null on Input Controller");
                return;
            }

            var map = PlayerInputActions.FindActionMap("Player", throwIfNotFound: false);
            if (map == null)
            {
                Debug.LogError("Action Map [Player] is not found on InputActionAsset");
                return;
            }
            
            MoveAction = map.FindAction("Move");
            AttackAction = map.FindAction("Attack");
        }

        private void OnEnable()
        {
            PlayerInputActions.FindActionMap("Player")?.Enable();
        }

        private void OnDisable()
        {
            PlayerInputActions.FindActionMap("Player")?.Disable();
        }

        private void Update()
        {
            if (!IsOwner || !IsClient)
                return;

            if (AttackAction != null && AttackAction.WasPressedThisFrame())
            {
                Vector3 attackDirection = LastNonZeroDirection.sqrMagnitude > 0.0001f
                    ? LastNonZeroDirection.normalized
                    : transform.forward;
                
                PlayerController.SendAttackServerRpc(attackDirection);
            }
        }


        private void FixedUpdate()
        {
            if (!IsOwner || !IsClient)
                return;

            if (MoveAction == null)
                return;

            Vector2 movementStickValue = MoveAction.ReadValue<Vector2>();
            Vector2 direction = new Vector3(movementStickValue.x, movementStickValue.y);

            if (direction.sqrMagnitude > 0.0001f)
            {
                LastNonZeroDirection = direction.normalized;
            }
            
            PlayerController.SendMovementInputServerRpc(direction);
        }
    }
}