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
                if (pawn != null && pawn.RaceProps.Humanlike)
                {
                    AvatarUtil.manager.SetPawn(pawn);
                    Texture2D avatar = AvatarUtil.manager.GetAvatar();
                    float width = 160;
                    float height = 160*avatar.height/avatar.width;
                    Rect rect = new(0, inspectPanel.PaneTopY - InspectPaneUtility.TabHeight - height, width, height);
                    GUI.DrawTexture(rect, avatar);
                    if (Event.current.type == EventType.MouseDown && Mouse.IsOver(rect))
                    { // capture mouse click
                        if (Event.current.button == 0) // leftbutton
                            AvatarUtil.manager.ToggleDrawHeadgear();
                        Event.current.Use();
                    }
                }
            }
        }
    }

    // redraw avatar whenever ingame portrait got redrawn
    [HarmonyPatch(typeof(PortraitsCache), "SetDirty")]
    public static class AvatarUpdateHookPatch
    {
        public static void Postfix(Pawn pawn)
        {
            if (pawn == AvatarUtil.manager.pawn)
                AvatarUtil.manager.ClearCachedAvatar();
        }
    }

    // redraw avatar when pawn ages (for wrinkles)
    [HarmonyPatch(typeof(Pawn_AgeTracker), "BirthdayBiological")]
    public static class Pawn_AgeTracker_BirthdayBiological_Patch
    {
        public static void Postfix(ref Pawn_AgeTracker __instance)
        {
            if (__instance.pawn == AvatarUtil.manager.pawn)
                AvatarUtil.manager.ClearCachedAvatar();
        }
    }

}
