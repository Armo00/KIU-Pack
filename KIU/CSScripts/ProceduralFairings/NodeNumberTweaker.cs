//  ==================================================
//  Procedural Fairings plug-in by Alexey Volynskov.

//  Licensed under CC-BY-4.0 terms: https://creativecommons.org/licenses/by/4.0/legalcode
//  ==================================================

using System;
using UnityEngine;

namespace Keramzit
{
    public class KzNodeNumberTweaker : PartModule
    {
        [KSPField] public string nodePrefix = "bottom";
        [KSPField] public int maxNumber;

        [KSPField(guiActiveEditor = true, guiName = "Fairing Nodes", groupName = PFUtils.PAWGroup, groupDisplayName = PFUtils.PAWName)]
        [UI_FloatRange(minValue = 1, maxValue = 8, stepIncrement = 1)]
        public float uiNumNodes = 2;

        [KSPField(isPersistant = true)]
        public int numNodes = 2;

        int numNodesBefore;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Node offset", guiFormat = "S4", guiUnits = "m", groupName = PFUtils.PAWGroup)]
        [UI_FloatEdit(sigFigs = 3, unit = "m", minValue = 0.1f, maxValue = 5, incrementLarge = 0.625f, incrementSmall = 0.125f, incrementSlide = 0.001f)]
        public float radius = 1.25f;

        [KSPField] public float radiusStepLarge = 0.625f;
        [KSPField] public float radiusStepSmall = 0.125f;

        [KSPField] public bool shouldResizeNodes = true;

        public int NodeSize => part.FindModuleImplementing<ProceduralFairingBase>() is ProceduralFairingBase fb ? 
                                    fb.FairingBaseNodeSize : 
                                    Math.Max(0, Mathf.RoundToInt(radius / radiusStepLarge) - 1);

        protected float oldRadius = -1000;
        public override string GetInfo() => $"Max Nodes: {maxNumber}";

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            SetupUI();

            uiNumNodes = numNodes;
            numNodesBefore = numNodes;

            if (HighLogic.LoadedSceneIsEditor)
                GameEvents.onVariantApplied.Add(OnPartVariantApplied);
        }

        public override void OnStartFinished(StartState state)
        {
            base.OnStartFinished(state);
            ResetNodePositions(false);
            if (HighLogic.LoadedSceneIsEditor)
                StartCoroutine(EditorChangeDetector());
        }

        public void OnDestroy()
        {
            GameEvents.onVariantApplied.Remove(OnPartVariantApplied);
        }

        private void OnPartVariantApplied(Part p, PartVariant variant)
        {
            if (p == part)
                ResetNodePositions(false);
        }

        public void ResetNodePositions(bool pushAttachments)
        {
            AddRemoveNodes();
            UpdateNodePositions(pushAttachments);
        }

        private System.Collections.IEnumerator EditorChangeDetector()
        {
            while (HighLogic.LoadedSceneIsEditor)
            {
                yield return new WaitForFixedUpdate();
                if (radius != oldRadius)
                    OnRadiusChanged(Fields[nameof(radius)], oldRadius);

                if (Convert.ToInt32(uiNumNodes) != numNodesBefore)
                    OnNumNodesChanged(Fields[nameof(uiNumNodes)], numNodesBefore);
            }
        }

        private void SetupUI()
        {
            //  Change the GUI text if there are no fairing attachment nodes.
            if (part.FindAttachNodes("connect") == null)
                Fields[nameof(uiNumNodes)].guiName = "Side Nodes";

            if (HighLogic.LoadedSceneIsEditor)
            {
                Fields[nameof(radius)].uiControlEditor.onFieldChanged += OnRadiusChanged;
                Fields[nameof(radius)].uiControlEditor.onSymmetryFieldChanged += OnRadiusChanged;

                Fields[nameof(uiNumNodes)].uiControlEditor.onFieldChanged += OnNumNodesChanged;
                Fields[nameof(uiNumNodes)].uiControlEditor.onSymmetryFieldChanged += OnNumNodesChanged;

                (Fields[nameof(radius)].uiControlEditor as UI_FloatEdit).incrementLarge = radiusStepLarge;
                (Fields[nameof(radius)].uiControlEditor as UI_FloatEdit).incrementSmall = radiusStepSmall;
                Fields[nameof(radius)].guiActiveEditor = shouldResizeNodes;

                (Fields[nameof(uiNumNodes)].uiControlEditor as UI_FloatRange).maxValue = maxNumber;
            }
        }

