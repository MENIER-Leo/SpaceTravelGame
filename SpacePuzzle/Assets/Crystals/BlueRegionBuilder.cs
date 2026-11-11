using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
#endif

// Construit en un clic (hors Play) le puzzle de la région Bleue sous cette ancre.
// Relancer la construction remplace le puzzle généré précédemment (Ctrl+Z pour annuler).
// Coordonnées locales : X sur le côté, Z vers le pôle (le Phare).
public class BlueRegionBuilder : MonoBehaviour
{
    [Header("Prefabs de cristaux (composant Crystal à la racine)")]
    [SerializeField] private GameObject emitterPrefab;
    [SerializeField] private GameObject relayPrefab;
    [SerializeField] private GameObject prismPrefab;
    [SerializeField] private GameObject mixerPrefab;
    [SerializeField] private GameObject receiverPrefab;

    [Header("Décor")]
    [Tooltip("Optionnel. Vide = sphère aplatie générée.")]
    [SerializeField] private GameObject rockPrefab;
    [SerializeField] private Material rockMaterial;
    [SerializeField] private Material iceMaterial;
    [Tooltip("Layer du rocher et du mur de glace : coché dans le Beam Mask, différent de Planete.")]
    [SerializeField] private string obstacleLayer = "Default";

    [Header("Région")]
    [Tooltip("Vide = cherché automatiquement dans la scène.")]
    [SerializeField] private RegionLight regionLight;
    [SerializeField] private string regionProperty = "_Lit_Bleu";
    [Tooltip("Coché : le puzzle s'étend vers +X au lieu de -X.")]
    [SerializeField] private bool mirrorX;
    [Tooltip("Multiplie les distances (le recalage au sol les raccourcit un peu).")]
    [SerializeField] private float spacing = 1.2f;

#if UNITY_EDITOR
    private const string RootName = "Puzzle_Bleu";
    private Transform root;
    private Transform planet;
    private int groundMask;

