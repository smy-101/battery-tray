using System.Runtime.InteropServices;
using BatteryTray.Core;
using Microsoft.Win32;

namespace BatteryTray.Scanning;

/// <summary>
/// HFP 耳机电量读取:定位 BTHENUM 注册表里的 HFP 实例,再经 CfgMgr32
/// 读 Windows 蓝牙栈缓存的私有属性 {104EA319-…} pid 2。
/// 同步方法(注册表+驱动 IO),调用方放线程池。精度 10%(HFP 协议限制)。
/// </summary>
internal static class HfpBatteryReader
{
    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Locate_DevNodeW(out int dnDevInst, string deviceId, int flags);

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_DevNode_PropertyW(
        int dnDevInst, ref DEVPROPKEY propertyKey, out int propertyType,
        IntPtr propertyBuffer, ref int propertyBufferSize, int flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVPROPKEY
    {
        public Guid Fmtid;
        public int Pid;
    }

    private static readonly DEVPROPKEY HfpBatteryKey = new()
    {
        Fmtid = new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"),
        Pid = 2,
    };

    private const int CR_SUCCESS = 0;
    private const int CR_BUFFER_SMALL = 0x1A;
    private const int DEVPROP_TYPE_BYTE = 0x3;
    private const int DEVPROP_TYPE_INT32 = 0x6;
    private const int DEVPROP_TYPE_UINT32 = 0x7;

    /// <summary>bluetoothAddressHex:12 位大写十六进制(如 "0025764F8E3F")。读不到返回 null。</summary>
    public static byte? Read(string bluetoothAddressHex)
    {
        try
        {
            using var bthenum = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\BTHENUM");
            if (bthenum is null) return null;
            foreach (var serviceKeyName in bthenum.GetSubKeyNames())
            {
                if (!HfpRules.IsHfpServiceKey(serviceKeyName)) continue;
                using var serviceKey = bthenum.OpenSubKey(serviceKeyName);
                if (serviceKey is null) continue;
                foreach (var instanceName in serviceKey.GetSubKeyNames())
                {
                    if (!HfpRules.IsAddressMatch(instanceName, bluetoothAddressHex)) continue;
                    var level = ReadDevNodeBattery(HfpRules.InstanceId(serviceKeyName, instanceName));
                    if (level is not null) return level;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn("HFP 读取异常 addr=" + bluetoothAddressHex, ex);
        }
        return null;
    }

    private static byte? ReadDevNodeBattery(string instanceId)
    {
        if (CM_Locate_DevNodeW(out var node, instanceId, 0) != CR_SUCCESS)
            return null;

        int size = 0;
        var key = HfpBatteryKey;
        int result = CM_Get_DevNode_PropertyW(node, ref key, out _, IntPtr.Zero, ref size, 0);
        if (result != CR_BUFFER_SMALL && result != CR_SUCCESS) return null;
        if (size <= 0) return null;

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (CM_Get_DevNode_PropertyW(node, ref key, out var type, buffer, ref size, 0) != CR_SUCCESS)
                return null;

            if (type == DEVPROP_TYPE_BYTE && size >= 1)
                return Marshal.ReadByte(buffer);
            if ((type == DEVPROP_TYPE_INT32 || type == DEVPROP_TYPE_UINT32) && size >= 4)
            {
                int value = Marshal.ReadInt32(buffer);
                if (value is >= 0 and <= 100) return (byte)value;
            }
            // 未知类型:单字节且 ≤100 时兜底采纳
            if (size >= 1 && Marshal.ReadByte(buffer) is <= 100 and var raw) return raw;
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
