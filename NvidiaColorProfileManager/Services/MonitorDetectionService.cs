using NvidiaColorProfileManager.Models;

namespace NvidiaColorProfileManager.Services;

/// <summary>
/// Servicio de detección de monitores NVIDIA.
/// </summary>
public class MonitorDetectionService : IMonitorDetectionService
{
    private readonly INvapiService _nvapiService;

    public MonitorDetectionService(INvapiService nvapiService)
    {
        _nvapiService = nvapiService;
    }

    public bool HasDisplays => _nvapiService.IsAvailable;

    public List<DisplayInfo> DetectDisplays()
    {
        return _nvapiService.GetDisplays();
    }
}
