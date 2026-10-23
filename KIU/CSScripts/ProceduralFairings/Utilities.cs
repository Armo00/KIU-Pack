//  ==================================================
//  Procedural Fairings plug-in by Alexey Volynskov.

//  Licensed under CC-BY-4.0 terms: https://creativecommons.org/licenses/by/4.0/legalcode
//  ==================================================

using UnityEngine;

namespace Keramzit
{
    struct BezierSlope
    {
        Vector2 p1, p2;

        public BezierSlope (Vector4 v)
        {
            p1 = new Vector2 (v.x, v.y);
            p2 = new Vector2 (v.z, v.w);
        }

        public Vector2 interp (float t)
        {
            Vector2 a = Vector2.Lerp (Vector2.zero, p1, t);
            Vector2 b = Vector2.Lerp (p1, p2, t);
            Vector2 c = Vector2.Lerp (p2, Vector2.one, t);
            Vector2 d = Vector2.Lerp (a, b, t);
            Vector2 e = Vector2.Lerp (b, c, t);

            return Vector2.Lerp (d, e, t);
        }
    }

    public static class PFUtils
    {
        public const string PAWName = "ProceduralFairings";
        public const string PAWGroup = "ProceduralFairings";
        public const string PAWShapeName = "Procedural Fairing Shape";
        public const string PAWShapeGroup = "ProceduralFairingsShape";
        public static void setFieldRange(BaseField field, float minval, float maxval)
        {
            if (field.uiControlEditor is UI_FloatRange fr)
            {
                fr.minValue = minval;
                fr.maxValue = maxval;
            }

            if (field.uiControlEditor is UI_FloatEdit fe)
            {
                fe.minValue = minval;
                fe.maxValue = maxval;
            }
        }

        public static void UpdateAttachedPartPos(AttachNode node, Part part, Vector3 oldPosWorld)
        {
            if (node is AttachNode && part is Part && 
                node.attachedPart is Part ap && ap.FindAttachNodeByPart(part) is AttachNode)
            {
                if (HighLogic.LoadedSceneIsFlight)
                    Debug.LogWarning($"[PF] PF Utilities Attempting to Update a Part Position during Flight Scene!\n{StackTraceUtility.ExtractStackTrace()}");

                var dp = part.transform.TransformPoint(node.position) - oldPosWorld;

                if (ap == part.parent)
                {
                    while (ap.parent) ap = ap.parent;
                    ap.transform.Translate(dp, Space.World);
                    part.transform.Translate(-dp, Space.World);
                }
                else
                {
                    ap.transform.Translate(dp, Space.World);
                }
            }
        }

        public static void UpdateNode(Part part, AttachNode node, Vector3 newPosition, int size, bool pushAttachments, float attachDiameter = 0)
        {
            if (node is AttachNode)
            {
                Vector3 oldPosWorld = part.transform.TransformPoint(node.position);
                node.position = newPosition;
                node.size = size;

                if (pushAttachments)
                    PFUtils.UpdateAttachedPartPos(node, part, oldPosWorld);

                if (node.attachedPart is Part)
                {
                    PFUtils.InformAttachedPartNodePositionChanged(node);
                    PFUtils.InformAttachNodeSizeChanged(node, attachDiameter > 0 ? attachDiameter : Mathf.Max(node.size, 0.01f));
                }
            }
        }

        public static void InformAttachedPartNodePositionChanged(AttachNode node)
        {
            if (node is AttachNode && node.attachedPart is Part)
            {
                BaseEventDetails baseEventDatum = new BaseEventDetails(0);
                baseEventDatum.Set("location", node.position);
                baseEventDatum.Set("orientation", node.orientation);
                baseEventDatum.Set("secondaryAxis", node.secondaryAxis);
                baseEventDatum.Set("node", node);

                node.attachedPart.SendEvent("OnPartAttachNodePositionChanged", baseEventDatum);
            }
        }

        public static void InformAttachNodeSizeChanged(AttachNode node, float diameter, float area=0)
        {
            if (node is AttachNode && node.attachedPart is Part)
            {
                var data = new BaseEventDetails(BaseEventDetails.Sender.USER);
                data.Set<AttachNode>("node", node);
                data.Set<float>("minDia", diameter);
                data.Set<float>("area", area > 0 ? area : Mathf.PI * diameter * diameter / 4);
                node.attachedPart.SendEvent("OnPartAttachNodeSizeChanged", data);
            }
        }

        public static string FormatMass(float mass) => (mass < 0.01) ? $"{mass * 1e3:N3}kg" : $"{mass:N3}t";
        public static string FormatCost(float cost) => $"{cost:N0}";
    }
}
