using System.Buffers.Binary;
using VectorAtc.Core.Models;

namespace VectorAtc.SimLink;

/// <summary>
/// Разбор сообщения SIMCONNECT_RECV_SIMOBJECT_DATA в <see cref="AircraftState"/>. Чистая функция без обращений к симулятору,
/// поэтому проверяется тестами на искусственных буферах.
/// Раскладка (little-endian): заголовок 12 байт (dwSize, dwVersion, dwID), затем dwRequestID, dwObjectID, dwDefineID,
/// dwFlags, dwentrynumber, dwoutof, dwDefineCount (по 4 байта), затем данные, начиная со смещения 40.
/// Данные: <see cref="Variables"/>.Length значений double в порядке определения.
/// </summary>
public static class SimConnectFrameParser
{
    public const uint RecvIdSimObjectData = 8;

    /// <summary>Имя переменной и единица измерения. Порядок задаёт порядок значений в сообщении.</summary>
    public static readonly IReadOnlyList<(string Name, string Units)> Variables = new[]
    {
        ("PLANE LATITUDE", "degrees"),
        ("PLANE LONGITUDE", "degrees"),
        ("INDICATED ALTITUDE", "feet"),
        ("PLANE HEADING DEGREES MAGNETIC", "degrees"),
        ("GROUND VELOCITY", "knots"),
        ("VERTICAL SPEED", "feet per minute"),
        ("SIM ON GROUND", "bool"),
        ("COM ACTIVE FREQUENCY:1", "KHz"),
        ("COM ACTIVE FREQUENCY:2", "KHz"),
    };

    private const int DefineCountOffset = 36;
    private const int RequestIdOffset = 12;
    private const int DataOffset = 40;

    private static double Value(ReadOnlySpan<byte> message, int i) =>
        BinaryPrimitives.ReadDoubleLittleEndian(message.Slice(DataOffset + i * sizeof(double), sizeof(double)));

    public static bool TryParseAircraft(ReadOnlySpan<byte> message, uint expectedRequestId, out AircraftState? state)
    {
        state = null;
        var count = Variables.Count;
        if (message.Length < DataOffset + count * sizeof(double))
            return false;
        if (BinaryPrimitives.ReadUInt32LittleEndian(message.Slice(8, 4)) != RecvIdSimObjectData)
            return false;
        if (BinaryPrimitives.ReadUInt32LittleEndian(message.Slice(RequestIdOffset, 4)) != expectedRequestId)
            return false;
        if (BinaryPrimitives.ReadUInt32LittleEndian(message.Slice(DefineCountOffset, 4)) != (uint)count)
            return false;

        state = new AircraftState(
            Position: new GeoPoint(Value(message, 0), Value(message, 1)),
            AltitudeFeet: Value(message, 2),
            HeadingMagneticDeg: Value(message, 3),
            GroundSpeedKnots: Value(message, 4),
            VerticalSpeedFpm: Value(message, 5),
            OnGround: Value(message, 6) > 0.5,
            Com1ActiveKhz: (int)Math.Round(Value(message, 7)),
            Com2ActiveKhz: (int)Math.Round(Value(message, 8)));
        return true;
    }
}
