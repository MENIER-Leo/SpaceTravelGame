using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Générateur de labyrinthe "intérieur de bateau", à usage unique, dans l'éditeur.
/// Clic droit sur le composant > "Générer" ou "Générer (nouvelle graine)".
/// - Grille calculée sur la surface du sol (axes du monde, sol non tourné).
/// - Le bateau est dans la longueur Z : poupe (entrée) en Z-, proue en Z+.
/// - Salle du canon au milieu, sur l'axe ; coursive centrale verrouillée de la proue jusqu'à elle.
///   Porte_Couloir au bout avant de la coursive, Porte_Canon devant la salle du canon.
/// - Portes du labyrinthe sur des passages incontournables, espacées ; chaque bouton est accessible avant sa porte.
/// </summary>
public class MazeGenerator : MonoBehaviour
{
    [System.Serializable]
    public struct Edge
    {
        public Vector2Int a, b;   // a = côté entrée, b = côté canon
        public Edge(Vector2Int a, Vector2Int b) { this.a = a; this.b = b; }
        public Vector2 Mid => (Vector2)(a + b) * 0.5f;
    }

    [Header("Sol")]
    [Tooltip("Objet sol (ou son parent). La grille est calculée sur sa surface. Vide = Width/Height et position du générateur.")]
    public Transform floor;

    [Header("Grille")]
    [Min(1f)] public float cellSize = 3f;
    [Min(7), Tooltip("Largeur du bateau (X). Utilisé seulement si aucun sol n'est renseigné.")] public int width = 9;
    [Min(10), Tooltip("Longueur du bateau (Z). Utilisé seulement si aucun sol n'est renseigné.")] public int height = 18;
    public int seed = 1;
    [Range(0f, 0.5f), Tooltip("Part des murs de couloir retirés pour créer des boucles (chemins alternatifs).")]
    public float loopChance = 0.1f;

    [Header("Forme du bateau (longueur = Z, proue vers Z+)")]
    [Min(0), Tooltip("Nombre de rangées sur lesquelles la proue s'affine.")]
    public int bowLength = 5;
    [Min(1), Tooltip("Largeur de la pointe de la proue, en cases.")]
    public int minBowWidth = 3;
    [Tooltip("Coupe les deux coins arrière (poupe arrondie).")]
    public bool roundStern = true;
    [Range(0f, 1f), Tooltip("Tendance des couloirs à continuer tout droit (longues coursives).")]
    public float straightness = 0.6f;
    [Range(0f, 1f), Tooltip("Préférence pour les couloirs dans la longueur du bateau.")]
    public float alongShipBias = 0.5f;

    [Header("Salles")]
    [Min(0)] public int roomCount = 4;
    [Min(2)] public int roomMinSize = 2;
    [Min(2)] public int roomMaxSize = 3;
    [Min(1), Tooltip("Nombre max de cases de la salle d'où le labyrinthe peut repartir (≈ nombre de sorties).")]
    public int roomExits = 2;
    [Range(0f, 1f), Tooltip("Part des salles collées à la coque (cabines).")]
    public float cabinRatio = 0.75f;

    [Header("Fin : coursive centrale + salle du canon")]
    [Min(2)] public int cannonRoomSize = 3;
    [Min(1), Tooltip("Largeur de la coursive centrale, en cases.")]
    public int corridorWidth = 1;
    [Range(0.2f, 0.7f), Tooltip("Position de la salle du canon le long du bateau (0 = poupe, 1 = proue).")]
    public float cannonPosition = 0.45f;
    [Min(1), Tooltip("Rangées de labyrinthe entre le bout de la coursive et la proue.")]
    public int bowGap = 2;

    [Header("Portes du labyrinthe")]
    [Min(0), Tooltip("Portes dans le labyrinthe, EN PLUS des 2 portes de la coursive.")]
    public int doorCount = 2;
    [Min(1), Tooltip("Écart minimum entre deux portes, en cases le long du chemin.")]
    public int minDoorSpacing = 6;
    [Min(0f), Tooltip("Distance minimum à vol d'oiseau entre deux portes, en cases.")]
    public float minDoorDistance = 4f;

    [Header("Boutons")]
    [Min(0), Tooltip("Distance minimum entre l'entrée et un bouton, en cases (en marchant).")]
    public int minButtonDistanceFromStart = 8;
    [Min(0), Tooltip("Écart minimum entre deux boutons, en cases.")]
    public int minButtonSeparation = 5;

