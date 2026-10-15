using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class SchoolDressing
{
    static System.Random rng;
    static int count;
    static Dictionary<string, Material> cache = new Dictionary<string, Material>();

    static float R() { return (float)rng.NextDouble(); }
    static float Rr(float a, float b) { return a + (b - a) * R(); }

    // ------------------------------------------------------------------ materials
    static Material Mk(string name, Color c, float smooth, float metal)
    {
        string path = "Assets/Materials/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = name;
            AssetDatabase.CreateAsset(m, path);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            EditorUtility.SetDirty(m);
        }
        cache[name] = m;
        return m;
    }
    static Material Mt(string n)
    {
        Material m;
        if (cache.TryGetValue(n, out m) && m != null) return m;
        m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/" + n + ".mat");
        cache[n] = m;
        return m;
    }
    static void Init(int seed)
    {
        rng = new System.Random(seed);
        count = 0;
        Mk("Dress_Paper", new Color(0.55f, 0.53f, 0.46f), 0.1f, 0f);
        Mk("Dress_Cork", new Color(0.36f, 0.25f, 0.15f), 0.05f, 0f);
        Mk("Dress_Clock", new Color(0.62f, 0.62f, 0.58f), 0.3f, 0f);
        Mk("Dress_Dark", new Color(0.04f, 0.04f, 0.04f), 0.2f, 0f);
        Mk("Dress_Red", new Color(0.45f, 0.04f, 0.03f), 0.4f, 0.2f);
        Mk("Dress_White", new Color(0.62f, 0.62f, 0.6f), 0.45f, 0f);
        Mk("Dress_Steel", new Color(0.35f, 0.36f, 0.37f), 0.55f, 0.7f);
        Mk("Dress_Pipe", new Color(0.22f, 0.24f, 0.25f), 0.35f, 0.6f);
        Mk("Dress_Bin", new Color(0.12f, 0.16f, 0.14f), 0.3f, 0.3f);
        Mk("Dress_Bench", new Color(0.24f, 0.15f, 0.08f), 0.2f, 0f);
        Mk("Dress_ExitSign", new Color(0.05f, 0.4f, 0.1f), 0.3f, 0f);
        Material ex = Mt("Dress_ExitSign");
        ex.EnableKeyword("_EMISSION");
        ex.SetColor("_EmissionColor", new Color(0.0f, 0.9f, 0.25f));
        ex.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        Color[] bookCols = {
            new Color(0.35f,0.08f,0.07f), new Color(0.08f,0.18f,0.30f), new Color(0.10f,0.26f,0.12f), new Color(0.38f,0.30f,0.10f),
            new Color(0.25f,0.12f,0.28f), new Color(0.30f,0.30f,0.28f), new Color(0.40f,0.18f,0.08f), new Color(0.07f,0.07f,0.09f) };
        for (int i = 0; i < bookCols.Length; i++) Mk("Dress_Book" + i, bookCols[i], 0.15f, 0f);
        Color[] posterCols = {
            new Color(0.40f,0.12f,0.10f), new Color(0.12f,0.22f,0.38f), new Color(0.18f,0.34f,0.18f), new Color(0.42f,0.36f,0.14f), new Color(0.30f,0.30f,0.30f) };
        for (int i = 0; i < posterCols.Length; i++) Mk("Dress_Poster" + i, posterCols[i], 0.1f, 0f);
    }

    // ------------------------------------------------------------------ primitives
    static GameObject Box(Transform p, string name, Vector3 c, Vector3 s, Quaternion rot, Material m, bool col)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(p, false);
        g.transform.position = c;
        g.transform.rotation = rot;
        g.transform.localScale = s;
        g.GetComponent<Renderer>().sharedMaterial = m;
        if (!col) Object.DestroyImmediate(g.GetComponent<Collider>());
        count++;
        return g;
    }
    static GameObject Cyl(Transform p, string name, Vector3 c, float diam, float height, Quaternion rot, Material m, bool col)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = name;
        g.transform.SetParent(p, false);
        g.transform.position = c;
        g.transform.rotation = rot;
        g.transform.localScale = new Vector3(diam, height * 0.5f, diam);
        g.GetComponent<Renderer>().sharedMaterial = m;
        if (!col) Object.DestroyImmediate(g.GetComponent<Collider>());
        count++;
        return g;
    }
    static bool Free(Vector3 c, Vector3 half, Quaternion rot)
    {
        return !Physics.CheckBox(c, half, rot, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }
    static Transform Group(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) Object.DestroyImmediate(t.gameObject);
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        return g.transform;
    }
    static Transform Root()
    {
        GameObject r = GameObject.Find("SchoolDetails");
        if (r == null) r = new GameObject("SchoolDetails");
        return r.transform;
    }
    static void Label(Transform p, string text, Vector3 pos, Vector3 normal, float size, Color col)
    {
        GameObject g = new GameObject("Text_" + text);
        g.transform.SetParent(p, false);
        g.transform.position = pos + normal * 0.012f;
        g.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up);
        TextMesh tm = g.AddComponent<TextMesh>();
        tm.text = text;
        tm.characterSize = size;
        tm.fontSize = 64;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = col;
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.font = f;
        g.GetComponent<MeshRenderer>().sharedMaterial = f.material;
        count++;
    }

    // wall surface check: every sample point of a w x h patch must touch a wall within a hair of the expected distance
    static bool SurfaceOK(Vector3 center, Vector3 normal, float w, float h, bool horizontalSurface)
    {
        Vector3 right, up;
        if (horizontalSurface) { right = Vector3.right; up = Vector3.forward; }
        else { up = Vector3.up; right = Vector3.Cross(up, normal).normalized; }
        Vector3[] pts = {
            center, center + right * (w * 0.5f) + up * (h * 0.5f), center - right * (w * 0.5f) + up * (h * 0.5f),
            center + right * (w * 0.5f) - up * (h * 0.5f), center - right * (w * 0.5f) - up * (h * 0.5f) };
        foreach (Vector3 p in pts)
        {
            RaycastHit hit;
            if (!Physics.Raycast(p + normal * 0.05f, -normal, out hit, 0.12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            if (Mathf.Abs(hit.distance - 0.05f) > 0.012f) return false;
            string n = hit.collider.name;
            bool ok = n.Contains("Wall") || n.Contains("Partition") || n.Contains("Floor") || n.Contains("Ceiling") || n.Contains("Roof") || n.Contains("seg") || n.Contains("sill") || n.Contains("head");
            if (!ok) return false;
        }
        return true;
    }

    // ================================================================== Phase 1: classrooms + corridor + west building props
    public static string Phase1()
    {
        if (EditorApplication.isPlaying) return "Editor is in play mode - aborting";
        Init(101);
        Transform root = Root();
        Transform g = Group(root, "ClassroomDetails");
        Physics.SyncTransforms();
        int shelves = 0, posters = 0, clocks = 0, notebooks = 0;

        // ground-floor classrooms: x = -/+ (2.1 .. 9.9), z blocks of 10 m
        for (int side = 0; side < 2; side++)
        {
            float sgn = side == 0 ? -1f : 1f;
            for (int room = 0; room < 4; room++)
            {
                float zs = 10f * room;
                Transform rt = Group(g, "Room_" + (side == 0 ? "L" : "R") + room);
                float cx = sgn * 6f;

                // chalk tray + wall clock above the board
                Box(rt, "ChalkTray", new Vector3(cx, 1.08f, zs + 0.18f), new Vector3(3.6f, 0.03f, 0.1f), Quaternion.identity, Mt("School_Wood"), false);
                Box(rt, "Chalk", new Vector3(cx - 1.2f, 1.105f, zs + 0.18f), new Vector3(0.08f, 0.015f, 0.015f), Quaternion.Euler(0, 20, 0), Mt("Dress_White"), false);
                Vector3 clockPos = new Vector3(cx, 2.55f, zs + 0.22f);
                Cyl(rt, "ClockFace", clockPos, 0.36f, 0.04f, Quaternion.Euler(90, 0, 0), Mt("Dress_Clock"), false);
                Box(rt, "ClockHandH", clockPos + new Vector3(0.02f, 0.04f, 0.025f), new Vector3(0.015f, 0.1f, 0.006f), Quaternion.Euler(0, 0, -50), Mt("Dress_Dark"), false);
                Box(rt, "ClockHandM", clockPos + new Vector3(0f, 0.07f, 0.03f), new Vector3(0.01f, 0.15f, 0.006f), Quaternion.Euler(0, 0, 20), Mt("Dress_Dark"), false);
                clocks++;

                // posters beside the board (only where the wall is really there)
                for (int k = 0; k < 2; k++)
                {
                    float px = cx + (k == 0 ? -2.65f : 2.65f);
                    Vector3 pc = new Vector3(px, 1.65f, zs + 0.1f);
                    if (Mathf.Abs(px) < 2.6f || Mathf.Abs(px) > 9.3f) continue;
                    if (!SurfaceOK(pc, Vector3.forward, 0.55f, 0.8f, false)) continue;
                    Box(rt, "Poster", pc + Vector3.forward * 0.008f, new Vector3(0.55f, 0.8f, 0.012f), Quaternion.Euler(0, 0, Rr(-3f, 3f)), Mt("Dress_Poster" + rng.Next(5)), false);
                    Box(rt, "PosterBand", pc + Vector3.forward * 0.015f + Vector3.up * 0.18f, new Vector3(0.42f, 0.1f, 0.006f), Quaternion.identity, Mt("Dress_Paper"), false);
                    posters++;
                }

                // bookshelf on the corridor-side wall, open face towards the room
                foreach (float zOff in new float[] { 2.1f, 8.0f })
                {
                    float wallFace = sgn * 2.1f;
                    Vector3 center = new Vector3(wallFace + sgn * 0.17f, 0.95f, zs + zOff);
                    if (!Free(center, new Vector3(0.16f, 0.92f, 1.0f), Quaternion.identity)) continue;
                    Transform sh = new GameObject("Bookshelf").transform;
                    sh.SetParent(rt, false);
                    sh.position = new Vector3(center.x, 0f, center.z);
                    sh.rotation = Quaternion.Euler(0, sgn < 0 ? 180f : 0f, 0); // local +x = open side
                    Material wood = Mt("School_Shelf");
                    Local(sh, "Back", new Vector3(-0.15f, 0.95f, 0f), new Vector3(0.02f, 1.9f, 2.0f), wood, true);
                    Local(sh, "SideA", new Vector3(0f, 0.95f, 0.985f), new Vector3(0.32f, 1.9f, 0.03f), wood, true);
                    Local(sh, "SideB", new Vector3(0f, 0.95f, -0.985f), new Vector3(0.32f, 1.9f, 0.03f), wood, true);
                    float[] levels = { 0.15f, 0.62f, 1.09f, 1.56f, 1.9f };
                    foreach (float ly in levels) Local(sh, "Board", new Vector3(0f, ly, 0f), new Vector3(0.32f, 0.03f, 2.0f), wood, true);
                    for (int lv = 0; lv < 4; lv++)
                    {
                        float zc = -0.9f;
                        int nb = 0;
                        while (zc < 0.88f && nb < 14)
                        {
                            if (lv == 3 && R() < 0.35f) { zc += Rr(0.15f, 0.35f); continue; } // gaps on the top shelf
                            float t = Rr(0.035f, 0.06f), h = Rr(0.22f, 0.34f);
                            Local(sh, "Book", new Vector3(Rr(0.0f, 0.04f), levels[lv] + 0.015f + h * 0.5f, zc + t * 0.5f), new Vector3(0.17f, h, t), Mt("Dress_Book" + rng.Next(8)), false);
                            zc += t + Rr(0.0f, 0.012f);
                            nb++;
                        }
                    }
                    shelves++;
                }
            }
        }

        // notebooks and pencils on upright student desks
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (!t.name.StartsWith("Top")) continue;
            Vector3 s = t.lossyScale;
            if (Mathf.Abs(s.x - 0.7f) > 0.02f || Mathf.Abs(s.y - 0.04f) > 0.01f || Mathf.Abs(s.z - 0.5f) > 0.02f) continue;
            if (Vector3.Dot(t.up, Vector3.up) < 0.99f) continue;
            if (R() > 0.4f) continue;
            Vector3 top = t.position + Vector3.up * (s.y * 0.5f);
            Quaternion yaw = Quaternion.Euler(0, t.eulerAngles.y + Rr(-18f, 18f), 0);
            Vector3 np = top + t.right * Rr(-0.12f, 0.12f) + t.forward * Rr(-0.08f, 0.08f) + Vector3.up * 0.006f;
            Box(g, "Notebook", np, new Vector3(0.21f, 0.012f, 0.29f), yaw, Mt("Dress_Book" + rng.Next(8)), false);
            if (R() < 0.6f)
                Box(g, "Pencil", np + t.right * 0.03f + Vector3.up * 0.012f, new Vector3(0.008f, 0.008f, 0.17f), yaw * Quaternion.Euler(0, 35, 0), Mt("School_Door"), false);
            notebooks++;
        }

        // ---------------- corridor props (x -1.9 .. 1.9) ----------------
        Transform cg = Group(root, "CorridorDetails");
        int props = 0;
        foreach (float sgn in new float[] { -1f, 1f })
        {
            float wallX = sgn * 1.9f;
            Vector3 inward = new Vector3(-sgn, 0f, 0f);
            foreach (float z in new float[] { 2.3f, 37.8f })
            {
                // notice board with pinned sheets, bench and bin below
                Vector3 bc = new Vector3(wallX + inward.x * 0.02f, 1.7f, z);
                if (SurfaceOK(bc, inward, 1.4f, 0.9f, false))
                {
                    Box(cg, "NoticeBoard", bc, new Vector3(0.03f, 0.9f, 1.4f), Quaternion.identity, Mt("Dress_Cork"), false);
                    for (int k = 0; k < 4; k++)
                        Box(cg, "Sheet", bc + inward * 0.02f + new Vector3(0, Rr(-0.25f, 0.25f), -0.5f + 0.33f * k), new Vector3(0.008f, 0.26f, 0.2f), Quaternion.Euler(Rr(-6f, 6f), 0, 0), Mt("Dress_Paper"), false);
                    props++;
                }
                Vector3 benchC = new Vector3(wallX + inward.x * 0.25f, 0.42f, z);
                if (Free(benchC, new Vector3(0.22f, 0.4f, 0.85f), Quaternion.identity))
                {
                    Box(cg, "BenchSeat", benchC, new Vector3(0.4f, 0.05f, 1.6f), Quaternion.identity, Mt("Dress_Bench"), true);
                    Box(cg, "BenchLegA", new Vector3(benchC.x, 0.2f, z - 0.7f), new Vector3(0.36f, 0.4f, 0.05f), Quaternion.identity, Mt("School_Metal"), true);
                    Box(cg, "BenchLegB", new Vector3(benchC.x, 0.2f, z + 0.7f), new Vector3(0.36f, 0.4f, 0.05f), Quaternion.identity, Mt("School_Metal"), true);
                    props++;
                }
                Vector3 binC = new Vector3(wallX + inward.x * 0.25f, 0.3f, z + 1.35f);
                if (Free(binC, new Vector3(0.2f, 0.3f, 0.2f), Quaternion.identity))
                {
                    Cyl(cg, "TrashBin", binC, 0.34f, 0.6f, Quaternion.identity, Mt("Dress_Bin"), true);
                    props++;
                }
            }
            // wall clocks
            foreach (float z in new float[] { 10f, 30f })
            {
                Vector3 cc = new Vector3(wallX + inward.x * 0.03f, 2.55f, z);
                if (!SurfaceOK(cc, inward, 0.4f, 0.4f, false)) continue;
                Cyl(cg, "CorridorClock", cc, 0.4f, 0.04f, Quaternion.Euler(0, 0, 90), Mt("Dress_Clock"), false);
                Box(cg, "HandH", cc + inward * 0.025f + Vector3.up * 0.04f, new Vector3(0.006f, 0.1f, 0.015f), Quaternion.Euler(Rr(0, 360), 0, 0), Mt("Dress_Dark"), false);
                Box(cg, "HandM", cc + inward * 0.03f + Vector3.up * 0.07f, new Vector3(0.006f, 0.15f, 0.01f), Quaternion.Euler(Rr(0, 360), 0, 0), Mt("Dress_Dark"), false);
                props++;
            }
        }
        // fire extinguishers (wall bracket + red cylinder)
        foreach (Vector3 ep in new Vector3[] { new Vector3(-1.82f, 0.95f, 0.9f), new Vector3(1.82f, 0.95f, 39.0f), new Vector3(-1.82f, 0.95f, 20.0f) })
        {
            if (!Free(ep, new Vector3(0.1f, 0.3f, 0.1f), Quaternion.identity)) continue;
            Cyl(cg, "FireExtinguisher", ep, 0.15f, 0.5f, Quaternion.identity, Mt("Dress_Red"), true);
            Box(cg, "ExtinguisherBracket", new Vector3(ep.x + Mathf.Sign(ep.x) * 0.07f, ep.y + 0.1f, ep.z), new Vector3(0.03f, 0.05f, 0.16f), Quaternion.identity, Mt("Dress_Steel"), false);
            props++;
        }
        // exit sign above the far door
        Box(cg, "ExitSign", new Vector3(0f, 2.62f, 39.76f), new Vector3(0.6f, 0.2f, 0.04f), Quaternion.identity, Mt("Dress_ExitSign"), false);
        Label(cg, "EXIT", new Vector3(0f, 2.62f, 39.74f), Vector3.back, 0.04f, new Color(0.7f, 1f, 0.75f));

        return "Phase1: shelves=" + shelves + " posters=" + posters + " clocks=" + clocks + " notebooks=" + notebooks + " corridorProps=" + props + " objects=" + count;
    }

    static GameObject Local(Transform p, string name, Vector3 lp, Vector3 size, Material m, bool col)
    {
        GameObject o = GameObject.CreatePrimitive(PrimitiveType.Cube);
        o.name = name;
        o.transform.SetParent(p, false);
        o.transform.localPosition = lp;
        o.transform.localScale = size;
        o.GetComponent<Renderer>().sharedMaterial = m;
        if (!col) Object.DestroyImmediate(o.GetComponent<Collider>());
        count++;
        return o;
    }

    // ================================================================== Phase 2: restrooms (second floor) + west-building details
    public static string Phase2()
    {
        if (EditorApplication.isPlaying) return "Editor is in play mode - aborting";
        Init(202);
        Transform root = Root();
        Transform rr = Group(root, "RestroomDetails");
        Physics.SyncTransforms();
        int soap = 0, paper = 0, bins = 0, pipes = 0;
        string log = "";

        foreach (string gender in new string[] { "Women", "Men" })
        {
            // sinks: soap dispenser on the wall, drain pipes under them
            List<Vector3> sinkPos = new List<Vector3>();
            Vector3 wallDir = Vector3.zero;
            for (int i = 1; i <= 4; i++)
            {
                GameObject sink = GameObject.Find("SecondFloor_" + gender + "_Sink_0" + i);
                GameObject mirror = GameObject.Find("SecondFloor_" + gender + "_Mirror_0" + i);
                if (sink == null) continue;
                Vector3 sp = sink.transform.position;
                sinkPos.Add(sp);
                Vector3 dir = Vector3.zero;
                if (mirror != null) { dir = mirror.transform.position - sp; dir.y = 0; }
                if (dir.sqrMagnitude < 0.0001f) continue;
                dir.Normalize();
                wallDir = dir;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                Vector3 wallPt = mirror.transform.position;                 // on the wall surface
                Vector3 dp = new Vector3(wallPt.x, sp.y + 0.28f, wallPt.z) + side * 0.34f - dir * 0.05f;
                if (Free(dp, new Vector3(0.05f, 0.08f, 0.04f), Quaternion.LookRotation(dir)))
                {
                    Box(rr, "SoapDispenser", dp, new Vector3(0.08f, 0.16f, 0.07f), Quaternion.LookRotation(dir), Mt("Dress_White"), false);
                    Box(rr, "SoapPump", dp + Vector3.down * 0.1f - dir * 0.03f, new Vector3(0.02f, 0.03f, 0.04f), Quaternion.LookRotation(dir), Mt("Dress_Steel"), false);
                    soap++;
                }
                // supply pipe and drain under the sink
                Vector3 pp = new Vector3(sp.x, sp.y * 0.5f - 0.02f, sp.z) + dir * 0.12f;
                if (Free(pp, new Vector3(0.03f, Mathf.Max(0.05f, sp.y * 0.45f), 0.03f), Quaternion.identity))
                {
                    Cyl(rr, "SinkDrainPipe", pp, 0.045f, Mathf.Max(0.2f, sp.y - 0.12f), Quaternion.identity, Mt("Dress_Pipe"), false);
                    Cyl(rr, "SinkTrap", pp + dir * 0.0f + Vector3.down * (pp.y - 0.14f), 0.05f, 0.18f, Quaternion.LookRotation(side) * Quaternion.Euler(90, 0, 0), Mt("Dress_Pipe"), false);
                    pipes++;
                }
            }
            // a pipe run along the wall behind the sinks
            if (sinkPos.Count >= 2 && wallDir != Vector3.zero)
            {
                Vector3 a = sinkPos[0], b = sinkPos[sinkPos.Count - 1];
                Vector3 mid = (a + b) * 0.5f; mid.y = 0.12f; mid += wallDir * 0.1f;
                float len = Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z)) + 0.8f;
                Vector3 run = (b - a); run.y = 0; run.Normalize();
                if (Free(mid, new Vector3(0.04f, 0.04f, len * 0.5f - 0.05f), Quaternion.LookRotation(run)))
                {
                    Cyl(rr, "WaterPipeRun", mid, 0.05f, len, Quaternion.LookRotation(run) * Quaternion.Euler(90, 0, 0), Mt("Dress_Pipe"), false);
                    pipes++;
                }
            }

            // toilet paper holders beside every toilet, bin next to the sinks
            for (int i = 1; i <= 4; i++)
            {
                GameObject tank = GameObject.Find("SecondFloor_" + gender + "_ToiletTank_0" + i);
                GameObject toilet = GameObject.Find("SecondFloor_" + gender + "_Toilet_0" + i);
                if (tank == null || toilet == null) continue;
                Vector3 dir = tank.transform.position - toilet.transform.position; dir.y = 0;
                if (dir.sqrMagnitude < 0.0001f) continue;
                dir.Normalize();
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                foreach (float sd in new float[] { 0.42f, -0.42f })
                {
                    Vector3 pc = new Vector3(tank.transform.position.x, 0.75f, tank.transform.position.z) + side * sd + dir * 0.13f;
                    // must sit on a wall: wall is behind the tank
                    if (!Physics.Raycast(pc - dir * 0.05f, dir, 0.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    if (!Free(pc - dir * 0.05f, new Vector3(0.07f, 0.07f, 0.04f), Quaternion.LookRotation(dir))) continue;
                    Box(rr, "PaperHolder", pc, new Vector3(0.14f, 0.12f, 0.1f), Quaternion.LookRotation(dir), Mt("Dress_Steel"), false);
                    Cyl(rr, "PaperRoll", pc - dir * 0.02f, 0.11f, 0.12f, Quaternion.LookRotation(side) * Quaternion.Euler(0, 0, 90), Mt("Dress_Paper"), false);
                    paper++;
                    break;
                }
            }
            // bin: search free floor spots around the first sink
            if (sinkPos.Count > 0 && wallDir != Vector3.zero)
            {
                Vector3 side = Vector3.Cross(Vector3.up, wallDir);
                Vector3[] tries = {
                    sinkPos[0] - wallDir * 0.45f - side * 0.9f, sinkPos[0] - wallDir * 0.45f + side * 0.9f,
                    sinkPos[sinkPos.Count - 1] - wallDir * 0.45f + side * 0.9f, sinkPos[sinkPos.Count - 1] - wallDir * 0.45f - side * 0.9f };
                foreach (Vector3 tp in tries)
                {
                    Vector3 c = new Vector3(tp.x, 0.3f, tp.z);
                    if (!Free(c, new Vector3(0.22f, 0.29f, 0.22f), Quaternion.identity)) continue;
                    Cyl(rr, "TrashBin", c, 0.34f, 0.6f, Quaternion.identity, Mt("Dress_Bin"), true);
                    bins++;
                    break;
                }
            }
        }
        log += "restrooms: soap=" + soap + " paper=" + paper + " bins=" + bins + " pipes=" + pipes + "; ";

        // ---------------- west building (ground floor halls) ----------------
        Transform wg = Group(root, "WestBuildingDetails");
        int wb = 0;
        // benches along the south wing wall, notice board + clock + extinguisher, bins
        float[] benchX = { -43f, -36f, -30f, -25f, -14f, -12f };
        foreach (float bx in benchX)
        {
            Vector3 bc = new Vector3(bx, 0.42f, 0.55f);
            if (!Free(bc, new Vector3(0.85f, 0.4f, 0.22f), Quaternion.identity)) continue;
            Box(wg, "BenchSeat", bc, new Vector3(1.6f, 0.05f, 0.4f), Quaternion.identity, Mt("Dress_Bench"), true);
            Box(wg, "BenchLegA", new Vector3(bx - 0.7f, 0.2f, 0.55f), new Vector3(0.05f, 0.4f, 0.36f), Quaternion.identity, Mt("School_Metal"), true);
            Box(wg, "BenchLegB", new Vector3(bx + 0.7f, 0.2f, 0.55f), new Vector3(0.05f, 0.4f, 0.36f), Quaternion.identity, Mt("School_Metal"), true);
            wb++;
        }
        foreach (float bx in new float[] { -40f, -28f, -16f })
        {
            Vector3 nc = new Vector3(bx, 1.7f, 0.12f);
            if (!SurfaceOK(nc, Vector3.forward, 1.4f, 0.9f, false)) continue;
            Box(wg, "NoticeBoard", nc + Vector3.forward * 0.015f, new Vector3(1.4f, 0.9f, 0.03f), Quaternion.identity, Mt("Dress_Cork"), false);
            for (int k = 0; k < 4; k++)
                Box(wg, "Sheet", nc + Vector3.forward * 0.035f + new Vector3(-0.5f + 0.33f * k, Rr(-0.22f, 0.22f), 0), new Vector3(0.2f, 0.26f, 0.008f), Quaternion.Euler(0, 0, Rr(-6f, 6f)), Mt("Dress_Paper"), false);
            wb++;
        }
        foreach (float cxp in new float[] { -34f, -22f })
        {
            Vector3 cc = new Vector3(cxp, 2.55f, 0.14f);
            if (!SurfaceOK(cc, Vector3.forward, 0.4f, 0.4f, false)) continue;
            Cyl(wg, "Clock", cc + Vector3.forward * 0.02f, 0.4f, 0.04f, Quaternion.Euler(90, 0, 0), Mt("Dress_Clock"), false);
            Box(wg, "HandH", cc + new Vector3(0.0f, 0.04f, 0.045f), new Vector3(0.015f, 0.1f, 0.006f), Quaternion.Euler(0, 0, Rr(0, 360)), Mt("Dress_Dark"), false);
            Box(wg, "HandM", cc + new Vector3(0.0f, 0.07f, 0.05f), new Vector3(0.01f, 0.15f, 0.006f), Quaternion.Euler(0, 0, Rr(0, 360)), Mt("Dress_Dark"), false);
            wb++;
        }
        foreach (Vector3 ep in new Vector3[] { new Vector3(-45f, 0.95f, 0.3f), new Vector3(-46.6f, 0.95f, 20f), new Vector3(-45f, 0.95f, 39.6f) })
        {
            if (!Free(ep, new Vector3(0.09f, 0.28f, 0.09f), Quaternion.identity)) continue;
            Cyl(wg, "FireExtinguisher", ep, 0.15f, 0.5f, Quaternion.identity, Mt("Dress_Red"), true);
            wb++;
        }
        // direction sign near the stairs
        Vector3 sgc = new Vector3(-19.4f, 2.45f, 0.14f);
        if (SurfaceOK(sgc, Vector3.forward, 1.2f, 0.3f, false))
        {
            Box(wg, "StairsSign", sgc + Vector3.forward * 0.02f, new Vector3(1.2f, 0.3f, 0.04f), Quaternion.identity, Mt("Dress_ExitSign"), false);
            Label(wg, "STAIRS", sgc + Vector3.forward * 0.045f, Vector3.forward, 0.045f, new Color(0.75f, 1f, 0.8f));
            wb++;
        }
        // lockers along the west wing wall (inside face of the outer wall)
        for (int i = 0; i < 12; i++)
        {
            Vector3 lc = new Vector3(-46.725f, 0.95f, 14.2f + 0.45f * i);
            if (!Free(lc, new Vector3(0.17f, 0.93f, 0.21f), Quaternion.identity)) continue;
            Box(wg, "Locker", lc, new Vector3(0.35f, 1.9f, 0.43f), Quaternion.identity, Mt(i % 2 == 0 ? "School_LockerA" : "School_LockerB"), true);
            wb++;
        }
        log += "westBuilding=" + wb;
        return "Phase2: " + log + " objects=" + count;
    }
}
