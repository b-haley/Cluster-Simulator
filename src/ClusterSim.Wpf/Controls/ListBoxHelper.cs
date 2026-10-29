using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace ClusterSim.Wpf.Controls;

/// <summary>
/// Attached Property для биндинга ListBox.SelectedItems в MVVM.
/// Позволяет синхронизировать множественное выделение с коллекцией в ViewModel.
/// </summary>
public static class ListBoxHelper
{
    public static readonly DependencyProperty SelectedItemsProperty =
        DependencyProperty.RegisterAttached(
            "SelectedItems",
            typeof(IList),
            typeof(ListBoxHelper),
            new PropertyMetadata(null, OnSelectedItemsChanged));

    public static IList GetSelectedItems(DependencyObject obj) =>
        (IList)obj.GetValue(SelectedItemsProperty);

    public static void SetSelectedItems(DependencyObject obj, IList value) =>
        obj.SetValue(SelectedItemsProperty, value);

    private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox listBox) return;

        listBox.SelectionChanged -= ListBox_SelectionChanged;

        if (e.NewValue is IList collection)
        {
            foreach (var item in collection)
            {
                if (!listBox.SelectedItems.Contains(item))
                {
                    listBox.SelectedItems.Add(item);
                }
            }
            listBox.SelectionChanged += ListBox_SelectionChanged;
        }
    }

    private static void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox listBox) return;
        var collection = GetSelectedItems(listBox);
        if (collection is null) return;

        foreach (var item in e.RemovedItems)
        {
            collection.Remove(item);
        }
        foreach (var item in e.AddedItems)
        {
            if (!collection.Contains(item))
            {
                collection.Add(item);
            }
        }
    }
}