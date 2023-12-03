using UnityEngine;
using Verse;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;

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
        public Color color1;
        public Color color2;
        public string GetPath(string gender)
        {
            if ((gender == "FemaleChild" || gender == "MaleChild") && unisexChildPath != null)
                return unisexChildPath;
            else if ((gender == "FemaleNewborn" || gender == "MaleNewborn") && unisexNewbornPath != null)
                return unisexNewbornPath;
            else if (unisexPath != null)
                return unisexPath;
            else if (gender == "Female")
                return femalePath;
            else if (gender == "Male")
                return malePath;
            else if (gender == "FemaleChild")
                return femaleChildPath;
            else if (gender == "MaleChild")
                return maleChildPath;
            else if (gender == "FemaleNewborn")
                return femaleNewbornPath;
            else if (gender == "MaleNewborn")
                return maleNewbornPath;
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
        public AvatarPart(string texPath, Color? color = null)
        {
            this.texPath = texPath;
            this.color = color;
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
    public class CompAvatar : ThingComp
    {
        private Texture2D avatar;
        private int lastUpdate;
        private Feature feature;
        private Feature GetFeature()
        {
            if (feature == null)
            {
                Pawn pawn = parent as Pawn;
                int v = 2632*pawn.ageTracker.BirthDayOfYear+3341*pawn.ageTracker.BirthYear;
                feature = new ((v%450)/90+1, (v%90)/15+1, (v%15)/3+1, (v%3)+1);
            }
            return feature;
        }
        public void Randomize()
        {
            if (feature != null)
            {
                feature = new (UnityEngine.Random.Range(1,6), UnityEngine.Random.Range(1,7), UnityEngine.Random.Range(1,6), UnityEngine.Random.Range(1,4));
            }
        }
        private void ClearTexture(Texture2D texture, Color color)
        {
            RenderTexture active = RenderTexture.active;
            RenderTexture canvas = RenderTexture.GetTemporary(texture.width, texture.height);
            RenderTexture.active = canvas;
            GL.Clear(true, true, color);
            texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            RenderTexture.ReleaseTemporary(canvas);
            RenderTexture.active = active;
        }
        private Texture2D MakeReadable(Texture2D texture)
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
        private Texture2D ScaleX2(Texture2D texture)
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
        private string GetPath(string gender, string partName, string typeName, string fallbackPath)
        {
            string result = null;
            foreach (AvatarDef def in DefDatabase<AvatarDef>.AllDefs)
                if (def.partName == partName && def.typeName == typeName)
                    result = def.GetPath(gender);
            if (result == null)
                result = fallbackPath;
            return result;
        }
        private void RenderAvatar()
        {
            Pawn pawn = parent as Pawn;
            int width = 40;
            int height = 40;
            Texture2D canvas = new (width, height);
            ClearTexture(canvas, new Color(.5f,.5f,.6f,.5f));
            string gender = (pawn.gender == Gender.Female) ? "Female" : "Male";
            int yOffset = 0;
            int eyeLevel = 19;
            if (pawn.ageTracker.AgeBiologicalYears < 3)
            {
                gender += "Newborn";
                yOffset = 3;
                eyeLevel = 17;
            }
            else if (pawn.ageTracker.AgeBiologicalYears < 13)
            {
                gender += "Child";
                yOffset = 2;
                eyeLevel = 18;
            }
            Color skinColor = pawn.story.SkinColor;
            Color hairColor = pawn.story.HairColor;
            List<AvatarPart> parts = new ();
            string headTypeName = pawn.story.headType.defName;
            if (headTypeName.StartsWith("Female_"))
                headTypeName = headTypeName.Substring(7);
            if (headTypeName.EndsWith("_Female"))
                headTypeName = headTypeName.Substring(0, headTypeName.Length-7);
            if (headTypeName.StartsWith("Male_"))
                headTypeName = headTypeName.Substring(5);
            if (headTypeName.EndsWith("_Male"))
                headTypeName = headTypeName.Substring(0, headTypeName.Length-5);
            if (pawn.health.hediffSet.hediffs.Exists(h => h.def.defName == "MissingBodyPart" && h.Part != null && h.Part.def.defName == "Head"))
            {
                string neck = "Core/"+gender+"/Neck";
                parts.Add(new AvatarPart(neck, skinColor));
            }
            else {
                string head   = GetPath(gender, "Head", headTypeName, "Core/"+gender+"/Head/AverageNormal");
                string tattoo = GetPath(gender, "Tattoo", pawn.style.FaceTattoo.defName, "Core/Unisex/Tattoo/NoTattoo");
                string beard  = GetPath(gender, "Beard", pawn.style.beardDef.defName, "Core/Unisex/Beard/NoBeard");
                string hair   = GetPath(gender, "Hair", pawn.story.hairDef.defName, "Core/Unisex/Hair/Bald");
                string neck = "Core/"+gender+"/Neck";
                string ears = "Core/Unisex/Ears/Ears_Human";
                string nose = "Core/"+gender+"/Nose/Nose"+GetFeature().nose.ToString();
                string eyes = "Core/"+gender+"/Eyes/Eyes"+GetFeature().eyes.ToString();
                string mouth = "Core/"+gender+"/Mouth/Mouth"+GetFeature().mouth.ToString();
                string brows = "Core/"+gender+"/Brows/Brows"+GetFeature().brows.ToString();
                foreach (AvatarDef def in DefDatabase<AvatarDef>.AllDefs.Where(d => d.partName == "Ears"))
                    if (pawn.genes.GenesListForReading.Exists(g => g.Active && g.def.defName == def.geneName))
                        ears = def.GetPath(gender);
                foreach (AvatarDef def in DefDatabase<AvatarDef>.AllDefs.Where(d => d.partName == "Nose"))
                    if (pawn.genes.GenesListForReading.Exists(g => g.Active && g.def.defName == def.geneName))
                        nose = def.GetPath(gender);
                parts.Add(new AvatarPart(neck, skinColor));
                parts.Add(new AvatarPart(ears, skinColor));
                parts.Add(new AvatarPart(head, skinColor));
                parts.Add(new AvatarPart(mouth, skinColor));
                parts.Add(new AvatarPart(nose, skinColor));
                parts.Add(new AvatarPart(eyes, skinColor));
                parts[5].eyeColor = (new Color(.6f,.6f,.6f,1), new Color(.1f,.1f,.1f,1));
                foreach (AvatarDef def in DefDatabase<AvatarDef>.AllDefs.Where(d => d.partName == "Eyes"))
                {
                    if (pawn.genes.GenesListForReading.Exists(g => g.Active && g.def.defName == def.geneName))
                        parts[5].eyeColor = (def.color1, def.color2);
                }
                bool hideTattoo = false;
                bool hideWrinkles = false;
                foreach (AvatarDef def in DefDatabase<AvatarDef>.AllDefs)
                    if (def.partName == "Head" && def.typeName == headTypeName)
                    {
                        hideTattoo = def.hideTattoo;
                        hideWrinkles = def.hideWrinkles;
                    }
                if (!pawn.genes.GenesListForReading.Exists(g => g.Active && g.def.defName == "Ageless") && !hideWrinkles)
                {
                    if (pawn.ageTracker.AgeBiologicalYears >= 55)
                        parts.Add(new AvatarPart("Core/Unisex/Facial/Wrinkles", skinColor));
                }
                foreach (AvatarDef def in DefDatabase<AvatarDef>.AllDefs.Where(d => d.partName == "Facial"))
                {
                    foreach (Gene g in pawn.genes.GenesListForReading)
                    {
                        // handle variants
                        if (g.Active && g.def.defName == def.geneName)
                        {
                            string path = def.GetPath(gender);
                            if (g.def.graphicData != null && g.def.graphicData.graphicPaths != null)
                            {
                                string variant = g.def.graphicData.GraphicPathFor(pawn); // should ends with A, B, C
                                path += variant[variant.Length-1];
                            }
                            parts.Add(new AvatarPart(path, skinColor));
                        }
                    }
                }
                if (!hideTattoo)
                    parts.Add(new AvatarPart(tattoo, new Color(1f,1f,1f,0.9f)));
                foreach (Hediff h in pawn.health.hediffSet.hediffs.Where(h => h.Part != null))
                {
                    if (h.def.defName == "MissingBodyPart")
                    {
                        if (h.Part.def.defName == "Nose")
                        {
                            if (pawn.ageTracker.AgeBiologicalYears < 3)
                                parts[4].texPath = "Core/Unisex/Nose_Missing_Newborn";
                            else if (pawn.ageTracker.AgeBiologicalYears < 13)
                                parts[4].texPath = "Core/Unisex/Nose_Missing_Child";
                            else
                                parts[4].texPath = "Core/Unisex/Nose_Missing";
                        }
                        else if (h.Part.def.defName == "Jaw")
                        {
                            if (pawn.ageTracker.AgeBiologicalYears < 3)
                                parts.Add(new AvatarPart("Core/Unisex/Jaw/Missing_Newborn", skinColor));
                            else if (pawn.ageTracker.AgeBiologicalYears < 13)
                                parts.Add(new AvatarPart("Core/Unisex/Jaw/Missing_Child", skinColor));
                            else
                                parts.Add(new AvatarPart("Core/Unisex/Jaw/Missing", skinColor));
                        }
                        else if (h.Part.def.defName == "Eye")
                        {
                            AvatarPart eye = new ("Core/Unisex/Eyes/Missing", skinColor);
                            if (h.Part.customLabel == "left eye")
                                eye.drawDexter = false;
                            else if (h.Part.customLabel == "right eye")
                                eye.drawSinister = false;
                            parts.Add(eye);
                        }
                        else if (h.Part.def.defName == "Ear")
                        {
                            if (h.Part.customLabel == "left ear")
                                parts[1].drawSinister = false;
                            else if (h.Part.customLabel == "right ear")
                                parts[1].drawDexter = false;
                        }
                    }
                    else if (h.def.defName == "Denture")
                    {
                        if (pawn.ageTracker.AgeBiologicalYears < 3)
                            parts.Add(new AvatarPart("Core/Unisex/Jaw/Denture_Newborn"));
                        else if (pawn.ageTracker.AgeBiologicalYears < 13)
                            parts.Add(new AvatarPart("Core/Unisex/Jaw/Denture_Child"));
                        else
                            parts.Add(new AvatarPart("Core/Unisex/Jaw/Denture"));
                    }
                    else if (h.def.defName == "BionicEye" || h.def.defName == "ArchotechEye")
                    {
                        AvatarPart eye = new ("Core/Unisex/Eyes/" + h.def.defName);
                        eye.drawDexter = false;
                        parts.Add(eye);
                    }
                }
                if (pawn.ageTracker.AgeBiologicalYears >= 3)
                {
                    parts.Add(new AvatarPart(beard, hairColor));
                    parts.Add(new AvatarPart(brows, hairColor));
                    parts.Add(new AvatarPart(hair, hairColor));
                }
                foreach (AvatarDef def in DefDatabase<AvatarDef>.AllDefs.Where(d => d.partName == "Headbone"))
                {
                    if (pawn.genes.GenesListForReading.Exists(g => g.Active && g.def.defName == def.geneName))
                        parts.Add(new AvatarPart(def.GetPath(gender)));
                }
            }
            foreach (AvatarPart part in parts)
            {
                if (part.texPath != null)
                {
                    Texture2D layer = ContentFinder<Texture2D>.Get(part.texPath);
                    // the path is defined in the def so the texture should exist
                    if (layer != null)
                    {
                        layer = MakeReadable(layer);
                        for (int y = 0; y < height-yOffset; y++)
                        {
                            for (int x = (part.drawDexter ? 0 : width/2); x < (part.drawSinister ? width : width/2); x++)
                            {
                                Color oldColor = canvas.GetPixel(x, y);
                                Color newColor = layer.GetPixel(x, y+yOffset);
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
                    }
                }
            }
            if (pawn.Dead) // pawn is dead
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        Color oldColor = canvas.GetPixel(x, y);
                        float gray = (oldColor.r + oldColor.g + oldColor.b-0.1f)/3f;
                        canvas.SetPixel(x, y, new Color(gray,gray,gray*1.2f,oldColor.a));
                    }
            canvas.Apply();
            avatar = ScaleX2(canvas);
            avatar.filterMode = FilterMode.Point;
        }
        public Texture2D GetAvatar()
        {
            if (avatar == null || Time.frameCount > lastUpdate + 20)
            {
                RenderAvatar();
                lastUpdate = Time.frameCount;
            }
            return avatar;
        }
        public void SaveAsPng()
        {
            string dir = Application.persistentDataPath + "/avatar/";
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            string savePath = dir + "avatar-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".png";
            File.WriteAllBytes(savePath, GetAvatar().EncodeToPNG());
        }
    }
}
