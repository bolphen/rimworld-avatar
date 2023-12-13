using HarmonyLib;
using RimWorld;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using System.IO;

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

    public class AvatarMod : Mod
    {
        public AvatarSettings settings;
        public static Dictionary<string, Texture2D> cachedTextures;
        public static AvatarManager manager; // stores the pawn, if any, and the avatar texture

        [DebugAction("Avatar", "Reload Textures")]
        public static void ClearCachedTextures()
        {
            foreach (KeyValuePair<string, Texture2D> kvp in cachedTextures)
                UnityEngine.Object.Destroy(kvp.Value);
            cachedTextures.Clear();
            manager.ClearCachedAvatar();
        }

        public Texture2D GetTexture(string texPath)
        {
            if (!cachedTextures.ContainsKey(texPath))
            {
                string path = Content.RootDir+"/Assets/"+texPath+".png";
                if (!File.Exists(path))
                { // fallback to RW texture manager
                    return ContentFinder<Texture2D>.Get(texPath);
                }
                Texture2D newTexture = new (1, 1);
                newTexture.LoadImage(File.ReadAllBytes(path));
                cachedTextures[texPath] = newTexture;
            }
            return cachedTextures[texPath];
        }

        public AvatarMod(ModContentPack content) : base(content)
        {
            manager = new (this);
            settings = GetSettings<AvatarSettings>();
            cachedTextures = new ();
        }

        public override string SettingsCategory() => "Avatar";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            settings.avatarWidth = (float)Math.Round(
                listingStandard.SliderLabeled("Avatar size", settings.avatarWidth, 80f, 320f));
            listingStandard.CheckboxLabeled("Compression", ref settings.avatarCompression,
                "Whether textures should be compressed. Avatars will look slightly more natural but also have more artifacts.");
            listingStandard.CheckboxLabeled("Scaling", ref settings.avatarScaling,
                "Whether avatars should be algorithmically scaled up for a smoother look.");
            listingStandard.CheckboxLabeled("Draw headgear by default", ref settings.defaultDrawHeadgear,
                "Whether headgear should be drawn by default. Can always be toggled by clicking on the avatar.");
            string samplePath = "UI/AvatarSample" + (settings.avatarCompression ? "C" : "") + (settings.avatarScaling ? "S" : "");
            Texture2D avatar = GetTexture(samplePath);
            avatar.filterMode = FilterMode.Point;
            float width = settings.avatarWidth;
            float height = width*avatar.height/avatar.width;
            listingStandard.ButtonImage(avatar, width, height);
            listingStandard.End();
            manager.ClearCachedAvatar();
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
                    AvatarMod.manager.SetPawn(pawn);
                    Texture2D avatar = AvatarMod.manager.GetAvatar();
                    float width = LoadedModManager.GetMod<AvatarMod>().settings.avatarWidth;
                    float height = width*avatar.height/avatar.width;
                    Rect rect = new(0, inspectPanel.PaneTopY - InspectPaneUtility.TabHeight - height, width, height);
                    GUI.DrawTexture(rect, avatar);
                    if (Event.current.type == EventType.MouseDown && Mouse.IsOver(rect))
                    { // capture mouse click
                        if (Event.current.button == 0) // leftbutton
                            AvatarMod.manager.ToggleDrawHeadgear();
                        /* else if (Event.current.button == 1) // rightbutton */
                        /*     Find.WindowStack.Add(AvatarMod.manager.GetFloatMenu()); */
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
            if (pawn == AvatarMod.manager.pawn)
                AvatarMod.manager.ClearCachedAvatar();
        }
    }

    // redraw avatar when pawn ages (for wrinkles)
    [HarmonyPatch(typeof(Pawn_AgeTracker), "BirthdayBiological")]
    public static class Pawn_AgeTracker_BirthdayBiological_Patch
    {
        public static void Postfix(ref Pawn_AgeTracker __instance)
        {
            if (__instance.pawn == AvatarMod.manager.pawn)
                AvatarMod.manager.ClearCachedAvatar();
        }
    }

    public class AvatarSettings : ModSettings
    {
        public float avatarWidth = 160f;
        public bool avatarCompression = false;
        public bool avatarScaling = true;
        public bool defaultDrawHeadgear = true;

        public void ToggleScaling()
        {
            avatarScaling = !avatarScaling;
            Write();
        }

        public void ToggleCompression()
        {
            avatarCompression = !avatarCompression;
            Write();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref avatarWidth, "avatarWidth");
            Scribe_Values.Look(ref avatarCompression, "avatarCompression");
            Scribe_Values.Look(ref avatarScaling, "avatarScaling");
            Scribe_Values.Look(ref defaultDrawHeadgear, "defaultDrawHeadgear");
        }
    }
}
