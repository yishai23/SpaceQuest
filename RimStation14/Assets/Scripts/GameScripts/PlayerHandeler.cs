using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;

public class PlayerHandeler : MonoBehaviour
{
    public Brain PlayerBrain;

    public Camera Camera;
    public float Zoom = 5;
    public float Smooth = 0.1f;

    public bool SpawnGhost;

    public void ChangeBrain(Brain brain)
    {

    }



    private void Update()
    {
        if (PlayerBrain != null)
        {
            Vector2 input = new Vector2(
                Input.GetAxisRaw("Horizontal"), // A/D or Left/Right
                Input.GetAxisRaw("Vertical")    // W/S or Up/Down
            );

            PlayerBrain.Move(input);



            Camera.transform.position =new Vector3( Vector2.Lerp(Camera.transform.position, PlayerBrain.transform.position, Smooth).x, Vector2.Lerp(Camera.transform.position, PlayerBrain.transform.position, Smooth).y,-10);
            Camera.orthographicSize = Mathf.Lerp(Camera.orthographicSize, Zoom, 0.1f);


        }
        else
        {
            if (SpawnGhost)
            {
                TextAsset jsonFile = Resources.Load<TextAsset>("Entities/Mobs/ghost");


                var args = new Dictionary<string, string>();
              //  args["gender"] = "m"; // Or "f"

                // Spawn entity with args
                GameObject ghost = EntitySpawner.instance.SpawnEntity(jsonFile.text, Vector3.zero, args);
                PlayerBrain = ghost.GetComponentInChildren<Brain>();
            }
        }
    }
}
