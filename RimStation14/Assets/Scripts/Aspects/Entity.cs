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

    public void SetDirection(int dir)
    {
        Direction = dir;

        foreach (var component in components)
        {
            if (component is SpriteScript)
            {
                if (component.GetComponent<SpriteScript>().SpriteType == "directional")
                {
                    component.GetComponent<SpriteScript>().SetFrame(Direction);
                }
            }
        }
    }

    public void UpdateStats()
    {
        float movsped = 0;
        foreach (var component in components)
        {

            if (component is Movement)
            {
                movsped += component.GetComponent<Movement>().Speed;
            }


        }
        MovementSpeed = movsped;

    }


    private void Update()
    {

    }
    private void Start()
    {
        FindComps();
        UpdateStats();
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
