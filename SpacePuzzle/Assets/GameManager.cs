using UnityEngine;

public class GameManager : MonoBehaviour
{

    GameObject player;
    GameObject mainCamera;
    PlayerMovement PMU;
    CameraFollow CVEC;

    void Start()
    {
        player = GameObject.Find("Joueur");
        PMU = player.GetComponent<PlayerMovement>();
        if (PMU == null) PMU = player.AddComponent<PlayerMovement>();

        mainCamera = GameObject.Find("Main Camera");
        CVEC = mainCamera.GetComponent<CameraFollow>();
        if (CVEC == null) CVEC = mainCamera.AddComponent<CameraFollow>();
    }

    void Update()
    {
        PMU.handleInput(mainCamera.transform);
    }

}
