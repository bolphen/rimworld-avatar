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
        public static List<AvatarManager> managers;
        // each manager stores a pawn, if any, and the avatar texture

        [DebugAction("Avatar", "Reload Textures")]
        public static void ClearCachedTextures()
        {
            foreach (KeyValuePair<string, Texture2D> kvp in cachedTextures)
                UnityEngine.Object.Destroy(kvp.Value);
            cachedTextures.Clear();
            foreach (AvatarManager manager in managers)
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
            managers = new ();
            managers.Add(new AvatarManager(this));
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
            listingStandard.CheckboxLabeled("Show hair with headgear", ref settings.showHairWithHeadgear,
                "Whether hair should be drawn along with headgear. Doesn't look good for some modded hair styles so you may want to turn this off.");
            listingStandard.CheckboxLabeled("Show in quest tab (experimental)", ref settings.showInQuestTab);
            string samplePath = "UI/AvatarSample" + (settings.avatarCompression ? "C" : "") + (settings.avatarScaling ? "S" : "");
            Texture2D avatar = GetTexture(samplePath);
            avatar.filterMode = FilterMode.Point;
            float width = settings.avatarWidth;
            float height = width*avatar.height/avatar.width;
            listingStandard.ButtonImage(avatar, width, height);
            listingStandard.End();
        }

        public List<Texture2D> GetAvatar(List<Pawn> pawns)
        {
            int count = Math.Min(5, pawns.Count);
            while (managers.Count < count)
            {
                managers.Add(new AvatarManager(this));
            }
            List<Texture2D> result = new ();
            for (int i = 0; i < count; i++)
            {
                managers[i].SetPawn(pawns[i]);
                result.Add(managers[i].GetAvatar());
            }
            return result;
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
                    AvatarManager manager = AvatarMod.managers[0];
                    manager.SetPawn(pawn);
                    Texture2D avatar = manager.GetAvatar();
                    float width = LoadedModManager.GetMod<AvatarMod>().settings.avatarWidth;
                    float height = width*avatar.height/avatar.width;
                    Rect rect = new(0, inspectPanel.PaneTopY - InspectPaneUtility.TabHeight - height, width, height);
                    GUI.DrawTexture(rect, avatar);
                    if (Event.current.type == EventType.MouseDown && Mouse.IsOver(rect))
                    { // capture mouse click
                        if (Event.current.button == 0) // leftbutton
                            manager.ToggleDrawHeadgear();
                        /* else if (Event.current.button == 1) // rightbutton */
                        /*     Find.WindowStack.Add(manager.GetFloatMenu()); */
                        Event.current.Use();
                    }
                }
            }
        }
    }

    // basically borrrowed from Portraits of the Rim
    [HarmonyPatch(typeof(MainTabWindow_Quests), "DoFactionInfo")]
    public static class QuestWindowPatch
    {
        public static void Prefix(ref MainTabWindow_Quests __instance, Rect rect, ref float curY)
        {
            AvatarMod mod = LoadedModManager.GetMod<AvatarMod>();
            if (mod.settings.showInQuestTab)
            {
                List<Pawn> pawns = new();
                foreach (var part in __instance.selected.PartsListForReading)
                {
                    List<Pawn> partPawns;
                    if (part is QuestPart_Hyperlinks hyperlinks)
                        partPawns = hyperlinks.pawns?.Where(p => p.RaceProps.Humanlike).ToList();
                    else if (part is QuestPart_PawnsArrive pawnsArrive)
                        partPawns = pawnsArrive.pawns?.Where(p => p.RaceProps.Humanlike).ToList();
                    else if (part is QuestPart_ExtraFaction extraFaction)
                        partPawns = extraFaction.affectedPawns?.Where(p => p.RaceProps.Humanlike).ToList();
                    else
                        partPawns = part.QuestLookTargets.Where(x => x.Thing is Pawn p && p.RaceProps.Humanlike).Select(x => x.Thing).Cast<Pawn>().ToList();
                    if (partPawns.Count > pawns.Count)
                        pawns = partPawns;
                }
                if (pawns.Count > 0)
                {
                    List<Texture2D> avatars = mod.GetAvatar(pawns);
                    float width = 120f;
                    float height = width*1.2f;
                    for (int i = 0; i < avatars.Count; i++)
                    {
                        GUI.DrawTexture(new(rect.width - (width+5)*(i+1), curY+15, width, height), avatars[i]);
                    }
                    curY += height+15;
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
            foreach (AvatarManager manager in AvatarMod.managers)
                if (pawn == manager.pawn)
                    manager.ClearCachedAvatar();
        }
    }

    // redraw avatar when pawn ages (for wrinkles)
    [HarmonyPatch(typeof(Pawn_AgeTracker), "BirthdayBiological")]
    public static class Pawn_AgeTracker_BirthdayBiological_Patch
    {
        public static void Postfix(ref Pawn_AgeTracker __instance)
        {
            foreach (AvatarManager manager in AvatarMod.managers)
                if (__instance.pawn == manager.pawn)
                    manager.ClearCachedAvatar();
        }
    }

    public class AvatarSettings : ModSettings
    {
        public float avatarWidth = 160f;
        public bool avatarCompression = false;
        public bool avatarScaling = true;
        public bool defaultDrawHeadgear = true;
        public bool showHairWithHeadgear = true;
        public bool showInQuestTab = true;

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
            Scribe_Values.Look(ref showHairWithHeadgear, "showHairWithHeadgear");
            Scribe_Values.Look(ref showInQuestTab, "showInQuestTab");
            foreach (AvatarManager manager in AvatarMod.managers)
                manager.ClearCachedAvatar();
        }
    }
}
