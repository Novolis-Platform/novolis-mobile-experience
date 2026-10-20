using Avalonia.Media;

namespace Novolis.MobileExperience.Shell;

/// <summary>Stable Slate semantic colors; user accent choices never change these meanings.</summary>
internal static class SlateVisualTokens
{
    public static readonly Color Background = Color.Parse("#0E1116");
    public static readonly Color Surface = Color.Parse("#131A22");
    public static readonly Color Raised = Color.Parse("#1D2631");
    public static readonly Color Border = Color.Parse("#303945");
    public static readonly Color Text = Color.Parse("#E8EDF2");
    public static readonly Color Muted = Color.Parse("#8DA0AE");
    public static readonly Color Command = Color.Parse("#EE9246");
    public static readonly Color CommandBright = Color.Parse("#EE9246");
    public static readonly Color Information = Color.Parse("#376FA8");
    public static readonly Color InformationDark = Color.Parse("#213D5A");
    public static readonly Color Success = Color.Parse("#60C39B");
    public static readonly Color SuccessDark = Color.Parse("#183C35");
    public static readonly Color Attention = Color.Parse("#E4AA55");
    public static readonly Color AttentionDark = Color.Parse("#4A351A");
    public static readonly Color Danger = Color.Parse("#B05B4B");
    public static readonly Color DangerDark = Color.Parse("#48252A");
    public static readonly Color Disabled = Color.Parse("#364451");
    public static readonly Color Privacy = Color.Parse("#785087");
    public static readonly Color Files = Color.Parse("#248782");
    public static readonly Color Updates = Color.Parse("#A97231");
    public static readonly Color System = Color.Parse("#364451");
    public static readonly Color Experiments = Color.Parse("#B05B4B");

    public static SolidColorBrush Brush(Color color) => new(color);
}
