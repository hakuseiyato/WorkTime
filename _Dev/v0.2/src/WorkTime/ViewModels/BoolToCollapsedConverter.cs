using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WorkTime.ViewModels;

/// <summary>
/// bool → Visibility 変換。
/// true で Collapsed、false で Visible (引数 "invert" で反転)。
/// </summary>
public class BoolToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool bv && bv;
        if (parameter is string s && s == "invert") b = !b;
        return b ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
