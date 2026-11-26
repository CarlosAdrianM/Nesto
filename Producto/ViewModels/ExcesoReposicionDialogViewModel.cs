using CommunityToolkit.Mvvm.Input;
using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto.ViewModels
{
    /// <summary>
    /// Sugerencia 1429 (Manuel y Carlos, 26/11/26): la tienda devuelve a Algete lo que le sobra de una familia, grupo o subgrupo
    /// sin ir producto a producto (POST api/Reposiciones/Exceso). «Calcular» es el ensayo (no crea nada): enseña cada producto con
    /// su stock, lo pendiente, el máximo de la tienda y el exceso. «Crear reposición» la crea de verdad, después de confirmar, y
    /// queda en preparación en la tienda hasta que se termine. Nesto no decide qué es exceso: lo dice la API.
    /// </summary>
    public class ExcesoReposicionDialogViewModel : DialogoNestoBase
    {
        public const string NOMBRE = "ExcesoReposicionDialog";
        public const string PARAMETRO_EMPRESA = "empresa";
        public const string PARAMETRO_ORIGEN = "origen";
        public const string PARAMETRO_DESTINO = "destino";
        /// <summary>Al crear, el Mensaje de la API para enseñarlo en la pantalla de «Enviar reposición».</summary>
        public const string PARAMETRO_MENSAJE = "mensaje";
        public const string TITULO_CONFIRMAR = "Devolver exceso";

        private readonly IServicioEnvioReposiciones _servicio;
        private readonly IServicioDialogos _dialogos;

        public ExcesoReposicionDialogViewModel(IServicioEnvioReposiciones servicio, IServicioDialogos dialogos)
        {
            _servicio = servicio ?? throw new ArgumentNullException(nameof(servicio));
            _dialogos = dialogos;
            Title = "Devolver exceso a Algete";
            CalcularCommand = new AsyncRelayCommand(CalcularAsync, () => PuedeCalcular);
            CrearCommand = new AsyncRelayCommand(CrearAsync, () => PuedeCrear);
            CancelarCommand = new RelayCommand(() => RaiseRequestClose(ResultadoBoton.Cancel), () => !EstaOcupado);
        }

        public IAsyncRelayCommand CalcularCommand { get; }
        public IAsyncRelayCommand CrearCommand { get; }
        public IRelayCommand CancelarCommand { get; }

        public string Empresa { get; private set; } = Constantes.Empresas.EMPRESA_DEFECTO;
        public string Origen { get; private set; }
        public string Destino { get; private set; } = Constantes.Almacenes.ALMACEN_ALGETE;

        public string Cabecera => $"Lo que {Origen} tiene de más y vuelve a {Destino}";

        private string _familia;
        public string Familia { get => _familia; set { if (SetProperty(ref _familia, value)) { FiltroCambiado(); } } }

        private string _grupo;
        public string Grupo { get => _grupo; set { if (SetProperty(ref _grupo, value)) { FiltroCambiado(); } } }

        private string _subgrupo;
        public string Subgrupo { get => _subgrupo; set { if (SetProperty(ref _subgrupo, value)) { FiltroCambiado(); } } }

        // Sugerencia 1429 (Manuel): filtros flexibles. Los números se escriben como texto y se comprueban al calcular.
        private string _textoNombre;
        /// <summary>El nombre contiene (varias palabras = todas). Vale también como filtro obligatorio.</summary>
        public string TextoNombre { get => _textoNombre; set { if (SetProperty(ref _textoNombre, value)) { FiltroCambiado(); } } }

        private string _textoNombreExcluir;
        /// <summary>El nombre no contiene (varias palabras = fuera si tiene cualquiera).</summary>
        public string TextoNombreExcluir { get => _textoNombreExcluir; set { if (SetProperty(ref _textoNombreExcluir, value)) { FiltroCambiado(); } } }

        private string _stockMinimo;
        /// <summary>Solo los de más de estas unidades en la tienda. Vacío = sin este filtro.</summary>
        public string StockMinimo { get => _stockMinimo; set { if (SetProperty(ref _stockMinimo, value)) { FiltroCambiado(); } } }

        private string _sinVentasEnMeses;
        /// <summary>Solo los que no se han vendido en la tienda en estos meses (1 a 60). Vacío = sin este filtro.</summary>
        public string SinVentasEnMeses { get => _sinVentasEnMeses; set { if (SetProperty(ref _sinVentasEnMeses, value)) { FiltroCambiado(); } } }

        private string _dejarEnTienda;
        /// <summary>Cuántas dejar de cada uno en la tienda. Vacío = el máximo de la ficha.</summary>
        public string DejarEnTienda { get => _dejarEnTienda; set { if (SetProperty(ref _dejarEnTienda, value)) { FiltroCambiado(); } } }

        public const int MAXIMO_MESES_SIN_VENTAS = 60;
        public const string MOTIVO_STOCK_MINIMO = "En «Solo con más de … unidades» escriba un número entero (0 o más), o déjelo vacío.";
        public const string MOTIVO_SIN_VENTAS = "En «Sin ventas en los últimos … meses» escriba un número del 1 al 60, o déjelo vacío.";
        public const string MOTIVO_DEJAR = "En «Dejar en la tienda … de cada uno» escriba un número entero (0 o más), o déjelo vacío para usar el máximo de la ficha.";

        private string _filtros;
        /// <summary>Los filtros que ha aplicado la API, en una línea.</summary>
        public string Filtros { get => _filtros; private set => SetProperty(ref _filtros, value); }

        public ObservableCollection<LineaExcesoReposicion> Lineas { get; } = new ObservableCollection<LineaExcesoReposicion>();

        private int _numeroLineas;
        public int NumeroLineas { get => _numeroLineas; private set => SetProperty(ref _numeroLineas, value); }

        private int _totalUnidades;
        public int TotalUnidades { get => _totalUnidades; private set => SetProperty(ref _totalUnidades, value); }

        private int _lineasSinMaximo;
        public int LineasSinMaximo { get => _lineasSinMaximo; private set => SetProperty(ref _lineasSinMaximo, value); }

        private string _mensaje;
        /// <summary>El Mensaje de la API («Se mandarían 418 unidades de 183 referencias…»).</summary>
        public string Mensaje { get => _mensaje; private set => SetProperty(ref _mensaje, value); }

        private string _error;
        /// <summary>El motivo de la API (400, 403, 409) o de lo que haya fallado; en rojo.</summary>
        public string Error
        {
            get => _error;
            private set
            {
                if (SetProperty(ref _error, value))
                {
                    OnPropertyChanged(nameof(HayError));
                }
            }
        }
        public bool HayError => !string.IsNullOrWhiteSpace(Error);

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    RefrescarComandos();
                }
            }
        }

        /// <summary>Ya se ha calculado con los filtros de ahora (si se cambian, hay que volver a calcular).</summary>
        private bool _calculado;
        public bool HayCalculo => _calculado;

        /// <summary>Algún filtro: la API nunca calcula «todo el almacén».</summary>
        public bool HayFiltro => !string.IsNullOrWhiteSpace(Familia) || !string.IsNullOrWhiteSpace(Grupo) || !string.IsNullOrWhiteSpace(Subgrupo)
            || !string.IsNullOrWhiteSpace(TextoNombre);

        /// <summary>Lo que no vale de los números (de usted); null si todo vale.</summary>
        public string MotivoFiltrosNoValidos
        {
            get
            {
                if (!Numero(StockMinimo, out int? stock) || stock < 0)
                {
                    return MOTIVO_STOCK_MINIMO;
                }
                if (!Numero(SinVentasEnMeses, out int? meses) || meses < 1 || meses > MAXIMO_MESES_SIN_VENTAS)
                {
                    return MOTIVO_SIN_VENTAS;
                }
                if (!Numero(DejarEnTienda, out int? dejar) || dejar < 0)
                {
                    return MOTIVO_DEJAR;
                }
                return null;
            }
        }

        /// <summary>Vacío = null (vale); un entero = ese; otra cosa = no vale.</summary>
        private static bool Numero(string texto, out int? valor)
        {
            valor = null;
            if (string.IsNullOrWhiteSpace(texto))
            {
                return true;
            }
            if (int.TryParse(texto.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int n))
            {
                valor = n;
                return true;
            }
            return false;
        }
        public bool PuedeCalcular => HayFiltro && !EstaOcupado && !string.IsNullOrWhiteSpace(Origen);
        public bool PuedeCrear => _calculado && NumeroLineas > 0 && !EstaOcupado;

        public override bool CanCloseDialog() => !EstaOcupado;

        public override void OnDialogOpened(ParametrosDialogo parameters)
        {
            string empresa = parameters?.GetValue<string>(PARAMETRO_EMPRESA);
            string origen = parameters?.GetValue<string>(PARAMETRO_ORIGEN);
            string destino = parameters?.GetValue<string>(PARAMETRO_DESTINO);
            Empresa = string.IsNullOrWhiteSpace(empresa) ? Constantes.Empresas.EMPRESA_DEFECTO : empresa.Trim();
            Origen = origen?.Trim();
            Destino = string.IsNullOrWhiteSpace(destino) ? Constantes.Almacenes.ALMACEN_ALGETE : destino.Trim();
            Title = $"Devolver exceso de {Origen} a {Destino}";
            OnPropertyChanged(nameof(Empresa));
            OnPropertyChanged(nameof(Origen));
            OnPropertyChanged(nameof(Destino));
            OnPropertyChanged(nameof(Cabecera));
            RefrescarComandos();
        }

        internal PeticionExcesoReposicion Peticion() => new PeticionExcesoReposicion
        {
            Empresa = Empresa,
            Origen = Origen,
            Destino = Destino,
            Familia = Limpio(Familia),
            Grupo = Limpio(Grupo),
            Subgrupo = Limpio(Subgrupo),
            TextoNombre = Limpio(TextoNombre),
            TextoNombreExcluir = Limpio(TextoNombreExcluir),
            StockMinimo = Numero(StockMinimo, out int? stock) ? stock : null,
            SinVentasEnMeses = Numero(SinVentasEnMeses, out int? meses) ? meses : null,
            DejarEnTienda = Numero(DejarEnTienda, out int? dejar) ? dejar : null
        };

        /// <summary>El ensayo: no crea nada.</summary>
        public async Task CalcularAsync()
        {
            if (!PuedeCalcular)
            {
                return;
            }
            string motivo = MotivoFiltrosNoValidos;
            if (motivo != null)
            {
                Limpiar();
                Error = motivo;
                return;
            }
            Error = null;
            EstaOcupado = true;
            try
            {
                ResultadoExcesoReposicion resultado = await _servicio.CalcularExceso(Peticion(), true).ConfigureAwait(true);
                Ensenar(resultado);
                _calculado = true;
            }
            catch (Exception ex)
            {
                Limpiar();
                Error = Motivo(ex, "calcular el exceso");
            }
            finally
            {
                EstaOcupado = false;
                OnPropertyChanged(nameof(HayCalculo));
                RefrescarComandos();
            }
        }

        /// <summary>Pregunta (con «No» por defecto) y crea la reposición de verdad.</summary>
        public async Task CrearAsync()
        {
            if (!PuedeCrear)
            {
                return;
            }
            if (_dialogos != null && !_dialogos.ShowConfirmationAnswer(TITULO_CONFIRMAR, TextoConfirmacion(Origen, Destino, TotalUnidades, NumeroLineas), true))
            {
                return;
            }
            Error = null;
            EstaOcupado = true;
            ResultadoExcesoReposicion resultado = null;
            try
            {
                resultado = await _servicio.CalcularExceso(Peticion(), false).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Error = Motivo(ex, "crear la reposición");
            }
            finally
            {
                EstaOcupado = false;
            }
            if (resultado == null)
            {
                return;
            }
            if (resultado.Reposicion == null)
            {
                // Mientras tanto ha dejado de haber exceso: nada creado
                Ensenar(resultado);
                Error = string.IsNullOrWhiteSpace(resultado.Mensaje) ? "No se ha creado ninguna reposición: ya no hay exceso." : resultado.Mensaje;
                return;
            }
            RaiseRequestClose(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo
            {
                { PARAMETRO_MENSAJE, TextoCreada(resultado) }
            }));
        }

        /// <summary>La confirmación, de usted, con las unidades y las referencias.</summary>
        public static string TextoConfirmacion(string origen, string destino, int unidades, int referencias)
            => $"Se va a crear la reposición de {origen} a {destino} con {Unidades(unidades)} de {Referencias(referencias)}. " +
               "Quedará en preparación en la tienda hasta que la termine. ¿Crearla?";

        internal static string TextoCreada(ResultadoExcesoReposicion resultado)
            => $"Reposición del exceso creada: {Unidades(resultado.TotalUnidades)} de {Referencias(resultado.NumeroLineas)}. " +
               "Prepárela y termínela como cualquier otra.";

        private static string Unidades(int n) => n == 1 ? "1 unidad" : $"{n} unidades";
        private static string Referencias(int n) => n == 1 ? "1 referencia" : $"{n} referencias";

        private void Ensenar(ResultadoExcesoReposicion resultado)
        {
            Lineas.Clear();
            foreach (LineaExcesoReposicion linea in resultado?.Lineas ?? new System.Collections.Generic.List<LineaExcesoReposicion>())
            {
                Lineas.Add(linea);
            }
            NumeroLineas = resultado?.NumeroLineas > 0 ? resultado.NumeroLineas : Lineas.Count;
            TotalUnidades = resultado?.TotalUnidades ?? 0;
            LineasSinMaximo = resultado?.LineasSinMaximo ?? 0;
            Mensaje = resultado?.Mensaje;
            Filtros = resultado?.Filtros;
        }

        private void Limpiar()
        {
            Lineas.Clear();
            NumeroLineas = 0;
            TotalUnidades = 0;
            LineasSinMaximo = 0;
            Mensaje = null;
            Filtros = null;
        }

        private void FiltroCambiado()
        {
            if (_calculado)
            {
                _calculado = false;
                Limpiar();
                OnPropertyChanged(nameof(HayCalculo));
            }
            OnPropertyChanged(nameof(HayFiltro));
            RefrescarComandos();
        }

        private void RefrescarComandos()
        {
            OnPropertyChanged(nameof(PuedeCalcular));
            OnPropertyChanged(nameof(PuedeCrear));
            CalcularCommand?.NotifyCanExecuteChanged();
            CrearCommand?.NotifyCanExecuteChanged();
            CancelarCommand?.NotifyCanExecuteChanged();
        }

        private static string Motivo(Exception ex, string que)
            => ex is EnvioReposicionException
                ? ex.Message
                : $"No se ha podido {que}: {ex.Message}";

        private static string Limpio(string texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
    }
}
