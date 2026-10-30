using System;
using System.Globalization;
using System.Windows.Data;

namespace ClusterSim.Wpf.Converters;

/// <summary>Сравнивает значение перечисления с именем из ConverterParameter.</summary>
public sealed class EnumMatchConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString();

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is string name
            ? Enum.Parse(targetType, name)
            : Binding.DoNothing;
}