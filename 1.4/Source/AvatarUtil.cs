using HarmonyLib;
using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace Avatar
{
    [StaticConstructorOnStartup]
    public static class AvatarUtil
    {
        static AvatarUtil()
        {
            new Harmony("AvatarMod").PatchAll();

            // add the CompAvatar to all humanlikes
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
            {
                if (def.race?.Humanlike ?? false)
                {
                    def.comps.Add(new CompProperties
                    {
                        compClass = typeof(CompAvatar)
                    });
                }
            }
        }
    }

    [HarmonyPatch(typeof(UIRoot_Play), "UIRootOnGUI")]
    public static class UIPatch
    {
        public static void Postfix()
        {
            if (Find.Selector.SelectedPawns.Count == 1)
            {
                Pawn pawn = Find.Selector.SelectedPawns.First();
                CompAvatar comp = pawn.GetComp<CompAvatar>();
                if (comp != null && pawn.ageTracker.AgeBiologicalYears >= 3)
                {
                    MainTabWindow_Inspect window = Find.WindowStack.WindowOfType<MainTabWindow_Inspect>();
                    if (window != null)
                    {
                        Texture2D avatar = comp.GetAvatar();
                        float width = 200f;
                        float height = 200f*avatar.height/avatar.width;
                        Rect rect = new(0, window.PaneTopY - 30f - height, width, height);
                        GUI.DrawTexture(rect, avatar);
                    }
                }
            }
        }
    }
}
