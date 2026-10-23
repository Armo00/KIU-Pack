//  ==================================================
//  Procedural Fairings plug-in by Alexey Volynskov.

//  Licensed under CC-BY-4.0 terms: https://creativecommons.org/licenses/by/4.0/legalcode
//  ==================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Keramzit
{
    public class KzFairingBaseShielding : PartModule, IAirstreamShield
    {
        readonly List<Part> shieldedParts = new List<Part>();
        ProceduralFairingSide sideFairing;

        float boundCylY0, boundCylY1, boundCylRad;
        float lookupRad;

        Vector3 lookupCenter;
        Vector3[] shape;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Parts shielded", groupName = PFUtils.PAWGroup, groupDisplayName = PFUtils.PAWName)]
        public int numShieldedDisplay;

        public bool ClosedAndLocked() => true;
        public Vessel GetVessel() => vessel;
        public Part GetPart() => part;

        public override void OnStart (StartState state)
        {
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
            {
                reset();
                GameEvents.onVesselWasModified.Add(onVesselModified);
                GameEvents.onVesselGoOffRails.Add(onVesselUnpack);
                GameEvents.onPartDie.Add(OnPartDestroyed);
            }
        }

        void OnDestroy ()
        {
            GameEvents.onVesselWasModified.Remove(onVesselModified);
            GameEvents.onVesselGoOffRails.Remove(onVesselUnpack);
            GameEvents.onPartDie.Remove(OnPartDestroyed);
        }

        public void reset()
        {
            getFairingParams();
            if (HighLogic.LoadedSceneIsEditor || (HighLogic.LoadedSceneIsFlight && !vessel.packed))
                enableShielding();
        }

        private bool AllFairingSidesAttached()
        {
            if (part.GetComponent<KzNodeNumberTweaker>() is KzNodeNumberTweaker nnt &&
                part.FindAttachNodes("connect") is AttachNode[] attached)
            {
                for (int i=0; i<nnt.numNodes; i++)
                {
                    if (!(attached[i] is AttachNode n && n.attachedPart && n.attachedPart.GetComponent<ProceduralFairingSide>() is ProceduralFairingSide))
                        return false;
                }
                return true;
            }
            return false;
        }

        AttachNode [] getFairingParams ()
        {
            if (part.GetComponent<KzNodeNumberTweaker>() is KzNodeNumberTweaker nnt &&
                part.FindAttachNodes("connect") is AttachNode[] attached)
            {
                if (!AllFairingSidesAttached())
                {
                    shape = null;
                    sideFairing = null;
                    boundCylY0 = boundCylY1 = boundCylRad = 0;
                    lookupCenter = Vector3.zero;
                    lookupRad = 0;
                    return null;
                }

                ProceduralFairingSide sf = attached[0].attachedPart.GetComponent<ProceduralFairingSide>();
                sideFairing = sf;

                //  Get the polyline shape.
                shape = (sf.inlineHeight <= 0) ?
                            ProceduralFairingBase.buildFairingShape(sf.baseRad, sf.maxRad, sf.cylStart, sf.cylEnd, sf.noseHeightRatio, sf.baseConeShape, sf.noseConeShape, (int)sf.baseConeSegments, (int)sf.noseConeSegments, sf.vertMapping, sf.mappingScale.y) :
                            ProceduralFairingBase.buildInlineFairingShape(sf.baseRad, sf.maxRad, sf.topRad, sf.cylStart, sf.cylEnd, sf.inlineHeight, sf.baseConeShape, (int)sf.baseConeSegments, sf.vertMapping, sf.mappingScale.y);

                //  Offset shape by thickness.

                for (int i = 0; i < shape.Length; ++i)
                {
                    if (i == 0 || i == shape.Length - 1)
                    {
                        shape[i] += new Vector3(sf.sideThickness, 0, 0);
                    }
                    else
                    {
                        Vector2 n = shape[i + 1] - shape[i - 1];
                        n.Set(n.y, -n.x);
                        n.Normalize();
                        shape[i] += new Vector3(n.x, n.y, 0) * sf.sideThickness;
                    }
                }

                //  Compute the bounds.

                boundCylRad = shape[0].x;
                boundCylY0 = shape[0].y;
                boundCylY1 = shape[0].y;

                foreach (Vector3 p in shape)
                {
                    boundCylRad = Math.Max(boundCylRad, p.x);
                    boundCylY0 = Math.Min(boundCylY0, p.y);
                    boundCylY1 = Math.Max(boundCylY1, p.y);
                }

                lookupCenter = new Vector3(0, (boundCylY0 + boundCylY1) / 2, 0);
                lookupRad = new Vector2(boundCylRad, (boundCylY1 - boundCylY0) / 2).magnitude;

                return attached;
            }
            return null;
        }

        void enableShielding ()
        {
            disableShielding ();

            var attached = getFairingParams ();

            if (!sideFairing)
            {
                return;
            }

            //  Get all parts in range.

            var parts = new List<Part>();

            var colliders = Physics.OverlapSphere (part.transform.TransformPoint (lookupCenter), lookupRad, 1);
            foreach (Collider coll in colliders)
            {
                if (coll.gameObject.GetComponentUpwards<Part>() is Part p)
                    parts.AddUnique(p);
            }

            //  Filter parts.

            float sizeSqr = lookupRad * lookupRad * 4;
            float boundCylRadSq = boundCylRad * boundCylRad;

            bool isInline = (sideFairing.inlineHeight > 0);
            bool topClosed = false;

            Matrix4x4 w2l = Matrix4x4.identity, w2lb = Matrix4x4.identity;

            Bounds topBounds = default (Bounds);

            if (isInline)
            {
                w2l = part.transform.worldToLocalMatrix;
                w2lb = w2l;

                for (int i = 0; i < 3; ++i)
                {
                    for (int j = 0; j < 3; ++j)
                    {
                        w2lb [i, j] = Mathf.Abs (w2lb [i, j]);
                    }
                }

                topBounds = new Bounds (new Vector3 (0, boundCylY1, 0), new Vector3 (sideFairing.topRad * 2, sideFairing.sideThickness, sideFairing.topRad * 2));
            }
            foreach (Part pt in parts)
            {
                //  Check special cases.

                if (pt == part)
                {
                    shieldedParts.Add (pt);

                    continue;
                }

                bool isSide = false;

                for (int i = 0; i < attached.Length; ++i)
                {
                    if (attached [i].attachedPart == pt)
                    {
                        isSide = true;

                        break;
                    }
                }

                if (isSide)
                {
                    continue;
                }

                //  Check if the top is closed in the inline case.

                var bounds = pt.GetRendererBounds ();

                var box = PartGeometryUtil.MergeBounds (bounds, pt.transform);

                if (isInline && !topClosed && pt.vessel == vessel)
                {
                    var wb = box; wb.Expand (sideFairing.sideThickness * 4);

                    var b = new Bounds (w2l.MultiplyPoint3x4 (wb.center), w2lb.MultiplyVector (wb.size));

                    if (b.Contains (topBounds.min) && b.Contains (topBounds.max))
                    {
                        topClosed = true;
                    }
                }

                //  Check if the centroid is within the fairing bounds.

                var c = part.transform.InverseTransformPoint (PartGeometryUtil.FindBoundsCentroid (bounds, null));

                float y = c.y;

                if (y < boundCylY0 || y > boundCylY1)
                {
                    continue;
                }

                float xsq = new Vector2 (c.x, c.z).sqrMagnitude;

                if (xsq > boundCylRadSq)
                {
                    continue;
                }

                //  Accurate centroid check.

                float x = Mathf.Sqrt (xsq);

                bool inside = false;

                for (int i = 1; i < shape.Length; ++i)
                {
                    var p0 = shape [i - 1];
                    var p1 = shape [i];

                    if (p0.y > p1.y)
                    {
                        var p = p0;

                        p0 = p1;
                        p1 = p;
                    }

                    if (y < p0.y || y > p1.y)
                    {
                        continue;
                    }

                    float dy = p1.y - p0.y, r;

                    if (dy <= 1e-6f)
                    {
                        r = (p0.x + p1.x) * 0.5f;
                    }
                    else
                    {
                        r = (p1.x - p0.x) * (y - p0.y) / dy + p0.x;
                    }

                    if (x > r)
                    {
                        continue;
                    }

                    inside = true;

                    break;
                }

                if (!inside)
                {
                    continue;
                }

                shieldedParts.Add (pt);
            }

            if (isInline && !topClosed)
            {
                disableShielding ();
                return;
            }

            //  Add shielding.
            foreach (Part p in shieldedParts)
                p.AddShield(this);

            numShieldedDisplay = shieldedParts.Count;

            if (part.GetComponent<ProceduralFairingBase>() is ProceduralFairingBase fbase)
                fbase.OnShieldingEnabled(shieldedParts);
        }

        void disableShielding()
        {
            if (shieldedParts is List<Part>)
            {
                if (part.GetComponent<ProceduralFairingBase>() is ProceduralFairingBase fbase)
                    fbase.OnShieldingDisabled(shieldedParts);
                foreach (Part p in shieldedParts)
                    p.RemoveShield(this);
                shieldedParts.Clear();
                numShieldedDisplay = shieldedParts.Count;
            }
        }

        void onVesselModified (Vessel v)
        {
            if (v != vessel)
            {
                var dp = v.vesselTransform.position - part.transform.TransformPoint (lookupCenter);

                if (dp.sqrMagnitude > lookupRad * lookupRad)
                {
                    return;
                }
            }

            enableShielding ();
        }

        void onVesselUnpack (Vessel v)
        {
            if (v == vessel)
                enableShielding();
        }

        void onVesselPack (Vessel v)
        {
            if (v == vessel)
                disableShielding();
        }

        void OnPartDestroyed (Part p)
        {
            if (p == part)
            {
                disableShielding();
                return;
            }

            //  Check for attached side fairing parts.

            if (part.GetComponent<KzNodeNumberTweaker>() is KzNodeNumberTweaker nnt &&
                part.FindAttachNodes("connect") is AttachNode[] attached)
            {
                for (int i = 0; i < nnt.numNodes; ++i)
                {
                    if (p == attached[i].attachedPart)
                    {
                        disableShielding();
                        return;
                    }
                }
            }

            //  Check for top parts in the inline/adapter case.
            if (p.vessel == vessel && sideFairing && sideFairing.inlineHeight > 0)
            {
                enableShielding ();
            }
        }

        public void OnPartPack() => disableShielding();
    }
}
