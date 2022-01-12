using Nesto.Infrastructure.Contracts;

namespace Nesto.Infrastructure.Tests
{
    internal class MiClaseFiltrable : IFiltrableItem
    {
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public bool Contains(string filtro)
        {
            return Nombre.ToLower().Contains(filtro.ToLower());
        }
    }
}
