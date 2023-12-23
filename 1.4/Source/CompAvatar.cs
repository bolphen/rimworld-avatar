using UnityEngine;
using Verse;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using RimWorld;

namespace Avatar
{
    public class AvatarDef : Def
    {
        public string partName;
        public string typeName;
        public string geneName;
        public string unisexPath;
        public string unisexChildPath;
        public string unisexNewbornPath;
        public string femalePath;
        public string malePath;
        public string femaleChildPath;
        public string maleChildPath;
        public string femaleNewbornPath;
        public string maleNewbornPath;
        public bool hideWrinkles;
        public bool hideTattoo;
        public bool hideHair;
        public bool hideBeard;
        public bool hideEyes;
        public bool hideEars;
        public bool hideNose;
        public bool hideMouth;
        public int hideTop = 0;
        public Color color1;
        public Color color2;
        public Color? overlay;
        public string GetPath(string gender, string lifeStage)
        {
            if (lifeStage == "Newborn" && unisexNewbornPath != null)
                return unisexNewbornPath;
            else if ((lifeStage == "Newborn" || lifeStage == "Child") && unisexChildPath != null)
                return unisexChildPath;
            else if (unisexPath != null)
                return unisexPath;
            else if (lifeStage == "Newborn")
                return gender == "Female" ? femaleNewbornPath : maleNewbornPath;
            else if (lifeStage == "Child")
                return gender == "Female" ? femaleChildPath : maleChildPath;
            else if (lifeStage == "")
                return gender == "Female" ? femalePath : malePath;
            else
                return null;
        }
    }
    public class AvatarPart
    {
        public string texPath;
        public Color? color;
        public (Color, Color)? eyeColor;
        public bool drawDexter = true;
        public bool drawSinister = true;
        public (string, int, bool)? fallback = null;
        public int hideTop = 0;
        public int offset = 0;
        public AvatarPart(string texPath, Color? color = null, int offset = 0)
        {
            this.texPath = texPath;
            this.color = color;
            this.offset = offset;
        }
        public static AvatarPart FromGene(Gene gene, Pawn pawn)
        {
            GeneGraphicData graphicData = gene.def.graphicData;
            Color color;
            bool recolor;
            switch (graphicData.colorType)
            {
                case GeneColorType.Hair: color = pawn.story.HairColor; recolor = true; break;
                case GeneColorType.Skin: color = pawn.story.SkinColor; recolor = true; break;
                default: color = graphicData.color ?? Color.white; recolor = false; break;
            }
            AvatarPart result = new (graphicData.GraphicPathFor(pawn), color);
            int offset = 0;
            switch (gene.def.graphicData.drawLoc)
            {
                case GeneDrawLoc.HeadTop: offset = 2; break;
                case GeneDrawLoc.HeadMiddle: offset = 4; break;
                case GeneDrawLoc.HeadLower: offset = 6; break;
            }
            result.fallback = (graphicData.GraphicPathFor(pawn) + "_south", offset, recolor);
            return result;
        }
    }
    public class Feature
    {
        public int nose;
        public int eyes;
        public int mouth;
        public int brows;
        public Feature(int nose, int eyes, int mouth, int brows)
        {
            this.nose = nose;
            this.eyes = eyes;
            this.mouth = mouth;
            this.brows = brows;
        }
    }

