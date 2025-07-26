using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;
/// <summary>
/// Управляет передвижением и вращением камеры для объекта игрока.
/// Скорость движения рассчитывается на основе средней скорости всех живых членов партии.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerGlobalActions))]
[RequireComponent(typeof(PartyManager))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Настройки движения")]
    [SerializeField] [Tooltip("Множитель скорости при спринте.")] private float sprintSpeedMultiplier = 1.5f;
    [SerializeField] private float jumpHeight = 1.0f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Настройки камеры")]
    [SerializeField] private float lookSensitivity = 0.1f;
    [SerializeField] [Range(45, 90)] private float verticalLookLimit = 85f;

    private CharacterController controller;
    private Transform cameraTransform;
    private PlayerGlobalActions playerGlobalActions;
    private PartyManager partyManager;

    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool jumpPressed;
    private bool sprintHeld;
    private float verticalVelocity;
    private float cameraVerticalRotation;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerGlobalActions = GetComponent<PlayerGlobalActions>();
        partyManager = GetComponent<PartyManager>();
        
        // Поиск камеры среди дочерних объектов. Важно для FPS-контроллера.
        cameraTransform = GetComponentInChildren<Camera>()?.transform;
        if (cameraTransform == null)
        {
            Debug.LogError($"[{nameof(PlayerMovement)}] Main camera not found as a child of '{gameObject.name}'. Disabling component.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        // Порядок важен: сначала вычисляем все векторы, затем применяем движение один раз.
        HandleLook();
        HandleGravityAndJump();
        
        Vector3 horizontalMove = CalculateHorizontalMovement();
        Vector3 finalMove = horizontalMove + (Vector3.up * verticalVelocity);
        
        controller.Move(finalMove * Time.deltaTime);
    }

    #region Input Handlers (Called by PlayerInput component)
    public void OnMove(InputAction.CallbackContext context) => moveInput = context.ReadValue<Vector2>();
    public void OnLook(InputAction.CallbackContext context) => lookInput = context.ReadValue<Vector2>();
    public void OnSprint(InputAction.CallbackContext context) => sprintHeld = context.ReadValueAsButton();
    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed && controller.isGrounded)
        {
            jumpPressed = true;
        }
    }
    #endregion

    #region Movement Logic
    private Vector3 CalculateHorizontalMovement()
    {
        float baseSpeed = GetCurrentPartyBaseSpeed();
        float currentSpeed = sprintHeld ? (baseSpeed * sprintSpeedMultiplier) : baseSpeed;

        Vector3 moveDirection = (transform.forward * moveInput.y) + (transform.right * moveInput.x);
        return moveDirection.normalized * currentSpeed;
    }

    private void HandleGravityAndJump()
    {
        if (controller.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f; // Небольшое постоянное притяжение к земле
        }

        if (jumpPressed && controller.isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpPressed = false;
        }

        verticalVelocity += gravity * Time.deltaTime;
    }

    private void HandleLook()
    {
        // Не вращаем камеру, если курсор свободен для взаимодействия с UI
        if (playerGlobalActions.IsCursorFree) return;

        transform.Rotate(Vector3.up, lookInput.x * lookSensitivity);

        cameraVerticalRotation -= lookInput.y * lookSensitivity;
        cameraVerticalRotation = Mathf.Clamp(cameraVerticalRotation, -verticalLookLimit, verticalLookLimit);
        cameraTransform.localEulerAngles = new Vector3(cameraVerticalRotation, 0, 0);
    }

    private float GetCurrentPartyBaseSpeed()
    {
        var livingMembers = partyManager.PartyMembers.ToList().Where(m => m != null && !m.IsDead).ToList();

        if (livingMembers.Count == 0)
        {
            return 0.5f;
        }
        
        float averagePartySpeed = livingMembers.Average(m => m.CurrentMovementSpeed);
        return Mathf.Max(0.5f, averagePartySpeed);
    }
    #endregion
}