// Local opt-in CZ-10B collision pilot for Procedural Fairings 6.8.0.0.
// Upstream: Alexey Volynskov and KSP-RO contributors; CC-BY-4.0.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Keramzit;

namespace ProceduralFairings
{
    // Main-thread only. Visible meshes and PF's force/hinge code are not changed.
    internal sealed class SimplifiedFairingColliders : IDisposable
    {
        internal const string RootName = "_PF_CZ10_CollisionPanels";
        private sealed class SharedGeometry
        {
            internal string Key;
            internal Mesh[] Meshes;
            internal Vector3[] Centers;
            internal int References;
        }
        private static readonly Dictionary<string, SharedGeometry> Cache = new Dictionary<string, SharedGeometry>();
        private readonly Part part;
        private readonly Transform model;
        private readonly ColliderPool stockPool;
        private readonly List<MeshCollider> colliders = new List<MeshCollider>();
        private Transform root;
        private Transform nose;
        private SharedGeometry geometry;
        internal int ActiveCount { get; private set; }

        internal SimplifiedFairingColliders(Part part, Transform model, ColliderPool stockPool)
        {
            this.part = part;
            this.model = model;
            this.stockPool = stockPool;
            // A KSP prefab/copy can already have our child hierarchy, but not this
            // nonserialized managed owner. Adopt it instead of adding 30 more panels.
            root = model.Find(RootName);
            if (root != null)
            {
                colliders.AddRange(root.GetComponentsInChildren<MeshCollider>(true));
                nose = model.Find("nose_collider");
                if (nose != null && nose.GetComponent<Collider>() != null) nose = null;
            }
        }

        internal bool TryApply(Vector3[] shape, SimplifiedFairingGeometry.Settings settings,
                               Vector3 nosePosition, out string reason)
        {
            string key = MakeKey(shape, settings);
            if (geometry != null && geometry.Key == key && root != null && nose != null)
            {
                // No new meshes or sharedMesh assignments on an unchanged shape.
                nose.localPosition = nosePosition;
                reason = null;
                return true;
            }
            SharedGeometry next = Acquire(key, shape, settings, out reason);
            if (next == null) return false;
            try
            {
                EnsureRoot();
                root.gameObject.SetActive(false);
                for (int i = 0; i < next.Meshes.Length; ++i)
                {
                    MeshCollider c;
                    if (i < colliders.Count && colliders[i] != null) c = colliders[i];
                    else
                    {
                        var obj = new GameObject("panel_" + i.ToString("D2"));
                        obj.transform.SetParent(root, false);
                        c = obj.AddComponent<MeshCollider>();
                        if (i < colliders.Count) colliders[i] = c; else colliders.Add(c);
                    }
                    c.gameObject.layer = model.gameObject.layer;
                    c.gameObject.tag = model.gameObject.tag;
                    c.transform.localPosition = next.Centers[i];
                    c.transform.localRotation = Quaternion.identity;
                    c.transform.localScale = Vector3.one;
                    c.isTrigger = false;
                    c.convex = true;
                    if (c.sharedMesh != next.Meshes[i]) c.sharedMesh = next.Meshes[i];
                    c.enabled = true;
                    c.gameObject.SetActive(true);
                }
                for (int i = next.Meshes.Length; i < colliders.Count; ++i)
                    if (colliders[i] != null)
                    {
                        colliders[i].enabled = false;
                        colliders[i].sharedMesh = null;
                        colliders[i].gameObject.SetActive(false);
                    }
                // Remove native collision-only children only AFTER the new geometry
                // is ready. Detaching immediately also avoids duplicate nose names
                // while Unity's Destroy is deferred to the end of the frame.
                RemoveStockColliders();
                if (nose == null)
                {
                    nose = new GameObject("nose_collider").transform;
                    nose.SetParent(model, false);
                }
                nose.localPosition = nosePosition;
                nose.localRotation = Quaternion.identity;
                nose.localScale = Vector3.one;
                var previous = geometry;
                geometry = next;
                next = null; // this instance now owns the cache lease
                Release(previous);
                ActiveCount = geometry.Meshes.Length;
                root.gameObject.SetActive(true);
                return true;
            }
            catch
            {
                // Clear all component references before releasing either mesh lease.
                ClearColliderMeshes();
                Release(next);
                Dispose();
                throw;
            }
        }

