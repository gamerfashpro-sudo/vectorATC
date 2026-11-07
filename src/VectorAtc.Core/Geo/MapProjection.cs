using VectorAtc.Core.Models;

namespace VectorAtc.Core.Geo;

/// <summary>
/// Перевод координат в точки окна для карты «север вверху» вокруг заданного центра. Плоское приближение (долгота
/// сжимается на cos широты), для радиусов в десятки и сотни миль ошибка мала. Масштаб такой, что радиус
/// <see cref="RangeNm"/> достигает края меньшей стороны окна.
/// </summary>
public sealed class MapProjection
{
    public MapProjection(GeoPoint center, double rangeNm, double widthPx, double heightPx)
    {
        Center = center;
        RangeNm = rangeNm > 0 ? rangeNm : 1;
        WidthPx = Math.Max(0, widthPx);
        HeightPx = Math.Max(0, heightPx);
        PixelsPerNm = Math.Min(WidthPx, HeightPx) / 2.0 / RangeNm;
    }

    public GeoPoint Center { get; }
    public double RangeNm { get; }
    public double WidthPx { get; }
    public double HeightPx { get; }
    public double PixelsPerNm { get; }

    public (double X, double Y) Project(GeoPoint point)
    {
        var northNm = (point.LatitudeDeg - Center.LatitudeDeg) * 60.0;
        var eastNm = (point.LongitudeDeg - Center.LongitudeDeg) * 60.0 * Math.Cos(Center.LatitudeDeg * Math.PI / 180.0);
        return (WidthPx / 2.0 + eastNm * PixelsPerNm, HeightPx / 2.0 - northNm * PixelsPerNm);
    }

    /// <summary>Попадает ли точка окна в видимую область с запасом margin пикселей.</summary>
    public bool IsVisible((double X, double Y) p, double margin = 0) =>
        p.X >= -margin && p.X <= WidthPx + margin && p.Y >= -margin && p.Y <= HeightPx + margin;
}
