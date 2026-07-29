using System;
using Avalonia.Controls;

namespace TomodachiDrawer.UI.Avalonia;

public partial class EarlyTspExitTool : Window
{
    // passed through from main window for simplicities sake.
    private readonly AppSettings _settings;
    private readonly Action _save;

    public EarlyTspExitTool()
        : this(new AppSettings(), () => { }) { }

    internal EarlyTspExitTool(AppSettings settings, Action save)
    {
        _settings = settings;
        _save = save;
        InitializeComponent();

        EnabledCheckBox.IsChecked = _settings.EarlyTspConvergenceEnabled;
        ThresholdUpDown.Value = (decimal)_settings.EarlyTspExitThreshold;
        DistanceUpDown.Value = _settings.EarlyTspExitSolutionsDistance;

        UpdateKnobState();
    }

    private void UpdateKnobState()
    {
        bool enabled = EnabledCheckBox.IsChecked ?? false;
        ThresholdUpDown.IsEnabled = enabled;
        DistanceUpDown.IsEnabled = enabled;
    }

    private void EnabledCheckBox_IsCheckedChanged(
        object? sender,
        global::Avalonia.Interactivity.RoutedEventArgs e
    )
    {
        UpdateKnobState();
        _settings.EarlyTspConvergenceEnabled = EnabledCheckBox.IsChecked ?? false;
        _save();
    }

    private void ThresholdUpDown_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        _settings.EarlyTspExitThreshold = (double)(ThresholdUpDown.Value ?? 0.05m);
        _save();
    }

    private void DistanceUpDown_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        _settings.EarlyTspExitSolutionsDistance = (int)(DistanceUpDown.Value ?? 10m);
        _save();
    }
}
