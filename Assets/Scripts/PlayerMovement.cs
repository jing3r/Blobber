using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq; // Для Enumerable.Any() и Min() в GetCurrentPartyMovementSpeed_Test, если он еще нужен

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    // Убираем walkSpeed и sprintSpeed
    // public float walkSpeed = 5f; 
    // public float sprintSpeed = 8f; 

    [Tooltip("Множитель скорости при спринте. Например, 1.5 означает на 50% быстрее.")]
    public float sprintSpeedMultiplier = 1.5f; 
    public float lookSensitivity = 0.1f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.0f;
    public float verticalLookLimit = 85f;

    private CharacterController controller;
    private Transform cameraTransform;
    private PlayerGlobalActions playerGlobalActions;
    private PartyManager partyManager; 

    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool jumpPressed = false;
    private bool sprintHeld = false;

    // currentSpeed и horizontalMove теперь полностью рассчитываются в HandleMovement
    private float verticalVelocity = 0f;
    private float cameraVerticalRotation = 0f;
    // private Vector3 horizontalMove; // Объявим его внутри Update или HandleMovement, если он больше нигде не нужен

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerGlobalActions = GetComponent<PlayerGlobalActions>(); 

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
        
        partyManager = GetComponent<PartyManager>(); 
        if (partyManager == null) 
        {
             partyManager = GetComponentInParent<PartyManager>();
        }
        if (partyManager == null)
        {
            Debug.LogError("PlayerMovement: PartyManager не найден! Скорость отряда не будет рассчитываться корректно, будет использована дефолтная скорость одиночки.", this);
        }
        // currentSpeed = walkSpeed; // Убрали, так как currentSpeed больше нет
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
        // HandleSprint(); // Убрали, логика спринта теперь в HandleMovement
        
        Vector3 calculatedHorizontalMove = HandleMovementAndGetVector(); // Получаем горизонтальное движение
        HandleLook(); // Поворот остается как был
        HandleGravityAndJump(); // Гравитация и прыжок остаются как были

        Vector3 finalMove = calculatedHorizontalMove + (Vector3.up * verticalVelocity);
        controller.Move(finalMove * Time.deltaTime);

        // Отладочный лог, если движение все еще не работает
        // if (finalMove.magnitude < 0.01f && (moveInput.x != 0 || moveInput.y != 0) && calculatedHorizontalMove.magnitude < 0.01f)
        // {
        //     Debug.LogWarning($"PlayerMovement Update: finalMove ({finalMove.magnitude}) or horizontalMove ({calculatedHorizontalMove.magnitude}) is very small despite input. Check calculated speed.");
        // }
    }

    // HandleSprint() удален

    private float GetCurrentPartyBaseSpeed() // Переименовал для ясности, что это базовая скорость до спринта
    {
        if (partyManager == null || partyManager.partyMembers.Count == 0)
        {
            // Фоллбэк, если нет PartyManager: используем CharacterStats на этом объекте, если он есть
            CharacterStats localStats = GetComponent<CharacterStats>(); 
            if (localStats != null) {
                // Debug.Log("PlayerMovement: Using local CharacterStats speed as fallback.");
                return localStats.CurrentMovementSpeed;
            }
            Debug.LogWarning("PlayerMovement: PartyManager not found and no local CharacterStats. Using default speed 3f.");
            return 3f; 
        }

        float totalPartySpeed = 0f;
        int contributingMembersCount = 0;

        foreach (CharacterStats member in partyManager.partyMembers)
        {
            if (member != null && !member.IsDead)
            {
                totalPartySpeed += member.CurrentMovementSpeed;
                contributingMembersCount++;
            }
        }

        if (contributingMembersCount == 0) 
        {
            // Debug.Log("PlayerMovement: No alive party members. Using very slow speed 0.5f.");
            return 0.5f; 
        }

        float averagePartySpeed = totalPartySpeed / contributingMembersCount;
        averagePartySpeed = Mathf.Ceil(averagePartySpeed); 
        
        return Mathf.Max(0.5f, averagePartySpeed);
    }

    // Изменяем HandleMovement, чтобы он ВОЗВРАЩАЛ вектор движения
    private Vector3 HandleMovementAndGetVector()
    {
        float baseSpeed = GetCurrentPartyBaseSpeed();
        float actualSpeed = sprintHeld ? (baseSpeed * sprintSpeedMultiplier) : baseSpeed;
        
        // Debug.Log($"PlayerMovement HandleMovement: BaseSpeed={baseSpeed:F1}, SprintHeld={sprintHeld}, ActualSpeed={actualSpeed:F1}");

        Vector3 moveDirection = (transform.forward * moveInput.y) + (transform.right * moveInput.x);
        
        // Если actualSpeed очень мал, и есть ввод, это проблема
        if (actualSpeed < 0.1f && (moveInput.x != 0 || moveInput.y != 0))
        {
            Debug.LogError($"PlayerMovement HandleMovement: ActualSpeed is critically low ({actualSpeed}) while input is present. Investigate CharacterStats speeds.");
        }

        return moveDirection.normalized * actualSpeed;
    }
    
    // HandleLook() и HandleGravityAndJump() остаются такими же, как в твоей рабочей версии
    private void HandleLook()
    {
        if (playerGlobalActions != null && playerGlobalActions.IsCursorFree)
        {
            return; 
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
            verticalVelocity = -2f; 
        }
        if (jumpPressed && isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpPressed = false;
        }
        if (!isGrounded) 
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
    }
}