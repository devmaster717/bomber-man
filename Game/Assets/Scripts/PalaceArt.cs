using System.Collections.Generic;
using BombArena.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The look shared by the 3D view and the 2D sprites rendered from it: materials (CC0 textures from ambientCG),
/// bevelled meshes, and the models for blocks, bombs, bombers, enemies, power-ups and the exit, plus flame and sparkle
/// effects. Blocks, the exit and the lighting follow <see cref="ArenaTheme.Current"/> (palace, fortress, garden or
/// frozen citadel); the characters, bombs and power-ups keep their royal look in every theme. Holds no rules.
/// Model space: one unit per tile, standing on y = 0, facing +z.
/// </summary>
public static class PalaceArt
{
    // ---- palette ----

    public static readonly Color Navy = new Color(0.05f, 0.07f, 0.14f);
    public static readonly Color GoldText = new Color(0.94f, 0.80f, 0.45f);

    /// <summary>Each player slot's enamel colour (body and jewel), matching the 2D sprites' slot colours.</summary>
    public static Color SlotColour(int slot) => slot switch
    {
        1 => new Color(0.78f, 0.12f, 0.14f), // ruby
        2 => new Color(0.12f, 0.30f, 0.80f), // sapphire
        _ => new Color(0.93f, 0.93f, 0.95f), // pearl
    };

    private static Color SlotTrim(int slot) => slot switch
    {
        1 => new Color(0.25f, 0.05f, 0.06f),
        2 => new Color(0.05f, 0.09f, 0.28f),
        _ => new Color(0.16f, 0.22f, 0.55f),
    };

    // ---- materials ----

    private static readonly Dictionary<string, Material> Loaded = new Dictionary<string, Material>();
    private static readonly Dictionary<(string, Color), Material> Tinted = new Dictionary<(string, Color), Material>();

    /// <summary>A material asset from Resources/Palace/Materials (Floor, Marble, Stone, Wood, Gold, Carpet, ...).</summary>
    public static Material Mat(string name)
    {
        if (!Loaded.TryGetValue(name, out var m) || m == null)
        {
            m = Resources.Load<Material>("Palace/Materials/" + name);
            if (m == null) Debug.LogError("Palace material missing: " + name);
            Loaded[name] = m;
        }
        return m;
    }

    /// <summary>A copy of a material asset with another base colour; colours above 1 bloom on glow materials.</summary>
    public static Material Tint(string name, Color colour)
    {
        if (!Tinted.TryGetValue((name, colour), out var m) || m == null)
        {
            m = new Material(Mat(name)) { name = name + " " + colour };
            m.SetColor("_BaseColor", colour);
            Tinted[(name, colour)] = m;
        }
        return m;
    }

    public static Material Glossy(Color c) => Tint("Glossy", c);
    public static Material Matte(Color c) => Tint("Matte", c);
    public static Material Glow(Color c) => Tint("Glow", c);

    /// <summary>An additive glow material showing a texture (power-up icons).</summary>
    public static Material Additive(Texture texture, Color colour)
    {
        var key = ("SoftGlow#" + texture.GetInstanceID(), colour);
        if (!Tinted.TryGetValue(key, out var m) || m == null)
        {
            m = new Material(Mat("SoftGlow"));
            m.SetTexture("_BaseMap", texture);
            m.SetColor("_BaseColor", colour);
            Tinted[key] = m;
        }
        return m;
    }

    // ---- meshes ----

    private static readonly Dictionary<PrimitiveType, Mesh> Primitives = new Dictionary<PrimitiveType, Mesh>();
    private static readonly Dictionary<(Vector3, float), Mesh> Boxes = new Dictionary<(Vector3, float), Mesh>();

    public static Mesh Primitive(PrimitiveType type)
    {
        if (!Primitives.TryGetValue(type, out var mesh) || mesh == null)
        {
            var go = GameObject.CreatePrimitive(type);
            mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Remove(go);
            Primitives[type] = mesh;
        }
        return mesh;
    }

