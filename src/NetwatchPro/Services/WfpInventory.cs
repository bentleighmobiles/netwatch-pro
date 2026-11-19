using System.Runtime.InteropServices;
using NetwatchPro.Models;

namespace NetwatchPro.Services;

/// <summary>
/// Real Windows Filtering Platform inventory via the fwpuclnt management API.
/// Enumerates providers, sublayers, callouts and layers. Requires elevation for
/// most operations; callers must degrade gracefully when FwpmEngineOpen0 fails.
/// </summary>
public static class WfpInventory
{
    public sealed class Result
    {
        public List<WfpObject> Objects { get; } = new();
        public List<WfpObject> Layers { get; } = new();
        public bool Ok { get; set; }
        public string Error { get; set; } = "";
    }

    public static Result Collect()
    {
        var r = new Result();
        IntPtr engine;
        uint ret = Native.FwpmEngineOpen0(IntPtr.Zero, 10 /* RPC_C_AUTHN_WINNT */, IntPtr.Zero, IntPtr.Zero, out engine);
        if (ret != 0 || engine == IntPtr.Zero)
        {
            r.Error = ret == 5 /* ERROR_ACCESS_DENIED */
                ? "access denied — run NETWATCH PRO elevated to enumerate WFP"
                : $"FwpmEngineOpen0 failed (0x{ret:X8}) — elevation required or BFE service stopped";
            return r;
        }

        try
        {
            var (providerObjects, byKey) = EnumProviders(engine);
            foreach (var o in providerObjects) r.Objects.Add(o);

            foreach (var s in EnumSubLayers(engine, byKey)) r.Objects.Add(s);
            foreach (var c in EnumCallouts(engine, byKey)) r.Objects.Add(c);

            var layers = EnumLayers(engine, byKey);
            foreach (var l in layers) r.Objects.Add(l);
            r.Layers.AddRange(layers);
            r.Ok = true;
        }
        catch (Exception ex)
        {
            r.Error = ex.Message;
        }
        finally
        {
            Native.FwpmEngineClose0(engine);
        }
        return r;
    }

    private static (List<WfpObject> Objects, Dictionary<Guid, string> Names) EnumProviders(IntPtr engine)
    {
        var objects = new List<WfpObject>();
        var names = new Dictionary<Guid, string>();
        IntPtr entries; uint count;
        if (Native.FwpmProviderEnum0(engine, IntPtr.Zero, out entries, out count) != 0 || count == 0) return (objects, names);
        try
        {
            for (uint i = 0; i < count; i++)
            {
                var p = Marshal.PtrToStructure<Native.FwpmProvider0>(Marshal.ReadIntPtr(entries, (int)i * IntPtr.Size));
                string name = p.DisplayData.Name ?? "—";
                names[p.ProviderKey] = name;
                objects.Add(new WfpObject
                {
                    Kind = WfpObjectKind.Provider,
                    Id = Short(p.ProviderKey),
                    Name = name,
                    Provider = name,
                    Sublayer = "—",
                    Layer = "—",
                    Action = "—",
                    Callout = "—",
                    Confidence = ConfidenceLevel.Confirmed,
                });
            }
        }
        finally { Native.FwpmFreeMemory0(ref entries); }
        return (objects, names);
    }

    private static List<WfpObject> EnumSubLayers(IntPtr engine, IReadOnlyDictionary<Guid, string> providerNames)
    {
        var list = new List<WfpObject>();
        IntPtr entries; uint count;
        if (Native.FwpmSubLayerEnum0(engine, IntPtr.Zero, out entries, out count) != 0 || count == 0) return list;
        try
        {
            for (uint i = 0; i < count; i++)
            {
                var s = Marshal.PtrToStructure<Native.FwpmSubLayer0>(Marshal.ReadIntPtr(entries, (int)i * IntPtr.Size));
                var provider = s.ProviderKey != IntPtr.Zero ? Marshal.PtrToStructure<Guid>(s.ProviderKey) : Guid.Empty;
                list.Add(new WfpObject
                {
                    Kind = WfpObjectKind.Sublayer,
                    Id = Short(s.SubLayerKey),
                    Name = s.DisplayData.Name ?? "—",
                    Provider = providerNames.TryGetValue(provider, out var pn) ? pn : (provider == Guid.Empty ? "—" : Short(provider)),
                    Sublayer = s.DisplayData.Name ?? "—",
                    Layer = "—",
                    Action = "—",
                    Callout = "—",
                    Confidence = ConfidenceLevel.Confirmed,
                });
            }
        }
        finally { Native.FwpmFreeMemory0(ref entries); }
        return list;
    }

