using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PersonalCloudLibrarySource
{
    internal sealed class ZipCentralEntry
    {
        public string Name { get; set; } = string.Empty;
        public bool IsUnsafeLinkOrSpecialFile { get; set; }
        public uint ExpectedCrc32 { get; set; }
    }

    // Strict ZIP32 central-directory inspection for the .NET Framework 4.6.2
    // Playnite host, which lacks ZipArchiveEntry.ExternalAttributes.
    // Deliberately fails closed for ZIP64/multi-disk/encryption/ambiguous names.
    internal static class ZipCentralDirectoryInspector
    {
        private const uint EndSignature = 0x06054B50;
        private const uint EntrySignature = 0x02014B50;
        private const int MaxCentralBytes = 32 * 1024 * 1024;

        public static IReadOnlyList<ZipCentralEntry> Read(Stream source)
        {
            if (source == null || !source.CanRead || !source.CanSeek ||
                source.Length < 22)
                throw new InvalidDataException("ZIP central directory is unavailable.");

            var tailSize = (int)Math.Min(source.Length, 65535 + 22);
            source.Position = source.Length - tailSize;
            var tail = new byte[tailSize];
            ReadExactly(source, tail, 0, tail.Length);
            var eocd = -1;
            for (var i = tail.Length - 22; i >= 0; i--)
            {
                if (UInt32(tail, i) != EndSignature) continue;
                var commentLength = UInt16(tail, i + 20);
                if (i + 22 + commentLength != tail.Length) continue;
                eocd = i;
                break;
            }
            if (eocd < 0)
                throw new InvalidDataException("ZIP end-of-central-directory record is invalid.");

            if (UInt16(tail, eocd + 4) != 0 ||
                UInt16(tail, eocd + 6) != 0 ||
                UInt16(tail, eocd + 8) != UInt16(tail, eocd + 10))
                throw new InvalidDataException("Multi-disk ZIPs are unsupported.");

            var count = UInt16(tail, eocd + 10);
            var centralBytes = UInt32(tail, eocd + 12);
            var centralOffset = UInt32(tail, eocd + 16);
            if (count == 0 || count == ushort.MaxValue ||
                centralBytes == uint.MaxValue || centralOffset == uint.MaxValue)
                throw new InvalidDataException("Empty or ZIP64 archives require separate review.");
            if (count > ArchiveInspectionPolicy.MaxEntries ||
                centralBytes > MaxCentralBytes)
                throw new InvalidDataException("ZIP central directory exceeds the safety cap.");

            var eocdAbsolute = source.Length - tailSize + eocd;
            if ((long)centralOffset + centralBytes != eocdAbsolute)
                throw new InvalidDataException(
                    "ZIP central directory offset, length or trailing records are inconsistent.");

            source.Position = centralOffset;
            var entries = new List<ZipCentralEntry>(count);
            var expectedEnd = (long)centralOffset + centralBytes;
            using (var reader = new BinaryReader(source, Encoding.UTF8, true))
            {
                for (var index = 0; index < count; index++)
                {
                    if (source.Position + 46 > expectedEnd ||
                        reader.ReadUInt32() != EntrySignature)
                        throw new InvalidDataException("ZIP central entry header is invalid.");
                    var madeBy = reader.ReadUInt16();
                    var needed = reader.ReadUInt16();
                    var flags = reader.ReadUInt16();
                    var compression = reader.ReadUInt16();
                    reader.ReadUInt16(); // modification time
                    reader.ReadUInt16(); // modification date
                    var crc32 = reader.ReadUInt32();
                    var compressed = reader.ReadUInt32();
                    var uncompressed = reader.ReadUInt32();
                    var nameLength = reader.ReadUInt16();
                    var extraLength = reader.ReadUInt16();
                    var commentLength = reader.ReadUInt16();
                    var diskStart = reader.ReadUInt16();
                    reader.ReadUInt16(); // internal attributes
                    var external = reader.ReadUInt32();
                    var localOffset = reader.ReadUInt32();

                    if (diskStart != 0 || compressed == uint.MaxValue ||
                        uncompressed == uint.MaxValue || localOffset == uint.MaxValue ||
                        (flags & (1 | 0x40 | 0x2000)) != 0 ||
                        (compression != 0 && compression != 8) ||
                        nameLength == 0 || nameLength > 4 * ArchiveInspectionPolicy.MaxPathCharacters ||
                        source.Position + nameLength + extraLength + commentLength > expectedEnd ||
                        localOffset >= centralOffset)
                        throw new InvalidDataException(
                            "Unsupported ZIP feature, encryption, split volume or entry metadata.");

                    var nameBytes = reader.ReadBytes(nameLength);
                    if (nameBytes.Length != nameLength)
                        throw new InvalidDataException("Truncated ZIP filename.");
                    // ASCII or explicitly declared UTF-8 only. Other code pages
                    // are ambiguous on Windows and require a separately reviewed decoder.
                    if ((flags & 0x800) == 0)
                    {
                        foreach (var value in nameBytes)
                            if (value > 127)
                                throw new InvalidDataException("Ambiguous non-UTF-8 ZIP entry name.");
                    }
                    var name = (flags & 0x800) != 0
                        ? new UTF8Encoding(false, true).GetString(nameBytes)
                        : Encoding.ASCII.GetString(nameBytes);
                    source.Position += extraLength + commentLength;

                    var directory = name.EndsWith("/", StringComparison.Ordinal) ||
                        name.EndsWith("\\", StringComparison.Ordinal);
                    var unixKind = (external >> 16) & 0xF000;
                    var unsafeType = unixKind != 0 &&
                        unixKind != (directory ? 0x4000u : 0x8000u);
                    // DOS link/reparse attribute, if the producer represented it.
                    unsafeType |= (external & (uint)FileAttributes.ReparsePoint) != 0;

                    entries.Add(new ZipCentralEntry
                    {
                        Name = name,
                        IsUnsafeLinkOrSpecialFile = unsafeType,
                        ExpectedCrc32 = crc32
                    });
                }
            }
            if (source.Position != expectedEnd)
                throw new InvalidDataException("ZIP central directory has unsupported trailing data.");
            return entries;
        }

        private static void ReadExactly(Stream input, byte[] buffer, int start, int length)
        {
            var count = 0;
            while (count < length)
            {
                var read = input.Read(buffer, start + count, length - count);
                if (read <= 0) throw new InvalidDataException("ZIP record is truncated.");
                count += read;
            }
        }

        private static ushort UInt16(byte[] data, int position)
            => (ushort)(data[position] | (data[position + 1] << 8));

        private static uint UInt32(byte[] data, int position)
            => (uint)(data[position] | data[position + 1] << 8 |
                data[position + 2] << 16 | data[position + 3] << 24);
    }
}
