using System;
using System.Globalization;
using System.Windows.Data;

namespace ClusterSim.Wpf.Converters;

/// <summary>Инвертирует булево значение для привязок IsEnabled.</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool boolValue ? !boolValue : value;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool boolValue ? !boolValue : value;
}