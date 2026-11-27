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
        private const uint LocalEntrySignature = 0x04034B50;

        private sealed class LocalMember
        {
            public long Offset;
            public uint CompressedBytes;
            public uint UncompressedBytes;
            public uint Crc32;
            public ushort Flags;
            public ushort Compression;
            public byte[] NameBytes;
        }
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
            // ZIP32 count is a 16-bit value; its sentinel was rejected above.
            // The general policy permits 100,000 entries, beyond ZIP32's range.
            if (centralBytes > MaxCentralBytes)
                throw new InvalidDataException("ZIP central directory exceeds the safety cap.");

            var eocdAbsolute = source.Length - tailSize + eocd;
            if ((long)centralOffset + centralBytes != eocdAbsolute)
                throw new InvalidDataException(
                    "ZIP central directory offset, length or trailing records are inconsistent.");

            source.Position = centralOffset;
            var entries = new List<ZipCentralEntry>(count);
            var members = new List<LocalMember>(count);
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

                    members.Add(new LocalMember
                    {
                        Offset = localOffset,
                        CompressedBytes = compressed,
                        UncompressedBytes = uncompressed,
                        Crc32 = crc32,
                        Flags = flags,
                        Compression = compression,
                        NameBytes = nameBytes
                    });
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
            ValidateLocalRanges(source, centralOffset, members);
            return entries;
        }

        // Compare the central and local representations of every member.
        // Require disjoint header, payload and optional ZIP32 descriptor
        // intervals. Unclaimed padding is allowed; ZIP64 is not supported.
        private static void ValidateLocalRanges(Stream source, long centralOffset,
            List<LocalMember> members)
        {
            members.Sort((left, right) => left.Offset.CompareTo(right.Offset));
            var header = new byte[30];
            long previousEnd = 0;
            for (var index = 0; index < members.Count; index++)
            {
                var member = members[index];
                var limit = index + 1 < members.Count ? members[index + 1].Offset : centralOffset;
                if (member.Offset < previousEnd || limit > centralOffset ||
                    member.Offset > limit - header.Length)
                    throw new InvalidDataException("ZIP local member ranges overlap or exceed the data area.");

                source.Position = member.Offset;
                ReadExactly(source, header, 0, header.Length);
                if (UInt32(header, 0) != LocalEntrySignature ||
                    UInt16(header, 6) != member.Flags ||
                    UInt16(header, 8) != member.Compression)
                    throw new InvalidDataException("ZIP local member header disagrees with central metadata.");
                var nameLength = UInt16(header, 26);
                var extraLength = UInt16(header, 28);
                var dataStart = member.Offset + header.Length + nameLength + extraLength;
                if (nameLength != member.NameBytes.Length || dataStart > limit ||
                    member.CompressedBytes > limit - dataStart)
                    throw new InvalidDataException("ZIP local member data exceeds the data area or has an inconsistent name.");
                var name = new byte[nameLength];
                ReadExactly(source, name, 0, name.Length);
                for (var i = 0; i < name.Length; i++)
                    if (name[i] != member.NameBytes[i])
                        throw new InvalidDataException("ZIP local filename disagrees with central metadata.");

                var descriptor = (member.Flags & 8) != 0;
                if (!MatchesLocalValue(UInt32(header, 14), member.Crc32, descriptor) ||
                    !MatchesLocalValue(UInt32(header, 18), member.CompressedBytes, descriptor) ||
                    !MatchesLocalValue(UInt32(header, 22), member.UncompressedBytes, descriptor))
                    throw new InvalidDataException("ZIP local CRC or length disagrees with central metadata.");

                previousEnd = dataStart + member.CompressedBytes;
                if (descriptor)
                    previousEnd = ValidateDescriptor(source, previousEnd, limit, member);
            }
        }

        private static bool MatchesLocalValue(uint local, uint central, bool descriptor)
            => local == central || (descriptor && local == 0);

        private static long ValidateDescriptor(Stream source, long offset, long limit, LocalMember member)
        {
            // The optional signature is not an unambiguous delimiter:
            // an unsigned descriptor may itself have that CRC value.
            // Accept precisely one matching ZIP32 interpretation.
            if (limit - offset < 12)
                throw new InvalidDataException("ZIP data descriptor is truncated.");
            source.Position = offset;
            var bytes = new byte[16];
            ReadExactly(source, bytes, 0, 12);
            var unsigned = DescriptorMatches(bytes, 0, member);
            var signed = false;
            if (UInt32(bytes, 0) == 0x08074B50 && limit - offset >= 16)
            {
                ReadExactly(source, bytes, 12, 4);
                signed = DescriptorMatches(bytes, 4, member);
            }
            if (unsigned == signed)
                throw new InvalidDataException("ZIP data descriptor is inconsistent or ambiguous.");
            return offset + (signed ? 16 : 12);
        }

        private static bool DescriptorMatches(byte[] bytes, int offset, LocalMember member)
            => UInt32(bytes, offset) == member.Crc32 &&
               UInt32(bytes, offset + 4) == member.CompressedBytes &&
               UInt32(bytes, offset + 8) == member.UncompressedBytes;

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
