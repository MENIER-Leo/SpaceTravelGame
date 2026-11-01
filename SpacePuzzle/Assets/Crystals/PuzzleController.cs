using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BeamManager))]
public class PuzzleController : MonoBehaviour
{
    [SerializeField] private float confirmDelay = 0.5f;   // la solution doit tenir ce temps avant validation
    [SerializeField] private bool lockWhenSolved = true;  // bloque tous les cristaux une fois résolu

    [Header("Affichage (optionnel)")]
    [SerializeField] private TMP_Text statusText;

    [Header("Événements")]
    public UnityEvent onSolved;
    public UnityEvent onUnsolved;

    private BeamManager beamManager;
    private Crystal[] crystals;
    private readonly List<Crystal> receivers = new List<Crystal>();
    private bool isSolved;
    private bool pending;
    private float pendingTimer;

    public bool IsSolved => isSolved;

    private void Awake()
    {
        beamManager = GetComponent<BeamManager>();
        RefreshCrystals();
    }

    private void OnEnable()
    {
        beamManager.OnRecomputed += Evaluate;
    }

    private void OnDisable()
    {
        beamManager.OnRecomputed -= Evaluate;
    }

    private void Start()
    {
        Evaluate();
    }

    public void RefreshCrystals()
    {
        crystals = GetComponentsInChildren<Crystal>();
        receivers.Clear();

        foreach (Crystal c in crystals)
        {
            if (c.crystalType == CrystalType.Receiver && c.targetColor != Colors.None)
                receivers.Add(c);
        }

        if (receivers.Count == 0)
            Debug.LogWarning($"{name} : aucun récepteur avec une couleur cible.");
    }

    private void Evaluate()
    {
        int satisfied = CountSatisfied();
        bool allSatisfied = receivers.Count > 0 && satisfied == receivers.Count;

        if (allSatisfied && !AnyRotating())
        {
            if (!isSolved && !pending)
            {
                pending = true;
                pendingTimer = 0f;
            }
        }
        else
        {
            pending = false;

            if (isSolved && !allSatisfied)
            {
                isSolved = false;
                onUnsolved.Invoke();
                Debug.Log($"{name} : puzzle de nouveau non résolu.");
            }
        }

        UpdateStatus(satisfied);
    }

    private void Update()
    {
        if (!pending) return;

        pendingTimer += Time.deltaTime;
        if (pendingTimer < confirmDelay) return;

        pending = false;
        Solve();
    }

    private void Solve()
    {
        // Sécurité : on revérifie au moment de valider
        if (CountSatisfied() != receivers.Count || AnyRotating()) return;

        isSolved = true;

        if (lockWhenSolved)
        {
            foreach (Crystal c in crystals)
            {
                c.StopTurning();
                c.rotatable = false;
            }
        }

        UpdateStatus(receivers.Count);
        onSolved.Invoke();
        Debug.Log($"{name} : puzzle résolu !");
    }

    private int CountSatisfied()
    {
        int count = 0;
        foreach (Crystal r in receivers)
            if (r.IsSatisfied) count++;
        return count;
    }

    private bool AnyRotating()
    {
        foreach (Crystal c in crystals)
            if (c.IsRotating) return true;
        return false;
    }

    private void UpdateStatus(int satisfied)
    {
        if (statusText == null) return;
        statusText.text = isSolved ? "Résolu !" : $"Récepteurs : {satisfied}/{receivers.Count}";
    }
}
