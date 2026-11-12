using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto.ViewModels
{
    /// <summary>
    /// Nesto#635 (API en NestoAPI#855): la ventana «Recogidas solicitadas». El vendedor pide la recogida (NestoApp) y aquí el almacén:
    /// <list type="bullet">
    /// <item>ve las pendientes (GET …/Solicitudes/Pendientes) con el cliente, qué hay que recoger, bultos, factura, QUIÉN RECOGE (reparto
    /// propio o el vendedor), quién y cuándo la pidió y si lleva el aviso de «sin respuesta»;</item>
    /// <item>la confirma corrigiendo bultos y texto (nace la nota NR{pedido}, cuyo código se enseña), o la rechaza con motivo obligatorio;</item>
    /// <item>en «Por cliente» (GET …/Solicitudes?cliente=, todas las del cliente con su estado) anula las que ya están en marcha (la API
    /// decide quién puede con 403/409 y dice en AvisoAnulacion lo que queda por hacer a mano).</item>
    /// </list>
    /// No hay endpoint de «todas las resueltas recientes»: lo resuelto se ve por cliente. Se refresca sola por SignalR (grupo
    /// «RecogidasSolicitadas» del AlmacenHub), sin sondeo. Los mensajes de la API se enseñan tal cual; con 503 (falta el script) se
    /// avisa y la lista queda vacía.
    /// </summary>
    public class RecogidasSolicitadasViewModel : ObservableObject, IReceptorNavegacion, ITabCloseConfirmation
    {
        public const int PESTANA_PENDIENTES = 0;
        public const int PESTANA_CLIENTE = 1;
        public const string MENSAJE_SIN_GRUPO = "Contestar las recogidas solicitadas es cosa de Almacén, Administración, Dirección e " +
            "Informática: usted puede verlas, pero no confirmarlas ni rechazarlas.";
        public const string MENSAJE_BULTOS = "Los bultos tienen que ser un número de 1 a 999.";
        public const string MENSAJE_FALTA_TEXTO = "Escriba qué hay que recoger.";
        public const string MENSAJE_FALTA_MOTIVO = "Indique el motivo del rechazo: se lo enseñamos a quien la pidió. No se ha rechazado.";
        public const string MENSAJE_POR_CLIENTE_SIN_PERMISO = "En «Por cliente» puede ver las recogidas de sus clientes y anular las que haya pedido usted.";
        public const string MENSAJE_FALTA_CLIENTE = "Escriba el número de cliente.";

        private readonly IServicioRecogidasSolicitadas _servicio;
        private readonly IServicioDialogos _dialogos;
        private readonly IGruposAlmacenTiempoReal _grupos;
        private readonly SynchronizationContext _contextoUI;
        private bool _enElGrupo;
        private int? _idAElegir;
        private int? _idEnEdicion;

        public RecogidasSolicitadasViewModel(IServicioRecogidasSolicitadas servicio, IConfiguracion configuracion, IServicioDialogos dialogos,
            IAvisosEnTiempoReal avisos)
            : this(servicio, configuracion, dialogos, avisos as IGruposAlmacenTiempoReal)
        {
        }

        /// <param name="grupos">Los avisos de SignalR del grupo «RecogidasSolicitadas». Sin él no llegan (se lee al abrir y con «Volver a leer»).</param>
        internal RecogidasSolicitadasViewModel(IServicioRecogidasSolicitadas servicio, IConfiguracion configuracion, IServicioDialogos dialogos,
            IGruposAlmacenTiempoReal grupos)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            _grupos = grupos;
            _contextoUI = SynchronizationContext.Current;
            PuedeGestionar = configuracion != null
                && (configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)
                    || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)
                    || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)
                    || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA));

            CargarCommand = new AsyncRelayCommand(RecargarTodoAsync, () => !EstaOcupado);
            ConfirmarCommand = new AsyncRelayCommand(ConfirmarAsync, () => PuedeContestar);
            RechazarCommand = new AsyncRelayCommand(RechazarAsync, () => PuedeContestar);
            BuscarClienteCommand = new AsyncRelayCommand(BuscarClienteAsync, () => !EstaOcupado && Disponible);
            AnularCommand = new AsyncRelayCommand(AnularAsync, () => !EstaOcupado && Disponible && SeleccionadaDelCliente?.SePuedeAnular == true);
        }

        public string Titulo => "Recogidas solicitadas";
        public string Ayuda => "Las recogidas que piden los vendedores. Al confirmarla se crea la nota de recogida (NR…): si la recoge el reparto " +
            "propio sigue el camino de siempre de las notas de recogida; si la recoge el vendedor, no entra en el reparto. En los dos casos " +
            "se cierra leyendo el código de la nota al llegar a Algete.";

        public string Empresa => Constantes.Empresas.EMPRESA_DEFECTO;

        /// <summary>Almacén, Administración, Dirección e Informática (los que la API deja confirmar y rechazar).</summary>
        public bool PuedeGestionar { get; }

        public IAsyncRelayCommand CargarCommand { get; }
        public IAsyncRelayCommand ConfirmarCommand { get; }
        public IAsyncRelayCommand RechazarCommand { get; }
        public IAsyncRelayCommand BuscarClienteCommand { get; }
        public IAsyncRelayCommand AnularCommand { get; }

        /// <summary>Lo que se está releyendo por un aviso de SignalR (los tests lo esperan).</summary>
        internal Task RecargaEnCurso { get; private set; } = Task.CompletedTask;

        // ------------------------------------------------------------------ estado

        private bool _disponible = true;
        /// <summary>False con 503 (falta el script en el servidor): nada se puede hacer.</summary>
        public bool Disponible
        {
            get => _disponible;
            private set
            {
                if (SetProperty(ref _disponible, value))
                {
                    NotificarComandos();
                }
            }
        }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    NotificarComandos();
                }
            }
        }

        private string _aviso;
        /// <summary>El aviso fijo de arriba: sin script (503), sin permiso (403) o sin grupo para contestar.</summary>
        public string Aviso
        {
            get => _aviso;
            private set
            {
                if (SetProperty(ref _aviso, value))
                {
                    OnPropertyChanged(nameof(HayAviso));
                }
            }
        }

        public bool HayAviso => !string.IsNullOrWhiteSpace(Aviso);

        private string _mensaje;
        public string Mensaje { get => _mensaje; private set => SetProperty(ref _mensaje, value); }

        private int _pestanaSeleccionada = PESTANA_PENDIENTES;
        public int PestanaSeleccionada { get => _pestanaSeleccionada; set => SetProperty(ref _pestanaSeleccionada, value); }

        // ------------------------------------------------------------------ pendientes

        public ObservableCollection<RecogidaSolicitada> Pendientes { get; } = new ObservableCollection<RecogidaSolicitada>();

        /// <summary>«2 recogidas pendientes de contestar».</summary>
        public string TextoPendientes => Pendientes.Count switch
        {
            0 => "No hay recogidas pendientes de contestar.",
            1 => "1 recogida pendiente de contestar.",
            _ => $"{Pendientes.Count} recogidas pendientes de contestar."
        };

        private RecogidaSolicitada _seleccionada;
        public RecogidaSolicitada Seleccionada
        {
            get => _seleccionada;
            set
            {
                if (SetProperty(ref _seleccionada, value))
                {
                    // Una recarga (SignalR) vuelve a elegir la misma: no se pierde lo que se estaba corrigiendo
                    if (value?.Id != _idEnEdicion)
                    {
                        _idEnEdicion = value?.Id;
                        BultosEditados = value?.Bultos.ToString();
                        TextoEditado = value?.Texto?.Trim();
                    }
                    OnPropertyChanged(nameof(HaySeleccionada));
                    NotificarComandos();
                }
            }
        }

        public bool HaySeleccionada => Seleccionada != null;

        private string _bultosEditados;
        /// <summary>Los bultos que se confirmarán (de entrada, los pedidos).</summary>
        public string BultosEditados { get => _bultosEditados; set => SetProperty(ref _bultosEditados, value); }

        private string _textoEditado;
        /// <summary>Qué hay que recoger, como se confirmará (de entrada, lo pedido).</summary>
        public string TextoEditado { get => _textoEditado; set => SetProperty(ref _textoEditado, value); }

        private bool PuedeContestar => PuedeGestionar && Disponible && !EstaOcupado && Seleccionada != null;

        // ------------------------------------------------------------------ por cliente

        private string _clienteBusqueda;
        public string ClienteBusqueda { get => _clienteBusqueda; set => SetProperty(ref _clienteBusqueda, value); }

        private string _contactoBusqueda;
        /// <summary>Opcional: sin él, todas las del cliente.</summary>
        public string ContactoBusqueda { get => _contactoBusqueda; set => SetProperty(ref _contactoBusqueda, value); }

        public ObservableCollection<RecogidaSolicitada> DelCliente { get; } = new ObservableCollection<RecogidaSolicitada>();

        private RecogidaSolicitada _seleccionadaDelCliente;
        public RecogidaSolicitada SeleccionadaDelCliente
        {
            get => _seleccionadaDelCliente;
            set
            {
                if (SetProperty(ref _seleccionadaDelCliente, value))
                {
                    NotificarComandos();
                }
            }
        }

        /// <summary>El cliente (y contacto) de la última búsqueda, para releerla cuando cambia algo.</summary>
        private (string Empresa, string Cliente, string Contacto)? _clienteCargado;

        private string _textoDelCliente;
        public string TextoDelCliente { get => _textoDelCliente; private set => SetProperty(ref _textoDelCliente, value); }

        // ------------------------------------------------------------------ llegar, leer y SignalR

        public void AlLlegar(ParametrosNavegacion parametros)
        {
            EntrarEnGrupo();
            if (!PuedeGestionar)
            {
                Aviso = MENSAJE_SIN_GRUPO;
            }
            _idAElegir = LeerEntero(parametros, NotificacionBuzon.PARAMETRO_RECOGIDA_ID);
            string cliente = LeerTexto(parametros, NotificacionBuzon.PARAMETRO_RECOGIDA_CLIENTE);
            string contacto = LeerTexto(parametros, NotificacionBuzon.PARAMETRO_RECOGIDA_CONTACTO);
            string empresa = LeerTexto(parametros, NotificacionBuzon.PARAMETRO_RECOGIDA_EMPRESA) ?? Empresa;
            bool anulada = parametros != null && parametros.ContainsKey(NotificacionBuzon.PARAMETRO_RECOGIDA_ANULADA)
                && parametros.GetValue<object>(NotificacionBuzon.PARAMETRO_RECOGIDA_ANULADA) is bool b && b;
            _ = LlegarAsync(empresa, cliente, contacto, anulada);
        }

        internal Task LlegadaEnCurso { get; private set; } = Task.CompletedTask;

        private Task LlegarAsync(string empresa, string cliente, string contacto, bool anulada)
        {
            LlegadaEnCurso = LlegarInternoAsync(empresa, cliente, contacto, anulada);
            return LlegadaEnCurso;
        }

        private async Task LlegarInternoAsync(string empresa, string cliente, string contacto, bool anulada)
        {
            int? id = _idAElegir;
            await CargarPendientesAsync().ConfigureAwait(true);
            if (string.IsNullOrEmpty(cliente) || !Disponible)
            {
                return;
            }
            bool estaPendiente = id.HasValue && Pendientes.Any(p => p.Id == id.Value);
            if (estaPendiente && !anulada)
            {
                PestanaSeleccionada = PESTANA_PENDIENTES;
                return;
            }
            // Una anulada, o una que ya no está entre las pendientes (la ha contestado alguien): se busca por su cliente
            if (!anulada && id.HasValue && !estaPendiente)
            {
                Mensaje = $"La recogida {id.Value} ya no está pendiente: abajo, en «Por cliente», se ve cómo quedó.";
            }
            if (anulada || id.HasValue)
            {
                ClienteBusqueda = cliente;
                ContactoBusqueda = contacto;
                PestanaSeleccionada = PESTANA_CLIENTE;
                await CargarDelClienteAsync(empresa, cliente, contacto, id).ConfigureAwait(true);
            }
        }

        private static int? LeerEntero(ParametrosNavegacion parametros, string clave)
        {
            if (parametros == null || !parametros.ContainsKey(clave))
            {
                return null;
            }
            object valor = parametros.GetValue<object>(clave);
            return valor switch
            {
                int i => i,
                string s when int.TryParse(s, out int n) => n,
                _ => null
            };
        }

        private static string LeerTexto(ParametrosNavegacion parametros, string clave)
        {
            if (parametros == null || !parametros.ContainsKey(clave))
            {
                return null;
            }
            string texto = parametros.GetValue<object>(clave)?.ToString()?.Trim();
            return string.IsNullOrEmpty(texto) ? null : texto;
        }

        /// <summary>«Volver a leer»: las pendientes y, si se ha buscado un cliente, las suyas.</summary>
        internal async Task RecargarTodoAsync()
        {
            await CargarPendientesAsync().ConfigureAwait(true);
            if (_clienteCargado.HasValue && Disponible)
            {
                await CargarDelClienteAsync(_clienteCargado.Value.Empresa, _clienteCargado.Value.Cliente, _clienteCargado.Value.Contacto, SeleccionadaDelCliente?.Id).ConfigureAwait(true);
            }
        }

        internal async Task CargarPendientesAsync()
        {
            EstaOcupado = true;
            try
            {
                List<RecogidaSolicitada> leidas = await _servicio.LeerPendientes(Empresa).ConfigureAwait(true) ?? new List<RecogidaSolicitada>();
                Disponible = true;
                if (Aviso != null && Aviso != MENSAJE_SIN_GRUPO)
                {
                    Aviso = PuedeGestionar ? null : MENSAJE_SIN_GRUPO;
                }
                int? elegir = _idAElegir ?? Seleccionada?.Id;
                _idAElegir = null;
                // Al vaciar la lista la rejilla deja la selección a null: se guarda lo que se estaba corrigiendo por si vuelve la misma
                int? idEnEdicion = _idEnEdicion;
                string bultosEnEdicion = BultosEditados;
                string textoEnEdicion = TextoEditado;
                Pendientes.Clear();
                foreach (RecogidaSolicitada recogida in leidas)
                {
                    Pendientes.Add(recogida);
                }
                Seleccionada = Pendientes.FirstOrDefault(p => p.Id == elegir) ?? Pendientes.FirstOrDefault();
                if (idEnEdicion.HasValue && Seleccionada?.Id == idEnEdicion)
                {
                    BultosEditados = bultosEnEdicion;
                    TextoEditado = textoEnEdicion;
                }
            }
            catch (RecogidaSolicitadaException ex) when (ex.Codigo == 503)
            {
                NoDisponible(ex.Message);
            }
            catch (RecogidaSolicitadaException ex) when (ex.Codigo == 403)
            {
                // Sin permiso en la API: la lista vacía y el porqué (las de un cliente suyo sí las puede ver en «Por cliente»)
                Pendientes.Clear();
                Seleccionada = null;
                Aviso = ex.Message + " " + MENSAJE_POR_CLIENTE_SIN_PERMISO;
            }
            catch (Exception ex)
            {
                Mensaje = "No se han podido leer las recogidas pendientes: " + ex.Message;
            }
            finally
            {
                EstaOcupado = false;
                OnPropertyChanged(nameof(TextoPendientes));
            }
        }

        internal async Task BuscarClienteAsync()
        {
            string cliente = ClienteBusqueda?.Trim();
            if (string.IsNullOrEmpty(cliente))
            {
                Mensaje = MENSAJE_FALTA_CLIENTE;
                return;
            }
            Mensaje = null;
            await CargarDelClienteAsync(Empresa, cliente, ContactoBusqueda?.Trim(), null).ConfigureAwait(true);
        }

        private async Task CargarDelClienteAsync(string empresa, string cliente, string contacto, int? elegir)
        {
            EstaOcupado = true;
            try
            {
                string contactoBuscado = string.IsNullOrWhiteSpace(contacto) ? null : contacto.Trim();
                List<RecogidaSolicitada> leidas = await _servicio.LeerDelCliente(empresa, cliente, contactoBuscado).ConfigureAwait(true)
                    ?? new List<RecogidaSolicitada>();
                _clienteCargado = (empresa, cliente, contactoBuscado);
                DelCliente.Clear();
                foreach (RecogidaSolicitada recogida in leidas)
                {
                    DelCliente.Add(recogida);
                }
                SeleccionadaDelCliente = DelCliente.FirstOrDefault(r => r.Id == elegir) ?? DelCliente.FirstOrDefault();
                TextoDelCliente = DelCliente.Count == 0
                    ? $"El cliente {cliente} no tiene recogidas solicitadas."
                    : $"Las {DelCliente.Count} más recientes del cliente {cliente}{(contactoBuscado == null ? string.Empty : "/" + contactoBuscado)}.";
            }
            catch (RecogidaSolicitadaException ex) when (ex.Codigo == 503)
            {
                NoDisponible(ex.Message);
            }
            catch (RecogidaSolicitadaException ex)
            {
                DelCliente.Clear();
                SeleccionadaDelCliente = null;
                TextoDelCliente = ex.Message;
            }
            catch (Exception ex)
            {
                TextoDelCliente = "No se han podido leer las recogidas del cliente: " + ex.Message;
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        private void NoDisponible(string motivo)
        {
            Disponible = false;
            Aviso = motivo;
            Pendientes.Clear();
            Seleccionada = null;
            DelCliente.Clear();
            SeleccionadaDelCliente = null;
            OnPropertyChanged(nameof(TextoPendientes));
        }

        private void EntrarEnGrupo()
        {
            if (_grupos == null || _enElGrupo)
            {
                return;
            }
            _enElGrupo = true;
            _grupos.GrupoHaCambiado -= AlCambiarGrupo;
            _grupos.GrupoHaCambiado += AlCambiarGrupo;
            _grupos.EntrarEnGrupo(GruposAlmacen.RECOGIDAS_SOLICITADAS);
        }

        private void SalirDelGrupo()
        {
            if (_grupos == null || !_enElGrupo)
            {
                return;
            }
            _enElGrupo = false;
            _grupos.GrupoHaCambiado -= AlCambiarGrupo;
            _grupos.SalirDeGrupo(GruposAlmacen.RECOGIDAS_SOLICITADAS);
        }

        /// <summary>Alguien ha pedido, confirmado, rechazado o anulado una recogida (llega en otro hilo): se vuelve a leer.</summary>
        private void AlCambiarGrupo(object sender, string grupo)
        {
            if (!_enElGrupo || !string.Equals(grupo?.Trim(), GruposAlmacen.RECOGIDAS_SOLICITADAS, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            if (_contextoUI != null)
            {
                _contextoUI.Post(_ => RecargaEnCurso = RecargarTodoAsync(), null);
            }
            else
            {
                RecargaEnCurso = RecargarTodoAsync();
            }
        }

        /// <summary>Al cerrar la pestaña se sale del grupo de SignalR.</summary>
        public bool ConfirmTabClose()
        {
            SalirDelGrupo();
            return true;
        }

        // ------------------------------------------------------------------ confirmar, rechazar y anular

        internal async Task ConfirmarAsync()
        {
            RecogidaSolicitada recogida = Seleccionada;
            if (recogida == null)
            {
                return;
            }
            Mensaje = null;
            if (!short.TryParse(BultosEditados?.Trim(), out short bultos) || bultos < 1 || bultos > 999)
            {
                Mensaje = MENSAJE_BULTOS;
                return;
            }
            string texto = TextoEditado?.Trim();
            if (string.IsNullOrEmpty(texto))
            {
                Mensaje = MENSAJE_FALTA_TEXTO;
                return;
            }
            // Solo se manda lo que se corrige (null: lo pedido)
            short? bultosCorregidos = bultos == recogida.Bultos ? (short?)null : bultos;
            string textoCorregido = string.Equals(texto, recogida.Texto?.Trim(), StringComparison.Ordinal) ? null : texto;

            if (_dialogos != null && !_dialogos.ShowConfirmationAnswer("Confirmar la recogida", PreguntaConfirmar(recogida, bultos)))
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                RecogidaSolicitada confirmada = await _servicio.Confirmar(recogida.Id, bultosCorregidos, textoCorregido).ConfigureAwait(true);
                EstaOcupado = false;
                _dialogos?.ShowNotification("Recogida confirmada", TextoConfirmada(confirmada ?? recogida));
                Mensaje = $"Recogida {recogida.Id} confirmada" + (string.IsNullOrWhiteSpace(confirmada?.CodigoDocumento) ? "." : $": nota {confirmada.CodigoDocumento.Trim()}.");
            }
            catch (Exception ex)
            {
                EstaOcupado = false;
                if (TratarError(ex, "confirmar"))
                {
                    return;
                }
            }
            await RecargarTodoAsync().ConfigureAwait(true);
        }

        internal static string PreguntaConfirmar(RecogidaSolicitada recogida, short bultos)
            => $"Se creará la nota de recogida de {bultos} {(bultos == 1 ? "bulto" : "bultos")} de {recogida.NombreCliente?.Trim()} " +
               $"({recogida.ClienteContacto}).\n\nQuién la recoge: {recogida.QuienRecogeTexto}.\n\n¿La confirma?";

        /// <summary>Lo que se le dice al confirmar: el código de la nota y qué camino sigue según quién la recoge.</summary>
        internal static string TextoConfirmada(RecogidaSolicitada confirmada)
        {
            string codigo = string.IsNullOrWhiteSpace(confirmada?.CodigoDocumento) ? "de la nota" : confirmada.CodigoDocumento.Trim();
            string nota = string.IsNullOrWhiteSpace(confirmada?.CodigoDocumento)
                ? "Se ha creado la nota de recogida."
                : $"Se ha creado la nota de recogida {codigo}.";
            if (confirmada != null && confirmada.LaRecogeElVendedor)
            {
                return nota + $"\n\nLa recoge el vendedor: la nota no entra en el picking ni en el reparto. Él enseñará el código {codigo} " +
                    "en su móvil y la nota se cierra leyendo ese código al llegar a Algete.";
            }
            return nota + "\n\nLa recoge el reparto propio: seguirá el camino normal de las notas de recogida (picking, Facturar rutas y la " +
                $"parada del repartidor) y se cierra leyendo {codigo} al llegar a Algete.";
        }

        internal async Task RechazarAsync()
        {
            RecogidaSolicitada recogida = Seleccionada;
            if (recogida == null || _dialogos == null)
            {
                return;
            }
            Mensaje = null;
            string motivo = _dialogos.GetText("Rechazar la recogida",
                $"¿Por qué rechaza la recogida de {recogida.NombreCliente?.Trim()} ({recogida.ClienteContacto})? Se lo enseñamos a quien la pidió.");
            if (motivo == null)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(motivo))
            {
                Mensaje = MENSAJE_FALTA_MOTIVO;
                return;
            }
            EstaOcupado = true;
            try
            {
                _ = await _servicio.Rechazar(recogida.Id, motivo.Trim()).ConfigureAwait(true);
                Mensaje = $"Recogida {recogida.Id} rechazada.";
            }
            catch (Exception ex)
            {
                EstaOcupado = false;
                if (TratarError(ex, "rechazar"))
                {
                    return;
                }
            }
            EstaOcupado = false;
            await RecargarTodoAsync().ConfigureAwait(true);
        }

        internal async Task AnularAsync()
        {
            RecogidaSolicitada recogida = SeleccionadaDelCliente;
            if (recogida == null || _dialogos == null)
            {
                return;
            }
            Mensaje = null;
            string motivo = _dialogos.GetText("Anular la recogida",
                $"Va a anular la recogida {recogida.Id} de {recogida.NombreCliente?.Trim()} ({recogida.EstadoTexto}). " +
                "Si ya está en marcha, solo pueden Almacén y Administración. Motivo (opcional):");
            if (motivo == null)
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                RecogidaSolicitada anulada = await _servicio.Anular(recogida.Id, string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim()).ConfigureAwait(true);
                EstaOcupado = false;
                if (!string.IsNullOrWhiteSpace(anulada?.AvisoAnulacion))
                {
                    _dialogos.ShowNotification("Recogida anulada", $"La recogida {recogida.Id} queda anulada, pero hay que hacer algo a mano:\n\n{anulada.AvisoAnulacion.Trim()}");
                }
                Mensaje = $"Recogida {recogida.Id} anulada.";
            }
            catch (Exception ex)
            {
                EstaOcupado = false;
                if (TratarError(ex, "anular"))
                {
                    return;
                }
            }
            await RecargarTodoAsync().ConfigureAwait(true);
        }

        /// <summary>
        /// El error de una acción, tal cual lo dice la API. Devuelve true si ya no hay que hacer nada más (503: se queda sin servicio);
        /// con 404 y 409 (otro la ha resuelto o ya no existe) se vuelve a leer, como tras una acción que sale bien.
        /// </summary>
        private bool TratarError(Exception ex, string accion)
        {
            if (ex is RecogidaSolicitadaException api)
            {
                if (api.Codigo == 503)
                {
                    NoDisponible(api.Message);
                    return true;
                }
                Mensaje = api.Message;
                _dialogos?.ShowError(api.Message);
                return !(api.Codigo == 404 || api.Codigo == 409);
            }
            Mensaje = $"No se ha podido {accion} la recogida: {ex.Message}";
            _dialogos?.ShowError(Mensaje);
            return true;
        }

        private void NotificarComandos()
        {
            CargarCommand?.NotifyCanExecuteChanged();
            ConfirmarCommand?.NotifyCanExecuteChanged();
            RechazarCommand?.NotifyCanExecuteChanged();
            BuscarClienteCommand?.NotifyCanExecuteChanged();
            AnularCommand?.NotifyCanExecuteChanged();
        }
    }
}
