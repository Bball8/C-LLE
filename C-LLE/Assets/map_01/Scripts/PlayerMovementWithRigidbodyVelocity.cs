using UnityEngine;

public class PlayerMovementWithRigidbodyVelocity : MonoBehaviour
{
    [Header("Speed Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;

    [Header("Animation offset")]
    public float rotationOffset = 270f;

    private Rigidbody rb;
    private Vector2 moveInput = Vector2.zero;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void SetMoveInput(Vector2 input)
    {
        input.x = Mathf.Clamp(input.x, -1f, 1f);
        input.y = Mathf.Clamp(input.y, -1f, 1f);
        if (input.sqrMagnitude > 1f)
            input.Normalize();
        moveInput = input;
    }

    private void FixedUpdate()
    {
        Vector3 moveDirection = new Vector3(moveInput.x, 0f, moveInput.y);

        rb.linearVelocity = new Vector3(
            moveDirection.x * moveSpeed,
            rb.linearVelocity.y,
            moveDirection.z * moveSpeed
        );

        // Keep only Y rotation
        rb.rotation = Quaternion.Euler(0f, rb.rotation.eulerAngles.y, 0f);

        if (moveDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            targetRotation *= Quaternion.Euler(0f, rotationOffset, 0f);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }
}