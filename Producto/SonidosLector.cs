using System.Media;

namespace Nesto.Modules.Producto
{
    /// <summary>
    /// Lo que suena al leer con el lector en Enviar y Recibir reposición: uno de error, distinto, cuando lo leído no
    /// entra (no está en la reposición, lo comparten varios productos, ya está todo…) y uno corto cuando entra bien.
    /// Detrás de una interfaz para que los ViewModels se puedan probar sin altavoces.
    /// </summary>
    public interface ISonidosLector
    {
        void Error();
        void Correcto();
    }

    /// <summary>Con los sonidos del sistema: «Detención crítica» para el error y el de fondo, corto y suave, para el bien.</summary>
    public class SonidosLectorSistema : ISonidosLector
    {
        public void Error() => SystemSounds.Hand.Play();

        public void Correcto() => SystemSounds.Asterisk.Play();
    }
}
