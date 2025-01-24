#if v1_4 || v1_5
#define BIOTECH
#endif
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Avatar
{
    public class AvatarDef : Def
    {
        public string typeName;

        public string unisexPath;
        public string unisexChildPath;
        public string unisexNewbornPath;
        public string femalePath;
        public string malePath;
        public string femaleChildPath;
        public string maleChildPath;
        public string femaleNewbornPath;
        public string maleNewbornPath;

        public bool replaceModdedTexture = true; // set to false to show both textures

        public string GetPath(string gender, string lifeStage)
        {
            if (lifeStage == "Newborn" && unisexNewbornPath != null)
                return unisexNewbornPath;
            if ((lifeStage == "Newborn" || lifeStage == "Child") && unisexChildPath != null)
                return unisexChildPath;
            if (unisexPath != null)
                return unisexPath;
            if (gender == "Female")
            {
                if (lifeStage == "Newborn" && femaleNewbornPath != null)
                    return femaleNewbornPath;
                if ((lifeStage == "Newborn" || lifeStage == "Child") && femaleChildPath != null)
                    return femaleChildPath;
                return femalePath;
            }
            else
            {
                if (lifeStage == "Newborn" && maleNewbornPath != null)
                    return maleNewbornPath;
                if ((lifeStage == "Newborn" || lifeStage == "Child") && maleChildPath != null)
                    return maleChildPath;
                return malePath;
            }
        }
    }

    public class AvatarFaceTattooDef : AvatarDef {};
    public class AvatarBodyTattooDef : AvatarDef {};
    public class AvatarBeardDef : AvatarDef {};
    public class AvatarHairDef : AvatarDef {};
    public class AvatarHeadDef : AvatarDef
    {
        public bool hideWrinkles;
        public bool hideHair;
        public bool hideBeard;
        public bool hideTattoo;
        public bool hideEyes;
        public bool hideEars;
        public bool hideNose;
        public bool hideMouth;
        public bool specialNoJaw = false;
        public bool reassignStandard = false;
        public string forceBodyType;
        public string facePaint;
        public Color? facePaintColor;
        public int headAttachmentOffset = 0;
        public List<EyePos> eyesPos;
    };
    public class AvatarFacePaintDef : AvatarDef {};
    public class AvatarBodyDef : AvatarDef {};
    public class AvatarHeadHediffDef : AvatarDef {};
    public class AvatarBodyHediffDef : AvatarDef {};

    public class AvatarApparelDef : AvatarDef {};
    public class AvatarBodygearDef : AvatarApparelDef {};
    public class AvatarBackgearDef : AvatarApparelDef {};
    public class AvatarFacegearDef : AvatarApparelDef
    {
        public bool hideHair;
        public bool hideBeard;
        public int hideTop = 0;
    };
    public class AvatarHeadgearDef : AvatarApparelDef
    {
        public bool hideHair;
        public bool hideBeard;
        public int hideTop = 0;
    }

    #if BIOTECH
    public class AvatarGeneDef : AvatarDef
    {
        public string geneName;
        public int offset;
    };
    public class AvatarEarsDef : AvatarGeneDef {};
    public class AvatarNoseDef : AvatarGeneDef {};
    public class AvatarMouthDef : AvatarGeneDef {};
    public class AvatarBrowsDef : AvatarGeneDef {};
    public class AvatarFacialDef : AvatarGeneDef {};
    public class AvatarHeadboneDef : AvatarGeneDef {};
    public class AvatarBackDef : AvatarGeneDef {};
    public class AvatarEyesDef : AvatarGeneDef
    {
        public Color? color1;
        public Color? color2;
        public List<EyePos> eyesPos;
    }
    #endif

    public class AvatarLayer
    {
        public string texPath;
        public string alphaMaskPath;
        public bool flipGraphic = false;
        public Color? color;
        public (Color, Color)? eyeColor;
        public string maskPath;
        public string gradientMask;
        public Color? colorB;
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
        #if BIOTECH
        public static AvatarLayer FromGene(Gene gene, Pawn pawn)
        {
            #if v1_4
            GeneGraphicData attachment = gene.def.graphicData;
            #else
            // this is a fallback method for auto compability of mods, so we
            // will ignore the fancy rendering features from 1.5, and take only
            // one graphic element like in 1.4
            PawnRenderNodeProperties attachment = gene.def.renderNodeProperties[0];
            #endif
            Color color;
            string recolor;
            switch (attachment.colorType)
            {
                #if v1_4
                case GeneColorType.Hair: color = pawn.story.HairColor; recolor = "yes"; break;
                case GeneColorType.Skin: color = pawn.story.SkinColor; recolor = "yes"; break;
                #else
                case PawnRenderNodeProperties.AttachmentColorType.Hair: color = pawn.story.HairColor; recolor = "yes"; break;
                case PawnRenderNodeProperties.AttachmentColorType.Skin: color = pawn.story.SkinColor; recolor = "yes"; break;
                #endif
                default: color = attachment.color ?? Color.white; recolor = "gray"; break;
            }
            string path;
            #if v1_4
            path = attachment.GraphicPathFor(pawn);
            #else
            PawnRenderNode node = new (pawn, attachment, null);
            node.gene = gene;
            path = node.TexPathFor(pawn);
            #endif
            int offset = 0;
            switch (gene.def.endogeneCategory)
            {
                case EndogeneCategory.Headbone:
                case EndogeneCategory.Ears: offset = 2; break;
                case EndogeneCategory.Nose: offset = 4; break;
                case EndogeneCategory.Jaw:  offset = 6; break;
            }
            offset = DefDatabase<AvatarGeneDef>.AllDefsListForReading.FirstOrFallback(def => def.geneName == gene.def.defName)?.offset ?? offset;
            AvatarLayer result = new (path, color);
            result.fallback = (path + "_south", offset, recolor);
            return result;
        }
        #endif
    }

    public class EyePos
    {
        public List<IntVec2> pos1;
        public List<IntVec2> pos2;
        public EyePos() {}
        public EyePos(int pos1x, int pos1y, int pos2x, int pos2y)
        {
            pos1 = new List<IntVec2> { new IntVec2 (pos1x, pos1y) };
            pos2 = new List<IntVec2> { new IntVec2 (pos2x, pos2y) };
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
}
