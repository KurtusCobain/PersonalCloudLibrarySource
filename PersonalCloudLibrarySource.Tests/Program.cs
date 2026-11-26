using NUnitLite;
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PersonalCloudLibrarySource.Tests
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            // Test executable only: a child probe inspects the supplied handle's
            // identity, never opens a path or reads the referenced file.
            if (args.Length > 0 && args[0] == InheritanceFixture.ProbeArgument)
                return InheritanceFixture.Probe(args);
            return new AutoRun().Execute(args);
        }
    }

    internal static class InheritanceFixture
    {
        internal const string ProbeArgument = "--pcls-test-inheritance-probe";
        internal const int Match = 3;
        internal const int NoMatch = 0;
        internal const int InvalidArguments = 4;

        [StructLayout(LayoutKind.Sequential)]
        internal struct HandleInformation
        {
            public uint Attributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            public uint VolumeSerial;
            public uint SizeHigh;
            public uint SizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetHandleInformation(SafeFileHandle handle, uint mask, uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetHandleInformation(SafeFileHandle handle, out uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetFileInformationByHandle(IntPtr handle, out HandleInformation information);

        internal static int Probe(string[] args)
        {
            long handle;
            uint volume, high, low;
            if (args.Length != 5 ||
                !long.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out handle) ||
                !uint.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out volume) ||
                !uint.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out high) ||
                !uint.TryParse(args[4], NumberStyles.None, CultureInfo.InvariantCulture, out low) ||
                handle <= 0 || (IntPtr.Size == 4 && handle > int.MaxValue))
                return InvalidArguments;
            HandleInformation observed;
            // A recycled numeric handle is not evidence of inheritance:
            // compare the disposable fixture's actual file identity.
            return GetFileInformationByHandle(new IntPtr(handle), out observed) &&
                observed.VolumeSerial == volume && observed.FileIndexHigh == high &&
                observed.FileIndexLow == low ? Match : NoMatch;
        }
    }
}
