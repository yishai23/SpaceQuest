using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Brain : MonoBehaviour
{
    public Entity Entity;

    private void Start()
    {
        Entity = GetComponent<SubEntity>().MainEntity;
    }

    public void Move(Vector2 input)
    {
        if (Entity == null) return;

        // Calculate movement
        Vector3 movement = new Vector3(input.x, input.y, 0) * Entity.MovementSpeed * Time.deltaTime;
        Entity.transform.position += movement;

        // Set Direction based on input
        SetDirection(input);
    }

    private void SetDirection(Vector2 input)
    {
        if (input == Vector2.zero) return;

        // Horizontal takes precedence
        if (input.x != 0)
        {
            Entity.Direction = input.x > 0 ? 1 : 3; // 3 = right, 1 = left
        }
        else if (input.y != 0)
        {
            Entity.Direction = input.y > 0 ? 2 : 0; // 2 = up, 0 = down
        }
    }
}
