namespace EasyLogiWheelSupport
{
    public partial class Plugin
    {
        internal static void LogDebug(string message)
        {
            if (_debugLogging == null || !_debugLogging.Value || _log == null)
            {
                return;
            }

            _log.LogInfo("[debug] " + message);
        }
    }
}
