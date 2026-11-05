using System.Buffers.Binary;
using VectorAtc.SimLink;
using Xunit;

namespace VectorAtc.Tests;

public class SimConnectFrameParserTests
{
    private static byte[] Frame(uint recvId = 8, uint requestId = 1, uint defineCount = 9, double[]? values = null)
    {
        values ??= new double[] { 55.0010, 37.0018, 512.5, 271.0, 12.0, -150.0, 1.0, 118100.0, 121500.0 };
        var buffer = new byte[40 + values.Length * 8];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0, 4), (uint)buffer.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(8, 4), recvId);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(12, 4), requestId);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(36, 4), defineCount);
        for (var i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteDoubleLittleEndian(buffer.AsSpan(40 + i * 8, 8), values[i]);
        return buffer;
    }

    [Fact]
    public void Parses_all_fields_in_definition_order()
    {
        Assert.True(SimConnectFrameParser.TryParseAircraft(Frame(), 1, out var s));
        Assert.NotNull(s);
        Assert.Equal(55.0010, s!.Position.LatitudeDeg);
        Assert.Equal(37.0018, s.Position.LongitudeDeg);
        Assert.Equal(512.5, s.AltitudeFeet);
        Assert.Equal(271.0, s.HeadingMagneticDeg);
        Assert.Equal(12.0, s.GroundSpeedKnots);
        Assert.Equal(-150.0, s.VerticalSpeedFpm);
        Assert.True(s.OnGround);
        Assert.Equal(118100, s.Com1ActiveKhz);
        Assert.Equal(121500, s.Com2ActiveKhz);
    }

    [Fact]
    public void Zero_on_ground_value_means_airborne()
    {
        var values = new double[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        Assert.True(SimConnectFrameParser.TryParseAircraft(Frame(values: values), 1, out var s));
        Assert.False(s!.OnGround);
    }

    [Fact]
    public void Rejects_other_message_kind()
    {
        Assert.False(SimConnectFrameParser.TryParseAircraft(Frame(recvId: 2), 1, out var s));
        Assert.Null(s);
    }

    [Fact]
    public void Rejects_other_request_id()
    {
        Assert.False(SimConnectFrameParser.TryParseAircraft(Frame(requestId: 7), 1, out _));
    }

    [Fact]
    public void Rejects_wrong_value_count()
    {
        Assert.False(SimConnectFrameParser.TryParseAircraft(Frame(defineCount: 8), 1, out _));
    }

    [Fact]
    public void Rejects_truncated_buffer()
    {
        var full = Frame();
        Assert.False(SimConnectFrameParser.TryParseAircraft(full.AsSpan(0, full.Length - 1), 1, out _));
        Assert.False(SimConnectFrameParser.TryParseAircraft(new byte[5], 1, out _));
    }
}
