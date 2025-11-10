// Brain.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Brain : MonoBehaviour
{
    public Entity Entity;

    private void Start()
    {
        var sub = GetComponent<SubEntity>();
        if (sub != null) Entity = sub.MainEntity;
    }

    public void Move(Vector2 input)
    {
        if (Entity == null) return;

        // Calculate movement
        Vector3 movement = new Vector3(input.x, input.y, 0f) * Entity.MovementSpeed * Time.deltaTime;
        Entity.transform.position += movement;

        // Set Direction based on input (horizontal precedence)
        SetDirection(input);
    }

    private void SetDirection(Vector2 input)
    {
        if (input == Vector2.zero) return;

        // Horizontal takes precedence
        if (Mathf.Abs(input.x) > 0.0001f)
        {
            // Mapping: 0 = down, 1 = up, 2 = right, 3 = left
            Entity.SetDirection(input.x > 0f ? 2 : 3);
        }
        else if (Mathf.Abs(input.y) > 0.0001f)
        {
            Entity.SetDirection(input.y > 0f ? 1 : 0);
        }
    }
}
