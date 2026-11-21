using UnityEngine;

[RequireComponent(typeof(Crystal))]
public class Phototropisme : MonoBehaviour
{
    
    [SerializeField] private float interval = 3f;
    [SerializeField] private int direction = 1;

    [Header("Frémissement avant de tourner")]
    [SerializeField] private Transform visual;
    [SerializeField] private float shiverTime = 0.5f;
    [SerializeField] private float shiverAngle = 4f;

    private Crystal crystal;
    private float timer;
    private Quaternion visualRest;

    private void Awake()
    {
        crystal = GetComponent<Crystal>();
        if (visual != null) visualRest = visual.localRotation;
    }

    private void OnDisable() => ResetTimer();

    public void ResetTimer()
    {
        timer = 0f;
        if (visual != null) visual.localRotation = visualRest;
    }

    private void Update()
    {
        if (crystal.IsLit || crystal.IsRotating || crystal.IsAbsorbed) { ResetTimer(); return; }

        timer += Time.deltaTime;

        if (visual != null && interval - timer < shiverTime)
            visual.localRotation = visualRest * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 40f) * shiverAngle);

        if (timer >= interval)
        {
            ResetTimer();
            crystal.AutoStep(direction);
        }
    }
}
