using Nesto.Modulos.Cajas.Models;

namespace Nesto.Modulos.Cajas.Interfaces
{
    internal interface IConvertidorFormatoBancario
    {
        bool PuedeConvertir(string contenido);
        ContenidoCuaderno43 Convertir(string contenido);
    }
}
