using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static Unity.Burst.Intrinsics.X86.Avx;

public class Entity : MonoBehaviour
{
    public List<Component> components = new List<Component>();
    public int Direction;


    //Stats
    public float MovementSpeed;
    public float Health;

    private void Update()
    {
        float movsped = 0;

        foreach (var component in components)
        {
            if (component is DirectionalSprite)
            {
                component.GetComponent<DirectionalSprite>().UpdateSprite(Direction);
            }

            if (component is Movement)
            {
                movsped += component.GetComponent<Movement>().Speed;
            }


        }


        MovementSpeed = movsped;

    }
    private void Start()
    {
        FindComps();
    }

    public void FindComps()
    {
        components.Clear();
        foreach(var comp in GetComponentsInChildren<Component>())
        {

            if(comp is Transform | comp is SpriteRenderer)
            {
                continue;
            }
            components.Add(comp);
        }
    }
}
