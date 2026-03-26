using System;
using System.Runtime.InteropServices;

namespace Nesto.Infrastructure.Shared
{
    public static class RdpClientInfo
    {
        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSQuerySessionInformation(
            IntPtr hServer,
            int sessionId,
            WTS_INFO_CLASS wtsInfoClass,
            out IntPtr ppBuffer,
            out int pBytesReturned);

        [DllImport("wtsapi32.dll")]
        private static extern void WTSFreeMemory(IntPtr pointer);

        private enum WTS_INFO_CLASS
        {
            WTSClientName = 10
        }

        private const int WTS_CURRENT_SESSION = -1;

        /// <summary>
        /// Obtiene el nombre real del equipo cliente RDP conectado actualmente.
        /// A diferencia de Environment.GetEnvironmentVariable("CLIENTNAME"),
        /// este método devuelve el valor actualizado tras una reconexión
        /// desde otro equipo.
        /// </summary>
        public static string GetCurrentClientName()
        {
            IntPtr buffer = IntPtr.Zero;
            try
            {
                bool success = WTSQuerySessionInformation(
                    IntPtr.Zero,
                    WTS_CURRENT_SESSION,
                    WTS_INFO_CLASS.WTSClientName,
                    out buffer,
                    out int bytesReturned);

                if (!success)
                {
                    return Environment.GetEnvironmentVariable("CLIENTNAME");
                }

                string clientName = Marshal.PtrToStringAuto(buffer);
                return string.IsNullOrEmpty(clientName)
                    ? Environment.GetEnvironmentVariable("CLIENTNAME")
                    : clientName;
            }
            catch
            {
                return Environment.GetEnvironmentVariable("CLIENTNAME");
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                {
                    WTSFreeMemory(buffer);
                }
            }
        }
    }
}
