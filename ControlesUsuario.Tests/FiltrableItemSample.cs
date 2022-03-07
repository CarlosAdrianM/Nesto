using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    internal class FiltrableItemSample : IFiltrableItem
    {
        public bool Contains(string filtro)
        {
            return true;
        }
    }
}
