using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{

    public string path = "Entities/human";
    private void Update()
    {
      
    }
    void test()
    {
        // Find spawner
        EntitySpawner spawner = FindObjectOfType<EntitySpawner>();
        if (spawner == null)
        {
            Debug.LogError("No EntitySpawner found in the scene!");
            return;
        }

        // Load JSON
        TextAsset jsonFile = Resources.Load<TextAsset>(path);
        if (jsonFile == null)
        {
            Debug.LogError("Could not find Resources/Entities/human.json!");
            return;
        }

        // Create arguments dictionary
        var args = new Dictionary<string, string>();
        args["gender"] = "f"; // Or "f"

        // Spawn entity with args
        GameObject human = spawner.SpawnEntity(jsonFile.text, Vector3.zero, args);

        if (human != null)
            Debug.Log($"Spawned entity: {human.name}");
    }

}
