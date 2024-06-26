using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;
using HarmonyLib;

namespace Avatar
{
    [StaticConstructorOnStartup]
    public static class ModCompatibility
    {
        public static bool CCMBar_Loaded = ModsConfig.IsActive("crashm.colorcodedmoodbar.11");
        public static bool ColonyGroups_Loaded = ModsConfig.IsActive("DerekBickley.LTOColonyGroupsFinal");
        public static bool FacialAnimation_Loaded = ModsConfig.IsActive("Nals.FacialAnimation");
        public static bool GradientHair_Loaded = ModsConfig.IsActive("automatic.gradienthair");
        public static bool Portraits_Loaded = ModsConfig.IsActive("twopenny.portraitsoftherim");
        public static bool VanillaFactionsExpanded_Loaded = ModsConfig.IsActive("OskarPotocki.VanillaFactionsExpanded.Core");

        private static Dictionary<string, FieldInfo> cachedFieldInfo = new ();
        private static Dictionary<string, MethodInfo> cachedMethodInfo = new ();
        public static FieldInfo GetFieldInfo(string fieldName)
        {
            if (!cachedFieldInfo.ContainsKey(fieldName))
                cachedFieldInfo[fieldName] = AccessTools.Field(fieldName);
            return cachedFieldInfo[fieldName];
        }
        public static MethodInfo GetMethodInfo(string methodName)
        {
            if (!cachedMethodInfo.ContainsKey(methodName))
                cachedMethodInfo[methodName] = AccessTools.Method(methodName);
            return cachedMethodInfo[methodName];
        }

        #if v1_3 || v1_4
        public static float GetVEOffset(ThingDef def)
        {
            if (!cachedMethodInfo.ContainsKey("VFECore:GetModExtension_ApparelDrawPosExtension"))
                cachedMethodInfo["VFECore:GetModExtension_ApparelDrawPosExtension"] = AccessTools.Method(typeof(Def), "GetModExtension", null, new Type[] {AccessTools.TypeByName("VFECore.ApparelDrawPosExtension")});
            var apparelDrawPosExtension = cachedMethodInfo["VFECore:GetModExtension_ApparelDrawPosExtension"].Invoke(def, null);
            if (apparelDrawPosExtension != null)
            {
                var drawSettings = GetFieldInfo("VFECore.ApparelDrawPosExtension:apparelDrawSettings").GetValue(apparelDrawPosExtension);
                if (drawSettings != null)
                    return ((Vector3) GetMethodInfo("VFECore.DrawSettings:GetDrawPosOffset").Invoke(drawSettings, new object[] {Rot4.South, new Vector3 (0,0,0)})).y;
                drawSettings = GetFieldInfo("VFECore.ApparelDrawPosExtension:shellPosDrawSettings").GetValue(apparelDrawPosExtension);
                if (drawSettings != null)
                    return ((Vector3) GetMethodInfo("VFECore.DrawSettings:GetDrawPosOffset").Invoke(drawSettings, new object[] {Rot4.South, new Vector3 (0,0,0)})).y;
            }
            return 0f;
        }
        #endif

        public static (string, Color)? GetGradientHair(Pawn pawn)
        {
            if (!cachedMethodInfo.ContainsKey("GradientHair:GetComp_CompGradientHair"))
                cachedMethodInfo["GradientHair:GetComp_CompGradientHair"] = AccessTools.Method(typeof(Pawn), "GetComp", null, new Type[] {AccessTools.TypeByName("GradientHair.CompGradientHair")});
            var compGradientHair = cachedMethodInfo["GradientHair:GetComp_CompGradientHair"].Invoke(pawn, null);
            if (compGradientHair != null)
            {
                var settings = GetFieldInfo("GradientHair.CompGradientHair:settings").GetValue(compGradientHair);
                if (settings != null && (bool) GetFieldInfo("GradientHair.GradientHairSettings:enabled").GetValue(settings))
                {
                    return (
                        (string) GetFieldInfo("GradientHair.GradientHairSettings:mask").GetValue(settings),
                        (Color) GetFieldInfo("GradientHair.GradientHairSettings:colorB").GetValue(settings)
                    );
                }
            }
            return null;
        }

        public static bool PortraitShown(Pawn pawn)
        {
            var portrait = GetMethodInfo("PortraitsOfTheRim.PortraitUtils:GetPortrait").Invoke(null, new object[] {pawn});
            if (portrait != null)
            {
                return !(bool) GetFieldInfo("PortraitsOfTheRim.Portrait:hidePortrait").GetValue(portrait);
            }
            return false;
        }
    }
}

