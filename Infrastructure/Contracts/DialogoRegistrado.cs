using System;
using System.Windows;

namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#490 (4C.2, paso 3, punto 3): un diálogo registrado con <c>ControlesUsuario.Dialogs.RegistroDialogos.RegistrarDialogo</c>:
    /// su nombre (con el que se abre en <see cref="IServicioDialogos"/>), su vista y su ViewModel.
    /// En 4C.5 pasa de ControlesUsuario a Infrastructure para que <see cref="IRegistroNesto"/> y <see cref="IResolutorNesto"/> lo usen.
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
}