    [Header("Objets provisoires (facultatif)")]
    public GameObject doorPrefab;
    [Tooltip("ATTENTION : tous ses enfants sont supprimés à chaque génération.")]
    public Transform doorsParent;
    public GameObject buttonPrefab;
    [Tooltip("ATTENTION : tous ses enfants sont supprimés à chaque génération.")]
    public Transform buttonsParent;

    [Header("Murs")]
    [Tooltip("Prefab Mur (un cube) depuis la fenêtre Project.")]
    public GameObject wallPrefab;
    [Tooltip("ATTENTION : tous ses enfants sont supprimés à chaque génération.")]
    public Transform wallsParent;
    public float wallHeight = 3f;
    public float wallThickness = 0.5f;

    [Header("Aide visuelle (Scene)")]
    public bool showRooms = true;
    public bool showMainPath = true;
    public bool showDeadEnds = true;
    public bool showDoorSpots = true;

    // ---- Résultat conservé pour les gizmos
    [SerializeField, HideInInspector] private Vector3 gridOrigin;
    [SerializeField, HideInInspector] private float gridCell = 3f;
    [SerializeField, HideInInspector] private int gridW, gridH;
    [SerializeField, HideInInspector] private Vector2Int start;
    [SerializeField, HideInInspector] private RectInt cannonRoom, corridor;
    [SerializeField, HideInInspector] private List<RectInt> rooms = new List<RectInt>();
    [SerializeField, HideInInspector] private List<Vector2Int> mainPath = new List<Vector2Int>();
    [SerializeField, HideInInspector] private List<Vector2Int> deadEnds = new List<Vector2Int>();
    [SerializeField, HideInInspector] private List<Edge> doorSpots = new List<Edge>();   // passages incontournables
    [SerializeField, HideInInspector] private List<Edge> doors = new List<Edge>();       // labyrinthe, puis Couloir, puis Canon
    [SerializeField, HideInInspector] private List<Vector2Int> buttons = new List<Vector2Int>();
    [SerializeField, HideInInspector] private int finalDoorIndex;

    // ---- Données de travail
    private bool[,] hWalls;    // [x, y] : mur le long de X sur la ligne z = y (0..H)
    private bool[,] vWalls;    // [x, y] : mur le long de Z sur la ligne x = x (0..W)
    private int[,] roomId;     // -1 = pas une salle du labyrinthe
    private bool[,] reserved;  // coursive + salle du canon (hors labyrinthe)
    private bool[,] outside;   // hors de la coque
    private Vector2Int[,] dirIn;
    private Edge doorAEdge, doorBEdge;
    private System.Random rng;

    private static readonly Vector2Int[] Dirs =
        { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    // ================================================================ Cadre de la grille

    private bool ComputeGridFrame()
    {
        gridCell = cellSize;
        if (floor == null)
        {
            gridW = width; gridH = height;
            gridOrigin = transform.position;
            return true;
        }

        Bounds b = GetBounds(floor);
        if (b.size.x < cellSize || b.size.z < cellSize)
        {
            Debug.LogError("MazeGenerator : sol trop petit ou sans Renderer/Collider.");
            return false;
        }

        gridW = Mathf.FloorToInt(b.size.x / cellSize);
        gridH = Mathf.FloorToInt(b.size.z / cellSize);
        float mx = (b.size.x - gridW * cellSize) * 0.5f;
        float mz = (b.size.z - gridH * cellSize) * 0.5f;
        gridOrigin = new Vector3(b.min.x + mx, b.max.y, b.min.z + mz);
        return true;
    }

    private static Bounds GetBounds(Transform t)
    {
        Renderer[] rends = t.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }
        Collider[] cols = t.GetComponentsInChildren<Collider>();
        if (cols.Length > 0)
        {
            Bounds b = cols[0].bounds;
            for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
            return b;
        }
        return new Bounds(t.position, Vector3.zero);
    }

