using System;
using System.Threading.Tasks;
namespace Nesto.Infrastructure.Contracts
{
    public interface IConfiguracion
    {
        // Dirección IP del servidor con NestoAPI
        Task<string> leerParametro(string empresa, string clave);
        string servidorAPI { get; }
        string usuario { get; }
        bool UsuarioEnGrupo(string grupo);
        string LeerParametroSync(string empresa, string clave);
        Task GuardarParametro(string empresa, string clave, string valor);
        void GuardarParametroSync(string empresa, string clave, string valor);
    }
}
