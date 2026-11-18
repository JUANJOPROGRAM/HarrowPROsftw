using System.Windows;
using System.Windows.Media.Animation;

namespace HarrowStudio.Desktop;

public sealed class GridLengthAnimation : AnimationTimeline
{
    public GridLength From { get; set; }
    public GridLength To { get; set; }
    public IEasingFunction? EasingFunction { get; set; }

    public override Type TargetPropertyType => typeof(GridLength);

    protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
    {
        var progress = animationClock.CurrentProgress ?? 1d;
        progress = EasingFunction?.Ease(progress) ?? progress;
        var from = From.IsAbsolute ? From.Value : defaultOriginValue is GridLength origin ? origin.Value : 0;
        var to = To.IsAbsolute ? To.Value : defaultDestinationValue is GridLength destination ? destination.Value : 0;
        return new GridLength(from + (to - from) * progress, GridUnitType.Pixel);
    }
}
