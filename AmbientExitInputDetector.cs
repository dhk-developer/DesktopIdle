namespace DesktopIdle;

internal static class AmbientExitInputDetector
{
    // Sensible fixed threshold: small desk/mouse-sensor drift is ignored, but a deliberate
    // mouse move still exits ambient mode quickly. This is deliberately not exposed in settings.
    private const int MouseExitThresholdPixels = 100;
    private const int MouseExitThresholdSquared = MouseExitThresholdPixels * MouseExitThresholdPixels;

    public static void ClearKeyStateLatches()
    {
        // Drain the "pressed since last call" latch so a key/mouse click from before
        // ambient mode does not immediately count as an exit request afterwards.
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

            // High bit: key/button is currently down.
            // Low bit: key/button was pressed since the last GetAsyncKeyState call.
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

        // Mouse buttons: left, right, cancel, middle, X1 and X2.
        for (var vk = 0x01; vk <= 0x06; vk++) keys.Add(vk);

        // Keyboard and common extended keys.
        for (var vk = 0x08; vk <= 0xFE; vk++) keys.Add(vk);

        return keys.ToArray();
    }
}
