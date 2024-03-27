using ControlesUsuario.Models;
using ControlesUsuario.ViewModels;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using static ControlesUsuario.Models.SelectorProveedorModel;

namespace ControlesUsuario
{
    /// <summary>
    /// Lógica de interacción para SelectorProveedor.xaml
    /// </summary>
    public partial class SelectorProveedor : UserControl
    {
        private SelectorProveedorViewModel _vm;
        private DispatcherTimer _timer;
        public SelectorProveedor()
        {
            InitializeComponent();

            PrepararSelector();
        }

        #region Propiedades

        #endregion

        #region Dependency Properties

        /// <summary>
        /// Gets or sets the CONTACTO para las llamadas a la API
        /// </summary>
        public string Contacto
        {
            get { return (string)GetValue(ContactoProperty); }
            set
            {
                SetValue(ContactoProperty, value);
            }
        }

        /// <summary>
        /// Identified the CONTACTO dependency property
        /// </summary>
        public static readonly DependencyProperty ContactoProperty =
            DependencyProperty.Register(nameof(Contacto), typeof(string),
              typeof(SelectorProveedor),
              new FrameworkPropertyMetadata(null,
                  FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                  new PropertyChangedCallback(OnContactoChanged)));

        private static void OnContactoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorProveedor selector = (SelectorProveedor)d;
            if (selector is null || selector.Empresa is null || selector.Proveedor is null)
            {
                return;
            }

            SelectorProveedorViewModel vm = selector.DataContext as SelectorProveedorViewModel;

            string newValue = (string)e.NewValue;
            if (newValue != null && newValue.Trim() != newValue)
            {
                newValue = newValue.Trim(); // Aplica Trim() al nuevo valor
                selector.SetValue(ContactoProperty, newValue);
            }

            selector.ResetTimer();
        }


        /// <summary>
        /// Gets or sets the EMPRESA para las llamadas a la API
        /// </summary>
        public string Empresa
        {
            get { return (string)GetValue(EmpresaProperty); }
            set
            {
                SetValue(EmpresaProperty, value);
            }
        }

        /// <summary>
        /// Identified the EMPRESA dependency property
        /// </summary>
        public static readonly DependencyProperty EmpresaProperty =
            DependencyProperty.Register(nameof(Empresa), typeof(string),
              typeof(SelectorProveedor),
                new FrameworkPropertyMetadata(Constantes.Empresas.EMPRESA_DEFECTO, new PropertyChangedCallback(OnEmpresaChanged)));

