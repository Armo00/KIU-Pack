using System;
using System.Collections.Generic;
using UnityEngine;

public partial class ConfigurableTank
{
    // Explicit collision-only children of bodyTransform. Empty preserves legacy
    // or third-party models; do not infer safe collision shapes from render bounds.
    [KSPField] public string continuousColliderTransforms = "";
    // Only these template collider components are removed. Visual transforms,
    // triggers and end-cap colliders not listed here remain untouched.
    [KSPField] public string continuousTemplateColliderTransforms = "";
    // Authored solid which KSP reclassifies as the Part's primary trigger.
    // Preserve that trigger; restore a separate physical surface at the same pose.
    [KSPField] public string continuousPrimarySolidTransform = "";
    private const string CollisionStretchName = "ConfigurableTank_CollisionStretch";
    private Transform collisionStretch;
    private readonly HashSet<string> templateColliderNames = new HashSet<string>();

    private void InitializeContinuousColliders()
    {
        if(string.IsNullOrEmpty(continuousColliderTransforms.Trim()))return;
        if(upperOnly || useFixedAnchorSpan)
            throw new ArgumentException("Continuous tank colliders cannot use structure or fixed-anchor mode");
        if(body.localPosition.sqrMagnitude>0.000001f || Quaternion.Angle(body.localRotation,Quaternion.identity)>.01f ||
            (body.localScale-Vector3.one).sqrMagnitude>0.000001f)
            throw new ArgumentException("Continuous tank colliders require an authored-zero body root");
        var selected=new List<Transform>();
        Transform existing=body.Find(CollisionStretchName);
        foreach(string token in continuousColliderTransforms.Split(',')) {
            string name=token.Trim();if(name.Length==0)continue;
            Transform t=body.Find(name);
            if(t==null && existing!=null)t=existing.Find(name);
            if(t==null || selected.Contains(t) || t.childCount!=0 || t.GetComponent<Renderer>()!=null ||
                t.GetComponent<MeshFilter>()!=null || t.GetComponent<Animation>()!=null)
                throw new ArgumentException("Expected a unique collision-only body child: "+name);
            Collider[] components=t.GetComponents<Collider>();
            if(components.Length!=1 || !(components[0] is BoxCollider || components[0] is MeshCollider))
                throw new ArgumentException("Unsupported continuous tank collider: "+name);
            MeshCollider mesh=components[0] as MeshCollider;
            if(mesh!=null && (!mesh.convex || mesh.sharedMesh==null))
                throw new ArgumentException("Continuous mesh collider must be convex: "+name);
            selected.Add(t);
        }
        if(selected.Count==0)throw new ArgumentException("No continuous body colliders selected");
        foreach(string token in continuousTemplateColliderTransforms.Split(','))
            if(token.Trim().Length>0)templateColliderNames.Add(token.Trim());
        if(templateColliderNames.Count==0)throw new ArgumentException("Explicit template collision names required");
        // Validate against the unchanged prefab too: a symmetry clone can inherit
        // an already stripped template and a stretched frame from its source Part.
        Part prefab=part.partInfo==null?null:part.partInfo.partPrefab;
        foreach(Transform templateRoot in new[]{lowerTemplate,upperTemplate}) {
            Transform authored=prefab==null?templateRoot:prefab.FindModelTransform(templateRoot.name);
            if(authored==null)throw new ArgumentException("Missing authored collision template");
            foreach(string name in templateColliderNames) {
                Transform t=authored.Find(name);
                Collider c=t==null?null:t.GetComponent<Collider>();
                if(c==null || c.isTrigger)throw new ArgumentException("Missing/non-solid template collider: "+name);
            }
            foreach(Collider c in authored.GetComponentsInChildren<Collider>(true))
                if(!templateColliderNames.Contains(c.transform.name))
                    throw new ArgumentException("Unclassified template collider: "+c.transform.name);
        }
        collisionStretch=existing;
        if(collisionStretch==null) {
            collisionStretch=new GameObject(CollisionStretchName).transform;
            collisionStretch.SetParent(body,false);
        }
        collisionStretch.localPosition=Vector3.zero;collisionStretch.localRotation=Quaternion.identity;collisionStretch.localScale=Vector3.one;
        foreach(Transform t in selected)if(t.parent!=collisionStretch)t.SetParent(collisionStretch,false);
        RestorePrimarySolid();
        StripExtensionColliders(lowerTemplate.gameObject);
        if(upperTemplate!=lowerTemplate)StripExtensionColliders(upperTemplate.gameObject);
        foreach(GameObject copy in copies)StripExtensionColliders(copy);
    }

    private void RestorePrimarySolid()
    {
        if(string.IsNullOrEmpty(continuousPrimarySolidTransform))return;
        Transform source=part.FindModelTransform(continuousPrimarySolidTransform);
        Collider original=source==null?null:source.GetComponent<Collider>();
        if(original==null)throw new ArgumentException("Missing declared primary solid");
        if(!original.isTrigger)return;
        string name="ConfigurableTank_Physical_"+source.name;
        Transform t=source.parent.Find(name);
        if(t==null) {
            t=new GameObject(name).transform;t.SetParent(source.parent,false);
            t.gameObject.layer=source.gameObject.layer;
            MeshCollider mesh=original as MeshCollider;BoxCollider box=original as BoxCollider;
            if(mesh!=null) {
                var c=t.gameObject.AddComponent<MeshCollider>();c.sharedMesh=mesh.sharedMesh;c.convex=mesh.convex;
            } else if(box!=null) {
                var c=t.gameObject.AddComponent<BoxCollider>();c.center=box.center;c.size=box.size;
            } else throw new ArgumentException("Unsupported primary solid type");
        }
        t.localPosition=source.localPosition;t.localRotation=source.localRotation;t.localScale=source.localScale;
        var physical=t.GetComponent<Collider>();physical.sharedMaterial=original.sharedMaterial;
        physical.isTrigger=false;physical.enabled=original.enabled;t.gameObject.SetActive(source.gameObject.activeSelf);
    }

    private void StripExtensionColliders(GameObject obj)
    {
        if(collisionStretch==null || obj==null)return;
        foreach(Collider c in obj.GetComponentsInChildren<Collider>(true)) {
            if(!templateColliderNames.Contains(c.transform.name))continue;
            // Destroy is deferred; disable before activating a newly cloned bay.
            c.enabled=false;
            UnityEngine.Object.Destroy(c);
        }
    }

    private void UpdateContinuousColliders(float down,float up)
    {
        if(collisionStretch==null)return;
        float scale=(mainBodyHeight+down+up)/mainBodyHeight;
        float start=-baseHeight*originFraction+bottomCapHeight;
        // Affine axial stretch: original lower edge -> start-down,
        // original upper edge -> start+mainBodyHeight+up. Radius never changes.
        collisionStretch.localScale=new Vector3(1,scale,1);
        collisionStretch.localPosition=Vector3.up*(start*(1-scale)-down);
    }
}
