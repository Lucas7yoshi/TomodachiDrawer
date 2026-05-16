using Avalonia.Controls;
using SkiaSharp;
using TomodachiDrawer.Core;
using TomodachiDrawer.Core.Models;

namespace TomodachiDrawer.UI.Avalonia;

public partial class ColourToHSVStepsTool : Window
{
    public ColourToHSVStepsTool()
    {
        InitializeComponent();
    }

    private void ColourHex_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!SKColor.TryParse(ColourHex.Text, out var skColor))
            return;
        
        var steps = ColourGameSteps.FromColour(skColor);

        if (steps.HueSteps > ColourGameSteps.FCR_HUE_SLIDER_STEP_COUNT / 2)
        {
            HueStepsOutput.Text = ((ColourGameSteps.FCR_HUE_SLIDER_STEP_COUNT - 1) - steps.HueSteps).ToString();
            HueStepsOutput.InnerRightContent = "taps from the right";
        }
        else
        {
            HueStepsOutput.Text = steps.HueSteps.ToString();
            HueStepsOutput.InnerRightContent = "taps from the left";
        }

        if (steps.SatSteps > ColourGameSteps.FCR_SATURATION_STEP_COUNT / 2)
        {
            SatStepsOutput.Text = ((ColourGameSteps.FCR_SATURATION_STEP_COUNT - 1) - steps.SatSteps).ToString();
            SatStepsLabel.Text = "Left:";
        }
        else
        {
            SatStepsOutput.Text = steps.SatSteps.ToString();
            SatStepsLabel.Text = "Right:";
        }
        SatStepsOutput.InnerRightContent = "taps";

        if (steps.ValSteps > ColourGameSteps.FCR_VALUE_STEP_COUNT / 2)
        {
            ValStepsOutput.Text = ((ColourGameSteps.FCR_VALUE_STEP_COUNT - 1) - steps.ValSteps).ToString();
            ValStepsLabel.Text = "Bottom:";
        }
        else
        {
            ValStepsOutput.Text = steps.ValSteps.ToString();
            ValStepsLabel.Text = "Top:";
        }
        ValStepsOutput.InnerRightContent = "taps";
    }
}