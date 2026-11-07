namespace VectorAtc.ViewModels;

public enum MapMarkerKind
{
    Own,
    Airborne,
    Ground
}

/// <summary>Значок на карте: центр в пикселях окна, курс для поворота и две строки подписи.</summary>
public sealed record MapMarker(double X, double Y, double HeadingDeg, string Title, string Subtitle, MapMarkerKind Kind);

/// <summary>Кольцо дальности: левый верхний угол и диаметр в пикселях, подпись над кольцом.</summary>
public sealed record MapRing(double Left, double Top, double Diameter, string Label, double LabelX, double LabelY);
