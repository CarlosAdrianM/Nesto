using ControlesUsuario.Models;
using ControlesUsuario.Services;
using Prism.Ioc;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ControlesUsuario
{
    /// <summary>
    /// Control para seleccionar un CCC (Código Cuenta Cliente / IBAN) de un cliente.
    /// Carlos 20/11/24: Creado siguiendo el patrón de SelectorDireccionEntrega con DI y anti-bucles.
    /// </summary>
    public partial class SelectorCCC : UserControl, INotifyPropertyChanged
    {
        private readonly IServicioCCC _servicioCCC;
        private bool _estaCargando = false;

        #region Constructores

        // TODO REFACTORIZACIÓN FUTURA: Igual que en SelectorDireccionEntrega,
        // considerar eliminar "DataContext = this" y usar solo ElementName bindings
        // para permitir que el control herede el DataContext del parent.
        // Por ahora dejamos DataContext = this para mantener el patrón actual.

        /// <summary>
        /// Constructor sin parámetros (usado por XAML).
        /// Carlos 20/11/24: Resuelve dependencias desde ContainerLocator igual que SelectorDireccionEntrega.
        /// </summary>
        public SelectorCCC()
        {
            InitializeComponent();
            // DataContext = this; // Comentado para seguir la lección aprendida

            try
            {
                _servicioCCC = ContainerLocator.Container.Resolve<IServicioCCC>();
            }
            catch
            {
                // Se usa solo para poder testar controles que incluyan un SelectorCCC
            }
        }

        /// <summary>
        /// Constructor con DI (PREFERIDO).
        /// </summary>
        public SelectorCCC(IServicioCCC servicioCCC)
        {
            InitializeComponent();
            // DataContext = this; // Comentado para seguir la lección aprendida

            _servicioCCC = servicioCCC ?? throw new ArgumentNullException(nameof(servicioCCC));
        }

        #endregion

        #region DependencyProperties - Entradas

        public static readonly DependencyProperty EmpresaProperty =
            DependencyProperty.Register(
                nameof(Empresa),
                typeof(string),
                typeof(SelectorCCC),
                new FrameworkPropertyMetadata(null, OnEmpresaChanged));

        public string Empresa
        {
            get => (string)GetValue(EmpresaProperty);
            set => SetValue(EmpresaProperty, value);
        }

        private static void OnEmpresaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var selector = (SelectorCCC)d;
            Debug.WriteLine($"[SelectorCCC] OnEmpresaChanged: '{e.OldValue}' -> '{e.NewValue}', _estaCargando={selector._estaCargando}");
            if (selector._estaCargando) return; // Prevenir bucles
            selector.CargarCCCsAsync();
        }

        public static readonly DependencyProperty ClienteProperty =
            DependencyProperty.Register(
                nameof(Cliente),
                typeof(string),
                typeof(SelectorCCC),
                new FrameworkPropertyMetadata(null, OnClienteChanged));

        public string Cliente
        {
            get => (string)GetValue(ClienteProperty);
            set => SetValue(ClienteProperty, value);
        }

        private static void OnClienteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var selector = (SelectorCCC)d;
            Debug.WriteLine($"[SelectorCCC] OnClienteChanged: '{e.OldValue}' -> '{e.NewValue}', _estaCargando={selector._estaCargando}");
            if (selector._estaCargando) return; // Prevenir bucles
            selector.CargarCCCsAsync();
        }

        public static readonly DependencyProperty ContactoProperty =
            DependencyProperty.Register(
                nameof(Contacto),
                typeof(string),
                typeof(SelectorCCC),
                new FrameworkPropertyMetadata(null, OnContactoChanged));

        public string Contacto
        {
            get => (string)GetValue(ContactoProperty);
            set => SetValue(ContactoProperty, value);
        }

        private static void OnContactoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var selector = (SelectorCCC)d;
            Debug.WriteLine($"[SelectorCCC] OnContactoChanged: '{e.OldValue}' -> '{e.NewValue}', _estaCargando={selector._estaCargando}");
            if (selector._estaCargando) return; // Prevenir bucles
            selector.CargarCCCsAsync();
        }

        public static readonly DependencyProperty FormaPagoProperty =
            DependencyProperty.Register(
                nameof(FormaPago),
                typeof(string),
                typeof(SelectorCCC),
                new FrameworkPropertyMetadata(null, OnFormaPagoChanged));

        public string FormaPago
        {
            get => (string)GetValue(FormaPagoProperty);
            set => SetValue(FormaPagoProperty, value);
        }

        private static void OnFormaPagoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var selector = (SelectorCCC)d;
            if (selector._estaCargando) return; // Prevenir bucles
            selector.ActualizarSeleccionSegunFormaPago();
        }

        #endregion

        #region DependencyProperty - Salida (TwoWay)

        public static readonly DependencyProperty CCCSeleccionadoProperty =
            DependencyProperty.Register(
                nameof(CCCSeleccionado),
                typeof(string),
                typeof(SelectorCCC),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnCCCSeleccionadoChanged));

        public string CCCSeleccionado
        {
            get => (string)GetValue(CCCSeleccionadoProperty);
            set => SetValue(CCCSeleccionadoProperty, value);
        }

        private static void OnCCCSeleccionadoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var selector = (SelectorCCC)d;

            // Comparar valores para evitar propagaciones innecesarias (prevenir bucles)
            if (e.OldValue?.ToString() == e.NewValue?.ToString())
                return;

            // Carlos 20/11/24: CRÍTICO - Disparar PropertyChanged para que el binding TwoWay funcione
            // Sin esto, el cambio NO se propaga de vuelta a pedido.ccc
            selector.OnPropertyChanged(nameof(CCCSeleccionado));
        }

        #endregion

        #region Propiedades Internas (INotifyPropertyChanged)

        private ObservableCollection<CCCItem> _listaCCCs;
        public ObservableCollection<CCCItem> ListaCCCs
        {
            get => _listaCCCs;
            set
            {
                if (_listaCCCs != value)
                {
                    _listaCCCs = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Lógica de Carga

        /// <summary>
        /// Carga los CCCs desde la API para la combinación Empresa/Cliente/Contacto actual.
        /// Incluye anti-loop con flag _estaCargando.
        /// </summary>
        private async void CargarCCCsAsync()
        {
            Debug.WriteLine($"[SelectorCCC] CargarCCCsAsync - Empresa='{Empresa}', Cliente='{Cliente}', Contacto='{Contacto}'");

            // Validar que tenemos los datos necesarios
            if (string.IsNullOrWhiteSpace(Empresa) ||
                string.IsNullOrWhiteSpace(Cliente) ||
                string.IsNullOrWhiteSpace(Contacto))
            {
                Debug.WriteLine($"[SelectorCCC] Datos incompletos - Saliendo");
                // Limpiar lista si faltan datos
                ListaCCCs = new ObservableCollection<CCCItem>();
                return;
            }

            // Modo degradado: si no hay servicio, no hacer nada
            if (_servicioCCC == null)
            {
                Debug.WriteLine("[SelectorCCC] Servicio CCC no disponible (modo degradado)");
                return;
            }

            _estaCargando = true;
            try
            {
                Debug.WriteLine($"[SelectorCCC] Llamando servicio ObtenerCCCs...");
                // Llamar al servicio
                var cccs = await _servicioCCC.ObtenerCCCs(Empresa, Cliente, Contacto);
                Debug.WriteLine($"[SelectorCCC] Servicio retornó {cccs?.Count() ?? 0} CCCs");

                // Construir lista con opción "(Sin CCC)"
                var lista = new ObservableCollection<CCCItem>();

                // 1. Agregar opción "(Sin CCC)" al principio
                lista.Add(new CCCItem
                {
                    empresa = Empresa,
                    cliente = Cliente,
                    contacto = Contacto,
                    numero = null, // NULL = Sin CCC
                    Descripcion = "(Sin CCC)",
                    estado = 1 // Válido
                });

                // 2. Agregar CCCs de la API
                foreach (var ccc in cccs)
                {
                    // Construir descripción formateada: "1 -> ES91 2100 0418 4502 0005 1332 (BBVA)"
                    if (ccc.EsInvalido)
                    {
                        ccc.Descripcion = $"{ccc.numero} (INVÁLIDO)";
                    }
                    else
                    {
                        // Formato preferido: número -> IBAN formateado (entidad)
                        string descripcion = ccc.numero;

                        if (!string.IsNullOrWhiteSpace(ccc.ibanFormateado))
                        {
                            descripcion = $"{ccc.numero} -> {ccc.ibanFormateado}";
                        }

                        if (!string.IsNullOrWhiteSpace(ccc.nombreEntidad))
                        {
                            descripcion += $" ({ccc.nombreEntidad})";
                        }
                        else if (!string.IsNullOrWhiteSpace(ccc.entidad))
                        {
                            descripcion += $" ({ccc.entidad})";
                        }

                        ccc.Descripcion = descripcion;
                    }

                    lista.Add(ccc);
                }

                ListaCCCs = lista;
                Debug.WriteLine($"[SelectorCCC] Lista asignada con {lista.Count} elementos");

                // Auto-seleccionar según lógica de negocio
                AutoSeleccionarCCC(lista);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SelectorCCC] Error al cargar CCCs: {ex.Message}");

                // Lista con solo "(Sin CCC)" en caso de error
                ListaCCCs = new ObservableCollection<CCCItem>
                {
                    new CCCItem
                    {
                        empresa = Empresa,
                        cliente = Cliente,
                        contacto = Contacto,
                        numero = null,
                        Descripcion = "(Sin CCC)",
                        estado = 1
                    }
                };

                // Seleccionar "(Sin CCC)" en caso de error
                CCCSeleccionado = null;
            }
            finally
            {
                _estaCargando = false;
            }
        }

        #endregion

        #region Lógica de Auto-Selección

        /// <summary>
        /// Auto-selecciona un CCC según la lógica de negocio:
        /// - Si FormaPago = "RCB" (Recibo) → seleccionar primer CCC válido
        /// - Si FormaPago != "RCB" → seleccionar "(Sin CCC)"
        /// - Si ya hay una selección válida, respetarla
        /// </summary>
        private void AutoSeleccionarCCC(ObservableCollection<CCCItem> lista)
        {
            Debug.WriteLine($"[SelectorCCC] AutoSeleccionarCCC - CCCSeleccionado actual='{CCCSeleccionado}', FormaPago='{FormaPago}', items en lista={lista?.Count ?? 0}");

            // Si ya hay una selección válida en la lista, respetarla
            if (!string.IsNullOrEmpty(CCCSeleccionado))
            {
                var existe = lista.Any(c => c.numero == CCCSeleccionado);
                Debug.WriteLine($"[SelectorCCC] AutoSeleccionarCCC - CCC '{CCCSeleccionado}' existe en lista: {existe}");
                if (existe)
                    return; // Mantener selección actual
                Debug.WriteLine($"[SelectorCCC] AutoSeleccionarCCC - ¡CCC NO EXISTE EN LISTA! Se va a resetear...");
            }

            // Lógica de auto-selección según forma de pago
            if (FormaPago?.Trim() == "RCB") // RCB = Recibo Bancario
            {
                // Forma de pago es RCB (Recibo) → Seleccionar primer CCC válido
                var primerValido = lista.FirstOrDefault(c => c.EsValido && !string.IsNullOrEmpty(c.numero));
                Debug.WriteLine($"[SelectorCCC] AutoSeleccionarCCC - FormaPago=RCB, seleccionando primerValido='{primerValido?.numero}'");
                CCCSeleccionado = primerValido?.numero;
            }
            else
            {
                // Forma de pago NO es Recibo → Seleccionar "(Sin CCC)"
                Debug.WriteLine($"[SelectorCCC] AutoSeleccionarCCC - FormaPago!=RCB, seleccionando null (Sin CCC)");
                CCCSeleccionado = null;
            }
            Debug.WriteLine($"[SelectorCCC] AutoSeleccionarCCC - CCCSeleccionado final='{CCCSeleccionado}'");
        }

        /// <summary>
        /// Actualiza la selección de CCC cuando cambia la FormaPago.
        /// </summary>
        private void ActualizarSeleccionSegunFormaPago()
        {
            if (_estaCargando || ListaCCCs == null || !ListaCCCs.Any())
                return;

            AutoSeleccionarCCC(ListaCCCs);
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
