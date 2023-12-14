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
        public int hideTop = 0;
        public int offset = 0;
        public AvatarPart(string texPath, Color? color = null, int offset = 0)
        {
            this.texPath = texPath;
            this.color = color;
            this.offset = offset;
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
        public static Texture2D MakeReadableCopy(Texture2D texture)
        {
            RenderTexture active = RenderTexture.active;
            RenderTexture canvas = RenderTexture.GetTemporary(texture.width, texture.height);
            Texture2D result = new (texture.width, texture.height);
            Graphics.Blit(texture, canvas);
            result.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
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
    }

    public class AvatarManager
    {
        public AvatarMod mod;
        public Pawn pawn;
        private Texture2D canvas;
        private Texture2D avatar;
        private bool drawHeadgear = true;
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
            foreach (AvatarDef def in DefDatabase<AvatarDef>.AllDefs)
                if (def.partName == partName && def.typeName == typeName)
                    result = def.GetPath(gender, lifeStage);
            return result ?? fallbackPath;
        }
        private Texture2D RenderAvatar()
        {
            int width = 40;
            int height = 48;
            if (canvas == null)
                canvas = new (width, height);
            TextureUtil.ClearTexture(canvas, new Color(.5f,.5f,.6f,.5f));
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
            Color skinColor = pawn.story.SkinColor;
            Color hairColor = pawn.story.HairColor;
            List<AvatarPart> parts = new ();
            AvatarPart coversAll = null;
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
            foreach (AvatarDef def in DefDatabase<AvatarDef>.AllDefs)
                if (def.partName == "Head" && def.typeName == headTypeName)
                {
                    hideTattoo = def.hideTattoo;
                    hideWrinkles = def.hideWrinkles;
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
            if (!pawn.health.hediffSet.hediffs.Exists(h => h.def.defName == "MissingBodyPart" && h.Part != null && h.Part.def.defName == "Head"))
            {
                string headPath   = GetPath(gender, lifeStage, "Head", headTypeName, "Core/"+gender+lifeStage+"/Head/AverageNormal");
                string faceTattooPath = GetPath(gender, lifeStage, "FaceTattoo", pawn.style.FaceTattoo.defName, "Core/Unisex/FaceTattoo/NoTattoo");
                string beardPath  = GetPath(gender, lifeStage, "Beard", pawn.style.beardDef.defName, "Core/Unisex/Beard/Curly");
                string hairPath = GetPath(gender, lifeStage, "Hair", pawn.story.hairDef.defName, "");
                string earsPath = "Core/Unisex/Ears/Ears_Human";
                string nosePath = "Core/"+gender+lifeStage+"/Nose/Nose"+GetFeature().nose.ToString();
                string eyesPath = "Core/"+gender+lifeStage+"/Eyes/Eyes"+GetFeature().eyes.ToString();
                string mouthPath = "Core/"+gender+lifeStage+"/Mouth/Mouth"+GetFeature().mouth.ToString();
                string browsPath = "Core/"+gender+lifeStage+"/Brows/Brows"+GetFeature().brows.ToString();
                if (hairPath == "")
                {
                    hairPath = "Core/Unisex/Hair/";
                    if (pawn.story.hairDef.styleTags != null)
                    {
                        if (pawn.story.hairDef.styleTags.Exists(t => t == "HairShort"))
                        {
                            if (!pawn.story.hairDef.styleTags.Exists(t => t == "HairLong")) // short
                                hairPath += gender == "Female" ? "Burgundy" : "GreasySwoop";
                            else // mid
                                hairPath += gender == "Female" ? "Victoria" : "Lackland";
                        }
                        else // long
                            hairPath += gender == "Female" ? "Long" : "Snazzy";
                    }
                    else
                        hairPath += gender == "Female" ? "Burgundy" : "GreasySwoop";

                }
                foreach (AvatarDef def in DefDatabase<AvatarDef>.AllDefs.Where(d => d.partName == "Ears"))
                    if (pawn.genes.GenesListForReading.Exists(g => g.Active && g.def.defName == def.geneName))
                        earsPath = def.GetPath(gender, lifeStage);
                foreach (AvatarDef def in DefDatabase<AvatarDef>.AllDefs.Where(d => d.partName == "Nose"))
                    if (pawn.genes.GenesListForReading.Exists(g => g.Active && g.def.defName == def.geneName))
                        nosePath = def.GetPath(gender, lifeStage);
                foreach (AvatarDef def in DefDatabase<AvatarDef>.AllDefs.Where(d => d.partName == "Eyes"))
                    if (pawn.genes.GenesListForReading.Exists(g => g.Active && g.def.defName == def.geneName))
                        eyesPath = def.GetPath(gender, lifeStage);
                foreach (AvatarDef def in DefDatabase<AvatarDef>.AllDefs.Where(d => d.partName == "Mouth"))
                    if (pawn.genes.GenesListForReading.Exists(g => g.Active && g.def.defName == def.geneName))
                        mouthPath = def.GetPath(gender, lifeStage);
                foreach (AvatarDef def in DefDatabase<AvatarDef>.AllDefs.Where(d => d.partName == "Brows"))
                    if (pawn.genes.GenesListForReading.Exists(g => g.Active && g.def.defName == def.geneName))
                        browsPath = def.GetPath(gender, lifeStage);
                AvatarPart ears = new (earsPath, skinColor);
                AvatarPart nose = new (nosePath, skinColor);
                AvatarPart eyes = new (eyesPath, skinColor);
                AvatarPart mouth = new (mouthPath, skinColor);
                AvatarPart head = new (headPath, skinColor);
                parts.Add(ears);
                parts.Add(head);
                parts.Add(mouth);
                parts.Add(nose);
                parts.Add(eyes);
                eyes.eyeColor = (new Color(.6f,.6f,.6f,1), new Color(.1f,.1f,.1f,1));
                foreach (AvatarDef def in DefDatabase<AvatarDef>.AllDefs.Where(d => d.partName == "EyeColor"))
                {
                    if (pawn.genes.GenesListForReading.Exists(g => g.Active && g.def.defName == def.geneName))
                        eyes.eyeColor = (def.color1, def.color2);
                }
                if (!hideWrinkles && !pawn.genes.GenesListForReading.Exists(g => g.Active && g.def.defName == "Ageless"))
                {
                    if (pawn.ageTracker.AgeBiologicalYears >= 0.7*pawn.RaceProps.lifeExpectancy)
                        parts.Add(new AvatarPart("Core/Unisex/Facial/Wrinkles", skinColor));
                }
                foreach (AvatarDef def in DefDatabase<AvatarDef>.AllDefs.Where(d => d.partName == "Facial"))
                {
                    foreach (Gene g in pawn.genes.GenesListForReading)
                    {
                        // handle variants
                        if (g.Active && g.def.defName == def.geneName)
                        {
                            string path = def.GetPath(gender, lifeStage);
                            if (g.def.graphicData != null && g.def.graphicData.graphicPaths != null)
                            {
                                string variant = g.def.graphicData.GraphicPathFor(pawn); // should end with A, B, C
                                path += variant[variant.Length-1];
                            }
                            parts.Add(new AvatarPart(path, skinColor));
                        }
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
                if (lifeStage != "Newborn")
                {
                    AvatarPart beard = new (beardPath, hairColor);
                    AvatarPart hair = new (hairPath, hairColor);
                    parts.Add(beard);
                    parts.Add(new AvatarPart(browsPath, hairColor));
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
                                parts.Add(new AvatarPart(def.GetPath(gender, lifeStage), a.DrawColor));
                        }
                    parts.Add(hair);
                    if (drawHeadgear)
                    {
                        List<AvatarPart> partsToAdd = new ();
                        foreach (Apparel a in pawn.apparel.WornApparel)
                        {
                            AvatarDef def = null;
                            CompStyleable comp = a.GetComp<CompStyleable>();
                            if (comp != null && comp.styleDef != null)
                                def = DefDatabase<AvatarDef>.GetNamedSilentFail(comp.styleDef.defName);
                            def ??= DefDatabase<AvatarDef>.GetNamedSilentFail(a.def.defName);
                            if (def != null && def.partName == "Headgear")
                            {
                                AvatarPart headgear = new (def.GetPath(gender, lifeStage), a.DrawColor);
                                if (!a.def.apparel.shellCoversHead)
                                {
                                    partsToAdd.Add(headgear);
                                    if (def.overlay is Color overlayColor)
                                        partsToAdd.Add(new AvatarPart(def.GetPath(gender, lifeStage)+"Overlay", overlayColor));
                                }
                                else
                                    coversAll = headgear;
                                hair.hideTop = def.hideHair ? height : Math.Max(hair.hideTop, def.hideTop);
                                head.hideTop = Math.Max(head.hideTop, def.hideTop);
                                beard.hideTop = def.hideBeard ? height : 0;
                            }
                        }
                        if (coversAll == null)
                            foreach (AvatarPart part in partsToAdd)
                                parts.Add(part);
                    }
                }
                foreach (AvatarDef def in DefDatabase<AvatarDef>.AllDefs.Where(d => d.partName == "Headbone"))
                {
                    if (pawn.genes.GenesListForReading.Exists(g => g.Active && g.def.defName == def.geneName))
                        parts.Add(new AvatarPart(def.GetPath(gender, lifeStage)));
                }
            }
            if (coversAll is AvatarPart someCoversAll)
                parts.Add(someCoversAll);
            foreach (AvatarPart part in parts)
            {
                if (part.texPath != null)
                {
                    Texture2D unreadableLayer = mod.GetTexture(part.texPath);
                    // the path is defined in the def so the texture should exist
                    if (unreadableLayer != null)
                    {
                        Texture2D layer = TextureUtil.MakeReadableCopy(unreadableLayer);
                        if (mod.settings.avatarCompression)
                            layer.Compress(true);
                        for (int y = height-layer.height-part.offset; y < height-part.hideTop-yOffset-part.offset; y++)
                        {
                            for (int x = (part.drawDexter ? 0 : width/2); x < (part.drawSinister ? width : width/2); x++)
                            {
                                Color oldColor = canvas.GetPixel(x, y);
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
                                    canvas.SetPixel(x, y, color);
                                }
                            }
                        }
                        if (part.eyeColor is (Color, Color) eyeColor)
                        { // draw eye colors manually
                            canvas.SetPixel(14, eyeLevel, eyeColor.Item1);
                            canvas.SetPixel(15, eyeLevel, eyeColor.Item2);
                            canvas.SetPixel(23, eyeLevel, eyeColor.Item2);
                            canvas.SetPixel(24, eyeLevel, eyeColor.Item1);
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
                new FloatMenuOption(mod.settings.avatarCompression ? "Turn off compression" : "Turn on compression", ToggleCompression),
                new FloatMenuOption(mod.settings.avatarScaling ? "Turn off scaling" : "Turn on scaling", ToggleScaling),
                new FloatMenuOption("Save as png", SaveAsPng)
            });
            return menu;
        }
    }
}