    /// <summary>
    /// A box with chamfered edges, so its edges catch the light. Each face, edge strip and corner is flat-shaded, and
    /// textures are projected along each facet's main axis, one copy per face.
    /// </summary>
    public static Mesh ChamferBox(Vector3 size, float bevel)
    {
        if (Boxes.TryGetValue((size, bevel), out var cached) && cached != null) return cached;
        var h = size / 2f;
        bevel = Mathf.Min(bevel, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.9f);
        var verts = new List<Vector3>();
        var tris = new List<int>();
        var uvs = new List<Vector2>();
        var normals = new List<Vector3>();

        void Facet(params Vector3[] p)
        {
            var n = Vector3.Cross(p[1] - p[0], p[2] - p[0]).normalized;
            var centre = Vector3.zero;
            foreach (var v in p) centre += v;
            centre /= p.Length;
            if (Vector3.Dot(n, centre) < 0f) { System.Array.Reverse(p); n = -n; }
            int axis = Mathf.Abs(n.x) > Mathf.Abs(n.y) ? (Mathf.Abs(n.x) > Mathf.Abs(n.z) ? 0 : 2) : (Mathf.Abs(n.y) > Mathf.Abs(n.z) ? 1 : 2);
            int start = verts.Count;
            foreach (var v in p)
            {
                verts.Add(v);
                normals.Add(n);
                // Project onto the face's plane, scaled so a face shows the texture once.
                var q = new Vector3((v.x + h.x) / size.x, (v.y + h.y) / size.y, (v.z + h.z) / size.z);
                uvs.Add(axis == 0 ? new Vector2(n.x > 0 ? 1 - q.z : q.z, q.y)
                      : axis == 1 ? new Vector2(q.x, n.y > 0 ? q.z : 1 - q.z)
                      : new Vector2(n.z > 0 ? q.x : 1 - q.x, q.y));
            }
            // Unity's front faces are those whose (b - a) x (c - a) points outwards.
            for (int i = 1; i + 1 < p.Length; i++) { tris.Add(start); tris.Add(start + i); tris.Add(start + i + 1); }
        }

        float ix = h.x - bevel, iy = h.y - bevel, iz = h.z - bevel;
        foreach (int s in new[] { -1, 1 })
        {
            // Faces.
            Facet(new Vector3(s * h.x, -iy, -iz), new Vector3(s * h.x, iy, -iz), new Vector3(s * h.x, iy, iz), new Vector3(s * h.x, -iy, iz));
            Facet(new Vector3(-ix, s * h.y, -iz), new Vector3(ix, s * h.y, -iz), new Vector3(ix, s * h.y, iz), new Vector3(-ix, s * h.y, iz));
            Facet(new Vector3(-ix, -iy, s * h.z), new Vector3(ix, -iy, s * h.z), new Vector3(ix, iy, s * h.z), new Vector3(-ix, iy, s * h.z));
        }
        foreach (int a in new[] { -1, 1 })
        foreach (int b in new[] { -1, 1 })
        {
            // Edge strips: along z (x,y signs), along x (y,z signs), along y (x,z signs).
            Facet(new Vector3(a * h.x, b * iy, -iz), new Vector3(a * ix, b * h.y, -iz), new Vector3(a * ix, b * h.y, iz), new Vector3(a * h.x, b * iy, iz));
            Facet(new Vector3(-ix, a * h.y, b * iz), new Vector3(ix, a * h.y, b * iz), new Vector3(ix, a * iy, b * h.z), new Vector3(-ix, a * iy, b * h.z));
            Facet(new Vector3(a * h.x, -iy, b * iz), new Vector3(a * h.x, iy, b * iz), new Vector3(a * ix, iy, b * h.z), new Vector3(a * ix, -iy, b * h.z));
            foreach (int c in new[] { -1, 1 })
                Facet(new Vector3(a * h.x, b * iy, c * iz), new Vector3(a * ix, b * h.y, c * iz), new Vector3(a * ix, b * iy, c * h.z));
        }

        var mesh = new Mesh { name = $"Chamfer {size} {bevel}" };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        Boxes[(size, bevel)] = mesh;
        return mesh;
    }

    // ---- building blocks ----

