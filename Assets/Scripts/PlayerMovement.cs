using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float lookSensitivity = 0.1f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.0f;
    public float verticalLookLimit = 85f;

    private CharacterController controller;
    private Transform cameraTransform;
    private PlayerGlobalActions playerGlobalActions;

    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool jumpPressed = false;
    private bool sprintHeld = false;

    private float currentSpeed;
    private float verticalVelocity = 0f;
    private float cameraVerticalRotation = 0f;
    private Vector3 horizontalMove;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerGlobalActions = GetComponent<PlayerGlobalActions>(); // PlayerGlobalActions должен быть на том же объекте

        cameraTransform = GetComponentInChildren<Camera>()?.transform;
        if (cameraTransform == null)
        {
            Debug.LogError("PlayerMovement: Камера не найдена как дочерний объект! Скрипт будет отключен.", this);
            enabled = false;
            return;
        }
        cameraTransform.localPosition = new Vector3(0, controller.height * 0.8f - controller.radius, controller.radius * 0.5f);

        if (playerGlobalActions == null)
        {
            Debug.LogWarning("PlayerMovement: PlayerGlobalActions не найден. Поворот камеры мышью не будет отключаться при свободном курсоре.", this);
        }
        currentSpeed = walkSpeed;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed && controller.isGrounded)
        {
            jumpPressed = true;
        }
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        sprintHeld = context.ReadValueAsButton();
    }

    public void OnCrouch(InputAction.CallbackContext context)
    {
        // Логика приседания (будет реализована позже)
    }

    void Update()
    {
        HandleSprint();
        HandleMovement();
        HandleLook();
        HandleGravityAndJump();

        Vector3 finalMove = horizontalMove + (Vector3.up * verticalVelocity);
        controller.Move(finalMove * Time.deltaTime);
    }

    private void HandleSprint()
    {
        currentSpeed = sprintHeld ? sprintSpeed : walkSpeed;
    }

    private void HandleMovement()
    {
        Vector3 moveDirection = (transform.forward * moveInput.y) + (transform.right * moveInput.x);
        horizontalMove = moveDirection.normalized * currentSpeed;
    }

    private void HandleLook()
    {
        if (playerGlobalActions != null && playerGlobalActions.IsCursorFree)
        {
            return; // Не вращаем камеру, если курсор свободен
        }

        transform.Rotate(Vector3.up, lookInput.x * lookSensitivity);

        cameraVerticalRotation -= lookInput.y * lookSensitivity;
        cameraVerticalRotation = Mathf.Clamp(cameraVerticalRotation, -verticalLookLimit, verticalLookLimit);
        cameraTransform.localEulerAngles = new Vector3(cameraVerticalRotation, 0, 0);
    }

    private void HandleGravityAndJump()
    {
        bool isGrounded = controller.isGrounded;

        if (isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f; // Небольшая сила вниз, чтобы контроллер оставался на земле
        }

        if (jumpPressed && isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpPressed = false;
        }

        if (!isGrounded) // Применяем гравитацию, только если в воздухе
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
    }
}