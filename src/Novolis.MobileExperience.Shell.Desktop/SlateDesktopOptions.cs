using Novolis.MobileExperience.Shell;

namespace Novolis.MobileExperience.Shell.Desktop;

/// <summary>Command-line presentation options for the Windows Slate prototype.</summary>
public sealed record SlateDesktopOptions(
    double Width,
    double Height,
    SlateNavigationEdge Navigation)
{
    /// <summary>Parses --width, --height, --size WIDTHxHEIGHT, and --edge.</summary>
    public static SlateDesktopOptions Parse(IReadOnlyList<string> args)
    {
        var width = 1920d;
        var height = 1200d;
        var navigation = SlateNavigationEdge.Automatic;

        for (var index = 0; index < args.Count; index++)
        {
            switch (args[index].ToLowerInvariant())
            {
                case "--width" when index + 1 < args.Count &&
                                   double.TryParse(args[++index], out var parsedWidth):
                    width = parsedWidth;
                    break;
                case "--height" when index + 1 < args.Count &&
                                    double.TryParse(args[++index], out var parsedHeight):
                    height = parsedHeight;
                    break;
                case "--size" when index + 1 < args.Count:
                    ParseSize(args[++index], ref width, ref height);
                    break;
                case "--edge" when index + 1 < args.Count:
                    navigation = ParseEdge(args[++index]);
                    break;
            }
        }

        return new SlateDesktopOptions(Math.Max(320, width), Math.Max(480, height), navigation);
    }

    private static void ParseSize(string value, ref double width, ref double height)
    {
        var parts = value.Split('x', 'X');
        if (parts.Length == 2
            && double.TryParse(parts[0], out var parsedWidth)
            && double.TryParse(parts[1], out var parsedHeight))
        {
            width = parsedWidth;
            height = parsedHeight;
        }
    }

    private static SlateNavigationEdge ParseEdge(string value) =>
        value.ToLowerInvariant() switch
        {
            "left" => SlateNavigationEdge.Left,
            "right" => SlateNavigationEdge.Right,
            "top" => SlateNavigationEdge.Top,
            "bottom" => SlateNavigationEdge.Bottom,
            _ => SlateNavigationEdge.Automatic,
        };
}
