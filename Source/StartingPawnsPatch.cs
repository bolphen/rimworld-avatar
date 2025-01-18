using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;

namespace Avatar
{
    // patch vanilla starting pawns to draw the avatars
    #if !v1_5
    [HarmonyPatch(typeof(Page_ConfigureStartingPawns), nameof(Page_ConfigureStartingPawns.DrawPortraitArea))]
    public static class StartingPawn_DrawPortraitArea_Patch
    {
        static AvatarMod mod = LoadedModManager.GetMod<AvatarMod>();
        public static void Postfix(Page_ConfigureStartingPawns __instance, Rect rect)
        {
            if (!mod.settings.hideMainAvatar)
            {
                Pawn pawn = __instance.curPawn;
                AvatarManager manager = AvatarMod.mainManager;
                manager.SetPawn(pawn);
                manager.drawHeadgear = __instance.renderHeadgear;
                manager.drawClothes = __instance.renderClothes;
                float w = Page_ConfigureStartingPawns.PawnPortraitSize.x;
                Rect avatarRect = new (rect.center.x + w, rect.yMin - 10f, w, w*1.2f);
                Texture avatar = manager.GetAvatar();
                GUI.DrawTexture(avatarRect, avatar);
            }
        }
    }
    #else
    [HarmonyPatch(typeof(StartingPawnUtility), nameof(StartingPawnUtility.DrawPortraitArea))]
    public static class StartingPawn_DrawPortraitArea_Patch
    {
        static AvatarMod mod = LoadedModManager.GetMod<AvatarMod>();
        public static void Postfix(Rect rect, int pawnIndex, bool renderClothes, bool renderHeadgear)
        {
            if (!mod.settings.hideMainAvatar)
            {
                Pawn pawn = StartingPawnUtility.StartingAndOptionalPawns[pawnIndex];
                AvatarManager manager = AvatarMod.mainManager;
                manager.SetPawn(pawn);
                manager.drawHeadgear = renderHeadgear;
                manager.drawClothes = renderClothes;
                float w = StartingPawnUtility.PawnPortraitSize.x;
                Rect avatarRect = new (rect.center.x + w, rect.yMin - 10f, w, w*1.2f);
                Texture avatar = manager.GetAvatar();
                GUI.DrawTexture(avatarRect, avatar);
            }
        }
    }
    #endif
}
