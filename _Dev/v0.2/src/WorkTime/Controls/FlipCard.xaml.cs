using System.Windows;
using UserControl = System.Windows.Controls.UserControl;

namespace WorkTime.Controls;

public partial class FlipCard : UserControl
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(FlipCard),
            new PropertyMetadata("00"));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(FlipCard),
            new PropertyMetadata(""));

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public FlipCard()
    {
        InitializeComponent();
    }
}
