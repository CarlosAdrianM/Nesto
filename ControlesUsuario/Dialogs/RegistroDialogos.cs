using Nesto.Infrastructure.Contracts;
using Prism.Ioc;
using System;
using System.Windows;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#490 (4C.2, paso 3, punto 3): un diálogo registrado con <see cref="RegistroDialogos.RegistrarDialogo"/>:
    /// su nombre (con el que se abre en <see cref="IServicioDialogos"/>), su vista y su ViewModel.
    /// </summary>
    public sealed class DialogoRegistrado
    {
        public DialogoRegistrado(string nombre, Type tipoVista, Type tipoViewModel)
        {
            if (string.IsNullOrWhiteSpace(nombre))
            {
                throw new ArgumentException("El diálogo necesita un nombre", nameof(nombre));
            }
            if (tipoVista == null || !typeof(FrameworkElement).IsAssignableFrom(tipoVista))
            {
                throw new ArgumentException($"La vista del diálogo «{nombre}» tiene que ser un FrameworkElement", nameof(tipoVista));
            }
            if (tipoViewModel == null || !typeof(IDialogoNesto).IsAssignableFrom(tipoViewModel))
            {
                throw new ArgumentException($"El ViewModel del diálogo «{nombre}» tiene que implementar IDialogoNesto", nameof(tipoViewModel));
            }
            Nombre = nombre;
            TipoVista = tipoVista;
            TipoViewModel = tipoViewModel;
        }

        public string Nombre { get; }
        public Type TipoVista { get; }
        public Type TipoViewModel { get; }

        /// <summary>
        /// Una vista nueva con un ViewModel nuevo como DataContext, los dos sacados de <paramref name="resolver"/>
        /// (el contenedor). Es lo que hacía Prism con RegisterDialog + el AutoWire del ViewModelLocator, pero
        /// explícito: la vista ya no pide su ViewModel.
        /// </summary>
        public FrameworkElement Crear(Func<Type, object> resolver)
        {
            if (resolver == null)
            {
                throw new ArgumentNullException(nameof(resolver));
            }
            var vista = (FrameworkElement)resolver(TipoVista);
            vista.DataContext = resolver(TipoViewModel);
            return vista;
        }
    }

    /// <summary>
    /// Nesto#490 (4C.2, paso 3, punto 3): registro propio de los diálogos, en vez del <c>RegisterDialog</c> de
    /// Prism. Cada diálogo se registra en el contenedor como un <see cref="DialogoRegistrado"/> con su nombre;
    /// <see cref="ServicioDialogosNesto"/> lo busca por ese nombre y crea la vista con su ViewModel.
    /// Mismos nombres que con Prism: por defecto, el nombre de la clase de la vista.
    /// </summary>
    public static class RegistroDialogos
    {
        /// <summary>
        /// Registra el diálogo <typeparamref name="TVista"/> con su ViewModel <typeparamref name="TViewModel"/>.
        /// Sin <paramref name="nombre"/>, se abre con el nombre de la clase de la vista (como con Prism).
        /// </summary>
        public static void RegistrarDialogo<TVista, TViewModel>(this IContainerRegistry registro, string nombre = null)
            where TVista : FrameworkElement
            where TViewModel : IDialogoNesto
        {
            if (registro == null)
            {
                throw new ArgumentNullException(nameof(registro));
            }
            nombre = string.IsNullOrWhiteSpace(nombre) ? typeof(TVista).Name : nombre;
            _ = registro.RegisterInstance(typeof(DialogoRegistrado), new DialogoRegistrado(nombre, typeof(TVista), typeof(TViewModel)), nombre);
        }

        /// <summary>
        /// La vista del diálogo registrado con ese nombre, con su ViewModel ya puesto; null si no hay ninguno
        /// registrado con ese nombre.
        /// </summary>
        public static FrameworkElement CrearDialogo(IContainerProvider contenedor, string nombre)
        {
            if (contenedor == null)
            {
                throw new ArgumentNullException(nameof(contenedor));
            }
            if (string.IsNullOrWhiteSpace(nombre)
                || (contenedor is IContainerRegistry registro && !registro.IsRegistered(typeof(DialogoRegistrado), nombre)))
            {
                return null;
            }
            var dialogo = contenedor.Resolve(typeof(DialogoRegistrado), nombre) as DialogoRegistrado;
            return dialogo?.Crear(tipo => contenedor.Resolve(tipo));
        }
    }
}
