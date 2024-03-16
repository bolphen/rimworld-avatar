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
        public AvatarMod mod;
        public Pawn pawn;
        private Feature feature;
        private Texture2D canvas;
        private Texture2D avatar;
        private bool drawHeadgear;
        private bool checkDowned = false;
        private Color bgColor = new Color(.5f,.5f,.6f,.5f);
        private int lastUpdateTime;
        private bool updateQueued = false;
        private Texture2D staticTexture;
        private DateTime? staticTextureLastModified;
        private int staticTextureLastCheck;
        public AvatarManager(AvatarMod mod)
        {
            this.mod = mod;
        }
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
        public void SetDrawHeadgear(bool drawHeadgear)
        {
            this.drawHeadgear = drawHeadgear;
            ClearCachedAvatar();
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
        public void ToggleDrawHeadgear()
        {
            drawHeadgear = !drawHeadgear;
            ClearCachedAvatar();
        }
        private Feature GetFeature()
        {
            if (feature == null)
            {
                int v = 2632*pawn.ageTracker.BirthDayOfYear+3341*pawn.ageTracker.BirthYear;
                feature = new ((v%450)/90+1, (v%90)/15+1, (v%15)/3+1, (v%3)+1);
            }
            return feature;
        }
        private string GetPath(string gender, string lifeStage, string partName, string typeName, string fallbackPath)
        {
            string result = null;
            foreach (AvatarDef def in mod.GetDefsForPart(partName))
                if (def.typeName == typeName)
                    result = def.GetPath(gender, lifeStage);
            return result ?? fallbackPath;
        }
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
            int eyeLevel = 27;
            if (pawn.ageTracker.CurLifeStage.defName == "HumanlikeBaby"
                || pawn.ageTracker.CurLifeStage.defName == "HumanlikeToddler") // from Toddlers mod
            {
                lifeStage = "Newborn";
                yOffset = 3;
                eyeLevel = 25;
            }
            else if (pawn.ageTracker.CurLifeStage.defName == "HumanlikeChild" || pawn.ageTracker.CurLifeStage.defName == "HumanlikePreTeenager")
            {
                lifeStage = "Child";
                yOffset = 2;
                eyeLevel = 26;
            }
            // babies are always downed, no need to draw them this way unless dead
            bool downed = checkDowned && (lifeStage != "Newborn" && pawn.Downed);
            int downedOffset = 10;
            Color skinColor = pawn.story.SkinColor;
            Color hairColor = pawn.story.HairColor;
            List<AvatarLayer> layers = new ();
            AvatarLayer coversAll = null;
            #if v1_4
            List<Gene> activeGenes = pawn.genes.GenesListForReading.Where(g => g.Active).ToList();
            // collect all cosmetic genes not handled by the defined defs
            List<Gene> cosmeticGenes = activeGenes.Where(g =>
                g.def.HasGraphic
                && !mod.GetDefsForPart("_Gene").Exists(def => def.geneName == g.def.defName && def.replaceModdedTexture)
                && g.def.graphicData.drawLoc != GeneDrawLoc.Tailbone
                && !g.def.graphicData.drawOnEyes) // eye textures cause most issues, easier to just ignore them
                .ToList();
            #endif
            string headTypeName = pawn.story.headType.defName;
            if (headTypeName.StartsWith("Female_"))
                headTypeName = headTypeName.Substring(7);
            if (headTypeName.EndsWith("_Female"))
                headTypeName = headTypeName.Substring(0, headTypeName.Length-7);
            if (headTypeName.StartsWith("Male_"))
                headTypeName = headTypeName.Substring(5);
            if (headTypeName.EndsWith("_Male"))
                headTypeName = headTypeName.Substring(0, headTypeName.Length-5);
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
            AvatarDef headTypeDef = null;
            string bodyTypeName = "";
            foreach (AvatarDef def in mod.GetDefsForPart("Head"))
                if (def.typeName == headTypeName)
                {
                    headTypeDef = def;
                    hideTattoo = def.hideTattoo;
                    hideWrinkles = def.hideWrinkles;
                    hideEyes = def.hideEyes;
                    hideEars = def.hideEars;
                    hideNose = def.hideNose;
                    hideMouth = def.hideMouth;
                    bodyTypeName = def.forceBodyType;
                }
            List<(Apparel, AvatarDef)> apparels = new ();
            List<(Apparel, AvatarDef)> backgears = new ();
            List<(Apparel, AvatarDef)> facegears = new ();
            List<(Apparel, AvatarDef)> headgears = new ();
            foreach (Apparel apparel in pawn.apparel.WornApparel)
            {
                AvatarDef def = null;
                CompStyleable comp = apparel.GetComp<CompStyleable>();
                if (comp != null && comp.styleDef != null)
                    def = DefDatabase<AvatarDef>.GetNamedSilentFail(comp.styleDef.defName);
                def ??= DefDatabase<AvatarDef>.GetNamedSilentFail(apparel.def.defName);
                if (def != null)
                {
                    switch(def.partName)
                    {
                        case "Apparel": apparels.Add((apparel, def)); break;
                        case "Backgear": backgears.Add((apparel, def)); break;
                        case "Facegear": facegears.Add((apparel, def)); break;
                        case "Headgear":
                            #if v1_4
                            if (apparel.def.apparel.shellCoversHead)
                                coversAll = new (def.GetPath(gender, lifeStage), apparel.DrawColor);
                            else
                            #endif
                            headgears.Add((apparel, def));
                            if (drawHeadgear)
                            {
                                if (mod.settings.showHairWithHeadgear)
                                    hideHair |= def.hideHair;
                                else
                                    hideHair |= apparel.def.apparel.bodyPartGroups.Exists(p => p.defName == "UpperHead" || p.defName == "FullHead");
                                hairHideTop = Math.Max(hairHideTop, def.hideTop);
                                headHideTop = Math.Max(headHideTop, def.hideTop);
                                hideBeard |= def.hideBeard;
                            }
                            break;
                    }
                }
                else if (apparel.def.apparel.bodyPartGroups.Exists(p => p.defName == "Torso")
                    && apparel.def.thingCategories != null) // warcaskets don't have this...
                {
                    if (apparel.def.thingCategories.Exists(p => p.defName == "ApparelArmor"))
                        apparels.Add((apparel, DefDatabase<AvatarDef>.GetNamedSilentFail("Avatar_GenericArmor")));
                    else if (!apparel.def.thingCategories.Exists(p => p.defName == "ApparelUtility"))
                        apparels.Add((apparel, DefDatabase<AvatarDef>.GetNamedSilentFail("Avatar_Generic")));
                }
            }
            // sorting
            // Vanilla apparels are already sorted. Unfortunately Vanilla Expanded added a new mechanic that breaks it.
            if (ModCompatibility.VanillaFactionsExpanded_Loaded)
            {
                apparels = apparels.OrderBy(a => ModCompatibility.GetVEOffset(a.Item1.def)).ToList();
            }
            // building layers
            foreach ((Apparel apparel, AvatarDef def) in backgears)
            {
                layers.Add(new AvatarLayer(def.GetPath(gender, lifeStage), apparel.DrawColor, 8));
                if (def.overlay is Color overlayColor)
                    layers.Add(new AvatarLayer(def.GetPath(gender, lifeStage)+"Overlay", overlayColor, 8));
            }
            string neckPath = GetPath(gender, lifeStage, "Body", bodyTypeName, "Core/"+gender+lifeStage+"/Neck");
            layers.Add(new AvatarLayer(neckPath, skinColor, 8));
            if (!hideTattoo)
            {
                string bodyTattooPath = GetPath(gender, lifeStage, "BodyTattoo", pawn.style.BodyTattoo?.defName, "Core/Unisex/BodyTattoo/NoTattoo");
                layers.Add(new AvatarLayer(bodyTattooPath, new Color(1f,1f,1f,0.8f), 8));
            }
            foreach ((Apparel apparel, AvatarDef def) in apparels)
            {
                layers.Add(new AvatarLayer(def.GetPath(gender, lifeStage), apparel.DrawColor, 8));
                if (def.overlay is Color overlayColor)
                    layers.Add(new AvatarLayer(def.GetPath(gender, lifeStage)+"Overlay", overlayColor, 8));
            }
            // draw head
            if (!pawn.health.hediffSet.hediffs.Exists(h => h.def.defName == "MissingBodyPart" && h.Part != null && h.Part.def.defName == "Head"))
            {
                string headPath   = GetPath(gender, lifeStage, "Head", headTypeName, "Core/"+gender+lifeStage+"/Head/AverageNormal");
                string faceTattooPath = GetPath(gender, lifeStage, "FaceTattoo", pawn.style.FaceTattoo?.defName, "Core/Unisex/FaceTattoo/NoTattoo");
                string beardPath  = GetPath(gender, lifeStage, "Beard", pawn.style.beardDef?.defName ?? "NoBeard", "BEARD");
                string hairPath = GetPath(gender, lifeStage, "Hair", pawn.story.hairDef.defName, "HAIR");
                string earsPath = "Core/Unisex/Ears/Ears_Human";
                string nosePath = "Core/"+gender+lifeStage+"/Nose/Nose"+GetFeature().nose.ToString();
                string eyesPath = "Core/"+gender+lifeStage+"/Eyes/Eyes"+GetFeature().eyes.ToString();
                string mouthPath = "Core/"+(mod.settings.noFemaleLips ? "Male" : gender)+lifeStage+"/Mouth/Mouth"+GetFeature().mouth.ToString();
                string browsPath = "Core/"+gender+lifeStage+"/Brows/Brows"+GetFeature().brows.ToString();
                (Color, Color) eyeColor = (new Color(.6f,.6f,.6f,1), new Color(.1f,.1f,.1f,1));
                Color earsColor = skinColor;
                #if v1_4
                foreach (Gene gene in activeGenes)
                {
                    foreach (AvatarDef def in mod.GetDefsForPart("Ears"))
                        if (gene.def.defName == def.geneName)
                        {
                            earsPath = def.GetPath(gender, lifeStage);
                            if (gene.def.HasGraphic && gene.def.graphicData.colorType == GeneColorType.Hair)
                                earsColor = hairColor;
                        }
                    foreach (AvatarDef def in mod.GetDefsForPart("Nose"))
                        if (gene.def.defName == def.geneName)
                            nosePath = def.GetPath(gender, lifeStage);
                    foreach (AvatarDef def in mod.GetDefsForPart("Eyes"))
                        if (gene.def.defName == def.geneName)
                            eyesPath = def.GetPath(gender, lifeStage);
                    foreach (AvatarDef def in mod.GetDefsForPart("Mouth"))
                        if (gene.def.defName == def.geneName)
                            mouthPath = def.GetPath(gender, lifeStage);
                    foreach (AvatarDef def in mod.GetDefsForPart("Brows"))
                        if (gene.def.defName == def.geneName)
                            browsPath = def.GetPath(gender, lifeStage);
                    foreach (AvatarDef def in mod.GetDefsForPart("EyeColor"))
                        if (gene.def.defName == def.geneName)
                            eyeColor = (def.color1 ?? eyeColor.Item1, def.color2 ?? eyeColor.Item2);
                }
                #endif
                AvatarLayer ears = new (earsPath, earsColor);
                AvatarLayer nose = new (nosePath, skinColor);
                #if v1_4
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
                AvatarLayer eyes = new (eyesPath, skinColor);
                eyes.eyeColor = eyeColor;
                AvatarLayer mouth = new (mouthPath, skinColor);
                if (mod.settings.noFemaleLips && gender == "Female" && lifeStage != "Newborn") mouth.offset = -1; // shift female lips
                AvatarLayer head = new (headPath, skinColor);
                head.hideTop = headHideTop;
                if (!hideEars && (!mod.settings.earsOnTop || ears.texPath == "Core/Unisex/Ears/Ears_Human")) layers.Add(ears);
                layers.Add(head);
                if (!hideMouth) layers.Add(mouth);
                if (!hideNose) layers.Add(nose);
                if (!hideEyes) layers.Add(eyes);
                if (!hideWrinkles && !mod.settings.noWrinkles)
                {
                    float ageThreshold = 0.7f*pawn.RaceProps.lifeExpectancy;
                    #if v1_4
                    foreach (Gene gene in activeGenes)
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
                    if (pawn.ageTracker.AgeBiologicalYears >= ageThreshold)
                        layers.Add(new AvatarLayer("Core/Unisex/Facial/Wrinkles", skinColor));
                }
                #if v1_4
                foreach (AvatarDef def in mod.GetDefsForPart("Facial"))
                {
                    foreach (Gene gene in activeGenes)
                    {
                        // handle variants
                        if (gene.def.defName == def.geneName)
                        {
                            string path = def.GetPath(gender, lifeStage);
                            if (gene.def.graphicData != null && gene.def.graphicData.graphicPaths != null)
                            {
                                string variant = gene.def.graphicData.GraphicPathFor(pawn); // should end with A, B, C
                                path += variant[variant.Length-1];
                            }
                            layers.Add(new AvatarLayer(path, skinColor));
                        }
                    }
                }
                foreach (Gene gene in cosmeticGenes)
                {
                    if (gene.def.endogeneCategory == EndogeneCategory.Jaw
                        || gene.def.graphicData.layer == GeneDrawLayer.PostSkin)
                    {
                        layers.Add(AvatarLayer.FromGene(gene, pawn));
                        cosmeticGenes.Remove(gene);
                        break;
                    }
                }
                #endif
                if (!hideTattoo)
                    layers.Add(new AvatarLayer(faceTattooPath, new Color(1f,1f,1f,0.8f)));
                foreach (Hediff h in pawn.health.hediffSet.hediffs.Where(h => h.Part != null))
                {
                    if (h.def.defName == "MissingBodyPart")
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
                                layers.Add(new AvatarLayer("Core/Unisex/Jaw/Missing" + lifeStage));
                        }
                        else if (h.Part.def.defName == "Eye")
                        {
                            AvatarLayer missingEyes = new ("Core/Unisex/Eyes/Missing", skinColor);
                            if (h.Part.customLabel == "left eye")
                                missingEyes.drawDexter = false;
                            else if (h.Part.customLabel == "right eye")
                                missingEyes.drawSinister = false;
                            layers.Add(missingEyes);
                        }
                        else if (h.Part.def.defName == "Ear")
                        {
                            if (h.Part.customLabel == "left ear")
                                ears.drawSinister = false;
                            else if (h.Part.customLabel == "right ear")
                                ears.drawDexter = false;
                        }
                    }
                    else
                    {
                        foreach (AvatarDef def in mod.GetDefsForPart("Hediff"))
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
                                    if (h.Part.customLabel?.StartsWith("left") ?? false)
                                    {
                                        prosthetic.drawDexter = false;
                                        if (h.Part.def.defName == "Ear") ears.drawSinister = false;
                                    }
                                    else if (h.Part.customLabel?.StartsWith("right") ?? false)
                                    {
                                        prosthetic.drawSinister = false;
                                        if (h.Part.def.defName == "Ear") ears.drawDexter = false;
                                    }
                                    layers.Add(prosthetic);
                                }
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
                    if (lifeStage != "Newborn")
                        layers.Add(hair);
                }
                else
                {
                    // facegear goes under hair
                    foreach ((Apparel apparel, AvatarDef def) in facegears)
                    {
                        layers.Add(new AvatarLayer(def.GetPath(gender, lifeStage), apparel.DrawColor, lifeStage == "" ? 0 : -1));
                        if (def.overlay is Color overlayColor)
                            layers.Add(new AvatarLayer(def.GetPath(gender, lifeStage)+"Overlay", overlayColor, lifeStage == "" ? 0 : -1));
                    }
                    // hair and headgear
                    if (!hideHair && lifeStage != "Newborn")
                        layers.Add(hair);
                    if (coversAll == null)
                        foreach ((Apparel apparel, AvatarDef def) in headgears)
                        {
                            layers.Add(new AvatarLayer(def.GetPath(gender, lifeStage), apparel.DrawColor));
                            if (def.overlay is Color overlayColor)
                                layers.Add(new AvatarLayer(def.GetPath(gender, lifeStage)+"Overlay", overlayColor));
                        }
                }
                if (!hideEars && (mod.settings.earsOnTop && ears.texPath != "Core/Unisex/Ears/Ears_Human")) layers.Add(ears);
                #if v1_4
                foreach (Gene gene in activeGenes)
                {
                    foreach (AvatarDef def in mod.GetDefsForPart("Headbone"))
                        if (gene.def.defName == def.geneName)
                            layers.Add(new AvatarLayer(def.GetPath(gender, lifeStage)));
                }
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
                        if (mod.settings.avatarCompression)
                            texture.Compress(true);

                        // ad hoc stuff for gradient hair
                        Texture2D mask = null;
                        Color colorB = new ();
                        if (layer.gradient is (string, Color) gradient)
                        {
                            mask = TextureUtil.ProcessVanillaTexture(gradient.Item1, (width, height), (62,68), 4, "no");
                            colorB = gradient.Item2;
                        }

                        for (int y = height-texture.height-layer.offset; y < height-layer.hideTop-yOffset-layer.offset; y++)
                        {
                            for (int x = (layer.drawDexter ? 0 : width/2); x < (layer.drawSinister ? width : width/2); x++)
                            {
                                Color oldColor = downed ? canvas.GetPixel(y-halfWidthHeightDiff, x) : canvas.GetPixel(x, y);
                                Color newColor = texture.GetPixel(x, y-(height-texture.height-layer.offset)+yOffset);
                                if (newColor.a > 0)
                                {
                                    Color color = new ();
                                    float alpha = newColor.a;
                                    if (layer.color is Color tint)
                                    {
                                        alpha *= tint.a;
                                        if (mask != null)
                                        {
                                            Color maskPixel = mask.GetPixel(x, y-(height-texture.height-layer.offset)+yOffset);
                                            color.r = oldColor.r*(1f-alpha) + newColor.r*(tint.r*maskPixel.r + colorB.r*maskPixel.g)*alpha;
                                            color.g = oldColor.g*(1f-alpha) + newColor.g*(tint.g*maskPixel.r + colorB.g*maskPixel.g)*alpha;
                                            color.b = oldColor.b*(1f-alpha) + newColor.b*(tint.b*maskPixel.r + colorB.b*maskPixel.g)*alpha;
                                            color.a = 1f;
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
                            if (downed)
                            {
                                canvas.SetPixel(eyeLevel-halfWidthHeightDiff, width-14-downedOffset, eyeColor.Item1);
                                canvas.SetPixel(eyeLevel-halfWidthHeightDiff, width-15-downedOffset, eyeColor.Item2);
                                canvas.SetPixel(eyeLevel-halfWidthHeightDiff, width-23-downedOffset, eyeColor.Item2);
                                canvas.SetPixel(eyeLevel-halfWidthHeightDiff, width-24-downedOffset, eyeColor.Item1);
                            }
                            else
                            {
                                canvas.SetPixel(14, eyeLevel, eyeColor.Item1);
                                canvas.SetPixel(15, eyeLevel, eyeColor.Item2);
                                canvas.SetPixel(23, eyeLevel, eyeColor.Item2);
                                canvas.SetPixel(24, eyeLevel, eyeColor.Item1);
                            }
                        }
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
        public Texture2D GetAvatar()
        {
            if (avatar == null || Time.frameCount > staticTextureLastCheck + 10) // don't check every frame
                TryGetStaticTexture();
            if (staticTexture != null) return staticTexture;
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
        private void SavePng(string filename, byte[] bytes)
        {
            string dir = System.IO.Path.Combine(Application.persistentDataPath, "avatar");
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, filename), bytes);
        }
        public void SaveAsPng()
        {
            SavePng("avatar-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".png", avatar.EncodeToPNG());
        }
        public void UpscaleSaveAsPng()
        {
            Texture2D upscaled = TextureUtil.MakeReadableCopy(avatar, 480, 576);
            SavePng("avatar-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + "-upscaled.png", upscaled.EncodeToPNG());
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
                SavePng(GetPawnName() + ".png", upscaled.EncodeToPNG());
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
        public FloatMenu GetFloatMenu()
        {
            FloatMenu menu = new (
            (staticTexture == null) ?
            new List<FloatMenuOption>() {
                new FloatMenuOption("Enable static portrait", EnableStatic),
                new FloatMenuOption("Save upscaled (480x576)", UpscaleSaveAsPng),
                new FloatMenuOption("Save original ("+avatar.width.ToString()+"x"+avatar.height.ToString()+")", SaveAsPng)
            }
            :
            new List<FloatMenuOption>() {
                new FloatMenuOption("Disable static portrait", DisableStatic),
            }
            );
            return menu;
        }
    }
}

