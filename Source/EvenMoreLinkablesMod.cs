using HarmonyLib;
using Verse;

namespace EvenMoreLinkables
{
    public class EvenMoreLinkablesMod : Mod
    {
        public EvenMoreLinkablesMod(ModContentPack pack) : base(pack)
        {
            new Harmony("EvenMoreLinkablesMod").PatchAll();
        }
    }
}