    public static GameObject Part(Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale, bool shadows = true)
    {
        var go = new GameObject(mesh.name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        return go;
    }

    private static GameObject Box(Transform parent, Vector3 size, float bevel, Material m, Vector3 position, bool shadows = true) =>
        Part(parent, ChamferBox(size, bevel), m, position, Vector3.one, shadows);

    private static GameObject Ball(Transform parent, Material m, Vector3 position, Vector3 scale, bool shadows = true) =>
        Part(parent, Primitive(PrimitiveType.Sphere), m, position, scale, shadows);

    private static GameObject Disc(Transform parent, Material m, Vector3 position, Vector3 scale, bool shadows = true) =>
        Part(parent, Primitive(PrimitiveType.Cylinder), m, position, scale, shadows);

    private static Transform Group(string name, Transform parent)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    public static void Remove(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Object.Destroy(o);
        else Object.DestroyImmediate(o);
    }

    // ---- arena pieces ----

    /// <summary>Height of the tallest arena piece, for cameras.</summary>
    public const float WallHeight = 0.92f;

    /// <summary>The theme's metal trim: gold, bronze, dark wood or silver.</summary>
    public static Material Trim() => ArenaTheme.Current.Key switch
    {
        "fortress" => Mat("Bronze"),
        "garden" => Tint("Wood", new Color(0.66f, 0.48f, 0.34f)),
        "frozen" => Mat("Silver"),
        _ => Mat("Gold"),
    };

    /// <summary>An inner pillar, which can't be broken.</summary>
    public static Transform Pillar(Transform parent)
    {
        var t = Group("Pillar", parent);
        switch (ArenaTheme.Current.Key)
        {
            case "fortress": // a red lacquered column on a stone base, banded in bronze
                Box(t, new Vector3(0.9f, 0.12f, 0.9f), 0.03f, Tint("Paving", new Color(0.8f, 0.78f, 0.75f)), new Vector3(0, 0.06f, 0));
                Disc(t, Mat("Lacquer"), new Vector3(0, 0.45f, 0), new Vector3(0.62f, 0.33f, 0.62f));
                Disc(t, Mat("Bronze"), new Vector3(0, 0.2f, 0), new Vector3(0.66f, 0.025f, 0.66f));
                Disc(t, Mat("Bronze"), new Vector3(0, 0.78f, 0), new Vector3(0.7f, 0.03f, 0.7f));
                // A little tiled cap, as on a gate tower's columns.
                Box(t, new Vector3(0.84f, 0.08f, 0.84f), 0.03f, Tint("Roof", new Color(0.62f, 0.58f, 0.58f)), new Vector3(0, 0.84f, 0));
                Box(t, new Vector3(0.36f, 0.06f, 0.36f), 0.02f, Mat("Bronze"), new Vector3(0, 0.9f, 0));
                break;
            case "garden": // a scholar's rock, weathered and lumpy, on a bed of moss
                Ball(t, Tint("Lawn", new Color(0.55f, 0.7f, 0.45f)), new Vector3(0, 0.03f, 0), new Vector3(0.92f, 0.1f, 0.92f), false);
                var rock = Mat("Rock");
                Ball(t, rock, new Vector3(0.02f, 0.24f, 0), new Vector3(0.78f, 0.5f, 0.66f)).transform.localRotation = Quaternion.Euler(0f, 30f, 8f);
                Ball(t, rock, new Vector3(0.1f, 0.52f, 0.02f), new Vector3(0.46f, 0.62f, 0.4f)).transform.localRotation = Quaternion.Euler(10f, 0f, 22f);
                Ball(t, rock, new Vector3(-0.12f, 0.74f, -0.04f), new Vector3(0.36f, 0.42f, 0.3f)).transform.localRotation = Quaternion.Euler(-8f, 40f, -28f);
                Ball(t, rock, new Vector3(-0.24f, 0.36f, 0.12f), new Vector3(0.34f, 0.4f, 0.3f)).transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
                break;
            case "frozen": // ice crystals rising from a snow drift
                Ball(t, Mat("Snow"), new Vector3(0, 0.04f, 0), new Vector3(0.94f, 0.2f, 0.94f));
                var ice = Tint("Ice", new Color(0.78f, 0.92f, 1.1f));
                Box(t, new Vector3(0.46f, 0.82f, 0.46f), 0.06f, ice, new Vector3(0, 0.45f, 0)).transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                Box(t, new Vector3(0.24f, 0.5f, 0.24f), 0.04f, ice, new Vector3(0.24f, 0.28f, 0.16f)).transform.localRotation = Quaternion.Euler(0f, 20f, -18f);
                break;
            default: // black marble on a gold plinth, with a gold capital and a marble inlay on top
                var gold = Mat("Gold");
                Box(t, new Vector3(0.96f, 0.08f, 0.96f), 0.025f, gold, new Vector3(0, 0.04f, 0));
                Box(t, new Vector3(0.84f, 0.66f, 0.84f), 0.05f, Mat("Marble"), new Vector3(0, 0.41f, 0));
                Box(t, new Vector3(0.96f, 0.08f, 0.96f), 0.025f, gold, new Vector3(0, 0.78f, 0));
                Box(t, new Vector3(0.62f, 0.04f, 0.62f), 0.015f, Mat("Marble"), new Vector3(0, 0.83f, 0));
                break;
        }
        return t;
    }

    /// <summary>The outer wall.</summary>
    public static Transform Wall(Transform parent)
    {
        var t = Group("Wall", parent);
        switch (ArenaTheme.Current.Key)
        {
            case "fortress": // grey brick rampart under a stone coping
                Box(t, new Vector3(1f, 0.84f, 1f), 0.03f, Mat("Brick"), new Vector3(0, 0.42f, 0));
                Box(t, new Vector3(1.04f, 0.08f, 1.04f), 0.02f, Tint("Paving", new Color(0.62f, 0.6f, 0.58f)), new Vector3(0, 0.86f, 0));
                break;
            case "garden": // white plaster under a grey tiled cap
                Box(t, new Vector3(1f, 0.76f, 1f), 0.03f, Mat("Plaster"), new Vector3(0, 0.38f, 0));
                Box(t, new Vector3(1.08f, 0.1f, 1.08f), 0.03f, Tint("Roof", new Color(0.9f, 0.92f, 1f)), new Vector3(0, 0.81f, 0));
                Box(t, new Vector3(0.5f, 0.04f, 1.02f), 0.015f, Tint("Roof", new Color(0.62f, 0.64f, 0.7f)), new Vector3(0, 0.88f, 0));
                break;
            case "frozen": // frosted stone under a thick cap of snow
                Box(t, new Vector3(1f, 0.82f, 1f), 0.03f, Tint("Brick", new Color(0.76f, 0.86f, 1f)), new Vector3(0, 0.41f, 0));
                Box(t, new Vector3(1.05f, 0.12f, 1.05f), 0.05f, Mat("Snow"), new Vector3(0, 0.84f, 0));
                break;
            default: // pale palace stone with a slim gold trim below the top
                Box(t, new Vector3(1f, 0.9f, 1f), 0.03f, Mat("Stone"), new Vector3(0, 0.45f, 0));
                Box(t, new Vector3(1.03f, 0.05f, 1.03f), 0.015f, Mat("Gold"), new Vector3(0, 0.76f, 0));
                break;
        }
        return t;
    }

    /// <summary>A breakable block.</summary>
    public static Transform Crate(Transform parent)
    {
        var t = Group("Crate", parent);
        switch (ArenaTheme.Current.Key)
        {
            case "fortress": // a supply crate of dark timber bound in bronze
                BoundCrate(t, Tint("Wood", new Color(0.95f, 0.84f, 0.7f)), Mat("Bronze"));
                break;
            case "garden": // a bamboo planter holding a round, flowering shrub
                Box(t, new Vector3(0.84f, 0.36f, 0.84f), 0.03f, Mat("Bamboo"), new Vector3(0, 0.18f, 0));
                Box(t, new Vector3(0.88f, 0.05f, 0.88f), 0.015f, Trim(), new Vector3(0, 0.36f, 0));
                var leaves = Tint("Lawn", new Color(0.46f, 0.6f, 0.4f));
                Ball(t, leaves, new Vector3(0, 0.56f, 0), new Vector3(0.8f, 0.5f, 0.8f));
                Ball(t, leaves, new Vector3(0.14f, 0.7f, -0.1f), new Vector3(0.46f, 0.38f, 0.46f));
                Ball(t, leaves, new Vector3(-0.16f, 0.68f, 0.12f), new Vector3(0.4f, 0.32f, 0.4f));
                // Blossoms scattered over the shrub: two shades, uneven, so they read as flowers rather than eyes.
                var pink = Matte(new Color(0.98f, 0.5f, 0.66f));
                var coral = Matte(new Color(1f, 0.66f, 0.5f));
                var blooms = new[]
                {
                    new Vector3(-0.3f, 0.6f, 0.1f), new Vector3(0.26f, 0.62f, 0.2f), new Vector3(0.05f, 0.82f, 0.18f),
                    new Vector3(-0.08f, 0.78f, -0.24f), new Vector3(0.3f, 0.74f, -0.14f), new Vector3(-0.2f, 0.84f, 0.02f),
                    new Vector3(0.12f, 0.66f, 0.32f), new Vector3(-0.28f, 0.62f, -0.2f),
                };
                for (int i = 0; i < blooms.Length; i++)
                    Ball(t, i % 3 == 2 ? coral : pink, blooms[i], Vector3.one * (i % 2 == 0 ? 0.075f : 0.06f), false);
                break;
            case "frozen": // a frosted crate under a slab of snow, its silver corners showing
                BoundCrate(t, Tint("Wood", new Color(0.78f, 0.84f, 0.95f)), Mat("Silver"));
                Box(t, new Vector3(0.56f, 0.06f, 0.56f), 0.03f, Mat("Snow"), new Vector3(0, 0.77f, 0));
                break;
            default: // wooden planks bound with gold corners and a gold band
                BoundCrate(t, Mat("Wood"), Mat("Gold"));
                break;
        }
        return t;
    }

    private static void BoundCrate(Transform t, Material wood, Material metal)
    {
        Box(t, new Vector3(0.84f, 0.74f, 0.84f), 0.04f, wood, new Vector3(0, 0.37f, 0));
        foreach (int sx in new[] { -1, 1 })
        foreach (int sz in new[] { -1, 1 })
            Box(t, new Vector3(0.11f, 0.76f, 0.11f), 0.02f, metal, new Vector3(sx * 0.41f, 0.38f, sz * 0.41f));
        Box(t, new Vector3(0.86f, 0.07f, 0.86f), 0.015f, metal, new Vector3(0, 0.62f, 0));
        Box(t, new Vector3(0.86f, 0.07f, 0.86f), 0.015f, metal, new Vector3(0, 0.12f, 0));
    }

    /// <summary>A polished black bomb with a gold band and cap; returns the body for tinting remote bombs.</summary>
    public static Transform Bomb(Transform parent, out Renderer body)
    {
        var t = Group("Bomb", parent);
        var gold = Mat("Gold");
        body = Ball(t, Glossy(new Color(0.04f, 0.04f, 0.05f)), new Vector3(0, 0.37f, 0), Vector3.one * 0.72f).GetComponent<Renderer>();
        Disc(t, gold, new Vector3(0, 0.37f, 0), new Vector3(0.735f, 0.03f, 0.735f));
        Disc(t, gold, new Vector3(0, 0.72f, 0), new Vector3(0.2f, 0.035f, 0.2f));
        Disc(t, Tint("Matte", new Color(0.85f, 0.78f, 0.6f)), new Vector3(0, 0.8f, 0), new Vector3(0.05f, 0.06f, 0.05f));
        Ball(t, Glow(new Color(4f, 2.2f, 0.6f)), new Vector3(0, 0.88f, 0), Vector3.one * 0.09f, false);
        return t;
    }

    public static Material BombMaterial(bool remote) =>
        Glossy(remote ? new Color(0.35f, 0.03f, 0.04f) : new Color(0.04f, 0.04f, 0.05f));

    /// <summary>The exit: a ring of the theme's trim set in the floor; dark stone inside while shut, a glowing portal once open.</summary>
    public static Transform Exit(Transform parent, out Renderer inner)
    {
        var t = Group("Exit", parent);
        Disc(t, Trim(), new Vector3(0, 0.02f, 0), new Vector3(0.9f, 0.02f, 0.9f), false);
        inner = Disc(t, ExitInner(false), new Vector3(0, 0.03f, 0), new Vector3(0.72f, 0.02f, 0.72f), false).GetComponent<Renderer>();
        return t;
    }

    public static Material ExitInner(bool open) => open ? Glow(new Color(1.2f, 2.2f, 2.6f)) : ArenaTheme.Current.Key switch
    {
        "fortress" => Tint("Paving", new Color(0.3f, 0.28f, 0.27f)),
        "garden" => Tint("Rock", new Color(0.45f, 0.5f, 0.5f)),
        "frozen" => Tint("Ice", new Color(0.25f, 0.4f, 0.7f)),
        _ => Mat("Marble"),
    };

    /// <summary>A power-up: an enamel tile in a gold frame with a glowing white symbol.</summary>
    public static Transform PowerUp(Transform parent, PowerUpKind kind)
    {
        var t = Group("Power-up " + kind, parent);
        var enamel = kind switch
        {
            PowerUpKind.FireUp => new Color(0.75f, 0.18f, 0.05f),
            PowerUpKind.BombUp => new Color(0.08f, 0.2f, 0.65f),
            PowerUpKind.RemoteControl => new Color(0.42f, 0.1f, 0.6f),
            _ => new Color(0.05f, 0.5f, 0.2f),
        };
        Box(t, new Vector3(0.66f, 0.07f, 0.66f), 0.02f, Mat("Gold"), Vector3.zero);
        Box(t, new Vector3(0.54f, 0.08f, 0.54f), 0.01f, Glossy(enamel), Vector3.zero);
        var icon = Part(t, Primitive(PrimitiveType.Quad), Additive(PowerUpIcons.For(kind), new Color(1.6f, 1.5f, 1.3f)),
            new Vector3(0, 0.05f, 0), Vector3.one * 0.46f, false);
        icon.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        return t;
    }

    // ---- characters ----

    /// <summary>A royal bomber's moving parts, posed each frame by <see cref="Pose"/>.</summary>
    public sealed class BomberRig
    {
        public Transform Root, Body, LeftFoot, RightFoot, LeftHand, RightHand;
        private float _phase;

        /// <summary>Walk cycle: bob, swing feet and hands while moving; breathe when still.</summary>
        public void Pose(float distanceMoved, float time)
        {
            _phase += distanceMoved * 9f;
            bool moving = distanceMoved > 1e-4f;
            float swing = moving ? Mathf.Sin(_phase) : 0f;
            float bob = moving ? Mathf.Abs(Mathf.Sin(_phase)) * 0.05f : Mathf.Sin(time * 2.2f) * 0.008f;
            Body.localPosition = new Vector3(0, bob, 0);
            LeftFoot.localPosition = new Vector3(-0.11f, 0.05f, 0.02f + swing * 0.09f);
            RightFoot.localPosition = new Vector3(0.11f, 0.05f, 0.02f - swing * 0.09f);
            LeftHand.localPosition = new Vector3(-0.25f, 0.33f, 0.04f - swing * 0.08f);
            RightHand.localPosition = new Vector3(0.25f, 0.33f, 0.04f + swing * 0.08f);
        }
    }

    /// <summary>
    /// A royal bomber: an enamel body in the slot's colour, a pearl-white helmet with a dark visor and glowing eyes,
    /// gold belt and crown with the slot's jewel.
    /// </summary>
    public static BomberRig Bomber(Transform parent, int slot)
    {
        var rig = new BomberRig { Root = Group("Bomber " + slot, parent) };
        var gold = Mat("Gold");
        var pearl = Glossy(new Color(0.96f, 0.95f, 0.92f));
        var trim = Glossy(SlotTrim(slot));
        rig.LeftFoot = Box(rig.Root, new Vector3(0.16f, 0.1f, 0.24f), 0.04f, trim, Vector3.zero).transform;
        rig.RightFoot = Box(rig.Root, new Vector3(0.16f, 0.1f, 0.24f), 0.04f, trim, Vector3.zero).transform;

        rig.Body = Group("Body", rig.Root);
        var b = rig.Body;
        Ball(b, Glossy(SlotColour(slot)), new Vector3(0, 0.34f, 0), new Vector3(0.44f, 0.42f, 0.4f));
        Disc(b, gold, new Vector3(0, 0.3f, 0), new Vector3(0.45f, 0.03f, 0.41f));
        rig.LeftHand = Ball(b, pearl, Vector3.zero, Vector3.one * 0.13f).transform;
        rig.RightHand = Ball(b, pearl, Vector3.zero, Vector3.one * 0.13f).transform;
        Ball(b, pearl, new Vector3(0, 0.74f, 0), Vector3.one * 0.46f);
        Ball(b, Glossy(new Color(0.03f, 0.04f, 0.08f)), new Vector3(0, 0.72f, 0.12f), new Vector3(0.34f, 0.25f, 0.24f));
        var eye = Glow(new Color(2.2f, 2.2f, 2.4f));
        Ball(b, eye, new Vector3(-0.065f, 0.735f, 0.235f), new Vector3(0.05f, 0.09f, 0.03f), false);
        Ball(b, eye, new Vector3(0.065f, 0.735f, 0.235f), new Vector3(0.05f, 0.09f, 0.03f), false);
        // Crown: a gold band with five points and the slot's jewel in front.
        Disc(b, gold, new Vector3(0, 0.98f, 0), new Vector3(0.27f, 0.035f, 0.27f));
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.PI * 2f / 5f;
            Ball(b, gold, new Vector3(Mathf.Sin(a) * 0.12f, 1.04f, Mathf.Cos(a) * 0.12f), new Vector3(0.05f, 0.09f, 0.05f));
        }
        Ball(b, Glow(SlotJewel(slot)), new Vector3(0, 0.99f, 0.13f), Vector3.one * 0.055f, false);
        rig.Pose(0f, 0f);
        return rig;
    }

