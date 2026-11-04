using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Nesto#618 (NestoAPI#786): botón «Nueva nota de cobro…» de la ficha del cliente. Se pone en XAML con el cliente enlazado
    /// (<c>Empresa</c>, <c>Cliente</c>, <c>Contacto</c>, <c>Nombre</c>) y abre el diálogo <see cref="NotaCobroDialogViewModel"/> con
    /// ellos propuestos. Solo lo ven los grupos que pueden crear notas (los mismos que «Nueva nota de recogida»).
    /// </summary>
    public partial class NuevaNotaCobroBoton : UserControl
    {
        public static readonly DependencyProperty EmpresaProperty = DependencyProperty.Register(nameof(Empresa), typeof(string),
            typeof(NuevaNotaCobroBoton), new PropertyMetadata(null));
        public static readonly DependencyProperty ClienteProperty = DependencyProperty.Register(nameof(Cliente), typeof(string),
            typeof(NuevaNotaCobroBoton), new PropertyMetadata(null, AlCambiarCliente));
        public static readonly DependencyProperty ContactoProperty = DependencyProperty.Register(nameof(Contacto), typeof(string),
            typeof(NuevaNotaCobroBoton), new PropertyMetadata(null));
        public static readonly DependencyProperty NombreProperty = DependencyProperty.Register(nameof(Nombre), typeof(string),
            typeof(NuevaNotaCobroBoton), new PropertyMetadata(null));

        private readonly IServicioDialogos _dialogos;

        public NuevaNotaCobroBoton()
        {
            InitializeComponent();
            IResolutorNesto contenedor = Contenedor.Actual;
            bool puede = false;
            if (contenedor != null)
            {
                _dialogos = contenedor.Resolver<IServicioDialogos>();
                puede = PermisosNotasEntrega.Puede(contenedor.Resolver<IConfiguracion>());
            }
            Visibility = puede && _dialogos != null ? Visibility.Visible : Visibility.Collapsed;
            Boton.IsEnabled = false;
        }

        public string Empresa
        {
            get => (string)GetValue(EmpresaProperty);
            set => SetValue(EmpresaProperty, value);
        }

        public string Cliente
        {
            get => (string)GetValue(ClienteProperty);
            set => SetValue(ClienteProperty, value);
        }

        public string Contacto
        {
            get => (string)GetValue(ContactoProperty);
            set => SetValue(ContactoProperty, value);
        }

        public string Nombre
        {
            get => (string)GetValue(NombreProperty);
            set => SetValue(NombreProperty, value);
        }

        private static void AlCambiarCliente(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var boton = (NuevaNotaCobroBoton)d;
            boton.Boton.IsEnabled = !string.IsNullOrWhiteSpace(e.NewValue as string);
        }

        private async void Boton_Click(object sender, RoutedEventArgs e)
        {
            if (_dialogos == null || string.IsNullOrWhiteSpace(Cliente))
            {
                return;
            }
            try
            {
                _ = await _dialogos.ShowDialogAsync(NotaCobroDialogViewModel.NOMBRE, new ParametrosDialogo
                {
                    { "empresa", Empresa?.Trim() },
                    { "cliente", Cliente?.Trim() },
                    { "contacto", Contacto?.Trim() },
                    { "nombre", Nombre?.Trim() }
                });
            }
            catch (Exception ex)
            {
                _dialogos.ShowError($"No se ha podido abrir la nota de cobro: {ex.Message}");
            }
        }
    }
}
