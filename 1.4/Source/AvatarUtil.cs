using HarmonyLib;
using RimWorld;
using System.Linq;
using System.Collections.Generic;
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

    [HarmonyPatch(typeof(InspectPaneUtility), "DoTabs")]
    public static class UIPatch
    {
        public static void Postfix(IInspectPane pane)
        {
            if (pane is MainTabWindow_Inspect inspectPanel && inspectPanel.OpenTabType is null)
            {
                CompAvatar comp = null;
                if (inspectPanel.SelThing is Pawn pawn)
                    comp = pawn.GetComp<CompAvatar>();
                else if (inspectPanel.SelThing is Corpse corpse && !corpse.IsDessicated())
                    comp = corpse.InnerPawn.GetComp<CompAvatar>();
                if (comp != null)
                {
                    Texture2D avatar = comp.GetAvatar();
                    float width = 200f;
                    float height = 200f*avatar.height/avatar.width;
                    Rect rect = new(0, inspectPanel.PaneTopY - InspectPaneUtility.TabHeight - height, width, height);
                    GUI.DrawTexture(rect, avatar);
                    if (Event.current.type == EventType.MouseDown && Mouse.IsOver(rect))
                    {
                        if (Event.current.button == 1) // rightbutton
                        {
                            Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>()
                                {
                                    // new FloatMenuOption("Randomize features", (() => comp.Randomize()), MenuOptionPriority.Default),
                                    new FloatMenuOption("Save as png", (() => comp.SaveAsPng()), MenuOptionPriority.Default)
                                })
                            );
                        }
                        Event.current.Use();
                    }
                }
            }
        }
    }
}
