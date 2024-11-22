namespace MVVM.Generator.Utilities;

internal static class GenDebugger
{
    private static bool launchedRequest = false;
    public static bool LaunchRequested
    {
        get
        {
            if (launchedRequest) return true;

            launchedRequest = true;
            return false;
        }
    }
}
