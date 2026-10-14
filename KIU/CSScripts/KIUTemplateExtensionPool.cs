using System;
using System.Collections.Generic;
using UnityEngine;
// Multi-template complete/remainder geometry. No launch or animation policy.
public sealed class KIUTemplateExtensionPool {
    private readonly Part owner;
    private readonly Transform upper,first;
    private readonly Vector3 origin;
    private readonly Transform[] templates;
    private readonly Dictionary<int,List<GameObject>> pools=new Dictionary<int,List<GameObject>>();
    private readonly int referenceSegments;
    private readonly float step;
    private readonly string prefix;
    public int Segments { get; private set; }
    public KIUTemplateExtensionPool(Part part,string upperName,string originName,string[] names,string instancePrefix,int reference,float segmentLength) {
        owner=part;upper=part.FindModelTransform(upperName);templates=new Transform[names.Length+1];prefix=instancePrefix;
        first=part.FindModelTransform(names[0]);if(upper==null||first==null)throw new InvalidOperationException("Missing extension model roots");
        Transform baseline=part.FindModelTransform(originName);origin=baseline==null?upper.localPosition:baseline.localPosition;
        referenceSegments=reference;step=segmentLength;
        for(int units=1;units<templates.Length;units++) {
            templates[units]=part.FindModelTransform(names[units-1]);pools[units]=new List<GameObject>();
            for(int i=0;;i++) { var existing=first.parent.Find(prefix+units+"_"+i);if(existing==null)break;pools[units].Add(existing.gameObject); }
        }
    }
    public void Apply(int count) {
        if(count<0)throw new ArgumentOutOfRangeException("count");
        int fullUnits=templates.Length-1,fullBays=count/fullUnits,remainder=count%fullUnits;
        for(int units=1;units<=fullUnits;units++) {
            var template=templates[units];if(template==null)throw new InvalidOperationException("Missing extension template "+units);
            var pool=pools[units];int needed=units==fullUnits?fullBays:units==remainder?1:0;
            for(int i=pool.Count;i<needed;i++) {
                var copy=UnityEngine.Object.Instantiate(template.gameObject);copy.name=prefix+units+"_"+i;
                copy.transform.SetParent(first.parent,false);pool.Add(copy);
            }
            template.gameObject.SetActive(false);
            for(int i=0;i<pool.Count;i++) {
                var tr=pool[i].transform;int offset=units==fullUnits?i*fullUnits:fullBays*fullUnits;
                tr.localPosition=template.localPosition+Vector3.up*(offset*step);
                tr.localRotation=template.localRotation;tr.localScale=Vector3.one;pool[i].SetActive(i<needed);
            }
        }
        upper.localPosition=origin+Vector3.up*((count-referenceSegments)*step);Segments=count;
        KIUModelRenderers.Invalidate(owner);
    }
}
