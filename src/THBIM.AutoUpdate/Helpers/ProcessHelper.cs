using System.Diagnostics;

namespace THBIM.AutoUpdate.Helpers;

public static class ProcessHelper
{
    public static bool IsRevitRunning()
    {
        return Process.GetProcessesByName("Revit").Length > 0;
    }
}
