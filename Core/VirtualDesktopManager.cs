using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Switcheroo.Core
{
    public class VirtualDesktopManager
    {
        private static readonly Guid VirtualDesktopManagerClsid = new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A");
        private const string VirtualDesktopsRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops";

        private readonly IVirtualDesktopManager _virtualDesktopManager;

        public VirtualDesktopManager()
        {
            _virtualDesktopManager = CreateVirtualDesktopManager();
        }

        public IReadOnlyList<VirtualDesktop> GetDesktops()
        {
            var desktops = GetDesktopsFromRegistry();
            return desktops.Count > 0 ? desktops : new List<VirtualDesktop> { new VirtualDesktop(Guid.Empty, "Desktop 1") };
        }

        public Guid? GetWindowDesktopId(IntPtr hWnd)
        {
            if (_virtualDesktopManager == null || hWnd == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                Guid desktopId;
                _virtualDesktopManager.GetWindowDesktopId(hWnd, out desktopId);
                return desktopId;
            }
            catch (COMException)
            {
                return null;
            }
        }

        private static IVirtualDesktopManager CreateVirtualDesktopManager()
        {
            try
            {
                var type = Type.GetTypeFromCLSID(VirtualDesktopManagerClsid);
                if (type == null)
                {
                    return null;
                }

                return (IVirtualDesktopManager)Activator.CreateInstance(type);
            }
            catch (COMException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static List<VirtualDesktop> GetDesktopsFromRegistry()
        {
            var desktops = new List<VirtualDesktop>();

            using (var key = Registry.CurrentUser.OpenSubKey(VirtualDesktopsRegistryPath, writable: false))
            {
                if (key == null)
                {
                    return desktops;
                }

                var virtualDesktopIds = key.GetValue("VirtualDesktopIDs") as byte[];
                if (virtualDesktopIds == null)
                {
                    return desktops;
                }

                const int guidSize = 16;
                var offset = 0;
                while (offset + guidSize <= virtualDesktopIds.Length)
                {
                    var desktopIdBytes = new byte[guidSize];
                    Array.Copy(virtualDesktopIds, offset, desktopIdBytes, 0, guidSize);
                    var desktopId = new Guid(desktopIdBytes);
                    desktops.Add(new VirtualDesktop(desktopId, GetDesktopName(key, desktopId, desktops.Count + 1)));
                    offset += guidSize;
                }
            }

            return desktops;
        }

        private static string GetDesktopName(RegistryKey virtualDesktopsKey, Guid desktopId, int fallbackIndex)
        {
            using (var desktopKey = virtualDesktopsKey.OpenSubKey(@"Desktops\" + desktopId.ToString("B"), writable: false))
            {
                var name = desktopKey?.GetValue("Name") as string;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }

            return "Desktop " + fallbackIndex;
        }
    }

    public class VirtualDesktop
    {
        public VirtualDesktop(Guid id, string name)
        {
            Id = id;
            Name = name;
        }

        public Guid Id { get; private set; }

        public string Name { get; private set; }
    }

    [ComImport]
    [Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVirtualDesktopManager
    {
        void IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, [MarshalAs(UnmanagedType.Bool)] out bool isCurrentDesktop);

        void GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);

        void MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
    }
}
