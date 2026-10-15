using Nesto.Infrastructure.Contracts;
using System;
using System.Windows;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#490 (4C.2, paso 3, punto 3): registro propio de los diálogos, en vez del <c>RegisterDialog</c> de
    /// Prism. Cada diálogo se registra en el contenedor como un <see cref="DialogoRegistrado"/> con su nombre;
    /// <see cref="ServicioDialogosNesto"/> lo busca por ese nombre y crea la vista con su ViewModel.
    /// Mismos nombres que con Prism: por defecto, el nombre de la clase de la vista.
    /// 4C.5: sobre <see cref="IRegistroNesto"/> e <see cref="IResolutorNesto"/>, sin tipos de Prism.
    /// </summary>
    public static class RegistroDialogos
    {
        /// <summary>
        /// Registra el diálogo <typeparamref name="TVista"/> con su ViewModel <typeparamref name="TViewModel"/>.
        /// Sin <paramref name="nombre"/>, se abre con el nombre de la clase de la vista (como con Prism).
        /// </summary>
        public static void RegistrarDialogo<TVista, TViewModel>(this IRegistroNesto registro, string nombre = null)
            where TVista : FrameworkElement
            where TViewModel : IDialogoNesto
        {
            if (registro == null)
            {
                throw new ArgumentNullException(nameof(registro));
            }
            nombre = string.IsNullOrWhiteSpace(nombre) ? typeof(TVista).Name : nombre;
            registro.Dialogo(nombre, new DialogoRegistrado(nombre, typeof(TVista), typeof(TViewModel)));
        }

        /// <summary>
        /// La vista del diálogo registrado con ese nombre, con su ViewModel ya puesto; null si no hay ninguno
        /// registrado con ese nombre.
        /// </summary>
        public static FrameworkElement CrearDialogo(IResolutorNesto contenedor, string nombre)
        {
            if (contenedor == null)
            {
                throw new ArgumentNullException(nameof(contenedor));
            }
            if (string.IsNullOrWhiteSpace(nombre))
            {
                return null;
            }
            return contenedor.Dialogo(nombre)?.Crear(contenedor.Resolver);
        }
    }
}
