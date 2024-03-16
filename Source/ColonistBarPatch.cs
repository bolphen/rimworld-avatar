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

    // patch vanilla colonist bar drawing function to use the avatars
    [HarmonyPatch(typeof(ColonistBarColonistDrawer), "DrawColonist")]
    public static class ColonistBar_Transpiler_Patch
    {
        public static Texture GetPortrait(Pawn pawn, Vector2 size, Rot4 rotation, Vector3 cameraOffset = default(Vector3), float cameraZoom = 1f, bool supersample = true, bool compensateForUIScale = true, bool renderHeadgear = true, bool renderClothes = true, IReadOnlyDictionary<Apparel, Color> overrideApparelColors = null, Color? overrideHairColor = null, bool stylingStation = false, PawnHealthState? healthStateOverride = null)
        {
            AvatarMod mod = LoadedModManager.GetMod<AvatarMod>();
            if (mod.settings.showInColonistBar)
            {
                return mod.GetColonistBarAvatar(pawn);
            }
            return PortraitsCache.Get(pawn, size, rotation, cameraOffset, cameraZoom, supersample, compensateForUIScale, renderHeadgear, renderClothes, overrideApparelColors, overrideHairColor, stylingStation, healthStateOverride);
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {

                if (instruction.Calls(AccessTools.Method(typeof(PortraitsCache), nameof(PortraitsCache.Get))))
                {
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method("ColonistBar_Transpiler_Patch:GetPortrait"));
                }
                else
                {
                    yield return instruction;
                }
            }
        }
    }

    // patch CCMBar drawing function to use the avatars
    [HarmonyPatch]
    public static class CCMBar_Transpiler_Patch
    {
        public static MethodInfo target;
        public static bool Prepare()
        {
            if (ModCompatibility.CCMBar_Loaded)
            {
                target = AccessTools.Method("ColoredMoodBar13.MoodPatch:DrawColonist");
                return target != null;
            }
            return false;
        }
        public static MethodBase TargetMethod()
        {
            return target;
        }
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => ColonistBar_Transpiler_Patch.Transpiler(instructions);
    }

    // patch ColonyGroups drawing function to use the avatars
    [HarmonyPatch]
    public static class ColonyGroups_Transpiler_Patch
    {
        public static MethodInfo target;
        public static bool Prepare()
        {
            if (ModCompatibility.ColonyGroups_Loaded)
            {
                target = AccessTools.Method("TacticalGroups.TacticalGroups_ColonistBarColonistDrawer:DrawColonist");
                return target != null;
            }
            return false;
        }
        public static MethodBase TargetMethod()
        {
            return target;
        }
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => ColonistBar_Transpiler_Patch.Transpiler(instructions);
    }

    // patch GetRect for vanilla colonist bar drawing function
    [HarmonyPatch(typeof(ColonistBarColonistDrawer), "GetPawnTextureRect")]
    class ColonistBar_GetRect_Patch
    {
        public static bool Prefix(ref Rect __result, Vector2 pos)
        {
            if (LoadedModManager.GetMod<AvatarMod>().settings.showInColonistBar)
            {
                float adjust = LoadedModManager.GetMod<AvatarMod>().settings.showInColonistBarSizeAdjust;
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
        public static MethodInfo target;
        public static bool Prepare()
        {
            if (ModCompatibility.ColonyGroups_Loaded)
            {
                target = AccessTools.Method("TacticalGroups.TacticalGroups_ColonistBarColonistDrawer:GetPawnTextureRect");
                return target != null;
            }
            return false;
        }
        public static MethodBase TargetMethod()
        {
            return target;
        }
        public static bool Prefix(ref Rect __result, Vector2 pos)
        {
            if (LoadedModManager.GetMod<AvatarMod>().settings.showInColonistBar)
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
        public static MethodInfo target;
        public static bool Prepare()
        {
            if (ModCompatibility.CCMBar_Loaded)
            {
                target = AccessTools.Method("ColoredMoodBar13.MoodCache:GetPawnTextureRect");
                return target != null;
            }
            return false;
        }
        public static MethodBase TargetMethod()
        {
            return target;
        }
        static bool Prefix(ref object __instance, ref Rect __result, Vector2 pos)
        {
            if (LoadedModManager.GetMod<AvatarMod>().settings.showInColonistBar)
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
        public static MethodInfo target;
        public static bool Prepare()
        {
            if (ModCompatibility.CCMBar_Loaded && ModCompatibility.ColonyGroups_Loaded)
            {
                target = AccessTools.Method("ColoredMoodBar13.MoodCache:GetPawnTextureRectCG");
                return target != null;
            }
            return false;
        }
        public static MethodBase TargetMethod()
        {
            return target;
        }
        static bool Prefix(ref Rect __result, Vector2 pos) => ColonyGroups_GetRect_Patch.Prefix(ref __result, pos);
    }

    // patch FacialAnimation colonist bar update function
    [HarmonyPatch]
    class FA_UpdatePortrait_Patch
    {
        public static MethodInfo target;
        public static bool Prepare()
        {
            if (ModCompatibility.FacialAnimation_Loaded)
            {
                target = AccessTools.Method("FacialAnimation.FacialAnimationControllerComp:UpdatePortrait");
                return target != null;
            }
            return false;
        }
        public static MethodBase TargetMethod()
        {
            return target;
        }
        public static void SetDirty(Pawn pawn)
        {
            if (!LoadedModManager.GetMod<AvatarMod>().settings.showInColonistBar)
            {
                PortraitsCache.SetDirty(pawn);
            }
        }
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
                if (instruction.Calls(AccessTools.Method(typeof(PortraitsCache), "SetDirty")))
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method("FA_UpdatePortrait_Patch:SetDirty"));
                else
                    yield return instruction;
        }
    }
}
