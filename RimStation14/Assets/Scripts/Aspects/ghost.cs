using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ghost : MonoBehaviour
{
    public float clickDistance = 0.3f; // max distance to consider a "click"

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0f; 

            Brain[] brains = FindObjectsOfType<Brain>();

            foreach (Brain brain in brains)
            {
                if (Vector3.Distance(mousePos, brain.transform.position) <= clickDistance)
                {
                    OnBrainClicked(brain);
                    break; 
                }
            }
        }
    }

    void OnBrainClicked(Brain brain)
    {
         FindAnyObjectByType<PlayerHandeler>().PlayerBrain = brain;
        Destroy(GetComponentInParent<Entity>().gameObject);

    }
}