    private static List<WfpObject> EnumCallouts(IntPtr engine, IReadOnlyDictionary<Guid, string> providerNames)
    {
        var list = new List<WfpObject>();
        IntPtr entries; uint count;
        if (Native.FwpmCalloutEnum0(engine, IntPtr.Zero, out entries, out count) != 0 || count == 0) return list;
        try
        {
            for (uint i = 0; i < count; i++)
            {
                var c = Marshal.PtrToStructure<Native.FwpmCallout0>(Marshal.ReadIntPtr(entries, (int)i * IntPtr.Size));
                var provider = c.ProviderKey != IntPtr.Zero ? Marshal.PtrToStructure<Guid>(c.ProviderKey) : Guid.Empty;
                list.Add(new WfpObject
                {
                    Kind = WfpObjectKind.Callout,
                    Id = Short(c.CalloutKey),
                    Name = c.DisplayData.Name ?? "—",
                    Provider = providerNames.TryGetValue(provider, out var pn) ? pn : (provider == Guid.Empty ? "—" : Short(provider)),
                    Sublayer = "—",
                    Layer = Short(c.ApplicableLayer),
                    Action = "Callout",
                    Callout = c.DisplayData.Name ?? "—",
                    Confidence = ConfidenceLevel.Confirmed,
                });
            }
        }
        finally { Native.FwpmFreeMemory0(ref entries); }
        return list;
    }

    private static List<WfpObject> EnumLayers(IntPtr engine, IReadOnlyDictionary<Guid, string> _)
    {
        var list = new List<WfpObject>();
        IntPtr entries; uint count;
        if (Native.FwpmLayerEnum0(engine, IntPtr.Zero, out entries, out count) != 0 || count == 0) return list;
        try
        {
            for (uint i = 0; i < count; i++)
            {
                var l = Marshal.PtrToStructure<Native.FwpmLayer0>(Marshal.ReadIntPtr(entries, (int)i * IntPtr.Size));
                list.Add(new WfpObject
                {
                    Kind = WfpObjectKind.Layer,
                    Id = Short(l.LayerKey),
                    Name = l.DisplayData.Name ?? "—",
                    Provider = "—",
                    Sublayer = "—",
                    Layer = l.DisplayData.Name ?? "—",
                    Action = "—",
                    Callout = "—",
                    Confidence = ConfidenceLevel.Confirmed,
                });
            }
        }
        finally { Native.FwpmFreeMemory0(ref entries); }
        return list;
    }

    private static string Short(Guid g) => g.ToString("N")[..8];

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct FwpmDisplayData0
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string? Name;
            [MarshalAs(UnmanagedType.LPWStr)] public string? Description;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct FwpByteBlob
        {
            public uint Size;
            public IntPtr Data;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct FwpmProvider0
        {
            public Guid ProviderKey;
            public FwpmDisplayData0 DisplayData;
            public uint Flags;
            public FwpByteBlob ProviderData;
            public IntPtr ServiceName; // wchar_t*
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct FwpmSubLayer0
        {
            public Guid SubLayerKey;
            public FwpmDisplayData0 DisplayData;
            public uint Flags;
            public IntPtr ProviderKey; // GUID*
            public FwpByteBlob ProviderData;
            public ushort Weight;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct FwpmCallout0
        {
            public Guid CalloutKey;
            public FwpmDisplayData0 DisplayData;
            public uint Flags;
            public IntPtr ProviderKey; // GUID*
            public FwpByteBlob ProviderData;
            public Guid ApplicableLayer;
            public uint CalloutId;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct FwpmLayer0
        {
            public Guid LayerKey;
            public FwpmDisplayData0 DisplayData;
            public uint Flags;
            public uint NumFields;
            public IntPtr Field; // FWPM_FIELD0*
            public Guid DefaultSubLayerKey;
            public ushort LayerId;
        }

        [DllImport("fwpuclnt.dll")]
        internal static extern uint FwpmEngineOpen0(IntPtr serverName, uint authnService, IntPtr authIdentity, IntPtr session, out IntPtr engineHandle);

        [DllImport("fwpuclnt.dll")]
        internal static extern uint FwpmEngineClose0(IntPtr engineHandle);

        [DllImport("fwpuclnt.dll")]
        internal static extern uint FwpmProviderEnum0(IntPtr engine, IntPtr enumTemplate, out IntPtr entries, out uint numEntries);

        [DllImport("fwpuclnt.dll")]
        internal static extern uint FwpmSubLayerEnum0(IntPtr engine, IntPtr enumTemplate, out IntPtr entries, out uint numEntries);

        [DllImport("fwpuclnt.dll")]
        internal static extern uint FwpmCalloutEnum0(IntPtr engine, IntPtr enumTemplate, out IntPtr entries, out uint numEntries);

        [DllImport("fwpuclnt.dll")]
        internal static extern uint FwpmLayerEnum0(IntPtr engine, IntPtr enumTemplate, out IntPtr entries, out uint numEntries);

        [DllImport("fwpuclnt.dll")]
        internal static extern void FwpmFreeMemory0(ref IntPtr p);
    }
}
