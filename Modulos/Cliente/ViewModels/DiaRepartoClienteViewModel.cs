using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>Nesto#606: una opción del desplegable «Día de reparto propio» (Ruta null = por su código postal).</summary>
    public class OpcionDiaReparto
    {
        public OpcionDiaReparto(string ruta, string nombre)
        {
            Ruta = ruta;
            Nombre = nombre;
        }

        public string Ruta { get; }
        public string Nombre { get; }
        public override string ToString() => Nombre;
    }

    /// <summary>
    /// Nesto#606 (NestoAPI#799): «Día de reparto propio» del contacto que se ve en la ficha del cliente. Es una excepción al día que
    /// le toca por su código postal («Solo abre los miércoles»). Se guarda al elegir el día (el motivo cambiado, con su botón) y la
    /// API recalcula los pedidos pendientes. Solo Dirección, Almacén y Administración lo cambian; el resto lo ve en texto. Con la
    /// API vieja (404) el control se esconde sin error.
    /// </summary>
    public class DiaRepartoClienteViewModel : ObservableObject
    {
        public const string POR_CODIGO_POSTAL = "Por su código postal";

        public static readonly IReadOnlyList<OpcionDiaReparto> OPCIONES = new List<OpcionDiaReparto>
        {
            new(null, POR_CODIGO_POSTAL),
            new("01", "lunes"),
            new("02", "martes"),
            new("03", "miércoles"),
            new("04", "jueves"),
            new("05", "viernes")
        };

        private readonly IDiaRepartoClienteService _servicio;
        private readonly IServicioDialogos _dialogos;
        private readonly IConfiguracion _configuracion;
        private int _version;
        private bool _aplicando;
        private DiaRepartoClienteModel _guardado;

        public DiaRepartoClienteViewModel(IDiaRepartoClienteService servicio, IServicioDialogos dialogos, IConfiguracion configuracion)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            _configuracion = configuracion;
            _opcionSeleccionada = OPCIONES[0];
            GuardarMotivoCommand = new AsyncRelayCommand(GuardarMotivoAsync, () => PuedeGuardarMotivo);
        }

        public AsyncRelayCommand GuardarMotivoCommand { get; }

        public IReadOnlyList<OpcionDiaReparto> Opciones => OPCIONES;

        public string Empresa { get; private set; }
        public string Cliente { get; private set; }
        public string Contacto { get; private set; }
        public bool HayCliente => !string.IsNullOrWhiteSpace(Cliente);

        /// <summary>Dirección, Almacén y Administración. La garantía de verdad está en la API (403).</summary>
        public bool PuedeCambiar => _configuracion != null && (_configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)
            || _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)
            || _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION));

        public bool SoloLectura => !PuedeCambiar;

        private bool _disponible = true;
        /// <summary>False con la API vieja (404): el control se esconde.</summary>
        public bool Disponible { get => _disponible; private set => SetProperty(ref _disponible, value); }

        private OpcionDiaReparto _opcionSeleccionada;
        /// <summary>Al elegirla el usuario se guarda al momento (PUT).</summary>
        public OpcionDiaReparto OpcionSeleccionada
        {
            get => _opcionSeleccionada;
            set
            {
                OpcionDiaReparto nueva = value ?? OPCIONES[0];
                if (!SetProperty(ref _opcionSeleccionada, nueva))
                {
                    return;
                }
                NotificarEstado();
                if (!_aplicando)
                {
                    GuardadoEnCurso = GuardarAsync(nueva.Ruta);
                }
            }
        }

        /// <summary>El guardado lanzado al elegir en el desplegable (para esperarlo en las pruebas).</summary>
        internal Task GuardadoEnCurso { get; private set; } = Task.CompletedTask;

        private string _motivo;
        public string Motivo
        {
            get => _motivo;
            set
            {
                if (SetProperty(ref _motivo, value))
                {
                    NotificarEstado();
                }
            }
        }

        public bool TieneExcepcion => !string.IsNullOrWhiteSpace(_guardado?.Ruta);

        /// <summary>Lo que ve quien no puede cambiarlo: «miércoles (Solo abre los miércoles)» o «Por su código postal».</summary>
        public string TextoDia => TieneExcepcion
            ? NombreDia(_guardado.Ruta, _guardado.Dia) + (string.IsNullOrWhiteSpace(_guardado.Motivo) ? string.Empty : $" ({_guardado.Motivo.Trim()})")
            : POR_CODIGO_POSTAL;

        public string Detalle => !TieneExcepcion || _guardado.FechaModificacion == null
            ? null
            : $"Puesto por {_guardado.Usuario?.Trim()} el {_guardado.FechaModificacion.Value.ToString("g", CultureInfo.GetCultureInfo("es-ES"))}";

        public bool MotivoCambiado => !string.Equals((Motivo ?? string.Empty).Trim(), (_guardado?.Motivo ?? string.Empty).Trim(), StringComparison.Ordinal);

        public bool PuedeGuardarMotivo => PuedeCambiar && HayCliente && !EstaOcupado && TieneExcepcion
            && OpcionSeleccionada?.Ruta == _guardado?.Ruta && MotivoCambiado;

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    OnPropertyChanged(nameof(SePuedeElegir));
                    NotificarEstado();
                }
            }
        }

        public bool SePuedeElegir => PuedeCambiar && HayCliente && !EstaOcupado;

        private string _mensaje;
        public string Mensaje
        {
            get => _mensaje;
            private set
            {
                if (SetProperty(ref _mensaje, value))
                {
                    OnPropertyChanged(nameof(HayMensaje));
                }
            }
        }

        public bool HayMensaje => !string.IsNullOrWhiteSpace(Mensaje);

        /// <summary>Cambia el contacto de la ficha y lee su día de reparto propio (la respuesta de uno anterior se descarta).</summary>
        public async Task CargarAsync(string empresa, string cliente, string contacto)
        {
            Empresa = string.IsNullOrWhiteSpace(empresa) ? Constantes.Empresas.EMPRESA_DEFECTO : empresa.Trim();
            Cliente = cliente?.Trim();
            Contacto = string.IsNullOrWhiteSpace(contacto) ? "0" : contacto.Trim();
            OnPropertyChanged(nameof(HayCliente));
            OnPropertyChanged(nameof(SePuedeElegir));
            int version = ++_version;
            Mensaje = null;
            Aplicar(null);
            if (!HayCliente || _servicio == null || !Disponible)
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                DiaRepartoClienteModel leido = await _servicio.Leer(Empresa, Cliente, Contacto);
                if (version == _version)
                {
                    Aplicar(leido);
                }
            }
            catch (DiaRepartoNoDisponibleException)
            {
                Disponible = false;
            }
            catch (Exception ex)
            {
                if (version == _version)
                {
                    Mensaje = ex.Message;
                }
            }
            finally
            {
                if (version == _version)
                {
                    EstaOcupado = false;
                }
            }
        }

        private void Aplicar(DiaRepartoClienteModel dia)
        {
            _guardado = dia;
            _aplicando = true;
            try
            {
                OpcionSeleccionada = OPCIONES.FirstOrDefault(o => o.Ruta != null && o.Ruta == dia?.Ruta?.Trim()) ?? OPCIONES[0];
                Motivo = dia?.Motivo?.Trim();
            }
            finally
            {
                _aplicando = false;
            }
            OnPropertyChanged(nameof(TieneExcepcion));
            OnPropertyChanged(nameof(TextoDia));
            OnPropertyChanged(nameof(Detalle));
            NotificarEstado();
        }

        public Task GuardarMotivoAsync() => GuardarAsync(OpcionSeleccionada?.Ruta);

        private async Task GuardarAsync(string ruta)
        {
            if (!HayCliente || _servicio == null)
            {
                return;
            }
            if (!PuedeCambiar)
            {
                _dialogos?.ShowError(DiaRepartoClienteService.MENSAJE_SIN_PERMISO);
                Aplicar(_guardado);
                return;
            }
            int version = ++_version;
            DiaRepartoClienteModel anterior = _guardado;
            string motivo = string.IsNullOrWhiteSpace(ruta) ? null : Motivo?.Trim();
            EstaOcupado = true;
            Mensaje = null;
            try
            {
                DiaRepartoGuardadoModel respuesta = await _servicio.Guardar(Empresa, Cliente, Contacto, ruta, motivo);
                if (version == _version)
                {
                    string rutaGuardada = string.IsNullOrWhiteSpace(respuesta?.Ruta) ? null : respuesta.Ruta.Trim();
                    Aplicar(rutaGuardada == null ? null : new DiaRepartoClienteModel
                    {
                        Empresa = Empresa,
                        Cliente = Cliente,
                        Contacto = Contacto,
                        Ruta = rutaGuardada,
                        Dia = respuesta.Dia,
                        Motivo = motivo,
                        Usuario = _configuracion?.usuario,
                        FechaModificacion = DateTime.Now
                    });
                    int n = respuesta?.PedidosRecalculados ?? 0;
                    Mensaje = $"Día de reparto propio {(rutaGuardada == null ? "quitado" : "guardado")}. {TextoPedidos(n)}";
                }
            }
            catch (DiaRepartoNoDisponibleException)
            {
                Disponible = false;
            }
            catch (Exception ex)
            {
                if (version == _version)
                {
                    Aplicar(anterior);
                }
                _dialogos?.ShowError($"No se ha podido guardar el día de reparto propio: {ex.Message}");
            }
            finally
            {
                if (version == _version)
                {
                    EstaOcupado = false;
                }
            }
        }

        internal static string TextoPedidos(int n) => n == 1
            ? "Se ha recalculado 1 pedido pendiente."
            : $"Se han recalculado {n} pedidos pendientes.";

        private static string NombreDia(string ruta, string dia)
            => !string.IsNullOrWhiteSpace(dia) ? dia.Trim() : OPCIONES.FirstOrDefault(o => o.Ruta == ruta?.Trim())?.Nombre ?? ruta;

        private void NotificarEstado()
        {
            OnPropertyChanged(nameof(MotivoCambiado));
            OnPropertyChanged(nameof(PuedeGuardarMotivo));
            GuardarMotivoCommand?.NotifyCanExecuteChanged();
        }
    }
}
