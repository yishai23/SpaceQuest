using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasePlate : MonoBehaviour
{

    public Entity Entity;
    public Building Building;
    public bool Built;

    private void Start()
    {
        Entity = GetComponent<SubEntity>().MainEntity;
        transform.position = RoundVec3(transform.position);
    }

    public Vector3 RoundVec3(Vector3 vec)
    {
        return new Vector3(Mathf.Round(vec.x), Mathf.Round(vec.y), 0);
    }

}
