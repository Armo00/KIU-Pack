// Opt-in ring-specific engine attachment support in GroupEngineNodes.dll.
// Opt-in only. Requires the host's Harmony 2; no Harmony binary is redistributed.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class KIUGroupedEngineNodes : PartModule
{
    [KSPField] public string nodeGroups = "outer12;outer10;outer8;middle6;middle4;middle8;inner3;inner2;inner1";
    internal string lastDiagnostic;

    internal string GroupOf(AttachNode node, out int count)
    {
        count = 0;
        if (node == null || node.id == null) return null;
        foreach (string group in nodeGroups.Split(';')) {
            string prefix = group.Trim();
            if (!node.id.StartsWith(prefix + "n", StringComparison.Ordinal)) continue;
            int index;
            if (!int.TryParse(node.id.Substring(prefix.Length + 1), out index) || index < 0) continue;
            int first = 0;
            while (first < prefix.Length && !char.IsDigit(prefix[first])) first++;
            if (first == prefix.Length || !int.TryParse(prefix.Substring(first), out count) || count < 1 || index >= count) continue;
            return prefix;
        }
        return null;
    }
}

[KSPAddon(KSPAddon.Startup.EditorAny, false)]
public class KIUGroupedEngineNodeHooks : MonoBehaviour
{
    private static bool installed;
    private static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly Type AttachmentType = typeof(EditorLogic).Assembly.GetType("Attachment", true);
    private static readonly MethodInfo RefreshSymmetryUI = typeof(EditorLogic).GetMethod("SetSymState", PrivateInstance);
    private static readonly FieldInfo TemporarySymmetry = typeof(EditorLogic).GetField("tmpSymMethodInUse", PrivateInstance);

    // Attachment is internal in KSP 1.12.3; bind its fields without publicizing or
    // modifying Assembly-CSharp and without introducing a load-time Harmony reference.
    private sealed class View
    {
        private readonly object value;
        private static readonly FieldInfo Mode = AttachmentType.GetField("mode"), Parent = AttachmentType.GetField("potentialParent"),
            Caller = AttachmentType.GetField("caller"), Other = AttachmentType.GetField("otherPartNode"),
            CallerNode = AttachmentType.GetField("callerPartNode"), Possible = AttachmentType.GetField("possible");
        public View(object v) { value = v; }
        public int mode { get { return Convert.ToInt32(Mode.GetValue(value)); } }
        public Part potentialParent { get { return (Part)Parent.GetValue(value); } }
        public Part caller { get { return (Part)Caller.GetValue(value); } }
        public AttachNode otherPartNode { get { return (AttachNode)Other.GetValue(value); } }
        public AttachNode callerPartNode { get { return (AttachNode)CallerNode.GetValue(value); } }
        public bool possible { set { Possible.SetValue(this.value, value); } }
    }

