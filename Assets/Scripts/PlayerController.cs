using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 4f;
    [Tooltip("Asignar Player/Move de InputSystem_Actions.")]
    [SerializeField] private InputActionReference moveActionReference;

    private Rigidbody2D playerBody;
    private InputAction moveAction;
    private Vector2 movementInput;

    // Antes de moverse por primera vez, el jugador mira hacia abajo.
    public Vector2 LastMovementDirection { get; private set; } = Vector2.down;

    private void Awake()
    {
        playerBody = GetComponent<Rigidbody2D>();
        playerBody.gravityScale = 0f;
    }

    private void OnEnable()
    {
        if (moveActionReference == null || moveActionReference.action == null)
        {
            Debug.LogError("Asigna Player/Move al campo Move Action Reference.", this);
            enabled = false;
            return;
        }

        // La copia permite gestionar la entrada sin desactivar la accion compartida.
        moveAction = moveActionReference.action.Clone();
        moveAction.Enable();
    }

    private void Update()
    {
        // Limitar la magnitud evita que las diagonales sean mas rapidas.
        movementInput = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
        if (movementInput.sqrMagnitude > 0.0001f)
            LastMovementDirection = movementInput.normalized;
    }

    private void FixedUpdate()
    {
        // linearVelocity se expresa en unidades por segundo.
        playerBody.linearVelocity = movementInput * moveSpeed;
    }

    private void OnDisable()
    {
        movementInput = Vector2.zero;
        if (playerBody != null)
        {
            playerBody.linearVelocity = Vector2.zero;
        }

        if (moveAction != null)
        {
            moveAction.Disable();
            moveAction.Dispose();
            moveAction = null;
        }
    }
}
