using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlesUsuario.Services;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto.ViewModels
{
    /// <summary>
    /// Sugerencia 1277 (Alfredo, Carlos 20/11/26): la ventana «Envíos internos» entre sedes (material sin stock para las tiendas, aparatos
    /// de demostración o de clientes). Arriba el alta (origen, destino, qué va y nº de bultos; «Crear e imprimir etiquetas»), debajo los
    /// vivos (o también los terminados) con dónde está cada bulto, «Reimprimir etiquetas» y «Anular» (con motivo). Solo Almacén,
    /// Dirección e Informática (la API también lo comprueba con 403). Se lee al abrir y con «Actualizar» (sin sondeo). Con un servidor
    /// que aún no lo tiene se dice y la ventana no rompe.
    /// </summary>
    public class EnviosInternosViewModel : ObservableObject, IReceptorNavegacion
    {
        public const int DIAS = 7;
        public const int BULTOS_MAXIMOS = 50;
        public const string MENSAJE_SIN_GRUPO = "Los envíos internos son cosa de Almacén, Dirección e Informática.";
        public const string MENSAJE_FALTA_ORIGEN = "Elija la sede de origen.";
        public const string MENSAJE_FALTA_DESTINO = "Elija la sede de destino.";
        public const string MENSAJE_MISMA_SEDE = "El origen y el destino tienen que ser sedes distintas.";
        public const string MENSAJE_FALTA_DESCRIPCION = "Escriba qué va en el envío (por ejemplo, «papel higiénico y café» o el aparato).";
        public static readonly string MENSAJE_BULTOS = $"Los bultos tienen que ser un número de 1 a {BULTOS_MAXIMOS}.";
        public const string MENSAJE_FALTA_MOTIVO = "Indique el motivo de la anulación. No se ha anulado nada.";

        private readonly IServicioEnviosInternos _servicio;
        private readonly IServicioDialogos _dialogos;

        public EnviosInternosViewModel(IServicioEnviosInternos servicio, IConfiguracion configuracion, IServicioDialogos dialogos)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            PuedeUsar = PuedeUsarEnviosInternos(configuracion);
            Aviso = PuedeUsar ? null : MENSAJE_SIN_GRUPO;
            CargarCommand = new AsyncRelayCommand(CargarAsync, () => PuedeUsar && !EstaOcupado);
            CrearCommand = new AsyncRelayCommand(CrearAsync, () => PuedeActuar);
            ReimprimirCommand = new AsyncRelayCommand(ReimprimirAsync, () => PuedeActuar && Seleccionado?.EstaVivo == true);
            AnularCommand = new AsyncRelayCommand(AnularAsync, () => PuedeActuar && Seleccionado?.EstaVivo == true);
        }

        /// <summary>Almacén, Dirección e Informática (la cinta y Agencias lo miran igual).</summary>
        public static bool PuedeUsarEnviosInternos(IConfiguracion configuracion)
            => configuracion != null
               && (configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)
                   || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)
                   || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA));

        public string Titulo => "Envíos internos";
        public string Ayuda => "Lo que se manda de una sede a otra que no es una reposición: material sin stock para las tiendas (papel, café, " +
            "folios…) o aparatos de demostración o de clientes. Cada bulto lleva su etiqueta EI y el repartidor la lee al cargarla y al entregarla.";

        public bool PuedeUsar { get; }

        public IAsyncRelayCommand CargarCommand { get; }
        public IAsyncRelayCommand CrearCommand { get; }
        public IAsyncRelayCommand ReimprimirCommand { get; }
        public IAsyncRelayCommand AnularCommand { get; }

        public IReadOnlyList<SedeEnvioInterno> Sedes { get; } = SedesEnvioInterno.Codigos
            .Select(c => new SedeEnvioInterno(c, SedesEnvioInterno.Nombre(c))).ToList();

        // ------------------------------------------------------------------ alta

        private string _origen;
        public string Origen { get => _origen; set => SetProperty(ref _origen, value); }

        private string _destino;
        public string Destino { get => _destino; set => SetProperty(ref _destino, value); }

        private string _descripcion;
        public string Descripcion { get => _descripcion; set => SetProperty(ref _descripcion, value); }

        private string _bultosTexto = "1";
        public string BultosTexto { get => _bultosTexto; set => SetProperty(ref _bultosTexto, value); }

        /// <summary>PURA. El motivo por el que el alta no vale, o null si vale.</summary>
        internal static string MotivoAltaNoValida(string origen, string destino, string descripcion, string bultosTexto, out int bultos)
        {
            bultos = 0;
            if (string.IsNullOrWhiteSpace(origen) || !SedesEnvioInterno.Codigos.Contains(origen.Trim().ToUpperInvariant()))
            {
                return MENSAJE_FALTA_ORIGEN;
            }
            if (string.IsNullOrWhiteSpace(destino) || !SedesEnvioInterno.Codigos.Contains(destino.Trim().ToUpperInvariant()))
            {
                return MENSAJE_FALTA_DESTINO;
            }
            if (string.Equals(origen.Trim(), destino.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return MENSAJE_MISMA_SEDE;
            }
            if (string.IsNullOrWhiteSpace(descripcion))
            {
                return MENSAJE_FALTA_DESCRIPCION;
            }
            if (!int.TryParse(bultosTexto?.Trim(), out bultos) || bultos < 1 || bultos > BULTOS_MAXIMOS)
            {
                bultos = 0;
                return MENSAJE_BULTOS;
            }
            return null;
        }

        // ------------------------------------------------------------------ estado y lista

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

        private bool _disponible = true;
        /// <summary>False si el servidor aún no tiene los envíos internos: nada se puede hacer.</summary>
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

        private string _aviso;
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

        private bool _verTerminados;
        /// <summary>«Ver también los terminados» (entregados y anulados de los últimos días). Al cambiarla se vuelve a leer.</summary>
        public bool VerTerminados
        {
            get => _verTerminados;
            set
            {
                if (SetProperty(ref _verTerminados, value) && PuedeUsar)
                {
                    CargaEnCurso = CargarAsync();
                }
            }
        }

        /// <summary>Lo que se está leyendo por la casilla (los tests lo esperan).</summary>
        internal Task CargaEnCurso { get; private set; } = Task.CompletedTask;

        public ObservableCollection<EnvioInterno> Lista { get; } = new ObservableCollection<EnvioInterno>();

        private EnvioInterno _seleccionado;
        public EnvioInterno Seleccionado
        {
            get => _seleccionado;
            set
            {
                if (SetProperty(ref _seleccionado, value))
                {
                    NotificarComandos();
                }
            }
        }

        public string TextoLista => Lista.Count switch
        {
            0 => VerTerminados ? "No hay envíos internos en los últimos días." : "No hay envíos internos en marcha.",
            1 => "1 envío interno.",
            _ => $"{Lista.Count} envíos internos."
        };

        private bool PuedeActuar => PuedeUsar && Disponible && !EstaOcupado && _servicio != null;

        // ------------------------------------------------------------------ llegar y leer

        public void AlLlegar(ParametrosNavegacion parametros)
        {
            if (PuedeUsar)
            {
                CargaEnCurso = CargarAsync();
            }
        }

        public async Task CargarAsync()
        {
            if (!PuedeUsar || _servicio == null)
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                List<EnvioInterno> leidos = await _servicio.Leer(DIAS, !VerTerminados).ConfigureAwait(true) ?? new List<EnvioInterno>();
                int? elegir = Seleccionado?.Id;
                Lista.Clear();
                foreach (EnvioInterno envio in leidos.Where(e => e != null))
                {
                    Lista.Add(envio);
                }
                Seleccionado = Lista.FirstOrDefault(e => e.Id == elegir) ?? Lista.FirstOrDefault();
                Disponible = true;
                Aviso = null;
            }
            catch (ApiRepartoException ex) when (ex.ServidorSinActualizar || ex.CodigoHttp == 403)
            {
                Disponible = !ex.ServidorSinActualizar;
                Aviso = ex.Message;
                Lista.Clear();
                Seleccionado = null;
            }
            catch (Exception ex)
            {
                Mensaje = "No se han podido leer los envíos internos: " + ex.Message;
            }
            finally
            {
                EstaOcupado = false;
                OnPropertyChanged(nameof(TextoLista));
            }
        }

        // ------------------------------------------------------------------ crear, reimprimir y anular

        internal async Task CrearAsync()
        {
            Mensaje = null;
            string motivo = MotivoAltaNoValida(Origen, Destino, Descripcion, BultosTexto, out int bultos);
            if (motivo != null)
            {
                Mensaje = motivo;
                return;
            }
            var nuevo = new NuevoEnvioInterno
            {
                Origen = Origen.Trim().ToUpperInvariant(),
                Destino = Destino.Trim().ToUpperInvariant(),
                Descripcion = Descripcion.Trim(),
                Bultos = bultos
            };
            EstaOcupado = true;
            EnvioInterno creado;
            try
            {
                creado = await _servicio.Crear(nuevo).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                EstaOcupado = false;
                TratarError(ex, "crear el envío interno");
                return;
            }
            EstaOcupado = false;
            Mensaje = TextoCreado(creado, nuevo);
            if (creado != null && !creado.EtiquetasImpresas)
            {
                _dialogos?.ShowError(Mensaje);
            }
            Descripcion = null;
            BultosTexto = "1";
            await CargarAsync().ConfigureAwait(true);
            Seleccionado = Lista.FirstOrDefault(e => e.Id == creado?.Id) ?? Seleccionado;
        }

        /// <summary>PURA. «Envío interno 12 creado (Algete → Alcobendas, 2 bultos): etiquetas EI12-1, EI12-2 impresas.»</summary>
        internal static string TextoCreado(EnvioInterno creado, NuevoEnvioInterno pedido)
        {
            if (creado == null)
            {
                return "Envío interno creado.";
            }
            int bultos = creado.Bultos > 0 ? creado.Bultos : pedido.Bultos;
            string cabecera = $"Envío interno {creado.Id} creado ({SedesEnvioInterno.Nombre(creado.Origen ?? pedido.Origen)} → " +
                $"{SedesEnvioInterno.Nombre(creado.Destino ?? pedido.Destino)}, {bultos} {(bultos == 1 ? "bulto" : "bultos")})";
            string codigos = creado.Codigos != null && creado.Codigos.Any() ? string.Join(", ", creado.Codigos) : null;
            if (creado.EtiquetasImpresas)
            {
                return codigos == null ? $"{cabecera}: etiquetas impresas." : $"{cabecera}: etiquetas {codigos} impresas.";
            }
            return $"{cabecera}, pero las etiquetas NO se han impreso: use «Reimprimir etiquetas»" + (codigos == null ? "." : $" ({codigos}).");
        }

        internal async Task ReimprimirAsync()
        {
            EnvioInterno envio = Seleccionado;
            if (envio == null)
            {
                return;
            }
            Mensaje = null;
            EstaOcupado = true;
            try
            {
                EnvioInterno impreso = await _servicio.ImprimirEtiquetas(envio.Id, null).ConfigureAwait(true);
                EstaOcupado = false;
                string codigos = impreso?.Codigos != null && impreso.Codigos.Any() ? $" ({string.Join(", ", impreso.Codigos)})" : string.Empty;
                if (impreso != null && !impreso.EtiquetasImpresas)
                {
                    Mensaje = $"Las etiquetas del envío interno {envio.Id} no se han impreso{codigos}. Compruebe la impresora y vuelva a intentarlo.";
                    _dialogos?.ShowError(Mensaje);
                    return;
                }
                Mensaje = $"Etiquetas del envío interno {envio.Id} impresas{codigos}.";
            }
            catch (Exception ex)
            {
                EstaOcupado = false;
                TratarError(ex, "reimprimir las etiquetas");
            }
        }

        internal async Task AnularAsync()
        {
            EnvioInterno envio = Seleccionado;
            if (envio == null || _dialogos == null)
            {
                return;
            }
            Mensaje = null;
            string motivo = _dialogos.GetText("Anular envío interno",
                $"¿Por qué anula el envío interno {envio.Id} ({envio.Trayecto}: {envio.Descripcion?.Trim()})? Si algún bulto ya está en una furgoneta no se podrá.");
            if (motivo == null)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(motivo))
            {
                Mensaje = MENSAJE_FALTA_MOTIVO;
                _dialogos.ShowError(MENSAJE_FALTA_MOTIVO);
                return;
            }
            EstaOcupado = true;
            try
            {
                await _servicio.Anular(envio.Id, motivo.Trim()).ConfigureAwait(true);
                EstaOcupado = false;
                Mensaje = $"Envío interno {envio.Id} anulado.";
            }
            catch (Exception ex)
            {
                EstaOcupado = false;
                if (TratarError(ex, "anular el envío interno"))
                {
                    return;
                }
            }
            await CargarAsync().ConfigureAwait(true);
        }

        /// <summary>
        /// El error de una acción, tal cual lo dice la API. True si no hay que releer; con 404/409 (otro lo ha cambiado, un bulto ya va
        /// en una furgoneta) se relee para ver cómo está.
        /// </summary>
        private bool TratarError(Exception ex, string accion)
        {
            if (ex is ApiRepartoException api)
            {
                Mensaje = api.Message;
                _dialogos?.ShowError(api.Message);
                if (api.ServidorSinActualizar)
                {
                    Disponible = false;
                    Aviso = api.Message;
                    return true;
                }
                return !(api.CodigoHttp == 404 || api.CodigoHttp == 409);
            }
            Mensaje = $"No se ha podido {accion}: {ex.Message}";
            _dialogos?.ShowError(Mensaje);
            return true;
        }

        private void NotificarComandos()
        {
            CargarCommand?.NotifyCanExecuteChanged();
            CrearCommand?.NotifyCanExecuteChanged();
            ReimprimirCommand?.NotifyCanExecuteChanged();
            AnularCommand?.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Una sede del combo: código (ALG) y nombre (Algete).</summary>
    public class SedeEnvioInterno
    {
        public SedeEnvioInterno(string codigo, string nombre)
        {
            Codigo = codigo;
            Nombre = nombre;
        }

        public string Codigo { get; }
        public string Nombre { get; }
    }
}
