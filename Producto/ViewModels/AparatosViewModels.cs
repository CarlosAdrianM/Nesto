using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto.ViewModels
{
    /// <summary>
    /// NestoAPI#913 (corte 1): los nombres que se enseñan de la situación y del tipo de movimiento (la API los manda como el nombre del
    /// enum). Lo que no se conoce sale tal cual.
    /// </summary>
    public static class TextosAparatos
    {
        public static string Situacion(string situacion)
        {
            switch (situacion)
            {
                case null: return null;
                case "EnStock": return "En stock";
                case "Vendido": return "Vendido";
                case "Inmovilizado": return "Inmovilizado";
                case "Alquilado": return "Alquilado";
                case "DevueltoAlProveedor": return "Devuelto al proveedor";
                case "SinRastro": return "Sin rastro";
                default: return situacion;
            }
        }

        public static string TipoMovimiento(string tipo)
        {
            switch (tipo)
            {
                case "Compra": return "Compra";
                case "DevolucionProveedor": return "Devolución al proveedor";
                case "Venta": return "Venta";
                case "Abono": return "Abono";
                case "Inmovilizado": return "Inmovilizado";
                case "RegistroAlquiler": return "Registro de alquiler";
                default: return tipo;
            }
        }
    }

    /// <summary>Una línea de la historia, ya con los textos de la pantalla.</summary>
    public class FilaMovimientoAparato
    {
        public FilaMovimientoAparato(MovimientoAparato movimiento)
        {
            Fecha = movimiento.Fecha;
            Tipo = TextosAparatos.TipoMovimiento(movimiento.Tipo);
            Documento = movimiento.Documento;
            Tercero = string.IsNullOrWhiteSpace(movimiento.NombreTercero)
                ? movimiento.Tercero?.Trim()
                : $"{movimiento.Tercero?.Trim()} {movimiento.NombreTercero.Trim()}".Trim();
            Texto = movimiento.Texto;
        }

        public DateTime? Fecha { get; }
        public string Tipo { get; }
        public string Documento { get; }
        public string Tercero { get; }
        public string Texto { get; }
    }

    /// <summary>
    /// NestoAPI#913 (corte 1): pestaña «Historia del aparato» de la ventana de Alquileres. Para el registro elegido (producto + nº de
    /// serie) enseña la situación que calcula la API de los movimientos (nunca la del registro) y la lista de movimientos. Solo lectura.
    /// Una respuesta que llega cuando ya se ha pedido otro aparato se descarta.
    /// </summary>
    public class HistoriaAparatoViewModel : ObservableObject
    {
        public const string MENSAJE_SIN_SERIE = "Elija en la relación un registro con producto y número de serie para ver su historia.";

        private readonly IServicioAparatos _servicio;
        private int _testigo;

        public HistoriaAparatoViewModel(IServicioAparatos servicio)
        {
            _servicio = servicio;
            Aviso = MENSAJE_SIN_SERIE;
        }

        private string _producto;
        public string Producto { get => _producto; private set => SetProperty(ref _producto, value); }

        private string _numeroSerie;
        public string NumeroSerie { get => _numeroSerie; private set => SetProperty(ref _numeroSerie, value); }

        private string _nombre;
        public string Nombre { get => _nombre; private set => SetProperty(ref _nombre, value); }

        private string _situacion;
        /// <summary>La situación calculada, con su nombre («En stock», «Vendido»…).</summary>
        public string Situacion { get => _situacion; private set => SetProperty(ref _situacion, value); }

        private string _textoSituacion;
        public string TextoSituacion { get => _textoSituacion; private set => SetProperty(ref _textoSituacion, value); }

        private string _textoRegistro;
        /// <summary>Lo que dice el registro y si la serie está repetida.</summary>
        public string TextoRegistro { get => _textoRegistro; private set => SetProperty(ref _textoRegistro, value); }

        private ObservableCollection<FilaMovimientoAparato> _movimientos = new ObservableCollection<FilaMovimientoAparato>();
        public ObservableCollection<FilaMovimientoAparato> Movimientos { get => _movimientos; private set => SetProperty(ref _movimientos, value); }

        private string _aviso;
        public string Aviso { get => _aviso; private set => SetProperty(ref _aviso, value); }

        private bool _estaCargando;
        public bool EstaCargando { get => _estaCargando; private set => SetProperty(ref _estaCargando, value); }

        /// <summary>Lee la historia del aparato. Nunca lanza: si la API no la da, se dice en <see cref="Aviso"/>.</summary>
        public async Task CargarAsync(string producto, string numeroSerie)
        {
            int testigo = ++_testigo;
            Vaciar();
            Producto = producto?.Trim();
            NumeroSerie = numeroSerie?.Trim();
            if (string.IsNullOrWhiteSpace(Producto) || string.IsNullOrWhiteSpace(NumeroSerie))
            {
                Aviso = MENSAJE_SIN_SERIE;
                return;
            }

            EstaCargando = true;
            Aviso = $"Leyendo la historia del {Producto} con nº de serie {NumeroSerie}…";
            HistoriaAparato historia = null;
            string motivo = null;
            try
            {
                historia = await _servicio.LeerHistoria(Producto, NumeroSerie);
            }
            catch (Exception ex)
            {
                motivo = ex.Message;
            }
            if (testigo != _testigo)
            {
                return; // ya se ha pedido otro aparato
            }
            EstaCargando = false;
            if (historia == null)
            {
                Aviso = $"No se ha podido leer la historia del {Producto} con nº de serie {NumeroSerie}: {motivo ?? "el servidor no ha contestado."}";
                return;
            }

            Nombre = historia.Nombre?.Trim();
            Situacion = TextosAparatos.Situacion(historia.Situacion);
            TextoSituacion = historia.TextoSituacion;
            TextoRegistro = TextoDelRegistro(historia);
            Movimientos = new ObservableCollection<FilaMovimientoAparato>(
                (historia.Movimientos ?? new List<MovimientoAparato>()).Select(m => new FilaMovimientoAparato(m)));
            Aviso = Movimientos.Count == 0 ? "No hay movimientos de este número de serie." : null;
        }

        private void Vaciar()
        {
            Nombre = null;
            Situacion = null;
            TextoSituacion = null;
            TextoRegistro = null;
            Movimientos = new ObservableCollection<FilaMovimientoAparato>();
            EstaCargando = false;
        }

        /// <summary>«Registro 1934: según el registro, alquilado a 22709. La serie está también en los registros 1940 y 1951.»</summary>
        public static string TextoDelRegistro(HistoriaAparato historia)
        {
            if (historia == null)
            {
                return null;
            }
            List<string> partes = new List<string>();
            if (historia.Registro == null)
            {
                partes.Add("No está en el registro de aparatos.");
            }
            else
            {
                string segun = TextosAparatos.Situacion(historia.Registro.SituacionSegunRegistro)?.ToLower(new CultureInfo("es-ES"));
                string cliente = string.IsNullOrWhiteSpace(historia.Registro.Cliente) ? string.Empty : $" (cliente {historia.Registro.Cliente.Trim()})";
                partes.Add($"Registro {historia.Registro.Numero}: según el registro, {segun}{cliente}.");
            }
            if (historia.OtrosRegistros != null && historia.OtrosRegistros.Count > 0)
            {
                partes.Add($"La serie está también en {(historia.OtrosRegistros.Count == 1 ? "el registro" : "los registros")} {Lista(historia.OtrosRegistros.Select(r => r.ToString()))}.");
            }
            return string.Join(" ", partes);
        }

        private static string Lista(IEnumerable<string> elementos)
        {
            List<string> todos = elementos.ToList();
            return todos.Count <= 1 ? string.Join(string.Empty, todos) : string.Join(", ", todos.Take(todos.Count - 1)) + " y " + todos.Last();
        }
    }

    /// <summary>Una fila de la lista de descuadres, con los textos de la pantalla.</summary>
    public class FilaDescuadreAparato
    {
        public FilaDescuadreAparato(DescuadreAparato descuadre)
        {
            Descuadre = descuadre;
        }

        public DescuadreAparato Descuadre { get; }
        public string Producto => Descuadre.Producto?.Trim();
        public string Nombre => Descuadre.Nombre?.Trim();
        public string NumeroSerie => Descuadre.NumeroSerie?.Trim();
        public string Motivo => Descuadre.Motivo;
        public string TextoMotivo => string.IsNullOrWhiteSpace(Descuadre.TextoMotivo) ? Descuadre.Motivo : Descuadre.TextoMotivo;
        public string SituacionSegunRegistro => TextosAparatos.Situacion(Descuadre.SituacionSegunRegistro) ?? "(sin registro)";
        public string SituacionCalculada => TextosAparatos.Situacion(Descuadre.SituacionCalculada);
        public string TextoSituacion => Descuadre.TextoSituacion;
        public string OtrasFormas => Descuadre.OtrasFormasDeLaSerie == null || Descuadre.OtrasFormasDeLaSerie.Count == 0
            ? null : string.Join(", ", Descuadre.OtrasFormasDeLaSerie);
        public bool TieneRegistro => Descuadre.TieneRegistro;
    }

    /// <summary>Un motivo del filtro («(Todos)» con <see cref="Codigo"/> null).</summary>
    public class MotivoDescuadreFiltro
    {
        public MotivoDescuadreFiltro(string codigo, string texto)
        {
            Codigo = codigo;
            Texto = texto;
        }

        public string Codigo { get; }
        public string Texto { get; }
        public override string ToString() => Texto;
    }

    /// <summary>
    /// NestoAPI#913 (corte 1): pestaña «Descuadres» de la ventana de Alquileres: los aparatos cuyo registro no coincide con lo que dicen
    /// los movimientos (por nº de serie), filtrables por producto y motivo, y el resumen por producto. Doble clic en uno con registro =
    /// ir a ese registro (lo hace la ventana con <see cref="IrAlRegistro"/>). Solo lectura.
    /// </summary>
    public class DescuadresAparatosViewModel : ObservableObject
    {
        public const string TODOS = "(Todos)";

        private readonly IServicioAparatos _servicio;
        private List<FilaDescuadreAparato> _todos = new List<FilaDescuadreAparato>();
        private int _testigo;

        public DescuadresAparatosViewModel(IServicioAparatos servicio)
        {
            _servicio = servicio;
            CargarCommand = new AsyncRelayCommand(CargarAsync, () => !EstaCargando);
            IrAlRegistroCommand = new RelayCommand<FilaDescuadreAparato>(OnIrAlRegistro, f => f != null && f.TieneRegistro);
            Aviso = "Pulse «Actualizar» para buscar los descuadres.";
        }

        public IAsyncRelayCommand CargarCommand { get; }
        public IRelayCommand<FilaDescuadreAparato> IrAlRegistroCommand { get; }

        /// <summary>Lo pone la ventana: ir al registro de ese producto y nº de serie.</summary>
        public Action<string, string> IrAlRegistro { get; set; }

        private bool _soloListaAlquileres;
        /// <summary>Solo los productos de la lista de alquileres (si no, también los que llevan nº de serie).</summary>
        public bool SoloListaAlquileres
        {
            get => _soloListaAlquileres;
            set
            {
                if (SetProperty(ref _soloListaAlquileres, value) && _leidoAlgunaVez)
                {
                    _ = CargarAsync();
                }
            }
        }

        private bool _leidoAlgunaVez;

        private ObservableCollection<FilaDescuadreAparato> _descuadres = new ObservableCollection<FilaDescuadreAparato>();
        public ObservableCollection<FilaDescuadreAparato> Descuadres { get => _descuadres; private set => SetProperty(ref _descuadres, value); }

        private ObservableCollection<ResumenProductoAparatos> _productos = new ObservableCollection<ResumenProductoAparatos>();
        public ObservableCollection<ResumenProductoAparatos> Productos { get => _productos; private set => SetProperty(ref _productos, value); }

        private ObservableCollection<MotivoDescuadreFiltro> _motivos = new ObservableCollection<MotivoDescuadreFiltro> { new MotivoDescuadreFiltro(null, TODOS) };
        public ObservableCollection<MotivoDescuadreFiltro> Motivos { get => _motivos; private set => SetProperty(ref _motivos, value); }

        private MotivoDescuadreFiltro _motivoElegido;
        public MotivoDescuadreFiltro MotivoElegido
        {
            get => _motivoElegido;
            set
            {
                if (SetProperty(ref _motivoElegido, value))
                {
                    Filtrar();
                }
            }
        }

        private string _filtroProducto;
        /// <summary>Referencia o parte del nombre del producto (sin mayúsculas).</summary>
        public string FiltroProducto
        {
            get => _filtroProducto;
            set
            {
                if (SetProperty(ref _filtroProducto, value))
                {
                    Filtrar();
                }
            }
        }

        private FilaDescuadreAparato _seleccionado;
        public FilaDescuadreAparato Seleccionado { get => _seleccionado; set => SetProperty(ref _seleccionado, value); }

        private string _aviso;
        public string Aviso { get => _aviso; private set => SetProperty(ref _aviso, value); }

        private string _resumen;
        public string Resumen { get => _resumen; private set => SetProperty(ref _resumen, value); }

        private bool _estaCargando;
        public bool EstaCargando
        {
            get => _estaCargando;
            private set
            {
                if (SetProperty(ref _estaCargando, value))
                {
                    CargarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>Lee los descuadres. Nunca lanza.</summary>
        public async Task CargarAsync()
        {
            int testigo = ++_testigo;
            _leidoAlgunaVez = true;
            EstaCargando = true;
            Aviso = "Buscando los descuadres…";
            InformeDescuadresAparatos informe = null;
            string motivo = null;
            try
            {
                informe = await _servicio.LeerDescuadres(SoloListaAlquileres);
            }
            catch (Exception ex)
            {
                motivo = ex.Message;
            }
            if (testigo != _testigo)
            {
                return;
            }
            EstaCargando = false;
            if (informe == null)
            {
                _todos = new List<FilaDescuadreAparato>();
                Productos = new ObservableCollection<ResumenProductoAparatos>();
                Filtrar();
                Aviso = $"No se han podido leer los descuadres: {motivo ?? "el servidor no ha contestado."}";
                return;
            }

            _todos = (informe.Descuadres ?? new List<DescuadreAparato>()).Select(d => new FilaDescuadreAparato(d)).ToList();
            Productos = new ObservableCollection<ResumenProductoAparatos>(informe.Productos ?? new List<ResumenProductoAparatos>());
            string elegido = MotivoElegido?.Codigo;
            List<MotivoDescuadreFiltro> motivos = new List<MotivoDescuadreFiltro> { new MotivoDescuadreFiltro(null, TODOS) };
            motivos.AddRange(_todos.Where(f => !string.IsNullOrWhiteSpace(f.Motivo)).GroupBy(f => f.Motivo)
                .Select(g => new MotivoDescuadreFiltro(g.Key, $"{g.First().TextoMotivo} ({g.Count()})")));
            Motivos = new ObservableCollection<MotivoDescuadreFiltro>(motivos);
            _motivoElegido = Motivos.FirstOrDefault(m => m.Codigo == elegido) ?? Motivos[0];
            OnPropertyChanged(nameof(MotivoElegido));
            Filtrar();
            Aviso = informe.Aviso;
            if (_todos.Count == 0 && string.IsNullOrWhiteSpace(Aviso))
            {
                Aviso = "No hay descuadres: el registro coincide con los movimientos.";
            }
        }

        /// <summary>LA REGLA (pura) del filtro de la pantalla: producto (referencia o nombre) y motivo (cualquiera de los suyos).</summary>
        public static bool PasaFiltro(FilaDescuadreAparato fila, string filtroProducto, string motivo)
        {
            if (fila == null)
            {
                return false;
            }
            if (!string.IsNullOrWhiteSpace(motivo)
                && fila.Motivo != motivo
                && (fila.Descuadre.Motivos == null || !fila.Descuadre.Motivos.Contains(motivo)))
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(filtroProducto))
            {
                return true;
            }
            string filtro = filtroProducto.Trim();
            return (fila.Producto ?? string.Empty).IndexOf(filtro, StringComparison.OrdinalIgnoreCase) >= 0
                || (fila.Nombre ?? string.Empty).IndexOf(filtro, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void Filtrar()
        {
            Descuadres = new ObservableCollection<FilaDescuadreAparato>(_todos.Where(f => PasaFiltro(f, FiltroProducto, MotivoElegido?.Codigo)));
            Resumen = _todos.Count == 0
                ? null
                : Descuadres.Count == _todos.Count
                    ? $"{_todos.Count} {(_todos.Count == 1 ? "descuadre" : "descuadres")}."
                    : $"{Descuadres.Count} de {_todos.Count} descuadres.";
        }

        private void OnIrAlRegistro(FilaDescuadreAparato fila)
        {
            if (fila == null || !fila.TieneRegistro)
            {
                return;
            }
            IrAlRegistro?.Invoke(fila.Producto, fila.NumeroSerie);
        }
    }
}
