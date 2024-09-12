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
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        static HarmonyInit ()
        {
            new Harmony("AvatarMod").PatchAll();
        }
    }

    [HarmonyPatch(typeof(PrefsData), nameof(PrefsData.Apply))]
    public static class GlobalHatSetting_Patch
    {
        public static void Postfix()
        {
            AvatarMod.ClearCachedAvatars();
        }
    }

    // patch vanilla colonist bar drawing function to use the avatars
    [HarmonyPatch(typeof(ColonistBarColonistDrawer), nameof(ColonistBarColonistDrawer.DrawColonist))]
    public static class ColonistBar_Transpiler_Patch
    {
        private static AvatarMod mod = LoadedModManager.GetMod<AvatarMod>();

        #if v1_3
        private static Texture GetPortrait(Pawn pawn, Vector2 size, Rot4 rotation, Vector3 cameraOffset = default(Vector3), float cameraZoom = 1f, bool supersample = true, bool compensateForUIScale = true, bool renderHeadgear = true, bool renderClothes = true, Dictionary<Apparel, Color> overrideApparelColors = null, Color? overrideHairColor = default(Color?), bool stylingStation = false)
        #else
        private static Texture GetPortrait(Pawn pawn, Vector2 size, Rot4 rotation, Vector3 cameraOffset = default(Vector3), float cameraZoom = 1f, bool supersample = true, bool compensateForUIScale = true, bool renderHeadgear = true, bool renderClothes = true, IReadOnlyDictionary<Apparel, Color> overrideApparelColors = null, Color? overrideHairColor = null, bool stylingStation = false, PawnHealthState? healthStateOverride = null)
        #endif
        {
            if (mod.settings.showInColonistBar)
            {
                // vanilla ignores the "renderHeadgear" parameter anyway...
                return mod.GetColonistBarAvatar(pawn, !Prefs.HatsOnlyOnMap, !ModCompatibility.ModdedNudity(pawn));
            }
            #if v1_3
            return PortraitsCache.Get(pawn, size, rotation, cameraOffset, cameraZoom, supersample, compensateForUIScale, renderHeadgear, renderClothes, overrideApparelColors, overrideHairColor, stylingStation);
            #else
            return PortraitsCache.Get(pawn, size, rotation, cameraOffset, cameraZoom, supersample, compensateForUIScale, renderHeadgear, renderClothes, overrideApparelColors, overrideHairColor, stylingStation, healthStateOverride);
            #endif
        }

        private static MethodInfo oldMethod = AccessTools.Method("PortraitsCache:Get");
        private static MethodInfo newMethod = AccessTools.Method("ColonistBar_Transpiler_Patch:GetPortrait");
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction.Calls(oldMethod) ? new CodeInstruction(OpCodes.Call, newMethod) : instruction;
            }
        }
    }

    // patch CCMBar drawing function to use the avatars
    [HarmonyPatch]
    public static class CCMBar_Transpiler_Patch
    {
        private static MethodInfo target;
        public static MethodBase TargetMethod() => target;
        public static bool Prepare()
        {
            if (ModCompatibility.CCMBar_Loaded)
            {
                target = AccessTools.Method("ColoredMoodBar13.MoodPatch:DrawColonist");
                return target != null;
            }
            return false;
        }
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => ColonistBar_Transpiler_Patch.Transpiler(instructions);
    }

    // patch ColonyGroups drawing function to use the avatars
    [HarmonyPatch]
    public static class ColonyGroups_Transpiler_Patch
    {
        private static MethodInfo target;
        public static MethodBase TargetMethod() => target;
        public static bool Prepare()
        {
            if (ModCompatibility.ColonyGroups_Loaded)
            {
                target = AccessTools.Method("TacticalGroups.TacticalGroups_ColonistBarColonistDrawer:DrawColonist");
                return target != null;
            }
            return false;
        }
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => ColonistBar_Transpiler_Patch.Transpiler(instructions);
    }

    // patch GetRect for vanilla colonist bar drawing function
    [HarmonyPatch(typeof(ColonistBarColonistDrawer), nameof(ColonistBarColonistDrawer.GetPawnTextureRect))]
    class ColonistBar_GetRect_Patch
    {
        private static AvatarMod mod = LoadedModManager.GetMod<AvatarMod>();
        public static bool Prefix(ref Rect __result, Vector2 pos)
        {
            if (mod.settings.showInColonistBar)
            {
                float adjust = mod.settings.showInColonistBarSizeAdjust;
                float width = (ColonistBarColonistDrawer.PawnTextureSize.x+2f*adjust)*Find.ColonistBar.Scale;
                Vector2 vector = new (width, width*1.2f);
                __result = new Rect (pos.x-adjust, pos.y - (vector.y - Find.ColonistBar.Size.y) - 1f, vector.x, vector.y).ContractedBy (1f);
                return false;
            }
            return true;
        }
    }

    // patch GetRect for ColonyGroups drawing function
    [HarmonyPatch]
    class ColonyGroups_GetRect_Patch
    {
        private static AvatarMod mod = LoadedModManager.GetMod<AvatarMod>();
        private static MethodInfo target;
        public static MethodBase TargetMethod() => target;
        public static bool Prepare()
        {
            if (ModCompatibility.ColonyGroups_Loaded)
            {
                target = AccessTools.Method("TacticalGroups.TacticalGroups_ColonistBarColonistDrawer:GetPawnTextureRect");
                return target != null;
            }
            return false;
        }
        public static bool Prefix(ref Rect __result, Vector2 pos)
        {
            if (mod.settings.showInColonistBar)
            {
                ModCompatibility.GetFieldInfo("TacticalGroups.TacticalGroups_ColonistBarColonistDrawer:PawnTextureSize").SetValue(null, new Vector2(40f, 48f));
                var bar = ModCompatibility.GetFieldInfo("TacticalGroups.TacticUtils:TacticalColonistBar").GetValue(null);
                float scale = (float) ModCompatibility.GetFieldInfo("TacticalGroups.TacticalGroupsSettings:PawnScale").GetValue(null);
                float size_y = ((Vector2) ModCompatibility.GetMethodInfo("TacticalGroups.TacticalColonistBar:get_Size").Invoke(bar, null)).y;
                float boxWidth = (float) ModCompatibility.GetFieldInfo("TacticalGroups.TacticalGroupsSettings:PawnBoxWidth").GetValue(null);
                float width = boxWidth+20*(scale-1f);
                Vector2 vector = new (width, width*1.2f);
                __result = new Rect (pos.x-10*(scale-1f), pos.y - (vector.y - size_y) - 1f, vector.x, vector.y).ContractedBy (1f);
                return false;
            }
            return true;
        }
    }

    // patch GetRect for CCMBar drawing function
    [HarmonyPatch]
    class CCMBar_GetRect_Patch
    {
        private static AvatarMod mod = LoadedModManager.GetMod<AvatarMod>();
        private static MethodInfo target;
        public static MethodBase TargetMethod() => target;
        public static bool Prepare()
        {
            if (ModCompatibility.CCMBar_Loaded)
            {
                target = AccessTools.Method("ColoredMoodBar13.MoodCache:GetPawnTextureRect");
                return target != null;
            }
            return false;
        }
        static bool Prefix(ref object __instance, ref Rect __result, Vector2 pos)
        {
            if (mod.settings.showInColonistBar)
            {
                if ((bool) ModCompatibility.GetFieldInfo("ColoredMoodBar13.MoodCache:ScalePortrait").GetValue(__instance))
                {
                    // the smaller portraits
                    float scale = (float) ModCompatibility.GetFieldInfo("ColoredMoodBar13.MoodCache:Scale").GetValue(__instance);
                    float width = ColonistBarColonistDrawer.PawnTextureSize.x*Find.ColonistBar.Scale*scale;
                    Vector2 vector = new (width, width*1.2f);
                    __result = new Rect (pos.x+1f, pos.y - (vector.y - Find.ColonistBar.Size.y*scale) - 1f, vector.x, vector.y).ContractedBy (1f);
                    return false;
                }
                return ColonistBar_GetRect_Patch.Prefix(ref __result, pos);
            }
            return true;
        }
    }

    // patch GetRectCG for CCMBar drawing function
    [HarmonyPatch]
    class CCMBar_ColonyGroups_GetRect_Patch
    {
        private static MethodInfo target;
        public static MethodBase TargetMethod() => target;
        public static bool Prepare()
        {
            if (ModCompatibility.CCMBar_Loaded && ModCompatibility.ColonyGroups_Loaded)
            {
                target = AccessTools.Method("ColoredMoodBar13.MoodCache:GetPawnTextureRectCG");
                return target != null;
            }
            return false;
        }
        static bool Prefix(ref Rect __result, Vector2 pos) => ColonyGroups_GetRect_Patch.Prefix(ref __result, pos);
    }

    // patch FacialAnimation colonist bar update function
    [HarmonyPatch]
    class FA_UpdatePortrait_Patch
    {
        private static AvatarMod mod = LoadedModManager.GetMod<AvatarMod>();
        private static MethodInfo target;
        public static MethodBase TargetMethod() => target;
        public static bool Prepare()
        {
            if (ModCompatibility.FacialAnimation_Loaded)
            {
                target = AccessTools.Method("FacialAnimation.FacialAnimationControllerComp:UpdatePortrait");
                return target != null;
            }
            return false;
        }
        public static void SetDirty(Pawn pawn)
        {
            if (!mod.settings.showInColonistBar)
            {
                PortraitsCache.SetDirty(pawn);
            }
        }
        private static MethodInfo oldMethod = AccessTools.Method("PortraitsCache:SetDirty");
        private static MethodInfo newMethod = AccessTools.Method("FA_UpdatePortrait_Patch:SetDirty");
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction.Calls(oldMethod) ? new CodeInstruction(OpCodes.Call, newMethod) : instruction;
            }
        }
    }
}
