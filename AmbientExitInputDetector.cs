namespace DesktopIdle;

internal static class AmbientExitInputDetector
{

    private const int MouseExitThresholdPixels = 100;
    private const int MouseExitThresholdSquared = MouseExitThresholdPixels * MouseExitThresholdPixels;

    public static void ClearKeyStateLatches()
    {

        foreach (var vk in ExitVirtualKeys)
        {
            _ = Win32.GetAsyncKeyState(vk);
        }
    }

    public static bool ShouldExitAmbientMode(Point cursorAnchor)
    {
        return HasMeaningfulMouseMovement(cursorAnchor) || HasDeliberateKeyOrMouseButtonInput();
    }

    private static bool HasMeaningfulMouseMovement(Point cursorAnchor)
    {
        if (cursorAnchor == Point.Empty) return false;

        var current = Cursor.Position;
        var dx = current.X - cursorAnchor.X;
        var dy = current.Y - cursorAnchor.Y;
        return (dx * dx) + (dy * dy) >= MouseExitThresholdSquared;
    }

    private static bool HasDeliberateKeyOrMouseButtonInput()
    {
        foreach (var vk in ExitVirtualKeys)
        {
            var state = Win32.GetAsyncKeyState(vk);


            if ((state & unchecked((short)0x8000)) != 0 || (state & 0x0001) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static readonly int[] ExitVirtualKeys = BuildExitVirtualKeys();

    private static int[] BuildExitVirtualKeys()
    {
        var keys = new List<int>();


        for (var vk = 0x01; vk <= 0x06; vk++) keys.Add(vk);


        for (var vk = 0x08; vk <= 0xFE; vk++) keys.Add(vk);

        return keys.ToArray();
    }
}