        public void OnRadiusChanged(BaseField f, object obj)
        {
            UpdateNodePositions(true);
            oldRadius = radius;
        }

        public void OnNumNodesChanged(BaseField f, object obj)
        {
            if (checkNodeAttachments())
            {
                uiNumNodes = numNodesBefore;
            }
            else
            {
                numNodes = Convert.ToInt32(uiNumNodes);
                numNodesBefore = numNodes;
                AddRemoveNodes();
                UpdateNodePositions(true);
            }
        }

        public void SetRadius(float rad, bool pushAttachments)
        {
            radius = rad;
            UpdateNodePositions(pushAttachments);
            oldRadius = radius;
        }

        string nodeName(int i) => $"{nodePrefix}{i:d2}";
        AttachNode findNode(int i) => part.FindAttachNode(nodeName(i));
        void SetNodeVisibility(AttachNode node, bool show) => node.position.x = show ? 0 : 10000;

        bool checkNodeAttachments()
        {
            for (int i = 1; i <= maxNumber; ++i)
            {
                if (findNode(i) is AttachNode node && node.attachedPart is Part)
                {
                    ScreenMessages.PostScreenMessage("Please detach any fairing parts before changing the number of nodes!", 4, ScreenMessageStyle.UPPER_CENTER, Color.yellow);
                    return true;
                }
            }
            return false;
        }

        private AttachNode GetExemplarNode()
        {
            for (int i = 1; i <= maxNumber; ++i)
            {
                if (findNode(i) is AttachNode node)
                    return node;
            }

            if (part.FindAttachNode("bottom") is AttachNode node2)
                return node2;
            return null;
        }

        private void AddRemoveNodes()
        {
            AttachNode exemplar = GetExemplarNode();
            float y = exemplar is AttachNode ? exemplar.position.y : 0;
            int nodeSize = exemplar is AttachNode ? exemplar.size : NodeSize;
            Vector3 dir = exemplar is AttachNode ? exemplar.orientation : Vector3.up;
            Vector3 pos = new Vector3(radius, y, 0);
            int i;
            part.stackSymmetry = numNodes - 1;

            for (i = 1; i <= numNodes; ++i)
            {
                if (findNode(i) == null)
                {
                    //  Create the fairing attachment node.
                    AttachNode node = new AttachNode
                    {
                        id = nodeName(i),
                        owner = part,
                        nodeType = AttachNode.NodeType.Stack,
                        position = pos,
                        orientation = dir,
                        originalPosition = pos,
                        originalOrientation = dir,
                        size = nodeSize
                    };

                    part.attachNodes.Add(node);
                }
            }

            for (; i <= maxNumber; ++i)
            {
                if (findNode(i) is AttachNode node2)
                {
                    if (HighLogic.LoadedSceneIsEditor)
                        SetNodeVisibility(node2, false);
                    else
                        part.attachNodes.Remove(node2);
                }
            }
        }

        private void UpdateNodePositions(bool pushAttachments)
        {
            for (int i = 1; i <= numNodes; ++i)
            {
                if (findNode(i) is AttachNode node)
                {
                    float a = Mathf.PI * 2 * (i - 1) / numNodes;
                    Vector3 newPos = new Vector3(Mathf.Cos(a) * radius, node.position.y, Mathf.Sin(a) * radius);
                    PFUtils.UpdateNode(part, node, newPos, NodeSize, pushAttachments);
                    node.originalPosition = new Vector3(Mathf.Cos(a), node.position.y, Mathf.Sin(a));
                }
            }
        }
    }
}