    // Salle du canon au milieu, coursive centrale vers la proue, coque, entrée à la poupe
    private bool SetupLayout()
    {
        if (gridW < 7 || gridH < 10)
        {
            Debug.LogError($"MazeGenerator : grille de {gridW}x{gridH} cases. Il faut au moins 7 de large (X) et 10 de long (Z).");
            return false;
        }

        int cs = Mathf.Clamp(cannonRoomSize, 2, gridW - 4);
        int cw = Mathf.Clamp(corridorWidth, 1, cs);
        int gap = Mathf.Max(1, bowGap);
        int maxY = gridH - gap - cs - 2;
        if (maxY < 2)
        {
            Debug.LogError("MazeGenerator : bateau trop court pour la salle du canon et la coursive. Allonge le sol en Z.");
            return false;
        }
        int yC0 = Mathf.Clamp(Mathf.RoundToInt(cannonPosition * gridH - cs * 0.5f), 2, maxY);

        cannonRoom = new RectInt((gridW - cs) / 2, yC0, cs, cs);
        int cx0 = (gridW - cw) / 2;
        int cTop = gridH - gap;   // première rangée de proue (exclue de la coursive)
        corridor = new RectInt(cx0, yC0 + cs, cw, cTop - (yC0 + cs));

        int xm = Mathf.Clamp(cx0 + cw / 2, cannonRoom.xMin, cannonRoom.xMax - 1);
        doorAEdge = new Edge(new Vector2Int(xm, cTop), new Vector2Int(xm, cTop - 1));
        doorBEdge = new Edge(new Vector2Int(xm, corridor.yMin), new Vector2Int(xm, corridor.yMin - 1));
        start = new Vector2Int(gridW / 2, 0);

        ComputeHull(cw);

        bool ok = InHull(start.x, start.y) && InHull(doorAEdge.a.x, doorAEdge.a.y)
                  && InHull(cx0 - 1, cTop - 1) && InHull(cx0 + cw, cTop - 1);
        foreach (Vector2Int p in cannonRoom.allPositionsWithin) ok &= InHull(p.x, p.y);
        foreach (Vector2Int p in corridor.allPositionsWithin) ok &= InHull(p.x, p.y);
        if (!ok)
        {
            Debug.LogError("MazeGenerator : la proue est trop fine pour la coursive. Baisse Bow Length, ou monte Min Bow Width / Bow Gap.");
            return false;
        }
        return true;
    }

    // Proue affinée (profil arrondi) + coins de poupe coupés
    private void ComputeHull(int cw)
    {
        outside = new bool[gridW, gridH];
        int bl = Mathf.Clamp(bowLength, 0, gridH - 1);
        int maxCut = Mathf.Max(0, (gridW - Mathf.Max(minBowWidth, cw)) / 2);

        for (int y = 0; y < gridH; y++)
        {
            int cut = 0;
            if (bl > 0 && y >= gridH - bl)
            {
                float t = (y - (gridH - bl) + 1) / (float)bl;
                cut = Mathf.RoundToInt(maxCut * Mathf.Pow(t, 1.5f));
            }
            if (roundStern && y == 0) cut = Mathf.Max(cut, 1);

            for (int x = 0; x < cut; x++)
            {
                outside[x, y] = true;
                outside[gridW - 1 - x, y] = true;
            }
        }
    }

    // ================================================================ Génération logique

    private void BuildGrid()
    {
        rng = new System.Random(seed);
        hWalls = new bool[gridW, gridH + 1];
        vWalls = new bool[gridW + 1, gridH];
        roomId = new int[gridW, gridH];
        reserved = new bool[gridW, gridH];
        for (int x = 0; x < gridW; x++) for (int y = 0; y <= gridH; y++) hWalls[x, y] = true;
        for (int x = 0; x <= gridW; x++) for (int y = 0; y < gridH; y++) vWalls[x, y] = true;
        for (int x = 0; x < gridW; x++) for (int y = 0; y < gridH; y++) roomId[x, y] = -1;
        foreach (Vector2Int p in cannonRoom.allPositionsWithin) reserved[p.x, p.y] = true;
        foreach (Vector2Int p in corridor.allPositionsWithin) reserved[p.x, p.y] = true;

        PlaceRooms();
        foreach (RectInt r in rooms) OpenInterior(r);
        OpenInterior(cannonRoom);
        OpenInterior(corridor);

        Carve();
        AddLoops();
        SetWall(doorAEdge.a, doorAEdge.b, false);
        SetWall(doorBEdge.a, doorBEdge.b, false);

        ComputeMainPath();
        ComputeDeadEnds();
        ChooseDoors();
        ChooseButtons();
    }

