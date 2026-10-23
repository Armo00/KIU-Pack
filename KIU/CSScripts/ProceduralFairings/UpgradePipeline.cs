using System;
using System.Linq;
using System.Runtime.InteropServices;
using SaveUpgradePipeline;
using UnityEngine;

namespace ProceduralFairings
{
    [UpgradeModule(LoadContext.SFS | LoadContext.Craft, sfsNodeUrl = "GAME/FLIGHTSTATE/VESSEL/PART", craftNodeUrl = "PART")]
    public class DeprecateAdapterAndResizer : UpgradeScript
    {
        public override string Name { get => "ProceduralFairings 6.0 PartModule Deprecater";  }
        public override string Description { get => "Upgrades pre-6.0 ProceduralFairings parts"; }
        public override Version EarliestCompatibleVersion { get => new Version(0, 0, 0); }
        public override Version TargetVersion { get => new Version(6, 0, 0); }

        private bool popup = false;

        public override TestResult OnTest(ConfigNode node, LoadContext loadContext, ref string nodeName)
        {
            nodeName = NodeUtil.GetPartNodeNameValue(node, loadContext);
            ConfigNode pfBaseNode = node.GetNode("MODULE", "name", "ProceduralFairingBase");
            ConfigNode adapterNode = node.GetNode("MODULE", "name", "ProceduralFairingAdapter");
            ConfigNode resizerNode = node.GetNode("MODULE", "name", "KzFairingBaseResizer");
            ConfigNode thrustPlateNode = node.GetNode("MODULE", "name", "KzThrustPlateResizer");
            TestResult res = TestResult.Pass;
            if (pfBaseNode is ConfigNode)
                res = adapterNode is ConfigNode || resizerNode is ConfigNode ? TestResult.Upgradeable : res;
            else if (thrustPlateNode is ConfigNode)
                res = TestResult.Upgradeable;
            return res;
        }

        public override void OnUpgrade(ConfigNode node, LoadContext loadContext, ConfigNode parentNode)
        {
            string nodeName = NodeUtil.GetPartNodeNameValue(node, loadContext);
            Debug.Log($"[PF] UpgradePipeline OnUpgrade() context {loadContext} for {nodeName}");
            // When the node is the PART, ParentNode is the Ship
            ConfigNode pfBaseNode = node.GetNode("MODULE", "name", "ProceduralFairingBase");
            if (node.GetNode("MODULE", "name", "ProceduralFairingAdapter") is ConfigNode adapterNode)
            {
                if (adapterNode.HasValue("baseSize"))
                    pfBaseNode.SetValue("baseSize", adapterNode.GetValue("baseSize"), true);
                if (adapterNode.HasValue("topSize"))
                    pfBaseNode.SetValue("topSize", adapterNode.GetValue("topSize"), true);
                if (adapterNode.HasValue("height"))
                    pfBaseNode.SetValue("height", adapterNode.GetValue("height"), true);
                if (adapterNode.HasValue("extraHeight"))
                    pfBaseNode.SetValue("extraHeight", adapterNode.GetValue("extraHeight"), true);
                if (adapterNode.HasValue("topNodeDecouplesWhenFairingsGone"))
                    pfBaseNode.SetValue("autoDecoupleTopNode", adapterNode.GetValue("topNodeDecouplesWhenFairingsGone"), true);
                if (adapterNode.HasValue("topNodeName"))
                    pfBaseNode.SetValue("topNodeName", adapterNode.GetValue("topNodeName"), true);
                pfBaseNode.SetValue("mode", $"{Keramzit.ProceduralFairingBase.BaseMode.Adapter}", true);
                Debug.Log($"[PF] Updated ProceduralFairingBase with Adapter data to {pfBaseNode}");
                node.RemoveNode(adapterNode);
            }
            if (node.GetNode("MODULE", "name", "KzFairingBaseResizer") is ConfigNode resizerNode)
            {
                if (resizerNode.HasValue("size"))
                    pfBaseNode.SetValue("baseSize", resizerNode.GetValue("size"), true);
                pfBaseNode.SetValue("mode", $"{Keramzit.ProceduralFairingBase.BaseMode.Payload}", true);
                Debug.Log($"[PF] Updated ProceduralFairingBase with Resizer data to {pfBaseNode}");
                node.RemoveNode(resizerNode);
            }
            if (node.GetNode("MODULE", "name", "KzThrustPlateResizer") is ConfigNode plateNode)
            {
                if (pfBaseNode is null)
                {
                    pfBaseNode = node.AddNode("MODULE");
                    pfBaseNode.AddValue("name", "ProceduralFairingBase");
                }
                if (plateNode.HasValue("size"))
                    pfBaseNode.SetValue("baseSize", plateNode.GetValue("size"), true);
                pfBaseNode.SetValue("mode", $"{Keramzit.ProceduralFairingBase.BaseMode.Plate}", true);
                Debug.Log($"[PF] Updated ProceduralFairingBase with ThrustPlate data to {pfBaseNode}");
                node.RemoveNode(plateNode);
            }
            if (loadContext == LoadContext.Craft && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !popup)
            {
                popup = true;
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                             new Vector2(0.5f, 0.5f),
                                             "ShowPFUpgradeWarningOS",
                                             $"{RuntimeInformation.OSDescription} Procedural Fairings Craft Upgrade Warning",
                                             $"ProceduralFairings has updated your .craft file!  The craft in the VAB may be out of sync with the upgraded file on your {RuntimeInformation.OSDescription} system, and some of your Procedural Fairings may have incorrect settings.  This is expected, please just reload the updated craft file!  Do NOT save the craft before reloading.",
                                             "OK",
                                             false,
                                             HighLogic.UISkin);
            } else if (loadContext == LoadContext.SFS && !popup && AssemblyLoader.loadedAssemblies.Any(a => a.name.StartsWith("CraftManager")))
            {
                GameEvents.onGameStateLoad.Add(DelayedMessage);
                popup = true;
            }
        }

        public void DelayedMessage(ConfigNode _)
        {
            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                         new Vector2(0.5f, 0.5f),
                                         "ShowPFUpgradeWarningCraftManager",
                                         $"Procedural Fairings / Craft Manager Craft Upgrade Compatibility Warning",
                                         $"ProceduralFairings has updated your save file, and detected that you are using the Craft Manager mod.  Craft Mananger does not currently call the stock Upgrade Pipeline for .craft files.  This may result in them not upgrading properly to their new settings for Procedural Fairings v6.  Please load each of your craft files once, using the stock loader, to trigger the update.",
                                         "OK",
                                         false,
                                         HighLogic.UISkin);
            GameEvents.onGameStateLoad.Remove(DelayedMessage);
        }
    }
}
