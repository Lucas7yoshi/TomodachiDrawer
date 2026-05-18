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
        
        var (HueSteps, SatSteps, ValSteps) = ColourPickerRouter.FromColour(skColor);

        if (HueSteps > ColourPickerRouter.FCR_HUE_SLIDER_STEP_COUNT / 2)
        {
            HueStepsOutput.Text = ((ColourPickerRouter.FCR_HUE_SLIDER_STEP_COUNT - 1) - HueSteps).ToString();
            HueStepsOutput.InnerRightContent = "ZL taps";
        }
        else
        {
            HueStepsOutput.Text = HueSteps.ToString();
            HueStepsOutput.InnerRightContent = "ZR taps";
        }

        if (SatSteps > ColourPickerRouter.FCR_SATURATION_STEP_COUNT / 2)
        {
            SatStepsOutput.Text = ((ColourPickerRouter.FCR_SATURATION_STEP_COUNT - 1) - SatSteps).ToString();
            SatStepsOutput.InnerRightContent = "right taps";
        }
        else
        {
            SatStepsOutput.Text = SatSteps.ToString();
            SatStepsOutput.InnerRightContent = "left taps";
        }

        if (ValSteps > ColourPickerRouter.FCR_VALUE_STEP_COUNT / 2)
        {
            ValStepsOutput.Text = ((ColourPickerRouter.FCR_VALUE_STEP_COUNT - 1) - ValSteps).ToString();
            ValStepsOutput.InnerRightContent = "up taps";
        }
        else
        {
            ValStepsOutput.Text = ValSteps.ToString();
            ValStepsOutput.InnerRightContent = "down taps";
        }
    }
}