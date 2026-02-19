using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace HardwareMonitorWidget.ViewModels;

public partial class MetricViewModel : ObservableObject
{
    [ObservableProperty]
    private string _label;

    [ObservableProperty]
    private string _unit;

    [ObservableProperty]
    private double _value;

    [ObservableProperty]
    private Brush _barBrush = Brushes.LimeGreen;

    [ObservableProperty]
    private Brush _textBrush = Brushes.LimeGreen;

    public string DisplayValue => $"{Value:0} {Unit}";

    public MetricViewModel(string label, string unit)
    {
        _label = label;
        _unit = unit;
    }

    partial void OnValueChanged(double value) => OnPropertyChanged(nameof(DisplayValue));
    partial void OnUnitChanged(string value)  => OnPropertyChanged(nameof(DisplayValue));
}
