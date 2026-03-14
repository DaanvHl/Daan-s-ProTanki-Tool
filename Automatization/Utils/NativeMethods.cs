using System.Runtime.InteropServices;

namespace Automatization.Utils
{
    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern uint MapVirtualKey(uint uCode, uint uMapType);
    }
}