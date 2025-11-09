using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Building : MonoBehaviour
{
    public Entity Entity;
    public bool Built;
    public BasePlate Ancoured;

    private void Start()
    {
        Entity = GetComponentInParent<Entity>();
    }

    public void Anchor()
    {

    }


}
