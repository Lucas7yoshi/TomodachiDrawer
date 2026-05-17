using Avalonia.Controls;
using Avalonia.Input;
using HarfBuzzSharp;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using Key = Avalonia.Input.Key;

namespace TomodachiDrawer.UI.Avalonia;

public partial class VirtualGamepadController : Window
{
    public IXbox360Controller? GamepadController = null;
    private string? _currentStickFreeStyle = null;

    public VirtualGamepadController()
    {
        InitializeComponent();
    }

    private async void HandlePressOrRelease(object? sender, bool pressed, PointerPointProperties pointerProperties)
    {
        if (pressed && !pointerProperties.IsLeftButtonPressed)
            return;

        if (!(sender is Control ctrl && ctrl.Tag is string tag))
            return;

        HandlePressOrRelease(tag, pressed);
    }

    private async void HandlePressOrRelease(string tag, bool pressed)
    {
        if (GamepadController == null)
            return;

        Xbox360Button? xboxBtn = tag switch
        {
            "ZL" => Xbox360Button.LeftShoulder,
            "ZR" => Xbox360Button.RightShoulder,
            "LS" => Xbox360Button.LeftThumb,
            "RS" => Xbox360Button.RightThumb,
            "Up" => Xbox360Button.Up,
            "Down" => Xbox360Button.Down,
            "Left" => Xbox360Button.Left,
            "Right" => Xbox360Button.Right,
            "X" => Xbox360Button.Y,
            "Y" => Xbox360Button.X,
            "A" => Xbox360Button.B,
            "B" => Xbox360Button.A,
            _ => null,
        };
        if (xboxBtn != null)
        {
            GamepadController.SetButtonState(xboxBtn, pressed);
            return;
        }

        Xbox360Slider? xboxSlider = tag switch
        {
            "L" => Xbox360Slider.LeftTrigger,
            "R" => Xbox360Slider.LeftTrigger,
            _ => null,
        };
        if (xboxSlider != null)
        {
            GamepadController.SetSliderValue(xboxSlider, (byte)(pressed ? 0xFF : 0x00));
            return;
        }

        (Xbox360Axis? xboxAxis, short xboxAxisValue) = tag switch
        {
            "RS_Right" => (Xbox360Axis.RightThumbX, short.MaxValue),
            "RS_Left" => (Xbox360Axis.RightThumbX, short.MinValue),
            "RS_Up" => (Xbox360Axis.RightThumbY, short.MaxValue),
            "RS_Down" => (Xbox360Axis.RightThumbY, short.MinValue),
            "LS_Right" => (Xbox360Axis.LeftThumbX, short.MaxValue),
            "LS_Left" => (Xbox360Axis.LeftThumbX, short.MinValue),
            "LS_Up" => (Xbox360Axis.LeftThumbY, short.MaxValue),
            "LS_Down" => (Xbox360Axis.LeftThumbY, short.MinValue),
            _ => (null, 0),
        };
        if (xboxAxis != null)
        {
            GamepadController.SetAxisValue(xboxAxis, pressed ? xboxAxisValue : (short)0);
            return;
        }
    }

    private void Button_PointerPressed(object? sender, PointerPressedEventArgs e) =>
        HandlePressOrRelease(sender, pressed: true, pointerProperties: e.Properties);

    private void Button_PointerReleased(object? sender, PointerReleasedEventArgs e) =>
        HandlePressOrRelease(sender, pressed: false, pointerProperties: e.Properties);

    private void Stick_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!(sender is Control ctrl && ctrl.Tag is string tag))
            return;

        if (!e.Properties.IsRightButtonPressed)
            return;

        _currentStickFreeStyle = tag;

        e.Handled = true;
    }

    private void Stick_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (GamepadController == null)
            return;


        if (_currentStickFreeStyle == null)
            return;
        
        var (xAxis, yAxis) = TagToAxis(_currentStickFreeStyle);
        GamepadController.SetAxisValue(xAxis, 0);
        GamepadController.SetAxisValue(yAxis, 0);

        _currentStickFreeStyle = null;

        e.Handled = true;
    }

    private void Stick_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (GamepadController == null)
            return;

        if (!(sender is Control ctrl && ctrl.Tag is string tag))
            return;

        if (_currentStickFreeStyle != tag)
            return;

        var (xAxis, yAxis) = TagToAxis(tag);

        var pos = e.GetPosition(ctrl);

        var xPercent = Math.Clamp(pos.X / ctrl.Width, 0, 1);
        var yPercent = Math.Clamp(pos.Y / ctrl.Height, 0, 1);

        short xAxisValue = (short)(xPercent * (short.MaxValue - short.MinValue) - short.MinValue);
        short yAxisValue = (short)((1 - yPercent) * (short.MaxValue - short.MinValue) - short.MinValue);

        GamepadController.SetAxisValue(xAxis, xAxisValue);
        GamepadController.SetAxisValue(yAxis, yAxisValue);
    }

    private static (Xbox360Axis xAxis, Xbox360Axis yAxis) TagToAxis(string tag)
    {
        return tag switch
        {
            "LS" => (Xbox360Axis.LeftThumbX, Xbox360Axis.LeftThumbY),
            "RS" => (Xbox360Axis.RightThumbX, Xbox360Axis.RightThumbY),
            _ => throw new NotImplementedException()
        };
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        var tag = KeyToTag(e.Key);
        if (tag == null)
            return;

        HandlePressOrRelease(tag, pressed: true);
    }

    private void Window_KeyUp(object? sender, KeyEventArgs e)
    {
        var tag = KeyToTag(e.Key);
        if (tag == null)
            return;

        HandlePressOrRelease(tag, pressed: false);
    }

    private static string? KeyToTag(Key key) => key switch
    {
        Key.Q => "ZL",
        Key.E => "L",
        Key.F => "LS",
        Key.W => "LS_Up",
        Key.S => "LS_Down",
        Key.A => "LS_Left",
        Key.D => "LS_Right",
        Key.Up => "Up",
        Key.Down => "Down",
        Key.Left => "Left",
        Key.Right => "Right",
        Key.O => "ZR",
        Key.U => "R",
        Key.Z => "A",
        Key.X => "B",
        Key.C => "X",
        Key.Y => "V",
        Key.H => "RS",
        Key.I => "Up",
        Key.K => "Down",
        Key.J => "Left",
        Key.L => "Right",
        _ => null,
    };
}
