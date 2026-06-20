using System.Text.Json;
using XBVault.Services;

namespace XBVault.Models;

public class PerformanceSnapshot
{
    public double CpuLoad { get; set; }
    public double GpuUsage { get; set; }
    public long AvailablePages { get; set; }
    public long TotalPages { get; set; }
    public long CommittedPages { get; set; }
    public long PageSize { get; set; }
    public long IOReadSpeed { get; set; }
    public long IOWriteSpeed { get; set; }
    public long IOOtherSpeed { get; set; }
    public long NetworkInBytes { get; set; }
    public long NetworkOutBytes { get; set; }
    public long DedicatedMemory { get; set; }
    public long DedicatedMemoryUsed { get; set; }
    public long SystemMemory { get; set; }
    public long SystemMemoryUsed { get; set; }

    public double MemoryPercent =>
        TotalPages > 0 ? (double)(TotalPages - AvailablePages) / TotalPages * 100 : 0;

    public long MemoryUsedBytes => (TotalPages - AvailablePages) * PageSize;
    public long MemoryTotalBytes => TotalPages * PageSize;
    public long MemoryCommittedBytes => CommittedPages * PageSize;

    public double MemoryUsedMB => MemoryUsedBytes / 1024.0 / 1024.0;
    public double MemoryTotalMB => MemoryTotalBytes / 1024.0 / 1024.0;

    public double IoTotalSpeed => IOReadSpeed + IOWriteSpeed + IOOtherSpeed;

    public static PerformanceSnapshot? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var snap = new PerformanceSnapshot
            {
                CpuLoad = root.TryGetProperty("CpuLoad", out var cpu) ? cpu.GetDouble() : 0,
                AvailablePages = root.TryGetProperty("AvailablePages", out var ap) ? ap.GetInt64() : 0,
                TotalPages = root.TryGetProperty("TotalPages", out var tp) ? tp.GetInt64() : 0,
                CommittedPages = root.TryGetProperty("CommittedPages", out var cp) ? cp.GetInt64() : 0,
                PageSize = root.TryGetProperty("PageSize", out var ps) ? ps.GetInt64() : 4096,
                IOReadSpeed = root.TryGetProperty("IOReadSpeed", out var rs) ? rs.GetInt64() : 0,
                IOWriteSpeed = root.TryGetProperty("IOWriteSpeed", out var ws) ? ws.GetInt64() : 0,
                IOOtherSpeed = root.TryGetProperty("IOOtherSpeed", out var os) ? os.GetInt64() : 0,
            };

            if (root.TryGetProperty("GPUData", out var gpu) &&
                gpu.TryGetProperty("AvailableAdapters", out var adapters) &&
                adapters.GetArrayLength() > 0)
            {
                var adapter = adapters[0];
                snap.DedicatedMemory = adapter.TryGetProperty("DedicatedMemory", out var dm) ? dm.GetInt64() : 0;
                snap.DedicatedMemoryUsed = adapter.TryGetProperty("DedicatedMemoryUsed", out var dmu) ? dmu.GetInt64() : 0;
                snap.SystemMemory = adapter.TryGetProperty("SystemMemory", out var sm) ? sm.GetInt64() : 0;
                snap.SystemMemoryUsed = adapter.TryGetProperty("SystemMemoryUsed", out var smu) ? smu.GetInt64() : 0;
                if (adapter.TryGetProperty("EnginesUtilization", out var engines) &&
                    engines.GetArrayLength() > 0)
                {
                    snap.GpuUsage = engines[0].GetDouble();
                }
            }

            if (root.TryGetProperty("NetworkingData", out var net))
            {
                snap.NetworkInBytes = net.TryGetProperty("NetworkInBytes", out var ni) ? ni.GetInt64() : 0;
                snap.NetworkOutBytes = net.TryGetProperty("NetworkOutBytes", out var no) ? no.GetInt64() : 0;
            }

            return snap;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to parse PerformanceSnapshot");
            return null;
        }
    }
}
