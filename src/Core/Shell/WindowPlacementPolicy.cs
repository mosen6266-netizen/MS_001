namespace Mosen.GroupScheduler.Core.Shell;

/// <summary>Pure geometry policy; can be tested without a graphical desktop or Windows.</summary>
public sealed record WindowPlacement(double Left, double Top, double Width, double Height, bool Maximized);
public sealed record DisplayArea(double Left, double Top, double Width, double Height);
public static class WindowPlacementPolicy
{
    public const double DefaultWidth = 1180;
    public const double DefaultHeight = 760;
    public static WindowPlacement Normalize(WindowPlacement? saved, DisplayArea workArea)
    {
        static bool Valid(double v) => double.IsFinite(v);
        if (!Valid(workArea.Left) || !Valid(workArea.Top) || !Valid(workArea.Width) || !Valid(workArea.Height)
            || workArea.Width < 100 || workArea.Height < 100)
            throw new ArgumentOutOfRangeException(nameof(workArea));
        double width = Math.Min(Math.Max(640, saved?.Width is double w && Valid(w) ? w : DefaultWidth), workArea.Width);
        double height = Math.Min(Math.Max(420, saved?.Height is double h && Valid(h) ? h : DefaultHeight), workArea.Height);
        double desiredX = saved?.Left is double x && Valid(x) ? x : workArea.Left + (workArea.Width - width) / 2;
        double desiredY = saved?.Top is double y && Valid(y) ? y : workArea.Top + (workArea.Height - height) / 2;
        double left = Math.Clamp(desiredX, workArea.Left, workArea.Left + workArea.Width - width);
        double top = Math.Clamp(desiredY, workArea.Top, workArea.Top + workArea.Height - height);
        return new WindowPlacement(left, top, width, height, saved?.Maximized ?? false);
    }
}