    private static Color SlotJewel(int slot) => slot switch
    {
        1 => new Color(2.4f, 0.2f, 0.3f),
        2 => new Color(0.3f, 0.7f, 2.6f),
        _ => new Color(0.4f, 2.0f, 0.9f),
    };

    /// <summary>An enemy's parts, posed each frame by <see cref="Pose"/>; the Phantom fades.</summary>
    public sealed class EnemyRig
    {
        public Transform Root, Body;
        public EnemyKind Kind;
        public readonly List<(Renderer renderer, Color colour)> Fading = new List<(Renderer, Color)>();
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private float _seed;

        public void Pose(float time)
        {
            float t = time * (Kind == EnemyKind.Runner ? 14f : 7f) + _seed;
            switch (Kind)
            {
                case EnemyKind.Walker: // a wobbling jelly
                    float s = Mathf.Sin(t) * 0.1f;
                    Body.localScale = new Vector3(1f - s * 0.5f, 1f + s, 1f - s * 0.5f);
                    break;
                case EnemyKind.Runner: // quick hops
                    Body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(t)) * 0.08f, 0);
                    break;
                default: // hovering
                    Body.localPosition = new Vector3(0, 0.04f + Mathf.Sin(time * 2.5f + _seed) * 0.04f, 0);
                    break;
            }
        }

        public void SetOpacity(float opacity)
        {
            foreach (var (r, c) in Fading)
            {
                _block.SetColor("_BaseColor", new Color(c.r, c.g, c.b, c.a * opacity));
                r.SetPropertyBlock(_block);
            }
        }

        public void Seed(int i) => _seed = i * 1.7f;
    }

    /// <summary>
    /// Enemies, each with its own shape: Walker a purple jelly, Runner a horned red imp, Wall-passer a jade golem,
    /// Phantom a see-through ghost. Like every model they face +z; the views turn them to look at the camera.
    /// </summary>
    public static EnemyRig Enemy(Transform parent, EnemyKind kind)
    {
        var rig = new EnemyRig { Root = Group(kind.ToString(), parent), Kind = kind };
        rig.Body = Group("Body", rig.Root);
        var b = rig.Body;
        switch (kind)
        {
            case EnemyKind.Walker:
                Ball(b, Glossy(new Color(0.42f, 0.12f, 0.62f)), new Vector3(0, 0.27f, 0), new Vector3(0.74f, 0.54f, 0.74f));
                Eyes(b, 0.36f, 0.3f, Glossy(Color.white), Glossy(new Color(0.03f, 0.03f, 0.03f)));
                break;
            case EnemyKind.Runner:
                Ball(b, Glossy(new Color(0.75f, 0.08f, 0.06f)), new Vector3(0, 0.36f, 0), new Vector3(0.5f, 0.62f, 0.5f));
                var horn = Mat("Gold");
                Ball(b, horn, new Vector3(-0.13f, 0.68f, 0), new Vector3(0.07f, 0.18f, 0.07f)).transform.localRotation = Quaternion.Euler(0, 0, 25f);
                Ball(b, horn, new Vector3(0.13f, 0.68f, 0), new Vector3(0.07f, 0.18f, 0.07f)).transform.localRotation = Quaternion.Euler(0, 0, -25f);
                var glare = Glow(new Color(2.6f, 1.9f, 0.3f));
                Ball(b, glare, new Vector3(-0.09f, 0.46f, 0.22f), new Vector3(0.09f, 0.06f, 0.04f), false);
                Ball(b, glare, new Vector3(0.09f, 0.46f, 0.22f), new Vector3(0.09f, 0.06f, 0.04f), false);
                break;
            case EnemyKind.WallPasser:
                Box(b, new Vector3(0.6f, 0.58f, 0.6f), 0.08f, Tint("Marble", new Color(0.35f, 0.95f, 0.6f)), new Vector3(0, 0.33f, 0));
                Box(b, new Vector3(0.62f, 0.06f, 0.62f), 0.02f, Mat("Gold"), new Vector3(0, 0.5f, 0));
                var cyan = Glow(new Color(0.3f, 2.4f, 2.2f));
                Box(b, new Vector3(0.1f, 0.06f, 0.04f), 0.01f, cyan, new Vector3(-0.12f, 0.38f, 0.3f), false);
                Box(b, new Vector3(0.1f, 0.06f, 0.04f), 0.01f, cyan, new Vector3(0.12f, 0.38f, 0.3f), false);
                break;
            default: // Phantom
                var ghost = new Color(0.85f, 0.92f, 1f, 0.72f);
                rig.Fading.Add((Ball(b, Tint("Ghost", ghost), new Vector3(0, 0.48f, 0), new Vector3(0.6f, 0.6f, 0.6f), false).GetComponent<Renderer>(), ghost));
                rig.Fading.Add((Ball(b, Tint("Ghost", ghost), new Vector3(0, 0.26f, 0), new Vector3(0.5f, 0.36f, 0.5f), false).GetComponent<Renderer>(), ghost));
                var hollow = new Color(0.05f, 0.06f, 0.15f, 0.95f);
                rig.Fading.Add((Ball(b, Tint("Ghost", hollow), new Vector3(-0.1f, 0.52f, 0.27f), new Vector3(0.1f, 0.14f, 0.05f), false).GetComponent<Renderer>(), hollow));
                rig.Fading.Add((Ball(b, Tint("Ghost", hollow), new Vector3(0.1f, 0.52f, 0.27f), new Vector3(0.1f, 0.14f, 0.05f), false).GetComponent<Renderer>(), hollow));
                break;
        }
        return rig;
    }

    private static void Eyes(Transform b, float y, float z, Material white, Material pupil)
    {
        foreach (int s in new[] { -1, 1 })
        {
            Ball(b, white, new Vector3(s * 0.12f, y, z), Vector3.one * 0.17f);
            Ball(b, pupil, new Vector3(s * 0.12f, y, z + 0.07f), Vector3.one * 0.08f, false);
        }
    }

    // ---- effects ----

    /// <summary>Rising flames for one burning tile; Play while it burns and Stop when it goes out.</summary>
    public static ParticleSystem Flames(Transform parent, Vector3 up)
    {
        var go = new GameObject("Flames");
        go.transform.SetParent(parent, false);
        go.transform.rotation = Quaternion.FromToRotation(Vector3.forward, up);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        var emission = ps.emission;
        emission.rateOverTime = GraphicsQuality.High ? 70f : 35f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.75f, 0.75f, 0.05f);
        var colour = ps.colorOverLifetime;
        colour.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.12f), 0.35f), new GradientColorKey(new Color(0.7f, 0.12f, 0.04f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
        colour.color = g;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.7f, 1f, 1.15f));
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Tint("Flame", new Color(2.2f, 1.7f, 1.2f));
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = ShadowCastingMode.Off;
        return ps;
    }

    /// <summary>A burst of gold sparkles and dust, for a crate breaking or a power-up being picked up.</summary>
    public static ParticleSystem Burst(Transform parent, Vector3 up, Color colour)
    {
        var go = new GameObject("Burst");
        go.transform.SetParent(parent, false);
        go.transform.rotation = Quaternion.FromToRotation(Vector3.forward, up);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = colour;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.3f;
        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Mat("Sparkle");
        r.shadowCastingMode = ShadowCastingMode.Off;
        return ps;
    }

    // ---- lighting and finish ----

    /// <summary>
    /// A filmic finish (bloom on metal and fire, ACES tone mapping, gentle vignette) for a camera, tinted for the
    /// current theme. Returns the volume so the caller can remove it with its scene.
    /// </summary>
    public static Volume Finish(Transform parent, Camera camera, bool threeD)
    {
        var data = camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = GraphicsQuality.High;
        data.antialiasing = AntialiasingMode.None; // MSAA from the pipeline asset

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(threeD ? 0.95f : 1.05f);
        bloom.intensity.Override(threeD ? 0.75f : 0.5f);
        bloom.scatter.Override(0.65f);
        bloom.tint.Override(ArenaTheme.Current.BloomTint);
        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(threeD ? TonemappingMode.ACES : TonemappingMode.Neutral);
        var grade = profile.Add<ColorAdjustments>(true);
        grade.postExposure.Override(threeD ? 0.1f : 0.05f);
        grade.contrast.Override(threeD ? 12f : 6f);
        grade.saturation.Override(threeD ? 8f : 6f);
        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(threeD ? 0.3f : 0.22f);
        vignette.smoothness.Override(0.45f);
        vignette.color.Override(ArenaTheme.Current.VignetteColour);

        var go = new GameObject("Palace finish");
        go.transform.SetParent(parent, false);
        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
        return volume;
    }

    /// <summary>The scene's sun from the front-left with soft shadows, ambient light and reflections, for the current theme.</summary>
    public static void Light()
    {
        var theme = ArenaTheme.Current;
        Light sun = null;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { sun = l; break; }
        if (sun == null) sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = theme.Sun;
        sun.intensity = theme.SunIntensity;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.75f;
        sun.transform.rotation = Quaternion.Euler(theme.SunAngles.x, theme.SunAngles.y, 0f);

        // Ambient light set directly (URP reads it from this probe): brighter from above, darker from below, so upward
        // faces aren't tinted by whatever sky the project had.
        var ambient = new SphericalHarmonicsL2();
        ambient.AddAmbientLight(theme.Ambient);
        ambient.AddDirectionalLight(Vector3.up, theme.AmbientFromAbove, 1f);
        RenderSettings.ambientMode = AmbientMode.Custom;
        RenderSettings.ambientProbe = ambient;
        // Reflections come from the theme's generated surroundings.
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = Surroundings();
        RenderSettings.reflectionIntensity = 1f;
    }

    private static readonly Dictionary<ArenaTheme, Cubemap> SurroundingsMade = new Dictionary<ArenaTheme, Cubemap>();

    /// <summary>
    /// A small generated cubemap of the theme's surroundings for reflections: its sky colour above, horizon, ground
    /// below; indoors (the palace hall) with chandelier glints and a band of windows, outdoors with the sun's glare.
    /// Without it, metal mirrors whatever sky the project had.
    /// </summary>
    public static Cubemap Surroundings()
    {
        var theme = ArenaTheme.Current;
        if (SurroundingsMade.TryGetValue(theme, out var made) && made != null) return made;
        const int n = 32;
        var cube = new Cubemap(n, TextureFormat.RGBAHalf, true) { name = theme.Name + " surroundings" };
        var top = theme.SkyTop;
        var horizon = theme.SkyHorizon;
        var below = theme.SkyBelow;
        var toSun = Quaternion.Euler(theme.SunAngles.x, theme.SunAngles.y, 0f) * Vector3.back;
        var pixels = new Color[n * n];
        for (int f = 0; f < 6; f++)
        {
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                var d = ((CubemapFace)f switch
                {
                    CubemapFace.PositiveX => new Vector3(1, -v, -u),
                    CubemapFace.NegativeX => new Vector3(-1, -v, u),
                    CubemapFace.PositiveY => new Vector3(u, 1, v),
                    CubemapFace.NegativeY => new Vector3(u, -1, -v),
                    CubemapFace.PositiveZ => new Vector3(u, -v, 1),
                    _ => new Vector3(-u, -v, -1),
                }).normalized;
                var c = d.y >= 0f ? Color.Lerp(horizon, top, Mathf.Pow(d.y, 0.6f)) : Color.Lerp(horizon, below, Mathf.Pow(-d.y, 0.5f));
                if (theme.Indoors)
                {
                    // Chandeliers: soft bright spots in a ring overhead.
                    float ring = Mathf.Abs(Mathf.Sin(Mathf.Atan2(d.z, d.x) * 3f));
                    if (d.y > 0.3f && d.y < 0.8f) c += new Color(3.2f, 2.6f, 1.8f) * Mathf.Pow(ring, 18f);
                    // A band of tall windows at the horizon gives edges a bright rim.
                    if (Mathf.Abs(d.y) < 0.15f) c += new Color(0.9f, 0.75f, 0.55f) * Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.Atan2(d.z, d.x) * 6f)), 10f);
                }
                else
                {
                    // Outdoors: the sun's glare, so polished edges catch a highlight.
                    c += theme.Sun * 4f * Mathf.Pow(Mathf.Max(0f, Vector3.Dot(d, toSun)), 64f);
                }
                pixels[y * n + x] = c;
            }
            cube.SetPixels(pixels, (CubemapFace)f);
        }
        cube.Apply(true, true);
        return SurroundingsMade[theme] = cube;
    }
}
