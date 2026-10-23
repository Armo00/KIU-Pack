using Keramzit;
using UnityEngine;

namespace ProceduralFairings
{
    public class FairingSideShapePreset
    {
        [Persistent] public string name = "Conic";
        [Persistent] public Vector4 baseConeShape = new Vector4(0.3f, 0.3f, 0.7f, 0.7f);
        [Persistent] public Vector4 noseConeShape = new Vector4(0.1f, 0, 0.7f, 0.7f);
        [Persistent] public int baseConeSegments = 7;
        [Persistent] public int noseConeSegments = 11;
        [Persistent] public float noseHeightRatio = 2;

        public void Apply(ProceduralFairingSide side)
        {
            side.baseConeShape = baseConeShape;
            side.noseConeShape = noseConeShape;
            side.baseConeSegments = baseConeSegments;
            side.noseConeSegments = noseConeSegments;
            side.noseHeightRatio = noseHeightRatio;
            side.ReadNoseCurveFromVec4();
            side.ReadBaseCurveFromVec4();
            side.rebuildMesh();
        }

        public bool IsApplied(ProceduralFairingSide side)
        {
            return CompareCurveShape(baseConeShape, side.baseCurveStartX, side.baseCurveStartY, side.baseCurveEndX, side.baseCurveEndY)
                && CompareCurveShape(noseConeShape, side.noseCurveStartX, side.noseCurveStartY, side.noseCurveEndX, side.noseCurveEndY)
                && side.baseConeSegments == baseConeSegments
                && side.noseConeSegments == noseConeSegments
                && side.noseHeightRatio == noseHeightRatio;
        }

        private static bool CompareCurveShape(Vector4 shape, float startX, float startY, float endX, float endY)
        {
            return Mathf.Approximately(shape.x, startX)
                && Mathf.Approximately(shape.y, startY)
                && Mathf.Approximately(shape.z, endX)
                && Mathf.Approximately(shape.w, endY);
        }
    }
}
