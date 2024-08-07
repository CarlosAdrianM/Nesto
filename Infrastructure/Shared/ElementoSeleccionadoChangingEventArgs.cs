﻿using Nesto.Infrastructure.Contracts;
using System;

namespace Nesto.Infrastructure.Shared
{
    public class ElementoSeleccionadoChangingEventArgs : EventArgs
    {
        public IFiltrableItem OldValue { get; set; }
        public IFiltrableItem NewValue { get; set; }
    }
}