    [ContextMenu("Construire le puzzle")]
    private void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning($"{name} : construction uniquement hors Play."); return; }

        foreach (GameObject p in new[] { emitterPrefab, relayPrefab, prismPrefab, mixerPrefab, receiverPrefab })
            if (p == null || p.GetComponent<Crystal>() == null)
            {
                Debug.LogWarning($"{name} : assigne les 5 prefabs de cristaux (avec Crystal à la racine).");
                return;
            }

        BeamManager manager = GetComponentInParent<BeamManager>();
        if (manager == null || manager.PlanetTransform == null)
        {
            Debug.LogWarning($"{name} : l'ancre doit être sous Crystal_Puzzle (BeamManager avec Planet).");
            return;
        }
        if (regionLight == null) regionLight = FindFirstObjectByType<RegionLight>();

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Construire la région Bleue");
        int group = Undo.GetCurrentGroup();

        Transform old = transform.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        GameObject rootGo = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(rootGo, "Puzzle root");
        rootGo.transform.SetParent(transform, false);
        root = rootGo.transform;
        planet = manager.PlanetTransform;
        groundMask = LayerMask.GetMask("Planete");

        // --- Cristaux ---
        Crystal emitterBlue  = MakeCrystal(emitterPrefab,  "Emitter_Blue",   6f,    -8f,  CrystalType.Emitter,  Colors.Blue,  Colors.None, 3, true,  true);
        Crystal degel1       = MakeCrystal(receiverPrefab, "Degel_1",        6f,     0f,  CrystalType.Receiver, Colors.None,  Colors.Blue, 0, false, false);
        Crystal relay        = MakeCrystal(relayPrefab,    "Relay_B",       -4f,    -8f,  CrystalType.Relay,    Colors.None,  Colors.None, 0, false, true);
        Crystal degel2       = MakeCrystal(receiverPrefab, "Degel_2",       -7.95f, -2f,  CrystalType.Receiver, Colors.None,  Colors.Blue, 0, false, false);
        Crystal prism        = MakeCrystal(prismPrefab,    "Prism_B",        0f,     0f,  CrystalType.Prism,    Colors.None,  Colors.None, 0, false, true);
        Crystal degel3       = MakeCrystal(receiverPrefab, "Degel_3",       -7.79f,  4.5f, CrystalType.Receiver, Colors.None,  Colors.Blue, 0, false, false);
        Crystal mixer        = MakeCrystal(mixerPrefab,    "Mixer_Cyan",   -14f,     0f,  CrystalType.Mixer,    Colors.None,  Colors.None, 4, true,  true);
        Crystal emitterGreen = MakeCrystal(emitterPrefab,  "Emitter_Green", -14f,  -12f,  CrystalType.Emitter,  Colors.Green, Colors.None, 0, false, true);
        Crystal receiverCyan = MakeCrystal(receiverPrefab, "Receiver_Cyan", -20f,    6f,  CrystalType.Receiver, Colors.None,  Colors.Cyan, 0, false, true);
        SetHint(mixer, Colors.Cyan);

        // --- Recalage au sol puis orientation résolue ---
        foreach (Crystal c in new[] { emitterBlue, degel1, relay, degel2, prism, degel3, mixer, emitterGreen, receiverCyan })
            c.AlignToPlanet(false);

        Aim(emitterBlue, relay);
        Aim(relay, prism);
        Aim(prism, mixer);
        Aim(mixer, receiverCyan);
        Aim(emitterGreen, mixer);

        // Les cristaux gelés ignorent Start Step : on mélange leur orientation directement
        Scramble(relay, 3);
        Scramble(prism, -3);

        // --- Glace, mur, rocher ---
        GameObject iceRelay = MakeIce(relay.transform);
        GameObject icePrism = MakeIce(prism.transform);
        GameObject iceGreen = MakeIce(emitterGreen.transform);
        GameObject wall = MakeIceWall(-14f, -9.5f, emitterGreen.transform);
        MakeRock("Rocher_1", 3f, -4f);

        // --- Dégel et allumage de la région ---
        Watch(degel1, new[] { relay }, new[] { iceRelay });
        Watch(degel2, new[] { prism }, new[] { icePrism });
        Watch(degel3, new Crystal[0], new[] { wall, iceGreen });
        ReceiverWatcher final = Watch(receiverCyan, new Crystal[0], new GameObject[0]);

        if (regionLight != null)
        {
            Undo.RecordObject(final, "Region light");
            UnityEventTools.AddStringPersistentListener(final.onLit, regionLight.LightRegion, regionProperty);
            EditorUtility.SetDirty(final);
        }
        else Debug.LogWarning($"{name} : aucun RegionLight trouvé, branche On Lit de Receiver_Cyan à la main.");

        Undo.CollapseUndoOperations(group);
        Debug.Log($"{name} : région Bleue construite (9 cristaux). Vérifie les raccourcis avec l'Orientation Finder.");
    }

    private Vector3 Local(float x, float z) => new Vector3((mirrorX ? -x : x) * spacing, 0f, z * spacing);

    private static SerializedProperty Prop(SerializedObject so, string field)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null) Debug.LogError($"Champ « {field} » introuvable sur {so.targetObject.name}.");
        return p;
    }

    private Crystal MakeCrystal(GameObject prefab, string objName, float x, float z, CrystalType type,
                                Colors emitted, Colors target, int startStep, bool rotatable, bool countsForVictory)
    {
        GameObject go = PrefabUtility.IsPartOfPrefabAsset(prefab)
            ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, root)
            : Instantiate(prefab, root);
        Undo.RegisterCreatedObjectUndo(go, objName);
        go.name = objName;
        go.SetActive(true);

        Undo.RecordObject(go.transform, "Place crystal");
        go.transform.localPosition = Local(x, z);
        go.transform.localRotation = Quaternion.identity;

        Crystal c = go.GetComponent<Crystal>();
        SerializedObject so = new SerializedObject(c);
        Prop(so, "crystalType").intValue = (int)type;
        Prop(so, "emittedColor").intValue = (int)emitted;
        Prop(so, "targetColor").intValue = (int)target;
        Prop(so, "rotatable").boolValue = rotatable;
        Prop(so, "countsForVictory").boolValue = countsForVictory;
        Prop(so, "startStep").intValue = startStep;
        Prop(so, "stepAngle").floatValue = 15f;
        Prop(so, "hintColor").intValue = (int)Colors.None;
        so.ApplyModifiedPropertiesWithoutUndo();
        return c;
    }

    private static void SetHint(Crystal c, Colors color)
    {
        SerializedObject so = new SerializedObject(c);
        Prop(so, "hintColor").intValue = (int)color;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private void Aim(Crystal source, Crystal target)
    {
        Vector3 up = source.transform.up;
        Vector3 dir = Vector3.ProjectOnPlane(target.transform.position - source.transform.position, up);
        if (dir.sqrMagnitude < 1e-4f) return;

        Quaternion rot = Quaternion.LookRotation(dir.normalized, up);
        if (source.crystalType == CrystalType.Prism)
        {
            // Sortie gauche vers la cible (droite en miroir) : l'autre sortie part vers le pôle
            float split = new SerializedObject(source).FindProperty("splitAngle").floatValue;
            rot = Quaternion.AngleAxis(mirrorX ? -split : split, up) * rot;
        }

        Undo.RecordObject(source.transform, "Aim");
        source.transform.rotation = rot;
    }

    private static void Scramble(Crystal c, int steps)
    {
        float step = new SerializedObject(c).FindProperty("stepAngle").floatValue;
        Undo.RecordObject(c.transform, "Scramble");
        c.transform.rotation = Quaternion.AngleAxis(steps * step, c.transform.up) * c.transform.rotation;
    }

    private GameObject MakeIce(Transform crystal)
    {
        Bounds b = new Bounds(crystal.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in crystal.GetComponentsInChildren<Renderer>())
        {
            if (r is LineRenderer || r is ParticleSystemRenderer) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        float size = any ? Mathf.Max(b.size.x, b.size.y, b.size.z) * 1.15f : 1.5f;

        GameObject ice = GameObject.CreatePrimitive(PrimitiveType.Cube);
        DestroyImmediate(ice.GetComponent<Collider>());   // ne bloque ni les rayons ni le joueur
        Undo.RegisterCreatedObjectUndo(ice, "Ice");
        ice.name = "Glace";
        ice.transform.SetParent(crystal, false);
        ice.transform.position = any ? b.center : crystal.position;
        ice.transform.localRotation = Quaternion.identity;
        Vector3 s = crystal.lossyScale;
        ice.transform.localScale = new Vector3(size / s.x, size / s.y, size / s.z);

        MeshRenderer mr = ice.GetComponent<MeshRenderer>();
        if (iceMaterial != null) mr.sharedMaterial = iceMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ice;
    }

    private GameObject PlaceObstacle(GameObject go, string objName, float x, float z)
    {
        Undo.RegisterCreatedObjectUndo(go, objName);
        go.name = objName;
        go.transform.SetParent(root, false);
        go.transform.localPosition = Local(x, z);
        go.transform.localRotation = Quaternion.identity;

        int layer = LayerMask.NameToLayer(obstacleLayer);
        if (layer >= 0) go.layer = layer;
        else Debug.LogWarning($"{name} : layer « {obstacleLayer} » introuvable.");

        if (!TerrainSnap.Snap(go.transform, planet, groundMask))
            Debug.LogWarning($"{objName} : sol introuvable sous l'obstacle.");
        return go;
    }

    private GameObject MakeIceWall(float x, float z, Transform facing)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);   // garde son collider : bloque le rayon
        wall.transform.localScale = new Vector3(3f, 4f, 0.6f);
        if (iceMaterial != null) wall.GetComponent<MeshRenderer>().sharedMaterial = iceMaterial;
        PlaceObstacle(wall, "Mur_Glace", x, z);

        Vector3 up = wall.transform.up;
        Vector3 dir = Vector3.ProjectOnPlane(facing.position - wall.transform.position, up);
        if (dir.sqrMagnitude > 1e-4f) wall.transform.rotation = Quaternion.LookRotation(dir.normalized, up);
        return wall;
    }

    private void MakeRock(string objName, float x, float z)
    {
        GameObject rock;
        if (rockPrefab != null)
        {
            rock = PrefabUtility.IsPartOfPrefabAsset(rockPrefab)
                ? (GameObject)PrefabUtility.InstantiatePrefab(rockPrefab)
                : Instantiate(rockPrefab);
        }
        else
        {
            rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.transform.localScale = new Vector3(2.2f, 2.4f, 2f);
            if (rockMaterial != null) rock.GetComponent<MeshRenderer>().sharedMaterial = rockMaterial;
        }

        PlaceObstacle(rock, objName, x, z);
        rock.transform.rotation = Quaternion.AngleAxis(Random.Range(0f, 360f), rock.transform.up) * rock.transform.rotation;
        rock.transform.position -= rock.transform.up * 0.3f;   // légèrement enfoncé
    }

    private static ReceiverWatcher Watch(Crystal receiver, Crystal[] unlock, GameObject[] hide)
    {
        ReceiverWatcher w = Undo.AddComponent<ReceiverWatcher>(receiver.gameObject);
        SerializedObject so = new SerializedObject(w);
        Prop(so, "receiver").objectReferenceValue = receiver;

        SerializedProperty u = Prop(so, "crystalsToUnlock");
        u.arraySize = unlock.Length;
        for (int i = 0; i < unlock.Length; i++) u.GetArrayElementAtIndex(i).objectReferenceValue = unlock[i];

        SerializedProperty h = Prop(so, "objectsToHide");
        h.arraySize = hide.Length;
        for (int i = 0; i < hide.Length; i++) h.GetArrayElementAtIndex(i).objectReferenceValue = hide[i];

        so.ApplyModifiedPropertiesWithoutUndo();
        return w;
    }
#endif
}
