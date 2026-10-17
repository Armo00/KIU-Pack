using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class KIUModelRenderers
{
    private static readonly FieldInfo CurrentHighlightColor=typeof(Part).GetField("currentHighlightColor",BindingFlags.Instance|BindingFlags.NonPublic);
    private static readonly int RimColor=Shader.PropertyToID("_RimColor");
public static void Invalidate(Part part)
    {
        // Consumers such as Vessel Viewer use these lists, not the scene hierarchy.
        part.ResetModelRenderersCache();
        part.ResetModelMeshRenderersCache();
        part.ResetModelSkinnedMeshRenderersCache();
    }
public static void Refresh(Part part)
    {
        if(part==null || (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight))return;
        Invalidate(part);
        var renderers=new List<Renderer>(part.FindModelComponents<Renderer>());
        // Hidden B9 options must be cleared together with active meshes. If
        // excluded, a renderer hidden while red can retain that property block
        // and display it again when an arm/variant is re-enabled.
        renderers.RemoveAll(r=>r==null || r.GetComponentInParent<Part>()!=part);
        KSPUtil.RemoveNonHighlightableRenderers(renderers);
        // The rim-highlight list and the outline Highlighter have separate caches.
        part.HighlightRenderer=renderers;
        part.RefreshHighlighter();
        if(CurrentHighlightColor!=null) {
            // Stock Highlight(Color) skips a cached unchanged color, including
            // "off". Reapply the Part's actual color directly to every owned
            // renderer, preserving genuine red placement errors/selection and
            // all other per-renderer shader properties.
            Color color=(Color)CurrentHighlightColor.GetValue(part);
            if(part.highlighter!=null) {
                if(color==Color.clear)part.highlighter.Off();else part.highlighter.ConstantOn(color);
            }
            color.a*=Highlighting.Highlighter.HighlighterLimit;
            var block=new MaterialPropertyBlock();
            foreach(Renderer renderer in renderers) {
                renderer.GetPropertyBlock(block);block.SetColor(RimColor,color);renderer.SetPropertyBlock(block);block.Clear();
            }
        }
    }
}
