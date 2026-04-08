using UnityEngine;
using System.Collections;
using System.Text;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	[RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
	[RequireComponent(typeof(PlayerInput))]
#endif
	public class FirstPersonController : MonoBehaviour
	{
		[Header("Player")]
		[Tooltip("Move speed of the character in m/s")]
		public float MoveSpeed = 4.0f;
		[Tooltip("Sprint speed of the character in m/s")]
		public float SprintSpeed = 6.0f;
		[Tooltip("Rotation speed of the character")]
		public float RotationSpeed = 1.0f;
		[Tooltip("Acceleration and deceleration")]
		public float SpeedChangeRate = 10.0f;
		[Tooltip("Stamina used per second while sprinting")]
		public float SprintStaminaCostPerSecond = 12.0f;
		[Tooltip("Minimum stamina required to start sprinting")]
		public float SprintMinStamina = 5.0f;

		[Header("Crouch")]
		[Tooltip("Crouch move speed of the character in m/s")]
		public float CrouchSpeed = 2.0f;
		[Tooltip("Controller height while crouching")]
		public float CrouchHeight = 1.0f;
		[Tooltip("Camera target local Y offset while crouching")]
		public float CrouchCameraOffset = -0.5f;
		[Tooltip("How fast to transition between stand and crouch")]
		public float CrouchTransitionSpeed = 20.0f;

		[Header("Crouch Debug")]
		[Tooltip("Logs crouch request and stand-up blockers to the Console.")]
		public bool DebugCrouchState;
		[Tooltip("Minimum time between repeated stand-up blocked logs.")]
		public float CrouchDebugLogCooldown = 0.5f;

		[Header("Encumbrance")]
		[Tooltip("Reduce player movement speed while grabbing or carrying heavy objects.")]
		public bool EnableMovementWeightPenalty = true;
		[Tooltip("Total carried weight at which movement reaches the minimum speed multiplier.")]
		public float WeightForMinimumSpeed = 80.0f;
		[Tooltip("Minimum movement multiplier at or above Weight For Minimum Speed.")]
		[Range(0.05f, 1.0f)]
		public float MinimumWeightSpeedMultiplier = 0.35f;
		[Tooltip("Disables sprinting when current carried weight meets or exceeds this value. Set to 0 to never disable sprint.")]
		public float SprintDisabledWeight = 45.0f;
		[Tooltip("How strongly carried weight affects crouch speed.")]
		[Range(0.0f, 1.0f)]
		public float CrouchWeightPenaltyScale = 0.6f;
		[Tooltip("How strongly carried weight affects climb speed.")]
		[Range(0.0f, 1.0f)]
		public float ClimbWeightPenaltyScale = 0.85f;

		[Header("Climb")]
		[Tooltip("Climb speed in m/s while on ladders")]
		public float ClimbSpeed = 3.0f;
		[Tooltip("If true, pressing jump will exit climbing")]
		public bool JumpToExitClimb = true;
		[Tooltip("Step size used to raise the player when starting to climb")]
		public float ClimbStartStep = 0.1f;
		[Tooltip("Max steps to raise the player when starting to climb")]
		public int ClimbStartMaxSteps = 5;
		[Tooltip("Extra gap kept between the player capsule and the ladder face when snapping into climb")]
		public float ClimbStartFacePadding = 0.05f;
		[Tooltip("Duration used to ease the player into the ladder attach position when starting a climb")]
		public float ClimbStartSnapDuration = 0.12f;
		[Tooltip("Small upward arc applied while easing into the ladder attach position")]
		public float ClimbStartSnapArcHeight = 0.03f;
		[Tooltip("Short grace period after entering a ladder before grounded state can cancel climbing")]
		public float ClimbStartGroundedGraceDuration = 0.15f;

		[Space(10)]
		[Tooltip("The height the player can jump")]
		public float JumpHeight = 1.2f;
		[Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
		public float Gravity = -15.0f;

		[Space(10)]
		[Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
		public float JumpTimeout = 0.1f;
		[Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
		public float FallTimeout = 0.15f;

		[Header("Player Grounded")]
		[Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
		public bool Grounded = true;
		[Tooltip("Useful for rough ground")]
		public float GroundedOffset = -0.14f;
		[Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
		public float GroundedRadius = 0.5f;
		[Tooltip("What layers the character uses as ground")]
		public LayerMask GroundLayers;

		[Header("Cinemachine")]
		[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
		public GameObject CinemachineCameraTarget;
		[Tooltip("How far in degrees can you move the camera up")]
		public float TopClamp = 90.0f;
		[Tooltip("How far in degrees can you move the camera down")]
		public float BottomClamp = -90.0f;
		[Tooltip("Time in seconds to smooth camera rotation input. Set to 0 to disable.")]
		public float CameraRotationSmoothTime = 0.05f;

		[Header("Camera Motion")]
		[Tooltip("Adds subtle camera bob and tilt while moving on the ground.")]
		public bool EnableCameraMotion = true;
		[Tooltip("Base bob frequency while walking.")]
		public float WalkBobFrequency = 8.0f;
		[Tooltip("Side-to-side bob amount while walking.")]
		public float WalkBobHorizontalAmplitude = 0.015f;
		[Tooltip("Vertical bob amount while walking.")]
		public float WalkBobVerticalAmplitude = 0.025f;
		[Tooltip("Frequency multiplier while sprinting.")]
		public float SprintBobFrequencyMultiplier = 1.35f;
		[Tooltip("Amplitude multiplier while sprinting.")]
		public float SprintBobAmplitudeMultiplier = 1.45f;
		[Tooltip("Frequency multiplier while crouching.")]
		public float CrouchBobFrequencyMultiplier = 0.75f;
		[Tooltip("Amplitude multiplier while crouching.")]
		public float CrouchBobAmplitudeMultiplier = 0.6f;
		[Tooltip("How quickly bob and tilt blend in and out.")]
		public float CameraMotionBlendSpeed = 14.0f;
		[Tooltip("Maximum camera roll angle applied while strafing.")]
		public float StrafeTilt = 1.5f;
		[Tooltip("Maximum upward camera offset while moving upward in a jump.")]
		public float JumpCameraUpwardOffset = 0.03f;
		[Tooltip("Maximum downward camera offset while falling.")]
		public float FallCameraDownwardOffset = 0.05f;
		[Tooltip("Upward speed that reaches full jump camera offset.")]
		public float JumpCameraMaxRiseSpeed = 5.0f;
		[Tooltip("Downward speed that reaches full fall camera offset.")]
		public float FallCameraMaxSpeed = 10.0f;
		[Tooltip("Lets carried weight affect the feel of camera motion while moving.")]
		public bool EnableWeightCameraMotionImpact = true;
		[Tooltip("Bob frequency multiplier when reaching maximum configured movement burden.")]
		public float WeightBobFrequencyMultiplierAtMax = 0.82f;
		[Tooltip("Bob amplitude multiplier when reaching maximum configured movement burden.")]
		public float WeightBobAmplitudeMultiplierAtMax = 1.2f;
		[Tooltip("Strafe tilt multiplier when reaching maximum configured movement burden.")]
		public float WeightStrafeTiltMultiplierAtMax = 0.8f;
		[Tooltip("Additional downward camera offset applied at full movement burden while grounded.")]
		public float WeightCameraDownOffsetAtMax = 0.02f;

		// cinemachine
		private float _cinemachineTargetPitch;

		// player
		private float _speed;
		private float _rotationVelocity;
		private float _verticalVelocity;
		private float _terminalVelocity = 53.0f;

		// timeout deltatime
		private float _jumpTimeoutDelta;
		private float _fallTimeoutDelta;

	
#if ENABLE_INPUT_SYSTEM
		private PlayerInput _playerInput;
#endif
		private CharacterController _controller;
		private StarterAssetsInputs _input;
		private GameObject _mainCamera;
		private PlayerVitals _vitals;
		private FPSInteractionSystem _interactionSystem;

		private const float _threshold = 0.00001f;
		private bool _wantsSprint;
		private bool _isCrouching;
		private bool _isClimbing;
		private Ladder _currentLadder;
		private bool _isClimbStartTransitioning;
		private Coroutine _climbStartRoutine;
		private bool _climbStartRestoreController;
		private float _climbGroundedGraceTimer;
		private float _standHeight;
		private Vector3 _standCenter;
		private Vector3 _cameraTargetInitialLocalPos;
		private Vector3 _cameraBaseLocalPosCurrent;
		private Vector3 _cameraMotionCurrentPosOffset;
		private float _cameraMotionCurrentRoll;
		private float _cameraBobTimer;
		private Vector2 _lookInputSmoothed;
		private Vector2 _lookInputSmoothVelocity;
		private float _mouseSensitivityMultiplier = 1.0f;
		private float _lastCrouchBlockedLogTime = float.NegativeInfinity;
		private bool _hasLoggedCrouchRequestState;
		private bool _lastLoggedCrouchRequestState;

		private bool IsCurrentDeviceMouse
		{
			get
			{
				#if ENABLE_INPUT_SYSTEM
				if (_playerInput == null)
				{
					return Mouse.current != null;
				}

				if (!string.IsNullOrWhiteSpace(_playerInput.currentControlScheme)
					&& _playerInput.currentControlScheme.IndexOf("mouse", System.StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}

				if (!string.IsNullOrWhiteSpace(_playerInput.currentControlScheme)
					&& _playerInput.currentControlScheme.IndexOf("keyboard", System.StringComparison.OrdinalIgnoreCase) >= 0
					&& Mouse.current != null)
				{
					return true;
				}

				if (Mouse.current == null)
				{
					return false;
				}

				foreach (InputDevice device in _playerInput.devices)
				{
					if (device == Mouse.current || device is Mouse)
					{
						return true;
					}
				}

				return false;
				#else
				return false;
				#endif
			}
		}

		private void Awake()
		{
			// get a reference to our main camera
			if (_mainCamera == null)
			{
				_mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
			}
		}

		private void Start()
		{
			_controller = GetComponent<CharacterController>();
			_input = GetComponent<StarterAssetsInputs>();
			_vitals = GetComponent<PlayerVitals>();
			_interactionSystem = GetComponent<FPSInteractionSystem>();
#if ENABLE_INPUT_SYSTEM
			_playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif
			SetMouseSensitivityMultiplier(GameplayMouseSensitivityRuntimeApplier.GetCurrentOrSavedSensitivity());

			// reset our timeouts on start
			_jumpTimeoutDelta = JumpTimeout;
			_fallTimeoutDelta = FallTimeout;

			_standHeight = _controller.height;
			_standCenter = _controller.center;
			_cameraTargetInitialLocalPos = CinemachineCameraTarget != null
				? CinemachineCameraTarget.transform.localPosition
				: Vector3.zero;
			_cameraBaseLocalPosCurrent = _cameraTargetInitialLocalPos;
		}

		private void Update()
		{
			UpdateSprintState();
			if (_isClimbStartTransitioning)
			{
				_verticalVelocity = 0f;
				GroundedCheck();
				UpdateCrouch();
				return;
			}

			JumpAndGravity();
			GroundedCheck();
			if (_isClimbing && Grounded && _climbGroundedGraceTimer <= 0f)
			{
				StopClimb();
			}
			if (_isClimbing && HasReachedLadderTop())
			{
				StopClimb();
			}
			UpdateCrouch();
			Move();

			if (_climbGroundedGraceTimer > 0f)
			{
				_climbGroundedGraceTimer -= Time.deltaTime;
			}
		}

		private void LateUpdate()
		{
			CameraRotation();
			UpdateCameraTargetTransform();
		}

		private void GroundedCheck()
		{
			// set sphere position, with offset
			Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
			Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
		}

		private void CameraRotation()
		{
			Vector2 lookInput = _input.look;
			if (CameraRotationSmoothTime > 0f)
			{
				_lookInputSmoothed = Vector2.SmoothDamp(
					_lookInputSmoothed,
					lookInput,
					ref _lookInputSmoothVelocity,
					CameraRotationSmoothTime
				);
				lookInput = _lookInputSmoothed;
			}
			else
			{
				_lookInputSmoothed = lookInput;
				_lookInputSmoothVelocity = Vector2.zero;
			}

			// if there is an input
			if (lookInput.sqrMagnitude >= _threshold)
			{
				//Don't multiply mouse input by Time.deltaTime
				float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;
				float effectiveRotationSpeed = RotationSpeed;
				if (IsCurrentDeviceMouse)
				{
					effectiveRotationSpeed *= _mouseSensitivityMultiplier;
				}

				_cinemachineTargetPitch -= lookInput.y * effectiveRotationSpeed * deltaTimeMultiplier;
				_rotationVelocity = lookInput.x * effectiveRotationSpeed * deltaTimeMultiplier;

				// clamp our pitch rotation
				_cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

				// rotate the player left and right
				transform.Rotate(Vector3.up * _rotationVelocity);
			}
		}

		public void SetMouseSensitivityMultiplier(float multiplier)
		{
			_mouseSensitivityMultiplier = Mathf.Max(0f, multiplier);
		}

		private void Move()
		{
			if (_isClimbing)
			{
				ClimbMove();
				return;
			}

			// set target speed based on move speed, sprint speed and if sprint is pressed
			float targetSpeed = GetTargetGroundSpeed(GetCurrentMovementBurdenKg());

			// a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

			// note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is no input, set the target speed to 0
			if (_input.move == Vector2.zero) targetSpeed = 0.0f;

			// a reference to the players current horizontal velocity
			float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

			float speedOffset = 0.1f;
			float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

			// accelerate or decelerate to target speed
			if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
			{
				// creates curved result rather than a linear one giving a more organic speed change
				// note T in Lerp is clamped, so we don't need to clamp our speed
				_speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);

				// round speed to 3 decimal places
				_speed = Mathf.Round(_speed * 1000f) / 1000f;
			}
			else
			{
				_speed = targetSpeed;
			}

			// normalise input direction
			Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

			// note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is a move input rotate player when the player is moving
			if (_input.move != Vector2.zero)
			{
				// move
				inputDirection = transform.right * _input.move.x + transform.forward * _input.move.y;
			}

			// move the player
			_controller.Move(inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
		}

		private void UpdateSprintState()
		{
			if (IsGrabActionLocked())
			{
				_wantsSprint = false;
				return;
			}

			if (_input.crouch || _isCrouching)
			{
				_wantsSprint = false;
				return;
			}

			float currentBurdenKg = GetCurrentMovementBurdenKg();
			if (IsSprintBlockedByWeight(currentBurdenKg))
			{
				_wantsSprint = false;
				return;
			}

			bool hasMoveInput = _input.move != Vector2.zero;
			if (_vitals == null || !_input.sprint || !hasMoveInput)
			{
				_wantsSprint = _input.sprint;
				return;
			}

			if (_vitals.CurrentStamina < SprintMinStamina)
			{
				_wantsSprint = false;
				return;
			}

			float staminaCost = SprintStaminaCostPerSecond * Time.deltaTime;
			_wantsSprint = _vitals.TryUseStamina(staminaCost);
		}

		private void UpdateCrouch()
		{
			bool wantsCrouch = !IsGrabActionLocked() && _input.crouch;
			LogCrouchRequestIfNeeded(wantsCrouch);

			string standUpBlockerSummary = string.Empty;
			bool canStandUp = wantsCrouch || CanStandUp(out standUpBlockerSummary);
			float targetHeight = wantsCrouch ? CrouchHeight : (canStandUp ? _standHeight : _controller.height);
			float heightDelta = _standHeight - targetHeight;
			float targetCenterY = _standCenter.y - (heightDelta * 0.5f);

			_controller.height = Mathf.Lerp(_controller.height, targetHeight, Time.deltaTime * CrouchTransitionSpeed);
			_controller.center = Vector3.Lerp(
				_controller.center,
				new Vector3(_standCenter.x, targetCenterY, _standCenter.z),
				Time.deltaTime * CrouchTransitionSpeed
			);

			_isCrouching = _controller.height < (_standHeight - 0.05f);

			if (!wantsCrouch && _isCrouching && !canStandUp)
			{
				LogStandUpBlocked(standUpBlockerSummary);
			}
		}

		private void UpdateCameraTargetTransform()
		{
			if (CinemachineCameraTarget == null)
			{
				return;
			}

			Vector3 baseTargetPos = new Vector3(
				_cameraTargetInitialLocalPos.x,
				_cameraTargetInitialLocalPos.y + (_isCrouching ? CrouchCameraOffset : 0.0f),
				_cameraTargetInitialLocalPos.z
			);
			float crouchBlend = 1f - Mathf.Exp(-CrouchTransitionSpeed * Time.deltaTime);
			_cameraBaseLocalPosCurrent = Vector3.Lerp(_cameraBaseLocalPosCurrent, baseTargetPos, crouchBlend);

			Vector3 targetMotionOffset = Vector3.zero;
			float targetRoll = 0f;
			if (EnableCameraMotion)
			{
				float motionScale = GetGroundMotionScale();
				if (motionScale > 0f)
				{
					float burdenT = GetMovementBurdenNormalized();
					float weightBobFrequencyMultiplier = Mathf.Lerp(1f, WeightBobFrequencyMultiplierAtMax, burdenT);
					float weightBobAmplitudeMultiplier = Mathf.Lerp(1f, WeightBobAmplitudeMultiplierAtMax, burdenT);
					float weightStrafeTiltMultiplier = Mathf.Lerp(1f, WeightStrafeTiltMultiplierAtMax, burdenT);
					float weightDownOffset = Mathf.Lerp(0f, WeightCameraDownOffsetAtMax, burdenT);

					float bobFrequency = WalkBobFrequency;
					if (_wantsSprint)
					{
						bobFrequency *= SprintBobFrequencyMultiplier;
					}

					if (_isCrouching)
					{
						bobFrequency *= CrouchBobFrequencyMultiplier;
					}
					bobFrequency *= weightBobFrequencyMultiplier;

					float bobAmplitudeScale = motionScale;
					if (_wantsSprint)
					{
						bobAmplitudeScale *= SprintBobAmplitudeMultiplier;
					}

					if (_isCrouching)
					{
						bobAmplitudeScale *= CrouchBobAmplitudeMultiplier;
					}
					bobAmplitudeScale *= weightBobAmplitudeMultiplier;

					_cameraBobTimer += Time.deltaTime * bobFrequency;
					targetMotionOffset = FirstPersonCameraMotion.EvaluateBob(
						_cameraBobTimer,
						WalkBobHorizontalAmplitude * bobAmplitudeScale,
						WalkBobVerticalAmplitude * bobAmplitudeScale
					);
					targetRoll = FirstPersonCameraMotion.EvaluateStrafeTilt(
						_input.move.x,
						StrafeTilt * weightStrafeTiltMultiplier,
						motionScale);
					targetMotionOffset.y -= weightDownOffset * motionScale;
				}
				else
				{
					_cameraBobTimer = 0f;
				}

				if (!Grounded && !_isClimbing)
				{
					targetMotionOffset.y += FirstPersonCameraMotion.EvaluateAirborneVerticalOffset(
						_verticalVelocity,
						JumpCameraUpwardOffset,
						FallCameraDownwardOffset,
						JumpCameraMaxRiseSpeed,
						FallCameraMaxSpeed
					);
				}
			}
			else
			{
				_cameraBobTimer = 0f;
			}

			float motionBlend = 1f - Mathf.Exp(-CameraMotionBlendSpeed * Time.deltaTime);
			_cameraMotionCurrentPosOffset = Vector3.Lerp(_cameraMotionCurrentPosOffset, targetMotionOffset, motionBlend);
			_cameraMotionCurrentRoll = Mathf.Lerp(_cameraMotionCurrentRoll, targetRoll, motionBlend);

			CinemachineCameraTarget.transform.localPosition = _cameraBaseLocalPosCurrent + _cameraMotionCurrentPosOffset;
			CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, _cameraMotionCurrentRoll);
		}

		private float GetGroundMotionScale()
		{
			if (_controller == null || !Grounded || _isClimbing || _input.move == Vector2.zero)
			{
				return 0f;
			}

			float maxMoveSpeed = GetTargetGroundSpeed(GetCurrentMovementBurdenKg());
			if (maxMoveSpeed <= 0f)
			{
				return 0f;
			}

			float horizontalSpeed = new Vector3(_controller.velocity.x, 0f, _controller.velocity.z).magnitude;
			return Mathf.Clamp01(horizontalSpeed / maxMoveSpeed);
		}

		private bool CanStandUp(out string blockerSummary)
		{
			blockerSummary = string.Empty;

			if (_controller == null)
			{
				return true;
			}

			float targetHeight = Mathf.Max(_standHeight, CrouchHeight);
			float currentHeight = Mathf.Clamp(_controller.height, 0f, targetHeight);
			float addedHeight = targetHeight - currentHeight;
			if (addedHeight <= 0.001f)
			{
				return true;
			}

			float checkRadius = Mathf.Max(0.05f, _controller.radius - (_controller.skinWidth * 0.25f));
			float currentTopOffset = Mathf.Max(0f, (currentHeight * 0.5f) - _controller.radius);
			Vector3 currentTop = transform.position + _controller.center + (Vector3.up * currentTopOffset);
			Vector3 targetTop = currentTop + (Vector3.up * addedHeight);
			Collider[] blockers = Physics.OverlapCapsule(
				currentTop,
				targetTop,
				checkRadius,
				GroundLayers,
				QueryTriggerInteraction.Ignore);

			if (blockers == null || blockers.Length == 0)
			{
				return true;
			}

			blockerSummary = BuildStandUpBlockerSummary(blockers, currentTop, targetTop, checkRadius);
			return false;
		}

		private void LogCrouchRequestIfNeeded(bool wantsCrouch)
		{
			if (!DebugCrouchState)
			{
				return;
			}

			if (_hasLoggedCrouchRequestState && wantsCrouch == _lastLoggedCrouchRequestState)
			{
				return;
			}

			_hasLoggedCrouchRequestState = true;
			_lastLoggedCrouchRequestState = wantsCrouch;
			Debug.Log(
				$"[CrouchDebug] Request changed. wantsCrouch={wantsCrouch}, inputCrouch={_input.crouch}, isCrouching={_isCrouching}, controllerHeight={_controller.height:F3}",
				this);
		}

		private void LogStandUpBlocked(string blockerSummary)
		{
			if (!DebugCrouchState)
			{
				return;
			}

			if (Time.time - _lastCrouchBlockedLogTime < Mathf.Max(0.05f, CrouchDebugLogCooldown))
			{
				return;
			}

			_lastCrouchBlockedLogTime = Time.time;
			Debug.LogWarning(
				$"[CrouchDebug] Stand up blocked. inputCrouch={_input.crouch}, isCrouching={_isCrouching}, controllerHeight={_controller.height:F3}, grounded={Grounded}. {blockerSummary}",
				this);
		}

		private string BuildStandUpBlockerSummary(Collider[] blockers, Vector3 currentTop, Vector3 targetTop, float checkRadius)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("Clearance capsule ");
			builder.Append(currentTop.ToString("F3"));
			builder.Append(" -> ");
			builder.Append(targetTop.ToString("F3"));
			builder.Append(", radius=");
			builder.Append(checkRadius.ToString("F3"));
			builder.Append(", blockers=");

			bool foundAny = false;
			for (int i = 0; i < blockers.Length; i++)
			{
				Collider blocker = blockers[i];
				if (blocker == null)
				{
					continue;
				}

				if (blocker.transform == transform || blocker.transform.IsChildOf(transform))
				{
					continue;
				}

				if (foundAny)
				{
					builder.Append(" | ");
				}

				foundAny = true;
				builder.Append(blocker.name);
				builder.Append(" [layer=");
				builder.Append(LayerMask.LayerToName(blocker.gameObject.layer));
				builder.Append(", center=");
				builder.Append(blocker.bounds.center.ToString("F3"));
				builder.Append(']');
			}

			if (!foundAny)
			{
				builder.Append("none");
			}

			return builder.ToString();
		}

		private void JumpAndGravity()
		{
			if (_isClimbing)
			{
				_verticalVelocity = 0f;
				if (!IsGrabActionLocked() && JumpToExitClimb && _input.jump)
				{
					StopClimb();
					_verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
				}
				return;
			}

			if (Grounded)
			{
				// reset the fall timeout timer
				_fallTimeoutDelta = FallTimeout;

				// stop our velocity dropping infinitely when grounded
				if (_verticalVelocity < 0.0f)
				{
					_verticalVelocity = -2f;
				}

				// Jump
				if (!IsGrabActionLocked() && _input.jump && _jumpTimeoutDelta <= 0.0f)
				{
					// the square root of H * -2 * G = how much velocity needed to reach desired height
					_verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
				}

				// jump timeout
				if (_jumpTimeoutDelta >= 0.0f)
				{
					_jumpTimeoutDelta -= Time.deltaTime;
				}
			}
			else
			{
				// reset the jump timeout timer
				_jumpTimeoutDelta = JumpTimeout;

				// fall timeout
				if (_fallTimeoutDelta >= 0.0f)
				{
					_fallTimeoutDelta -= Time.deltaTime;
				}

				// if we are not grounded, do not jump
				_input.jump = false;
			}

			// apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
			if (_verticalVelocity < _terminalVelocity)
			{
				_verticalVelocity += Gravity * Time.deltaTime;
			}
		}

		private void ClimbMove()
		{
			float verticalInput = _input.move.y;
			if (_currentLadder != null && verticalInput > 0f && HasReachedLadderTop())
			{
				return;
			}
			Vector3 climbDirection = _currentLadder != null
				? _currentLadder.GetClimbDirection()
				: Vector3.up;
			float climbSpeed = ClimbSpeed * EvaluateMovementWeightMultiplier(GetCurrentMovementBurdenKg(), ClimbWeightPenaltyScale);
			Vector3 climbVelocity = climbDirection * (verticalInput * climbSpeed);
			_controller.Move(climbVelocity * Time.deltaTime);
		}

		private float GetCurrentMovementBurdenKg()
		{
			return _interactionSystem != null
				? Mathf.Max(0f, _interactionSystem.CurrentMovementBurdenKg)
				: 0f;
		}

		private float GetTargetGroundSpeed(float burdenWeightKg)
		{
			float baseSpeed = _isCrouching ? CrouchSpeed : (_wantsSprint ? SprintSpeed : MoveSpeed);
			float penaltyScale = _isCrouching ? CrouchWeightPenaltyScale : 1f;
			return baseSpeed * EvaluateMovementWeightMultiplier(burdenWeightKg, penaltyScale);
		}

		private float EvaluateMovementWeightMultiplier(float burdenWeightKg, float penaltyScale = 1f)
		{
			if (!EnableMovementWeightPenalty || burdenWeightKg <= 0f)
			{
				return 1f;
			}

			float clampedPenaltyScale = Mathf.Clamp01(penaltyScale);
			float clampedMinimumMultiplier = Mathf.Clamp(MinimumWeightSpeedMultiplier, 0.05f, 1f);
			float targetMinimumMultiplier = Mathf.Lerp(1f, clampedMinimumMultiplier, clampedPenaltyScale);
			if (WeightForMinimumSpeed <= 0f)
			{
				return targetMinimumMultiplier;
			}

			float burdenT = Mathf.Clamp01(burdenWeightKg / WeightForMinimumSpeed);
			return Mathf.Lerp(1f, targetMinimumMultiplier, burdenT);
		}

		private bool IsSprintBlockedByWeight(float burdenWeightKg)
		{
			return EnableMovementWeightPenalty &&
				SprintDisabledWeight > 0f &&
				burdenWeightKg >= SprintDisabledWeight;
		}

		private float GetMovementBurdenNormalized()
		{
			if (!EnableWeightCameraMotionImpact)
			{
				return 0f;
			}

			if (WeightForMinimumSpeed <= 0f)
			{
				return GetCurrentMovementBurdenKg() > 0f ? 1f : 0f;
			}

			return Mathf.Clamp01(GetCurrentMovementBurdenKg() / WeightForMinimumSpeed);
		}

		public void StartClimb(Ladder ladder)
		{
			if (ladder == null)
			{
				return;
			}

			_currentLadder = ladder;
			_isClimbing = true;
			_verticalVelocity = 0f;
			_climbGroundedGraceTimer = Mathf.Max(0f, ClimbStartGroundedGraceDuration);
		}

		public void TryStartClimbFromInteract(Ladder ladder)
		{
			if (ladder == null)
			{
				return;
			}

			if (_isClimbStartTransitioning && _currentLadder == ladder)
			{
				return;
			}

			if (_climbStartRoutine != null)
			{
				CancelClimbStartTransition(clearCurrentLadder: false);
			}

			_climbStartRoutine = StartCoroutine(BeginClimbFromInteractRoutine(ladder));
		}

		public void StopClimb()
		{
			_isClimbing = false;
			_climbGroundedGraceTimer = 0f;
			CancelClimbStartTransition(clearCurrentLadder: false);
			_currentLadder = null;
		}

		private void RaiseUntilNotGrounded()
		{
			if (_controller == null || ClimbStartStep <= 0f || ClimbStartMaxSteps <= 0)
			{
				return;
			}

			Vector3 climbDirection = _currentLadder != null
				? _currentLadder.GetClimbDirection()
				: Vector3.up;
			if (climbDirection.sqrMagnitude <= _threshold)
			{
				climbDirection = Vector3.up;
			}

			for (int i = 0; i < ClimbStartMaxSteps; i++)
			{
				transform.position += climbDirection.normalized * ClimbStartStep;
				GroundedCheck();
				if (!Grounded)
				{
					break;
				}
			}
		}

		private IEnumerator BeginClimbFromInteractRoutine(Ladder ladder)
		{
			_currentLadder = ladder;
			_isClimbStartTransitioning = true;
			_isClimbing = false;
			_verticalVelocity = 0f;

			yield return SmoothMoveToPosition(
				ResolveLadderStartPosition(ladder),
				Mathf.Max(0f, ClimbStartSnapDuration),
				Mathf.Max(0f, ClimbStartSnapArcHeight));

			_isClimbStartTransitioning = false;
			_climbStartRoutine = null;
			StartClimb(ladder);
			GroundedCheck();

			if (Grounded)
			{
				RaiseUntilNotGrounded();
				GroundedCheck();
			}
		}

		private void CancelClimbStartTransition(bool clearCurrentLadder)
		{
			if (_climbStartRoutine != null)
			{
				StopCoroutine(_climbStartRoutine);
				_climbStartRoutine = null;
			}

			if (_climbStartRestoreController && _controller != null)
			{
				_controller.enabled = true;
				_climbStartRestoreController = false;
			}

			_isClimbStartTransitioning = false;
			if (clearCurrentLadder)
			{
				_currentLadder = null;
			}
		}

		private Vector3 ResolveLadderStartPosition(Ladder ladder)
		{
			if (ladder == null)
			{
				return transform.position;
			}

			float controllerRadius = _controller != null ? _controller.radius : 0.3f;
			float skinWidth = _controller != null ? Mathf.Max(0f, _controller.skinWidth) : 0f;
			float clearance = controllerRadius + skinWidth + Mathf.Max(0f, ClimbStartFacePadding);
			return ladder.GetClimbAttachPoint(transform.position, clearance);
		}

		private IEnumerator SmoothMoveToPosition(Vector3 targetPosition, float duration, float arcHeight)
		{
			if (_controller == null ||
				duration <= 0.001f ||
				(transform.position - targetPosition).sqrMagnitude <= 0.0001f)
			{
				transform.position = targetPosition;
				yield break;
			}

			_climbStartRestoreController = _controller.enabled;
			if (_climbStartRestoreController)
			{
				_controller.enabled = false;
			}

			Vector3 startPosition = transform.position;
			float elapsed = 0f;

			while (elapsed < duration)
			{
				elapsed += Time.deltaTime;
				float t = Mathf.Clamp01(elapsed / duration);
				float easedT = Mathf.SmoothStep(0f, 1f, t);
				Vector3 position = Vector3.Lerp(startPosition, targetPosition, easedT);

				if (arcHeight > 0f)
				{
					position += Vector3.up * (Mathf.Sin(easedT * Mathf.PI) * arcHeight);
				}

				transform.position = position;
				yield return null;
			}

			transform.position = targetPosition;

			if (_climbStartRestoreController && _controller != null)
			{
				_controller.enabled = true;
			}

			_climbStartRestoreController = false;
		}

		private bool HasReachedLadderTop()
		{
			if (_currentLadder == null)
			{
				return false;
			}

			Vector3 climbDirection = _currentLadder.GetClimbDirection();
			Vector3 bottom = _currentLadder.GetClimbBottomWorld();
			float maxDistance = _currentLadder.GetClimbExtent();
			float currentDistance = Vector3.Dot(transform.position - bottom, climbDirection);
			return currentDistance >= maxDistance;
		}

		private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
		{
			if (lfAngle < -360f) lfAngle += 360f;
			if (lfAngle > 360f) lfAngle -= 360f;
			return Mathf.Clamp(lfAngle, lfMin, lfMax);
		}

		private bool IsGrabActionLocked()
		{
			return _interactionSystem != null && _interactionSystem.IsGrabActive;
		}

		private void OnDrawGizmosSelected()
		{
			Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
			Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

			if (Grounded) Gizmos.color = transparentGreen;
			else Gizmos.color = transparentRed;

			// when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
			Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
		}
	}
}