    private void PlaceRooms()
    {
        rooms.Clear();
        int minS = roomMinSize;
        int maxS = Mathf.Max(roomMinSize, roomMaxSize);
        int attempts = 0;

        while (rooms.Count < roomCount && attempts++ < 1000)
        {
            int rw = rng.Next(minS, maxS + 1);
            int rh = rng.Next(minS, maxS + 1);
            if (rw > gridW - 2 || rh > gridH - 2) continue;

            int y0 = rng.Next(0, gridH - rh + 1);
            int x0;
            if (rng.NextDouble() < cabinRatio)
            {
                // Cabine : collée à la coque, côté bâbord ou tribord
                int left = 0, right = gridW;
                for (int y = y0; y < y0 + rh; y++)
                {
                    int first = 0;
                    while (first < gridW && outside[first, y]) first++;
                    int lastX = gridW - 1;
                    while (lastX >= 0 && outside[lastX, y]) lastX--;
                    left = Mathf.Max(left, first);
                    right = Mathf.Min(right, lastX + 1);
                }
                x0 = rng.Next(2) == 0 ? left : right - rw;
            }
            else x0 = rng.Next(0, gridW - rw + 1);

            var rect = new RectInt(x0, y0, rw, rh);
            if (!RectFree(rect) || rect.Contains(start)) continue;

            var margin = new RectInt(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2);
            if (margin.Overlaps(cannonRoom) || margin.Overlaps(corridor)) continue;

            bool overlaps = false;
            foreach (RectInt r in rooms) if (margin.Overlaps(r)) { overlaps = true; break; }
            if (overlaps) continue;

            foreach (Vector2Int p in rect.allPositionsWithin) roomId[p.x, p.y] = rooms.Count;
            rooms.Add(rect);
        }

        if (rooms.Count < roomCount)
            Debug.LogWarning($"MazeGenerator : seulement {rooms.Count} salle(s) placée(s) sur {roomCount} (manque de place).");
    }

    private bool RectFree(RectInt r)
    {
        foreach (Vector2Int p in r.allPositionsWithin)
            if (!InHull(p.x, p.y) || reserved[p.x, p.y]) return false;
        return true;
    }

    private void OpenInterior(RectInt r)
    {
        for (int x = r.xMin; x < r.xMax; x++)
            for (int y = r.yMin; y < r.yMax; y++)
            {
                if (x + 1 < r.xMax) vWalls[x + 1, y] = false;
                if (y + 1 < r.yMax) hWalls[x, y + 1] = false;
            }
    }

    // Recursive backtracker pondéré : préfère continuer tout droit et dans la longueur du bateau
    private void Carve()
    {
        var visited = new bool[gridW, gridH];
        dirIn = new Vector2Int[gridW, gridH];
        for (int x = 0; x < gridW; x++)
            for (int y = 0; y < gridH; y++)
                visited[x, y] = reserved[x, y] || outside[x, y];

        var stack = new Stack<Vector2Int>();
        var options = new List<Vector2Int>(4);
        var weights = new List<float>(4);
        Visit(start, visited, stack);

        do
        {
            while (stack.Count > 0)
            {
                Vector2Int c = stack.Peek();
                options.Clear();
                weights.Clear();
                float total = 0f;

                foreach (Vector2Int d in Dirs)
                {
                    Vector2Int n = c + d;
                    if (!Inside(n) || visited[n.x, n.y]) continue;
                    float w = 1f
                            + (d.x == 0 ? alongShipBias * 2f : 0f)
                            + (d == dirIn[c.x, c.y] ? straightness * 4f : 0f);
                    options.Add(d);
                    weights.Add(w);
                    total += w;
                }
                if (options.Count == 0) { stack.Pop(); continue; }

                double r = rng.NextDouble() * total;
                int pick = 0;
                for (; pick < options.Count - 1; pick++)
                {
                    r -= weights[pick];
                    if (r <= 0) break;
                }

                Vector2Int dir = options[pick];
                Vector2Int next = c + dir;
                SetWall(c, next, false);
                dirIn[next.x, next.y] = dir;
                Visit(next, visited, stack);
            }
        }
        while (ConnectOrphan(visited, stack));
    }

    private void Visit(Vector2Int c, bool[,] visited, Stack<Vector2Int> stack)
    {
        int r = roomId[c.x, c.y];
        if (r < 0) { visited[c.x, c.y] = true; stack.Push(c); return; }

        var border = new List<Vector2Int>();
        foreach (Vector2Int p in rooms[r].allPositionsWithin)
        {
            visited[p.x, p.y] = true;
            foreach (Vector2Int d in Dirs)
            {
                Vector2Int n = p + d;
                if (Inside(n) && roomId[n.x, n.y] != r) { border.Add(p); break; }
            }
        }

        Shuffle(border);
        for (int i = 0; i < border.Count && i < roomExits; i++) stack.Push(border[i]);
    }

