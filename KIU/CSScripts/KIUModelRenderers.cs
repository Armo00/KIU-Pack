using System;
using System.Collections.Generic;
using UnityEngine;

public static class KIUModelRenderers
{
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
        renderers.RemoveAll(r=>r==null || !r.gameObject.activeInHierarchy || r.GetComponentInParent<Part>()!=part);
        KSPUtil.RemoveNonHighlightableRenderers(renderers);
        // The rim-highlight list and the outline Highlighter have separate caches.
        part.HighlightRenderer=renderers;
        part.RefreshHighlighter();
        if(part.highlighter!=null) {
            bool highlighted=part.HighlightActive,recursive=part.RecurseHighlight;
            // Highlight skips unchanged colors, so reapply an active selection to
            // new renderers without toggling any attached parts' highlight state.
            part.SetHighlight(false,false);
            if(highlighted)part.SetHighlight(true,false);
            part.RecurseHighlight=recursive;
        }
    }
}
