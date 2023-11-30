using UnityEngine;
using Verse;
using System.Linq;
using System.Collections.Generic;

namespace Avatar
{
    public class AvatarDef : Def
    {
        public string partName;
        public string typeName;
        public string geneName;
        public string unisexPath;
        public string unisexChildPath;
        public string femalePath;
        public string malePath;
        public string girlPath;
        public string boyPath;
        public bool hideWrinkles;
        public bool hideTattoo;
        public string GetPath(string gender)
        {
            if ((gender == "Girl" || gender == "Boy") && unisexChildPath != null)
                return unisexChildPath;
            else if (unisexPath != null)
                return unisexPath;
            else if (gender == "Female")
                return femalePath;
            else if (gender == "Male")
                return malePath;
            else if (gender == "Girl")
                return girlPath;
            else if (gender == "Boy")
                return girlPath;
            else
                return null;
        }
    }
    public class AvatarPart
    {
        public string texPath;
        public Color? color;
        public bool drawDexter = true;
        public bool drawSinister = true;
        public AvatarPart(string texPath, Color? color = null)
        {
            this.texPath = texPath;
            this.color = color;
        }
    }
    public class CompAvatar : ThingComp
    {
        private Texture2D avatar;
        private int lastUpdate;
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
            {
                Log.Warning("Missing def for " + partName + ": " + typeName);
                result = fallbackPath;
            }
            return result;
        }
        private void RenderAvatar()
        {
            Pawn pawn = parent as Pawn;
            int width = 40;
            int height = 40;
            string gender = (pawn.gender == Gender.Female) ? "Female" : "Male";
            int yOffset = 0;
            int eyeLevel = 19;
            if (pawn.ageTracker.AgeBiologicalYears < 13)
            { // children
                gender = (pawn.gender == Gender.Female) ? "Girl" : "Boy";
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
            string head   = GetPath(gender, "Head", headTypeName, "Core/"+gender+"/Head/AverageNormal");
            string tattoo = GetPath(gender, "Tattoo", pawn.style.FaceTattoo.defName, "Core/Unisex/Tattoo/NoTattoo");
            string beard  = GetPath(gender, "Beard", pawn.style.beardDef.defName, "Core/Unisex/Beard/NoBeard");
            string hair   = GetPath(gender, "Hair", pawn.story.hairDef.defName, "Core/Unisex/Hair/Bald");
            string neck = "Core/"+gender+"/Neck";
            string ears = "Core/Unisex/Ears/Ears_Human";
            string nose = "Core/"+gender+"/Nose/Nose"+((pawn.ageTracker.BirthDayOfYear%5)+1).ToString();
            string eyes = "Core/"+gender+"/Eyes/Eyes"+((pawn.ageTracker.BirthDayOfYear%6)+1).ToString();
            string mouth = "Core/"+gender+"/Mouth/Mouth"+((pawn.ageTracker.BirthYear%5)+1).ToString();
            string brows = "Core/"+gender+"/Brows/Brows"+((pawn.ageTracker.BirthDayOfYear%3)+1).ToString();
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
            foreach (Hediff h in pawn.health.hediffSet.hediffs.Where(h => h.Part != null))
            {
                if (h.Part.def.defName == "Nose")
                {
                    if (h.def.defName == "MissingBodyPart")
                    {
                        if (pawn.ageTracker.AgeBiologicalYears < 13)
                            parts[4].texPath = "Core/Unisex/Nose_Missing_Child";
                        else
                            parts[4].texPath = "Core/Unisex/Nose_Missing";
                    }
                }
                else if (h.Part.customLabel == "left eye")
                {
                    if (h.def.defName == "MissingBodyPart" || h.def.defName == "BionicEye" || h.def.defName == "ArchotechEye")
                    {
                        AvatarPart eye = new AvatarPart("Core/Unisex/Eyes/" + h.def.defName);
                        eye.drawDexter = false;
                        parts.Add(eye);
                    }
                }
                else if (h.Part.customLabel == "right eye")
                {
                    if (h.def.defName == "MissingBodyPart" || h.def.defName == "BionicEye" || h.def.defName == "ArchotechEye")
                    {
                        AvatarPart eye = new AvatarPart("Core/Unisex/Eyes/" + h.def.defName);
                        eye.drawSinister = false;
                        parts.Add(eye);
                    }
                }
                else if (h.Part.customLabel == "left ear")
                {
                    if (h.def.defName == "MissingBodyPart")
                        parts[1].drawSinister = false;
                }
                else if (h.Part.customLabel == "right ear")
                {
                    if (h.def.defName == "MissingBodyPart")
                        parts[1].drawDexter = false;
                }
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
                    parts.Add(new AvatarPart("Core/Unisex/Wrinkles", skinColor));
            }
            foreach (AvatarDef def in DefDatabase<AvatarDef>.AllDefs.Where(d => d.partName == "Facial"))
            {
                if (pawn.genes.GenesListForReading.Exists(g => g.Active && g.def.defName == def.geneName))
                    parts.Add(new AvatarPart(def.GetPath(gender), skinColor));
            }
            if (!hideTattoo)
                parts.Add(new AvatarPart(tattoo));
            parts.Add(new AvatarPart(beard, hairColor));
            parts.Add(new AvatarPart(brows, hairColor));
            parts.Add(new AvatarPart(hair, hairColor));
            foreach (AvatarDef def in DefDatabase<AvatarDef>.AllDefs.Where(d => d.partName == "Headbone"))
            {
                if (pawn.genes.GenesListForReading.Exists(g => g.Active && g.def.defName == def.geneName))
                    parts.Add(new AvatarPart(def.GetPath(gender)));
            }
            (Color, Color) eyeColor;
            if (pawn.genes.GenesListForReading.Exists(g => g.Active && g.def.defName == "Eyes_Gray"))
            {
                eyeColor = (new Color(.4f,.4f,.4f,1), new Color(.6f,.6f,.6f,1));
            }
            else if (pawn.genes.GenesListForReading.Exists(g => g.Active && g.def.defName == "Eyes_Red"))
                eyeColor = (new Color(1f,.1f,.1f,1), new Color(.7f,0f,0f,1));
            else
                eyeColor = (new Color(.6f,.6f,.6f,1), new Color(.1f,.1f,.1f,1));
            Texture2D layer;
            Texture2D canvas = new (width, height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    canvas.SetPixel(x, y, new Color(.5f,.5f,.6f,.5f));
            foreach (AvatarPart part in parts)
            {
                layer = ContentFinder<Texture2D>.Get(part.texPath);
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
                                if (part.color is Color tint)
                                {
                                    color.r = oldColor.r*(1f-newColor.a) + newColor.r*tint.r*newColor.a;
                                    color.g = oldColor.g*(1f-newColor.a) + newColor.g*tint.g*newColor.a;
                                    color.b = oldColor.b*(1f-newColor.a) + newColor.b*tint.b*newColor.a;
                                    color.a = 1f;
                                }
                                else
                                {
                                    color.r = oldColor.r*(1f-newColor.a) + newColor.r*newColor.a;
                                    color.g = oldColor.g*(1f-newColor.a) + newColor.g*newColor.a;
                                    color.b = oldColor.b*(1f-newColor.a) + newColor.b*newColor.a;
                                    color.a = 1f;
                                }
                                canvas.SetPixel(x, y, color);
                            }
                        }
                    }
                    if (part.texPath == eyes)
                    { // draw eye colors manually
                        canvas.SetPixel(14, eyeLevel, eyeColor.Item1);
                        canvas.SetPixel(15, eyeLevel, eyeColor.Item2);
                        canvas.SetPixel(23, eyeLevel, eyeColor.Item2);
                        canvas.SetPixel(24, eyeLevel, eyeColor.Item1);
                    }
                }
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
    }
}