    // Relie une case jamais atteinte (sécurité), jamais à la zone réservée ni hors coque
    private bool ConnectOrphan(bool[,] visited, Stack<Vector2Int> stack)
    {
        for (int x = 0; x < gridW; x++)
            for (int y = 0; y < gridH; y++)
            {
                if (visited[x, y]) continue;
                var c = new Vector2Int(x, y);
                foreach (Vector2Int d in Dirs)
                {
                    Vector2Int n = c + d;
                    if (!Inside(n) || !visited[n.x, n.y] || reserved[n.x, n.y] || outside[n.x, n.y]) continue;
                    SetWall(c, n, false);
                    Visit(c, visited, stack);
                    return true;
                }
            }
        return false;
    }

    // Boucles uniquement entre deux cases de couloir du labyrinthe
    private void AddLoops()
    {
        for (int x = 0; x < gridW; x++)
            for (int y = 1; y < gridH; y++)
                if (hWalls[x, y] && IsPlain(x, y - 1) && IsPlain(x, y) && rng.NextDouble() < loopChance)
                    hWalls[x, y] = false;

        for (int x = 1; x < gridW; x++)
            for (int y = 0; y < gridH; y++)
                if (vWalls[x, y] && IsPlain(x - 1, y) && IsPlain(x, y) && rng.NextDouble() < loopChance)
                    vWalls[x, y] = false;
    }

    private bool IsPlain(int x, int y) => roomId[x, y] < 0 && !reserved[x, y] && !outside[x, y];

    private Vector2Int CannonCell() =>
        new Vector2Int(cannonRoom.x + cannonRoom.width / 2, cannonRoom.y + cannonRoom.height / 2);

    private void ComputeMainPath()
    {
        mainPath.Clear();
        Vector2Int goal = CannonCell();
        var prev = new Dictionary<Vector2Int, Vector2Int> { [start] = start };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            if (c == goal) break;
            foreach (Vector2Int d in Dirs)
            {
                Vector2Int n = c + d;
                if (!Inside(n) || HasWall(c, d) || prev.ContainsKey(n)) continue;
                prev[n] = c;
                queue.Enqueue(n);
            }
        }

