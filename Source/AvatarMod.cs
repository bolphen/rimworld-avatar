using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Verse;
#if v1_5
using LudeonTK;
#endif
using RimWorld;
using HarmonyLib;

namespace Avatar
{
    public class AvatarMod : Mod
    {
        public AvatarSettings settings;

        public static Dictionary<string, Texture2D> cachedTextures = new ();

        public static AvatarManager mainManager = new ();
        public static Dictionary<Pawn, AvatarManager> colonistBarManagers = new ();
        public static Dictionary<Pawn, AvatarManager> questTabManagers = new ();
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

        public Texture2D GetTexture(string texPath, bool fallback=true)
        {
            if (!cachedTextures.ContainsKey(texPath))
            {
                string path = Content.RootDir+"/Assets/"+texPath+".png";
                if (!System.IO.File.Exists(path))
                { // fallback to RW texture manager
                    return fallback ? ContentFinder<Texture2D>.Get(texPath) : null;
                }
                Texture2D newTexture = new (1, 1);
                newTexture.LoadImage(System.IO.File.ReadAllBytes(path));
                cachedTextures[texPath] = newTexture;
            }
            return cachedTextures[texPath];
        }

        public AvatarMod(ModContentPack content) : base(content)
        {
            AvatarManager.mod = this;
            settings = GetSettings<AvatarSettings>();
        }

        public override string SettingsCategory() => "Avatar";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect viewRect = new (0, 0, inRect.width - 17f, settings.scrollHeight);
            Widgets.BeginScrollView(inRect, ref settings.scroll, viewRect);
            Listing_Standard listingStandard = new Listing_Standard(inRect.AtZero(), () => settings.scroll)
            {
                maxOneColumn = true,
                ColumnWidth = viewRect.width
            };
            listingStandard.Begin(viewRect);
            #if v1_3
            listingStandard.Label("Avatar size");
            settings.avatarWidth = (float)Math.Round(
                listingStandard.Slider(settings.avatarWidth, 80f, 320f));
            #else
            settings.avatarWidth = (float)Math.Round(
                listingStandard.SliderLabeled("Avatar size", settings.avatarWidth, 80f, 320f));
            #endif
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
            listingStandard.CheckboxLabeled("Hide background", ref settings.hideBackground);
            listingStandard.CheckboxLabeled("Use same lips for both gender", ref settings.noFemaleLips);
            listingStandard.CheckboxLabeled("Disable wrinkles for all pawns", ref settings.noWrinkles);
            listingStandard.CheckboxLabeled("Show special ears on top", ref settings.earsOnTop,
                "Whether xenogene ears should be drawn on top of hair and headgear.");
            listingStandard.GapLine();
            listingStandard.CheckboxLabeled("Hide main avatar", ref settings.hideMainAvatar);
            listingStandard.CheckboxLabeled("Show avatars in colonist bar (experimental)", ref settings.showInColonistBar);
            if (settings.showInColonistBar && !ModCompatibility.ColonyGroups_Loaded)
            {
                #if v1_3
                listingStandard.Label("Colonist bar avatar size adjustment");
                settings.showInColonistBarSizeAdjust = (float)(
                    listingStandard.Slider(settings.showInColonistBarSizeAdjust, 0f, 10f));
                #else
                settings.showInColonistBarSizeAdjust = (float)(
                    listingStandard.SliderLabeled("Colonist bar avatar size adjustment", settings.showInColonistBarSizeAdjust, 0f, 10f));
                #endif
            }
            if (ModCompatibility.CCMBar_Loaded && listingStandard.ButtonText("Refresh colonist bar"))
            {
                AccessTools.Method("ColoredMoodBar13.MoodPatch:CGMarkColonistsDirty").Invoke(null, new object[] {null});
            }
            listingStandard.CheckboxLabeled("Show avatars in quest tab (experimental)", ref settings.showInQuestTab);
            listingStandard.GapLine();
            listingStandard.Label("Path to the AI-gen executable");
            settings.aiGenExecutable = listingStandard.TextEntry(settings.aiGenExecutable);
            if (Event.current.type == EventType.Layout)
            {
                settings.scrollHeight = listingStandard.CurHeight;
            }
            listingStandard.End();
            Widgets.EndScrollView();
        }

