#if v1_4 || v1_5
#define BIOTECH
#endif
#if v1_5
#define ANOMALY
#endif
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;

namespace Avatar
{
    public class AvatarManager
    {
        public static AvatarMod mod;
        public Pawn pawn;
        private Feature feature;
        private Texture2D canvas;
        private Texture2D avatar;
        private bool _drawHeadgear = true;
        private bool _drawClothes = true;
        public bool drawHeadgear { get => this._drawHeadgear;
            set
            {
                if (this._drawHeadgear != value)
                {
                    this._drawHeadgear = value;
                    ClearCachedAvatar();
                }
            }
        }
        public bool drawClothes { get => this._drawClothes;
            set
            {
                if (this._drawClothes != value)
                {
                    this._drawClothes = value;
                    ClearCachedAvatar();
                }
            }
        }
        private bool checkDowned = false;
        private Color bgColor = new Color(.5f,.5f,.6f,.5f);
        private int lastUpdateTime;
        private bool updateQueued = false;
        private Texture2D staticTexture;
        private DateTime? staticTextureLastModified;
        private int staticTextureLastCheck;
        public void ClearCachedAvatar()
        {
            if (avatar != null)
            {
                // cap the update frequency
                if (Time.frameCount > lastUpdateTime + 5)
                { // destroy old texture
                    UnityEngine.Object.Destroy(avatar);
                    avatar = null;
                    feature = null;
                    updateQueued = false;
                }
                else
                {
                    updateQueued = true;
                }
            }
        }
        public void SetPawn(Pawn pawn)
        {
            if (this.pawn != pawn)
            {
                this.pawn = pawn;
                drawHeadgear = mod.settings.defaultDrawHeadgear;
                ClearCachedAvatar();
            }
        }
        public void SetBGColor(Color color)
        {
            if (bgColor != color)
            {
                bgColor = color;
                ClearCachedAvatar();
            }
        }
        public void SetCheckDowned(bool checkDowned)
        {
            if (this.checkDowned != checkDowned)
            {
                this.checkDowned = checkDowned;
                ClearCachedAvatar();
            }
        }
        private int Seed()
        {
            #if ANOMALY
            if (pawn.IsDuplicate && Find.PawnDuplicator.duplicates.ContainsKey(pawn.duplicate.duplicateOf))
            {
                Pawn original = Find.PawnDuplicator.duplicates[pawn.duplicate.duplicateOf].First();
                return 2632*original.ageTracker.BirthDayOfYear+3341*original.ageTracker.BirthYear;
            }
            #endif
            return 2632*pawn.ageTracker.BirthDayOfYear+3341*pawn.ageTracker.BirthYear;
        }
        private Feature GetFeature()
        {
            if (feature == null)
            {
                int v = Seed();
                feature = new ((v%450)/90+1, (v%90)/15+1, (v%15)/3+1, (v%3)+1);
            }
            return feature;
        }
        private string GetPath<T>(string gender, string lifeStage, string typeName, string fallbackPath) where T: AvatarDef
        {
            string result = null;
            foreach (T def in DefDatabase<T>.AllDefs)
                if (def.typeName == typeName)
                    result = def.GetPath(gender, lifeStage);
            return result ?? fallbackPath;
        }
        private T GetApparelDef<T>(Apparel apparel) where T: AvatarApparelDef
        {
            T def = null;
            CompStyleable comp = apparel.GetComp<CompStyleable>();
            if (comp != null && comp.styleDef != null)
                def = DefDatabase<T>.GetNamedSilentFail(comp.styleDef.defName);
            return def ?? DefDatabase<T>.GetNamedSilentFail(apparel.def.defName);
        }
        #if !(v1_3 || v1_4)
        private List<AvatarLayer> HandleRenderNode(List<PawnRenderNodeProperties> props, Hediff hediff, string path, Color skinColor, Color hairColor)
        {
            List<AvatarLayer> layers = new ();
            foreach (PawnRenderNodeProperties prop in props)
            {
                AvatarLayer attachment = new (path);
                if (prop.flipGraphic == true)
                    attachment.flipGraphic = true;
                if (prop.texPaths != null)
                {
                    PawnRenderNode node = new (pawn, prop, null);
                    node.hediff = hediff;
                    string variant = node.TexPathFor(pawn); // should end with A, B, C
                    attachment.texPath += variant[variant.Length-1];
                }
                if (prop.colorType == PawnRenderNodeProperties.AttachmentColorType.Skin)
                    attachment.color = skinColor;
                if (prop.colorType == PawnRenderNodeProperties.AttachmentColorType.Hair)
                    attachment.color = hairColor;
                layers.Add(attachment);
            }
            return layers;
        }
        #endif
        private bool ShouldShowWrinkles()
        {
            float ageThreshold = 0.7f*pawn.RaceProps.lifeExpectancy;
            #if BIOTECH
            foreach (Gene gene in pawn.genes.GenesListForReading.Where(g => g.Active))
            {
                if (gene.def.defName == "Ageless")
                    ageThreshold = float.PositiveInfinity;
                else if (!gene.def.statFactors.NullOrEmpty())
                {
                    foreach (StatModifier statModifier in gene.def.statFactors)
                    {
                        if (statModifier.stat == StatDefOf.LifespanFactor)
                            ageThreshold *= statModifier.value;
                    }
                }
            }
            #endif
            return pawn.ageTracker.AgeBiologicalYears >= ageThreshold;
        }
        #if BIOTECH
        private bool GeneUseHairColor(Gene gene)
        {
            #if v1_4
            return gene.def.HasGraphic && gene.def.graphicData.colorType == GeneColorType.Hair;
            #else
            return gene.def.renderNodeProperties?.Count >= 1 && gene.def.renderNodeProperties[0].colorType == PawnRenderNodeProperties.AttachmentColorType.Hair;
            #endif
        }
        #endif
        private Texture2D RenderAvatar()
        {
            lastUpdateTime = Time.frameCount;
            int width = 40;
            int height = 48;
            int halfWidthHeightDiff = (height-width)/2;
            if (canvas == null)
                canvas = new (width, height);
            TextureUtil.ClearTexture(canvas, bgColor);
            string gender = (pawn.gender == Gender.Female) ? "Female" : "Male";
            string lifeStage = "";
            int yOffset = 0;
            int eyeLevel = 0;
            if (pawn.ageTracker.CurLifeStage.defName == "HumanlikeBaby"
                || pawn.ageTracker.CurLifeStage.defName == "HumanlikeToddler") // from Toddlers mod
            {
                lifeStage = "Newborn";
                yOffset = 3;
                eyeLevel = -2;
            }
            else if (pawn.ageTracker.CurLifeStage.defName == "HumanlikeChild" || pawn.ageTracker.CurLifeStage.defName == "HumanlikePreTeenager")
            {
                lifeStage = "Child";
                yOffset = 2;
                eyeLevel = -1;
            }
            // babies are always downed, no need to draw them this way unless dead
            bool downed = checkDowned && (lifeStage != "Newborn" && pawn.Downed);
            int downedOffset = 10;
            Color skinColor = pawn.story.SkinColor;
            Color hairColor = pawn.story.hairColor;
            #if ANOMALY
            if (pawn.IsShambler) hairColor = MutantUtility.GetShamblerColor(hairColor);
            #endif
            List<AvatarLayer> layers = new ();
            AvatarLayer coversAll = null;
            #if BIOTECH
            List<Gene> activeGenes = pawn.genes.GenesListForReading.Where(g => g.Active).ToList();
            // collect all cosmetic genes not handled by the defined defs
            List<Gene> cosmeticGenes = activeGenes.Where(g =>
                !DefDatabase<AvatarGeneDef>.AllDefsListForReading.Exists(def => def.geneName == g.def.defName && def.replaceModdedTexture)
                #if v1_4
                && g.def.HasGraphic
                && g.def.graphicData.drawLoc != GeneDrawLoc.Tailbone
                && !g.def.graphicData.drawOnEyes // eye textures cause most issues, easier to just ignore them
                #else
                && g.def.renderNodeProperties?.Count == 1
                && g.def.renderNodeProperties[0].parentTagDef?.defName != "Body"
                #endif
                ).ToList();
            #endif
            #if v1_3
            string headTypeName = pawn.story.HeadGraphicPath.Split('/').Last();
            headTypeName = headTypeName.Remove(headTypeName.LastIndexOf('_'), 1);
            #else
            string headTypeName = pawn.story.headType.defName;
            #endif
            if (headTypeName.StartsWith("Female_"))
                headTypeName = headTypeName.Substring(7);
            if (headTypeName.EndsWith("_Female"))
                headTypeName = headTypeName.Substring(0, headTypeName.Length-7);
            if (headTypeName.StartsWith("Male_"))
                headTypeName = headTypeName.Substring(5);
            if (headTypeName.EndsWith("_Male"))
                headTypeName = headTypeName.Substring(0, headTypeName.Length-5);
            if (pawn.Drawer.renderer.CurRotDrawMode == RotDrawMode.Dessicated)
            {
                headTypeName = "SSS_HeadType";
                skinColor = new (0.8f, 0.7f, 0.6f);
            }
            bool hideTattoo = false;
            bool hideWrinkles = false;
            bool hideEyes = false;
            bool hideEars = false;
            bool hideNose = false;
            bool hideMouth = false;
            bool hideHair = false;
            bool hideBeard = false;
            int hairHideTop = 0;
            int headHideTop = 0;
            AvatarHeadDef headTypeDef = null;
            string bodyTypeName = "";
            List<EyePos> eyesPos = new List<EyePos> {new EyePos (14,27,15,27), new EyePos (24,27,23,27)};
            foreach (AvatarHeadDef def in DefDatabase<AvatarHeadDef>.AllDefs)
                if (def.typeName == headTypeName)
                {
                    headTypeDef = def;
                    hideTattoo = def.hideTattoo;
                    hideHair = def.hideHair;
                    hideBeard = def.hideBeard;
                    hideWrinkles = def.hideWrinkles;
                    hideEyes = def.hideEyes;
                    hideEars = def.hideEars;
                    hideNose = def.hideNose;
                    hideMouth = def.hideMouth;
                    bodyTypeName = def.forceBodyType;
                }
            List<(Apparel, AvatarBodygearDef)> bodygears = new ();
            List<(Apparel, AvatarBackgearDef)> backgears = new ();
            List<(Apparel, AvatarFacegearDef)> facegears = new ();
            List<(Apparel, AvatarHeadgearDef)> headgears = new ();
            if (drawClothes)
            {
                foreach (Apparel apparel in pawn.apparel.WornApparel)
                {
                    if (GetApparelDef<AvatarBodygearDef>(apparel) is AvatarBodygearDef bodygearDef)
                        bodygears.Add((apparel, bodygearDef));
                    else if (GetApparelDef<AvatarBackgearDef>(apparel) is AvatarBackgearDef backgearDef)
                        backgears.Add((apparel, backgearDef));
                    else if (GetApparelDef<AvatarFacegearDef>(apparel) is AvatarFacegearDef facegearDef)
                        facegears.Add((apparel, facegearDef));
                    else if (GetApparelDef<AvatarHeadgearDef>(apparel) is AvatarHeadgearDef headgearDef)
                    {
                        if (drawHeadgear)
                        {
                            #if v1_3 || v1_4
                            if (apparel.def.apparel.shellCoversHead)
                            #else
                            if (apparel.def.apparel.renderSkipFlags?.FirstOrDefault()?.defName == "Head")
                            #endif
                                coversAll = new (headgearDef.GetPath(gender, lifeStage), apparel.DrawColor);
                            else
                                headgears.Add((apparel, headgearDef));
                            if (mod.settings.showHairWithHeadgear)
                                hideHair |= headgearDef.hideHair;
                            else
                                hideHair |= apparel.def.apparel.bodyPartGroups.Exists(p => p.defName == "UpperHead" || p.defName == "FullHead");
                            hairHideTop = Math.Max(hairHideTop, headgearDef.hideTop);
                            headHideTop = Math.Max(headHideTop, headgearDef.hideTop);
                            hideBeard |= headgearDef.hideBeard;
                        }
                    }
                    else if (apparel.def.apparel.bodyPartGroups.Exists(p => p.defName == "Torso")
                        && apparel.def.thingCategories != null) // warcaskets don't have this...
                    {
                        if (apparel.def.thingCategories.Exists(p => p.defName == "ApparelArmor"))
                            bodygears.Add((apparel, DefDatabase<AvatarBodygearDef>.GetNamedSilentFail("Avatar_GenericArmor")));
                        else if (!apparel.def.thingCategories.Exists(p => p.defName == "ApparelUtility"))
                            bodygears.Add((apparel, DefDatabase<AvatarBodygearDef>.GetNamedSilentFail("Avatar_Generic")));
                    }
                }
            }
            // sorting
            // Vanilla apparels are already sorted.
            // However the offset can be set manually which changes rendering order.
            #if v1_3 || v1_4
            if (ModCompatibility.VanillaFactionsExpanded_Loaded)
            {
                bodygears = bodygears.OrderBy(a => ModCompatibility.GetVEOffset(a.Item1.def)).ToList();
            }
            #else
            bodygears = bodygears.OrderBy(a => a.Item1.def.apparel?.drawData?.dataSouth?.offset?.y ?? 0f).ToList();
            #endif
            // building layers
            #if BIOTECH
            foreach (Gene gene in activeGenes)
            {
                foreach (AvatarBackDef def in DefDatabase<AvatarBackDef>.AllDefs)
                    if (gene.def.defName == def.geneName)
                        layers.Add(new AvatarLayer(def.GetPath(gender, lifeStage)));
            }
            #endif
            foreach ((Apparel apparel, AvatarBackgearDef def) in backgears)
            {
                layers.Add(new AvatarLayer(def.GetPath(gender, lifeStage), apparel.DrawColor, 8));
            }
            List<AvatarLayer> body_layers = new ();
            string neckPath = GetPath<AvatarBodyDef>(gender, lifeStage, bodyTypeName, "Core/"+gender+lifeStage+"/Neck");
            body_layers.Add(new AvatarLayer(neckPath, skinColor, 8));
            if (!hideTattoo)
            {
                string bodyTattooPath = GetPath<AvatarBodyTattooDef>(gender, lifeStage, pawn.style.BodyTattoo?.defName, "Core/Unisex/BodyTattoo/NoTattoo");
                body_layers.Add(new AvatarLayer(bodyTattooPath, new Color(1f,1f,1f,0.8f), 8));
            }
            #if ANOMALY
            if (pawn.IsShambler && !(headTypeName == "SSS_HeadType") && !mod.settings.noCorpseGore)
            {
                body_layers.Add(new AvatarLayer("Core/Unisex/Corpse/BodyScar" + ((Seed()%125)/25+1).ToString(), skinColor, 8));
            }
            #endif
            if (pawn.Drawer.renderer.CurRotDrawMode == RotDrawMode.Rotting && !mod.settings.noCorpseGore)
            {
                layers.Add(new ("Core/Unisex/Corpse/Skeleton" + lifeStage, new (0.8f, 0.7f, 0.6f), 8));
                foreach (AvatarLayer layer in body_layers)
                {
                    layer.alphaMaskPath = "Core/Unisex/Corpse/BodyMask" + ((Seed()%625)/125+1).ToString();
                }
            }
            foreach (AvatarLayer layer in body_layers)
                layers.Add(layer);
            foreach ((Apparel apparel, AvatarBodygearDef def) in bodygears)
            {
                layers.Add(new AvatarLayer(def.GetPath(gender, lifeStage), apparel.DrawColor, 8));
            }
            foreach (Hediff h in pawn.health.hediffSet.hediffs.Where(h => h.Part != null))
            {
                foreach (AvatarBodyHediffDef def in DefDatabase<AvatarBodyHediffDef>.AllDefs)
                {
                    if (h.def.defName == def.typeName)
                    {
                        AvatarLayer prosthetic = new (def.GetPath(gender, lifeStage));
                        prosthetic.offset = 8;
                        if (h.Part.def.defName == "Shoulder")
                        {
                            prosthetic.color = skinColor;
                            if (h.Part.woundAnchorTag == "LeftShoulder")
                                prosthetic.drawDexter = false;
                            else
                                prosthetic.drawSinister = false;
                            layers.Add(prosthetic);
                        }
                        else
                        {
                            #if v1_3 || v1_4
                            layers.Add(prosthetic);
                            #else
                            if (h.def.renderNodeProperties != null)
                            {
                                foreach (AvatarLayer layer in HandleRenderNode(h.def.renderNodeProperties, h, def.GetPath(gender, lifeStage), skinColor, hairColor))
                                {
                                    layer.offset = 8;
                                    layers.Add(layer);
                                }
                            }
                            else
                                layers.Add(prosthetic);
                            #endif
                        }
                    }
                }
            }
            // draw head
            if (!pawn.health.hediffSet.hediffs.Exists(h => h.def.defName == "MissingBodyPart" && h.Part != null && h.Part.def.defName == "Head"))
            {
                string headPath   = GetPath<AvatarHeadDef>(gender, lifeStage, headTypeName, "Core/"+gender+lifeStage+"/Head/AverageNormal");
                string faceTattooPath = GetPath<AvatarFaceTattooDef>(gender, lifeStage, pawn.style.FaceTattoo?.defName, "Core/Unisex/FaceTattoo/NoTattoo");
                string beardPath  = GetPath<AvatarBeardDef>(gender, lifeStage, pawn.style.beardDef?.defName ?? "NoBeard", "BEARD");
                string hairPath = GetPath<AvatarHairDef>(gender, lifeStage, pawn.story.hairDef?.defName ?? "Bald", "HAIR");
                string earsPath = "Core/Unisex/Ears/Ears_Human";
                string nosePath = "Core/"+gender+lifeStage+"/Nose/Nose"+GetFeature().nose.ToString();
                string eyesPath = "Core/"+gender+lifeStage+"/Eyes/Eyes"+GetFeature().eyes.ToString();
                string mouthPath = "Core/"+(mod.settings.noFemaleLips ? "Male" : gender)+lifeStage+"/Mouth/Mouth"+GetFeature().mouth.ToString();
                string browsPath = "Core/"+gender+lifeStage+"/Brows/Brows"+GetFeature().brows.ToString();
                (Color, Color) eyeColor = (new Color(.6f,.6f,.6f,1), new Color(.1f,.1f,.1f,1));
                Color earsColor = skinColor;
                Color noseColor = skinColor;
                List<AvatarLayer> head_layers = new ();
                #if BIOTECH
                foreach (Gene gene in activeGenes)
                {
                    foreach (AvatarEarsDef def in DefDatabase<AvatarEarsDef>.AllDefs)
                        if (gene.def.defName == def.geneName)
                        {
                            earsPath = def.GetPath(gender, lifeStage);
                            if (GeneUseHairColor(gene))
                                earsColor = hairColor;
                        }
                    foreach (AvatarNoseDef def in DefDatabase<AvatarNoseDef>.AllDefs)
                        if (gene.def.defName == def.geneName)
                        {
                            nosePath = def.GetPath(gender, lifeStage);
                            if (GeneUseHairColor(gene))
                                noseColor = hairColor;
                        }
                    foreach (AvatarMouthDef def in DefDatabase<AvatarMouthDef>.AllDefs)
                        if (gene.def.defName == def.geneName)
                            mouthPath = def.GetPath(gender, lifeStage);
                    foreach (AvatarBrowsDef def in DefDatabase<AvatarBrowsDef>.AllDefs)
                        if (gene.def.defName == def.geneName)
                            browsPath = def.GetPath(gender, lifeStage);
                    foreach (AvatarEyesDef def in DefDatabase<AvatarEyesDef>.AllDefs)
                        if (gene.def.defName == def.geneName)
                        {
                            eyesPath = def.GetPath(gender, lifeStage) ?? eyesPath;
                            eyeColor = (def.color1 ?? eyeColor.Item1, def.color2 ?? eyeColor.Item2);
                            eyesPos = def.eyesPos ?? eyesPos;
                        }
                }
                #endif
                if (pawn.story.traits.allTraits.Exists(t => t.def.defName == "BodyMastery")
                    || pawn.health.hediffSet.hediffs.Exists(t => t.def.defName == "VoidTouched"))
                    eyeColor = (new Color(0.7f, 0.7f, 0.7f), new Color(1f, 1f, 1f));
                AvatarLayer ears = new (earsPath, earsColor);
                AvatarLayer nose = new (nosePath, noseColor);
                #if BIOTECH
                foreach (Gene gene in cosmeticGenes)
                {
                    if (gene.def.endogeneCategory == EndogeneCategory.Ears)
                    {
                        ears = AvatarLayer.FromGene(gene, pawn);
                        cosmeticGenes.Remove(gene);
                        break;
                    }
                    else if (gene.def.endogeneCategory == EndogeneCategory.Nose)
                    {
                        nose = AvatarLayer.FromGene(gene, pawn);
                        cosmeticGenes.Remove(gene);
                        break;
                    }
                }
                #endif
                if (pawn.Drawer.renderer.CurRotDrawMode == RotDrawMode.Rotting
                    #if ANOMALY
                    || pawn.IsShambler
                    #endif
                )
                {
                    // try to make the eyes look more lifeless
                    Color avg = new ((eyeColor.Item1.r + eyeColor.Item2.r*2)/3, (eyeColor.Item1.g + eyeColor.Item2.g*2)/3, (eyeColor.Item1.b + eyeColor.Item2.b*2)/3);
                    eyeColor.Item1 = avg;
                    eyeColor.Item2 = avg;
                }
                AvatarLayer eyes = new (eyesPath, skinColor);
                eyes.eyeColor = eyeColor;
                AvatarLayer mouth = new (mouthPath, skinColor);
                if (mod.settings.noFemaleLips && gender == "Female" && lifeStage != "Newborn") mouth.offset = -1; // shift female lips
                AvatarLayer head = new (headPath, skinColor);
                head.hideTop = headHideTop;
                if (!hideEars && (!mod.settings.earsOnTop || ears.texPath == "Core/Unisex/Ears/Ears_Human")) layers.Add(ears);
                // start to build head layers
                head_layers.Add(head);
                if (!hideWrinkles && !mod.settings.noWrinkles)
                {
                    if (ShouldShowWrinkles())
                        head_layers.Add(new AvatarLayer("Core/Unisex/Facial/Wrinkles", skinColor));
                }
                #if BIOTECH
                foreach (Gene gene in cosmeticGenes)
                {
                    if (gene.def.endogeneCategory == EndogeneCategory.Jaw
                        #if v1_4
                        || gene.def.graphicData.layer == GeneDrawLayer.PostSkin
                        #endif
                        )
                    {
                        head_layers.Add(AvatarLayer.FromGene(gene, pawn));
                        cosmeticGenes.Remove(gene);
                        break;
                    }
                }
                #endif
                if (!hideMouth) head_layers.Add(mouth);
                if (!hideNose) head_layers.Add(nose);
                if (!hideEyes) head_layers.Add(eyes);
                #if BIOTECH
                foreach (AvatarFacialDef def in DefDatabase<AvatarFacialDef>.AllDefs)
                {
                    foreach (Gene gene in activeGenes)
                    {
                        // handle variants
                        if (gene.def.defName == def.geneName)
                        {
                            string path = def.GetPath(gender, lifeStage);
                            #if v1_4
                            if (gene.def.graphicData != null && gene.def.graphicData.graphicPaths != null)
                            {
                                string variant = gene.def.graphicData.GraphicPathFor(pawn); // should end with A, B, C
                            #else
                            if (gene.def.renderNodeProperties?.Count == 1 && gene.def.renderNodeProperties[0].texPaths != null)
                            {
                                PawnRenderNode node = new (pawn, gene.def.renderNodeProperties[0], null);
                                node.gene = gene;
                                string variant = node.TexPathFor(pawn); // should end with A, B, C
                            #endif
                                path += variant[variant.Length-1];
                            }
                            head_layers.Add(new AvatarLayer(path, GeneUseHairColor(gene) ? hairColor : skinColor));
                        }
                    }
                }
                #endif
                if (!hideTattoo)
                    head_layers.Add(new AvatarLayer(faceTattooPath, new Color(1f,1f,1f,0.8f)));
                #if ANOMALY
                if (pawn.IsShambler && !(headTypeName == "SSS_HeadType") && !mod.settings.noCorpseGore)
                {
                    head_layers.Add(new AvatarLayer("Core/Unisex/Corpse/FaceScar" + ((Seed()%25)/5+1).ToString(), skinColor));
                }
                #endif
                if (pawn.Drawer.renderer.CurRotDrawMode == RotDrawMode.Rotting && !mod.settings.noCorpseGore)
                {
                    layers.Add(new ("Core/Unisex/Corpse/Skull" + lifeStage, new (0.8f, 0.7f, 0.6f)));
                    foreach (AvatarLayer layer in head_layers)
                    {
                        layer.alphaMaskPath = "Core/Unisex/Corpse/FaceMask" + ((Seed()%5)+1).ToString();
                    }
                }
                foreach (AvatarLayer layer in head_layers)
                    layers.Add(layer);
                foreach (Hediff h in pawn.health.hediffSet.hediffs.Where(h => h.Part != null))
                {
                    if (h is Hediff_MissingPart _)
                    {
                        if (h.Part.def.defName == "Nose")
                        {
                            nose.texPath = "Core/Unisex/Nose/Missing" + lifeStage;
                        }
                        else if (h.Part.def.defName == "Jaw")
                        {
                            if (headTypeDef != null && headTypeDef.specialNoJaw)
                                head.texPath += "NoJaw";
                            else
                                layers.Add(new AvatarLayer("Core/Unisex/Jaw/Missing" + lifeStage, skinColor));
                        }
                        else if (h.Part.def.defName == "Eye")
                        {
                            AvatarLayer missingEyes = new ("Core/Unisex/Eyes/Missing", skinColor);
                            if (h.Part.woundAnchorTag == "LeftEye") // this means it's left...
                                missingEyes.drawDexter = false;
                            else
                                missingEyes.drawSinister = false;
                            layers.Add(missingEyes);
                        }
                        else if (h.Part.def.defName == "Ear")
                        {
                            #if v1_3 || v1_4
                            if (h.Part.customLabel == "left ear")
                            #else
                            if (h.Part.flipGraphic)
                            #endif
                                ears.drawSinister = false;
                            else
                                ears.drawDexter = false;
                        }
                    }
                    else if (h is Hediff_AddedPart _)
                    {
                        foreach (AvatarHeadHediffDef def in DefDatabase<AvatarHeadHediffDef>.AllDefs)
                        {
                            if (h.def.defName == def.typeName)
                            {
                                if (h.Part.def.defName == "Nose")
                                {
                                    nose.texPath = def.GetPath(gender, lifeStage);
                                    nose.color = null;
                                }
                                else
                                {
                                    AvatarLayer prosthetic = new (def.GetPath(gender, lifeStage));
                                    if (h.Part.def.defName == "Eye")
                                    {
                                        if (h.Part.woundAnchorTag == "LeftEye")
                                            prosthetic.drawDexter = false;
                                        else
                                            prosthetic.drawSinister = false;
                                        if (lifeStage != "") prosthetic.offset = -1;
                                        layers.Add(prosthetic);
                                    }
                                    else if (h.Part.def.defName == "Ear")
                                    {
                                        #if v1_3 || v1_4
                                        if (h.Part.customLabel == "left ear")
                                        #else
                                        if (h.Part.flipGraphic)
                                        #endif
                                        {
                                            ears.drawSinister = false;
                                            prosthetic.drawDexter = false;
                                        }
                                        else
                                        {
                                            ears.drawDexter = false;
                                            prosthetic.drawSinister = false;
                                        }
                                        layers.Add(prosthetic);
                                    }
                                    else
                                    {
                                        #if v1_3 || v1_4
                                        layers.Add(prosthetic);
                                        #else
                                        if (h.def.renderNodeProperties != null)
                                            foreach (AvatarLayer layer in HandleRenderNode(h.def.renderNodeProperties, h, def.GetPath(gender, lifeStage), skinColor, hairColor))
                                                layers.Add(layer);
                                        else
                                            layers.Add(prosthetic);
                                        #endif
                                    }
                                }
                            }
                        }
                    }
                    else if (h is Hediff_Injury injury && injury.IsPermanent())
                    {
                        string scarName = h.Part.def.defName + "_" + h.def.defName;
                        foreach (AvatarHeadHediffDef def in DefDatabase<AvatarHeadHediffDef>.AllDefs)
                        {
                            if (scarName == def.typeName)
                            {
                                AvatarLayer scar = new (def.GetPath(gender, lifeStage), skinColor);
                                if (lifeStage != "") scar.offset = -1;
                                if (h.Part.def.defName == "Eye")
                                {
                                    if (h.Part.woundAnchorTag == "LeftEye")
                                        scar.drawDexter = false;
                                    else
                                        scar.drawSinister = false;
                                }
                                layers.Add(scar);
                            }
                        }
                    }
                }
                #if v1_4
                foreach (Gene gene in cosmeticGenes)
                {
                    if (gene.def.graphicData.layer == GeneDrawLayer.PostTattoo)
                    {
                        layers.Add(AvatarLayer.FromGene(gene, pawn));
                        cosmeticGenes.Remove(gene);
                        break;
                    }
                }
                #endif
                AvatarLayer beard = new (beardPath, hairColor);
                if (beardPath == "BEARD")
                    beard.fallback = (pawn.style.beardDef.texPath + "_south", 8, "yes");
                AvatarLayer hair = new (hairPath, hairColor);
                hair.hideTop = hairHideTop;
                if (hairPath == "HAIR")
                    hair.fallback = (pawn.story.hairDef.texPath + "_south", 4, "yes");
                // gradient hair mod support
                if (ModCompatibility.GradientHair_Loaded)
                    hair.gradient = ModCompatibility.GetGradientHair(pawn);
                AvatarLayer brows = new (browsPath, hairColor);
                if (!hideBeard && lifeStage != "Newborn")
                    layers.Add(beard);
                if (!hideEyes && lifeStage != "Newborn")
                    layers.Add(brows);
                if (!drawHeadgear)
                {
                    if (!hideHair && lifeStage != "Newborn")
                        layers.Add(hair);
                }
                else
                {
                    // facegear goes under hair
                    foreach ((Apparel apparel, AvatarFacegearDef def) in facegears)
                    {
                        layers.Add(new AvatarLayer(def.GetPath(gender, lifeStage), apparel.DrawColor, lifeStage == "" ? 0 : -1));
                    }
                    // hair and headgear
                    if (!hideHair && lifeStage != "Newborn")
                        layers.Add(hair);
                    if (coversAll == null)
                        foreach ((Apparel apparel, AvatarHeadgearDef def) in headgears)
                        {
                            layers.Add(new AvatarLayer(def.GetPath(gender, lifeStage), apparel.DrawColor));
                        }
                }
                if (!hideEars && (mod.settings.earsOnTop && ears.texPath != "Core/Unisex/Ears/Ears_Human")) layers.Add(ears);
                #if BIOTECH
                foreach (Gene gene in activeGenes)
                {
                    foreach (AvatarHeadboneDef def in DefDatabase<AvatarHeadboneDef>.AllDefs)
                        if (gene.def.defName == def.geneName)
                            layers.Add(new AvatarLayer(def.GetPath(gender, lifeStage)));
                }
                // dump all remaining cosmetic genes here
                foreach (Gene gene in cosmeticGenes)
                    layers.Add(AvatarLayer.FromGene(gene, pawn));
                #endif
                if (drawHeadgear && coversAll != null)
                    layers.Add(coversAll);
            }
            // end of head drawing


            // render the texture
            foreach (AvatarLayer layer in layers)
            {
                if (layer.texPath != null)
                {
                    Texture2D texture = null;
                    Texture2D mask = null;
                    Texture2D alphaMask = null;
                    if (layer.fallback is (string, int, string) fallback)
                    {
                        // fallback to vanilla texture
                        if (ContentFinder<Texture2D>.Get(fallback.Item1, false) != null)
                            texture = TextureUtil.ProcessVanillaTexture(fallback.Item1, (width, height), (62,68), fallback.Item2, fallback.Item3);
                    }
                    else
                    {
                        Texture2D unreadableTexture = mod.GetTexture(layer.texPath);
                        // the path is defined in the def so the texture should exist
                        if (unreadableTexture != null)
                            texture = TextureUtil.MakeReadableCopy(unreadableTexture);
                    }
                    if (texture != null)
                    {
                        Texture2D maskTexture = mod.GetTexture(layer.texPath + "m", false);
                        if (maskTexture != null)
                            mask = TextureUtil.MakeReadableCopy(maskTexture);
                        if (layer.alphaMaskPath != null)
                        {
                            Texture2D alphaMaskTexture = mod.GetTexture(layer.alphaMaskPath, false);
                            if (alphaMaskTexture != null)
                                alphaMask = TextureUtil.MakeReadableCopy(alphaMaskTexture);
                        }
                        if (mod.settings.avatarCompression)
                            texture.Compress(true);

                        // ad hoc stuff for gradient hair
                        Color? colorB = null;
                        if (mask == null && layer.gradient is (string, Color) gradient)
                        {
                            mask = TextureUtil.ProcessVanillaTexture(gradient.Item1, (width, height), (62,68), 4, "no");
                            colorB = gradient.Item2;
                        }

                        for (int y = height-texture.height-layer.offset; y < height-layer.hideTop-yOffset-layer.offset; y++)
                        {
                            for (int x = (layer.drawDexter ? 0 : width/2); x < (layer.drawSinister ? width : width/2); x++)
                            {
                                Color oldColor = downed ? canvas.GetPixel(y-halfWidthHeightDiff, x) : canvas.GetPixel(x, y);
                                Color newColor = texture.GetPixel(layer.flipGraphic ? (width-1 - x) : x, y-(height-texture.height-layer.offset)+yOffset);
                                float alpha = newColor.a;
                                if (alphaMask != null)
                                    alpha *= alphaMask.GetPixel(x, y-(height-texture.height-layer.offset)+yOffset).r;
                                if (alpha > 0)
                                {
                                    Color color = new ();
                                    if (layer.color is Color tint)
                                    {
                                        alpha *= tint.a;
                                        if (mask != null)
                                        {
                                            if (colorB is Color tint2)
                                            {
                                                Color maskPixel = mask.GetPixel(x, y-(height-texture.height-layer.offset)+yOffset);
                                                color.r = oldColor.r*(1f-alpha) + newColor.r*(tint.r*maskPixel.r + tint2.r*maskPixel.g)*alpha;
                                                color.g = oldColor.g*(1f-alpha) + newColor.g*(tint.g*maskPixel.r + tint2.g*maskPixel.g)*alpha;
                                                color.b = oldColor.b*(1f-alpha) + newColor.b*(tint.b*maskPixel.r + tint2.b*maskPixel.g)*alpha;
                                                color.a = 1f;
                                            }
                                            else
                                            {
                                                Color maskPixel = mask.GetPixel(x, y-(height-texture.height-layer.offset)+yOffset);
                                                color.r = oldColor.r*(1f-alpha) + newColor.r*(tint.r*maskPixel.r + 1-maskPixel.r)*alpha;
                                                color.g = oldColor.g*(1f-alpha) + newColor.g*(tint.g*maskPixel.r + 1-maskPixel.r)*alpha;
                                                color.b = oldColor.b*(1f-alpha) + newColor.b*(tint.b*maskPixel.r + 1-maskPixel.r)*alpha;
                                                color.a = 1f;
                                            }
                                        }
                                        else
                                        {
                                            color.r = oldColor.r*(1f-alpha) + newColor.r*tint.r*alpha;
                                            color.g = oldColor.g*(1f-alpha) + newColor.g*tint.g*alpha;
                                            color.b = oldColor.b*(1f-alpha) + newColor.b*tint.b*alpha;
                                            color.a = 1f;
                                        }
                                    }
                                    else
                                    {
                                        color.r = oldColor.r*(1f-alpha) + newColor.r*alpha;
                                        color.g = oldColor.g*(1f-alpha) + newColor.g*alpha;
                                        color.b = oldColor.b*(1f-alpha) + newColor.b*alpha;
                                        color.a = 1f;
                                    }
                                    if (downed)
                                    {
                                        if (y >= halfWidthHeightDiff && y < height-halfWidthHeightDiff
                                            && x <= width-downedOffset)
                                            canvas.SetPixel(y-halfWidthHeightDiff, width-x-downedOffset, color);
                                    }
                                    else
                                        canvas.SetPixel(x, y, color);
                                }
                            }
                        }
                        if (layer.eyeColor is (Color, Color) eyeColor)
                        { // draw eye colors manually
                            foreach (EyePos eye in eyesPos)
                            {
                                if (downed)
                                {
                                    canvas.SetPixel(eye.pos1.z+eyeLevel-halfWidthHeightDiff, width-eye.pos1.x-downedOffset, eyeColor.Item1);
                                    canvas.SetPixel(eye.pos2.z+eyeLevel-halfWidthHeightDiff, width-eye.pos2.x-downedOffset, eyeColor.Item2);
                                }
                                else
                                {
                                    canvas.SetPixel(eye.pos1.x, eye.pos1.z+eyeLevel, eyeColor.Item1);
                                    canvas.SetPixel(eye.pos2.x, eye.pos2.z+eyeLevel, eyeColor.Item2);
                                }
                            }
                        }
                        if (alphaMask != null) UnityEngine.Object.Destroy(alphaMask);
                        if (mask != null) UnityEngine.Object.Destroy(mask);
                        UnityEngine.Object.Destroy(texture);
                    }
                }
            }
            if (pawn.Dead)
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        Color oldColor = canvas.GetPixel(x, y);
                        float gray = (oldColor.r + oldColor.g + oldColor.b-0.1f)/3f;
                        canvas.SetPixel(x, y, new Color(gray,gray,gray*1.2f,oldColor.a));
                    }
            canvas.Apply();
            if (mod.settings.addOutline)
                TextureUtil.AddOutline(canvas);
            if (avatar != null)
            { // destroy old texture
                UnityEngine.Object.Destroy(avatar);
            }
            if (mod.settings.avatarScaling)
                avatar = TextureUtil.ScaleX2(canvas);
            else
            {
                avatar = TextureUtil.MakeReadableCopy(canvas);
                avatar.Apply();
            }
            avatar.filterMode = FilterMode.Point;
            return avatar;
        }
        public Texture2D GetAvatar(bool allowStatic = true)
        {
            if (allowStatic)
            {
                if (avatar == null || Time.frameCount > staticTextureLastCheck + 10) // don't check every frame
                    TryGetStaticTexture();
                if (staticTexture != null) return staticTexture;
            }
            if (updateQueued) ClearCachedAvatar();
            return avatar ?? RenderAvatar();
        }
        public string GetPawnName()
        {
            return pawn.Name.ToStringFull.Replace("'", "").Replace(" ", "_") + "_" + pawn.thingIDNumber.ToString();
        }
        public void TryGetStaticTexture()
        {
            staticTextureLastCheck = Time.frameCount;
            string path = System.IO.Path.Combine(Application.persistentDataPath, "avatar", GetPawnName() + ".png");
            if (System.IO.File.Exists(path))
            {
                if (staticTexture == null)
                    staticTexture = new (1, 1);
                DateTime lastModified = System.IO.File.GetLastWriteTime(path);
                if (lastModified != staticTextureLastModified)
                {
                    staticTexture.LoadImage(System.IO.File.ReadAllBytes(path));
                    staticTextureLastModified = lastModified;
                }
            }
            else if (staticTexture != null)
            {
                staticTextureLastModified = null;
                UnityEngine.Object.Destroy(staticTexture);
                staticTexture = null;
            }
        }
        private static void SavePng(string filename, Texture2D texture)
        {
            string dir = System.IO.Path.Combine(Application.persistentDataPath, "avatar");
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
            TextureUtil.SavePng(System.IO.Path.Combine(dir, filename), texture);
        }
        public void SaveAsPng()
        {
            SavePng("avatar-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".png", avatar);
        }
        public void UpscaleSaveAsPng()
        {
            Texture2D upscaled = TextureUtil.MakeReadableCopy(avatar, 480, 576);
            SavePng("avatar-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + "-upscaled.png", upscaled);
            UnityEngine.Object.Destroy(upscaled);
        }
        public void EnableStatic()
        {
            string dir = System.IO.Path.Combine(Application.persistentDataPath, "avatar");
            string path = System.IO.Path.Combine(dir, GetPawnName() + ".png");
            string backup = System.IO.Path.Combine(dir, GetPawnName() + "-backup.png");
            if (System.IO.File.Exists(backup))
            {
                System.IO.File.Move(backup, path);
            }
            else
            {
                Texture2D upscaled = TextureUtil.MakeReadableCopy(avatar, 480, 576);
                SavePng(GetPawnName() + ".png", upscaled);
                UnityEngine.Object.Destroy(upscaled);
            }
            TryGetStaticTexture();
        }
        public void DisableStatic()
        {
            string dir = System.IO.Path.Combine(Application.persistentDataPath, "avatar");
            string path = System.IO.Path.Combine(dir, GetPawnName() + ".png");
            if (System.IO.File.Exists(path))
            {
                string backup = System.IO.Path.Combine(dir, GetPawnName() + "-backup.png");
                if (System.IO.File.Exists(backup))
                    System.IO.File.Delete(backup);
                System.IO.File.Move(path, backup);
            }
        }
        public string GetPrompts()
        {
            string prompts_joind = mod.settings.aiGenPreamble
                .Replace("{age}", pawn.ageTracker.AgeBiologicalYears.ToString())
                .Replace("{gender}", (pawn.gender == Gender.Female) ? "female" : "male")
                .Replace("{lifestage}", pawn.ageTracker.CurLifeStage.defName.Substring(9).ToLower());
            HashSet<string> prompts = new ();
            if (ShouldShowWrinkles())
                prompts.Add(DefDatabase<AIGenPromptDef>.GetNamedSilentFail("Wrinkles")?.prompt ?? "");
            HashSet<string> overridden = new ();
            {
                AIGenPromptDef def = DefDatabase<AIGenPromptDef>.GetNamedSilentFail(pawn.story.hairDef.defName);
                if (def != null)
                {
                    prompts.Add(def.prompt);
                }
            }
            if (pawn.style.beardDef?.defName != "NoBeard")
            {
                AIGenPromptDef def = DefDatabase<AIGenPromptDef>.GetNamedSilentFail(pawn.style.beardDef.defName);
                prompts.Add(def?.prompt ?? "beard");
            }
            if (pawn.style.faceTattoo?.defName != "NoTattoo_Face")
            {
                AIGenPromptDef def = DefDatabase<AIGenPromptDef>.GetNamedSilentFail(pawn.style.faceTattoo.defName);
                prompts.Add(def?.prompt ?? "facial tattoo");
            }
            if (pawn.style.bodyTattoo?.defName != "NoTattoo_Body")
            {
                AIGenPromptDef def = DefDatabase<AIGenPromptDef>.GetNamedSilentFail(pawn.style.bodyTattoo.defName);
                prompts.Add(def?.prompt ?? "body tattoo");
            }
            #if BIOTECH
            foreach (Gene gene in pawn.genes.GenesListForReading.Where(g => g.Active))
            {
                AIGenPromptDef def = DefDatabase<AIGenPromptDef>.GetNamedSilentFail(gene.def.defName);
                if (def != null)
                {
                    prompts.Add(def.prompt);
                    foreach (string p in def.overrides.Split(',').Select(p => p.Trim()))
                    {
                        overridden.Add(p);
                    }
                }
            }
            #endif
            if (drawClothes)
            {
                foreach (Apparel apparel in pawn.apparel.WornApparel)
                {
                    if (apparel.def.apparel.layers.Exists(p => p.defName == "Belt"))
                        continue;
                    if (!apparel.def.apparel.layers.Exists(p => p.defName == "Overhead" || p.defName == "EyeCover")
                        || drawHeadgear)
                    {
                        AIGenPromptDef def = DefDatabase<AIGenPromptDef>.GetNamedSilentFail(apparel.def.defName);
                        if (def != null)
                        {
                            prompts.Add(def.prompt);
                            foreach (string p in def.overrides.Split(',').Select(p => p.Trim()))
                            {
                                overridden.Add(p);
                            }
                        }
                        else
                            prompts.Add(apparel.def.label);
                    }
                }
            }
            foreach (string p in overridden)
                prompts.Remove(p);
            prompts_joind += string.Join(", ", prompts);
            return prompts_joind;
        }
        public void OpenPromptsWindow()
        {
            Find.WindowStack.Add(new Prompts_Window(pawn, drawHeadgear, drawClothes));
        }
        public string SaveToStaticPortrait()
        {
            string dir = System.IO.Path.Combine(Application.persistentDataPath, "avatar");
            string path = System.IO.Path.Combine(dir, GetPawnName() + ".png");
            Texture2D upscaled = TextureUtil.MakeReadableCopy(GetAvatar(false), 480, 576);
            SavePng(GetPawnName() + ".png", upscaled);
            UnityEngine.Object.Destroy(upscaled);
            ClearCachedAvatar();
            return path;
        }
        public FloatMenu GetFloatMenu()
        {
            List<FloatMenuOption> options = new ();
            if (staticTexture == null)
            {
                options.Add(new ("Enable static portrait", EnableStatic));
                if (mod.settings.aiGenExecutable?.Length > 0)
                    options.Add(new ("Generate portrait", OpenPromptsWindow));
                options.Add(new ("Save upscaled (480x576)", UpscaleSaveAsPng));
                options.Add(new ("Save original ("+avatar.width.ToString()+"x"+avatar.height.ToString()+")", SaveAsPng));
            }
            else
            {
                options.Add(new ("Disable static portrait", DisableStatic));
                if (mod.settings.aiGenExecutable?.Length > 0)
                    options.Add(new ("Regenerate portrait", OpenPromptsWindow));
            }
            return new FloatMenu(options);
        }
        public bool CheckCursor(Vector2 pos)
        {
            Texture2D displayed = GetAvatar();
            int x = (int) (pos.x * (float) displayed.width);
            int y = (int) (pos.y * (float) displayed.height);
            return displayed.GetPixel(x, y).a > 0;
        }
    }

    public static class AIGen
    {
        public static System.Diagnostics.Process NewProcess(string image, string prompts)
        {
            System.Diagnostics.Process process = new ();
            process.StartInfo.FileName = AvatarManager.mod.settings.aiGenExecutable;
            process.StartInfo.Arguments = string.Format("\"{0}\" \"{1}\"", image, prompts);
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardError = true;
            process.EnableRaisingEvents = true;
            // Dumps stderr to RimWorld log
            String errorString = "AI-gen process failed with the following error:";                 
            process.ErrorDataReceived += new System.Diagnostics.DataReceivedEventHandler((sender, e) =>
            {
                // EOF is represented by a null
                if (e.Data != null) errorString += "\n  " + e.Data;
            });
            process.Exited += new EventHandler((sender, e) =>
            {
                if (process.ExitCode != 0) Log.Error(errorString);
            });
            return process;
        }
    }

    public class AIGenPromptDef : Def
    {
        public string prompt;
        public string overrides = "";
    }

    public class Prompts_Window : Window
    {
        private AvatarManager manager;
        protected string curPrompts;
        public override Vector2 InitialSize => new Vector2(800f, 200f);
        public Prompts_Window(Pawn pawn, bool drawHeadgear = false, bool drawClothes = true)
        {
            manager = new ();
            manager.SetPawn(pawn);
            manager.SetBGColor(new Color(0,0,0,0));
            manager.drawHeadgear = drawHeadgear;
            manager.drawClothes = drawClothes;
            manager.SetCheckDowned(false);
            curPrompts = manager.GetPrompts();
            doCloseX = true;
            draggable = true;
            forcePause = true;
        }
        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, 35f), "Prompts");
            Text.Font = GameFont.Small;
            GUI.DrawTexture(new Rect(0f, 35f, 100f, 120f), manager.GetAvatar(false));
            curPrompts = Widgets.TextArea(new Rect(120f, 35f, rect.width / 2f + 60f, InitialSize.y - 95f), curPrompts);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(120f, InitialSize.y - 60f, rect.width / 2f + 60f, 20f), "The base prompts (\"front portrait\" etc.) can be set in the mod settings.");
            Text.Font = GameFont.Small;
            bool enterPressed = false;
            if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                enterPressed = true;
                Event.current.Use();
            }
            bool drawHeadgear = manager.drawHeadgear;
            bool drawClothes = manager.drawClothes;
            bool toggled = false;
            Widgets.CheckboxLabeled(new Rect(rect.width / 2f + 200f, 35f, rect.width / 2f - 200f, 30f), "Draw headgear", ref drawHeadgear, !drawClothes);
            Widgets.CheckboxLabeled(new Rect(rect.width / 2f + 200f, 65f, rect.width / 2f - 200f, 30f), "Draw clothes", ref drawClothes);
            if (drawHeadgear != manager.drawHeadgear)
            {
                manager.drawHeadgear = drawHeadgear;
                toggled = true;
            }
            if (drawClothes != manager.drawClothes)
            {
                manager.drawClothes = drawClothes;
                toggled = true;
            }
            if (toggled)
            {
                curPrompts = manager.GetPrompts();
            }
            manager.drawClothes = drawClothes;
            if (Widgets.ButtonText(new Rect(rect.width / 2f + 200f, 105f, rect.width / 2f - 200f, 35f), "OK") || enterPressed)
            {
                if (curPrompts.Length > 0)
                {
                    System.Diagnostics.Process process = AIGen.NewProcess(manager.SaveToStaticPortrait(), curPrompts);
                    try
                    {
                        process.Start();
                        Messages.Message("AI-gen process launched", MessageTypeDefOf.TaskCompletion, historical: false);
                        Find.WindowStack.TryRemove(this);
                        process.BeginErrorReadLine();
                    }
                    // Handles the case where the process failed to start
                    catch (System.ComponentModel.Win32Exception e)
                    {
                        Messages.Message("AI-gen process failed to start", MessageTypeDefOf.RejectInput, historical: false);
                        Log.Error("AI-gen process failed to start:\n  " + e.Message);
                    }
                }
                else
                {
                    Messages.Message("Prompts cannot be empty", MessageTypeDefOf.RejectInput, historical: false);
                }
                Event.current.Use();
            }
        }
        public override void PostClose()
        {
            manager.ClearCachedAvatar();
        }
    }
}