        if (!prev.ContainsKey(goal)) return;
        for (Vector2Int c = goal; c != start; c = prev[c]) mainPath.Add(c);
        mainPath.Add(start);
        mainPath.Reverse();
    }

    private void ComputeDeadEnds()
    {
        deadEnds.Clear();
        for (int x = 0; x < gridW; x++)
            for (int y = 0; y < gridH; y++)
            {
                var c = new Vector2Int(x, y);
                if (c == start || !IsPlain(x, y)) continue;
                int walls = 0;
                foreach (Vector2Int d in Dirs) if (HasWall(c, d)) walls++;
                if (walls == 3) deadEnds.Add(c);
            }
    }

    // Portes du labyrinthe : passages incontournables, réparties et espacées ; puis les 2 portes de la coursive
    private void ChooseDoors()
    {
        doorSpots.Clear();
        doors.Clear();
        finalDoorIndex = 0;

        Vector2Int exit = doorAEdge.a;
        int mazeL = mainPath.IndexOf(exit);   // nombre de pas entre l'entrée et la porte de la coursive
        if (mazeL < 0)
        {
            Debug.LogError("MazeGenerator : chemin principal introuvable.");
            return;
        }

        var cand = new List<int>();
        var candRoom = new List<bool>();
        for (int i = 0; i < mazeL; i++)
        {
            Vector2Int a = mainPath[i], b = mainPath[i + 1];
            int ra = roomId[a.x, a.y], rb = roomId[b.x, b.y];
            if (ra >= 0 && ra == rb) continue;   // intérieur d'une salle

            SetWall(a, b, true);
            bool unavoidable = !Bfs(start).ContainsKey(exit);
            SetWall(a, b, false);
            if (!unavoidable) continue;

            doorSpots.Add(new Edge(a, b));
            cand.Add(i);
            candRoom.Add(ra != rb);   // entrée de salle : emplacement préféré
        }

        var fixedDoors = new List<Edge> { doorAEdge, doorBEdge };
        var chosen = new List<Edge>();
        float roomBonus = minDoorSpacing * 0.5f;
        int last = -1;

        for (int k = 0; k < doorCount; k++)
        {
            float target = (k + 1) * mazeL / (float)(doorCount + 1);
            int best = -1;
            float bestScore = float.MaxValue;

            for (int j = 0; j < cand.Count; j++)
            {
                int i = cand[j];
                if (i <= last) continue;
                if (i < minDoorSpacing / 2 || mazeL - i < minDoorSpacing) continue;   // ni collée à l'entrée, ni à la coursive
                if (last >= 0 && i - last < minDoorSpacing) continue;                // écart le long du chemin
                if (TooClose(doorSpots[j], chosen) || TooClose(doorSpots[j], fixedDoors)) continue;   // écart à vol d'oiseau

                float s = Mathf.Abs(i - target) - (candRoom[j] ? roomBonus : 0f);
                if (s < bestScore) { bestScore = s; best = j; }
            }

            if (best < 0) break;
            chosen.Add(doorSpots[best]);
            last = cand[best];
        }

        doors.AddRange(chosen);
        finalDoorIndex = doors.Count;
        doors.Add(doorAEdge);
        doors.Add(doorBEdge);

        if (chosen.Count < doorCount)
            Debug.LogWarning($"MazeGenerator : seulement {chosen.Count} porte(s) dans le labyrinthe sur {doorCount}. " +
                             "Baisse Min Door Spacing / Min Door Distance ou Loop Chance, ou change de graine.");
    }

    private bool TooClose(Edge e, List<Edge> others)
    {
        foreach (Edge o in others)
            if (Vector2.Distance(e.Mid, o.Mid) < minDoorDistance) return true;
        return false;
    }

    // Bouton k : dans la zone que la porte précédente vient d'ouvrir, avec un détour maximal
    // (loin de l'entrée de cette zone ET de la porte k). Les 2 boutons de la coursive sont
    // dans la zone ouverte par la dernière porte du labyrinthe, éloignés l'un de l'autre.
    private void ChooseButtons()
    {
        buttons.Clear();
        var pathSet = new HashSet<Vector2Int>(mainPath);
        var deadSet = new HashSet<Vector2Int>(deadEnds);
        Dictionary<Vector2Int, int> fromStart = Bfs(start);   // toutes portes ouvertes

        for (int k = 0; k < doors.Count; k++)
        {
            int layer = Mathf.Min(k, finalDoorIndex);              // les 2 portes finales partagent la même zone
            Vector2Int entry = layer > 0 ? doors[layer - 1].b : start;
            Vector2Int target = doors[layer].a;

            SetDoors(layer, true);
            Dictionary<Vector2Int, int> zone = Bfs(start);
            Dictionary<Vector2Int, int> fromEntry = Bfs(entry);
            Dictionary<Vector2Int, int> fromDoor = Bfs(target);
            SetDoors(layer, false);

            var before = new HashSet<Vector2Int>();               // déjà accessible avant la porte précédente
            if (layer > 0)
            {
                SetDoors(layer - 1, true);
                before = new HashSet<Vector2Int>(Bfs(start).Keys);
                SetDoors(layer - 1, false);
            }

            Vector2Int best = target;
            float bestScore = float.MinValue;

            // Passe 0 : zone nouvelle + contraintes ; passe 1 : zone nouvelle ; passe 2 : toute la zone
            for (int pass = 0; pass < 3 && bestScore == float.MinValue; pass++)
                foreach (Vector2Int c in zone.Keys)
                {
                    if (c == start || buttons.Contains(c)) continue;
                    if (pass < 2 && before.Contains(c)) continue;
                    if (!fromEntry.TryGetValue(c, out int de) || !fromDoor.TryGetValue(c, out int dd)) continue;

                    int gap = MinButtonGap(c);
                    if (pass == 0)
                    {
                        if (!fromStart.TryGetValue(c, out int ds) || ds < minButtonDistanceFromStart) continue;
                        if (gap < minButtonSeparation) continue;
                    }

                    float s = de + dd
                            + (deadSet.Contains(c) ? 3f : 0f)
                            + (roomId[c.x, c.y] >= 0 ? 3f : 0f)
                            - (pathSet.Contains(c) ? 4f : 0f)
                            + Mathf.Min(gap, 10) * 2f;
                    if (s > bestScore) { bestScore = s; best = c; }
                }

            buttons.Add(best);
        }
    }

    private int MinButtonGap(Vector2Int c)
    {
        int min = int.MaxValue;
        foreach (Vector2Int b in buttons)
            min = Mathf.Min(min, Mathf.Abs(c.x - b.x) + Mathf.Abs(c.y - b.y));
        return min;
    }

    private string DoorLabel(int k) =>
        k < finalDoorIndex ? (k + 1).ToString() : (k == finalDoorIndex ? "Couloir" : "Canon");

    // ================================================================ Outils grille

    private bool Inside(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < gridW && c.y < gridH;

    private bool InHull(int x, int y) => x >= 0 && y >= 0 && x < gridW && y < gridH && !outside[x, y];

    // Un mur n'est construit que s'il touche au moins une case dans la coque
    private bool HWallVisible(int x, int y) => hWalls[x, y] && (InHull(x, y - 1) || InHull(x, y));
    private bool VWallVisible(int x, int y) => vWalls[x, y] && (InHull(x - 1, y) || InHull(x, y));

    private void SetWall(Vector2Int a, Vector2Int b, bool value)
    {
        if (b.x == a.x + 1) vWalls[a.x + 1, a.y] = value;
        else if (b.x == a.x - 1) vWalls[a.x, a.y] = value;
        else if (b.y == a.y + 1) hWalls[a.x, a.y + 1] = value;
        else if (b.y == a.y - 1) hWalls[a.x, a.y] = value;
    }

    private bool HasWall(Vector2Int c, Vector2Int d)
    {
        if (d == Vector2Int.right) return vWalls[c.x + 1, c.y];
        if (d == Vector2Int.left) return vWalls[c.x, c.y];
        if (d == Vector2Int.up) return hWalls[c.x, c.y + 1];
        return hWalls[c.x, c.y];
    }

    private void SetDoors(int fromIndex, bool closed)
    {
        for (int j = fromIndex; j < doors.Count; j++) SetWall(doors[j].a, doors[j].b, closed);
    }

    private Dictionary<Vector2Int, int> Bfs(Vector2Int from)
    {
        var dist = new Dictionary<Vector2Int, int> { [from] = 0 };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            foreach (Vector2Int d in Dirs)
            {
                Vector2Int n = c + d;
                if (!Inside(n) || HasWall(c, d) || dist.ContainsKey(n)) continue;
                dist[n] = dist[c] + 1;
                queue.Enqueue(n);
            }
        }
        return dist;
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private Vector3 CellPos(Vector2Int c, float y) =>
        gridOrigin + new Vector3((c.x + 0.5f) * gridCell, y - gridOrigin.y, (c.y + 0.5f) * gridCell);

    private Vector3 EdgePos(Edge e, float y) => (CellPos(e.a, y) + CellPos(e.b, y)) * 0.5f;

    // ================================================================ Construction (éditeur)

#if UNITY_EDITOR
    [ContextMenu("Générer")]
    private void Generate()
    {
        if (wallPrefab == null || wallsParent == null)
        {
            Debug.LogError("MazeGenerator : renseigne Wall Prefab et Walls Parent.");
            return;
        }
        foreach (Transform p in new[] { wallsParent, doorsParent, buttonsParent })
        {
            if (p == null) continue;
            if (transform.IsChildOf(p) || (floor != null && floor.IsChildOf(p)))
            {
                Debug.LogError($"MazeGenerator : le générateur et le sol ne doivent pas être sous {p.name} (ils seraient supprimés).");
                return;
            }
        }

        Undo.RecordObject(this, "Générer le labyrinthe");
        if (!ComputeGridFrame() || !SetupLayout()) return;
        BuildGrid();

        ClearChildren(wallsParent);
        ClearChildren(doorsParent);
        ClearChildren(buttonsParent);

        BuildWalls();

        if (doorPrefab != null && doorsParent != null)
            for (int k = 0; k < doors.Count; k++)
            {
                float yaw = doors[k].a.x != doors[k].b.x ? 90f : 0f;
                SpawnOnFloor(doorPrefab, doorsParent, $"Porte_{DoorLabel(k)}", EdgePos(doors[k], gridOrigin.y), yaw);
            }

        if (buttonPrefab != null && buttonsParent != null)
            for (int k = 0; k < buttons.Count; k++)
                SpawnOnFloor(buttonPrefab, buttonsParent, $"Bouton_{DoorLabel(k)}", CellPos(buttons[k], gridOrigin.y), 0f);

        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"MazeGenerator : bateau de {gridW}x{gridH} cases (graine {seed}), {rooms.Count} salle(s), " +
                  $"coursive de {corridor.height} cases, {finalDoorIndex} porte(s) dans le labyrinthe + 2 portes de coursive. " +
                  $"Spawn conseillé : {CellPos(start, gridOrigin.y + 1f)}");
    }

    [ContextMenu("Générer (nouvelle graine)")]
    private void GenerateNewSeed()
    {
        Undo.RecordObject(this, "Nouvelle graine");
        seed = Random.Range(1, 100000);
        Generate();
    }

    private void BuildWalls()
    {
        float cs = gridCell, t = wallThickness, len = cs - t;

        for (int x = 0; x < gridW; x++)
            for (int y = 0; y <= gridH; y++)
                if (HWallVisible(x, y))
                    SpawnWall($"Mur_H_{x}_{y}", new Vector3((x + 0.5f) * cs, 0f, y * cs), 0f, new Vector3(len, wallHeight, t));

        for (int x = 0; x <= gridW; x++)
            for (int y = 0; y < gridH; y++)
                if (VWallVisible(x, y))
                    SpawnWall($"Mur_V_{x}_{y}", new Vector3(x * cs, 0f, (y + 0.5f) * cs), 90f, new Vector3(len, wallHeight, t));

        // Piliers aux angles touchés par au moins un mur construit
        for (int px = 0; px <= gridW; px++)
            for (int pz = 0; pz <= gridH; pz++)
            {
                bool used =
                    (px > 0 && HWallVisible(px - 1, pz)) ||
                    (px < gridW && HWallVisible(px, pz)) ||
                    (pz > 0 && VWallVisible(px, pz - 1)) ||
                    (pz < gridH && VWallVisible(px, pz));
                if (used)
                    SpawnWall($"Pilier_{px}_{pz}", new Vector3(px * cs, 0f, pz * cs), 0f, new Vector3(t, wallHeight, t));
            }
    }

    private GameObject Instantiate(GameObject prefab, Transform parent, string objName)
    {
        GameObject go = PrefabUtility.IsPartOfPrefabAsset(prefab)
            ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent)
            : Object.Instantiate(prefab, parent);
        go.name = objName;
        Undo.RegisterCreatedObjectUndo(go, "Générer le labyrinthe");
        return go;
    }

    private void SpawnWall(string objName, Vector3 localPos, float yaw, Vector3 scale)
    {
        GameObject go = Instantiate(wallPrefab, wallsParent, objName);
        go.transform.SetPositionAndRotation(
            gridOrigin + localPos + Vector3.up * (wallHeight * 0.5f),
            Quaternion.Euler(0f, yaw, 0f));
        go.transform.localScale = scale;
    }

    // Garde l'échelle du prefab et pose le bas de l'objet sur le sol
    private void SpawnOnFloor(GameObject prefab, Transform parent, string objName, Vector3 pos, float yaw)
    {
        GameObject go = Instantiate(prefab, parent, objName);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        Bounds b = GetBounds(go.transform);
        if (b.size != Vector3.zero) go.transform.position += Vector3.up * (pos.y - b.min.y);
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
    }
