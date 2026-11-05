using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Nesto#523 (NestoAPI#623): «Origen» del cliente en la ficha: «Origen: Lead de Meta · campaña Harmonia (05/11/26, Paloma)».
    /// Dirección, Administración, Vendedores y Tiendas (e Informática) lo cambian con el desplegable y «Guardar origen» (PUT); el resto
    /// lo ve en texto. La garantía de verdad está en la API (403/400: se enseña su mensaje). Sin catálogo ni origen (API vieja) no se ve.
    /// </summary>
    public class OrigenClienteViewModel : ObservableObject
    {
        /// <summary>El grupo de seguridad de los vendedores (no está en Constantes.GruposSeguridad).</summary>
        public const string GRUPO_VENDEDORES = "Vendedores";

        private readonly IOrigenesClienteService _servicio;
        private readonly IServicioDialogos _dialogos;
        private readonly IConfiguracion _configuracion;
        private int _version;
        private bool _aplicando;
        private OrigenDeClienteModel _guardado;
        private List<OrigenClienteModel> _catalogo = new();

        public OrigenClienteViewModel(IOrigenesClienteService servicio, IServicioDialogos dialogos, IConfiguracion configuracion)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            _configuracion = configuracion;
            Opciones = new ObservableCollection<OrigenClienteModel> { new OrigenClienteModel() };
            _opcionSeleccionada = Opciones[0];
            GuardarCommand = new AsyncRelayCommand(GuardarAsync, () => PuedeGuardar);
        }

        public AsyncRelayCommand GuardarCommand { get; }

        /// <summary>«(ninguno)» y el catálogo (más el del cliente si ya no está activo).</summary>
        public ObservableCollection<OrigenClienteModel> Opciones { get; }

        public string Empresa { get; private set; }
        public string Cliente { get; private set; }
        public bool HayCliente => !string.IsNullOrWhiteSpace(Cliente);

        public bool PuedeCambiar => _configuracion != null && (_configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)
            || _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)
            || _configuracion.UsuarioEnGrupo(GRUPO_VENDEDORES)
            || _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)
            || _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA));

        public bool SoloLectura => !PuedeCambiar;

        private bool _disponible;
        /// <summary>Hay catálogo o el cliente tiene origen. Con la API sin los endpoints, false: el control no se ve.</summary>
        public bool Disponible { get => _disponible; private set => SetProperty(ref _disponible, value); }

        public bool TieneOrigen => !string.IsNullOrWhiteSpace(_guardado?.Origen);

        /// <summary>«Lead de Meta · campaña Harmonia (05/11/26, Paloma)» o «(ninguno)».</summary>
        public string TextoOrigen => TieneOrigen ? Texto(_guardado, _catalogo) : OrigenClienteTexto.NINGUNO;

        private OrigenClienteModel _opcionSeleccionada;
        public OrigenClienteModel OpcionSeleccionada
        {
            get => _opcionSeleccionada;
            set
            {
                // null llega de la vista al rehacer la lista: no es «quitarlo» («(ninguno)» es un elemento con Codigo null).
                if (value == null || !SetProperty(ref _opcionSeleccionada, value))
                {
                    return;
                }
                NotificarEstado();
            }
        }

        public bool Cambiado => !_aplicando && !string.Equals(Normalizar(OpcionSeleccionada?.Codigo), Normalizar(_guardado?.Origen), StringComparison.OrdinalIgnoreCase);

        public bool PuedeGuardar => PuedeCambiar && HayCliente && !EstaOcupado && Cambiado;

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

        /// <summary>Cambia el cliente de la ficha y lee el catálogo y su origen (la respuesta de uno anterior se descarta).</summary>
        public async Task CargarAsync(string empresa, string cliente)
        {
            Empresa = string.IsNullOrWhiteSpace(empresa) ? Constantes.Empresas.EMPRESA_DEFECTO : empresa.Trim();
            Cliente = cliente?.Trim();
            OnPropertyChanged(nameof(HayCliente));
            OnPropertyChanged(nameof(SePuedeElegir));
            int version = ++_version;
            Mensaje = null;
            Aplicar(null);
            if (!HayCliente || _servicio == null)
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                List<OrigenClienteModel> catalogo;
                try
                {
                    catalogo = await _servicio.LeerOrigenes() ?? new List<OrigenClienteModel>();
                }
                catch (Exception)
                {
                    catalogo = new List<OrigenClienteModel>(); // sin catálogo se sigue enseñando el origen si lo tiene
                }
                OrigenDeClienteModel leido = await _servicio.LeerOrigen(Empresa, Cliente);
                if (version == _version)
                {
                    _catalogo = catalogo.Where(o => o != null && !string.IsNullOrWhiteSpace(o.Codigo)).ToList();
                    Aplicar(leido);
                }
            }
            catch (Exception)
            {
                // Sin origen (API vieja o caída): la ficha sigue igual, sin error.
                if (version == _version)
                {
                    Aplicar(null);
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

        private void Aplicar(OrigenDeClienteModel origen)
        {
            _guardado = string.IsNullOrWhiteSpace(origen?.Origen) ? null : origen;
            _aplicando = true;
            try
            {
                RehacerOpciones();
                OpcionSeleccionada = Opciones.FirstOrDefault(o => o.Codigo != null
                    && string.Equals(o.Codigo.Trim(), _guardado?.Origen?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Opciones[0];
            }
            finally
            {
                _aplicando = false;
            }
            // Al rehacer la lista el desplegable pierde lo elegido aunque sea el mismo elemento: se avisa siempre.
            OnPropertyChanged(nameof(OpcionSeleccionada));
            Disponible = _catalogo.Count > 0 || TieneOrigen;
            OnPropertyChanged(nameof(TieneOrigen));
            OnPropertyChanged(nameof(TextoOrigen));
            NotificarEstado();
        }

        private void RehacerOpciones()
        {
            var nuevas = new List<OrigenClienteModel> { Opciones.FirstOrDefault(o => o.Codigo == null) ?? new OrigenClienteModel() };
            nuevas.AddRange(_catalogo);
            if (TieneOrigen && !nuevas.Any(o => o.Codigo != null && string.Equals(o.Codigo.Trim(), _guardado.Origen.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                nuevas.Add(new OrigenClienteModel { Codigo = _guardado.Origen.Trim(), Descripcion = _guardado.Descripcion });
            }
            if (nuevas.SequenceEqual(Opciones))
            {
                return;
            }
            Opciones.Clear();
            foreach (OrigenClienteModel o in nuevas)
            {
                Opciones.Add(o);
            }
        }

        public async Task GuardarAsync()
        {
            if (!HayCliente || _servicio == null || !Cambiado)
            {
                return;
            }
            if (!PuedeCambiar)
            {
                _dialogos?.ShowError(OrigenesClienteService.MENSAJE_SIN_PERMISO);
                Aplicar(_guardado);
                return;
            }
            int version = ++_version;
            OrigenDeClienteModel anterior = _guardado;
            OrigenClienteModel elegido = OpcionSeleccionada;
            string codigo = Normalizar(elegido?.Codigo);
            EstaOcupado = true;
            Mensaje = null;
            try
            {
                OrigenDeClienteModel respuesta = await _servicio.GuardarOrigen(Empresa, Cliente, codigo);
                if (version == _version)
                {
                    string guardado = Normalizar(respuesta?.Origen);
                    Aplicar(guardado == null ? null : new OrigenDeClienteModel
                    {
                        Origen = guardado,
                        Descripcion = string.IsNullOrWhiteSpace(respuesta?.Descripcion) ? elegido?.Descripcion : respuesta.Descripcion,
                        FechaOrigen = respuesta?.FechaOrigen ?? DateTime.Now,
                        UsuarioOrigen = string.IsNullOrWhiteSpace(respuesta?.UsuarioOrigen) ? _configuracion?.usuario : respuesta.UsuarioOrigen
                    });
                    Mensaje = guardado == null ? "Origen quitado." : "Origen guardado.";
                }
            }
            catch (Exception ex)
            {
                if (version == _version)
                {
                    Aplicar(anterior);
                }
                _dialogos?.ShowError($"No se ha podido guardar el origen del cliente: {ex.Message}");
            }
            finally
            {
                if (version == _version)
                {
                    EstaOcupado = false;
                }
            }
        }

        /// <summary>«Lead de Meta · campaña Harmonia (05/11/26, Paloma)». La campaña sale del catálogo.</summary>
        internal static string Texto(OrigenDeClienteModel origen, IEnumerable<OrigenClienteModel> catalogo)
        {
            if (string.IsNullOrWhiteSpace(origen?.Origen))
            {
                return OrigenClienteTexto.NINGUNO;
            }
            OrigenClienteModel delCatalogo = catalogo?.FirstOrDefault(o => o?.Codigo != null
                && string.Equals(o.Codigo.Trim(), origen.Origen.Trim(), StringComparison.OrdinalIgnoreCase));
            string descripcion = string.IsNullOrWhiteSpace(origen.Descripcion) ? delCatalogo?.Descripcion : origen.Descripcion;
            string texto = OrigenClienteTexto.DescripcionConCampana(descripcion, delCatalogo?.Campana, origen.Origen);
            var detalle = new List<string>();
            if (origen.FechaOrigen != null)
            {
                detalle.Add(origen.FechaOrigen.Value.ToString("dd/MM/yy", CultureInfo.GetCultureInfo("es-ES")));
            }
            string usuario = OrigenClienteTexto.UsuarioSinDominio(origen.UsuarioOrigen);
            if (usuario != null)
            {
                detalle.Add(usuario);
            }
            return detalle.Count == 0 ? texto : $"{texto} ({string.Join(", ", detalle)})";
        }

        private static string Normalizar(string codigo) => string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim();

        private void NotificarEstado()
        {
            OnPropertyChanged(nameof(Cambiado));
            OnPropertyChanged(nameof(PuedeGuardar));
            GuardarCommand?.NotifyCanExecuteChanged();
        }
    }
}
