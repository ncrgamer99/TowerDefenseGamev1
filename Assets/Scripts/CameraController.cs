using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float moveSpeed = 8f;

    private void Update()
    {
        Vector3 moveDirection = Vector3.zero;

        if (Input.GetKey(KeyCode.UpArrow))
        {
            moveDirection += transform.forward;
        }

        if (Input.GetKey(KeyCode.DownArrow))
        {
            moveDirection -= transform.forward;
        }

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            moveDirection -= transform.right;
        }

        if (Input.GetKey(KeyCode.RightArrow))
        {
            moveDirection += transform.right;
        }

        moveDirection.y = 0f;
        moveDirection.Normalize();

        transform.position += moveDirection * moveSpeed * Time.deltaTime;
    }
}