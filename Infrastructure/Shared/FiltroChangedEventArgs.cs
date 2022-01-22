using System;

namespace Nesto.Infrastructure.Shared
{
    public class FiltroChangedEventArgs : EventArgs
    {
        public string OldValue { get; set; }
        public string NewValue { get; set; }
    }
}
