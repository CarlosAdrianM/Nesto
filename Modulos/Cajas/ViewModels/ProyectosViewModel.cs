using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlesUsuario.Services;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.ViewModels
{
    /// <summary>
    /// Proyectos (gemela de NestoAPI#901, Carlos 25/11/26): cada convocatoria de un curso del SEPE, feria, evento o campaña es un
    /// proyecto al que se le imputan, ya contabilizados, apuntes de las cuentas 6 y 7 por importe, para sacar su cuenta de
    /// explotación. Tres pestañas: «Proyectos» (esta clase: lista, alta, edición, cerrar, reabrir y borrar), «Apuntes por imputar»
    /// (<see cref="ApuntesProyectosViewModel"/>) y «Cuenta de explotación» (<see cref="CuentaExplotacionProyectoViewModel"/>).
    /// Los permisos los decide la API (Administración, Dirección e Informática; reabrir, solo Dirección): sus 403 se enseñan tal cual.
    /// </summary>
    public class ProyectosViewModel : ObservableObject, IReceptorNavegacion
    {
        internal const string TITULO = ApuntesProyectosViewModel.TITULO;
        public const int PESTANA_PROYECTOS = 0;
        public const int PESTANA_APUNTES = 1;
        public const int PESTANA_CUENTA = 2;

        private readonly IProyectosService _servicio;
        private readonly IServicioDialogos _dialogos;
        private readonly string _empresa = Constantes.Empresas.EMPRESA_DEFECTO;
        private bool _cargadoAlAbrir;

        public ProyectosViewModel(IProyectosService servicio, IServicioDialogos dialogos, IGuardadorFicherosDescarga guardador)
            : this(servicio, dialogos, guardador, null, null)
        {
        }

        /// <param name="hoy">Para las fechas por defecto de los apuntes (los tests fijan el día).</param>
        /// <param name="abrir">Cómo se abre el Excel guardado (los tests no abren nada).</param>
        internal ProyectosViewModel(IProyectosService servicio, IServicioDialogos dialogos, IGuardadorFicherosDescarga? guardador,
            Func<DateTime>? hoy, Action<string>? abrir)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            Titulo = "Proyectos";
            Apuntes = new ApuntesProyectosViewModel(servicio, dialogos, _empresa, hoy);
            Cuenta = new CuentaExplotacionProyectoViewModel(servicio, dialogos, guardador, _empresa, abrir);
            TiposFiltro = new List<OpcionTipoProyecto> { new OpcionTipoProyecto(null, "Todos") };
            TiposFiltro.AddRange(ReglasPantallaProyectos.Tipos);
            _tipoFiltro = TiposFiltro[0];

            BuscarCommand = new AsyncRelayCommand(BuscarAsync, () => !EstaOcupado);
            NuevoCommand = new RelayCommand(Nuevo, () => !EstaOcupado);
            EditarCommand = new RelayCommand(Editar, () => ProyectoSeleccionado != null && !ProyectoSeleccionado.EstaCerrado && !EstaOcupado);
            GuardarCommand = new AsyncRelayCommand(GuardarAsync, () => Edicion != null && !EstaOcupado);
            CancelarEdicionCommand = new RelayCommand(() => Edicion = null, () => Edicion != null);
            CerrarCommand = new AsyncRelayCommand(CerrarAsync, () => ProyectoSeleccionado != null && !ProyectoSeleccionado.EstaCerrado && !EstaOcupado);
            ReabrirCommand = new AsyncRelayCommand(ReabrirAsync, () => ProyectoSeleccionado != null && ProyectoSeleccionado.EstaCerrado && !EstaOcupado);
            BorrarCommand = new AsyncRelayCommand(BorrarAsync, () => ProyectoSeleccionado != null && !EstaOcupado);
            VerCuentaCommand = new AsyncRelayCommand(VerCuentaAsync, () => ProyectoSeleccionado != null);
        }

        public string Titulo { get; }

        public ApuntesProyectosViewModel Apuntes { get; }
        public CuentaExplotacionProyectoViewModel Cuenta { get; }

        private int _pestanaSeleccionada;
        public int PestanaSeleccionada
        {
            get => _pestanaSeleccionada;
            set
            {
                if (SetProperty(ref _pestanaSeleccionada, value) && value == PESTANA_APUNTES && !Apuntes.HaBuscado)
                {
                    _ = Apuntes.BuscarAsync();
                }
            }
        }

        // ---------- Filtros y lista ----------

        public List<OpcionTipoProyecto> TiposFiltro { get; }
        public IReadOnlyList<OpcionTipoProyecto> Tipos => ReglasPantallaProyectos.Tipos;

        private string? _filtro;
        public string? Filtro { get => _filtro; set => SetProperty(ref _filtro, value); }

        private OpcionTipoProyecto _tipoFiltro;
        public OpcionTipoProyecto TipoFiltro
        {
            get => _tipoFiltro;
            set
            {
                if (SetProperty(ref _tipoFiltro, value ?? TiposFiltro[0]) && _haBuscado)
                {
                    _ = BuscarAsync();
                }
            }
        }

        private bool _soloAbiertos = true;
        public bool SoloAbiertos
        {
            get => _soloAbiertos;
            set
            {
                if (SetProperty(ref _soloAbiertos, value) && _haBuscado)
                {
                    _ = BuscarAsync();
                }
            }
        }

        private bool _haBuscado;

        public ObservableCollection<ProyectoModel> Proyectos { get; } = new ObservableCollection<ProyectoModel>();

        private ProyectoModel? _proyectoSeleccionado;
        public ProyectoModel? ProyectoSeleccionado
        {
            get => _proyectoSeleccionado;
            set
            {
                if (SetProperty(ref _proyectoSeleccionado, value))
                {
                    NotificarComandos();
                }
            }
        }

        private string _resumen = string.Empty;
        public string Resumen { get => _resumen; private set => SetProperty(ref _resumen, value); }

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

        public IAsyncRelayCommand BuscarCommand { get; }

        public async Task BuscarAsync()
        {
            int? seleccionado = ProyectoSeleccionado?.Id;
            try
            {
                EstaOcupado = true;
                List<ProyectoModel> lista = await _servicio.Listar(_empresa, SoloAbiertos, TipoFiltro?.Codigo, Limpio(Filtro));
                _haBuscado = true;
                Proyectos.Clear();
                foreach (ProyectoModel p in lista?.Where(p => p != null) ?? Enumerable.Empty<ProyectoModel>())
                {
                    Proyectos.Add(p);
                }
                ProyectoSeleccionado = seleccionado.HasValue ? Proyectos.FirstOrDefault(p => p.Id == seleccionado.Value) : null;
                Resumen = TextoResumen(Proyectos);
            }
            catch (Exception ex)
            {
                _dialogos.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
            await RefrescarListasAsync();
        }

        /// <summary>Los proyectos para las otras pestañas: los abiertos (imputar) y todos (cuenta de explotación). Si falla, se quedan como estaban.</summary>
        private async Task RefrescarListasAsync()
        {
            try
            {
                List<ProyectoModel> todos = await _servicio.Listar(_empresa, false, null, null) ?? new List<ProyectoModel>();
                Apuntes.PonerProyectosAbiertos(todos.Where(p => p != null && !p.EstaCerrado));
                Cuenta.PonerProyectos(todos);
            }
            catch (Exception)
            {
                // La lista principal ya ha enseñado el error si lo hay
            }
        }

        internal static string TextoResumen(IReadOnlyCollection<ProyectoModel> proyectos)
        {
            if (proyectos.Count == 0)
            {
                return "No hay proyectos con esos filtros.";
            }
            string cuantos = proyectos.Count == 1 ? "1 proyecto" : $"{proyectos.Count} proyectos";
            decimal resultado = proyectos.Sum(p => p.Resultado);
            return $"{cuantos}; resultado de los que se ven: {ReglasPantallaProyectos.Euros(resultado)}.";
        }

        private static string? Limpio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

        // ---------- Alta y edición ----------

        private ProyectoEdicion? _edicion;
        /// <summary>El proyecto que se está creando o editando (panel lateral); null si no se edita nada.</summary>
        public ProyectoEdicion? Edicion
        {
            get => _edicion;
            private set
            {
                if (SetProperty(ref _edicion, value))
                {
                    OnPropertyChanged(nameof(HayEdicion));
                    GuardarCommand.NotifyCanExecuteChanged();
                    CancelarEdicionCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool HayEdicion => Edicion != null;

        public IRelayCommand NuevoCommand { get; }
        public IRelayCommand EditarCommand { get; }
        public IAsyncRelayCommand GuardarCommand { get; }
        public IRelayCommand CancelarEdicionCommand { get; }

        private void Nuevo() => Edicion = new ProyectoEdicion(null) { Tipo = ReglasPantallaProyectos.Tipos[0], FechaDesde = DateTime.Today };

        private void Editar()
        {
            if (ProyectoSeleccionado != null)
            {
                Edicion = new ProyectoEdicion(ProyectoSeleccionado);
            }
        }

        public async Task GuardarAsync()
        {
            ProyectoEdicion? edicion = Edicion;
            if (edicion == null)
            {
                return;
            }
            List<string> motivos = edicion.Motivos();
            if (motivos.Count > 0)
            {
                _dialogos.ShowError(string.Join(Environment.NewLine, motivos));
                return;
            }
            try
            {
                EstaOcupado = true;
                ProyectoModel datos = edicion.ParaEnviar(_empresa);
                ProyectoModel guardado = edicion.Id.HasValue
                    ? await _servicio.Modificar(edicion.Id.Value, datos)
                    : await _servicio.Crear(datos);
                Edicion = null;
                Resumen = edicion.Id.HasValue ? $"Proyecto {guardado?.Codigo?.Trim()} guardado." : $"Proyecto {guardado?.Codigo?.Trim()} creado.";
                if (guardado != null)
                {
                    _proyectoSeleccionado = guardado;
                }
            }
            catch (Exception ex)
            {
                _dialogos.ShowError(ex.Message);
                return;
            }
            finally
            {
                EstaOcupado = false;
            }
            string resumen = Resumen;
            await BuscarAsync();
            Resumen = resumen + " " + TextoResumen(Proyectos);
        }

        // ---------- Cerrar, reabrir, borrar ----------

        public IAsyncRelayCommand CerrarCommand { get; }
        public IAsyncRelayCommand ReabrirCommand { get; }
        public IAsyncRelayCommand BorrarCommand { get; }

        public async Task CerrarAsync()
        {
            ProyectoModel? proyecto = ProyectoSeleccionado;
            if (proyecto == null)
            {
                return;
            }
            if (!_dialogos.ShowConfirmationAnswer(TITULO,
                $"¿Cerrar el proyecto {proyecto.CodigoYNombre}?{Environment.NewLine}Cerrado no admite cambios ni imputaciones nuevas; reabrirlo solo puede hacerlo Dirección.",
                porDefectoNo: true))
            {
                return;
            }
            await CambiarEstadoAsync(() => _servicio.Cerrar(proyecto.Id), $"Proyecto {proyecto.Codigo?.Trim()} cerrado.");
        }

        public async Task ReabrirAsync()
        {
            ProyectoModel? proyecto = ProyectoSeleccionado;
            if (proyecto == null)
            {
                return;
            }
            if (!_dialogos.ShowConfirmationAnswer(TITULO, $"¿Reabrir el proyecto {proyecto.CodigoYNombre}?", porDefectoNo: true))
            {
                return;
            }
            await CambiarEstadoAsync(() => _servicio.Reabrir(proyecto.Id), $"Proyecto {proyecto.Codigo?.Trim()} reabierto.");
        }

        public async Task BorrarAsync()
        {
            ProyectoModel? proyecto = ProyectoSeleccionado;
            if (proyecto == null)
            {
                return;
            }
            if (!_dialogos.ShowConfirmationAnswer(TITULO, $"¿Borrar el proyecto {proyecto.CodigoYNombre}? No se puede deshacer.", porDefectoNo: true))
            {
                return;
            }
            await CambiarEstadoAsync(() => _servicio.Borrar(proyecto.Id), $"Proyecto {proyecto.Codigo?.Trim()} borrado.", quitarSeleccion: true);
        }

        private async Task CambiarEstadoAsync(Func<Task> accion, string hecho, bool quitarSeleccion = false)
        {
            try
            {
                EstaOcupado = true;
                await accion();
            }
            catch (Exception ex)
            {
                _dialogos.ShowError(ex.Message);
                return;
            }
            finally
            {
                EstaOcupado = false;
            }
            if (quitarSeleccion)
            {
                ProyectoSeleccionado = null;
            }
            await BuscarAsync();
            Resumen = hecho + " " + Resumen;
        }

        // ---------- Cuenta de explotación ----------

        public IAsyncRelayCommand VerCuentaCommand { get; }

        /// <summary>Doble clic en un proyecto: su cuenta de explotación.</summary>
        public async Task VerCuentaAsync()
        {
            ProyectoModel? proyecto = ProyectoSeleccionado;
            if (proyecto == null)
            {
                return;
            }
            PestanaSeleccionada = PESTANA_CUENTA;
            await Cuenta.ElegirAsync(proyecto);
        }

        private void NotificarComandos()
        {
            BuscarCommand.NotifyCanExecuteChanged();
            NuevoCommand.NotifyCanExecuteChanged();
            EditarCommand.NotifyCanExecuteChanged();
            GuardarCommand.NotifyCanExecuteChanged();
            CerrarCommand.NotifyCanExecuteChanged();
            ReabrirCommand.NotifyCanExecuteChanged();
            BorrarCommand.NotifyCanExecuteChanged();
            VerCuentaCommand.NotifyCanExecuteChanged();
        }

        public void AlLlegar(ParametrosNavegacion parametros)
        {
            if (!_cargadoAlAbrir)
            {
                _cargadoAlAbrir = true;
                _ = BuscarAsync();
            }
        }
    }

    /// <summary>Lo que se edita de un proyecto en el panel lateral (alta o edición).</summary>
    public class ProyectoEdicion : ObservableObject
    {
        public ProyectoEdicion(ProyectoModel? original)
        {
            Id = original?.Id;
            if (original != null)
            {
                _codigo = original.Codigo?.Trim();
                _nombre = original.Nombre?.Trim();
                _tipo = ReglasPantallaProyectos.Tipos.FirstOrDefault(t => string.Equals(t.Codigo, original.Tipo?.Trim(), StringComparison.OrdinalIgnoreCase));
                _fechaDesde = original.FechaDesde;
                _fechaHasta = original.FechaHasta;
                _producto = original.Producto?.Trim();
                _eventoId = original.EventoId;
                _presupuestoIngresos = original.PresupuestoIngresos;
                _presupuestoGastos = original.PresupuestoGastos;
                _comentarios = original.Comentarios;
            }
        }

        /// <summary>Null en un alta.</summary>
        public int? Id { get; }
        public bool EsNuevo => !Id.HasValue;
        public string TituloPanel => EsNuevo ? "Nuevo proyecto" : "Editar proyecto";

        private string? _codigo;
        public string? Codigo { get => _codigo; set => SetProperty(ref _codigo, value); }

        private string? _nombre;
        public string? Nombre { get => _nombre; set => SetProperty(ref _nombre, value); }

        private OpcionTipoProyecto? _tipo;
        public OpcionTipoProyecto? Tipo { get => _tipo; set => SetProperty(ref _tipo, value); }

        private DateTime? _fechaDesde;
        public DateTime? FechaDesde { get => _fechaDesde; set => SetProperty(ref _fechaDesde, value); }

        private DateTime? _fechaHasta;
        public DateTime? FechaHasta { get => _fechaHasta; set => SetProperty(ref _fechaHasta, value); }

        private string? _producto;
        public string? Producto { get => _producto; set => SetProperty(ref _producto, value); }

        private readonly int? _eventoId;

        private decimal? _presupuestoIngresos;
        public decimal? PresupuestoIngresos { get => _presupuestoIngresos; set => SetProperty(ref _presupuestoIngresos, value); }

        private decimal? _presupuestoGastos;
        public decimal? PresupuestoGastos { get => _presupuestoGastos; set => SetProperty(ref _presupuestoGastos, value); }

        private string? _comentarios;
        public string? Comentarios { get => _comentarios; set => SetProperty(ref _comentarios, value); }

        public List<string> Motivos() => ReglasPantallaProyectos.MotivosProyectoNoValido(Codigo, Nombre, Tipo?.Codigo, FechaDesde, FechaHasta,
            PresupuestoIngresos, PresupuestoGastos);

        /// <summary>El cuerpo de POST/PUT api/Proyectos (el código, en mayúsculas; lo vacío, null).</summary>
        public ProyectoModel ParaEnviar(string empresa) => new ProyectoModel
        {
            Id = Id ?? 0,
            Empresa = empresa,
            Codigo = Codigo?.Trim().ToUpperInvariant(),
            Nombre = Nombre?.Trim(),
            Tipo = Tipo?.Codigo,
            FechaDesde = FechaDesde?.Date,
            FechaHasta = FechaHasta?.Date,
            Producto = string.IsNullOrWhiteSpace(Producto) ? null : Producto.Trim(),
            EventoId = _eventoId,
            PresupuestoIngresos = PresupuestoIngresos,
            PresupuestoGastos = PresupuestoGastos,
            Comentarios = string.IsNullOrWhiteSpace(Comentarios) ? null : Comentarios.Trim()
        };
    }
}
