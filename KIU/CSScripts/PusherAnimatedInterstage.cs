// Stock ModuleAnimateGeneric remains responsible for motion, saving and PAW controls.
// Unlike a blind Toggle at staging, this always requests extension, including
// when the user has already manually extended the pushers before separating.
public class PusherAnimatedInterstage : PartModule
{
    private ModuleAnimateGeneric animation;
    public override void OnStart(StartState state)
    {
        animation = part.FindModuleImplementing<ModuleAnimateGeneric>();
    }
    public override void OnActive()
    {
        if (animation == null) animation = part.FindModuleImplementing<ModuleAnimateGeneric>();
        // In stock MAG animSwitch=true means the next Toggle requests extension.
        if (animation != null && animation.animSwitch) animation.Toggle();
    }
}
