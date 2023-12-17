using HarmonyLib;
using RimWorld;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Avatar
{
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        public static bool CMBar_Loaded = ModsConfig.IsActive("crashm.colorcodedmoodbar.11");

        static HarmonyInit ()
        {
            new Harmony("AvatarMod").PatchAll();
        }
    }

    [HarmonyPatch(typeof(ColonistBarColonistDrawer), "DrawColonist")]
    public static class ColonistBar_Transpiler_Patch
    {
        public static Texture GetPortrait(Pawn pawn, Vector2 size, Rot4 rotation, Vector3 cameraOffset = default(Vector3), float cameraZoom = 1f, bool supersample = true, bool compensateForUIScale = true, bool renderHeadgear = true, bool renderClothes = true, IReadOnlyDictionary<Apparel, Color> overrideApparelColors = null, Color? overrideHairColor = null, bool stylingStation = false, PawnHealthState? healthStateOverride = null)
        {
            AvatarMod mod = LoadedModManager.GetMod<AvatarMod>();
            if (mod.settings.showInColonistBar)
            {
                return mod.GetAvatar(pawn, new Color(0,0,0,0));
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

    [HarmonyPatch]
    public static class CMBar_Transpiler_Patch
    {
        public static MethodInfo target;
        public static bool Prepare()
        {
            if (HarmonyInit.CMBar_Loaded)
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

    [HarmonyPatch(typeof(ColonistBarColonistDrawer), "GetPawnTextureRect")]
    class ColonistBar_GetRect_Patch
    {
        public static bool Prefix(ref Rect __result, Vector2 pos)
        {
            if (LoadedModManager.GetMod<AvatarMod>().settings.showInColonistBar)
            {
                float adjust = LoadedModManager.GetMod<AvatarMod>().settings.showInColonistBarSizeAdjust;
                float x = pos.x;
                float y = pos.y;
                float width = (ColonistBarColonistDrawer.PawnTextureSize.x+2f*adjust)*Find.ColonistBar.Scale;
                Vector2 vector = new (width, width*1.2f);
                __result = new Rect (x-adjust, y - (vector.y - Find.ColonistBar.Size.y) - 1f, vector.x, vector.y).ContractedBy (1f);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    class CMBar_GetRect_Patch
    {
        public static MethodInfo target;
        public static bool Prepare()
        {
            if (HarmonyInit.CMBar_Loaded)
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
        static bool Prefix(ref Rect __result, Vector2 pos) => ColonistBar_GetRect_Patch.Prefix(ref __result, pos);
    }
}
