namespace ProceduralFairings
{
    class PFSettings : GameParameters.CustomParameterNode
    {
        public override string Title => "Procedural Fairings Settings";
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override string Section => "Procedural Fairings";
        public override string DisplaySection => Section;
        public override int SectionOrder => 1;
        public override bool HasPresets => false;

        [GameParameters.CustomIntParameterUI("Maximum Part Diameter", toolTip = "The maximum diameter of a Procedural Fairings part", minValue = 5, maxValue = 500, stepSize = 1, displayFormat = "N0")]
        public int maxDiameter = 50;

        [GameParameters.CustomParameterUI("Show Decouple Node Hint", toolTip = "Show the decoupling node hints in the Adapter parts")]
        public bool showNodeHint = true;

    }
}