        public Texture2D GetColonistBarAvatar(Pawn pawn, bool drawHeadgear, bool drawClothes)
        {
            if (!colonistBarManagers.ContainsKey(pawn))
            {
                AvatarManager manager = new ();
                manager.SetPawn(pawn);
                manager.SetBGColor(new Color(0,0,0,0));
                manager.drawHeadgear = drawHeadgear;
                manager.drawClothes = drawClothes;
                manager.SetCheckDowned(true);
                colonistBarManagers[pawn] = manager;
            }
            return colonistBarManagers[pawn].GetAvatar();
        }

        public Texture2D GetQuestTabAvatar(Pawn pawn)
        {
            if (!questTabManagers.ContainsKey(pawn))
            {
                AvatarManager manager = new ();
                manager.SetPawn(pawn);
                questTabManagers[pawn] = manager;
            }
            return questTabManagers[pawn].GetAvatar();
        }
    }

    [HarmonyPatch(typeof(InspectPaneUtility), "DoTabs")]
    public static class UIPatch
    {
        static AvatarMod mod = LoadedModManager.GetMod<AvatarMod>();
        private static Vector2 relPos(Vector2 absPos, Rect rect)
        {
            return new((absPos.x-rect.x)/rect.width, 1f-(absPos.y-rect.y)/rect.height);
        }
        public static void Postfix(IInspectPane pane)
        {
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
                    if (Event.current.type == EventType.MouseDown && Mouse.IsOver(rect)
                        && manager.CheckCursor(relPos(Event.current.mousePosition, rect)))
                    { // capture mouse click
                        if (Event.current.button == 0) // leftbutton
                            manager.drawHeadgear = !manager.drawHeadgear;
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
        static AvatarMod mod = LoadedModManager.GetMod<AvatarMod>();
        public static void Prefix(ref MainTabWindow_Quests __instance, Rect rect, ref float curY)
        {
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
        public bool hideBackground = false;
        public bool hideMainAvatar = false;
        public bool showInQuestTab = true;
        public bool showInColonistBar = false;
        public float showInColonistBarSizeAdjust = 0f;
        public bool noFemaleLips = false;
        public bool noWrinkles = false;
        public bool earsOnTop = false;

        public string aiGenExecutable = "";

        public Vector2 scroll;
        public float scrollHeight = 0;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref avatarWidth, "avatarWidth");
            Scribe_Values.Look(ref avatarCompression, "avatarCompression");
            Scribe_Values.Look(ref avatarScaling, "avatarScaling");
            Scribe_Values.Look(ref defaultDrawHeadgear, "defaultDrawHeadgear");
            Scribe_Values.Look(ref showHairWithHeadgear, "showHairWithHeadgear");
            Scribe_Values.Look(ref addOutline, "addOutline");
            Scribe_Values.Look(ref hideBackground, "hideBackground");
            Scribe_Values.Look(ref hideMainAvatar, "hideMainAvatar");
            Scribe_Values.Look(ref showInQuestTab, "showInQuestTab");
            Scribe_Values.Look(ref showInColonistBar, "showInColonistBar");
            Scribe_Values.Look(ref showInColonistBarSizeAdjust, "showInColonistBarSizeAdjust");
            Scribe_Values.Look(ref noFemaleLips, "noFemaleLips");
            Scribe_Values.Look(ref noWrinkles, "noWrinkles");
            Scribe_Values.Look(ref earsOnTop, "earsOnTop");
            Scribe_Values.Look(ref aiGenExecutable, "aiGenExecutable");
            if (hideBackground)
                AvatarMod.mainManager.SetBGColor(new Color(0,0,0,0));
            else
                AvatarMod.mainManager.SetBGColor(new Color(.5f,.5f,.6f,.5f));
            AvatarMod.ClearCachedAvatars();
        }
    }
}
