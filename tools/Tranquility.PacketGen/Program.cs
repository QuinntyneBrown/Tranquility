using System.Net.Sockets;
using Tranquility.Core.Ccsds;

// PacketGen: sends CCSDS space packets carrying SampleSat science telemetry
// over UDP for demonstrations and end-to-end tests of the vertical slice.
//
// Usage: packetgen [host] [port] [count] [intervalMs]
// Defaults: 127.0.0.1 10015 10 1000

string host = args.Length > 0 ? args[0] : "127.0.0.1";
int port = args.Length > 1 ? int.Parse(args[1]) : 10015;
int count = args.Length > 2 ? int.Parse(args[2]) : 10;
int intervalMs = args.Length > 3 ? int.Parse(args[3]) : 1000;

Console.WriteLine($"PacketGen: sending {count} SampleSat packets to {host}:{port} every {intervalMs} ms");

using var udp = new UdpClient();
var random = new Random();

for (int i = 0; i < count; i++)
{
    // Sweep the temperature raw counts so values cross the warning threshold
    // (eng = -20 + 0.05 * raw; warning band is [-10, 30] per SampleSat.xml).
    ushort counter = (ushort)i;
    ushort tempRaw = (ushort)(600 + (i * 97) % 500); // eng 10.0 .. 34.95
    byte mode = (byte)(i % 3);
    float volts = 1.4f + (float)random.NextDouble() * 0.2f;

    byte[] packet = BuildSciPacket(counter, tempRaw, mode, volts, sequenceCount: (ushort)(i & 0x3FFF));
    await udp.SendAsync(packet, host, port);

    double eng = -20 + 0.05 * tempRaw;
    Console.WriteLine($"  sent seq={i} counter={counter} tempRaw={tempRaw} (eng {eng:F2}) mode={mode} volts={volts:F3}");

    if (i < count - 1)
    {
        await Task.Delay(intervalMs);
    }
}

Console.WriteLine("Done.");
return 0;

// Builds one SciPacket: 6-byte SPP primary header (CCSDS 133.0-B) followed by
// Counter(u16) Temperature(u12) Mode(u4) BusVoltage(f32), big-endian.
static byte[] BuildSciPacket(ushort counter, ushort tempRaw, byte mode, float volts, ushort sequenceCount)
{
    const int dataLength = 8; // 2 + 1.5 + 0.5 + 4 bytes
    var packet = new byte[SpacePacketHeader.Length + dataLength];

    // Primary header: version 0, TM, no secondary header, APID 100, unsegmented.
    packet[0] = (byte)(100 >> 8 & 0x07);
    packet[1] = 100 & 0xFF;
    packet[2] = (byte)(0xC0 | (sequenceCount >> 8 & 0x3F));
    packet[3] = (byte)(sequenceCount & 0xFF);
    ushort lengthField = dataLength - 1;
    packet[4] = (byte)(lengthField >> 8);
    packet[5] = (byte)(lengthField & 0xFF);

    packet[6] = (byte)(counter >> 8);
    packet[7] = (byte)(counter & 0xFF);
    packet[8] = (byte)(tempRaw >> 4);
    packet[9] = (byte)((tempRaw & 0x0F) << 4 | (mode & 0x0F));

    var voltsBits = BitConverter.GetBytes(volts);
    if (BitConverter.IsLittleEndian)
    {
        Array.Reverse(voltsBits);
    }

    voltsBits.CopyTo(packet, 10);
    return packet;
}
