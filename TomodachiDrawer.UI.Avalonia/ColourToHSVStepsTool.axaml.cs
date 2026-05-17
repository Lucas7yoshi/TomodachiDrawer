using Avalonia.Controls;

using SkiaSharp;

using TomodachiDrawer.Core;

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
        
        var steps = ColourPickerRouter.FromColour(skColor);

        if (steps.HueSteps > ColourPickerRouter.FCR_HUE_SLIDER_STEP_COUNT / 2)
        {
            HueStepsOutput.Text = ((ColourPickerRouter.FCR_HUE_SLIDER_STEP_COUNT - 1) - steps.HueSteps).ToString();
            HueStepsOutput.InnerRightContent = "ZL taps";
        }
        else
        {
            HueStepsOutput.Text = steps.HueSteps.ToString();
            HueStepsOutput.InnerRightContent = "ZR taps";
        }

        if (steps.SatSteps > ColourPickerRouter.FCR_SATURATION_STEP_COUNT / 2)
        {
            SatStepsOutput.Text = ((ColourPickerRouter.FCR_SATURATION_STEP_COUNT - 1) - steps.SatSteps).ToString();
            SatStepsOutput.InnerRightContent = "right taps";
        }
        else
        {
            SatStepsOutput.Text = steps.SatSteps.ToString();
            SatStepsOutput.InnerRightContent = "left taps";
        }

        if (steps.ValSteps > ColourPickerRouter.FCR_VALUE_STEP_COUNT / 2)
        {
            ValStepsOutput.Text = ((ColourPickerRouter.FCR_VALUE_STEP_COUNT - 1) - steps.ValSteps).ToString();
            ValStepsOutput.InnerRightContent = "up taps";
        }
        else
        {
            ValStepsOutput.Text = steps.ValSteps.ToString();
            ValStepsOutput.InnerRightContent = "down taps";
        }
    }
}