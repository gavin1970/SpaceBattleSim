using System.Runtime.InteropServices;

[Flags]
public enum EXECUTION_STATE : uint
{
    ES_CONTINUOUS = 0x80000000,
    ES_SYSTEM_REQUIRED = 0x00000001,
    ES_DISPLAY_REQUIRED = 0x00000002
}

public static class  Externs
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

    public static bool PreventAutoLock()
    {
        return SetThreadExecutionState(
            EXECUTION_STATE.ES_CONTINUOUS |
            EXECUTION_STATE.ES_SYSTEM_REQUIRED |
            EXECUTION_STATE.ES_DISPLAY_REQUIRED
        ) != 0;
    }

    public static bool RestoreAutoLock() => SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS) != 0;
}
