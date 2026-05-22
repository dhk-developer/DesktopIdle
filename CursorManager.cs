namespace DesktopIdle;

internal static class CursorManager
{
    public static void MoveToCurrentScreenCentre()
    {
        try
        {
            var current = Cursor.Position;
            var screen = Screen.FromPoint(current);
            var bounds = screen.Bounds;

            Cursor.Position = new Point(
                bounds.Left + bounds.Width / 2,
                bounds.Top + bounds.Height / 2);
        }
        catch
        {
            // Cursor movement is a comfort fix, not a critical path. 
        }
    }
}
