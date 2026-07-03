using UnityEngine;

namespace YourMod
{
    public class MyInterstage : PartModule
    {
        private ModuleAnimateGeneric anim;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            anim = part.FindModuleImplementing<ModuleAnimateGeneric>();
        }

        public override void OnActive()
        {
            base.OnActive();
            if (anim != null)
            {
                anim.Toggle();
            }
        }
    }
}