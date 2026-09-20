using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private float moveSpeed = 5f; // Velocidad de movimiento del jugador
    private Rigidbody2D rb; // Referencia al rigid body del jugador
    private Vector2 moveInput; // Input de movimiento

    void Start()
    {
        // Obtenemos y asignamos el componente rigid body
        rb = GetComponent<Rigidbody2D>();
    }

    public void OnMove(InputValue value)
    {
        // Cuando el jugador usa los controles de movimiento, obtenemos el valor de la dirección
        moveInput = value.Get<Vector2>();
    }

    void Update()
    {
        // Usando la dirección del input y la velocidad de movimiento, movemos al personaje del jugador
        rb.linearVelocity = moveInput * moveSpeed;
    }
}
