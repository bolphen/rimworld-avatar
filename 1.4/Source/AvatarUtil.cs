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
        // stores the pawn, if any, and the avatar texture
        public static AvatarManager manager;

        static AvatarUtil()
        {
            new Harmony("AvatarMod").PatchAll();
            manager = new ();
        }
    }

    [HarmonyPatch(typeof(InspectPaneUtility), "DoTabs")]
    public static class UIPatch
    {
        public static void Postfix(IInspectPane pane)
        {
            if (pane is MainTabWindow_Inspect inspectPanel && inspectPanel.OpenTabType is null)
            {
                Pawn pawn = null;
                if (inspectPanel.SelThing is Pawn selectedPawn)
                    pawn = selectedPawn;
                else if (inspectPanel.SelThing is Corpse corpse && !corpse.IsDessicated())
                    pawn = corpse.InnerPawn;
                if (pawn != null && (pawn.def.race?.Humanlike ?? false))
                {
                    AvatarUtil.manager.SetPawn(pawn);
                    Texture2D avatar = AvatarUtil.manager.GetAvatar();
                    float width = 200f;
                    float height = 200f*avatar.height/avatar.width;
                    Rect rect = new(0, inspectPanel.PaneTopY - InspectPaneUtility.TabHeight - height, width, height);
                    GUI.DrawTexture(rect, avatar);
                    if (Event.current.type == EventType.MouseDown && Mouse.IsOver(rect))
                    { // capture mouse click
                        Event.current.Use();
                    }
                }
            }
        }
    }
}
