using System.ComponentModel;

namespace TomodachiDrawer.Core.ImageProcessing
{
    public enum TomodachiLifeMask
    {
        // Body Bottoms
        [Description("Bottoms: Shorts")]
        BodyBottomsPantsH,
        [Description("Bottoms: Pants")]
        BodyBottomsPantsL,
        [Description("Bottoms: Skirt Long")]
        BodyBottomsSkirtL,
        [Description("Bottoms: Skirt Short")]
        BodyBottomsSkirtS,

        // Body Tops
        [Description("Tops: Short-sleeve dress")]
        BodyTopsLongH, // TODO: Figure out what H/L/N mean... Then name more appropriately.
        [Description("Tops: Long-sleeve dress")]
        BodyTopsLongL,
        [Description("Tops: Sleeveless dress")]
        BodyTopsLongN,
        [Description("Tops: Dress")]
        BodyTopsDressH,
        [Description("Tops: Robe")]
        BodyTopsRobeL,
        [Description("Tops: Short-sleeve")]
        BodyTopsTshirtH,
        [Description("Tops: Long-sleeve")]
        BodyTopsTshirtL,
        [Description("Tops: Tank Top")]
        BodyTopsTshirtN,

        // Goods
        [Description("Treasure: Game")]
        GoodsGame,
        [Description("Treasure: TV")]
        GoodsTV, // TV is more intuitive of a name than "dvd"
        [Description("Treasure: Book")]
        GoodsBook,

        // Headwear
        [Description("Headwear: Cap")]
        HeadwearCap,
        [Description("Headwear: Headwear")]
        HeadwearCostume,
        [Description("Headwear: Hat")]
        HeadwearHat,

        // Interior
        [Description("Interior Wall/Floor")]
        InteriorRoom,

        // Object
        [Description("Object: Ball")]
        ObjectBall,
        [Description("Object: Cone")]
        ObjectCone,
        [Description("Object: Cube")]
        ObjectCube,
        [Description("Object: Cylinder")]
        ObjectCylinder,
        [Description("Object: Egg")]
        ObjectEgg,
        [Description("Object: Half Ball")]
        ObjectHalfBall,
        [Description("Object: Octahedron")]
        ObjectOctahedron,
        [Description("Object: Pyramid")]
        ObjectPyramid,
        [Description("Object: Roof Cone")]
        ObjectRoofCone,
        [Description("Object: Roof Delta")] // Find what its called in game
        ObjectRoofDelta,
        [Description("Object: Roof Dome")]
        ObjectRoofDome,
        [Description("Object: Roof Pyramid")]
        ObjectRoofPyramid,
    }

    public static class TomodachiLifeMasksExtensions
    {
        public static string GetFileName(this TomodachiLifeMask mask) => mask switch
        {
            // Body Bottoms
            TomodachiLifeMask.BodyBottomsPantsH  => "bodyBottomsAUgcPantsH_Uia.png",
            TomodachiLifeMask.BodyBottomsPantsL  => "bodyBottomsAUgcPantsL_Uia.png",
            TomodachiLifeMask.BodyBottomsSkirtL  => "bodyBottomsAUgcSkirtL_Uia.png",
            TomodachiLifeMask.BodyBottomsSkirtS  => "bodyBottomsAUgcSkirtS_Uia.png",
            // Body Tops
            TomodachiLifeMask.BodyTopsLongH      => "bodyTopslongUgcAlineH_Uia.png",
            TomodachiLifeMask.BodyTopsLongL      => "bodyTopslongUgcAlineL_Uia.png",
            TomodachiLifeMask.BodyTopsLongN      => "bodyTopslongUgcAlineN_Uia.png",
            TomodachiLifeMask.BodyTopsDressH     => "bodyTopslongUgcDressH_Uia.png",
            TomodachiLifeMask.BodyTopsRobeL      => "bodyTopslongUgcRobeL_Uia.png",
            TomodachiLifeMask.BodyTopsTshirtH    => "bodyTopsUgcTshirtH_Uia.png",
            TomodachiLifeMask.BodyTopsTshirtL    => "bodyTopsUgcTshirtL_Uia.png",
            TomodachiLifeMask.BodyTopsTshirtN    => "bodyTopsUgcTshirtN_Uia.png",
            // Goods
            TomodachiLifeMask.GoodsGame          => "GoodsGame00_Uia.png",
            TomodachiLifeMask.GoodsTV            => "GoodsDVD00_Uia.png",
            TomodachiLifeMask.GoodsBook          => "GoodsBook00_Uia.png",
            // Headwear
            TomodachiLifeMask.HeadwearCap        => "headwearUgcCap_Uia.png",
            TomodachiLifeMask.HeadwearCostume    => "headwearUgcCostume_Uia.png",
            TomodachiLifeMask.HeadwearHat        => "headwearUgcHat_Uia.png",
            // Interior
            TomodachiLifeMask.InteriorRoom       => "InteriorRoom00_Uia.png",
            // Object
            TomodachiLifeMask.ObjectBall         => "ObjectBall00_Uia.png",
            TomodachiLifeMask.ObjectCone         => "ObjectCone00_Uia.png",
            TomodachiLifeMask.ObjectCube         => "ObjectCube00_Uia.png",
            TomodachiLifeMask.ObjectCylinder     => "ObjectCylinder00_Uia.png",
            TomodachiLifeMask.ObjectEgg          => "ObjectEgg00_Uia.png",
            TomodachiLifeMask.ObjectHalfBall     => "ObjectHalfBall00_Uia.png",
            TomodachiLifeMask.ObjectOctahedron   => "ObjectOctahedron00_Uia.png",
            TomodachiLifeMask.ObjectPyramid      => "ObjectPyramid00_Uia.png",
            TomodachiLifeMask.ObjectRoofCone     => "ObjectRoofCone00_Uia.png",
            TomodachiLifeMask.ObjectRoofDelta    => "ObjectRoofDelta00_Uia.png",
            TomodachiLifeMask.ObjectRoofDome     => "ObjectRoofDome00_Uia.png",
            TomodachiLifeMask.ObjectRoofPyramid  => "ObjectRoofPyramid00_Uia.png",
            _ => throw new ArgumentOutOfRangeException(nameof(mask), mask, null),
        };
    }
}
