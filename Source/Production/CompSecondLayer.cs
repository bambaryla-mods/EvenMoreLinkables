using RimWorld;
using Verse;

namespace Hobbes.MoreLinkablesProduction
{
    public class CompProperties_SecondLayer : CompProperties
    {
        public GraphicData graphicData = null;

        public CompProperties_SecondLayer()
        {
            compClass = typeof(CompSecondLayer);
        }
    }

    public class CompSecondLayer : ThingComp
    {
        private Graphic graphicInt;

        private float Y => parent.def.altitudeLayer.AltitudeFor() + 0.2f;

        public Graphic Graphic
        {
            get
            {
                if (graphicInt == null)
                {
                    graphicInt = Props.graphicData.GraphicColoredFor(parent);
                }
                return graphicInt;
            }
        }

        public CompProperties_SecondLayer Props => (CompProperties_SecondLayer)props;

        public override void PostDraw()
        {
            Graphic.Draw(GenThing.TrueCenter(parent.Position, parent.Rotation, parent.def.size, Y), parent.Rotation, parent);
        }
    }
}
