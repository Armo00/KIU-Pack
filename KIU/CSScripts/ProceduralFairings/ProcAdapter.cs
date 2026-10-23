//  ==================================================
//  Procedural Fairings plug-in by Alexey Volynskov.

//  Licensed under CC-BY-4.0 terms: https://creativecommons.org/licenses/by/4.0/legalcode
//  ==================================================

using System.Collections;
using UnityEngine;

namespace Keramzit
{
    public class ProceduralFairingAdapter : PartModule
    {
        // Leave these fields here, so a legacy loader can reference them.
        [KSPField(isPersistant = true)] public float baseSize;
        [Persistent] public float topSize;
        [Persistent] public float height;
        [Persistent] public float extraHeight;
        [KSPField] public string topNodeName = "top1";
        [KSPField (isPersistant = true)] public bool topNodeDecouplesWhenFairingsGone;

        public override void OnStartFinished(StartState state)
        {
            base.OnStartFinished(state);
            StartCoroutine(DestroyMe());
        }
        private IEnumerator DestroyMe()
        {
            yield return new WaitForSeconds(1);
            Destroy(this);
        }
    }
}
