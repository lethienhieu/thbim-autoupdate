using System.Management;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace THBIM.AutoUpdate.Helpers;

[SupportedOSPlatform("windows")]
public static class MachineIdHelper
{
    private static string? _cached;

    public static string Get()
    {
        if (!string.IsNullOrEmpty(_cached)) return _cached;

        try
        {
            string? uuid = GetWmiOne("Win32_ComputerSystemProduct", "UUID");
            string? board = GetWmiOne("Win32_BaseBoard", "SerialNumber");
            string? bios = GetWmiOne("Win32_BIOS", "SerialNumber");
            string disks = string.Join("|", GetWmiMany("Win32_DiskDrive", "SerialNumber"));
            string media = string.Join("|", GetWmiMany("Win32_PhysicalMedia", "SerialNumber"));
            string? volC = GetWmiOne("Win32_LogicalDisk", "VolumeSerialNumber", "DeviceID='C:'");
            string? machineGuid = ReadReg(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid");
            string host = Environment.MachineName;

            var raw = string.Join(";", new[] { uuid, board, bios, disks, media, volC, machineGuid, host }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            _cached = BitConverter.ToString(bytes).Replace("-", "")[..16];
            return _cached;
        }
        catch
        {
            var s = Environment.MachineName ?? "UNKNOWN";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
            _cached = BitConverter.ToString(bytes).Replace("-", "")[..16];
            return _cached;
        }
    }

    private static string? GetWmiOne(string cls, string prop, string? where = null)
    {
        try
        {
            var q = string.IsNullOrWhiteSpace(where) ? $"SELECT {prop} FROM {cls}" : $"SELECT {prop} FROM {cls} WHERE {where}";
            using var s = new ManagementObjectSearcher(q);
            foreach (var o in s.Get())
            {
                var v = o.Properties[prop]?.Value?.ToString();
                if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
            }
        }
        catch { }
        return null;
    }

    private static string[] GetWmiMany(string cls, string prop)
    {
        try
        {
            using var s = new ManagementObjectSearcher($"SELECT {prop} FROM {cls}");
            return s.Get()
                .Cast<ManagementBaseObject>()
                .Select(o => o.Properties[prop]?.Value?.ToString())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim())
                .ToArray();
        }
        catch { return []; }
    }

    private static string? ReadReg(string path, string name)
    {
        try { return Registry.GetValue(path, name, null)?.ToString(); }
        catch { return null; }
    }
}
