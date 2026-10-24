using System;
using System.Globalization;
using System.IO;
using SaveUpgradePipeline;
using UnityEngine;

// The upgrade runs on saved PART nodes before PartModule reconstruction, where
// the old sibling B9 node is still available. Rules are cfg-owned and explicit.
[UpgradeModule(LoadContext.SFS | LoadContext.Craft, sfsNodeUrl="GAME/FLIGHTSTATE/VESSEL/PART", craftNodeUrl="PART")]
public class ConfigurableTankLengthUpgrade : UpgradeScript
{
    public override string Name { get { return "KIU ConfigurableTank legacy length selection"; } }
    public override string Description { get { return "Preserve opted-in legacy length selections as ConfigurableTank segments"; } }
    public override Version EarliestCompatibleVersion { get { return new Version(0,0,0); } }
    public override Version TargetVersion { get { return new Version(1,0,0); } }
    // Stock compares the file's KSP version, not this module's schema version.
    // Eligibility is instead determined by our explicit Part rule + old module.
    protected override TestResult VersionTest(Version version) { return TestResult.Upgradeable; }

    private static string Canonical(string name) { return (name??"").Replace('_','.'); }
    internal static bool MatchesPart(ConfigNode node,LoadContext context,string configuredName)
    {
        string saved=NodeUtil.GetPartNodeNameValue(node,context);
        // Craft "part" includes an instance suffix; SFS "name" does not.
        if(context==LoadContext.Craft && saved!=null) {
            int split=saved.LastIndexOf('_');uint instance;
            if(split>=0 && uint.TryParse(saved.Substring(split+1),NumberStyles.None,CultureInfo.InvariantCulture,out instance))
                saved=saved.Substring(0,split);
        }
        return Canonical(saved)==Canonical(configuredName);
    }
    internal static ConfigNode RuleFor(ConfigNode node,LoadContext context)
    {
        if(node==null || GameDatabase.Instance==null)return null;
        foreach(ConfigNode rule in GameDatabase.Instance.GetConfigNodes("CONFIGURABLE_TANK_LENGTH_UPGRADE"))
            if(MatchesPart(node,context,rule.GetValue("part")))return rule;
        return null;
    }
    public override TestResult OnTest(ConfigNode node,LoadContext context,ref string nodeName)
    {
        nodeName=NodeUtil.GetPartNodeNameValue(node,context);
        ConfigNode rule=RuleFor(node,context);if(rule==null)return TestResult.Pass;
        string reason;
        int result=ConfigurableTankLengthMigration.Inspect(node,rule,out reason);
        if(result<0) { Debug.LogError("[ConfigurableTank] Legacy length migration rejected: "+nodeName+" "+reason);return TestResult.Failed; }
        return result==0?TestResult.Pass:TestResult.Upgradeable;
    }
    public override void OnUpgrade(ConfigNode node,LoadContext context,ConfigNode parent)
    {
        ConfigNode rule=RuleFor(node,context);if(rule==null)return;
        string reason;
        if(!ConfigurableTankLengthMigration.Migrate(node,rule,out reason))
            throw new InvalidOperationException("Legacy length migration failed: "+reason);
        Debug.Log("[ConfigurableTank] Migrated legacy length: "+NodeUtil.GetPartNodeNameValue(node,context)+" "+reason);
    }
}