        private static void OnEmpresaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorProveedor selector = (SelectorProveedor)d;
        }

        /// <summary>
        /// Gets or sets the ETIQUETA para las llamadas a la API
        /// </summary>
        public string Etiqueta
        {
            get { return (string)GetValue(EtiquetaProperty); }
            set
            {
                SetValue(EtiquetaProperty, value);
            }
        }

        /// <summary>
        /// Identified the ETIQUETA dependency property
        /// </summary>
        public static readonly DependencyProperty EtiquetaProperty =
            DependencyProperty.Register(nameof(Etiqueta), typeof(string),
              typeof(SelectorProveedor),
              new UIPropertyMetadata("Seleccione un proveedor:"));





        /// <summary>
        /// Gets or sets the PROVEEDOR para las llamadas a la API
        /// </summary>
        public string Proveedor
        {
            get { return (string)GetValue(ProveedorProperty); }
            set
            {
                SetValue(ProveedorProperty, value);
            }
        }

        /// <summary>
        /// Identified the PROVEEDOR dependency property
        /// </summary>
        public static readonly DependencyProperty ProveedorProperty =
            DependencyProperty.Register(nameof(Proveedor), typeof(string),
              typeof(SelectorProveedor),
              new FrameworkPropertyMetadata(null,
                  FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                  new PropertyChangedCallback(OnProveedorChanged)));

        private static void OnProveedorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorProveedor selector = (SelectorProveedor)d;
            if (selector == null || e.NewValue == e.OldValue)
            {
                return;
            }

            SelectorProveedorViewModel vm = selector.DataContext as SelectorProveedorViewModel;

            string newValue = (string)e.NewValue;
            if (newValue != null && newValue != newValue.Trim())
            {
                newValue = newValue.Trim(); // Aplica Trim() al nuevo valor
                selector.SetValue(ProveedorProperty, newValue);
            }


            if (selector.Proveedor is null)
            {
                vm.ListaProveedores.ElementoSeleccionado = null;
            }
            else if (vm.ListaProveedores.Filtro != selector.Proveedor.Trim())
            {
                selector.txtFiltro.Text = selector.Proveedor.Trim();
                vm.ListaProveedores.Filtro = selector.Proveedor.Trim();
            };
            
            if (selector.Contacto == (vm.ListaProveedores?.ElementoSeleccionado as ProveedorDTO)?.Contacto &&
                selector.Proveedor == (vm.ListaProveedores?.ElementoSeleccionado as ProveedorDTO)?.Proveedor)
            {
                return;
            }

            selector.ResetTimer();
        }






        /// <summary>
        /// Gets or sets the PROVEEDORCOMPLETO para las llamadas a la API
        /// </summary>
        public ProveedorDTO ProveedorCompleto
        {
            get { return (ProveedorDTO)GetValue(ProveedorCompletoProperty); }
            set
            {
                SetValue(ProveedorCompletoProperty, value);
            }
        }

        /// <summary>
        /// Identified the PROVEEDORCOMPLETO dependency property
        /// </summary>
        public static readonly DependencyProperty ProveedorCompletoProperty =
            DependencyProperty.Register(nameof(ProveedorCompleto), typeof(ProveedorDTO),
              typeof(SelectorProveedor),
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnProveedorCompletoChanged)));

        private static void OnProveedorCompletoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
                SelectorProveedor selector = (SelectorProveedor)d;
        }

        #endregion

        #region Funciones auxiliares
        private async Task PrepararSelector()
        {
            _vm = DataContext as SelectorProveedorViewModel;

            _vm.ListaProveedores.HayQueCargarDatos += async () => {
                // Por aquí entra cuando buscamos un proveedor en el selector directamente
                await _vm.CargarDatos(Empresa, txtFiltro.Text, null);
            };

            _vm.ListaProveedores.ElementoSeleccionadoChanged += (sender, args) =>
            {
                ProveedorDTO oldProveedor = args.OldValue as ProveedorDTO;
                ProveedorDTO newProveedor = args.NewValue as ProveedorDTO;

                if (_vm.ListaProveedores is not null && _vm.ListaProveedores is not null && ProveedorCompleto != _vm.ListaProveedores.ElementoSeleccionado)
                {
                    selectorProveedor.SetValue(ProveedorCompletoProperty, _vm.ListaProveedores.ElementoSeleccionado as ProveedorDTO);
                }
                if (Proveedor != newProveedor?.Proveedor)
                {
                    selectorProveedor.SetValue(ProveedorProperty, newProveedor?.Proveedor);
                }
                if (Contacto != newProveedor?.Contacto)
                {
                    selectorProveedor.SetValue(ContactoProperty, newProveedor?.Contacto);
                }
                //OnPropertyChanged(string.Empty);
                _vm.ActualizarPropertyChanged();
            };


        }

        private void ResetTimer()
        {
            if (_timer == null)
            {
                _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Normal, TimerElapsed, Dispatcher);
            }
            else
            {
                _timer.Stop();
                _timer.Start();
            }
        }

        private void TimerElapsed(object sender, EventArgs e)
        {
            // Por aquí entra cuando se cambian el Cliente y Contacto seleccionados por Binding
            _timer.Stop();
            _vm.CargarDatos(selectorProveedor.Empresa, selectorProveedor.Proveedor, selectorProveedor.Contacto);
        }

        #endregion

        #region Eventos de la vista
        private void pnlDatosProveedor_MouseUp(object sender, MouseButtonEventArgs e)
        {

        }

        private void brdProveedor_MouseUp(object sender, MouseButtonEventArgs e)
        {
            SelectorProveedorViewModel vm = DataContext as SelectorProveedorViewModel;
            try
            {
                Border controlSender = (Border)sender;
                vm.ListaProveedores.ElementoSeleccionado = (ProveedorDTO)controlSender.DataContext;
                vm.ListaProveedores.Filtro = ProveedorCompleto?.Proveedor;
                vm.ListaProveedores.ListaOriginal?.Clear();
                txtFiltro.Focus();
            }
            catch
            {
                vm.ListaProveedores.Filtro = "Desconectado";
            }
        }

        private void txtFiltro_KeyUp(object sender, KeyEventArgs e)
        {

        }
        #endregion
    }
}
