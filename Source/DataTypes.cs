using UnityEngine;
using Verse;

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
        public bool specialNoJaw = false;
        public string forceBodyType;
        public bool replaceModdedTexture = true; // set to false to show both textures

        public Color? color1;
        public Color? color2;
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

    public class AvatarLayer
    {
        public string texPath;
        public Color? color;
        public (Color, Color)? eyeColor;
        public (string, Color)? gradient;
        public bool drawDexter = true;
        public bool drawSinister = true;
        public (string, int, string)? fallback = null;
        public int hideTop = 0;
        public int offset = 0;
        public AvatarLayer(string texPath, Color? color = null, int offset = 0)
        {
            this.texPath = texPath;
            this.color = color;
            this.offset = offset;
        }
        #if v1_4
        public static AvatarLayer FromGene(Gene gene, Pawn pawn)
        {
            GeneGraphicData graphicData = gene.def.graphicData;
            Color color;
            string recolor;
            switch (graphicData.colorType)
            {
                case GeneColorType.Hair: color = pawn.story.HairColor; recolor = "yes"; break;
                case GeneColorType.Skin: color = pawn.story.SkinColor; recolor = "yes"; break;
                default: color = graphicData.color ?? Color.white; recolor = "gray"; break;
            }
            AvatarLayer result = new (graphicData.GraphicPathFor(pawn), color);
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
        #endif
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
}
