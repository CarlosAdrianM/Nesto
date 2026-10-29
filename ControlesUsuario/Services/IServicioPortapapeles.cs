namespace ControlesUsuario.Services
{
    /// <summary>
    /// Nesto (29/10/26): el portapapeles de Windows detrás de una interfaz, para que los ViewModels copien (p. ej. el enlace de
    /// pago de una inscripción) sin tocar tipos de WPF y se puedan probar.
    /// </summary>
    public interface IServicioPortapapeles
    {
        /// <summary>Copia el texto. Devuelve false si Windows no deja (el portapapeles lo tiene otro programa); nunca lanza.</summary>
        bool Copiar(string texto);
    }
}