        private void EnsureRoot()
        {
            if (root == null)
            {
                root = new GameObject(RootName).transform;
                root.SetParent(model, false);
            }
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
            root.gameObject.layer = model.gameObject.layer;
            root.gameObject.tag = model.gameObject.tag;
        }

        private void RemoveStockColliders()
        {
            foreach (Collider c in part.FindModelComponents<Collider>())
            {
                if (c == null || c.transform.IsChildOf(root)) continue;
                // Stock PF's MU colliders are collision-only leaf objects. Preserve
                // any visible object if another compatible model embeds a collider.
                if (c.transform == model || c.GetComponentInChildren<Renderer>(true) != null ||
                    c.GetComponentInChildren<MeshFilter>(true) != null || c.transform.childCount != 0)
                {
                    c.enabled = false;
                    UnityEngine.Object.Destroy(c);
                }
                else
                {
                    c.gameObject.SetActive(false);
                    c.transform.SetParent(null, false);
                    stockPool.Release(c);
                }
            }
            stockPool.ReleaseCacheToPool();
        }

        private static string MakeKey(Vector3[] shape, SimplifiedFairingGeometry.Settings s)
        {
            var b = new StringBuilder(512);
            b.Append(s.Sectors).Append('|').Append(s.NosePanels);
            foreach (double v in new[] { s.SpanDegrees, s.Thickness, s.Clearance, s.SeamGap, s.MaxExpansionRatio })
                b.Append('|').Append(v.ToString("R", CultureInfo.InvariantCulture));
            foreach (Vector3 p in shape)
                b.Append('|').Append(p.x.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                 .Append(p.y.ToString("R", CultureInfo.InvariantCulture));
            return b.ToString();
        }

        private static SharedGeometry Acquire(string key, Vector3[] shape,
                                               SimplifiedFairingGeometry.Settings s, out string reason)
        {
            if (Cache.TryGetValue(key, out SharedGeometry cached))
            {
                ++cached.References;
                reason = null;
                return cached;
            }
            var points = new SimplifiedFairingGeometry.Point[shape.Length];
            for (int i = 0; i < shape.Length; ++i)
                points[i] = new SimplifiedFairingGeometry.Point(shape[i].x, shape[i].y);
            if (!SimplifiedFairingGeometry.TryBuild(points, s, out var plan, out reason)) return null;
            var created = new SharedGeometry { Key = key, Meshes = new Mesh[plan.Panels.Length], Centers = new Vector3[plan.Panels.Length], References = 1 };
            try
            {
                for (int i = 0; i < plan.Panels.Length; ++i)
                {
                    var panel = plan.Panels[i];
                    var centered = SimplifiedFairingGeometry.CenterForFloat(panel);
                    created.Centers[i] = new Vector3(centered.Center.X, centered.Center.Y, centered.Center.Z);
                    var vertices = new Vector3[panel.Vertices.Length];
                    for (int j = 0; j < vertices.Length; ++j)
                    {
                        var v = centered.Vertices[j];
                        vertices[j] = new Vector3(v.X, v.Y, v.Z);
                    }
                    var mesh = new Mesh { name = "PF_CZ10_Collision_" + i };
                    created.Meshes[i] = mesh;
                    mesh.vertices = vertices;
                    mesh.triangles = panel.Triangles;
                    mesh.RecalculateBounds();
                    // Keep readable for Unity convex cooking and native inspection.
                }
                Cache.Add(key, created);
                return created;
            }
            catch
            {
                foreach (Mesh mesh in created.Meshes) if (mesh != null) UnityEngine.Object.Destroy(mesh);
                throw;
            }
        }

        private static void Release(SharedGeometry entry)
        {
            if (entry == null || --entry.References != 0) return;
            Cache.Remove(entry.Key);
            foreach (Mesh mesh in entry.Meshes) if (mesh != null) UnityEngine.Object.Destroy(mesh);
        }

        private void ClearColliderMeshes()
        {
            if (root != null) root.gameObject.SetActive(false);
            foreach (MeshCollider c in colliders) if (c != null) { c.enabled = false; c.sharedMesh = null; }
        }

        public void Dispose()
        {
            ClearColliderMeshes();
            if (root != null)
            {
                root.SetParent(null, false);
                UnityEngine.Object.Destroy(root.gameObject);
                root = null;
            }
            if (nose != null)
            {
                nose.SetParent(null, false);
                UnityEngine.Object.Destroy(nose.gameObject);
                nose = null;
            }
            colliders.Clear();
            Release(geometry);
            geometry = null;
            ActiveCount = 0;
        }
    }
}
