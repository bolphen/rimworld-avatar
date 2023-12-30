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
    public class AvatarMod : Mod
    {
        public AvatarSettings settings;

        public static Dictionary<string, List<AvatarDef>> avatarDefs;
        public static Dictionary<string, Texture2D> cachedTextures;

        public static AvatarManager mainManager;
        public static Dictionary<Pawn, AvatarManager> colonistBarManagers;
        public static Dictionary<Pawn, AvatarManager> questTabManagers;
        // each manager stores a pawn, if any, and the avatar texture

        [DebugAction("Avatar", "Reload Textures")]
        public static void ClearCachedTextures()
        {
            foreach (KeyValuePair<string, Texture2D> kvp in cachedTextures)
                UnityEngine.Object.Destroy(kvp.Value);
            cachedTextures.Clear();
            ClearCachedAvatars();
        }

        public static void ClearCachedAvatars()
        {
            mainManager.ClearCachedAvatar();
            foreach (KeyValuePair<Pawn, AvatarManager> kvp in colonistBarManagers)
                kvp.Value.ClearCachedAvatar();
            colonistBarManagers.Clear();
            foreach (KeyValuePair<Pawn, AvatarManager> kvp in questTabManagers)
                kvp.Value.ClearCachedAvatar();
            questTabManagers.Clear();
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

        public void CacheAvatarDefs()
        {
            foreach (AvatarDef def in DefDatabase<AvatarDef>.AllDefs)
            {
                if (!avatarDefs.ContainsKey(def.partName))
                    avatarDefs[def.partName] = new();
                avatarDefs[def.partName].Add(def);
            }
            avatarDefs["_Gene"] = DefDatabase<AvatarDef>.AllDefs.Where(def => def.geneName != null).ToList();
        }

        public List<AvatarDef> GetDefsForPart(string partName)
        {
            if (avatarDefs.Count == 0)
                CacheAvatarDefs();
            return avatarDefs.ContainsKey(partName) ? avatarDefs[partName] : new();
        }

        public AvatarMod(ModContentPack content) : base(content)
        {
            mainManager = new (this);
            colonistBarManagers = new ();
            questTabManagers = new ();
            settings = GetSettings<AvatarSettings>();
            avatarDefs = new ();
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
            string samplePath = "UI/AvatarSample" + (settings.avatarCompression ? "C" : "") + (settings.avatarScaling ? "S" : "");
            Texture2D avatar = GetTexture(samplePath);
            avatar.filterMode = FilterMode.Point;
            float width = settings.avatarWidth;
            float height = width*avatar.height/avatar.width;
            listingStandard.ButtonImage(avatar, width, height);
            listingStandard.GapLine();
            listingStandard.CheckboxLabeled("Draw headgear by default", ref settings.defaultDrawHeadgear,
                "Whether headgear should be drawn by default. Can always be toggled by clicking on the avatar.");
            listingStandard.CheckboxLabeled("Show hair with headgear", ref settings.showHairWithHeadgear,
                "Whether hair should be drawn along with headgear. Doesn't look good for some modded hair styles so you may want to turn this off.");
            listingStandard.CheckboxLabeled("Draw a black outline", ref settings.addOutline);
            listingStandard.CheckboxLabeled("Use same lips for both gender", ref settings.noFemaleLips);
            listingStandard.CheckboxLabeled("Disable wrinkles for all pawns", ref settings.noWrinkles);
            listingStandard.CheckboxLabeled("Show special ears on top", ref settings.earsOnTop,
                "Whether xenogene ears should be drawn on top of hair and headgear.");
            listingStandard.GapLine();
            listingStandard.CheckboxLabeled("Hide main avatar", ref settings.hideMainAvatar);
            listingStandard.CheckboxLabeled("Show avatars in colonist bar (experimental)", ref settings.showInColonistBar);
            settings.showInColonistBarSizeAdjust = (float)(
                listingStandard.SliderLabeled("Colonist bar avatar size adjustment", settings.showInColonistBarSizeAdjust, 0f, 10f));
            listingStandard.Label("Note: If you use ColorCodedMoodBar, the size won't change immediately. You need to go to its setting for a forced refresh.");
            listingStandard.CheckboxLabeled("Show avatars in quest tab (experimental)", ref settings.showInQuestTab);
            listingStandard.End();
        }

        public Texture2D GetColonistBarAvatar(Pawn pawn)
        {
            if (!colonistBarManagers.ContainsKey(pawn))
            {
                AvatarManager manager = new (this);
                manager.SetPawn(pawn);
                manager.SetBGColor(new Color(0,0,0,0));
                manager.SetCheckDowned(true);
                colonistBarManagers[pawn] = manager;
            }
            return colonistBarManagers[pawn].GetAvatar();
        }

        public Texture2D GetQuestTabAvatar(Pawn pawn)
        {
            if (!questTabManagers.ContainsKey(pawn))
            {
                AvatarManager manager = new (this);
                manager.SetPawn(pawn);
                questTabManagers[pawn] = manager;
            }
            return questTabManagers[pawn].GetAvatar();
        }
    }

    [HarmonyPatch(typeof(InspectPaneUtility), "DoTabs")]
    public static class UIPatch
    {
        public static void Postfix(IInspectPane pane)
        {
            AvatarMod mod = LoadedModManager.GetMod<AvatarMod>();
            if (!mod.settings.hideMainAvatar && pane is MainTabWindow_Inspect inspectPanel && inspectPanel.OpenTabType is null)
            {
                Pawn pawn = null;
                if (inspectPanel.SelThing is Pawn selectedPawn)
                    pawn = selectedPawn;
                else if (inspectPanel.SelThing is Corpse corpse && !corpse.IsDessicated())
                    pawn = corpse.InnerPawn;
                if (pawn != null && pawn.RaceProps.Humanlike)
                {
                    AvatarManager manager = AvatarMod.mainManager;
                    manager.SetPawn(pawn);
                    Texture2D avatar = manager.GetAvatar();
                    float width = mod.settings.avatarWidth;
                    float height = width*avatar.height/avatar.width;
                    Rect rect = new(0, inspectPanel.PaneTopY - InspectPaneUtility.TabHeight - height, width, height);
                    GUI.DrawTexture(rect, avatar);
                    if (Event.current.type == EventType.MouseDown && Mouse.IsOver(rect))
                    { // capture mouse click
                        if (Event.current.button == 0) // leftbutton
                            manager.ToggleDrawHeadgear();
                        else if (Event.current.button == 1) // rightbutton
                            Find.WindowStack.Add(manager.GetFloatMenu());
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
                    foreach (Pawn pawn in partPawns)
                    {
                        if (!pawns.Contains(pawn))
                            pawns.Add(pawn);
                    }
                }
                if (pawns.Count > 0)
                {
                    float width = pawns.Count > 4 ? 80f : 120f;
                    float height = width*1.2f;
                    for (int i = 0; i < pawns.Count; i++)
                    {
                        Rect avatarRect = new(rect.width - (width+5)*(i%5+1), curY+15+(height+5)*(i/5), width, height);
                        GUI.DrawTexture(avatarRect, mod.GetQuestTabAvatar(pawns[i]));
                        if (Mouse.IsOver(avatarRect))
                        {
                            TooltipHandler.TipRegion(avatarRect, pawns[i].LabelCap);
                        }
                    }
                    curY += 10f+(height+5f)*(float)Math.Ceiling(pawns.Count/5f);
                }
            }
        }
    }

    // disable some of vanilla's portrait updating
    [HarmonyPatch(typeof(Verse.AI.JobDriver), "SetInitialPosture")]
    public static class AvatarJobDriverPatch
    {
        public static void SetDirty(Pawn _)
        {
            // DO NOTHING!
        }
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
                if (instruction.Calls(AccessTools.Method(typeof(PortraitsCache), "SetDirty")))
                    yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, AccessTools.Method("AvatarJobDriverPatch:SetDirty"));
                else
                    yield return instruction;
        }
    }

    // redraw avatar whenever ingame portrait got redrawn
    [HarmonyPatch(typeof(PortraitsCache), "SetDirty")]
    public static class AvatarUpdateHookPatch
    {
        public static void Postfix(Pawn pawn)
        {
            if (pawn == AvatarMod.mainManager.pawn)
                AvatarMod.mainManager.ClearCachedAvatar();
            if (AvatarMod.colonistBarManagers.ContainsKey(pawn))
                AvatarMod.colonistBarManagers[pawn].ClearCachedAvatar();
            if (AvatarMod.questTabManagers.ContainsKey(pawn))
                AvatarMod.questTabManagers[pawn].ClearCachedAvatar();
        }
    }

    // redraw avatar when pawn ages (for wrinkles)
    [HarmonyPatch(typeof(Pawn_AgeTracker), "BirthdayBiological")]
    public static class Pawn_AgeTracker_BirthdayBiological_Patch
    {
        public static void Postfix(ref Pawn_AgeTracker __instance)
        {
            if (__instance.pawn == AvatarMod.mainManager.pawn)
                AvatarMod.mainManager.ClearCachedAvatar();
            if (AvatarMod.colonistBarManagers.ContainsKey(__instance.pawn))
                AvatarMod.colonistBarManagers[__instance.pawn].ClearCachedAvatar();
            if (AvatarMod.questTabManagers.ContainsKey(__instance.pawn))
                AvatarMod.questTabManagers[__instance.pawn].ClearCachedAvatar();
        }
    }

    public class AvatarSettings : ModSettings
    {
        public float avatarWidth = 160f;
        public bool avatarCompression = false;
        public bool avatarScaling = true;
        public bool defaultDrawHeadgear = true;
        public bool showHairWithHeadgear = true;
        public bool addOutline = false;
        public bool hideMainAvatar = false;
        public bool showInQuestTab = true;
        public bool showInColonistBar = false;
        public float showInColonistBarSizeAdjust = 0f;
        public bool noFemaleLips = false;
        public bool noWrinkles = false;
        public bool earsOnTop = false;

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
            Scribe_Values.Look(ref addOutline, "addOutline");
            Scribe_Values.Look(ref hideMainAvatar, "hideMainAvatar");
            Scribe_Values.Look(ref showInQuestTab, "showInQuestTab");
            Scribe_Values.Look(ref showInColonistBar, "showInColonistBar");
            Scribe_Values.Look(ref showInColonistBarSizeAdjust, "showInColonistBarSizeAdjust");
            Scribe_Values.Look(ref noFemaleLips, "noFemaleLips");
            Scribe_Values.Look(ref noWrinkles, "noWrinkles");
            Scribe_Values.Look(ref earsOnTop, "earsOnTop");
            AvatarMod.ClearCachedAvatars();
        }
    }
}
