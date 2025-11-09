using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class Background : MonoBehaviour
{
    public Camera targetCamera; // main camera to follow; if null, Camera.main used
    public List<ParallaxLayer> layers = new List<ParallaxLayer>();

    Vector3 previousCameraPos;

    void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        previousCameraPos = targetCamera != null ? targetCamera.transform.position : Vector3.zero;

        // Make sure layers have references (use children if not assigned)
        if (layers == null || layers.Count == 0)
        {
            layers = new List<ParallaxLayer>(GetComponentsInChildren<ParallaxLayer>());
        }
    }

    void LateUpdate()
    {
        if (targetCamera == null) return;

        Vector3 camPos = targetCamera.transform.position;
        Vector3 delta = camPos - previousCameraPos;

        // Apply delta to each layer
        for (int i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            if (layer == null) continue;
            layer.ApplyParallax(delta, targetCamera);
        }

        previousCameraPos = camPos;
    }
}