#endif

    // ================================================================ Gizmos

    private void DrawRect(RectInt r, float y)
    {
        Vector3 center = gridOrigin + new Vector3((r.x + r.width * 0.5f) * gridCell, y - gridOrigin.y, (r.y + r.height * 0.5f) * gridCell);
        Gizmos.DrawWireCube(center, new Vector3(r.width * gridCell, 0.1f, r.height * gridCell));
    }

    private void OnDrawGizmos()
    {
        if (gridW == 0) return;
        float y = gridOrigin.y + 0.2f;

        if (showRooms)
        {
            Gizmos.color = Color.cyan;
            foreach (RectInt r in rooms) DrawRect(r, y);
            Gizmos.color = Color.blue;
            DrawRect(corridor, y);
            Gizmos.color = Color.red;
            DrawRect(cannonRoom, y);
        }

        if (showMainPath && mainPath.Count > 1)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < mainPath.Count - 1; i++)
                Gizmos.DrawLine(CellPos(mainPath[i], y), CellPos(mainPath[i + 1], y));
            Gizmos.DrawSphere(CellPos(mainPath[0], y), gridCell * 0.2f);
        }

        if (showDeadEnds)
        {
            Gizmos.color = Color.yellow;
            foreach (Vector2Int c in deadEnds) Gizmos.DrawSphere(CellPos(c, y), gridCell * 0.1f);
        }

        if (showDoorSpots)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f);   // orange : emplacements valides
            foreach (Edge e in doorSpots) Gizmos.DrawCube(EdgePos(e, y), Vector3.one * gridCell * 0.15f);

            for (int k = 0; k < doors.Count; k++)
            {
                Vector3 dp = EdgePos(doors[k], y);
                Gizmos.color = Color.red;
                Gizmos.DrawCube(dp, Vector3.one * gridCell * 0.3f);

                if (k < buttons.Count)
                {
                    Vector3 bp = CellPos(buttons[k], y);
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(bp, gridCell * 0.25f);
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawLine(bp, dp);
#if UNITY_EDITOR
                    Handles.Label(bp + Vector3.up, $"Bouton {DoorLabel(k)}");
#endif
                }
#if UNITY_EDITOR
                Handles.Label(dp + Vector3.up, $"Porte {DoorLabel(k)}");
#endif
            }
        }
    }
}