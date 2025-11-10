namespace RTUB.Web.EmailTemplates.Models;

/// <summary>
/// Model for button partial
/// </summary>
public class ButtonModel
{
    public string Href { get; set; } = "#";
    public string Label { get; set; } = "Clique aqui";
    public string BgColor { get; set; } = "#6E56CF";
    public string TextColor { get; set; } = "#FFFFFF";
    public bool IsSecondary { get; set; } = false;
}
