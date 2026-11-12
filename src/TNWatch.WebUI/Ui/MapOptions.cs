namespace TNWatch.WebUI.Ui;

/// <summary>Configuration section "Map" (passed to the Leaflet interop).</summary>
public sealed class MapOptions
{
    public const string SectionName = "Map";

    public string TileUrl { get; set; } = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";

    public string Attribution { get; set; } = "&copy; OpenStreetMap-Mitwirkende";

    public double CenterLatitude { get; set; } = 48.78;

    public double CenterLongitude { get; set; } = 9.25;

    public int Zoom { get; set; } = 10;
}
