using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot editor tool: fits out the second-floor staff rooms (open-plan workstations), rebuilds the
/// two restrooms as proper rooms with individual lockable-style stall rooms, and hangs a SchoolDoor on
/// every doorway that did not have one. Safe to delete after running.
/// </summary>
public static class SecondFloorFitout
{
    const float FY = 3.5f;                 // second-floor walking surface
    static System.Random rng;
    static int count;
    static Dictionary<string, Material> cache = new Dictionary<string, Material>();

    static float R() { return (float)rng.NextDouble(); }
    static float Rr(float a, float b) { return a + (b - a) * R(); }

    // ------------------------------------------------------------ materials
    static void Mk(string name, float r, float g, float b, float smooth, float metal)
    {
        string path = "Assets/Materials/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = name;
            AssetDatabase.CreateAsset(m, path);
            m.SetColor("_BaseColor", new Color(r, g, b));
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            EditorUtility.SetDirty(m);
        }
        cache[name] = m;
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
        Mk("FO_Desk", 0.66f, 0.66f, 0.63f, 0.5f, 0f);
        Mk("FO_Panel", 0.27f, 0.12f, 0.07f, 0.15f, 0f);
        Mk("FO_Shelf", 0.60f, 0.55f, 0.44f, 0.2f, 0f);
        Mk("FO_Black", 0.035f, 0.035f, 0.04f, 0.35f, 0f);
        Mk("FO_Screen", 0.01f, 0.012f, 0.015f, 0.8f, 0f);
        Mk("FO_Key", 0.30f, 0.30f, 0.29f, 0.3f, 0f);
        Mk("FO_Orange", 0.58f, 0.22f, 0.04f, 0.1f, 0f);
        Mk("FO_PaperW", 0.60f, 0.60f, 0.56f, 0.05f, 0f);
        Mk("FO_Blue", 0.12f, 0.20f, 0.34f, 0.1f, 0f);
        Mk("FO_Gray", 0.30f, 0.30f, 0.30f, 0.2f, 0f);
        Mk("FO_Tile", 0.50f, 0.54f, 0.53f, 0.6f, 0f);
        Mk("FO_TileFloor", 0.30f, 0.33f, 0.32f, 0.55f, 0f);
        Mk("FO_Porcelain", 0.68f, 0.68f, 0.65f, 0.85f, 0f);
        Mk("FO_Chrome", 0.55f, 0.55f, 0.57f, 0.85f, 0.9f);
        Mk("FO_Mirror", 0.16f, 0.18f, 0.20f, 0.95f, 0.6f);
        Mk("FO_Stall", 0.20f, 0.32f, 0.34f, 0.3f, 0f);
        Mk("FO_Dark", 0.03f, 0.03f, 0.03f, 0.2f, 0f);
        Mk("FO_Yellow", 0.55f, 0.45f, 0.05f, 0.2f, 0f);
        Mk("School_Door", 0.26f, 0.20f, 0.13f, 0.2f, 0f);
        Mk("School_Metal", 0.15f, 0.15f, 0.16f, 0.5f, 0.6f);
    }

    // ------------------------------------------------------------ primitives (world space)
    static GameObject Cube(Transform p, string name, Vector3 c, Vector3 s, float yaw, Material m, bool col)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(p, false);
        g.transform.position = c;
        g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        g.transform.localScale = s;
        g.GetComponent<Renderer>().sharedMaterial = m;
        if (!col) Object.DestroyImmediate(g.GetComponent<Collider>());
        count++;
        return g;
    }
    // vertical cylinder; sx/sz are diameters, h is the full height
    static GameObject Cyl(Transform p, string name, Vector3 c, float sx, float h, float sz, Material m, bool col)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = name;
        g.transform.SetParent(p, false);
        g.transform.position = c;
        g.transform.localScale = new Vector3(sx, h * 0.5f, sz);
        g.GetComponent<Renderer>().sharedMaterial = m;
        if (!col) Object.DestroyImmediate(g.GetComponent<Collider>());
        count++;
        return g;
    }
    static GameObject CylRot(Transform p, string name, Vector3 c, float dia, float len, Quaternion rot, Material m)
    {
        GameObject g = Cyl(p, name, c, dia, len, dia, m, false);
        g.transform.rotation = rot;
        return g;
    }
    static Transform Sub(Transform p, string n)
    {
        GameObject g = new GameObject(n);
        g.transform.SetParent(p, false);
        return g.transform;
    }
    static GameObject Leaf(Transform p, string name, Vector3 lp, Vector3 size, Material m, bool col)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(p, false);
        g.transform.localPosition = lp;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = m;
        if (!col) Object.DestroyImmediate(g.GetComponent<Collider>());
        count++;
        return g;
    }

    // ------------------------------------------------------------ doors (same SchoolDoor system as everywhere else)
    static GameObject MakeDoor(Transform parent, string name, Vector3 hinge, float yaw, float width, float height, float thick, float openAngle, bool startOpen, Material m, bool handles)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = hinge;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        GameObject leaf = Leaf(root.transform, name + "_Leaf", new Vector3(0f, height * 0.5f, width * 0.5f), new Vector3(thick, height, width), m, true);
        SchoolDoor sd = root.AddComponent<SchoolDoor>();
        sd.leaf = leaf.transform;
        sd.openAngle = openAngle;
        sd.rotationSpeed = 360f;
        sd.startOpen = startOpen;
        if (handles)
        {
            Material metal = Mt("School_Metal");
            Leaf(root.transform, "Handle_A", new Vector3(-(thick * 0.5f + 0.025f), 1.0f, width - 0.12f), new Vector3(0.03f, 0.03f, 0.13f), metal, false);
            Leaf(root.transform, "Handle_B", new Vector3((thick * 0.5f + 0.025f), 1.0f, width - 0.12f), new Vector3(0.03f, 0.03f, 0.13f), metal, false);
        }
        return root;
    }

    // ------------------------------------------------------------ office chair
    static void OfficeChair(Transform p, string name, Vector3 floorPos, float yaw)
    {
        Transform g = new GameObject(name).transform;
        g.SetParent(p, false);
        g.position = floorPos;
        g.rotation = Quaternion.Euler(0f, yaw, 0f);
        Material black = Mt("FO_Black");
        Leaf(g, "Seat", new Vector3(0f, 0.50f, 0f), new Vector3(0.46f, 0.07f, 0.46f), black, true);
        Leaf(g, "Back", new Vector3(0f, 0.84f, 0.21f), new Vector3(0.42f, 0.50f, 0.06f), black, true);
        Leaf(g, "ArmL", new Vector3(-0.25f, 0.68f, 0.0f), new Vector3(0.04f, 0.04f, 0.28f), black, false);
        Leaf(g, "ArmR", new Vector3(0.25f, 0.68f, 0.0f), new Vector3(0.04f, 0.04f, 0.28f), black, false);
        Leaf(g, "ArmPostL", new Vector3(-0.25f, 0.58f, 0.0f), new Vector3(0.03f, 0.2f, 0.03f), black, false);
        Leaf(g, "ArmPostR", new Vector3(0.25f, 0.58f, 0.0f), new Vector3(0.03f, 0.2f, 0.03f), black, false);
        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.name = "Post";
        post.transform.SetParent(g, false);
        post.transform.localPosition = new Vector3(0f, 0.28f, 0f);
        post.transform.localScale = new Vector3(0.06f, 0.2f, 0.06f);
        post.GetComponent<Renderer>().sharedMaterial = Mt("FO_Gray");
        Object.DestroyImmediate(post.GetComponent<Collider>());
        for (int i = 0; i < 5; i++)
        {
            GameObject leg = Leaf(g, "StarLeg", new Vector3(0f, 0.07f, 0f), new Vector3(0.05f, 0.03f, 0.30f), black, false);
            leg.transform.localRotation = Quaternion.Euler(0f, i * 72f, 0f);
            leg.transform.localPosition = leg.transform.localRotation * new Vector3(0f, 0.07f, 0.15f);
        }
        count += 2;
    }

    // ------------------------------------------------------------ open-plan workstation cluster (spine along X)
    static void Cluster(Transform p, string tag, float xc, float zStart, float length, int stations)
    {
        float zs = zStart + 1.5f;
        float x0 = xc - length * 0.5f;
        float pitch = length / stations;
        Material panel = Mt("FO_Panel"), shelf = Mt("FO_Shelf");

        // low brown spine panel with a shelf unit on top (as in the reference photo)
        Cube(p, tag + "_Spine", new Vector3(xc, FY + 0.575f, zs), new Vector3(length, 1.15f, 0.06f), 0f, panel, true);
        float sy = FY + 1.15f;
        Cube(p, tag + "_ShelfBase", new Vector3(xc, sy + 0.015f, zs), new Vector3(length, 0.03f, 0.38f), 0f, shelf, true);
        Cube(p, tag + "_ShelfTop", new Vector3(xc, sy + 0.385f, zs), new Vector3(length, 0.03f, 0.38f), 0f, shelf, false);
        for (int i = 0; i <= stations; i++)
            Cube(p, tag + "_ShelfSide", new Vector3(x0 + i * pitch, sy + 0.2f, zs), new Vector3(0.03f, 0.37f, 0.38f), 0f, shelf, false);

        // folders and boxes on the shelf (orange / white / blue / grey)
        string[] fm = { "FO_Orange", "FO_PaperW", "FO_Blue", "FO_Gray", "FO_Orange" };
        for (int i = 0; i < stations; i++)
        {
            float cx = x0 + i * pitch + 0.08f;
            float end = x0 + (i + 1) * pitch - 0.08f;
            while (cx < end - 0.06f)
            {
                float t = Rr(0.04f, 0.07f), h = Rr(0.22f, 0.32f);
                float side = R() < 0.5f ? -1f : 1f;
                GameObject f = Cube(p, "Folder", new Vector3(cx + t * 0.5f, sy + 0.03f + h * 0.5f, zs + side * 0.07f), new Vector3(t, h, 0.24f), 0f, Mt(fm[rng.Next(fm.Length)]), false);
                if (R() < 0.12f) f.transform.rotation = Quaternion.Euler(0f, 0f, Rr(-14f, 14f));
                cx += t + Rr(0.0f, 0.03f);
            }
        }

        // brown partition fins between stations and at both ends
        for (int i = 0; i <= stations; i++)
        {
            float x = x0 + i * pitch;
            for (int s = -1; s <= 1; s += 2)
                Cube(p, tag + "_Fin", new Vector3(x, FY + 0.57f, zs + s * 0.405f), new Vector3(0.05f, 1.14f, 0.75f), 0f, panel, true);
        }

        // stations on both sides of the spine
        for (int s = -1; s <= 1; s += 2)
            for (int i = 0; i < stations; i++)
                Station(p, tag, x0 + (i + 0.5f) * pitch, zs, s, pitch);
    }

    static void Station(Transform p, string tag, float x, float zs, int side, float pitch)
    {
        float w = pitch - 0.1f;
        float dy = FY + 0.74f;
        Material desk = Mt("FO_Desk");

        // white desk with a rounded outer edge
        Cube(p, tag + "_Desk", new Vector3(x, dy, zs + side * 0.38f), new Vector3(w, 0.04f, 0.70f), 0f, desk, true);
        Cyl(p, tag + "_DeskCurve", new Vector3(x, dy, zs + side * 0.73f), w, 0.04f, 0.34f, desk, true);
        for (int k = -1; k <= 1; k += 2)
            Cyl(p, tag + "_DeskLeg", new Vector3(x + k * (w * 0.5f - 0.08f), FY + 0.36f, zs + side * 0.80f), 0.05f, 0.72f, 0.05f, Mt("FO_Chrome"), false);

        // black drawer pedestal under half of the desks
        if (R() < 0.55f)
            Cube(p, tag + "_Drawers", new Vector3(x - w * 0.5f + 0.27f, FY + 0.275f, zs + side * 0.45f), new Vector3(0.4f, 0.55f, 0.5f), 0f, Mt("FO_Black"), true);

        // monitor, keyboard, mouse
        float mz = zs + side * 0.22f;
        Cube(p, "MonitorBase", new Vector3(x, dy + 0.026f, mz), new Vector3(0.22f, 0.012f, 0.16f), 0f, Mt("FO_Black"), false);
        Cube(p, "MonitorNeck", new Vector3(x, dy + 0.1f, mz), new Vector3(0.04f, 0.14f, 0.03f), 0f, Mt("FO_Black"), false);
        Cube(p, "MonitorBody", new Vector3(x, dy + 0.30f, mz), new Vector3(0.56f, 0.33f, 0.035f), 0f, Mt("FO_Black"), false);
        Cube(p, "MonitorGlass", new Vector3(x, dy + 0.30f, mz + side * 0.019f), new Vector3(0.52f, 0.29f, 0.004f), 0f, Mt("FO_Screen"), false);
        Cube(p, "Keyboard", new Vector3(x, dy + 0.028f, zs + side * 0.52f), new Vector3(0.42f, 0.015f, 0.14f), 0f, Mt("FO_Key"), false);
        Cube(p, "Mouse", new Vector3(x + 0.34f, dy + 0.03f, zs + side * 0.54f), new Vector3(0.06f, 0.025f, 0.1f), 0f, Mt("FO_Key"), false);

        // paperwork: orange folder stack, loose sheets, mug
        Cube(p, "FolderStack", new Vector3(x - w * 0.32f, dy + 0.045f, zs + side * 0.33f), new Vector3(0.24f, 0.05f, 0.32f), Rr(-12f, 12f), Mt("FO_Orange"), false);
        Cube(p, "Sheets", new Vector3(x + w * 0.28f, dy + 0.026f, zs + side * 0.4f), new Vector3(0.21f, 0.008f, 0.30f), Rr(-25f, 25f), Mt("FO_PaperW"), false);
        if (R() < 0.6f)
            Cyl(p, "Mug", new Vector3(x + w * 0.4f, dy + 0.07f, zs + side * 0.3f), 0.08f, 0.1f, 0.08f, Mt("FO_PaperW"), false);

        // black office chair facing the desk, slightly pulled out / turned like someone just left
        float yaw = (side > 0 ? 0f : 180f) + Rr(-28f, 28f);
        OfficeChair(p, tag + "_Chair", new Vector3(x + Rr(-0.12f, 0.12f), FY, zs + side * Rr(1.12f, 1.32f)), yaw);
    }

    // ------------------------------------------------------------ staff rooms
    public static string StaffRooms()
    {
        if (EditorApplication.isPlaying) return "Editor is in play mode - aborting";
        Init(310);
        Transform root = RootT();
        Transform g = Fresh(root, "StaffRooms");

        // old placeholder single desks / chairs in both rooms are replaced by the workstations
        int removed = 0;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string n = t.name;
            if (n.StartsWith("SecondFloor_StaffWest_Desk") || n.StartsWith("SecondFloor_StaffWest_Chair") ||
                n.StartsWith("SecondFloor_StaffEast_Desk") || n.StartsWith("SecondFloor_StaffEast_Chair") || n.StartsWith("SecondFloor_StaffEast_GuestChair"))
            { Object.DestroyImmediate(t.gameObject); removed++; }
        }

        // East bookcase poked through the divider wall at z=12: slide it fully into the staff room
        GameObject bc = GameObject.Find("SecondFloor_StaffEast_Bookcase");
        if (bc != null)
        {
            Bounds b = bc.GetComponent<Renderer>().bounds;
            float newZ = 12.2f + b.size.z * 0.5f;
            bc.transform.position = new Vector3(bc.transform.position.x, bc.transform.position.y, newZ);
        }

        // West staff room (x -28.77..-20.97, z 12.1..19.9): two clusters, aisle on the door axis (z ~16)
        Transform w = Sub(g, "StaffWest");
        Cluster(w, "W_A", -25.5f, 12.2f, 3.6f, 2);
        Cluster(w, "W_B", -25.5f, 16.8f, 3.6f, 2);

        // East staff room (x -16.77..-8.97, z 12.1..24.9): three clusters around the door aisle (z ~18.5)
        Transform e = Sub(g, "StaffEast");
        Cluster(e, "E_A", -13.8f, 12.2f, 3.6f, 2);
        Cluster(e, "E_B", -13.8f, 21.7f, 3.6f, 2);
        Cluster(e, "E_C", -11.1f, 16.7f, 3.6f, 2);

        // a copier against the east wall, a few sheets dropped on the floor
        Cube(e, "Copier", new Vector3(-9.45f, FY + 0.45f, 15.4f), new Vector3(0.6f, 0.9f, 0.75f), 0f, Mt("FO_Desk"), true);
        Cube(e, "CopierLid", new Vector3(-9.45f, FY + 0.93f, 15.4f), new Vector3(0.6f, 0.06f, 0.75f), 0f, Mt("FO_Gray"), false);
        foreach (Transform room in new Transform[] { w, e })
            for (int i = 0; i < 5; i++)
            {
                float x = room == w ? Rr(-23.0f, -21.4f) : Rr(-15.9f, -13.0f);
                float z = room == w ? Rr(15.0f, 17.0f) : Rr(15.5f, 21.0f);
                Cube(room, "FloorSheet", new Vector3(x, FY + 0.004f, z), new Vector3(0.21f, 0.004f, 0.30f), Rr(0f, 360f), Mt("FO_PaperW"), false);
            }
        return "StaffRooms: removed " + removed + " placeholder pieces, built objects=" + count;
    }

    static Transform RootT()
    {
        GameObject r = GameObject.Find("SecondFloorFitout");
        if (r == null) r = new GameObject("SecondFloorFitout");
        return r.transform;
    }
    static Transform Fresh(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) Object.DestroyImmediate(t.gameObject);
        return Sub(parent, name);
    }

    // ------------------------------------------------------------ restrooms
    // Common builder. wall = the wall the stalls stand against (north, z = zBack). Door of the room, baffle, sinks etc. are passed in.
    public static string Restrooms()
    {
        if (EditorApplication.isPlaying) return "Editor is in play mode - aborting";
        Init(420);
        Transform root = RootT();
        Transform g = Fresh(root, "Restrooms");

        // remove the old placeholder fittings
        int removed = 0;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name.StartsWith("SecondFloor_Women_") || t.name.StartsWith("SecondFloor_Men_"))
            { Object.DestroyImmediate(t.gameObject); removed++; }
        }

        // ---------------- WOMEN: inner x -28.77..-20.97, z 28.1..39.9, entrance on the east wall (z ~34), 5 stalls ----------------
        BuildRestroom(g, "Women", -28.77f, -20.97f, 28.1f, 39.9f, 5, true);
        // ---------------- MEN: inner x -16.77..-8.97, z 25.1..39.9, entrance on the west wall (z ~32.5), 3 stalls + urinals ----------------
        BuildRestroom(g, "Men", -16.77f, -8.97f, 25.1f, 39.9f, 3, false);
        return "Restrooms: removed " + removed + " placeholder fittings, built objects=" + count;
    }

    static void BuildRestroom(Transform parent, string who, float xa, float xb, float za, float zb, int stalls, bool women)
    {
        Transform t = Sub(parent, who);
        Material tile = Mt("FO_Tile"), stall = Mt("FO_Stall"), porc = Mt("FO_Porcelain"), chrome = Mt("FO_Chrome");
        Material mirror = Mt("FO_Mirror"), dark = Mt("FO_Dark");
        float entranceZ = women ? 34.0f : 32.5f;      // centre of the entrance door
        int doorWall = women ? 1 : -1;                 // +1: entrance on the east (xb) wall, -1: on the west (xa) wall
        float doorX = women ? xb : xa;
        float inward = women ? -1f : 1f;               // direction from the door into the room along X

        // floor tile and lower wall tile bands
        Cube(t, "FloorTile", new Vector3((xa + xb) * 0.5f, FY + 0.004f, (za + zb) * 0.5f), new Vector3(xb - xa, 0.008f, zb - za), 0f, Mt("FO_TileFloor"), false);
        float bandH = 1.2f;
        Cube(t, "TileBand_S", new Vector3((xa + xb) * 0.5f, FY + bandH * 0.5f, za + 0.006f), new Vector3(xb - xa, bandH, 0.012f), 0f, tile, false);
        float stallFront = zb - 1.7f;
        // west and east bands, interrupted by the entrance door
        float wallXs = xa + 0.006f, wallXe = xb - 0.006f;
        if (women)
        {
            Cube(t, "TileBand_W", new Vector3(wallXs, FY + bandH * 0.5f, (za + stallFront) * 0.5f), new Vector3(0.012f, bandH, stallFront - za), 0f, tile, false);
            Cube(t, "TileBand_E1", new Vector3(wallXe, FY + bandH * 0.5f, (za + 33.35f) * 0.5f), new Vector3(0.012f, bandH, 33.35f - za), 0f, tile, false);
            Cube(t, "TileBand_E2", new Vector3(wallXe, FY + bandH * 0.5f, (34.65f + stallFront) * 0.5f), new Vector3(0.012f, bandH, stallFront - 34.65f), 0f, tile, false);
        }
        else
        {
            Cube(t, "TileBand_E", new Vector3(wallXe, FY + bandH * 0.5f, (za + stallFront) * 0.5f), new Vector3(0.012f, bandH, stallFront - za), 0f, tile, false);
            Cube(t, "TileBand_W1", new Vector3(wallXs, FY + bandH * 0.5f, (za + 31.85f) * 0.5f), new Vector3(0.012f, bandH, 31.85f - za), 0f, tile, false);
            Cube(t, "TileBand_W2", new Vector3(wallXs, FY + bandH * 0.5f, (33.15f + stallFront) * 0.5f), new Vector3(0.012f, bandH, stallFront - 33.15f), 0f, tile, false);
        }

        // entry privacy baffle, in front of the door but clear of the swinging leaf
        float baffleX = doorX + inward * 2.0f;
        Cube(t, "EntryBaffle", new Vector3(baffleX, FY + 1.2f, entranceZ + 0.1f), new Vector3(0.1f, 2.4f, 1.9f), 0f, stall, true);

        // ---- individual stall rooms along the north wall: floor-to-ceiling-ish partitions, own door, toilet, roll holder, hook ----
        float x0 = xa;
        for (int i = 0; i <= stalls; i++)
            Cube(t, "StallPartition", new Vector3(x0 + i * 1.0f, FY + 0.15f + 1.0f, (stallFront + zb) * 0.5f), new Vector3(0.05f, 2.0f, zb - stallFront), 0f, stall, true);
        for (int i = 0; i < stalls; i++)
        {
            float sx0 = x0 + i * 1.0f + 0.025f, sx1 = x0 + (i + 1) * 1.0f - 0.025f, cx = (sx0 + sx1) * 0.5f;
            // toilet against the north wall
            Cube(t, "Cistern", new Vector3(cx, FY + 0.70f, zb - 0.11f), new Vector3(0.40f, 0.40f, 0.20f), 0f, porc, true);
            Cyl(t, "FlushButton", new Vector3(cx, FY + 0.91f, zb - 0.11f), 0.05f, 0.02f, 0.05f, chrome, false);
            Cube(t, "BowlBack", new Vector3(cx, FY + 0.21f, zb - 0.32f), new Vector3(0.32f, 0.42f, 0.28f), 0f, porc, true);
            Cyl(t, "BowlFront", new Vector3(cx, FY + 0.21f, zb - 0.46f), 0.36f, 0.42f, 0.36f, porc, true);
            Cyl(t, "Seat", new Vector3(cx, FY + 0.43f, zb - 0.46f), 0.38f, 0.03f, 0.38f, dark, false);
            Cube(t, "SeatBack", new Vector3(cx, FY + 0.43f, zb - 0.33f), new Vector3(0.34f, 0.03f, 0.22f), 0f, dark, false);
            // toilet-roll holder on the partition and a coat hook on the back wall
            Cube(t, "RollHolder", new Vector3(sx0 + 0.07f, FY + 0.78f, zb - 0.62f), new Vector3(0.1f, 0.12f, 0.14f), 0f, chrome, false);
            CylRot(t, "Roll", new Vector3(sx0 + 0.07f, FY + 0.78f, zb - 0.62f), 0.11f, 0.1f, Quaternion.Euler(0f, 0f, 90f), Mt("FO_PaperW"));
            Cube(t, "CoatHook", new Vector3(cx + 0.28f, FY + 1.7f, zb - 0.04f), new Vector3(0.04f, 0.04f, 0.07f), 0f, chrome, false);
            if (women) Cyl(t, "SanitaryBin", new Vector3(sx1 - 0.14f, FY + 0.15f, zb - 0.38f), 0.2f, 0.3f, 0.2f, Mt("FO_Gray"), false);
            // the stall's own door (same SchoolDoor system), hinged on the west partition, opens outward
            MakeDoor(t, who + "_StallDoor_" + (i + 1), new Vector3(sx0 + 0.005f, FY + 0.15f, stallFront), 90f, (sx1 - sx0) - 0.03f, 1.85f, 0.03f, 85f, (i % 3 == 1), stall, false);
            Cube(t, "StallLatch", new Vector3(sx1 - 0.06f, FY + 1.0f, stallFront - 0.025f), new Vector3(0.05f, 0.05f, 0.03f), 0f, chrome, false);
        }

        // ---- sinks, mirrors, soap and towels ----
        if (women)
        {
            float wx = xa;
            float[] sz = { 29.2f, 30.5f, 31.8f, 33.1f };
            foreach (float z in sz) Basin(t, new Vector3(wx, FY, z), 1, 0, true);
            for (int k = 0; k < sz.Length - 1; k++)
                Cube(t, "SoapDispenser", new Vector3(wx + 0.05f, FY + 1.12f, (sz[k] + sz[k + 1]) * 0.5f), new Vector3(0.09f, 0.16f, 0.07f), 0f, porc, false);
            Cube(t, "PipeRun", new Vector3(wx + 0.07f, FY + 0.14f, (sz[0] + sz[sz.Length - 1]) * 0.5f), new Vector3(0.05f, 0.05f, sz[sz.Length - 1] - sz[0] + 0.5f), 0f, Mt("FO_Gray"), false);
            Cube(t, "TowelDispenser", new Vector3(-26.4f, FY + 1.3f, za + 0.1f), new Vector3(0.3f, 0.42f, 0.14f), 0f, porc, false);
            Cyl(t, "TowelBin", new Vector3(-26.4f, FY + 0.3f, za + 0.32f), 0.36f, 0.6f, 0.36f, Mt("FO_Gray"), true);
            Cube(t, "HandDryer", new Vector3(-25.0f, FY + 1.2f, za + 0.12f), new Vector3(0.28f, 0.26f, 0.2f), 0f, chrome, false);
            Cyl(t, "FloorDrain", new Vector3(-25.0f, FY + 0.012f, 33.0f), 0.2f, 0.012f, 0.2f, dark, false);
            Cyl(t, "WasteBin", new Vector3(xb - 0.35f, FY + 0.3f, 36.2f), 0.34f, 0.6f, 0.34f, Mt("FO_Gray"), true);
            Cyl(t, "MopBucket", new Vector3(xb - 0.45f, FY + 0.14f, za + 0.45f), 0.32f, 0.28f, 0.32f, Mt("FO_Yellow"), true);
            Cyl(t, "MopStick", new Vector3(xb - 0.3f, FY + 0.65f, za + 0.25f), 0.03f, 1.3f, 0.03f, Mt("FO_Key"), false).transform.rotation = Quaternion.Euler(8f, 0f, -6f);
        }
        else
        {
            float sx = xb;
            // urinals along the east wall with privacy dividers
            float[] uz = { 27.0f, 28.3f, 29.6f, 30.9f };
            foreach (float z in uz)
            {
                Cube(t, "Urinal", new Vector3(sx - 0.17f, FY + 0.85f, z), new Vector3(0.34f, 0.58f, 0.36f), 0f, porc, true);
                Cube(t, "UrinalBasin", new Vector3(sx - 0.30f, FY + 0.78f, z), new Vector3(0.1f, 0.34f, 0.24f), 0f, dark, false);
                Cyl(t, "UrinalPipe", new Vector3(sx - 0.05f, FY + 1.45f, z), 0.03f, 0.55f, 0.03f, chrome, false);
                Cyl(t, "UrinalTrap", new Vector3(sx - 0.07f, FY + 0.3f, z), 0.05f, 0.5f, 0.05f, chrome, false);
            }
            for (int k = 0; k <= uz.Length; k++)
            {
                float z = (k == 0) ? uz[0] - 0.65f : (k == uz.Length ? uz[uz.Length - 1] + 0.65f : (uz[k - 1] + uz[k]) * 0.5f);
                Cube(t, "UrinalDivider", new Vector3(sx - 0.19f, FY + 0.95f, z), new Vector3(0.38f, 0.9f, 0.03f), 0f, stall, true);
            }
            // basins along the south wall, mirrors above
            float[] bx = { -14.9f, -13.6f, -12.3f, -11.0f };
            foreach (float x in bx) Basin(t, new Vector3(x, FY, za), 0, 1, false);
            for (int k = 0; k < bx.Length - 1; k++)
                Cube(t, "SoapDispenser", new Vector3((bx[k] + bx[k + 1]) * 0.5f, FY + 1.12f, za + 0.05f), new Vector3(0.07f, 0.16f, 0.09f), 0f, porc, false);
            Cube(t, "PipeRun", new Vector3((bx[0] + bx[bx.Length - 1]) * 0.5f, FY + 0.14f, za + 0.07f), new Vector3(bx[bx.Length - 1] - bx[0] + 0.5f, 0.05f, 0.05f), 0f, Mt("FO_Gray"), false);
            Cube(t, "TowelDispenser", new Vector3(-9.6f, FY + 1.3f, za + 0.1f), new Vector3(0.3f, 0.42f, 0.14f), 0f, porc, false);
            Cyl(t, "TowelBin", new Vector3(-9.6f, FY + 0.3f, za + 0.32f), 0.36f, 0.6f, 0.36f, Mt("FO_Gray"), true);
            Cube(t, "HandDryer", new Vector3(-16.0f, FY + 1.2f, za + 0.12f), new Vector3(0.28f, 0.26f, 0.2f), 0f, chrome, false);
            Cyl(t, "FloorDrain", new Vector3(-12.9f, FY + 0.012f, 29.5f), 0.2f, 0.012f, 0.2f, dark, false);
            Cyl(t, "WasteBin", new Vector3(xa + 0.35f, FY + 0.3f, 35.2f), 0.34f, 0.6f, 0.34f, Mt("FO_Gray"), true);
            Cyl(t, "MopBucket", new Vector3(xb - 0.5f, FY + 0.14f, 35.6f), 0.32f, 0.28f, 0.32f, Mt("FO_Yellow"), true);
            Cyl(t, "MopStick", new Vector3(xb - 0.3f, FY + 0.65f, 35.9f), 0.03f, 1.3f, 0.03f, Mt("FO_Key"), false).transform.rotation = Quaternion.Euler(-8f, 0f, 6f);
        }
    }

    // wall basin. wallAxis: wall normal direction in plan -> (nx, nz) = (1,0): basin against the west wall (x = pos.x), (0,1): against the south wall (z = pos.z)
    static void Basin(Transform t, Vector3 wallPos, int nx, int nz, bool unused)
    {
        Material porc = Mt("FO_Porcelain"), chrome = Mt("FO_Chrome"), mirror = Mt("FO_Mirror"), gray = Mt("FO_Gray");
        Vector3 n = new Vector3(nx, 0f, nz);               // points from the wall into the room
        Vector3 a = new Vector3(-nz, 0f, nx);              // along the wall
        float yaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg;
        Vector3 basinC = wallPos + n * 0.25f + Vector3.up * 0.85f;
        Cube(t, "Basin", basinC, new Vector3(0.5f * Mathf.Abs(nx) + 0.46f * Mathf.Abs(nz), 0.14f, 0.46f * Mathf.Abs(nx) + 0.5f * Mathf.Abs(nz)), 0f, porc, true);
        Cube(t, "BasinInner", basinC + Vector3.up * 0.045f, new Vector3(0.38f * Mathf.Abs(nx) + 0.34f * Mathf.Abs(nz), 0.02f, 0.34f * Mathf.Abs(nx) + 0.38f * Mathf.Abs(nz)), 0f, Mt("FO_Dark"), false);
        Cyl(t, "Faucet", wallPos + n * 0.12f + Vector3.up * 0.97f, 0.03f, 0.12f, 0.03f, chrome, false);
        Cube(t, "Spout", wallPos + n * 0.2f + Vector3.up * 1.02f, new Vector3(0.03f * Mathf.Abs(nx) + 0.03f * Mathf.Abs(nz), 0.03f, 0.03f), 0f, chrome, false);
        Cyl(t, "Drain", wallPos + n * 0.08f + Vector3.up * 0.4f, 0.04f, 0.8f, 0.04f, gray, false);
        Cube(t, "Mirror", wallPos + n * 0.015f + Vector3.up * 1.55f, new Vector3(0.03f * Mathf.Abs(nx) + 0.5f * Mathf.Abs(nz), 0.8f, 0.5f * Mathf.Abs(nx) + 0.03f * Mathf.Abs(nz)), 0f, mirror, false);
        Cube(t, "MirrorFrame", wallPos + n * 0.008f + Vector3.up * 1.55f, new Vector3(0.015f * Mathf.Abs(nx) + 0.56f * Mathf.Abs(nz), 0.86f, 0.56f * Mathf.Abs(nx) + 0.015f * Mathf.Abs(nz)), 0f, Mt("FO_Gray"), false);
    }

    // ------------------------------------------------------------ doors for every doorway
    public static string Doors()
    {
        if (EditorApplication.isPlaying) return "Editor is in play mode - aborting";
        Init(530);
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        Material doorM = Mt("School_Door");
        Transform ajar = GameObject.Find("School/AjarDoors") != null ? GameObject.Find("School/AjarDoors").transform : null;
        if (ajar == null) { ajar = new GameObject("GroundFloorDoors").transform; }

        // ground floor: the four classroom doorways that were left open frames (z = 5/15/25/35, both corridor walls)
        string[] names = { "Door_L1", "Door_L3", "Door_R0", "Door_R2" };
        float[] xs = { -2f, -2f, 2f, 2f };
        float[] zs = { 14.4f, 34.4f, 4.4f, 24.4f };
        for (int i = 0; i < names.Length; i++)
        {
            if (GameObject.Find(names[i]) != null) continue;
            float side = xs[i] < 0 ? -1f : 1f;
            MakeDoor(ajar, names[i], new Vector3(xs[i], 0f, zs[i]), 0f, 1.1f, 2.1f, 0.04f, side * 75f, false, doorM, true);
            sb.AppendLine("added " + names[i]);
        }

        // second floor: Classroom_2B had hinge + handles but no door leaf
        GameObject d2b = GameObject.Find("SecondFloorDoor_Classroom_2B");
        if (d2b != null && d2b.transform.Find("SecondFloorDoor_Classroom_2B_Leaf") == null)
        {
            GameObject src = GameObject.Find("SecondFloorDoor_Classroom_2A_Leaf");
            float width = src != null ? src.transform.localScale.z : 1.32f;
            GameObject leaf = Leaf(d2b.transform, "SecondFloorDoor_Classroom_2B_Leaf", new Vector3(0f, 1.04f, width * 0.5f), new Vector3(0.06f, 2.08f, width), doorM, true);
            leaf.transform.SetAsFirstSibling();
            SchoolDoor sd = d2b.GetComponent<SchoolDoor>();
            if (sd != null) { sd.leaf = leaf.transform; sd.openAngle = 85f; sd.rotationSpeed = 360f; EditorUtility.SetDirty(sd); }
            sb.AppendLine("added leaf for SecondFloorDoor_Classroom_2B (width " + width.ToString("F2") + ")");
        }

        // make sure every SchoolDoor knows its leaf
        foreach (SchoolDoor d in Object.FindObjectsByType<SchoolDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (d.leaf != null) continue;
            foreach (Transform c in d.transform) if (c.GetComponent<Collider>() != null) { d.leaf = c; EditorUtility.SetDirty(d); break; }
        }
        sb.AppendLine("total SchoolDoor components: " + Object.FindObjectsByType<SchoolDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
        return sb.ToString();
    }

    // ------------------------------------------------------------ validation
    public static string Validate()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        Physics.SyncTransforms();
        GameObject rootGO = GameObject.Find("SecondFloorFitout");
        if (rootGO == null) return "no fitout root";
        Transform root = rootGO.transform;

        // 1) pieces that stick into walls / other geometry, or sink below the floor
        int penetrating = 0, sunk = 0, checkedCount = 0;
        Dictionary<string, int> hitNames = new Dictionary<string, int>();
        foreach (Collider c in root.GetComponentsInChildren<Collider>())
        {
            if (c.transform.IsChildOf(root) == false) continue;
            if (c.GetComponentInParent<SchoolDoor>() != null) continue;      // door leaves are swept separately
            BoxCollider bc = c as BoxCollider;
            if (bc == null) continue;
            checkedCount++;
            Bounds b = c.bounds;
            if (b.min.y < FY - 0.03f) sunk++;
            Vector3 half = Vector3.Max(Vector3.zero, c.transform.lossyScale * 0.5f - new Vector3(0.012f, 0.012f, 0.012f));
            Collider[] hits = Physics.OverlapBox(c.transform.position, half, c.transform.rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool bad = false;
            foreach (Collider h in hits)
            {
                if (h == c || h.transform.IsChildOf(root)) continue;
                string n = h.name;
                if (n.Contains("Floor") || n.Contains("Panel") && n.Contains("East")) continue;   // resting on the floor slab is fine
                bad = true;
                int k; hitNames.TryGetValue(n, out k); hitNames[n] = k + 1;
            }
            if (bad) penetrating++;
        }
        sb.AppendLine("box colliders checked=" + checkedCount + " penetrating other geometry=" + penetrating + " below floor=" + sunk);
        foreach (KeyValuePair<string, int> kv in hitNames) sb.AppendLine("   hit " + kv.Key + " x" + kv.Value);

        // 2) walk test on the second floor with all door leaves removed from the test
        List<Collider> leaves = new List<Collider>();
        foreach (SchoolDoor d in Object.FindObjectsByType<SchoolDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (d.leaf == null) continue;
            Collider lc = d.leaf.GetComponent<Collider>();
            if (lc != null && lc.enabled && d.transform.position.y > 3f) { leaves.Add(lc); lc.enabled = false; }
        }
        Physics.SyncTransforms();
        float step = 0.2f, x0 = -29f, x1 = -8.8f, z0 = 0.2f, z1 = 40f;
        int nx = Mathf.RoundToInt((x1 - x0) / step) + 1, nz = Mathf.RoundToInt((z1 - z0) / step) + 1;
        bool[,] blocked = new bool[nx, nz];
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
                blocked[i, j] = Physics.CheckCapsule(new Vector3(x0 + i * step, FY + 0.45f, z0 + j * step), new Vector3(x0 + i * step, FY + 1.5f, z0 + j * step), 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        int[,] dist = new int[nx, nz];
        for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++) dist[i, j] = -1;
        System.Func<float, float, int[]> cell = (x, z) => new int[] { Mathf.RoundToInt((x - x0) / step), Mathf.RoundToInt((z - z0) / step) };
        int[] s = cell(-18.9f, 14f);
        Queue<int[]> q = new Queue<int[]>();
        if (!blocked[s[0], s[1]]) { dist[s[0], s[1]] = 0; q.Enqueue(s); }
        while (q.Count > 0)
        {
            int[] c = q.Dequeue();
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int ax = c[0] + dx, az = c[1] + dz;
                    if (ax < 0 || az < 0 || ax >= nx || az >= nz || blocked[ax, az] || dist[ax, az] >= 0) continue;
                    dist[ax, az] = dist[c[0], c[1]] + 1;
                    q.Enqueue(new int[] { ax, az });
                }
        }
        string[] goalNames = { "StaffWest aisle", "StaffWest near cluster", "StaffEast aisle", "StaffEast far end", "Women entry", "Women sinks", "Women stall 1 (inside)", "Women stall 5 (inside)", "Men entry", "Men urinals", "Men stall 1 (inside)", "Men stall 3 (inside)", "Classroom 2A", "Classroom 2B", "Storage" };
        float[][] goals = {
            new float[] { -22.2f, 16.0f }, new float[] { -23.0f, 16.0f }, new float[] { -15.0f, 18.5f }, new float[] { -10.2f, 18.5f },
            new float[] { -21.9f, 34.0f }, new float[] { -27.2f, 31.0f }, new float[] { -28.27f, 39.0f }, new float[] { -24.27f, 39.0f },
            new float[] { -15.8f, 32.5f }, new float[] { -10.3f, 29.0f }, new float[] { -16.27f, 39.0f }, new float[] { -14.27f, 39.0f },
            new float[] { -24.0f, 5.0f }, new float[] { -13.0f, 5.0f }, new float[] { -25.0f, 24.0f } };
        for (int g = 0; g < goals.Length; g++)
        {
            int[] gc = cell(goals[g][0], goals[g][1]);
            bool ok = !blocked[gc[0], gc[1]] && dist[gc[0], gc[1]] >= 0;
            sb.AppendLine((ok ? "OK    " : "BLOCK ") + goalNames[g] + (blocked[gc[0], gc[1]] ? "  (spot itself occupied)" : ""));
        }
        foreach (Collider lc in leaves) lc.enabled = true;
        Physics.SyncTransforms();
        return sb.ToString();
    }
}
