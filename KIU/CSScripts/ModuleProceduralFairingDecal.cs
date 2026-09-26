// KIU shared PF surface markings. No compile-time dependency on ProceduralFairings.
// PF owns geometry, colliders, shielding, mass, hinges and separation.
using System;
using System.Collections.Generic;
using UnityEngine;
#if !PF_DECAL_TEST
using System.Reflection;
using UnityEngine.Rendering;

public class ModuleProceduralFairingDecal : PartModule
{
    [KSPField] public string decalID = "marking";
    [KSPField] public string textureURL = "";
    // UV rectangle in Unity texture coordinates, not image top-left coordinates.
    [KSPField] public Vector4 textureRect = new Vector4(0, 0, 1, 1);
    [KSPField] public float referenceDiameter = 5.2f;
    [KSPField] public float width = 2.2f;
    [KSPField] public float height = 1.466667f;
    [KSPField] public float groupHeight = 2.601667f;
    [KSPField] public float groupWidth = 2.2f;
    [KSPField] public float verticalOffset = 0.5675f;
    [KSPField] public float barrelFraction = 0.42f;
    [KSPField] public float surfaceOffset = 0.0015f;

    private PartModule fairing;
    private MeshFilter shell;
    private Mesh mesh;
    private Material material;
    private GameObject decal;
    private FieldInfo[] watched;
    private float nextPoll;
    private int lastSignature;
    private bool started, built, reported;
    private const string Prefix = "KIU_PF_Decal_";
    private static readonly string[] ShapeFields = {
        "baseRad", "maxRad", "cylStart", "cylEnd", "topRad", "inlineHeight",
        "sideThickness", "numSegs", "numSideParts", "noseHeightRatio",
        "baseConeShape", "noseConeShape", "baseConeSegments", "noseConeSegments"
    };

    public override void OnStart(StartState state)
    {
        base.OnStart(state);
        if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) return;
        foreach (PartModule candidate in part.Modules)
            if (candidate.moduleName == "ProceduralFairingSide") { fairing = candidate; break; }
        if (fairing == null) { Warn("ProceduralFairingSide is missing"); return; }
        watched = new FieldInfo[ShapeFields.Length];
        for (int i = 0; i < watched.Length; ++i)
        {
            watched[i] = fairing.GetType().GetField(ShapeFields[i]);
            if (watched[i] == null) { Warn("Unsupported PF interface: " + ShapeFields[i]); return; }
        }
        started = true;
    }

    // Run after PF's editor shape update. Poll field/bounds fingerprints, not arrays every frame.
    public void LateUpdate()
    {
        if (!started || part == null || fairing == null || Time.realtimeSinceStartup < nextPoll) return;
        nextPoll = Time.realtimeSinceStartup + 0.35f;
        try
        {
            var found = part.FindModelComponent<MeshFilter>("model");
            if (found == null || found.sharedMesh == null || found.sharedMesh.vertexCount < 3) return;
            if (shell != found)
            {
                Clear(); shell = found; built = false;
                // Editor symmetry/copy can clone generated children. Remove only this module's copies.
                for (int i = shell.transform.childCount - 1; i >= 0; --i)
                {
                    var child = shell.transform.GetChild(i);
                    if (child.name == Prefix + decalID) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                }
            }
            int signature = found.sharedMesh.GetInstanceID();
            unchecked
            {
                signature = signature * 31 + found.sharedMesh.vertexCount;
                signature = signature * 31 + found.sharedMesh.bounds.GetHashCode();
                foreach (var field in watched) signature = signature * 31 + field.GetValue(fairing).GetHashCode();
            }
            if (built && signature == lastSignature) return;
            if (material == null)
            {
                Texture texture = GameDatabase.Instance.GetTexture(textureURL, false);
                Shader shader = Shader.Find("KSP/Alpha/Cutoff");
                if (texture == null || shader == null) { Warn("Missing texture or shader: " + textureURL); return; }
                material = new Material(shader) { name = Prefix + decalID, mainTexture = texture };
                material.SetColor("_Color", Color.white); material.SetFloat("_Cutoff", 0.5f);
                decal = new GameObject(Prefix + decalID); decal.layer = shell.gameObject.layer;
                decal.transform.SetParent(shell.transform, false);
                mesh = new Mesh { name = Prefix + decalID };
                decal.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = decal.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                // No collider, physical volume, force or extra Part is created.
            }
            float radius = Read("maxRad") + Read("sideThickness");
            var layout = KIUPFDecalGeometry.Place(radius, Read("cylStart"), Read("cylEnd"),
                Read("numSideParts"), referenceDiameter, width, height, groupWidth, groupHeight, verticalOffset, barrelFraction);
            var data = KIUPFDecalGeometry.Clip(found.sharedMesh.vertices, found.sharedMesh.normals,
                found.sharedMesh.triangles, radius, layout, textureRect, surfaceOffset);
            mesh.Clear(); mesh.SetVertices(data.vertices); mesh.SetNormals(data.normals);
            mesh.SetUVs(0, data.uv); mesh.SetTriangles(data.triangles, 0); mesh.RecalculateBounds();
            decal.SetActive(data.triangles.Count > 0);
            lastSignature = signature; built = true;
        }
        catch (Exception ex) { Warn(ex.Message); Clear(); started = false; }
    }
    private float Read(string key) { return Convert.ToSingle(fairing.GetType().GetField(key).GetValue(fairing)); }
    private void Warn(string message) { if (!reported) Debug.LogWarning("[KIU PF decal] " + message, this); reported = true; }
    private void Clear()
    {
        if (decal != null) { decal.SetActive(false); Destroy(decal); }
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
        decal = null; mesh = null; material = null;
    }
    public void OnDestroy() { Clear(); }
}
#endif

