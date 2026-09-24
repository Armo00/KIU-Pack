// Shared trigger; stock ModuleAnimateGeneric owns motion, persistence and PAW/AG.
public class PusherAnimatedInterstage : PartModule
{
    [KSPField] public bool triggerOnDecouple = false;
    [KSPField] public string decouplerNodeID = "";
    private ModuleAnimateGeneric animation;
    private ModuleDecouple decoupler;
    private bool wasSeparated;
    public override void OnStart(StartState state)
    {
        animation=part.FindModuleImplementing<ModuleAnimateGeneric>();
        foreach(PartModule module in part.Modules) {
            ModuleDecouple candidate=module as ModuleDecouple;
            if(candidate!=null && (decouplerNodeID.Length==0 || candidate.explosiveNodeID==decouplerNodeID)) {decoupler=candidate;break;}
        }
        wasSeparated=decoupler!=null && decoupler.isDecoupled;
    }
    private void RequestExtension()
    {
        if(animation==null)animation=part.FindModuleImplementing<ModuleAnimateGeneric>();
        if(animation!=null && animation.animSwitch)animation.Toggle();
    }
    public override void OnActive() { RequestExtension(); }
    public void FixedUpdate()
    {
        if(!triggerOnDecouple || !HighLogic.LoadedSceneIsFlight || decoupler==null)return;
        bool separated=decoupler.isDecoupled;
        if(separated && !wasSeparated)RequestExtension();
        wasSeparated=separated;
    }
}