    public static class TextureUtil
    {
        public static void ClearTexture(Texture2D texture, Color color)
        {
            RenderTexture active = RenderTexture.active;
            RenderTexture canvas = RenderTexture.GetTemporary(texture.width, texture.height);
            RenderTexture.active = canvas;
            GL.Clear(true, true, color);
            texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            RenderTexture.ReleaseTemporary(canvas);
            RenderTexture.active = active;
        }
        public static Texture2D MakeReadableCopy(Texture2D texture, int? targetWidth = null, int? targetHeight = null)
        {
            int width = targetWidth ?? texture.width;
            int height = targetHeight ?? texture.height;
            RenderTexture active = RenderTexture.active;
            RenderTexture canvas = RenderTexture.GetTemporary(width, height);
            Texture2D result = new (width, height);
            Graphics.Blit(texture, canvas);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            RenderTexture.ReleaseTemporary(canvas);
            RenderTexture.active = active;
            return result;
        }
        public static Texture2D ScaleX2(Texture2D texture)
        {
            Texture2D result = new (2*texture.width, 2*texture.height);
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    if (x > 0 && y > 0 && texture.GetPixel(x-1,y) == texture.GetPixel(x,y-1) && texture.GetPixel(x,y) != texture.GetPixel(x-1,y-1))
                        result.SetPixel(2*x, 2*y, texture.GetPixel(x,y-1));
                    else
                        result.SetPixel(2*x, 2*y, texture.GetPixel(x,y));
                    if (x < texture.width-1 && y > 0 && texture.GetPixel(x+1,y) == texture.GetPixel(x,y-1) && texture.GetPixel(x,y) != texture.GetPixel(x+1,y-1))
                        result.SetPixel(2*x+1, 2*y, texture.GetPixel(x,y-1));
                    else
                        result.SetPixel(2*x+1, 2*y, texture.GetPixel(x,y));
                    if (x > 0 && y < texture.height-1 && texture.GetPixel(x-1,y) == texture.GetPixel(x,y+1) && texture.GetPixel(x,y) != texture.GetPixel(x-1,y+1))
                        result.SetPixel(2*x, 2*y+1, texture.GetPixel(x,y+1));
                    else
                        result.SetPixel(2*x, 2*y+1, texture.GetPixel(x,y));
                    if (x < texture.width-1 && y < texture.height-1 && texture.GetPixel(x+1,y) == texture.GetPixel(x,y+1) && texture.GetPixel(x,y) != texture.GetPixel(x+1,y+1))
                        result.SetPixel(2*x+1, 2*y+1, texture.GetPixel(x,y+1));
                    else
                        result.SetPixel(2*x+1, 2*y+1, texture.GetPixel(x,y));
                }
            }
            result.Apply();
            return result;
        }
        public static Texture2D ProcessVanillaTexture(string texPath, (int, int) size, (int, int) scale, int yOffset, bool recolor)
        {
            if (!AvatarMod.cachedTextures.ContainsKey(texPath))
            {
                Texture2D raw = ContentFinder<Texture2D>.Get(texPath);
                Texture2D resized = MakeReadableCopy(raw, scale.Item1, scale.Item2);
                Texture2D result = new (size.Item1, size.Item2);
                int xOffset = (scale.Item1-size.Item1)/2;

                Texture2D grayPalette = LoadedModManager.GetMod<AvatarMod>().GetTexture("gray");
                for (int y = 0; y < result.height; y++)
                {
                    for (int x = 0; x < result.width; x++)
                    {
                        Color old = resized.GetPixel(x+xOffset,y+yOffset);
                        if (old.a < 0.5)
                            result.SetPixel(x,y,new Color(0,0,0,0));
                        else {
                            float gray = (old.r+old.g+old.b)/3f;
                            if (recolor)
                            {
                                if (gray > 0.9)
                                    result.SetPixel(x,y,grayPalette.GetPixel(0,0));
                                else if (gray > 0.3)
                                    result.SetPixel(x,y,grayPalette.GetPixel(1,0));
                                else
                                    result.SetPixel(x,y,grayPalette.GetPixel(2,0));
                            }
                            else
                            {
                                gray = ((float)Math.Round(gray*5f)+2f)/7f;
                                result.SetPixel(x,y,new Color(gray, gray, gray, 1f));
                            }
                        }
                    }
                }
                result.Apply();
                AvatarMod.cachedTextures[texPath] = result;
                UnityEngine.Object.Destroy(resized);
            }
            return MakeReadableCopy(AvatarMod.cachedTextures[texPath]);
        }
        public static void AddOutline(Texture2D texture)
        {
            Texture2D copy = MakeReadableCopy(texture);
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    if (copy.GetPixel(x,y).a < 0.9)
                    {
                        bool isOutline = false;
                        if (x > 0 && copy.GetPixel(x-1,y).a > 0.9) isOutline = true;
                        if (y > 0 && copy.GetPixel(x,y-1).a > 0.9) isOutline = true;
                        if (x < texture.width-1 && copy.GetPixel(x+1,y).a > 0.9) isOutline = true;
                        if (y < texture.height-1 && copy.GetPixel(x,y+1).a > 0.9) isOutline = true;
                        if (isOutline)
                            texture.SetPixel(x,y,new Color(.1f,.1f,.1f,1f));
                    }
                }
            }
            texture.Apply();
            UnityEngine.Object.Destroy(copy);
        }
    }

    public class AvatarManager
    {
        public AvatarMod mod;
        public Pawn pawn;
        private Texture2D canvas;
        private Texture2D avatar;
        private bool drawHeadgear;
        private bool checkDowned = false;
        private Color bgColor = new Color(.5f,.5f,.6f,.5f);
        public AvatarManager(AvatarMod mod)
        {
            this.mod = mod;
        }
        public void ClearCachedAvatar()
        {
            if (avatar != null)
            { // destroy old texture
                UnityEngine.Object.Destroy(avatar);
                avatar = null;
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
        public void ToggleCompression()
        {
            mod.settings.ToggleCompression();
            ClearCachedAvatar();
        }
        public void ToggleScaling()
        {
            mod.settings.ToggleScaling();
            ClearCachedAvatar();
        }
        private Feature GetFeature()
        {
            int v = 2632*pawn.ageTracker.BirthDayOfYear+3341*pawn.ageTracker.BirthYear;
            return new ((v%450)/90+1, (v%90)/15+1, (v%15)/3+1, (v%3)+1);
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
            if (pawn.ageTracker.CurLifeStage.defName == "HumanlikeBaby")
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
            List<AvatarPart> parts = new ();
            AvatarPart coversAll = null;
            List<Gene> activeGenes = pawn.genes.GenesListForReading.Where(g => g.Active).ToList();
            // collect all cosmetic genes not handled by the defined defs
            List<Gene> cosmeticGenes = activeGenes.Where(g =>
                g.def.HasGraphic
                && !mod.GetDefsForPart("_Gene").Exists(def => def.geneName == g.def.defName)
                && g.def.graphicData.drawLoc != GeneDrawLoc.Tailbone)
                .ToList();
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
            foreach (AvatarDef def in mod.GetDefsForPart("Head"))
                if (def.typeName == headTypeName)
                {
                    hideTattoo = def.hideTattoo;
                    hideWrinkles = def.hideWrinkles;
                    hideEyes = def.hideEyes;
                    hideEars = def.hideEars;
                    hideNose = def.hideNose;
                    hideMouth = def.hideMouth;
                }
            string neckPath = "Core/"+gender+lifeStage+"/Neck";
            parts.Add(new AvatarPart(neckPath, skinColor, 8));
            if (!hideTattoo)
            {
                string bodyTattooPath = GetPath(gender, lifeStage, "BodyTattoo", pawn.style.BodyTattoo.defName, "Core/Unisex/BodyTattoo/NoTattoo");
                parts.Add(new AvatarPart(bodyTattooPath, new Color(1f,1f,1f,0.8f), 8));
            }
            foreach (Apparel apparel in pawn.apparel.WornApparel)
            {
                AvatarDef def = null;
                CompStyleable comp = apparel.GetComp<CompStyleable>();
                if (comp != null && comp.styleDef != null)
                    def = DefDatabase<AvatarDef>.GetNamedSilentFail(comp.styleDef.defName);
                def ??= DefDatabase<AvatarDef>.GetNamedSilentFail(apparel.def.defName);
                if (def != null && def.partName == "Apparel")
                {
                    parts.Add(new AvatarPart(def.GetPath(gender, lifeStage), apparel.DrawColor, 8));
                    if (def.overlay is Color overlayColor)
                        parts.Add(new AvatarPart(def.GetPath(gender, lifeStage)+"Overlay", overlayColor, 8));
                }
                else if (def == null && apparel.def.apparel.bodyPartGroups.Exists(p => p.defName == "Torso")
                    && apparel.def.thingCategories != null) // warcaskets don't have this...
                {
                    if (apparel.def.thingCategories.Exists(p => p.defName == "ApparelArmor"))
                        parts.Add(new AvatarPart("Core/Apparel/GenericArmor"+lifeStage, apparel.DrawColor, 8));
                    else if (!apparel.def.thingCategories.Exists(p => p.defName == "ApparelUtility"))
                        parts.Add(new AvatarPart("Core/Apparel/Generic"+lifeStage, apparel.DrawColor, 8));
                }
            }
            // draw head
            if (!pawn.health.hediffSet.hediffs.Exists(h => h.def.defName == "MissingBodyPart" && h.Part != null && h.Part.def.defName == "Head"))
            {
                string headPath   = GetPath(gender, lifeStage, "Head", headTypeName, "Core/"+gender+lifeStage+"/Head/AverageNormal");
                string faceTattooPath = GetPath(gender, lifeStage, "FaceTattoo", pawn.style.FaceTattoo.defName, "Core/Unisex/FaceTattoo/NoTattoo");
                string beardPath  = GetPath(gender, lifeStage, "Beard", pawn.style.beardDef.defName, "BEARD");
                string hairPath = GetPath(gender, lifeStage, "Hair", pawn.story.hairDef.defName, "HAIR");
                string earsPath = "Core/Unisex/Ears/Ears_Human";
                string nosePath = "Core/"+gender+lifeStage+"/Nose/Nose"+GetFeature().nose.ToString();
                string eyesPath = "Core/"+gender+lifeStage+"/Eyes/Eyes"+GetFeature().eyes.ToString();
                string mouthPath = "Core/"+(mod.settings.noFemaleLips ? "Male" : gender)+lifeStage+"/Mouth/Mouth"+GetFeature().mouth.ToString();
                string browsPath = "Core/"+gender+lifeStage+"/Brows/Brows"+GetFeature().brows.ToString();
                (Color, Color) eyeColor = (new Color(.6f,.6f,.6f,1), new Color(.1f,.1f,.1f,1));
                foreach (Gene gene in activeGenes)
                {
                    foreach (AvatarDef def in mod.GetDefsForPart("Ears"))
                        if (gene.def.defName == def.geneName)
                            earsPath = def.GetPath(gender, lifeStage);
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
                            eyeColor = (def.color1, def.color2);
                }
                AvatarPart ears = new (earsPath, skinColor);
                AvatarPart nose = new (nosePath, skinColor);
                foreach (Gene gene in cosmeticGenes)
                {
                    if (gene.def.endogeneCategory == EndogeneCategory.Ears)
                    {
                        ears = AvatarPart.FromGene(gene, pawn);
                        cosmeticGenes.Remove(gene);
                        break;
                    }
                    else if (gene.def.endogeneCategory == EndogeneCategory.Nose)
                    {
                        nose = AvatarPart.FromGene(gene, pawn);
                        cosmeticGenes.Remove(gene);
                        break;
                    }
                }
                AvatarPart eyes = new (eyesPath, skinColor);
                eyes.eyeColor = eyeColor;
                AvatarPart mouth = new (mouthPath, skinColor);
                if (mod.settings.noFemaleLips && gender == "Female" && lifeStage != "Newborn") mouth.offset = -1; // shift female lips
                AvatarPart head = new (headPath, skinColor);
                if (!hideEars && (!mod.settings.earsOnTop || ears.texPath == "Core/Unisex/Ears/Ears_Human")) parts.Add(ears);
                parts.Add(head);
                if (!hideMouth) parts.Add(mouth);
                if (!hideNose) parts.Add(nose);
                if (!hideEyes) parts.Add(eyes);
                if (!hideWrinkles && !mod.settings.noWrinkles)
                {
                    float ageThreshold = 0.7f*pawn.RaceProps.lifeExpectancy;
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
                    if (pawn.ageTracker.AgeBiologicalYears >= ageThreshold)
                        parts.Add(new AvatarPart("Core/Unisex/Facial/Wrinkles", skinColor));
                }
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
                            parts.Add(new AvatarPart(path, skinColor));
                        }
                    }
                }
                foreach (Gene gene in cosmeticGenes)
                {
                    if (gene.def.endogeneCategory == EndogeneCategory.Jaw
                        || gene.def.graphicData.layer == GeneDrawLayer.PostSkin)
                    {
                        parts.Add(AvatarPart.FromGene(gene, pawn));
                        cosmeticGenes.Remove(gene);
                        break;
                    }
                }
                if (!hideTattoo)
                    parts.Add(new AvatarPart(faceTattooPath, new Color(1f,1f,1f,0.8f)));
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
                            parts.Add(new AvatarPart("Core/Unisex/Jaw/Missing" + lifeStage));
                        }
                        else if (h.Part.def.defName == "Eye")
                        {
                            AvatarPart missingEyes = new ("Core/Unisex/Eyes/Missing", skinColor);
                            if (h.Part.customLabel == "left eye")
                                missingEyes.drawDexter = false;
                            else if (h.Part.customLabel == "right eye")
                                missingEyes.drawSinister = false;
                            parts.Add(missingEyes);
                        }
                        else if (h.Part.def.defName == "Ear")
                        {
                            if (h.Part.customLabel == "left ear")
                                ears.drawSinister = false;
                            else if (h.Part.customLabel == "right ear")
                                ears.drawDexter = false;
                        }
                    }
                    else if (h.def.defName == "Denture")
                    {
                        parts.Add(new AvatarPart("Core/Unisex/Jaw/Denture" + lifeStage));
                    }
                    else if (h.def.defName == "BionicEye" || h.def.defName == "ArchotechEye")
                    {
                        AvatarPart modEyes = new ("Core/Unisex/Eyes/" + h.def.defName);
                        if (h.Part.customLabel == "left eye")
                            modEyes.drawDexter = false;
                        else if (h.Part.customLabel == "right eye")
                            modEyes.drawSinister = false;
                        parts.Add(modEyes);
                    }
                }
                foreach (Gene gene in cosmeticGenes)
                {
                    if (gene.def.graphicData.layer == GeneDrawLayer.PostTattoo)
                    {
                        parts.Add(AvatarPart.FromGene(gene, pawn));
                        cosmeticGenes.Remove(gene);
                        break;
                    }
                }
                if (lifeStage != "Newborn")
                {
                    AvatarPart beard = new (beardPath, hairColor);
                    if (beardPath == "BEARD")
                        beard.fallback = (pawn.style.beardDef.texPath + "_south", 8, true);
                    AvatarPart hair = new (hairPath, hairColor);
                    if (hairPath == "HAIR")
                        hair.fallback = (pawn.story.hairDef.texPath + "_south", 4, true);
                    AvatarPart brows = new (browsPath, hairColor);
                    parts.Add(beard);
                    if (!hideEyes)
                        parts.Add(brows);
                    if (drawHeadgear)
                        // facegear goes under hair
                        foreach (Apparel a in pawn.apparel.WornApparel)
                        {
                            AvatarDef def = null;
                            CompStyleable comp = a.GetComp<CompStyleable>();
                            if (comp != null && comp.styleDef != null)
                                def = DefDatabase<AvatarDef>.GetNamedSilentFail(comp.styleDef.defName);
                            def ??= DefDatabase<AvatarDef>.GetNamedSilentFail(a.def.defName);
                            if (def != null && def.partName == "Facegear")
                            {
                                parts.Add(new AvatarPart(def.GetPath(gender, lifeStage), a.DrawColor));
                                if (def.overlay is Color overlayColor)
                                    parts.Add(new AvatarPart(def.GetPath(gender, lifeStage)+"Overlay", overlayColor));
                            }
                        }
                    if (!drawHeadgear)
                        parts.Add(hair);
                    else
                    {
                        bool hideHair = false;
                        List<AvatarPart> partsToAdd = new ();
                        foreach (Apparel apparel in pawn.apparel.WornApparel)
                        {
                            AvatarDef def = null;
                            CompStyleable comp = apparel.GetComp<CompStyleable>();
                            if (comp != null && comp.styleDef != null)
                                def = DefDatabase<AvatarDef>.GetNamedSilentFail(comp.styleDef.defName);
                            def ??= DefDatabase<AvatarDef>.GetNamedSilentFail(apparel.def.defName);
                            if (def != null && def.partName == "Headgear")
                            {
                                AvatarPart headgear = new (def.GetPath(gender, lifeStage), apparel.DrawColor);
                                if (!apparel.def.apparel.shellCoversHead)
                                {
                                    partsToAdd.Add(headgear);
                                    if (def.overlay is Color overlayColor)
                                        partsToAdd.Add(new AvatarPart(def.GetPath(gender, lifeStage)+"Overlay", overlayColor));
                                }
                                else
                                    coversAll = headgear;
                                if (mod.settings.showHairWithHeadgear)
                                    hideHair |= def.hideHair;
                                else
                                    hideHair |= apparel.def.apparel.bodyPartGroups.Exists(p => p.defName == "UpperHead" || p.defName == "FullHead");
                                hair.hideTop = Math.Max(hair.hideTop, def.hideTop);
                                head.hideTop = Math.Max(head.hideTop, def.hideTop);
                                beard.hideTop = def.hideBeard ? height : 0;
                            }
                        }
                        if (!hideHair)
                            parts.Add(hair);
                        if (coversAll == null)
                            foreach (AvatarPart part in partsToAdd)
                                parts.Add(part);
                    }
                }
                if (!hideEars && (mod.settings.earsOnTop && ears.texPath != "Core/Unisex/Ears/Ears_Human")) parts.Add(ears);
                foreach (Gene gene in activeGenes)
                {
                    foreach (AvatarDef def in mod.GetDefsForPart("Headbone"))
                        if (gene.def.defName == def.geneName)
                            parts.Add(new AvatarPart(def.GetPath(gender, lifeStage)));
                }
                foreach (Gene gene in cosmeticGenes)
                    parts.Add(AvatarPart.FromGene(gene, pawn));
            }
            // end of head drawing

            if (coversAll is AvatarPart someCoversAll)
                parts.Add(someCoversAll);

            // render the texture
            foreach (AvatarPart part in parts)
            {
                if (part.texPath != null)
                {
                    Texture2D layer = null;
                    if (part.fallback is (string, int, bool) fallback)
                    {
                        // fallback to vanilla texture
                        if (ContentFinder<Texture2D>.Get(fallback.Item1, false) != null)
                            layer = TextureUtil.ProcessVanillaTexture(fallback.Item1, (width, height), (62,68), fallback.Item2, fallback.Item3);
                    }
                    else
                    {
                        Texture2D unreadableLayer = mod.GetTexture(part.texPath);
                        // the path is defined in the def so the texture should exist
                        if (unreadableLayer != null)
                            layer = TextureUtil.MakeReadableCopy(unreadableLayer);
                    }
                    if (layer != null)
                    {
                        if (mod.settings.avatarCompression)
                            layer.Compress(true);
                        for (int y = height-layer.height-part.offset; y < height-part.hideTop-yOffset-part.offset; y++)
                        {
                            for (int x = (part.drawDexter ? 0 : width/2); x < (part.drawSinister ? width : width/2); x++)
                            {
                                Color oldColor = downed ? canvas.GetPixel(y-halfWidthHeightDiff, x) : canvas.GetPixel(x, y);
                                Color newColor = layer.GetPixel(x, y-(height-layer.height-part.offset)+yOffset);
                                if (newColor.a > 0)
                                {
                                    Color color = new ();
                                    float alpha = newColor.a;
                                    if (part.color is Color tint)
                                    {
                                        alpha *= tint.a;
                                        color.r = oldColor.r*(1f-alpha) + newColor.r*tint.r*alpha;
                                        color.g = oldColor.g*(1f-alpha) + newColor.g*tint.g*alpha;
                                        color.b = oldColor.b*(1f-alpha) + newColor.b*tint.b*alpha;
                                        color.a = 1f;
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
                        if (part.eyeColor is (Color, Color) eyeColor)
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
                        UnityEngine.Object.Destroy(layer);
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
            return avatar ?? RenderAvatar();
        }
        public void SaveAsPng()
        {
            string dir = Application.persistentDataPath + "/avatar/";
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            string savePath = dir + "avatar-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".png";
            File.WriteAllBytes(savePath, avatar.EncodeToPNG());
        }
        public FloatMenu GetFloatMenu()
        {
            FloatMenu menu = new (new List<FloatMenuOption>()
            {
                new FloatMenuOption("Save as png", SaveAsPng)
            });
            return menu;
        }
    }
}