// KSP does not always run mod UpgradeScripts for files already at the current
// game version (including the stock direct editor-load API). Use public load
// events as a narrow fallback; no Harmony patch and no B9 runtime dependency.
[KSPAddon(KSPAddon.Startup.MainMenu,true)]
public class ConfigurableTankLengthLoadEvents : MonoBehaviour
{
    private ShipConstruct migratedShip;
    public void Awake()
    {
        DontDestroyOnLoad(this);
        GameEvents.onEditorLoad.Add(EditorLoaded);
        GameEvents.onEditorStarted.Add(EditorStarted);
        GameEvents.onProtoPartSnapshotLoad.Add(ProtoLoaded);
    }
    public void OnDestroy()
    {
        GameEvents.onEditorLoad.Remove(EditorLoaded);
        GameEvents.onEditorStarted.Remove(EditorStarted);
        GameEvents.onProtoPartSnapshotLoad.Remove(ProtoLoaded);
    }
    private void EditorStarted()
    {
        if(EditorLogic.fetch!=null)EditorLoaded(EditorLogic.fetch.ship,default(KSP.UI.Screens.CraftBrowserDialog.LoadType));
    }
    // KSP's EventData delegate wrapper requires an instance target.
    private void ProtoLoaded(GameEvents.FromToAction<ProtoPartSnapshot,ConfigNode> loaded)
    {
        // Constructor: the raw PART node is supplied before snapshot modules are
        // built. Load(Vessel): the same event is fired again with a null node.
        // Only transform the former; KSP constructs its module list afterwards.
        ConfigNode node=loaded.to;
        ConfigNode rule=ConfigurableTankLengthUpgrade.RuleFor(node,LoadContext.SFS);if(rule==null)return;
        string reason;int result=ConfigurableTankLengthMigration.Inspect(node,rule,out reason);
        if(result<0)throw new InvalidOperationException("Invalid saved length: "+reason);
        if(result==0)return;
        if(!ConfigurableTankLengthMigration.Migrate(node,rule,out reason))throw new InvalidOperationException(reason);
        Debug.Log("[ConfigurableTank] Migrated proto-part length: "+reason);
    }
    private void EditorLoaded(ShipConstruct ship,KSP.UI.Screens.CraftBrowserDialog.LoadType loadType)
    {
        ConfigNode root=ShipConstruction.ShipConfig;
        if(root==null || ship==null || ship.parts.Count==0 || migratedShip==ship)return;
        // KSP/module-indexing mods may rewrite the cached ConfigNode and discard
        // an obsolete module before this event. Read the selected craft without
        // modifying it, and require a matching full-vessel identity/part count.
        string sourcePath=EditorDriver.filePathToLoad;
        if(!string.IsNullOrEmpty(sourcePath) && File.Exists(sourcePath)) {
            ConfigNode source=ConfigNode.Load(sourcePath);
            if(source!=null && source.GetValue("ship")==ship.shipName && source.GetNodes("PART").Length==ship.parts.Count)
                root=source;
        }
        bool changed=false;
        foreach(ConfigNode saved in root.GetNodes("PART")) {
            ConfigNode rule=ConfigurableTankLengthUpgrade.RuleFor(saved,LoadContext.Craft);if(rule==null)continue;
            string reason;int state=ConfigurableTankLengthMigration.Inspect(saved,rule,out reason);
            if(state<0)throw new InvalidOperationException("Invalid craft length: "+reason);
            if(state==0)continue;
            string identity=saved.GetValue("part");int underscore=identity==null?-1:identity.LastIndexOf('_');uint id;
            if(underscore<0 || !uint.TryParse(identity.Substring(underscore+1),out id))throw new InvalidOperationException("Missing legacy craft ID");
            Part target=null;foreach(Part p in ship.parts)if(p.craftID==id){if(target!=null)throw new InvalidOperationException("Ambiguous craft ID");target=p;}
            var controller=target==null?null:target.FindModuleImplementing<ConfigurableTank>();
            if(controller==null || !controller.useFixedAnchorSpan)throw new InvalidOperationException("Legacy span target not found");
            if(!ConfigurableTankLengthUpgrade.MatchesPart(saved,LoadContext.Craft,target.partInfo.name))
                throw new InvalidOperationException("Legacy craft ID matched a different Part");
            if(!ConfigurableTankLengthMigration.Migrate(saved,rule,out reason))throw new InvalidOperationException(reason);
            ConfigNode current=saved.GetNode("MODULE","name","ConfigurableTank");
            int count=int.Parse(current.GetValue("segments"),CultureInfo.InvariantCulture);
            if(count>controller.maxSegments)throw new InvalidOperationException("Legacy length exceeds configured controller range");
            controller.SetSegmentCount(count,false);
            // Keep subsequent in-memory reloads consistent without writing the
            // user's original file, touching unrelated modules or resaving a ship.
            foreach(ConfigNode cached in ShipConstruction.ShipConfig.GetNodes("PART"))
                if(cached.GetValue("part")==identity) {
                    ConfigNode targetNode=cached.GetNode("MODULE","name","ConfigurableTank");
                    if(targetNode==null){targetNode=cached.AddNode("MODULE");targetNode.AddValue("name","ConfigurableTank");}
                    targetNode.SetValue("segments",count.ToString(CultureInfo.InvariantCulture),true);
                }
            changed=true;
            Debug.Log("[ConfigurableTank] Migrated editor length on "+target.partInfo.name+": "+reason);
        }
        if(changed)migratedShip=ship;
    }
}

