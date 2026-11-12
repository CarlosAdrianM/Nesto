using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto.ViewModels
{
    /// <summary>
    /// Nesto#636 (API en NestoAPI#857, fase 3): la ventana «Envíos de proveedor por asignar». La API pone sola la fecha de recepción del
    /// pedido de compra cuando el correo de seguimiento de una agencia es de un único pedido; cuando no puede decidir (ningún candidato o
    /// varios), el envío queda por asignar y aquí Compras:
    /// <list type="bullet">
    /// <item>ve los envíos (GET …/PorAsignar) con agencia, códigos, remitente del paquete, proveedor propuesto, entrega estimada, la fecha
    /// de recepción que se pondría y sus correos;</item>
    /// <item>elige el pedido entre los candidatos (el Sugerido, preseleccionado) o, plegados debajo, el resto de pedidos en el aire, y lo
    /// asigna (con «Recordar este remitente para este proveedor» si el envío trae remitente del paquete); 409 = ya estaba aplicado a otro:
    /// se pregunta si reasignar;</item>
    /// <item>o lo descarta («No es una compra», con confirmación);</item>
    /// <item>en «Remitentes recordados» ve y borra la memoria de remitentes del paquete.</item>
    /// </list>
    /// Sin temporizadores: se lee al llegar, tras cada acción y con «Actualizar». El Mensaje de la API se enseña tal cual; con 503 (falta
    /// el script en el servidor) se avisa y todo queda vacío.
    /// </summary>
    public class EnviosProveedorPorAsignarViewModel : ObservableObject, IReceptorNavegacion
    {
        public const int PESTANA_POR_ASIGNAR = 0;
        public const int PESTANA_REMITENTES = 1;
        public const int DIAS_POR_DEFECTO = 7;
        public const string MENSAJE_SIN_PEDIDO = "Elija el pedido de compra al que corresponde el envío.";

        private readonly IServicioEnviosProveedorPorAsignar _servicio;
        private readonly IServicioDialogos _dialogos;
        private int? _correoAElegir;

        public EnviosProveedorPorAsignarViewModel(IServicioEnviosProveedorPorAsignar servicio, IServicioDialogos dialogos)
        {
            _servicio = servicio;
            _dialogos = dialogos;

            CargarCommand = new AsyncRelayCommand(RecargarTodoAsync, () => !EstaOcupado);
            AsignarCommand = new AsyncRelayCommand(AsignarAsync, () => Disponible && !EstaOcupado && EnvioSeleccionado != null && PedidoElegido != null);
            DescartarCommand = new AsyncRelayCommand(DescartarAsync, () => Disponible && !EstaOcupado && EnvioSeleccionado != null);
            BorrarRemitenteCommand = new AsyncRelayCommand(BorrarRemitenteAsync, () => Disponible && !EstaOcupado && RemitenteSeleccionado != null);
        }

        public string Titulo => "Envíos de proveedor por asignar";
        public string Ayuda => "Los avisos de las agencias que no se han podido asignar solos a un pedido de compra (ningún pedido candidato, o " +
            "varios). Elija el pedido y pulse «Asignar a este pedido»: se pone la fecha de recepción en sus líneas pendientes, también para " +
            "los demás correos del mismo envío. Si el aviso no es de ninguna compra, pulse «No es una compra».";

        public IAsyncRelayCommand CargarCommand { get; }
        public IAsyncRelayCommand AsignarCommand { get; }
        public IAsyncRelayCommand DescartarCommand { get; }
        public IAsyncRelayCommand BorrarRemitenteCommand { get; }

        /// <summary>Los días hacia atrás que se piden a la API.</summary>
        public IReadOnlyList<int> DiasPosibles { get; } = new[] { 7, 15, 30, 60 };

        private int _dias = DIAS_POR_DEFECTO;
        public int Dias
        {
            get => _dias;
            set
            {
                if (SetProperty(ref _dias, value) && !EstaOcupado)
                {
                    _ = CargarEnviosAsync();
                }
            }
        }

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
        /// <summary>El aviso fijo de arriba: sin script (503) o sin permiso (403).</summary>
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
        /// <summary>Lo último que ha pasado (el Mensaje de la API tal cual tras asignar o descartar).</summary>
        public string Mensaje { get => _mensaje; private set => SetProperty(ref _mensaje, value); }

        private int _pestanaSeleccionada = PESTANA_POR_ASIGNAR;
        public int PestanaSeleccionada { get => _pestanaSeleccionada; set => SetProperty(ref _pestanaSeleccionada, value); }

        // ------------------------------------------------------------------ envíos

        public ObservableCollection<EnvioProveedorPorAsignar> Envios { get; } = new ObservableCollection<EnvioProveedorPorAsignar>();

        /// <summary>Todos los pedidos de compra en el aire (de ahí sale «el resto» de cada envío).</summary>
        private List<PedidoCompraPorAsignar> _pedidosEnAire = new List<PedidoCompraPorAsignar>();

        public string TextoEnvios => Envios.Count switch
        {
            0 => "No hay envíos por asignar.",
            1 => "1 envío por asignar.",
            _ => $"{Envios.Count} envíos por asignar."
        };

        private EnvioProveedorPorAsignar _envioSeleccionado;
        public EnvioProveedorPorAsignar EnvioSeleccionado
        {
            get => _envioSeleccionado;
            set
            {
                if (SetProperty(ref _envioSeleccionado, value))
                {
                    PrepararEleccion(value);
                    OnPropertyChanged(nameof(HayEnvioSeleccionado));
                    OnPropertyChanged(nameof(HayRemitentePaquete));
                    NotificarComandos();
                }
            }
        }

        public bool HayEnvioSeleccionado => EnvioSeleccionado != null;

        /// <summary>La casilla «Recordar este remitente…» solo tiene sentido si el envío trae quién expide el paquete.</summary>
        public bool HayRemitentePaquete => EnvioSeleccionado?.HayRemitentePaquete == true;

        private bool _recordarRemitente = true;
        public bool RecordarRemitente { get => _recordarRemitente; set => SetProperty(ref _recordarRemitente, value); }

        /// <summary>Los candidatos del envío elegido (el Sugerido, preseleccionado).</summary>
        public ObservableCollection<PedidoCompraPorAsignar> Candidatos { get; } = new ObservableCollection<PedidoCompraPorAsignar>();

        /// <summary>Los demás pedidos en el aire (no candidatos de este envío), por si no es ninguno de los candidatos.</summary>
        public ObservableCollection<PedidoCompraPorAsignar> OtrosPedidos { get; } = new ObservableCollection<PedidoCompraPorAsignar>();

        public bool HayCandidatos => Candidatos.Count > 0;

        public string TextoCandidatos => Candidatos.Count == 0
            ? "No hay pedidos candidatos para este envío: búsquelo en «Otros pedidos en el aire»."
            : "Pedidos candidatos (el sugerido sale ya elegido):";

        private bool _otrosDesplegados;
        /// <summary>«Otros pedidos en el aire»: plegado salvo que el envío no tenga candidatos.</summary>
        public bool OtrosDesplegados { get => _otrosDesplegados; set => SetProperty(ref _otrosDesplegados, value); }

        private PedidoCompraPorAsignar _candidatoSeleccionado;
        public PedidoCompraPorAsignar CandidatoSeleccionado
        {
            get => _candidatoSeleccionado;
            set
            {
                if (SetProperty(ref _candidatoSeleccionado, value) && value != null)
                {
                    OtroPedidoSeleccionado = null;
                }
                OnPropertyChanged(nameof(PedidoElegido));
                OnPropertyChanged(nameof(TextoPedidoElegido));
                NotificarComandos();
            }
        }

        private PedidoCompraPorAsignar _otroPedidoSeleccionado;
        public PedidoCompraPorAsignar OtroPedidoSeleccionado
        {
            get => _otroPedidoSeleccionado;
            set
            {
                if (SetProperty(ref _otroPedidoSeleccionado, value) && value != null)
                {
                    CandidatoSeleccionado = null;
                }
                OnPropertyChanged(nameof(PedidoElegido));
                OnPropertyChanged(nameof(TextoPedidoElegido));
                NotificarComandos();
            }
        }

        /// <summary>El pedido al que se asignará: el candidato elegido o, si no, el de los otros pedidos en el aire.</summary>
        public PedidoCompraPorAsignar PedidoElegido => CandidatoSeleccionado ?? OtroPedidoSeleccionado;

        public string TextoPedidoElegido => PedidoElegido == null
            ? MENSAJE_SIN_PEDIDO
            : $"Se asignará al pedido {PedidoElegido.Pedido} de {PedidoElegido.ProveedorTexto}" +
              (PedidoElegido.EsResto ? $" ({PedidoCompraPorAsignar.RESTO})." : ".");

        private void PrepararEleccion(EnvioProveedorPorAsignar envio)
        {
            Candidatos.Clear();
            OtrosPedidos.Clear();
            _candidatoSeleccionado = null;
            _otroPedidoSeleccionado = null;
            if (envio != null)
            {
                foreach (PedidoCompraPorAsignar candidato in envio.Candidatos ?? new List<PedidoCompraPorAsignar>())
                {
                    Candidatos.Add(candidato);
                }
                foreach (PedidoCompraPorAsignar pedido in _pedidosEnAire.Where(p => !Candidatos.Any(c => c.EsElMismo(p))))
                {
                    OtrosPedidos.Add(pedido);
                }
            }
            RecordarRemitente = true;
            OtrosDesplegados = envio != null && Candidatos.Count == 0;
            OnPropertyChanged(nameof(CandidatoSeleccionado));
            OnPropertyChanged(nameof(OtroPedidoSeleccionado));
            CandidatoSeleccionado = Candidatos.FirstOrDefault(c => c.Sugerido);
            OnPropertyChanged(nameof(HayCandidatos));
            OnPropertyChanged(nameof(TextoCandidatos));
        }

        // ------------------------------------------------------------------ remitentes recordados

        public ObservableCollection<RemitentePaqueteRecordado> Remitentes { get; } = new ObservableCollection<RemitentePaqueteRecordado>();

        private RemitentePaqueteRecordado _remitenteSeleccionado;
        public RemitentePaqueteRecordado RemitenteSeleccionado
        {
            get => _remitenteSeleccionado;
            set
            {
                if (SetProperty(ref _remitenteSeleccionado, value))
                {
                    NotificarComandos();
                }
            }
        }

        private string _textoRemitentes;
        public string TextoRemitentes { get => _textoRemitentes; private set => SetProperty(ref _textoRemitentes, value); }

        // ------------------------------------------------------------------ llegar y leer

        public void AlLlegar(ParametrosNavegacion parametros)
        {
            _correoAElegir = LeerEntero(parametros, NotificacionBuzon.PARAMETRO_CORREO_COMPRA);
            if (_correoAElegir.HasValue)
            {
                PestanaSeleccionada = PESTANA_POR_ASIGNAR;
            }
            LlegadaEnCurso = RecargarTodoAsync();
        }

        /// <summary>Lo que se está leyendo al llegar (los tests lo esperan).</summary>
        internal Task LlegadaEnCurso { get; private set; } = Task.CompletedTask;

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

        /// <summary>«Actualizar»: los envíos y los remitentes recordados.</summary>
        internal async Task RecargarTodoAsync()
        {
            await CargarEnviosAsync().ConfigureAwait(true);
            if (Disponible)
            {
                await CargarRemitentesAsync().ConfigureAwait(true);
            }
        }

        internal async Task CargarEnviosAsync()
        {
            EstaOcupado = true;
            try
            {
                EnviosProveedorPorAsignar leido = await _servicio.LeerPorAsignar(Dias).ConfigureAwait(true) ?? new EnviosProveedorPorAsignar();
                Disponible = true;
                Aviso = null;
                int? correo = _correoAElegir;
                _correoAElegir = null;
                int? clave = EnvioSeleccionado?.Clave;
                _pedidosEnAire = leido.PedidosEnAire ?? new List<PedidoCompraPorAsignar>();
                Envios.Clear();
                foreach (EnvioProveedorPorAsignar envio in leido.Envios ?? new List<EnvioProveedorPorAsignar>())
                {
                    Envios.Add(envio);
                }
                // La rejilla puede haber dejado la selección a null al vaciarse: se fuerza a preparar la elección otra vez
                _envioSeleccionado = null;
                EnvioProveedorPorAsignar elegido = null;
                if (correo.HasValue)
                {
                    elegido = Envios.FirstOrDefault(e => e.Contiene(correo.Value));
                    if (elegido == null)
                    {
                        Mensaje = $"El correo {correo.Value} ya no está por asignar: lo habrá asignado o descartado otra persona " +
                            $"(o es de hace más de {Dias} días).";
                    }
                }
                EnvioSeleccionado = elegido
                    ?? Envios.FirstOrDefault(e => e.Clave == clave)
                    ?? Envios.FirstOrDefault();
                if (EnvioSeleccionado == null)
                {
                    PrepararEleccion(null);
                    OnPropertyChanged(nameof(EnvioSeleccionado));
                    OnPropertyChanged(nameof(HayEnvioSeleccionado));
                    OnPropertyChanged(nameof(HayRemitentePaquete));
                }
            }
            catch (EnvioProveedorPorAsignarException ex) when (ex.Codigo == 503)
            {
                NoDisponible(ex.Message);
            }
            catch (EnvioProveedorPorAsignarException ex) when (ex.Codigo == 403)
            {
                Vaciar();
                Aviso = ex.Message;
            }
            catch (Exception ex)
            {
                Mensaje = "No se han podido leer los envíos por asignar: " + ex.Message;
            }
            finally
            {
                EstaOcupado = false;
                OnPropertyChanged(nameof(TextoEnvios));
            }
        }

        internal async Task CargarRemitentesAsync()
        {
            EstaOcupado = true;
            try
            {
                List<RemitentePaqueteRecordado> leidos = await _servicio.LeerRemitentesPaquete().ConfigureAwait(true)
                    ?? new List<RemitentePaqueteRecordado>();
                int? id = RemitenteSeleccionado?.Id;
                Remitentes.Clear();
                foreach (RemitentePaqueteRecordado remitente in leidos.OrderBy(r => r.RemitentePaquete))
                {
                    Remitentes.Add(remitente);
                }
                RemitenteSeleccionado = Remitentes.FirstOrDefault(r => r.Id == id);
                TextoRemitentes = Remitentes.Count switch
                {
                    0 => "No hay remitentes recordados.",
                    1 => "1 remitente recordado.",
                    _ => $"{Remitentes.Count} remitentes recordados."
                };
            }
            catch (EnvioProveedorPorAsignarException ex) when (ex.Codigo == 503)
            {
                NoDisponible(ex.Message);
            }
            catch (Exception ex)
            {
                Remitentes.Clear();
                RemitenteSeleccionado = null;
                TextoRemitentes = "No se han podido leer los remitentes recordados: " + ex.Message;
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
            Vaciar();
            Remitentes.Clear();
            RemitenteSeleccionado = null;
            TextoRemitentes = null;
        }

        private void Vaciar()
        {
            _pedidosEnAire = new List<PedidoCompraPorAsignar>();
            Envios.Clear();
            EnvioSeleccionado = null;
            PrepararEleccion(null);
            OnPropertyChanged(nameof(TextoEnvios));
        }

        // ------------------------------------------------------------------ asignar y descartar

        internal async Task AsignarAsync()
        {
            EnvioProveedorPorAsignar envio = EnvioSeleccionado;
            PedidoCompraPorAsignar pedido = PedidoElegido;
            if (envio == null)
            {
                return;
            }
            if (pedido == null)
            {
                Mensaje = MENSAJE_SIN_PEDIDO;
                return;
            }
            Mensaje = null;
            bool recordar = envio.HayRemitentePaquete && RecordarRemitente;
            EstaOcupado = true;
            try
            {
                RespuestaAsignacionEnvioProveedor respuesta;
                try
                {
                    respuesta = await _servicio.Asignar(envio.Clave, pedido.EmpresaPedido, pedido.Pedido, recordar, false).ConfigureAwait(true);
                }
                catch (EnvioProveedorPorAsignarException ex) when (ex.Codigo == 409)
                {
                    // Ya estaba aplicado a otro pedido: se pregunta (No por defecto) y, si dice que sí, se repite reasignando
                    if (_dialogos == null || !_dialogos.ShowConfirmationAnswer("Reasignar el envío", PreguntaReasignar(ex.Message, pedido), true))
                    {
                        EstaOcupado = false;
                        Mensaje = ex.Message + " No se ha cambiado nada.";
                        await RecargarTodoAsync().ConfigureAwait(true);
                        return;
                    }
                    respuesta = await _servicio.Asignar(envio.Clave, pedido.EmpresaPedido, pedido.Pedido, recordar, true).ConfigureAwait(true);
                }
                EstaOcupado = false;
                Mensaje = string.IsNullOrWhiteSpace(respuesta?.Mensaje)
                    ? $"Envío asignado al pedido {pedido.Pedido}."
                    : respuesta.Mensaje.Trim();
            }
            catch (Exception ex)
            {
                EstaOcupado = false;
                if (TratarError(ex, "asignar"))
                {
                    return;
                }
            }
            await RecargarTodoAsync().ConfigureAwait(true);
        }

        internal static string PreguntaReasignar(string motivo, PedidoCompraPorAsignar pedido)
            => $"{motivo?.Trim()}\n\n¿Lo asigna de todas formas al pedido {pedido?.Pedido}? La fecha que ya se puso en el otro pedido no se deshace: " +
               "si estaba mal, corríjala allí a mano.";

        internal async Task DescartarAsync()
        {
            EnvioProveedorPorAsignar envio = EnvioSeleccionado;
            if (envio == null)
            {
                return;
            }
            Mensaje = null;
            if (_dialogos != null && !_dialogos.ShowConfirmationAnswer("No es una compra", PreguntaDescartar(envio), true))
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                RespuestaDescarteEnvioProveedor respuesta = await _servicio.Descartar(envio.Clave).ConfigureAwait(true);
                EstaOcupado = false;
                Mensaje = string.IsNullOrWhiteSpace(respuesta?.Mensaje) ? "Envío descartado." : respuesta.Mensaje.Trim();
            }
            catch (Exception ex)
            {
                EstaOcupado = false;
                if (TratarError(ex, "descartar"))
                {
                    return;
                }
            }
            await RecargarTodoAsync().ConfigureAwait(true);
        }

        internal static string PreguntaDescartar(EnvioProveedorPorAsignar envio)
        {
            int correos = envio?.Correos?.Count ?? 0;
            string cuales = correos > 1 ? $"sus {correos} correos" : "su correo";
            string codigos = string.IsNullOrWhiteSpace(envio?.CodigosTexto) ? string.Empty : $" ({envio.CodigosTexto})";
            return $"El envío de {envio?.AgenciaTexto}{codigos} no es de ninguna compra: se descartan {cuales} y deja de salir aquí. " +
                "No se toca ningún pedido.\n\n¿Lo descarta?";
        }

        internal async Task BorrarRemitenteAsync()
        {
            RemitentePaqueteRecordado remitente = RemitenteSeleccionado;
            if (remitente == null)
            {
                return;
            }
            Mensaje = null;
            if (_dialogos != null && !_dialogos.ShowConfirmationAnswer("Olvidar el remitente",
                $"Se olvidará que «{remitente.RemitentePaquete?.Trim()}» envía por el proveedor {remitente.Proveedor?.Trim()}. " +
                "Los próximos avisos suyos ya no se asignarán por él.\n\n¿Lo borra?", true))
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                await _servicio.BorrarRemitentePaquete(remitente.Id).ConfigureAwait(true);
                EstaOcupado = false;
                Mensaje = $"Remitente «{remitente.RemitentePaquete?.Trim()}» olvidado.";
            }
            catch (Exception ex)
            {
                EstaOcupado = false;
                if (TratarError(ex, "borrar"))
                {
                    return;
                }
            }
            await CargarRemitentesAsync().ConfigureAwait(true);
        }

        /// <summary>
        /// El error de una acción, tal cual lo dice la API. Devuelve true si ya no hay que hacer nada más (503: se queda sin servicio;
        /// 400 y 403: no ha cambiado nada); con 404 y 409 (otro lo ha resuelto o ya no existe) se vuelve a leer.
        /// </summary>
        private bool TratarError(Exception ex, string accion)
        {
            if (ex is EnvioProveedorPorAsignarException api)
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
            Mensaje = $"No se ha podido {accion}: {ex.Message}";
            _dialogos?.ShowError(Mensaje);
            return true;
        }

        private void NotificarComandos()
        {
            CargarCommand?.NotifyCanExecuteChanged();
            AsignarCommand?.NotifyCanExecuteChanged();
            DescartarCommand?.NotifyCanExecuteChanged();
            BorrarRemitenteCommand?.NotifyCanExecuteChanged();
        }
    }
}
