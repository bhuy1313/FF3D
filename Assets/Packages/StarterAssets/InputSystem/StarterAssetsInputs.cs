using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	public class StarterAssetsInputs : MonoBehaviour
	{
		[Header("Character Input Values")]
		public Vector2 move;
		public Vector2 look;
		public bool jump;
		public bool sprint;
		public bool crouch;
		public bool interact;
		public bool pickup;
		public bool use;
		public bool drop;
		public bool grab;
		public bool dispatchNotes;
		public int slot = -1;

		[Header("Movement Settings")]
		public bool analogMovement;

		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = true;
		public bool cursorInputForLook = true;

#if ENABLE_INPUT_SYSTEM
		public void OnMove(InputValue value)
		{
			MoveInput(value.Get<Vector2>());
		}

		public void OnLook(InputValue value)
		{
			if(cursorInputForLook)
			{
				LookInput(value.Get<Vector2>());
			}
		}

		public void OnJump(InputValue value)
		{
			JumpInput(value.isPressed);
		}

		public void OnSprint(InputValue value)
		{
			SprintInput(value.isPressed);
		}

		public void OnCrouch(InputValue value)
		{
			if (!value.isPressed)
			{
				return;
			}

			ToggleCrouchInput();
		}

		public void OnInteract(InputValue value)
		{
			InteractInput(value.isPressed);
		}

		public void OnPickup(InputValue value)
		{
			PickupInput(value.isPressed);
		}

		public void OnUse(InputValue value)
		{
			UseInput(value.isPressed);
		}

		public void OnDrop(InputValue value)
		{
			DropInput(value.isPressed);
		}

		public void OnGrab(InputValue value)
		{
			GrabInput(value.isPressed);
		}

		public void OnToggleDispatchNotes(InputValue value)
		{
			if (!value.isPressed)
			{
				return;
			}

			ToggleDispatchNotesInput();
		}

		public void OnSlot1(InputValue value) { SlotInput(0, value.isPressed); }
		public void OnSlot2(InputValue value) { SlotInput(1, value.isPressed); }
		public void OnSlot3(InputValue value) { SlotInput(2, value.isPressed); }
		public void OnSlot4(InputValue value) { SlotInput(3, value.isPressed); }
		public void OnSlot5(InputValue value) { SlotInput(4, value.isPressed); }
		public void OnSlot6(InputValue value) { SlotInput(5, value.isPressed); }
#endif


		public void MoveInput(Vector2 newMoveDirection)
		{
			move = newMoveDirection;
		} 

		public void LookInput(Vector2 newLookDirection)
		{
			look = newLookDirection;
		}

		public void JumpInput(bool newJumpState)
		{
			jump = newJumpState;
		}

		public void SprintInput(bool newSprintState)
		{
			sprint = newSprintState;
		}

		public void CrouchInput(bool newCrouchState)
		{
			crouch = newCrouchState;
		}

		public void ToggleCrouchInput()
		{
			crouch = !crouch;
		}

		public void ToggleDispatchNotesInput()
		{
			dispatchNotes = !dispatchNotes;
		}

		public void InteractInput(bool newInteractState)
		{
			interact = newInteractState;
		}

		public void PickupInput(bool newPickupState)
		{
			pickup = newPickupState;
		}

		public void UseInput(bool newUseState)
		{
			use = newUseState;
		}

		public void DropInput(bool newDropState)
		{
			drop = newDropState;
		}

		public void GrabInput(bool newGrabState)
		{
			grab = newGrabState;
		}

		public void DispatchNotesInput(bool newDispatchNotesState)
		{
			dispatchNotes = newDispatchNotesState;
		}

		public void ClearGameplayActionInputs()
		{
			jump = false;
			sprint = false;
			crouch = false;
			interact = false;
			pickup = false;
			use = false;
			drop = false;
			slot = -1;
		}

		public void SlotInput(int slotIndex, bool isPressed)
		{
			if (!isPressed)
			{
				return;
			}

			slot = slotIndex;
		}

		private void LateUpdate()
		{
			// one-shot inputs: clear after being read this frame
			interact = false;
			pickup = false;
			use = false;
			drop = false;
			grab = false;
			slot = -1;
		}
		
		private void OnApplicationFocus(bool hasFocus)
		{
			SetCursorState(cursorLocked);
		}

		private void SetCursorState(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
		}
	}
	
}
