using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ClusterSim.Wpf.Controls;

[TemplatePart(Name = PartTrack, Type = typeof(Border))]
[TemplatePart(Name = PartRange, Type = typeof(Border))]
[TemplatePart(Name = PartLowerThumb, Type = typeof(Thumb))]
[TemplatePart(Name = PartUpperThumb, Type = typeof(Thumb))]
public class RangeSlider : Control
{
    private const string PartTrack = "PART_Track";
    private const string PartRange = "PART_Range";
    private const string PartLowerThumb = "PART_LowerThumb";
    private const string PartUpperThumb = "PART_UpperThumb";

    private Border? _track;
    private Border? _range;
    private Thumb? _lowerThumb;
    private Thumb? _upperThumb;

    static RangeSlider()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(RangeSlider),
            new FrameworkPropertyMetadata(typeof(RangeSlider)));
    }

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(RangeSlider),
            new PropertyMetadata(0.0, OnRangeChanged));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(RangeSlider),
            new PropertyMetadata(100.0, OnRangeChanged));

    public static readonly DependencyProperty LowerValueProperty =
        DependencyProperty.Register(nameof(LowerValue), typeof(double), typeof(RangeSlider),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRangeChanged));

    public static readonly DependencyProperty UpperValueProperty =
        DependencyProperty.Register(nameof(UpperValue), typeof(double), typeof(RangeSlider),
            new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRangeChanged));

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double LowerValue
    {
        get => (double)GetValue(LowerValueProperty);
        set => SetValue(LowerValueProperty, value);
    }

    public double UpperValue
    {
        get => (double)GetValue(UpperValueProperty);
        set => SetValue(UpperValueProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _track = GetTemplateChild(PartTrack) as Border;
        _range = GetTemplateChild(PartRange) as Border;
        _lowerThumb = GetTemplateChild(PartLowerThumb) as Thumb;
        _upperThumb = GetTemplateChild(PartUpperThumb) as Thumb;

        if (_lowerThumb is not null)
        {
            _lowerThumb.DragDelta += OnLowerDrag;
        }

        if (_upperThumb is not null)
        {
            _upperThumb.DragDelta += OnUpperDrag;
        }

        SizeChanged += (_, _) => UpdateVisuals();
        UpdateVisuals();
    }

    private void OnLowerDrag(object sender, DragDeltaEventArgs e) =>
        SetCurrentValue(LowerValueProperty, ClampLower(LowerValue + e.HorizontalChange * ValuePerPixel));

    private void OnUpperDrag(object sender, DragDeltaEventArgs e) =>
        SetCurrentValue(UpperValueProperty, ClampUpper(UpperValue + e.HorizontalChange * ValuePerPixel));

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RangeSlider slider)
        {
            slider.NormalizeValues();
            slider.UpdateVisuals();
        }
    }

    private void NormalizeValues()
    {
        var lower = Math.Min(Math.Max(LowerValue, Minimum), Maximum);
        var upper = Math.Min(Math.Max(UpperValue, Minimum), Maximum);

        if (lower > upper)
        {
            lower = upper;
        }

        if (Math.Abs(LowerValue - lower) > double.Epsilon)
        {
            SetCurrentValue(LowerValueProperty, lower);
        }

        if (Math.Abs(UpperValue - upper) > double.Epsilon)
        {
            SetCurrentValue(UpperValueProperty, upper);
        }
    }

    private double ClampLower(double value) =>
        Math.Min(Math.Max(value, Minimum), UpperValue);

    private double ClampUpper(double value) =>
        Math.Max(Math.Min(value, Maximum), LowerValue);

    private double TrackWidth => _track?.ActualWidth ?? 0;

    private double ThumbWidth => _lowerThumb?.ActualWidth > 0 ? _lowerThumb.ActualWidth : 16;

    private double ValuePerPixel
    {
        get
        {
            var usable = Math.Max(TrackWidth - ThumbWidth, 1);
            return (Maximum - Minimum) / usable;
        }
    }

    private void UpdateVisuals()
    {
        if (_track is null || _range is null || _lowerThumb is null || _upperThumb is null)
        {
            return;
        }

        var span = Math.Max(Maximum - Minimum, double.Epsilon);
        var usable = Math.Max(TrackWidth - ThumbWidth, 1);

        var lowerX = (LowerValue - Minimum) / span * usable;
        var upperX = (UpperValue - Minimum) / span * usable;

        Canvas.SetLeft(_lowerThumb, lowerX);
        Canvas.SetLeft(_upperThumb, upperX);
        Canvas.SetLeft(_range, lowerX + ThumbWidth / 2);
        _range.Width = Math.Max(upperX - lowerX, 0);
    }
}