internal static class ConfigurableTankLengthMigration
{
    private static ConfigNode FindLegacy(ConfigNode part,ConfigNode rule,out int matches)
    {
        matches=0;ConfigNode result=null;
        foreach(ConfigNode node in part.GetNodes("MODULE"))
            if(node.GetValue("name")=="ModuleB9PartSwitch" && node.GetValue("moduleID")==rule.GetValue("moduleID")) {result=node;matches++;}
        return result;
    }
    private static bool ReadCount(ConfigNode part,ConfigNode rule,out ConfigNode old,out ConfigNode current,out int count,out string reason)
    {
        count=0;reason="";int matches;old=FindLegacy(part,rule,out matches);current=null;
        if(matches!=1){reason="expected exactly one legacy length module";return false;}
        int maximum;
        if(!int.TryParse(rule.GetValue("maxSegments"),NumberStyles.None,CultureInfo.InvariantCulture,out maximum)||maximum<0)
            {reason="invalid migration rule";return false;}
        string prefix=rule.GetValue("subtypePrefix"),subtype=old.GetValue("currentSubtype");
        if(string.IsNullOrEmpty(prefix)||string.IsNullOrEmpty(subtype)||!subtype.StartsWith(prefix,StringComparison.Ordinal)||
           !int.TryParse(subtype.Substring(prefix.Length),NumberStyles.None,CultureInfo.InvariantCulture,out count)||count<0||count>maximum)
            {reason="invalid or missing legacy subtype";return false;}
        int currentCount=0;
        foreach(ConfigNode node in part.GetNodes("MODULE"))if(node.GetValue("name")=="ConfigurableTank") {current=node;currentCount++;}
        if(currentCount>1){reason="ambiguous target modules";return false;}
        if(current!=null && current.HasValue("segments")) {
            int selected;
            if(!int.TryParse(current.GetValue("segments"),NumberStyles.Integer,CultureInfo.InvariantCulture,out selected)||selected<0||selected>maximum)
                {reason="invalid current segment count";return false;}
            // Never overwrite an explicitly saved new selection with stale B9 state.
            count=selected;
        }
        return true;
    }
    internal static int Inspect(ConfigNode part,ConfigNode rule,out string reason)
    {
        int matches;FindLegacy(part,rule,out matches);reason="no legacy module";
        if(matches==0)return 0;
        ConfigNode old,current;int count;
        return ReadCount(part,rule,out old,out current,out count,out reason)?1:-1;
    }
    internal static bool Migrate(ConfigNode part,ConfigNode rule,out string reason)
    {
        ConfigNode old,current;int count;
        if(!ReadCount(part,rule,out old,out current,out count,out reason))return false;
        if(current==null){current=part.AddNode("MODULE");current.AddValue("name","ConfigurableTank");}
        current.SetValue("segments",count.ToString(CultureInfo.InvariantCulture),true);
        part.RemoveNode(old);
        reason="segments="+count;
        return true;
    }
}
