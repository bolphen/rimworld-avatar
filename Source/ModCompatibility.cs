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
        public static bool DBH_Loaded = ModsConfig.IsActive("Dubwise.DubsBadHygiene");
        public static bool FacialAnimation_Loaded = ModsConfig.IsActive("Nals.FacialAnimation");
        public static bool GradientHair_Loaded = ModsConfig.IsActive("automatic.gradienthair");
        public static bool Portraits_Loaded = ModsConfig.IsActive("twopenny.portraitsoftherim");
        public static bool RegrowthCore_Loaded = ModsConfig.IsActive("ReGrowth.BOTR.Core");
        public static bool RJW_Loaded = ModsConfig.IsActive("rim.job.world");
        public static bool VanillaFactionsExpanded_Loaded = ModsConfig.IsActive("OskarPotocki.VanillaFactionsExpanded.Core");
        public static bool VREHighmate_Loaded = ModsConfig.IsActive("vanillaracesexpanded.highmate");

        private static Dictionary<string, FieldInfo> cachedFieldInfo = new ();
        private static Dictionary<string, MethodInfo> cachedMethodInfo = new ();
        private static Dictionary<string, Def> cachedDef = new ();
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

        public static bool GetDBHNudity(Pawn pawn)
        {
            if (!cachedDef.ContainsKey("DubsBadHygiene.DubDef:Washing"))
                cachedDef["DubsBadHygiene.DubDef:Washing"] = AccessTools.StaticFieldRefAccess<Def>("DubsBadHygiene.DubDef:Washing");
            return pawn.health.hediffSet.HasHediff((HediffDef) cachedDef["DubsBadHygiene.DubDef:Washing"]);
        }

        public static bool GetRegrowthNudity(Pawn pawn)
        {
            #if v1_3
            return false;
            #else
            return (bool) GetMethodInfo("ReGrowthCore.ReGrowthUtils:IsBathingNow").Invoke(null, new object[] {pawn});
            #endif
        }

        public static bool GetRJWNudity(Pawn pawn)
        {
            #if !(v1_3 || v1_4)
            if (!cachedMethodInfo.ContainsKey("RJW:GetComp_CompRJW"))
                cachedMethodInfo["RJW:GetComp_CompRJW"] = AccessTools.Method(typeof(Pawn), "GetComp", null, new Type[] {AccessTools.TypeByName("rjw.CompRJW")});
            var compRJW = cachedMethodInfo["RJW:GetComp_CompRJW"].Invoke(pawn, null);
            if (compRJW != null)
            {
                return (bool) GetFieldInfo("rjw.CompRJW:drawNude").GetValue(compRJW);
            }
            #endif
            return false;
        }

        public static bool GetVREHighmateNudity(Pawn pawn)
        {
            if (!cachedDef.ContainsKey("VanillaRacesExpandedHighmate.InternalDefOf:VRE_Naked"))
                cachedDef["VanillaRacesExpandedHighmate.InternalDefOf:VRE_Naked"] = AccessTools.StaticFieldRefAccess<Def>("VanillaRacesExpandedHighmate.InternalDefOf:VRE_Naked");
            return pawn.health.hediffSet.HasHediff((HediffDef) cachedDef["VanillaRacesExpandedHighmate.InternalDefOf:VRE_Naked"]);
        }

        public static bool ModdedNudity(Pawn pawn)
        {
            bool nudity = false;
            if (ModCompatibility.DBH_Loaded)
                nudity |= ModCompatibility.GetDBHNudity(pawn);
            if (ModCompatibility.RegrowthCore_Loaded)
                nudity |= ModCompatibility.GetRegrowthNudity(pawn);
            if (ModCompatibility.RJW_Loaded)
                nudity |= ModCompatibility.GetRJWNudity(pawn);
            if (ModCompatibility.VREHighmate_Loaded)
                nudity |= ModCompatibility.GetVREHighmateNudity(pawn);
            return nudity;
        }
    }
}