// Shared with the executable offline regression harness; do not duplicate this algorithm in previews.
public static class KIUPFDecalGeometry
{
    public struct Layout { public float width, height, centre, scale; }
    public sealed class Data
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> triangles = new List<int>();
    }
    private struct Vertex { public Vector3 p, n; public Vector2 q; }
    public static Layout Place(float radius, float bottom, float top, float sides, float reference,
        float width, float height, float groupWidth, float groupHeight, float offset, float fraction)
    {
        if (radius <= 0 || top - bottom <= 0.02f || width <= 0 || height <= 0 || groupWidth <= 0 || groupHeight <= 0 || reference <= 0)
            return new Layout();
        float length = top - bottom;
        float scale = Math.Min(2 * radius / reference, 0.86f * length / groupHeight);
        // Pair layout is identical for both modules, even with 3+ petals and very short barrels.
        scale = Math.Min(scale, (float)(0.76 * 2 * Math.PI * radius / Math.Max(2, sides)) / groupWidth);
        float half = groupHeight * scale * 0.5f;
        float centre = Math.Max(bottom + half + 0.03f * length,
            Math.Min(top - half - 0.03f * length, bottom + length * Math.Max(0, Math.Min(1, fraction))));
        return new Layout { width = width * scale, height = height * scale,
            centre = centre + offset * scale, scale = scale };
    }
    public static Data Clip(Vector3[] vertices, Vector3[] normals, int[] triangles, float radius,
        Layout layout, Vector4 uvRect, float offset)
    {
        var output = new Data();
        if (layout.width <= 0 || layout.height <= 0 || normals.Length != vertices.Length) return output;
        float lo = layout.centre - layout.height / 2, hi = layout.centre + layout.height / 2;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            var poly = new List<Vertex>(3); bool outer = true;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int j = 0; j < 3; ++j)
            {
                int k = triangles[i + j]; var p = vertices[k]; var n = normals[k];
                // Reject inner skin, cut edges and rims using radial normal direction.
                if (p.x * n.x + p.z * n.z < 0.3f * radius) outer = false;
                minY = Math.Min(minY, p.y); maxY = Math.Max(maxY, p.y);
                // Unity is left-handed: viewed from +X with +Y up, +Z is the viewer's right.
                poly.Add(new Vertex { p = p, n = n, q = new Vector2((float)Math.Atan2(p.z, p.x) * radius, p.y) });
            }
            if (!outer || maxY < lo || minY > hi) continue;
            poly = Slice(poly, 0, -layout.width / 2, true);
            poly = Slice(poly, 0, layout.width / 2, false);
            poly = Slice(poly, 1, lo, true); poly = Slice(poly, 1, hi, false);
            for (int j = 1; j + 1 < poly.Count; ++j)
            {
                var a = poly[0]; var b = poly[j]; var c = poly[j + 1];
                if (Vector3.Cross(b.p - a.p, c.p - a.p).sqrMagnitude < 1e-14f) continue;
                foreach (var v in new[] { a, b, c })
                {
                    var normal = v.n.normalized;
                    output.triangles.Add(output.vertices.Count);
                    output.vertices.Add(v.p + normal * offset); output.normals.Add(normal);
                    float u = v.q.x / layout.width + 0.5f, vv = (v.q.y - lo) / layout.height;
                    output.uv.Add(new Vector2(uvRect.x + u * (uvRect.z - uvRect.x), uvRect.y + vv * (uvRect.w - uvRect.y)));
                }
            }
        }
        return output;
    }
    private static List<Vertex> Slice(List<Vertex> input, int axis, float at, bool lower)
    {
        var result = new List<Vertex>();
        for (int i = 0; i < input.Count; ++i)
        {
            var a = input[i]; var b = input[(i + 1) % input.Count];
            float da = (axis == 0 ? a.q.x : a.q.y) - at, db = (axis == 0 ? b.q.x : b.q.y) - at;
            bool ia = lower ? da >= 0 : da <= 0, ib = lower ? db >= 0 : db <= 0;
            if (ia) result.Add(a);
            if (ia != ib)
            {
                float t = da / (da - db);
                result.Add(new Vertex { p = a.p + (b.p - a.p) * t,
                    n = a.n + (b.n - a.n) * t, q = a.q + (b.q - a.q) * t });
            }
        }
        return result;
    }
}
