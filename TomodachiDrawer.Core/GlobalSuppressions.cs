// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Style",
    "IDE0305:Simplify collection initialization",
    Justification = "It's less intuitive to read.",
    Scope = "member",
    Target = "~M:TomodachiDrawer.Core.CanvasDrawer.DrawImage(SkiaSharp.SKBitmap,TomodachiDrawer.Core.ImageProcessing.Quantizers.QuantizerSettings,System.String,System.Single,System.Boolean)~System.Threading.Tasks.Task"
)]
[assembly: SuppressMessage(
    "Style",
    "IDE0028:Simplify collection initialization",
    Justification = "<Pending>",
    Scope = "member",
    Target = "~M:TomodachiDrawer.Core.CanvasDrawer.DetectBucketZones(TomodachiDrawer.Core.Models.ColourLayer,System.Int32,System.Int32,System.Int32)"
)]
[assembly: SuppressMessage(
    "Style",
    "IDE0306:Simplify collection initialization",
    Justification = "<Pending>",
    Scope = "member",
    Target = "~M:TomodachiDrawer.Core.CanvasDrawer.DetectBucketZones(TomodachiDrawer.Core.Models.ColourLayer,System.Int32,System.Int32,System.Int32)"
)]
[assembly: SuppressMessage(
    "Style",
    "IDE0300:Simplify collection initialization",
    Justification = "Switching { for [ is really minor",
    Scope = "member",
    Target = "~M:TomodachiDrawer.Core.CanvasDrawer.DetectBucketZones(TomodachiDrawer.Core.Models.ColourLayer,System.Int32,System.Int32,System.Int32)"
)]
[assembly: SuppressMessage(
    "Style",
    "IDE0305:Simplify collection initialization",
    Justification = "<Pending>",
    Scope = "member",
    Target = "~M:TomodachiDrawer.Core.CanvasDrawer.FineDetailTsp(TomodachiDrawer.Core.Interfaces.ISwitchOutput,TomodachiDrawer.Core.Models.ColourLayer,System.Single)"
)]
