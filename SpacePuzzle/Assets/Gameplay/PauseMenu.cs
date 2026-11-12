using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    [SerializeField] private GameObject menu;
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
    [SerializeField, Tooltip("Scripts coupés pendant la pause (déplacement, caméra, interactions...)")]
    private MonoBehaviour[] disableWhilePaused;

    private bool[] previousStates;
    private CursorLockMode previousLock;
    private bool previousVisible;
    [SerializeField] private string menuScene = "Menu";

    private void Start()
    {
        if (menu != null) menu.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(pauseKey))
        {
            if (IsPaused) Resume();
            else Pause();
        }
    }

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;
        Time.timeScale = 0f;

        // On mémorise l'état de chaque script pour le restaurer tel quel
        // (ex. : si une cinématique avait déjà coupé le déplacement).
        previousStates = new bool[disableWhilePaused.Length];
        for (int i = 0; i < disableWhilePaused.Length; i++)
        {
            if (disableWhilePaused[i] == null) continue;
            previousStates[i] = disableWhilePaused[i].enabled;
            disableWhilePaused[i].enabled = false;
        }

        previousLock = Cursor.lockState;
        previousVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (menu != null) menu.SetActive(true);
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        Time.timeScale = 1f;

        for (int i = 0; i < disableWhilePaused.Length; i++)
            if (disableWhilePaused[i] != null) disableWhilePaused[i].enabled = previousStates[i];

        Cursor.lockState = previousLock;
        Cursor.visible = previousVisible;

        if (menu != null) menu.SetActive(false);
    }

    public void Restart()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void BackToMenu()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuScene);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        // Sécurité : ne jamais laisser le jeu figé en changeant de scène.
        if (IsPaused) { IsPaused = false; Time.timeScale = 1f; }
    }
}
