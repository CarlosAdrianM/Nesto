using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>Una opción del filtro: el valor con que se trabaja y el texto que se enseña.</summary>
    public class OpcionFiltroPromesaRiesgo
    {
        public OpcionFiltroPromesaRiesgo(string valor, string texto)
        {
            Valor = valor;
            Texto = texto;
        }

        public string Valor { get; }
        public string Texto { get; }
    }

    /// <summary>Una fila de la lista: el riesgo de la API con los textos ya preparados para la vista.</summary>
    public class FilaPedidoPromesaRiesgo
    {
        public const string SIN_FECHA = "Sin fecha";

        public FilaPedidoPromesaRiesgo(PedidoPromesaRiesgo riesgo)
        {
            Riesgo = riesgo ?? throw new ArgumentNullException(nameof(riesgo));
        }

        public PedidoPromesaRiesgo Riesgo { get; }
        public int Id => Riesgo.Id;
        public string Empresa => string.IsNullOrWhiteSpace(Riesgo.Empresa) ? Constantes.Empresas.EMPRESA_DEFECTO : Riesgo.Empresa.Trim();
        public int Pedido => Riesgo.Pedido;
        public string Cliente => Riesgo.Cliente?.Trim();
        public string NombreCliente => Riesgo.NombreCliente?.Trim();
        /// <summary>«15191 · Peluquería Prueba» (o solo el número si no hay nombre).</summary>
        public string ClienteYNombre => string.IsNullOrWhiteSpace(NombreCliente) ? Cliente : $"{Cliente} · {NombreCliente}";
        /// <summary>El de estética y, si lo tiene, el de peluquería: «JGP / PEL: ASH».</summary>
        public string Vendedor
        {
            get
            {
                string estetica = Riesgo.Vendedor?.Trim();
                string peluqueria = Riesgo.VendedorPeluqueria?.Trim();
                if (string.IsNullOrEmpty(peluqueria) || string.Equals(peluqueria, estetica, StringComparison.OrdinalIgnoreCase))
                {
                    return estetica;
                }
                return string.IsNullOrEmpty(estetica) ? $"PEL: {peluqueria}" : $"{estetica} / PEL: {peluqueria}";
            }
        }
        public DateTime FechaPrometida => Riesgo.FechaPrometida.Date;
        public DateTime? FechaNueva => Riesgo.FechaNueva?.Date;
        public bool SinFecha => Riesgo.SinFecha;
        public string TextoFechaPrometida => FechaPrometida.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        /// <summary>La fecha nueva o «Sin fecha».</summary>
        public string TextoFechaNueva => FechaNueva.HasValue ? FechaNueva.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : SIN_FECHA;
        /// <summary>«3 días», «1 día» o «Sin fecha».</summary>
        public string TextoRetraso
        {
            get
            {
                int? dias = Riesgo.DiasRetraso;
                if (!dias.HasValue)
                {
                    return SIN_FECHA;
                }
                return dias.Value == 1 ? "1 día" : $"{dias.Value} días";
            }
        }
        public string Motivo => Riesgo.Motivo?.Trim();
        public string Evento => Riesgo.Evento?.Trim();
        public DateTime Detectado => Riesgo.Detectado;
        public string TextoDetectado => Riesgo.Detectado.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        public bool EstaResuelto => Riesgo.EstaResuelto;
        public string Estado => EstaResuelto ? "Resuelto" : "Pendiente";
        /// <summary>«24/10/2026 09:15 · Se ha servido.» o null si sigue pendiente.</summary>
        public string TextoResuelto => !EstaResuelto
            ? null
            : Riesgo.Resuelto.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
              + (string.IsNullOrWhiteSpace(Riesgo.MotivoResolucion) ? string.Empty : " · " + Riesgo.MotivoResolucion.Trim());
    }

    /// <summary>
    /// Nesto#546 (NestoAPI#693, corte 2): «Pedidos con la fecha en riesgo». La API detecta, registra y avisa (campana y correo)
    /// cuando la fecha de entrega a la agencia prometida al cliente se retrasa o se queda sin fecha; aquí se ve la lista (el
    /// vendedor los suyos, el jefe los de su equipo, Dirección todos: lo decide la API con el usuario) y se actúa: abrir el
    /// pedido y copiar un texto para avisar al cliente. Cambiar el modo de servir, pedir una reposición puntual y sustituir
    /// el producto se hacen por ahora desde el pedido (los botones lo abren); la acción directa queda para otro corte.
    /// </summary>
    public class PedidosPromesaRiesgoViewModel : ObservableObject, IReceptorNavegacion
    {
        public const string FILTRO_PENDIENTES = "Pendientes";
        public const string FILTRO_RESUELTOS = "Resueltos";
        public const string FILTRO_TODOS = "Todos";

        private readonly IServicioPedidosPromesaRiesgo _servicio;
        private readonly IAbridorPedidos _abridorPedidos;
        private readonly Action<string> _copiarAlPortapapeles;
        private readonly bool _filtrosListos;
        private int? _pedidoAElegir;

        public PedidosPromesaRiesgoViewModel(IServicioPedidosPromesaRiesgo servicio, IAbridorPedidos abridorPedidos)
            : this(servicio, abridorPedidos, null)
        {
        }

        /// <param name="copiarAlPortapapeles">Para las pruebas; sin él, el portapapeles de Windows.</param>
        internal PedidosPromesaRiesgoViewModel(IServicioPedidosPromesaRiesgo servicio, IAbridorPedidos abridorPedidos, Action<string> copiarAlPortapapeles)
        {
            _servicio = servicio;
            _abridorPedidos = abridorPedidos;
            _copiarAlPortapapeles = copiarAlPortapapeles ?? CopiarConWindows;
            CargarCommand = new AsyncRelayCommand(CargarAsync);
            AbrirPedidoCommand = new RelayCommand<FilaPedidoPromesaRiesgo>(AbrirPedido, fila => PuedeActuar(fila));
            CopiarTextoClienteCommand = new RelayCommand(CopiarTextoCliente, () => PuedeActuar(Seleccionado));
            CambiarModoCommand = new RelayCommand(() => AbrirPedidoPara("cambie el modo de servir desde la cabecera del pedido"), () => PuedeActuar(Seleccionado));
            ReposicionPuntualCommand = new RelayCommand(() => AbrirPedidoPara("pida la reposición puntual a la tienda que tiene el producto desde el pedido"), () => PuedeActuar(Seleccionado));
            SustituirProductoCommand = new RelayCommand(() => AbrirPedidoPara("sustituya el producto que falla en las líneas del pedido"), () => PuedeActuar(Seleccionado));
            _filtrosListos = true;
        }

        public static IReadOnlyList<OpcionFiltroPromesaRiesgo> Filtros { get; } = new List<OpcionFiltroPromesaRiesgo>
        {
            new OpcionFiltroPromesaRiesgo(FILTRO_PENDIENTES, "Pendientes"),
            new OpcionFiltroPromesaRiesgo(FILTRO_RESUELTOS, "Resueltos"),
            new OpcionFiltroPromesaRiesgo(FILTRO_TODOS, "Todos")
        };

        public string Titulo => "Pedidos con la fecha en riesgo";
        public string Ayuda => "Pedidos cuya entrega a la agencia prometida al cliente ya no se cumple: se retrasan o se quedan sin fecha. " +
            "Usted ve los suyos; el jefe de equipo, los de su equipo; Dirección, todos. Un pedido se resuelve solo cuando vuelve a cumplir " +
            "la fecha, se sirve o se renegocia la fecha. Abra el pedido para actuar y avise al cliente con el texto preparado.";
        public string Empresa { get; set; } = Constantes.Empresas.EMPRESA_DEFECTO;

        public ObservableCollection<FilaPedidoPromesaRiesgo> Pedidos { get; } = new ObservableCollection<FilaPedidoPromesaRiesgo>();

        // ---------- NestoAPI#693 (Carlos, 28/10/26): qué avisos recibe el usuario ----------
        /// <summary>Las opciones que da la API (Todo, SoloCorreo, SoloCampana, Nada), con su texto.</summary>
        public ObservableCollection<OpcionAvisosPromesaRiesgo> OpcionesAvisos { get; } = new ObservableCollection<OpcionAvisosPromesaRiesgo>();

        private bool _avisosListos;
        private string _avisos;
        /// <summary>Lo elegido. Al cambiarlo desde el desplegable se guarda en el servidor; al cargar no.</summary>
        public string Avisos
        {
            get => _avisos;
            set
            {
                if (SetProperty(ref _avisos, value) && _avisosListos && !string.IsNullOrWhiteSpace(value))
                {
                    _ = GuardarAvisosAsync(value);
                }
            }
        }

        private async Task CargarAvisosAsync()
        {
            try
            {
                AvisosPromesaRiesgo avisos = await _servicio.LeerAvisos().ConfigureAwait(true);
                if (avisos == null)
                {
                    return;
                }
                _avisosListos = false;
                OpcionesAvisos.Clear();
                foreach (OpcionAvisosPromesaRiesgo opcion in avisos.Opciones ?? new List<OpcionAvisosPromesaRiesgo>())
                {
                    OpcionesAvisos.Add(opcion);
                }
                Avisos = avisos.Valor;
            }
            catch (Exception)
            {
                // Sin preferencia no pasa nada: la ventana sigue; el desplegable queda vacío
            }
            finally
            {
                _avisosListos = OpcionesAvisos.Count > 0;
            }
        }

        private async Task GuardarAvisosAsync(string valor)
        {
            try
            {
                AvisosPromesaRiesgo guardado = await _servicio.GuardarAvisos(valor).ConfigureAwait(true);
                Mensaje = $"Avisos de pedidos en riesgo: {guardado?.Texto ?? valor}.";
            }
            catch (Exception ex)
            {
                Mensaje = "No se ha podido guardar qué avisos recibe: " + ex.Message;
            }
        }

        private string _filtro = FILTRO_PENDIENTES;
        /// <summary>Pendientes (por defecto), Resueltos o Todos. Al cambiarlo se vuelve a leer.</summary>
        public string Filtro
        {
            get => _filtro;
            set
            {
                if (SetProperty(ref _filtro, value) && _filtrosListos)
                {
                    _ = CargarAsync();
                }
            }
        }

        private FilaPedidoPromesaRiesgo _seleccionado;
        public FilaPedidoPromesaRiesgo Seleccionado
        {
            get => _seleccionado;
            set
            {
                if (SetProperty(ref _seleccionado, value))
                {
                    OnPropertyChanged(nameof(HaySeleccionado));
                    OnPropertyChanged(nameof(TextoCliente));
                    AvisarComandos();
                }
            }
        }

        public bool HaySeleccionado => Seleccionado != null;

        /// <summary>El texto, de usted, para avisar al cliente del pedido elegido (null sin pedido).</summary>
        public string TextoCliente => Seleccionado == null ? null : TextoParaCliente(Seleccionado.Riesgo);

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    AvisarComandos();
                }
            }
        }

        private bool _cargada;
        /// <summary>Ya se ha leído al menos una vez.</summary>
        public bool Cargada { get => _cargada; private set => SetProperty(ref _cargada, value); }

        private string _mensaje;
        public string Mensaje { get => _mensaje; set => SetProperty(ref _mensaje, value); }

        /// <summary>«3 pedidos en riesgo (1 sin fecha)», «Ningún pedido con la fecha en riesgo.»…</summary>
        public string Resumen
        {
            get
            {
                int pendientes = Pedidos.Count(p => !p.EstaResuelto);
                int sinFecha = Pedidos.Count(p => !p.EstaResuelto && p.SinFecha);
                int resueltos = Pedidos.Count - pendientes;
                string detalleSinFecha = sinFecha == 0 ? string.Empty : sinFecha == 1 ? " (1 sin fecha)" : $" ({sinFecha} sin fecha)";
                switch (Filtro)
                {
                    case FILTRO_PENDIENTES:
                        return pendientes == 0 ? "Ningún pedido con la fecha en riesgo."
                            : pendientes == 1 ? "1 pedido con la fecha en riesgo" + detalleSinFecha
                            : $"{pendientes} pedidos con la fecha en riesgo" + detalleSinFecha;
                    case FILTRO_RESUELTOS:
                        return resueltos == 0 ? "Ningún pedido resuelto en los últimos días." : resueltos == 1 ? "1 resuelto" : $"{resueltos} resueltos";
                    default:
                        return $"{pendientes} en riesgo{detalleSinFecha} y " + (resueltos == 1 ? "1 resuelto" : $"{resueltos} resueltos");
                }
            }
        }

        public IAsyncRelayCommand CargarCommand { get; }
        /// <summary>Abre el pedido de la fila (doble clic) o, sin fila, el elegido.</summary>
        public IRelayCommand<FilaPedidoPromesaRiesgo> AbrirPedidoCommand { get; }
        /// <summary>Copia al portapapeles el texto para avisar al cliente del pedido elegido.</summary>
        public IRelayCommand CopiarTextoClienteCommand { get; }
        /// <summary>Pendiente de hacer en un clic: por ahora abre el pedido para cambiar el modo de servir desde ahí.</summary>
        public IRelayCommand CambiarModoCommand { get; }
        /// <summary>Pendiente de hacer en un clic: por ahora abre el pedido para pedir la reposición puntual desde ahí.</summary>
        public IRelayCommand ReposicionPuntualCommand { get; }
        /// <summary>Pendiente de hacer en un clic: por ahora abre el pedido para sustituir el producto desde ahí.</summary>
        public IRelayCommand SustituirProductoCommand { get; }

        /// <summary>
        /// Cada vez que se llega (la cinta o el aviso de la campana, con la pestaña nueva o la ya abierta) se vuelve a leer. Si la
        /// campana trae el pedido del aviso, se deja elegido al terminar de leer (y se pasa a «Todos» si no está entre los pendientes).
        /// </summary>
        public void AlLlegar(ParametrosNavegacion parametros)
        {
            _pedidoAElegir = LeerPedido(parametros);
            if (!EstaOcupado)
            {
                _ = CargarAsync();
            }
        }

        internal static int? LeerPedido(ParametrosNavegacion parametros)
        {
            if (parametros == null || !parametros.ContainsKey(NotificacionBuzon.PARAMETRO_PROMESA_PEDIDO))
            {
                return null;
            }
            object valor = parametros.GetValue<object>(NotificacionBuzon.PARAMETRO_PROMESA_PEDIDO);
            if (valor == null)
            {
                return null;
            }
            return int.TryParse(valor.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int pedido) && pedido > 0 ? pedido : (int?)null;
        }

        public async Task CargarAsync()
        {
            Mensaje = null;
            EstaOcupado = true;
            try
            {
                List<PedidoPromesaRiesgo> leidos = await _servicio.Leer(Empresa, Filtro != FILTRO_PENDIENTES).ConfigureAwait(true) ?? new List<PedidoPromesaRiesgo>();
                Poner(leidos);
                if (OpcionesAvisos.Count == 0)
                {
                    await CargarAvisosAsync().ConfigureAwait(true); // NestoAPI#693: una vez por ventana
                }
                if (_pedidoAElegir.HasValue && Seleccionado == null && Filtro == FILTRO_PENDIENTES)
                {
                    // El aviso habla de un pedido que ya no está pendiente (se ha resuelto entretanto): se enseña con los resueltos
                    _filtro = FILTRO_TODOS;
                    OnPropertyChanged(nameof(Filtro));
                    Poner(await _servicio.Leer(Empresa, true).ConfigureAwait(true) ?? new List<PedidoPromesaRiesgo>());
                    if (Seleccionado == null)
                    {
                        Mensaje = $"El pedido {_pedidoAElegir.Value} ya no está entre los pedidos con la fecha en riesgo.";
                    }
                }
                _pedidoAElegir = null;
            }
            catch (PedidosPromesaRiesgoException ex)
            {
                Mensaje = ex.Message;
            }
            catch (Exception ex)
            {
                Mensaje = "No se ha podido hablar con el servidor: " + ex.Message;
            }
            finally
            {
                EstaOcupado = false;
            }
            Cargada = true;
        }

        private void Poner(IEnumerable<PedidoPromesaRiesgo> leidos)
        {
            int? pedidoAnterior = _pedidoAElegir ?? Seleccionado?.Pedido;
            Seleccionado = null;
            Pedidos.Clear();
            foreach (PedidoPromesaRiesgo riesgo in Filtrar(leidos, Filtro))
            {
                Pedidos.Add(new FilaPedidoPromesaRiesgo(riesgo));
            }
            if (pedidoAnterior.HasValue)
            {
                Seleccionado = Pedidos.FirstOrDefault(p => p.Pedido == pedidoAnterior.Value);
            }
            OnPropertyChanged(nameof(Resumen));
        }

        /// <summary>Pura: la API manda los pendientes y, pidiéndolos, los resueltos detrás; el filtro deja los que tocan.</summary>
        internal static IEnumerable<PedidoPromesaRiesgo> Filtrar(IEnumerable<PedidoPromesaRiesgo> riesgos, string filtro)
        {
            IEnumerable<PedidoPromesaRiesgo> validos = (riesgos ?? Enumerable.Empty<PedidoPromesaRiesgo>()).Where(r => r != null);
            switch (filtro)
            {
                case FILTRO_RESUELTOS:
                    return validos.Where(r => r.EstaResuelto);
                case FILTRO_TODOS:
                    return validos;
                default:
                    return validos.Where(r => !r.EstaResuelto);
            }
        }

        private bool PuedeActuar(FilaPedidoPromesaRiesgo fila) => !EstaOcupado && (fila ?? Seleccionado) != null;

        private void AbrirPedido(FilaPedidoPromesaRiesgo fila)
        {
            FilaPedidoPromesaRiesgo destino = fila ?? Seleccionado;
            if (destino == null || _abridorPedidos == null)
            {
                return;
            }
            try
            {
                _abridorPedidos.Abrir(destino.Empresa, destino.Pedido);
            }
            catch (Exception ex)
            {
                Mensaje = $"No se ha podido abrir el pedido {destino.Pedido}: {ex.Message}";
            }
        }

        /// <summary>Las acciones que aún no se hacen en un clic desde aquí: se abre el pedido y se dice dónde hacerlo.</summary>
        private void AbrirPedidoPara(string queHacer)
        {
            FilaPedidoPromesaRiesgo destino = Seleccionado;
            if (destino == null)
            {
                return;
            }
            Mensaje = null;
            AbrirPedido(destino);
            if (Mensaje == null)
            {
                // Si el abridor ha fallado, se queda su motivo; si no, se dice dónde hacerlo
                Mensaje = $"Se ha abierto el pedido {destino.Pedido}: {queHacer}.";
            }
        }

        private void CopiarTextoCliente()
        {
            string texto = TextoCliente;
            if (string.IsNullOrWhiteSpace(texto))
            {
                return;
            }
            try
            {
                _copiarAlPortapapeles(texto);
                Mensaje = $"Texto para el cliente del pedido {Seleccionado.Pedido} copiado al portapapeles. Péguelo en el correo o en el WhatsApp.";
            }
            catch (Exception ex)
            {
                Mensaje = "No se ha podido copiar al portapapeles: " + ex.Message;
            }
        }

        /// <summary>
        /// El texto, de usted, para avisar al cliente: su pedido, la fecha que se le prometió y la nueva (o que de momento no
        /// hay fecha). Sin el porqué interno (reposiciones, inventarios): eso es para nosotros.
        /// </summary>
        public static string TextoParaCliente(PedidoPromesaRiesgo riesgo)
        {
            if (riesgo == null)
            {
                return null;
            }
            string prometida = riesgo.FechaPrometida.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            var s = new StringBuilder();
            _ = s.Append("Buenos días. Le escribimos por su pedido ").Append(riesgo.Pedido.ToString(CultureInfo.InvariantCulture)).Append(". ");
            if (riesgo.FechaNueva.HasValue)
            {
                _ = s.Append("La entrega que le habíamos prometido para el ").Append(prometida)
                    .Append(" se retrasa: la fecha nueva de salida es el ").Append(riesgo.FechaNueva.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)).Append(". ");
            }
            else
            {
                _ = s.Append("La entrega que le habíamos prometido para el ").Append(prometida)
                    .Append(" se retrasa y de momento no podemos darle una fecha nueva; en cuanto la tengamos se la confirmaremos. ");
            }
            _ = s.Append("Si lo prefiere, podemos servirle ahora lo que ya tenemos y el resto en cuanto llegue, o cambiarle el producto por otro parecido. ")
                .Append("Lamentamos las molestias y quedamos a su disposición.");
            return s.ToString();
        }

        private void AvisarComandos()
        {
            AbrirPedidoCommand.NotifyCanExecuteChanged();
            CopiarTextoClienteCommand.NotifyCanExecuteChanged();
            CambiarModoCommand.NotifyCanExecuteChanged();
            ReposicionPuntualCommand.NotifyCanExecuteChanged();
            SustituirProductoCommand.NotifyCanExecuteChanged();
        }

        private static void CopiarConWindows(string texto) => System.Windows.Clipboard.SetText(texto);
    }
}
