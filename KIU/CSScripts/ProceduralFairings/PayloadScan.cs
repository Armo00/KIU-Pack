//  ==================================================
//  Procedural Fairings plug-in by Alexey Volynskov.

//  Licensed under CC-BY-4.0 terms: https://creativecommons.org/licenses/by/4.0/legalcode
//  ==================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Keramzit
{
    struct PayloadScan
    {
        public List<float> profile;
        public List<Part> payload;
        public HashSet<Part> hash;

        public Matrix4x4 w2l;

        public float ofs, verticalStep, extraRadius;

        public PayloadScan (Part p, float vs, float er)
        {
            profile = new List<float>(128);
            payload = new List<Part>(128);
            hash = new HashSet<Part>(128);

            hash.Add(p);

            w2l = p.transform.worldToLocalMatrix;
            ofs = 0;
            verticalStep = vs;
            extraRadius = er;
        }

        public void AddPart(Part p, Part prevPart)
        {
            if (p is Part && !hash.Contains(p))
            {
                hash.Add(p);

                if (p.GetComponent<LaunchClamp>() == null &&
                    !(p == prevPart.parent && prevPart.srfAttachNode.attachedPart == p))
                {
                    payload.Add(p);
                }
            }
        }

        public void AddPayloadEdge (Vector3 v0, Vector3 v1)
        {
            float r0 = Mathf.Sqrt(v0.x * v0.x + v0.z * v0.z) + extraRadius;
            float r1 = Mathf.Sqrt(v1.x * v1.x + v1.z * v1.z) + extraRadius;

            float y0 = (v0.y - ofs) / verticalStep;
            float y1 = (v1.y - ofs) / verticalStep;

            if (y0 > y1)
            {
                float tmp;

                tmp = y0; y0 = y1; y1 = tmp;
                tmp = r0; r0 = r1; r1 = tmp;
            }

            int h0 = Mathf.FloorToInt (y0);
            int h1 = Mathf.FloorToInt (y1);

            if (h1 < 0)
            {
                return;
            }

            if (h1 >= profile.Count)
            {
                var farray = new float [h1 - profile.Count + 1];

                for (int i = 0; i < farray.Length; i++)
                {
                    farray [i] = 0;
                }

                profile.AddRange (farray);
            }

            if (h0 >= 0)
            {
                profile [h0] = Mathf.Max (profile [h0], r0);
            }

            profile [h1] = Mathf.Max (profile [h1], r1);

            if (h0 != h1)
            {
                float k = (r1 - r0) / (y1 - y0);
                float b = r0 + k * (h0 + 1 - y0);
                float maxR = Mathf.Max (r0, r1);

                for (int h = Math.Max (h0, 0); h < h1; ++h)
                {
                    float r = Mathf.Min (k * (h - h0) + b, maxR);

                    profile [h] = Mathf.Max (profile [h], r);
                    profile [h + 1] = Mathf.Max (profile [h + 1], r);
                }
            }
        }

        public void AddPayload(Bounds box, Matrix4x4 boxTm)
        {
            Matrix4x4 m = w2l * boxTm;

            Vector3 p0 = box.min, p1 = box.max;

            var verts = new Vector3 [8];

            for (int i = 0; i < 8; ++i)
            {
                verts [i] = m.MultiplyPoint3x4 (new Vector3 ((i & 1) != 0 ? p1.x : p0.x, (i & 2) != 0 ? p1.y : p0.y, (i & 4) != 0 ? p1.z : p0.z));
            }

            AddPayloadEdge (verts [0], verts [1]);
            AddPayloadEdge (verts [2], verts [3]);
            AddPayloadEdge (verts [4], verts [5]);
            AddPayloadEdge (verts [6], verts [7]);

            AddPayloadEdge (verts [0], verts [2]);
            AddPayloadEdge (verts [1], verts [3]);
            AddPayloadEdge (verts [4], verts [6]);
            AddPayloadEdge (verts [5], verts [7]);

            AddPayloadEdge (verts [0], verts [4]);
            AddPayloadEdge (verts [1], verts [5]);
            AddPayloadEdge (verts [2], verts [6]);
            AddPayloadEdge (verts [3], verts [7]);
        }

        public void AddPayload(Collider c)
        {
            var mc = c as MeshCollider;
            var bc = c as BoxCollider;

            if (mc)
            {
                var m = w2l * mc.transform.localToWorldMatrix;

                var verts = mc.sharedMesh.vertices;
                var faces = mc.sharedMesh.triangles;

                for (int i = 0; i < faces.Length; i += 3)
                {
                    var v0 = m.MultiplyPoint3x4 (verts [faces [i]]);
                    var v1 = m.MultiplyPoint3x4 (verts [faces [i + 1]]);
                    var v2 = m.MultiplyPoint3x4 (verts [faces [i + 2]]);

                    AddPayloadEdge (v0, v1);
                    AddPayloadEdge (v1, v2);
                    AddPayloadEdge (v2, v0);
                }
            }
            else if (bc)
                AddPayload(new Bounds (bc.center, bc.size), bc.transform.localToWorldMatrix);
            else
                AddPayload(c.bounds, Matrix4x4.identity);
        }
    }
}
