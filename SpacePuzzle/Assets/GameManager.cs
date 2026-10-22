using UnityEngine;

public class GameManager : MonoBehaviour
{

    GameObject player;
    GameObject camera;
    PlayerMovement PMU;
    CameraFollow CVEC;

    void Start()
    {
        player = GameObject.Find("Joueur");
        PMU = player.AddComponent<PlayerMovement>();

        camera = GameObject.Find("Main Camera");
        CVEC = camera.AddComponent<CameraFollow>();
    }

    void Update()
    {
        PMU.handleInput(camera.transform);
    }

}