    public void Awake()
    {
        if (installed) return;
        Type harmony = null, hm = null;
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies()) {
            if (harmony == null) harmony = a.GetType("HarmonyLib.Harmony", false);
            if (hm == null) hm = a.GetType("HarmonyLib.HarmonyMethod", false);
        }
        if (harmony == null || hm == null) {
            Debug.LogWarning("[KIUGroupedEngineNodes] Inactive: host Harmony 2 is unavailable. Stock behavior retained.");
            return;
        }
        object instance = null;
        try {
            instance = Activator.CreateInstance(harmony, new object[] { "KIU.GroupedEngineNodes" });
            Patch(harmony, hm, instance, "createSymmetry", new[] { typeof(int), AttachmentType }, "BeforeCreate", null, "AfterCreate");
            Patch(harmony, hm, instance, "CheckSymPartsAttach", new[] { AttachmentType }, "BeforeLookup", "AfterLookup", "RestoreLookup");
            installed = true;
            Debug.Log("[KIUGroupedEngineNodes] Opt-in grouped stack-node support installed.");
        } catch (Exception e) {
            // Do not leave a count-only half-patch installed.
            MethodInfo undo = harmony.GetMethod("UnpatchAll", new[] { typeof(string) });
            if (instance != null && undo != null) undo.Invoke(instance, new object[] { "KIU.GroupedEngineNodes" });
            Debug.LogError("[KIUGroupedEngineNodes] Hook registration failed: " + e);
        }
    }

    private static void Patch(Type h, Type hm, object owner, string method, Type[] args, string prefix, string postfix, string finalizer)
    {
        MethodInfo target = typeof(EditorLogic).GetMethod(method, PrivateInstance, null, args, null);
        if (target == null) throw new MissingMethodException(method);
        foreach (MethodInfo patch in h.GetMethods()) {
            ParameterInfo[] p = patch.GetParameters();
            if (patch.Name != "Patch" || p.Length < 5 || p[0].ParameterType != typeof(MethodBase)) continue;
            object[] values = new object[p.Length]; values[0] = target;
            bool hasFinalizer = false;
            for (int i = 1; i < p.Length; i++) {
                string name = p[i].Name == "prefix" ? prefix : p[i].Name == "postfix" ? postfix : p[i].Name == "finalizer" ? finalizer : null;
                if (p[i].Name == "finalizer") hasFinalizer = true;
                if (name != null) values[i] = Activator.CreateInstance(hm, new object[] { typeof(KIUGroupedEngineNodeHooks).GetMethod(name) });
            }
            if (!hasFinalizer) continue; // Guaranteed restoration is mandatory.
            patch.Invoke(owner, values); return;
        }
        throw new MissingMethodException("Harmony.Patch with finalizer");
    }

    public sealed class Scope
    {
        public Part parent;
        public List<AttachNode> originalNodes;
        public int originalStack;
        public bool restoreStack;
        public HashSet<Part> moving = new HashSet<Part>();
    }

    private static bool Resolve(View a, out KIUGroupedEngineNodes module, out string group, out int count, out List<AttachNode> nodes)
    {
        module = null; group = null; count = 0; nodes = null;
        if (!HighLogic.LoadedSceneIsEditor || a == null || (int)a.mode != 0 || a.potentialParent == null || a.caller == null) return false;
        Part p = a.potentialParent;
        // Multi-parent symmetry is a different operation; do not change it in this candidate.
        if (p.symmetryCounterparts.Count != 0) return false;
        module = p.FindModuleImplementing<KIUGroupedEngineNodes>();
        if (module == null || !module.isEnabled || a.otherPartNode == null || a.otherPartNode.nodeType != AttachNode.NodeType.Stack) return false;
        group = module.GroupOf(a.otherPartNode, out count);
        if (group == null) return false;
        nodes = new List<AttachNode>();
        HashSet<string> ids = new HashSet<string>();
        foreach (AttachNode n in p.attachNodes) {
            int ignored;
            if (n.nodeType == AttachNode.NodeType.Stack && module.GroupOf(n, out ignored) == group && ids.Add(n.id)) nodes.Add(n);
        }
        return nodes.Count == count && nodes.Contains(a.otherPartNode);
    }

    public static void BeforeCreate(EditorLogic __instance, ref int __0, object __1, out Scope __state)
    {
        __state = null;
        View a = __1 == null ? null : new View(__1);
        KIUGroupedEngineNodes module; string group; int count; List<AttachNode> nodes;
        if (__0 == 0 || (int)__instance.symmetryMethod != 0 ||
            (TemporarySymmetry != null && (bool)TemporarySymmetry.GetValue(__instance)) ||
            !Resolve(a, out module, out group, out count, out nodes)) return;
        // Preserve the explicit symmetry-off command. For symmetry-on, actual snap
        // target is authoritative, not the last ring's global Part.stackSymmetry.
        __state = new Scope { parent = a.potentialParent, originalStack = a.potentialParent.stackSymmetry, restoreStack = true };
        string diagnostic = group + ":node=" + a.otherPartNode.id + ":input=" + __0 + ":stack=" + __state.originalStack;
        if (module.lastDiagnostic != diagnostic) {
            module.lastDiagnostic = diagnostic;
            Debug.Log("[KIUGroupedEngineNodes] " + diagnostic + ":target=" + a.otherPartNode.id + ":copies=" + (count - 1));
        }
        __0 = count - 1;
        a.potentialParent.stackSymmetry = count - 1;
        if (__instance.symmetryMode != count - 1) {
            __instance.symmetryMode = count - 1;
            if (RefreshSymmetryUI != null) RefreshSymmetryUI.Invoke(__instance, null);
        }
    }

    public static Exception AfterCreate(Exception __exception, Scope __state)
    {
        if (__state != null && __state.parent != null && __state.restoreStack) __state.parent.stackSymmetry = __state.originalStack;
        return __exception;
    }

    public static void BeforeLookup(object __0, out Scope __state)
    {
        __state = null;
        View a = __0 == null ? null : new View(__0);
        KIUGroupedEngineNodes module; string group; int count; List<AttachNode> nodes;
        if (!Resolve(a, out module, out group, out count, out nodes) || a.caller.symmetryCounterparts.Count == 0 || (int)a.caller.symMethod != 0) return;
        __state = new Scope { parent = a.potentialParent, originalNodes = a.potentialParent.attachNodes };
        __state.moving.Add(a.caller);
        foreach (Part clone in a.caller.symmetryCounterparts) __state.moving.Add(clone);
        // B9 retains its real AttachNode objects and identifiers. Only this synchronous
        // stock lookup sees the reduced list. No positions, transforms or save data change.
        a.potentialParent.attachNodes = nodes;
    }

    public static void AfterLookup(object __0, object __result, Scope __state)
    {
        if (__state == null || __result == null) return;
        View original = new View(__0);
        Array results = (Array)__result;
        var used = new HashSet<AttachNode> { original.otherPartNode };
        bool valid = results.Length + 1 == __state.parent.attachNodes.Count;
        if (original.otherPartNode.attachedPart != null && !__state.moving.Contains(original.otherPartNode.attachedPart)) valid = false;
        foreach (object value in results) {
            if (value == null) { valid = false; continue; }
            View a = new View(value);
            if (a.otherPartNode == null || a.callerPartNode == null || a.potentialParent != __state.parent) { valid = false; continue; }
            AttachNode n = a.otherPartNode;
            if (!__state.parent.attachNodes.Contains(n) || !used.Add(n) || (n.attachedPart != null && !__state.moving.Contains(n.attachedPart))) valid = false;
        }
        if (!valid) {
            original.possible = false;
            foreach (object value in results) if (value != null) {
                View a = new View(value);
                a.possible = false;
                if (a.caller != null) a.caller.SetHighlightColor(Color.red);
            }
        }
    }

    public static Exception RestoreLookup(Exception __exception, Scope __state)
    {
        if (__state != null && __state.parent != null && __state.originalNodes != null) __state.parent.attachNodes = __state.originalNodes;
        return __exception;
    }
}
