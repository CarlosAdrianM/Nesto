using ControlesUsuario.Dialogs;
using ControlesUsuario.Services;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.OfertasCombinadas.Interfaces;
using Nesto.Modulos.OfertasCombinadas.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Nesto.Modulos.OfertasCombinadas.ViewModels
{
    public delegate void NuevaOfertaCombinadaCreadaHandler(OfertaCombinadaWrapper nuevoItem);
    public delegate void NuevaOfertaFamiliaCreadaHandler(OfertaPermitidaFamiliaWrapper nuevoItem);
    public delegate void NuevaCampanaCreadaHandler(CampanaWrapper nuevoItem);
    public delegate void NuevaOfertaProductoCreadaHandler(OfertaProductoWrapper nuevoItem);
    public delegate void NuevaOfertaEscalonadaCreadaHandler(OfertaEscalonadaWrapper nuevoItem);

    public class OfertasCombinadasViewModel : ViewModelBase
    {
        private readonly IOfertasCombinadasService _service;
        private readonly IConfiguracion _configuracion;
        private readonly IDialogService _dialogService;
        private readonly IRegionManager _regionManager;
        private readonly IServicioProducto _servicioProducto;

        public OfertasCombinadasViewModel(IOfertasCombinadasService service, IConfiguracion configuracion, IDialogService dialogService, IRegionManager regionManager, IServicioProducto servicioProducto)
        {
            _service = service;
            _configuracion = configuracion;
            _dialogService = dialogService;
            _regionManager = regionManager;
            _servicioProducto = servicioProducto;

            OfertasCombinadas = new ObservableCollection<OfertaCombinadaWrapper>();
            OfertasFamilia = new ObservableCollection<OfertaPermitidaFamiliaWrapper>();
            OfertasEscalonadas = new ObservableCollection<OfertaEscalonadaWrapper>();
            Campanas = new ObservableCollection<CampanaWrapper>();
            ResumenCampanas = new ObservableCollection<ResumenCampanaModel>();
            OfertasProducto = new ObservableCollection<OfertaProductoWrapper>();

            CargarCommand = new DelegateCommand(async () => await OnCargar());
            NuevaOfertaCombinadaCommand = new DelegateCommand(OnNuevaOfertaCombinada);
            GuardarOfertaCombinadaCommand = new DelegateCommand<object>(async (o) => await OnGuardarOfertaCombinada(o as OfertaCombinadaWrapper));
            EliminarOfertaCombinadaCommand = new DelegateCommand<object>(async (o) => await OnEliminarOfertaCombinada(o as OfertaCombinadaWrapper));

            NuevaOfertaFamiliaCommand = new DelegateCommand(OnNuevaOfertaFamilia);
            GuardarOfertaFamiliaCommand = new DelegateCommand<object>(async (o) => await OnGuardarOfertaFamilia(o as OfertaPermitidaFamiliaWrapper));
            EliminarOfertaFamiliaCommand = new DelegateCommand<object>(async (o) => await OnEliminarOfertaFamilia(o as OfertaPermitidaFamiliaWrapper));

            NuevaOfertaEscalonadaCommand = new DelegateCommand(OnNuevaOfertaEscalonada);
            GuardarOfertaEscalonadaCommand = new DelegateCommand<object>(async (o) => await OnGuardarOfertaEscalonada(o as OfertaEscalonadaWrapper));
            EliminarOfertaEscalonadaCommand = new DelegateCommand<object>(async (o) => await OnEliminarOfertaEscalonada(o as OfertaEscalonadaWrapper));
            AnadirReferenciasCommand = new DelegateCommand(async () => await OnAnadirReferencias(), () => OfertaEscalonadaSeleccionada != null);
            NuevoProductoEscalonadoCommand = new DelegateCommand(OnNuevoProductoEscalonado, () => OfertaEscalonadaSeleccionada != null);
            EliminarProductoEscalonadoCommand = new DelegateCommand<object>(OnEliminarProductoEscalonado);
            NuevoTramoCommand = new DelegateCommand(OnNuevoTramo, () => OfertaEscalonadaSeleccionada != null);
            EliminarTramoCommand = new DelegateCommand<object>(OnEliminarTramo);

            NuevaCampanaCommand = new DelegateCommand(OnNuevaCampana);
            GuardarCampanaCommand = new DelegateCommand<object>(async (o) => await OnGuardarCampana(o as CampanaWrapper));
            EliminarCampanaCommand = new DelegateCommand<object>(async (o) => await OnEliminarCampana(o as CampanaWrapper));
            CerrarCampanaCommand = new DelegateCommand(async () => await OnCerrarCampana(), () => CampanaSeleccionada != null);
            BorrarCampanaCommand = new DelegateCommand(async () => await OnBorrarCampana(), () => CampanaSeleccionada != null);
            QuitarFiltroCampanaCommand = new DelegateCommand(() => CampanaSeleccionada = null, () => CampanaSeleccionada != null);
            NuevaOfertaProductoCommand = new DelegateCommand(OnNuevaOfertaProducto);
            GuardarOfertaProductoCommand = new DelegateCommand<object>(async (o) => await OnGuardarOfertaProducto(o as OfertaProductoWrapper));
            EliminarOfertaProductoCommand = new DelegateCommand<object>(async (o) => await OnEliminarOfertaProducto(o as OfertaProductoWrapper));

            NuevoDetalleCommand = new DelegateCommand(OnNuevoDetalle, () => OfertaCombinadaSeleccionada != null);
            NuevoDetalleAlternativoCommand = new DelegateCommand(OnNuevoDetalleAlternativo, () => DetalleSeleccionado != null);
            EliminarDetalleCommand = new DelegateCommand<object>(OnEliminarDetalle);

            Titulo = "Ofertas Combinadas";
            Empresa = Constantes.Empresas.EMPRESA_DEFECTO;
            _soloActivas = true;

            _ = CargarDatosIniciales();
        }

        private async Task CargarDatosIniciales()
        {
            await OnCargar(mostrarConfirmacion: false);
            await CargarSubgrupos();
        }

        // NestoAPI#289: subgrupos para el combo de las filas de filtro. La primera opción (en
        // blanco) deja la fila sin filtro de subgrupo; si la carga falla, el combo queda solo
        // con esa opción y la pantalla sigue siendo usable.
        private async Task CargarSubgrupos()
        {
            var opcionEnBlanco = new SubgrupoComboModel { Grupo = string.Empty, Subgrupo = string.Empty, Nombre = "(sin subgrupo)" };
            try
            {
                List<SubgrupoComboModel> subgrupos = await _service.GetSubgrupos() ?? new List<SubgrupoComboModel>();
                subgrupos.Insert(0, opcionEnBlanco);
                Subgrupos = subgrupos;
            }
            catch
            {
                Subgrupos = new List<SubgrupoComboModel> { opcionEnBlanco };
            }
        }

        #region Propiedades

        private string _empresa;
        public string Empresa
        {
            get => _empresa;
            set => SetProperty(ref _empresa, value);
        }

        private bool _estaCargando;
        public bool EstaCargando
        {
            get => _estaCargando;
            set => SetProperty(ref _estaCargando, value);
        }

        // NestoAPI#289: items del combo de subgrupos (compartido por todas las filas del grid).
        private List<SubgrupoComboModel> _subgrupos;
        public List<SubgrupoComboModel> Subgrupos
        {
            get => _subgrupos;
            set => SetProperty(ref _subgrupos, value);
        }

        private bool _soloActivas;
        public bool SoloActivas
        {
            get => _soloActivas;
            set
            {
                if (SetProperty(ref _soloActivas, value))
                {
                    _ = OnCargar(mostrarConfirmacion: true);
                }
            }
        }

        // Ofertas Combinadas (tab 1)
        private ObservableCollection<OfertaCombinadaWrapper> _ofertasCombinadas;
        public ObservableCollection<OfertaCombinadaWrapper> OfertasCombinadas
        {
            get => _ofertasCombinadas;
            set => SetProperty(ref _ofertasCombinadas, value);
        }

        private OfertaCombinadaWrapper _ofertaCombinadaSeleccionada;
        public OfertaCombinadaWrapper OfertaCombinadaSeleccionada
        {
            get => _ofertaCombinadaSeleccionada;
            set
            {
                if (SetProperty(ref _ofertaCombinadaSeleccionada, value))
                {
                    ((DelegateCommand)NuevoDetalleCommand).RaiseCanExecuteChanged();
                    CargarDetalles();
                }
            }
        }

        private ObservableCollection<DetalleOfertaCombinadaWrapper> _detallesOfertaSeleccionada;
        public ObservableCollection<DetalleOfertaCombinadaWrapper> DetallesOfertaSeleccionada
        {
            get => _detallesOfertaSeleccionada;
            set => SetProperty(ref _detallesOfertaSeleccionada, value);
        }

        private DetalleOfertaCombinadaWrapper _detalleSeleccionado;
        public DetalleOfertaCombinadaWrapper DetalleSeleccionado
        {
            get => _detalleSeleccionado;
            set
            {
                if (SetProperty(ref _detalleSeleccionado, value))
                {
                    ((DelegateCommand)NuevoDetalleAlternativoCommand).RaiseCanExecuteChanged();
                }
            }
        }

        // Ofertas por Familia (tab 2)
        private ObservableCollection<OfertaPermitidaFamiliaWrapper> _ofertasFamilia;
        public ObservableCollection<OfertaPermitidaFamiliaWrapper> OfertasFamilia
        {
            get => _ofertasFamilia;
            set => SetProperty(ref _ofertasFamilia, value);
        }

        // Ofertas Escalonadas (tab 3)
        private ObservableCollection<OfertaEscalonadaWrapper> _ofertasEscalonadas;
        public ObservableCollection<OfertaEscalonadaWrapper> OfertasEscalonadas
        {
            get => _ofertasEscalonadas;
            set => SetProperty(ref _ofertasEscalonadas, value);
        }

        private OfertaEscalonadaWrapper _ofertaEscalonadaSeleccionada;
        public OfertaEscalonadaWrapper OfertaEscalonadaSeleccionada
        {
            get => _ofertaEscalonadaSeleccionada;
            set
            {
                if (SetProperty(ref _ofertaEscalonadaSeleccionada, value))
                {
                    ((DelegateCommand)AnadirReferenciasCommand).RaiseCanExecuteChanged();
                    ((DelegateCommand)NuevoProductoEscalonadoCommand).RaiseCanExecuteChanged();
                    ((DelegateCommand)NuevoTramoCommand).RaiseCanExecuteChanged();
                }
            }
        }

        // Campañas (tab 4, NestoAPI#423)
        private ObservableCollection<CampanaWrapper> _campanas;
        public ObservableCollection<CampanaWrapper> Campanas
        {
            get => _campanas;
            set => SetProperty(ref _campanas, value);
        }

        // Las caducadas se ocultan por defecto: la lista es el histórico entero de campañas y
        // llenarla de las del año pasado esconde las que están vivas.
        private bool _incluirCampanasCaducadas;
        public bool IncluirCampanasCaducadas
        {
            get => _incluirCampanasCaducadas;
            set
            {
                if (SetProperty(ref _incluirCampanasCaducadas, value))
                {
                    _ = OnCargarCampanas();
                }
            }
        }

        // Los nombres de campana que existen, con sus recuentos. Llena el desplegable y, sobre
        // todo, ensena los numeros ANTES de operar en bloque.
        private ObservableCollection<ResumenCampanaModel> _resumenCampanas;
        public ObservableCollection<ResumenCampanaModel> ResumenCampanas
        {
            get => _resumenCampanas;
            set => SetProperty(ref _resumenCampanas, value);
        }

        private ResumenCampanaModel _campanaSeleccionada;
        public ResumenCampanaModel CampanaSeleccionada
        {
            get => _campanaSeleccionada;
            set
            {
                if (SetProperty(ref _campanaSeleccionada, value))
                {
                    // La rejilla tiene que ENSENAR lo que la operacion en bloque va a tocar. Sin
                    // esto, el boton de "Borrar campana" queda al lado de una lista con TODAS las
                    // filas, y da la impresion de que se las va a llevar todas por delante.
                    AplicarFiltroDeCampana();
                    ((DelegateCommand)CerrarCampanaCommand).RaiseCanExecuteChanged();
                    ((DelegateCommand)BorrarCampanaCommand).RaiseCanExecuteChanged();
                    ((DelegateCommand)QuitarFiltroCampanaCommand).RaiseCanExecuteChanged();
                    RaisePropertyChanged(nameof(HayFiltroDeCampana));
                    RaisePropertyChanged(nameof(TextoDelFiltro));
                }
            }
        }

        // Ofertas "6+2" de producto (pestana nueva)
        private ObservableCollection<OfertaProductoWrapper> _ofertasProducto;
        public ObservableCollection<OfertaProductoWrapper> OfertasProducto
        {
            get => _ofertasProducto;
            set => SetProperty(ref _ofertasProducto, value);
        }

        // Las caducadas se esconden por defecto: la lista es el historico entero y las del ano
        // pasado taparian las que estan vivas.
        private bool _incluirOfertasProductoCaducadas;
        public bool IncluirOfertasProductoCaducadas
        {
            get => _incluirOfertasProductoCaducadas;
            set
            {
                if (SetProperty(ref _incluirOfertasProductoCaducadas, value))
                {
                    _ = OnCargarOfertasProducto();
                }
            }
        }

        /// <summary>
        /// Todas las filas tal como llegaron del servidor. `Campanas` es lo que se ve, que puede
        /// ser un subconjunto si hay filtro; esta es la lista de la que se parte para filtrar sin
        /// tener que volver a pedirlas.
        /// </summary>
        private readonly List<CampanaWrapper> _todasLasCampanas = new List<CampanaWrapper>();

        public bool HayFiltroDeCampana => CampanaSeleccionada != null;

        /// <summary>Lo que se ensena junto al boton de quitar el filtro, para que se vea que la
        /// rejilla NO esta mostrando todo.</summary>
        public string TextoDelFiltro => CampanaSeleccionada == null
            ? string.Empty
            : $"Viendo solo la campana '{CampanaSeleccionada.Campana}'";

        // Por defecto se ven TODOS los descuentos de tarifa, no solo los que llevan fechas o
        // audiencia. Los que hay que mantener hoy (las rebajas metidas antes de que existieran las
        // campanas) no llevan ni lo uno ni lo otro: esconderlos obligaria a seguir borrandolos por
        // SQL, que es justo lo que esta pantalla viene a evitar.
        private bool _soloCampanas;
        public bool SoloCampanas
        {
            get => _soloCampanas;
            set
            {
                if (SetProperty(ref _soloCampanas, value))
                {
                    _ = OnCargarCampanas();
                }
            }
        }


        /// <summary>
        /// NestoAPI#423: a quien se le publica el descuento en la tienda. El 3 ("solo publico") NO
        /// esta y no es un olvido: lo prohibe CK_DescuentosProducto_Audiencia, porque el motor de
        /// precios no mira la audiencia y le descontaria igual al profesional en el pedido — la
        /// tienda diria una cosa y Nesto cobraria otra.
        /// </summary>
        public List<AudienciaCampanaOpcion> AudienciasCampana { get; } = new List<AudienciaCampanaOpcion>
        {
            new AudienciaCampanaOpcion { Valor = 0, Texto = "No va a la web" },
            new AudienciaCampanaOpcion { Valor = 1, Texto = "Solo profesionales" },
            new AudienciaCampanaOpcion { Valor = 2, Texto = "Profesionales y publico" }
        };

        // Texto pegado con las referencias separadas por comas, espacios o saltos de línea.
        private string _referenciasTexto;
        public string ReferenciasTexto
        {
            get => _referenciasTexto;
            set => SetProperty(ref _referenciasTexto, value);
        }

        #endregion

        #region Comandos

        public ICommand CargarCommand { get; }
        public ICommand NuevaOfertaCombinadaCommand { get; }
        public ICommand GuardarOfertaCombinadaCommand { get; }
        public ICommand EliminarOfertaCombinadaCommand { get; }
        public ICommand NuevoDetalleCommand { get; }
        public ICommand NuevoDetalleAlternativoCommand { get; }
        public ICommand EliminarDetalleCommand { get; }

        public ICommand NuevaOfertaFamiliaCommand { get; }
        public ICommand GuardarOfertaFamiliaCommand { get; }
        public ICommand EliminarOfertaFamiliaCommand { get; }

        public ICommand NuevaOfertaEscalonadaCommand { get; }
        public ICommand GuardarOfertaEscalonadaCommand { get; }
        public ICommand EliminarOfertaEscalonadaCommand { get; }
        public ICommand AnadirReferenciasCommand { get; }
        public ICommand NuevoProductoEscalonadoCommand { get; }
        public ICommand EliminarProductoEscalonadoCommand { get; }
        public ICommand NuevoTramoCommand { get; }
        public ICommand EliminarTramoCommand { get; }

        public ICommand NuevaCampanaCommand { get; }
        public ICommand GuardarCampanaCommand { get; }
        public ICommand EliminarCampanaCommand { get; }
        public ICommand CerrarCampanaCommand { get; }
        public ICommand BorrarCampanaCommand { get; }
        public ICommand QuitarFiltroCampanaCommand { get; }

        public ICommand NuevaOfertaProductoCommand { get; }
        public ICommand GuardarOfertaProductoCommand { get; }
        public ICommand EliminarOfertaProductoCommand { get; }

        public event NuevaOfertaCombinadaCreadaHandler NuevaOfertaCombinadaCreada;
        public event NuevaOfertaFamiliaCreadaHandler NuevaOfertaFamiliaCreada;
        public event NuevaOfertaEscalonadaCreadaHandler NuevaOfertaEscalonadaCreada;
        public event NuevaCampanaCreadaHandler NuevaCampanaCreada;
        public event NuevaOfertaProductoCreadaHandler NuevaOfertaProductoCreada;

        #endregion

        #region Ofertas Combinadas

        private async Task OnCargar(bool mostrarConfirmacion = true)
        {
            if (mostrarConfirmacion && TieneCambiosSinGuardar())
            {
                var confirmacion = _dialogService.ShowConfirmationAnswer(
                    "Cambios sin guardar",
                    "Hay cambios sin guardar que se perderan. Desea continuar?");
                if (!confirmacion) return;
            }

            try
            {
                EstaCargando = true;
                OfertasCombinadas.Clear();
                OfertasFamilia.Clear();
                OfertasEscalonadas.Clear();
                Campanas.Clear();
                OfertasProducto.Clear();
                DetallesOfertaSeleccionada = null;
                OfertaEscalonadaSeleccionada = null;

                var ofertas = await _service.GetOfertasCombinadas(Empresa, SoloActivas);
                foreach (var item in ofertas.OrderByDescending(o => o.Id))
                {
                    OfertasCombinadas.Add(new OfertaCombinadaWrapper(item));
                }

                var ofertasFamilia = await _service.GetOfertasPermitidasFamilia(Empresa);
                foreach (var item in ofertasFamilia.OrderBy(o => o.Familia).ThenBy(o => o.FiltroProducto))
                {
                    OfertasFamilia.Add(new OfertaPermitidaFamiliaWrapper(item));
                }

                var ofertasEscalonadas = await _service.GetOfertasEscalonadas(Empresa, SoloActivas);
                foreach (var item in (ofertasEscalonadas ?? new List<OfertaEscalonadaModel>()).OrderByDescending(o => o.Id))
                {
                    OfertasEscalonadas.Add(new OfertaEscalonadaWrapper(item));
                }

                await CargarCampanas();
                await CargarOfertasProducto();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private bool TieneCambiosSinGuardar()
        {
            return OfertasCombinadas.Any(o => o.HaCambiado || o.Id == 0)
                || OfertasFamilia.Any(o => o.HaCambiado || o.NOrden == 0)
                || OfertasEscalonadas.Any(o => o.HaCambiado || o.Id == 0)
                || Campanas.Any(o => o.HaCambiado || o.Id == 0)
                || OfertasProducto.Any(o => o.HaCambiado || o.NOrden == 0);
        }

        private void OnNuevaOfertaCombinada()
        {
            var nuevo = new OfertaCombinadaWrapper();
            OfertasCombinadas.Add(nuevo);
            OfertaCombinadaSeleccionada = nuevo;
            NuevaOfertaCombinadaCreada?.Invoke(nuevo);
        }

        private void CargarDetalles()
        {
            if (OfertaCombinadaSeleccionada == null)
            {
                DetallesOfertaSeleccionada = null;
                return;
            }
            DetallesOfertaSeleccionada = OfertaCombinadaSeleccionada.Detalles;
        }

        private void OnNuevoDetalle()
        {
            if (OfertaCombinadaSeleccionada == null) return;

            var detalle = new DetalleOfertaCombinadaWrapper();
            OfertaCombinadaSeleccionada.Detalles.Add(detalle);
            OfertaCombinadaSeleccionada.HaCambiado = true;
        }

        // Añade una alternativa intercambiable a la línea seleccionada: una nueva línea en su
        // mismo grupo (heredando cantidad y precio). Si la seleccionada aún no tenía grupo, se le
        // asigna uno nuevo y pasa a ser la primera alternativa. Ej.: camiseta de regalo en
        // cualquier talla → cada talla es una alternativa del mismo grupo.
        private void OnNuevoDetalleAlternativo()
        {
            if (OfertaCombinadaSeleccionada == null || DetalleSeleccionado == null) return;

            // Los grupos de alternativas son solo de producto concreto (limitación de NestoAPI#282).
            if (DetalleSeleccionado.EsFiltro)
            {
                _dialogService.ShowError("Las líneas de filtro no pueden pertenecer a un grupo de alternativas.");
                return;
            }

            int grupo = DetalleSeleccionado.GrupoAlternativa ?? SiguienteGrupoAlternativa();
            DetalleSeleccionado.GrupoAlternativa = grupo;

            var alternativa = new DetalleOfertaCombinadaWrapper
            {
                Cantidad = DetalleSeleccionado.Cantidad,
                Precio = DetalleSeleccionado.Precio,
                GrupoAlternativa = grupo
            };
            OfertaCombinadaSeleccionada.Detalles.Add(alternativa);
            OfertaCombinadaSeleccionada.HaCambiado = true;
            DetalleSeleccionado = alternativa;
        }

        private int SiguienteGrupoAlternativa()
        {
            var gruposExistentes = OfertaCombinadaSeleccionada.Detalles
                .Where(d => d.GrupoAlternativa.HasValue)
                .Select(d => d.GrupoAlternativa.Value)
                .ToList();
            return gruposExistentes.Count == 0 ? 1 : gruposExistentes.Max() + 1;
        }

        private void OnEliminarDetalle(object parameter)
        {
            if (parameter is not DetalleOfertaCombinadaWrapper detalle) return;
            if (OfertaCombinadaSeleccionada == null) return;

            OfertaCombinadaSeleccionada.Detalles.Remove(detalle);
            OfertaCombinadaSeleccionada.HaCambiado = true;
        }

        private async Task OnGuardarOfertaCombinada(OfertaCombinadaWrapper oferta)
        {
            if (oferta == null) return;

            if (string.IsNullOrWhiteSpace(oferta.Nombre))
            {
                _dialogService.ShowError("Debe introducir un nombre para la oferta.");
                return;
            }

            if (oferta.Detalles.Count == 0)
            {
                _dialogService.ShowError("Una oferta combinada debe tener al menos un producto.");
                return;
            }

            // Se admiten ofertas de un solo producto (p. ej. 2ª unidad al 50 %): varias
            // líneas con precio, o una sola línea con el precio total en el importe mínimo.
            // Una sola línea sin importe mínimo no la podría autorizar el validador de precios,
            // SALVO con "Regalo menor importe" (NestoAPI#289/#290): el suelo dinámico la hace
            // autorizable — es la config natural del 2+1 por filtro de subgrupo.
            if (oferta.Detalles.Count == 1 && oferta.ImporteMinimo <= 0 && !oferta.RegalarMenorImporte)
            {
                _dialogService.ShowError("Una oferta combinada de una sola línea debe tener un importe mínimo mayor que cero.");
                return;
            }

            // NestoAPI#292: mismas reglas que el servidor para las unidades regaladas.
            if (oferta.UnidadesRegaladas > 1 && !oferta.RegalarMenorImporte)
            {
                _dialogService.ShowError("Para regalar más de una unidad, la oferta debe tener marcado 'Regalo menor imp.' (sin la regla, el regalo se expresa con líneas a precio 0).");
                return;
            }
            if (oferta.RegalarMenorImporte && oferta.UnidadesPorInstancia > 0 && oferta.UnidadesRegaladas >= oferta.UnidadesPorInstancia)
            {
                _dialogService.ShowError($"Las unidades regaladas ({oferta.UnidadesRegaladas}) deben ser menores que el total de unidades de la oferta ({oferta.UnidadesPorInstancia}): al menos una unidad debe cobrarse.");
                return;
            }

            // NestoAPI#282: cada línea es de producto concreto O de filtro (familia, prefijo del
            // nombre, grupo y/o subgrupo — NestoAPI#289), nunca ambas cosas ni ninguna. Mismas
            // reglas que valida el servidor.
            foreach (var d in oferta.Detalles)
            {
                bool tieneProducto = !string.IsNullOrWhiteSpace(d.Producto);
                bool tieneFiltro = !string.IsNullOrWhiteSpace(d.Familia) || !string.IsNullOrWhiteSpace(d.FiltroProducto)
                    || !string.IsNullOrWhiteSpace(d.Grupo) || !string.IsNullOrWhiteSpace(d.Subgrupo);
                if (!tieneProducto && !tieneFiltro)
                {
                    _dialogService.ShowError("Cada línea debe llevar un producto o un filtro (familia, principio del nombre y/o subgrupo).");
                    return;
                }
                if (tieneProducto && tieneFiltro)
                {
                    _dialogService.ShowError($"La línea del producto '{d.Producto.Trim()}' no puede llevar también familia o filtro: una línea es de producto concreto O de filtro.");
                    return;
                }
                if (!tieneProducto && d.GrupoAlternativa.HasValue)
                {
                    _dialogService.ShowError("Las líneas de filtro no pueden pertenecer a un grupo de alternativas.");
                    return;
                }
            }

            try
            {
                EstaCargando = true;

                var createModel = new OfertaCombinadaCreateModel
                {
                    Empresa = Empresa,
                    Nombre = oferta.Nombre,
                    ImporteMinimo = oferta.ImporteMinimo,
                    FechaDesde = oferta.FechaDesde,
                    FechaHasta = oferta.FechaHasta,
                    RegalarMenorImporte = oferta.RegalarMenorImporte,
                    UnidadesRegaladas = oferta.UnidadesRegaladas,
                    Detalles = oferta.Detalles.Select(d => new OfertaCombinadaDetalleCreateModel
                    {
                        Id = d.Id,
                        // Null (no cadena vacía) en las filas de filtro: el servidor distingue
                        // fila de producto y fila de filtro por Producto == null.
                        Producto = string.IsNullOrWhiteSpace(d.Producto) ? null : d.Producto.Trim(),
                        Familia = string.IsNullOrWhiteSpace(d.Familia) ? null : d.Familia.Trim(),
                        FiltroProducto = string.IsNullOrWhiteSpace(d.FiltroProducto) ? null : d.FiltroProducto.Trim(),
                        Grupo = string.IsNullOrWhiteSpace(d.Grupo) ? null : d.Grupo.Trim(),
                        Subgrupo = string.IsNullOrWhiteSpace(d.Subgrupo) ? null : d.Subgrupo.Trim(),
                        Cantidad = d.Cantidad,
                        Precio = d.Precio,
                        GrupoAlternativa = d.GrupoAlternativa,
                        PermitirCantidadMenor = d.PermitirCantidadMenor
                    }).ToList()
                };

                OfertaCombinadaModel resultado;
                if (oferta.Id == 0)
                {
                    resultado = await _service.CreateOfertaCombinada(createModel);
                    _dialogService.ShowNotification($"Oferta combinada '{resultado.Nombre}' creada");
                }
                else
                {
                    resultado = await _service.UpdateOfertaCombinada(oferta.Id, createModel);
                    _dialogService.ShowNotification($"Oferta combinada '{resultado.Nombre}' actualizada");
                }

                oferta.ActualizarDesdeServidor(resultado);
                CargarDetalles();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private async Task OnEliminarOfertaCombinada(OfertaCombinadaWrapper oferta)
        {
            if (oferta == null) return;

            if (oferta.Id == 0)
            {
                OfertasCombinadas.Remove(oferta);
                return;
            }

            var confirmacion = _dialogService.ShowConfirmationAnswer(
                "Eliminar oferta combinada",
                $"Se eliminara la oferta '{oferta.Nombre}' y todos sus productos. Continuar?");
            if (!confirmacion) return;

            try
            {
                EstaCargando = true;
                await _service.DeleteOfertaCombinada(oferta.Id);
                OfertasCombinadas.Remove(oferta);
                _dialogService.ShowNotification("Oferta combinada eliminada");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        #endregion

        #region Ofertas Escalonadas

        private void OnNuevaOfertaEscalonada()
        {
            var nuevo = new OfertaEscalonadaWrapper();
            OfertasEscalonadas.Add(nuevo);
            OfertaEscalonadaSeleccionada = nuevo;
            NuevaOfertaEscalonadaCreada?.Invoke(nuevo);
        }

        /// <summary>
        /// Convierte el texto pegado (separado por comas, puntos y coma, espacios, tabuladores o
        /// saltos de línea — lo típico de un Excel o un correo) en la lista de referencias, sin
        /// vacíos ni duplicados.
        /// </summary>
        public static List<string> ParsearReferencias(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return new List<string>();
            }
            return texto
                .Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(r => r.Trim())
                .Where(r => r.Length > 0)
                .Distinct()
                .ToList();
        }

        private async Task OnAnadirReferencias()
        {
            if (OfertaEscalonadaSeleccionada == null) return;

            var referencias = ParsearReferencias(ReferenciasTexto);
            if (referencias.Count == 0) return;

            var yaExistentes = OfertaEscalonadaSeleccionada.Productos
                .Select(p => p.Producto?.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToHashSet();

            try
            {
                EstaCargando = true;
                foreach (var referencia in referencias.Where(r => !yaExistentes.Contains(r)))
                {
                    var wrapper = new OfertaEscalonadaProductoWrapper { Producto = referencia };
                    try
                    {
                        // Sin cliente/contacto devuelve el precio de tarifa, que es el precio base
                        // de la oferta (editable). Las referencias que no existan se marcan en rojo.
                        var producto = await _servicioProducto.BuscarProducto(Empresa, referencia, null, null, 1);
                        if (producto != null)
                        {
                            wrapper.Producto = producto.Producto?.Trim() ?? referencia;
                            wrapper.ProductoNombre = producto.Nombre;
                            wrapper.PrecioBase = producto.Precio;
                        }
                        else
                        {
                            wrapper.NoEncontrado = true;
                            wrapper.ProductoNombre = "*** NO EXISTE ***";
                        }
                    }
                    catch
                    {
                        wrapper.NoEncontrado = true;
                        wrapper.ProductoNombre = "*** NO EXISTE ***";
                    }
                    OfertaEscalonadaSeleccionada.AnadirProducto(wrapper);
                }

                ReferenciasTexto = string.Empty;
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private void OnNuevoProductoEscalonado()
        {
            if (OfertaEscalonadaSeleccionada == null) return;
            OfertaEscalonadaSeleccionada.AnadirProducto(new OfertaEscalonadaProductoWrapper());
        }

        private void OnEliminarProductoEscalonado(object parameter)
        {
            if (parameter is not OfertaEscalonadaProductoWrapper producto) return;
            if (OfertaEscalonadaSeleccionada == null) return;

            OfertaEscalonadaSeleccionada.Productos.Remove(producto);
            OfertaEscalonadaSeleccionada.HaCambiado = true;
        }

        private void OnNuevoTramo()
        {
            if (OfertaEscalonadaSeleccionada == null) return;

            // Sugerimos continuar la escala: una unidad más que el último tramo.
            short siguienteCantidad = (short)(OfertaEscalonadaSeleccionada.Tramos
                .Select(t => (int)t.CantidadMinima)
                .DefaultIfEmpty(1)
                .Max() + 1);
            OfertaEscalonadaSeleccionada.AnadirTramo(new OfertaEscalonadaTramoWrapper { CantidadMinima = siguienteCantidad });
        }

        private void OnEliminarTramo(object parameter)
        {
            if (parameter is not OfertaEscalonadaTramoWrapper tramo) return;
            if (OfertaEscalonadaSeleccionada == null) return;

            OfertaEscalonadaSeleccionada.Tramos.Remove(tramo);
            OfertaEscalonadaSeleccionada.HaCambiado = true;
        }

        private async Task OnGuardarOfertaEscalonada(OfertaEscalonadaWrapper oferta)
        {
            if (oferta == null) return;

            if (string.IsNullOrWhiteSpace(oferta.Nombre))
            {
                _dialogService.ShowError("Debe introducir un nombre para la oferta.");
                return;
            }

            if (!oferta.Productos.Any(p => !string.IsNullOrWhiteSpace(p.Producto)))
            {
                _dialogService.ShowError("Una oferta escalonada debe tener al menos un producto.");
                return;
            }

            if (oferta.Tramos.Count == 0)
            {
                _dialogService.ShowError("Una oferta escalonada debe tener al menos un tramo (cantidad mínima y descuento).");
                return;
            }

            try
            {
                EstaCargando = true;

                var createModel = new OfertaEscalonadaCreateModel
                {
                    Empresa = Empresa,
                    Nombre = oferta.Nombre,
                    FechaDesde = oferta.FechaDesde,
                    FechaHasta = oferta.FechaHasta,
                    Productos = oferta.Productos
                        .Where(p => !string.IsNullOrWhiteSpace(p.Producto))
                        .Select(p => new OfertaEscalonadaProductoCreateModel
                        {
                            Id = p.Id,
                            Producto = p.Producto?.Trim(),
                            PrecioBase = p.PrecioBase
                        }).ToList(),
                    Tramos = oferta.Tramos.Select(t => new OfertaEscalonadaTramoCreateModel
                    {
                        Id = t.Id,
                        CantidadMinima = t.CantidadMinima,
                        Descuento = t.DescuentoPorcentaje / 100m
                    }).ToList()
                };

                OfertaEscalonadaModel resultado;
                if (oferta.Id == 0)
                {
                    resultado = await _service.CreateOfertaEscalonada(createModel);
                    _dialogService.ShowNotification($"Oferta escalonada '{resultado.Nombre}' creada");
                }
                else
                {
                    resultado = await _service.UpdateOfertaEscalonada(oferta.Id, createModel);
                    _dialogService.ShowNotification($"Oferta escalonada '{resultado.Nombre}' actualizada");
                }

                oferta.ActualizarDesdeServidor(resultado);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private async Task OnEliminarOfertaEscalonada(OfertaEscalonadaWrapper oferta)
        {
            if (oferta == null) return;

            if (oferta.Id == 0)
            {
                OfertasEscalonadas.Remove(oferta);
                return;
            }

            var confirmacion = _dialogService.ShowConfirmationAnswer(
                "Eliminar oferta escalonada",
                $"Se eliminara la oferta '{oferta.Nombre}' con todos sus productos y tramos. Continuar?");
            if (!confirmacion) return;

            try
            {
                EstaCargando = true;
                await _service.DeleteOfertaEscalonada(oferta.Id);
                OfertasEscalonadas.Remove(oferta);
                _dialogService.ShowNotification("Oferta escalonada eliminada");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaCargando = false;
            }
        }


        /// <summary>
        /// Cerrar una campana entera: le pone fecha de fin y deja de aplicarse. Es la operacion
        /// PREFERIBLE al borrado —deja traza de que hubo campana, permite consultarla despues y se
        /// deshace quitando la fecha—, asi que es la que se ofrece primero.
        /// </summary>
        private async Task OnCerrarCampana()
        {
            ResumenCampanaModel campana = CampanaSeleccionada;
            if (campana == null) return;

            bool confirmacion = _dialogService.ShowConfirmationAnswer(
                "Cerrar campana",
                $"Se pondra fecha de fin a las {campana.Filas} filas de '{campana.Campana}', " +
                $"de las que {campana.FilasQueViajan} se anuncian en la tienda. " +
                "Dejaran de aplicarse en los pedidos y se retiraran de la web. Continuar?");
            if (!confirmacion) return;

            try
            {
                EstaCargando = true;
                ResultadoOperacionCampanaModel resultado = await _service.CerrarCampana(campana.Campana);
                _dialogService.ShowNotification(
                    $"Campana cerrada: {resultado.FilasAfectadas} filas, {resultado.ProductosEncolados} productos a republicar");
                await CargarCampanas();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        /// <summary>
        /// Borrar la campana entera. Es lo que el 31/08/2026 hubo que hacer con un DELETE por SQL
        /// sobre una ventana de cinco minutos del reloj. Se avisa de que no hay vuelta atras porque
        /// aqui NO la hay: las filas desaparecen.
        /// </summary>
        private async Task OnBorrarCampana()
        {
            ResumenCampanaModel campana = CampanaSeleccionada;
            if (campana == null) return;

            bool confirmacion = _dialogService.ShowConfirmationAnswer(
                "Borrar campana",
                $"Se BORRARAN las {campana.Filas} filas de '{campana.Campana}'. Esto no se puede deshacer. " +
                "Si solo quieres que deje de aplicarse, usa Cerrar campana, que si es reversible. Continuar?");
            if (!confirmacion) return;

            try
            {
                EstaCargando = true;
                ResultadoOperacionCampanaModel resultado = await _service.DeleteCampanaPorNombre(campana.Campana);
                _dialogService.ShowNotification(
                    $"Campana borrada: {resultado.FilasAfectadas} filas, {resultado.ProductosEncolados} productos a republicar");
                CampanaSeleccionada = null;
                await CargarCampanas();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        #endregion

        #region Ofertas de producto ("6+2")

        private async Task OnCargarOfertasProducto()
        {
            try
            {
                EstaCargando = true;
                await CargarOfertasProducto();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private async Task CargarOfertasProducto()
        {
            OfertasProducto.Clear();
            var ofertas = await _service.GetOfertasProducto(IncluirOfertasProductoCaducadas);
            foreach (var item in ofertas ?? new List<OfertaProductoModel>())
            {
                OfertasProducto.Add(new OfertaProductoWrapper(item));
            }
        }

        // 6+2 de salida: es con diferencia la oferta mas comun, y es la que pedia el correo que
        // origino esta pestana.
        private void OnNuevaOfertaProducto()
        {
            var nuevo = new OfertaProductoWrapper { CantidadConPrecio = 6, CantidadRegalo = 2 };
            OfertasProducto.Add(nuevo);
            NuevaOfertaProductoCreada?.Invoke(nuevo);
        }

        private async Task OnGuardarOfertaProducto(OfertaProductoWrapper oferta)
        {
            if (oferta == null) return;

            // Solo lo que se ve a simple vista: los solapes de fechas y que el producto exista
            // los comprueba la API, que es la unica que puede.
            if (string.IsNullOrWhiteSpace(oferta.Producto))
            {
                _dialogService.ShowError("Debe introducir el producto.");
                return;
            }
            if (oferta.CantidadConPrecio < 1 || oferta.CantidadRegalo < 1)
            {
                _dialogService.ShowError("Las cantidades tienen que ser al menos 1.");
                return;
            }

            try
            {
                EstaCargando = true;
                OfertaProductoModel aGuardar = oferta.AModelo();

                OfertaProductoModel resultado = oferta.NOrden == 0
                    ? await _service.CreateOfertaProducto(aGuardar)
                    : await _service.UpdateOfertaProducto(oferta.NOrden, aGuardar);

                oferta.ActualizarDesdeServidor(resultado);
                _dialogService.ShowNotification($"Oferta de {resultado.Producto} guardada");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private async Task OnEliminarOfertaProducto(OfertaProductoWrapper oferta)
        {
            if (oferta == null) return;

            if (oferta.NOrden == 0)
            {
                OfertasProducto.Remove(oferta);
                return;
            }

            bool confirmacion = _dialogService.ShowConfirmationAnswer(
                "Eliminar oferta",
                $"Se eliminara la oferta {oferta.Resumen} del producto {oferta.Producto}. " +
                "Los pedidos que la lleven dejaran de estar autorizados. Continuar?");
            if (!confirmacion) return;

            try
            {
                EstaCargando = true;
                await _service.DeleteOfertaProducto(oferta.NOrden);
                OfertasProducto.Remove(oferta);
                _dialogService.ShowNotification("Oferta eliminada");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        #endregion

        #region Ofertas por Familia

        private void OnNuevaOfertaFamilia()
        {
            var nuevo = new OfertaPermitidaFamiliaWrapper { CantidadConPrecio = 6, CantidadRegalo = 1 };
            OfertasFamilia.Add(nuevo);
            NuevaOfertaFamiliaCreada?.Invoke(nuevo);
        }

        private async Task OnGuardarOfertaFamilia(OfertaPermitidaFamiliaWrapper oferta)
        {
            if (oferta == null) return;

            if (string.IsNullOrWhiteSpace(oferta.Familia))
            {
                _dialogService.ShowError("Debe introducir una familia.");
                return;
            }

            try
            {
                EstaCargando = true;

                var createModel = new OfertaPermitidaFamiliaCreateModel
                {
                    Empresa = Empresa,
                    Familia = oferta.Familia?.Trim(),
                    CantidadConPrecio = oferta.CantidadConPrecio,
                    CantidadRegalo = oferta.CantidadRegalo,
                    FiltroProducto = oferta.FiltroProducto
                };

                OfertaPermitidaFamiliaModel resultado;
                if (oferta.NOrden == 0)
                {
                    resultado = await _service.CreateOfertaPermitidaFamilia(createModel);
                    _dialogService.ShowNotification($"Oferta por familia '{resultado.Familia}' creada");
                }
                else
                {
                    resultado = await _service.UpdateOfertaPermitidaFamilia(oferta.NOrden, createModel);
                    _dialogService.ShowNotification($"Oferta por familia '{resultado.Familia}' actualizada");
                }

                oferta.ActualizarDesdeServidor(resultado);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private async Task OnEliminarOfertaFamilia(OfertaPermitidaFamiliaWrapper oferta)
        {
            if (oferta == null) return;

            if (oferta.NOrden == 0)
            {
                OfertasFamilia.Remove(oferta);
                return;
            }

            var confirmacion = _dialogService.ShowConfirmationAnswer(
                "Eliminar oferta por familia",
                $"Se eliminara la oferta de familia '{oferta.Familia}'. Continuar?");
            if (!confirmacion) return;

            try
            {
                EstaCargando = true;
                await _service.DeleteOfertaPermitidaFamilia(oferta.NOrden);
                OfertasFamilia.Remove(oferta);
                _dialogService.ShowNotification("Oferta por familia eliminada");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        #endregion

        #region Campanas (NestoAPI#423)

        // Se recarga sola al cambiar el filtro de caducadas, sin tocar el resto de pestanas: es
        // una consulta distinta al servidor, no un filtro en memoria.
        private async Task OnCargarCampanas()
        {
            try
            {
                EstaCargando = true;
                await CargarCampanas();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private async Task CargarCampanas()
        {
            ResumenCampanas.Clear();
            foreach (var resumen in await _service.GetNombresDeCampana() ?? new List<ResumenCampanaModel>())
            {
                ResumenCampanas.Add(resumen);
            }

            _todasLasCampanas.Clear();
            var campanas = await _service.GetCampanas(IncluirCampanasCaducadas, SoloCampanas);
            foreach (var item in campanas ?? new List<CampanaModel>())
            {
                _todasLasCampanas.Add(new CampanaWrapper(item));
            }

            // Si la campana que estaba filtrada ya no existe (se acaba de borrar), se quita el
            // filtro solo: dejarlo puesto ensenaria una rejilla vacia sin explicar por que.
            if (CampanaSeleccionada != null &&
                !ResumenCampanas.Any(r => string.Equals(r.Campana, CampanaSeleccionada.Campana, StringComparison.OrdinalIgnoreCase)))
            {
                CampanaSeleccionada = null;
            }

            AplicarFiltroDeCampana();
        }

        /// <summary>
        /// Deja en `Campanas` lo que el usuario tiene que ver: todo, o solo las filas de la
        /// campana elegida.
        /// </summary>
        private void AplicarFiltroDeCampana()
        {
            Campanas.Clear();

            IEnumerable<CampanaWrapper> aMostrar = CampanaSeleccionada == null
                ? _todasLasCampanas
                : _todasLasCampanas.Where(c => string.Equals(c.Campana, CampanaSeleccionada.Campana, StringComparison.OrdinalIgnoreCase));

            foreach (CampanaWrapper campana in aMostrar)
            {
                Campanas.Add(campana);
            }
        }

        private void OnNuevaCampana()
        {
            // Audiencia 2 (profesional y publico) y vigente desde hoy: es lo que se quiere el 99 %
            // de las veces, y lo que menos sorprende si alguien guarda sin mirar.
            var nuevo = new CampanaWrapper
            {
                AudienciaOferta = 2,
                FechaDesde = DateTime.Today
            };
            _todasLasCampanas.Add(nuevo);
            Campanas.Add(nuevo);
            NuevaCampanaCreada?.Invoke(nuevo);
        }

        private async Task OnGuardarCampana(CampanaWrapper campana)
        {
            if (campana == null) return;

            // Las validaciones de verdad estan en la API (es la unica que puede comprobar solapes
            // y niveles del motor de precios). Aqui solo se atajan las dos que se ven a simple
            // vista, para no gastar una llamada en decir lo obvio.
            bool tieneProducto = !string.IsNullOrWhiteSpace(campana.Producto);
            bool tieneFamilia = !string.IsNullOrWhiteSpace(campana.Familia);
            if (tieneProducto == tieneFamilia)
            {
                _dialogService.ShowError("La campana tiene que ser de un producto O de una familia, no de las dos cosas ni de ninguna.");
                return;
            }
            if (campana.DescuentoPorcentaje <= 0)
            {
                _dialogService.ShowError("El descuento tiene que ser mayor que cero.");
                return;
            }

            try
            {
                EstaCargando = true;

                var aGuardar = campana.AModelo();

                CampanaModel resultado = campana.Id == 0
                    ? await _service.CreateCampana(aGuardar)
                    : await _service.UpdateCampana(campana.Id, aGuardar);

                campana.ActualizarDesdeServidor(resultado);
                _dialogService.ShowNotification(campana.Id == 0
                    ? "Campana creada"
                    : "Campana guardada. Los productos se republicaran en los proximos minutos");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private async Task OnEliminarCampana(CampanaWrapper campana)
        {
            if (campana == null) return;

            if (campana.Id == 0)
            {
                _ = _todasLasCampanas.Remove(campana);
                _ = Campanas.Remove(campana);
                return;
            }

            var confirmacion = _dialogService.ShowConfirmationAnswer(
                "Eliminar campana",
                $"Se eliminara la campana de '{campana.Ambito}'. Los productos volveran a su precio normal en la tienda. Continuar?");
            if (!confirmacion) return;

            try
            {
                EstaCargando = true;
                await _service.DeleteCampana(campana.Id);
                _ = _todasLasCampanas.Remove(campana);
                _ = Campanas.Remove(campana);
                _dialogService.ShowNotification("Campana eliminada. Los productos se republicaran en los proximos minutos");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        #endregion
    }

    #region Wrappers

    public class OfertaCombinadaWrapper : BindableBase
    {
        private bool _rastreandoCambios = true;

        public OfertaCombinadaWrapper()
        {
            Detalles = new ObservableCollection<DetalleOfertaCombinadaWrapper>();
        }

        public OfertaCombinadaWrapper(OfertaCombinadaModel model)
        {
            _rastreandoCambios = false;
            Id = model.Id;
            Nombre = model.Nombre;
            ImporteMinimo = model.ImporteMinimo;
            FechaDesde = model.FechaDesde;
            FechaHasta = model.FechaHasta;
            RegalarMenorImporte = model.RegalarMenorImporte;
            UnidadesRegaladas = model.UnidadesRegaladas < 1 ? (short)1 : model.UnidadesRegaladas;
            Usuario = model.Usuario;
            FechaModificacion = model.FechaModificacion;
            Detalles = new ObservableCollection<DetalleOfertaCombinadaWrapper>(
                (model.Detalles ?? new List<OfertaCombinadaDetalleModel>())
                    .Select(d => new DetalleOfertaCombinadaWrapper(d)));
            _rastreandoCambios = true;
            HaCambiado = false;
        }

        public void ActualizarDesdeServidor(OfertaCombinadaModel model)
        {
            _rastreandoCambios = false;
            Id = model.Id;
            Nombre = model.Nombre;
            ImporteMinimo = model.ImporteMinimo;
            FechaDesde = model.FechaDesde;
            FechaHasta = model.FechaHasta;
            RegalarMenorImporte = model.RegalarMenorImporte;
            UnidadesRegaladas = model.UnidadesRegaladas < 1 ? (short)1 : model.UnidadesRegaladas;
            Usuario = model.Usuario;
            FechaModificacion = model.FechaModificacion;
            Detalles = new ObservableCollection<DetalleOfertaCombinadaWrapper>(
                (model.Detalles ?? new List<OfertaCombinadaDetalleModel>())
                    .Select(d => new DetalleOfertaCombinadaWrapper(d)));
            _rastreandoCambios = true;
            HaCambiado = false;
        }

        public int Id { get; set; }

        private string _nombre;
        public string Nombre
        {
            get => _nombre;
            set { if (SetProperty(ref _nombre, value) && _rastreandoCambios) HaCambiado = true; }
        }

        private decimal _importeMinimo;
        public decimal ImporteMinimo
        {
            get => _importeMinimo;
            set { if (SetProperty(ref _importeMinimo, value) && _rastreandoCambios) HaCambiado = true; }
        }

        private DateTime? _fechaDesde;
        public DateTime? FechaDesde
        {
            get => _fechaDesde;
            set { if (SetProperty(ref _fechaDesde, value) && _rastreandoCambios) HaCambiado = true; }
        }

        private DateTime? _fechaHasta;
        public DateTime? FechaHasta
        {
            get => _fechaHasta;
            set { if (SetProperty(ref _fechaHasta, value) && _rastreandoCambios) HaCambiado = true; }
        }

        // NestoAPI#290: la unidad a base 0 debe ser la de menor tarifa del conjunto y las pagadas
        // cubrir su tarifa. Por defecto true en ofertas nuevas; desmarcar para promos que regalan
        // a propósito un artículo más caro que lo comprado (p. ej. la de Lisap con el aparato).
        private bool _regalarMenorImporte = true;
        public bool RegalarMenorImporte
        {
            get => _regalarMenorImporte;
            set
            {
                if (SetProperty(ref _regalarMenorImporte, value) && _rastreandoCambios) HaCambiado = true;
                RaisePropertyChanged(nameof(ResumenOferta));
            }
        }

        // NestoAPI#292: el usuario piensa en "3+2" (3 cobradas + 2 regaladas). Regaladas se
        // persiste (UnidadesRegaladas); Cobradas se deriva del total de unidades por instancia
        // y, al editarla, ajusta sola la cantidad de la fila en la config natural (un grupo de
        // alternativas o una única fila). De cada oferta salen gratis las Regaladas unidades
        // MÁS BARATAS del conjunto y las cobradas van a su tarifa.
        private short _unidadesRegaladas = 1;
        public short UnidadesRegaladas
        {
            get => _unidadesRegaladas;
            set
            {
                short valor = value < 1 ? (short)1 : value;
                int cobradasAntes = UnidadesCobradas;
                if (SetProperty(ref _unidadesRegaladas, valor) && _rastreandoCambios) HaCambiado = true;
                // Al cambiar las regaladas se conservan las cobradas (3+1 → 3+2 pasa el total
                // de 4 a 5), que es como piensa el usuario.
                if (cobradasAntes > 0)
                {
                    UnidadesCobradas = cobradasAntes;
                }
                RaisePropertyChanged(nameof(UnidadesCobradas));
                RaisePropertyChanged(nameof(ResumenOferta));
            }
        }

        // Total de unidades por instancia: la cantidad de cada grupo de alternativas (una vez
        // por grupo, la cantidad es del grupo) más las cantidades de las filas sueltas.
        public int UnidadesPorInstancia => Detalles == null
            ? 0
            : Detalles.Where(d => d.GrupoAlternativa.HasValue)
                  .GroupBy(d => d.GrupoAlternativa.Value)
                  .Sum(g => (int)g.First().Cantidad)
              + Detalles.Where(d => !d.GrupoAlternativa.HasValue).Sum(d => (int)d.Cantidad);

        public int UnidadesCobradas
        {
            get => Math.Max(UnidadesPorInstancia - UnidadesRegaladas, 0);
            set
            {
                // Editable en la config natural (un único grupo de alternativas o una única
                // fila): la cantidad de la fila se pone sola a cobradas + regaladas. En configs
                // complejas, las cantidades se editan en las propias filas.
                if (value < 1 || Detalles == null)
                {
                    return;
                }
                short total = (short)(value + UnidadesRegaladas);
                var grupos = Detalles.Where(d => d.GrupoAlternativa.HasValue).GroupBy(d => d.GrupoAlternativa.Value).ToList();
                var sueltas = Detalles.Where(d => !d.GrupoAlternativa.HasValue).ToList();
                if (grupos.Count == 1 && sueltas.Count == 0)
                {
                    foreach (var d in grupos[0])
                    {
                        d.Cantidad = total;
                    }
                }
                else if (grupos.Count == 0 && sueltas.Count == 1)
                {
                    sueltas[0].Cantidad = total;
                }
                RaisePropertyChanged(nameof(UnidadesCobradas));
                RaisePropertyChanged(nameof(ResumenOferta));
            }
        }

        // Columna informativa "3+2" para leer la oferta de un vistazo.
        public string ResumenOferta => RegalarMenorImporte && UnidadesPorInstancia > 0
            ? $"{UnidadesCobradas}+{UnidadesRegaladas}"
            : string.Empty;

        public string Usuario { get; set; }
        public DateTime FechaModificacion { get; set; }

        private ObservableCollection<DetalleOfertaCombinadaWrapper> _detalles;
        public ObservableCollection<DetalleOfertaCombinadaWrapper> Detalles
        {
            get => _detalles;
            set
            {
                // Enganchamos cada detalle (y los que se añadan después) para que cualquier edición
                // de una línea marque la oferta como cambiada y aparezca el botón Guardar.
                if (_detalles != null)
                {
                    _detalles.CollectionChanged -= DetallesCollectionChanged;
                }
                SetProperty(ref _detalles, value);
                if (_detalles != null)
                {
                    foreach (var detalle in _detalles)
                    {
                        Vincular(detalle);
                    }
                    _detalles.CollectionChanged += DetallesCollectionChanged;
                }
            }
        }

        // Añadir o quitar líneas también es un cambio; y las líneas nuevas hay que engancharlas.
        private void DetallesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (DetalleOfertaCombinadaWrapper detalle in e.NewItems)
                {
                    Vincular(detalle);
                }
            }
            if (_rastreandoCambios)
            {
                HaCambiado = true;
            }
        }

        private void Vincular(DetalleOfertaCombinadaWrapper detalle)
        {
            detalle.AlCambiar = () =>
            {
                if (_rastreandoCambios) HaCambiado = true;
                // NestoAPI#292: editar cantidades en las filas también mueve el "3+2" de la cabecera.
                RaisePropertyChanged(nameof(UnidadesCobradas));
                RaisePropertyChanged(nameof(ResumenOferta));
            };
        }

        private bool _haCambiado;
        public bool HaCambiado
        {
            get => _haCambiado;
            set => SetProperty(ref _haCambiado, value);
        }
    }

    public class DetalleOfertaCombinadaWrapper : BindableBase
    {
        public DetalleOfertaCombinadaWrapper() { }

        public DetalleOfertaCombinadaWrapper(OfertaCombinadaDetalleModel model)
        {
            Id = model.Id;
            Producto = model.Producto;
            ProductoNombre = model.ProductoNombre;
            Familia = model.Familia;
            FiltroProducto = model.FiltroProducto;
            Grupo = model.Grupo;
            Subgrupo = model.Subgrupo;
            Cantidad = model.Cantidad;
            Precio = model.Precio;
            GrupoAlternativa = model.GrupoAlternativa;
            PermitirCantidadMenor = model.PermitirCantidadMenor;
        }

        // La oferta engancha aquí para enterarse de cualquier edición del detalle y marcarse como
        // cambiada (mostrar el botón Guardar). Lo asigna OfertaCombinadaWrapper al vincular el detalle.
        internal Action AlCambiar { get; set; }

        public int Id { get; set; }

        private string _producto;
        public string Producto
        {
            get => _producto;
            set { if (SetProperty(ref _producto, value)) AlCambiar?.Invoke(); }
        }

        private string _productoNombre;
        public string ProductoNombre
        {
            get => _productoNombre;
            set
            {
                if (SetProperty(ref _productoNombre, value))
                {
                    RaisePropertyChanged(nameof(texto));
                }
            }
        }

        public string texto
        {
            get => ProductoNombre;
            set => ProductoNombre = value;
        }

        // NestoAPI#282: fila de FILTRO. Con el producto vacío, la línea casa las líneas del pedido
        // por familia y/o prefijo del nombre, y la cantidad se cuenta agregada entre todas.
        private string _familia;
        public string Familia
        {
            get => _familia;
            set { if (SetProperty(ref _familia, value)) AlCambiar?.Invoke(); }
        }

        private string _filtroProducto;
        public string FiltroProducto
        {
            get => _filtroProducto;
            set { if (SetProperty(ref _filtroProducto, value)) AlCambiar?.Invoke(); }
        }

        // NestoAPI#289: el filtro también puede casar por Grupo/Subgrupo del producto (AND con
        // familia/prefijo). Se editan juntos desde el combo de subgrupos (GrupoSubgrupoClave).
        private string _grupo;
        public string Grupo
        {
            get => _grupo;
            set
            {
                if (SetProperty(ref _grupo, value))
                {
                    RaisePropertyChanged(nameof(GrupoSubgrupoClave));
                    AlCambiar?.Invoke();
                }
            }
        }

        private string _subgrupo;
        public string Subgrupo
        {
            get => _subgrupo;
            set
            {
                if (SetProperty(ref _subgrupo, value))
                {
                    RaisePropertyChanged(nameof(GrupoSubgrupoClave));
                    AlCambiar?.Invoke();
                }
            }
        }

        // SelectedValue del combo de subgrupos: "Grupo|Subgrupo" ("|" = opción en blanco).
        // El separador evita ambigüedad si el grupo tiene menos de 3 letras.
        public string GrupoSubgrupoClave
        {
            get => $"{Grupo?.Trim()}|{Subgrupo?.Trim()}";
            set
            {
                string[] partes = (value ?? "|").Split('|');
                Grupo = partes.Length > 0 && !string.IsNullOrWhiteSpace(partes[0]) ? partes[0].Trim() : null;
                Subgrupo = partes.Length > 1 && !string.IsNullOrWhiteSpace(partes[1]) ? partes[1].Trim() : null;
            }
        }

        public bool EsFiltro => string.IsNullOrWhiteSpace(Producto)
            && (!string.IsNullOrWhiteSpace(Familia) || !string.IsNullOrWhiteSpace(FiltroProducto)
                || !string.IsNullOrWhiteSpace(Grupo) || !string.IsNullOrWhiteSpace(Subgrupo));

        private short _cantidad;
        public short Cantidad
        {
            get => _cantidad;
            set { if (SetProperty(ref _cantidad, value)) AlCambiar?.Invoke(); }
        }

        private decimal _precio;
        public decimal Precio
        {
            get => _precio;
            set { if (SetProperty(ref _precio, value)) AlCambiar?.Invoke(); }
        }

        // Líneas con el mismo GrupoAlternativa son intercambiables ("elige 1"); null = obligatoria.
        private int? _grupoAlternativa;
        public int? GrupoAlternativa
        {
            get => _grupoAlternativa;
            set { if (SetProperty(ref _grupoAlternativa, value)) AlCambiar?.Invoke(); }
        }

        // Si true, Cantidad es un MÁXIMO: el pedido puede llevar de 0 a Cantidad (extra opcional).
        private bool _permitirCantidadMenor;
        public bool PermitirCantidadMenor
        {
            get => _permitirCantidadMenor;
            set { if (SetProperty(ref _permitirCantidadMenor, value)) AlCambiar?.Invoke(); }
        }
    }

    public class OfertaEscalonadaWrapper : BindableBase
    {
        private bool _rastreandoCambios = true;

        public OfertaEscalonadaWrapper()
        {
            Productos = new ObservableCollection<OfertaEscalonadaProductoWrapper>();
            Tramos = new ObservableCollection<OfertaEscalonadaTramoWrapper>();
        }

        public OfertaEscalonadaWrapper(OfertaEscalonadaModel model)
        {
            CargarDesdeModelo(model);
        }

        public void ActualizarDesdeServidor(OfertaEscalonadaModel model)
        {
            CargarDesdeModelo(model);
        }

        private void CargarDesdeModelo(OfertaEscalonadaModel model)
        {
            _rastreandoCambios = false;
            Id = model.Id;
            Nombre = model.Nombre;
            FechaDesde = model.FechaDesde;
            FechaHasta = model.FechaHasta;
            Usuario = model.Usuario;
            FechaModificacion = model.FechaModificacion;
            Productos = new ObservableCollection<OfertaEscalonadaProductoWrapper>(
                (model.Productos ?? new List<OfertaEscalonadaProductoModel>())
                    .Select(p => Vincular(new OfertaEscalonadaProductoWrapper(p))));
            Tramos = new ObservableCollection<OfertaEscalonadaTramoWrapper>(
                (model.Tramos ?? new List<OfertaEscalonadaTramoModel>())
                    .OrderBy(t => t.CantidadMinima)
                    .Select(t => Vincular(new OfertaEscalonadaTramoWrapper(t))));
            _rastreandoCambios = true;
            HaCambiado = false;
        }

        // Las ediciones dentro de los grids hijos marcan la oferta como cambiada para que
        // aparezca el botón Guardar.
        private OfertaEscalonadaProductoWrapper Vincular(OfertaEscalonadaProductoWrapper producto)
        {
            producto.AlCambiar = () => { if (_rastreandoCambios) HaCambiado = true; };
            return producto;
        }

        private OfertaEscalonadaTramoWrapper Vincular(OfertaEscalonadaTramoWrapper tramo)
        {
            tramo.AlCambiar = () => { if (_rastreandoCambios) HaCambiado = true; };
            return tramo;
        }

        public void AnadirProducto(OfertaEscalonadaProductoWrapper producto)
        {
            Productos.Add(Vincular(producto));
            HaCambiado = true;
        }

        public void AnadirTramo(OfertaEscalonadaTramoWrapper tramo)
        {
            Tramos.Add(Vincular(tramo));
            HaCambiado = true;
        }

        public int Id { get; set; }

        private string _nombre;
        public string Nombre
        {
            get => _nombre;
            set { if (SetProperty(ref _nombre, value) && _rastreandoCambios) HaCambiado = true; }
        }

        private DateTime? _fechaDesde;
        public DateTime? FechaDesde
        {
            get => _fechaDesde;
            set { if (SetProperty(ref _fechaDesde, value) && _rastreandoCambios) HaCambiado = true; }
        }

        private DateTime? _fechaHasta;
        public DateTime? FechaHasta
        {
            get => _fechaHasta;
            set { if (SetProperty(ref _fechaHasta, value) && _rastreandoCambios) HaCambiado = true; }
        }

        // Con notificación (a diferencia del resto de wrappers): tras guardar, ActualizarDesdeServidor
        // rellena usuario y fecha en la fila recién creada y el grid debe refrescarlos sin recargar.
        private string _usuario;
        public string Usuario
        {
            get => _usuario;
            set => SetProperty(ref _usuario, value);
        }

        private DateTime _fechaModificacion;
        public DateTime FechaModificacion
        {
            get => _fechaModificacion;
            set => SetProperty(ref _fechaModificacion, value);
        }

        private ObservableCollection<OfertaEscalonadaProductoWrapper> _productos;
        public ObservableCollection<OfertaEscalonadaProductoWrapper> Productos
        {
            get => _productos;
            set => SetProperty(ref _productos, value);
        }

        private ObservableCollection<OfertaEscalonadaTramoWrapper> _tramos;
        public ObservableCollection<OfertaEscalonadaTramoWrapper> Tramos
        {
            get => _tramos;
            set => SetProperty(ref _tramos, value);
        }

        private bool _haCambiado;
        public bool HaCambiado
        {
            get => _haCambiado;
            set => SetProperty(ref _haCambiado, value);
        }
    }

    public class OfertaEscalonadaProductoWrapper : BindableBase
    {
        public OfertaEscalonadaProductoWrapper() { }

        public OfertaEscalonadaProductoWrapper(OfertaEscalonadaProductoModel model)
        {
            Id = model.Id;
            Producto = model.Producto;
            ProductoNombre = model.ProductoNombre;
            PrecioBase = model.PrecioBase;
        }

        internal Action AlCambiar { get; set; }

        public int Id { get; set; }

        private string _producto;
        public string Producto
        {
            get => _producto;
            set
            {
                if (SetProperty(ref _producto, value))
                {
                    // Al corregir la referencia a mano se quita la marca de "no existe"; si sigue
                    // sin existir, el ProductoBehavior del grid lo vuelve a señalar al validar.
                    NoEncontrado = false;
                    AlCambiar?.Invoke();
                }
            }
        }

        private string _productoNombre;
        public string ProductoNombre
        {
            get => _productoNombre;
            set
            {
                if (SetProperty(ref _productoNombre, value))
                {
                    RaisePropertyChanged(nameof(texto));
                }
            }
        }

        public string texto
        {
            get => ProductoNombre;
            set => ProductoNombre = value;
        }

        // Null = al guardar, el servidor precarga el PVP de ficha del producto.
        private decimal? _precioBase;
        public decimal? PrecioBase
        {
            get => _precioBase;
            set { if (SetProperty(ref _precioBase, value)) AlCambiar?.Invoke(); }
        }

        // La referencia pegada no existe en la empresa: se pinta en rojo para que el usuario la
        // corrija o la quite (si se guarda tal cual, el servidor la rechaza indicando cuál es).
        private bool _noEncontrado;
        public bool NoEncontrado
        {
            get => _noEncontrado;
            set => SetProperty(ref _noEncontrado, value);
        }
    }

    public class OfertaEscalonadaTramoWrapper : BindableBase
    {
        public OfertaEscalonadaTramoWrapper() { }

        public OfertaEscalonadaTramoWrapper(OfertaEscalonadaTramoModel model)
        {
            Id = model.Id;
            CantidadMinima = model.CantidadMinima;
            DescuentoPorcentaje = model.Descuento * 100m;
        }

        internal Action AlCambiar { get; set; }

        public int Id { get; set; }

        private short _cantidadMinima;
        public short CantidadMinima
        {
            get => _cantidadMinima;
            set { if (SetProperty(ref _cantidadMinima, value)) AlCambiar?.Invoke(); }
        }

        // El usuario teclea el porcentaje (25 = 25 %); el API trabaja en tanto por uno.
        private decimal _descuentoPorcentaje;
        public decimal DescuentoPorcentaje
        {
            get => _descuentoPorcentaje;
            set { if (SetProperty(ref _descuentoPorcentaje, value)) AlCambiar?.Invoke(); }
        }
    }

    public class OfertaPermitidaFamiliaWrapper : BindableBase
    {
        private bool _rastreandoCambios = true;

        public OfertaPermitidaFamiliaWrapper() { }

        public OfertaPermitidaFamiliaWrapper(OfertaPermitidaFamiliaModel model)
        {
            _rastreandoCambios = false;
            NOrden = model.NOrden;
            Familia = model.Familia;
            FamiliaDescripcion = model.FamiliaDescripcion;
            CantidadConPrecio = model.CantidadConPrecio;
            CantidadRegalo = model.CantidadRegalo;
            FiltroProducto = model.FiltroProducto;
            Usuario = model.Usuario;
            FechaModificacion = model.FechaModificacion;
            _rastreandoCambios = true;
            HaCambiado = false;
        }

        public void ActualizarDesdeServidor(OfertaPermitidaFamiliaModel model)
        {
            _rastreandoCambios = false;
            NOrden = model.NOrden;
            Familia = model.Familia;
            FamiliaDescripcion = model.FamiliaDescripcion;
            CantidadConPrecio = model.CantidadConPrecio;
            CantidadRegalo = model.CantidadRegalo;
            FiltroProducto = model.FiltroProducto;
            Usuario = model.Usuario;
            FechaModificacion = model.FechaModificacion;
            _rastreandoCambios = true;
            HaCambiado = false;
        }

        public int NOrden { get; set; }

        private string _familia;
        public string Familia
        {
            get => _familia;
            set { if (SetProperty(ref _familia, value) && _rastreandoCambios) HaCambiado = true; }
        }

        private string _familiaDescripcion;
        public string FamiliaDescripcion
        {
            get => _familiaDescripcion;
            set => SetProperty(ref _familiaDescripcion, value);
        }

        private short _cantidadConPrecio;
        public short CantidadConPrecio
        {
            get => _cantidadConPrecio;
            set { if (SetProperty(ref _cantidadConPrecio, value) && _rastreandoCambios) HaCambiado = true; }
        }

        private short _cantidadRegalo;
        public short CantidadRegalo
        {
            get => _cantidadRegalo;
            set { if (SetProperty(ref _cantidadRegalo, value) && _rastreandoCambios) HaCambiado = true; }
        }

        private string _filtroProducto;
        public string FiltroProducto
        {
            get => _filtroProducto;
            set { if (SetProperty(ref _filtroProducto, value) && _rastreandoCambios) HaCambiado = true; }
        }

        public string Usuario { get; set; }
        public DateTime FechaModificacion { get; set; }

        private bool _haCambiado;
        public bool HaCambiado
        {
            get => _haCambiado;
            set => SetProperty(ref _haCambiado, value);
        }
    }

    /// <summary>
    /// NestoAPI#423: una campana en la rejilla. El usuario teclea PORCENTAJES (20 = 20 %) porque
    /// es como se habla de una campana; la tabla y la API trabajan en tanto por uno (0,20). La
    /// conversion vive aqui, en un solo sitio: meter un 20 en la columna equivocada seria un
    /// 2.000 % de descuento.
    /// </summary>
    public class CampanaWrapper : BindableBase
    {
        private bool _rastreandoCambios = true;

        public CampanaWrapper() { }

        public CampanaWrapper(CampanaModel model)
        {
            _rastreandoCambios = false;
            Volcar(model);
            _rastreandoCambios = true;
            HaCambiado = false;
        }

        public void ActualizarDesdeServidor(CampanaModel model)
        {
            _rastreandoCambios = false;
            Volcar(model);
            _rastreandoCambios = true;
            HaCambiado = false;
        }

        private void Volcar(CampanaModel model)
        {
            Id = model.Id;
            Producto = model.Producto;
            Familia = model.Familia;
            Grupo = model.Grupo;
            DescuentoPorcentaje = model.Descuento * 100M;
            DescuentoPublicoPorcentaje = model.DescuentoPublico.HasValue
                ? model.DescuentoPublico.Value * 100M
                : (decimal?)null;
            AudienciaOferta = model.AudienciaOferta;
            FechaDesde = model.FechaDesde;
            FechaHasta = model.FechaHasta;
            Campana = model.Campana;
            Vigente = model.Vigente;
            Usuario = model.Usuario;
            FechaModificacion = model.FechaModificacion;
        }

        public CampanaModel AModelo()
        {
            return new CampanaModel
            {
                Id = Id,
                Producto = string.IsNullOrWhiteSpace(Producto) ? null : Producto.Trim(),
                Familia = string.IsNullOrWhiteSpace(Familia) ? null : Familia.Trim(),
                Grupo = string.IsNullOrWhiteSpace(Grupo) ? null : Grupo.Trim(),
                Descuento = DescuentoPorcentaje / 100M,
                DescuentoPublico = DescuentoPublicoPorcentaje.HasValue
                    ? DescuentoPublicoPorcentaje.Value / 100M
                    : (decimal?)null,
                AudienciaOferta = AudienciaOferta,
                FechaDesde = FechaDesde,
                FechaHasta = FechaHasta,
                Campana = string.IsNullOrWhiteSpace(Campana) ? null : Campana.Trim()
            };
        }

        public int Id { get; set; }

        /// <summary>A que se aplica, para los mensajes y para ordenar de un vistazo.</summary>
        public string Ambito => !string.IsNullOrWhiteSpace(Familia)
            ? (string.IsNullOrWhiteSpace(Grupo) ? Familia : $"{Familia} / {Grupo}")
            : Producto;

        private string _producto;
        public string Producto
        {
            get => _producto;
            set { if (SetProperty(ref _producto, value) && _rastreandoCambios) HaCambiado = true; RaisePropertyChanged(nameof(Ambito)); }
        }

        private string _familia;
        public string Familia
        {
            get => _familia;
            set { if (SetProperty(ref _familia, value) && _rastreandoCambios) HaCambiado = true; RaisePropertyChanged(nameof(Ambito)); }
        }

        private string _grupo;
        public string Grupo
        {
            get => _grupo;
            set { if (SetProperty(ref _grupo, value) && _rastreandoCambios) HaCambiado = true; RaisePropertyChanged(nameof(Ambito)); }
        }

        // El usuario teclea 20 para un 20 %.
        private decimal _descuentoPorcentaje;
        public decimal DescuentoPorcentaje
        {
            get => _descuentoPorcentaje;
            set { if (SetProperty(ref _descuentoPorcentaje, value) && _rastreandoCambios) HaCambiado = true; }
        }

        // Vacio = el publico se lleva el mismo porcentaje que el profesional.
        private decimal? _descuentoPublicoPorcentaje;
        public decimal? DescuentoPublicoPorcentaje
        {
            get => _descuentoPublicoPorcentaje;
            set { if (SetProperty(ref _descuentoPublicoPorcentaje, value) && _rastreandoCambios) HaCambiado = true; }
        }

        private byte _audienciaOferta;
        public byte AudienciaOferta
        {
            get => _audienciaOferta;
            set { if (SetProperty(ref _audienciaOferta, value) && _rastreandoCambios) HaCambiado = true; }
        }

        private DateTime? _fechaDesde;
        public DateTime? FechaDesde
        {
            get => _fechaDesde;
            set { if (SetProperty(ref _fechaDesde, value) && _rastreandoCambios) HaCambiado = true; }
        }

        private DateTime? _fechaHasta;
        public DateTime? FechaHasta
        {
            get => _fechaHasta;
            set { if (SetProperty(ref _fechaHasta, value) && _rastreandoCambios) HaCambiado = true; }
        }

        private string _campana;
        public string Campana
        {
            get => _campana;
            set { if (SetProperty(ref _campana, value) && _rastreandoCambios) HaCambiado = true; }
        }

        private bool _vigente;
        public bool Vigente
        {
            get => _vigente;
            set => SetProperty(ref _vigente, value);
        }

        public string Usuario { get; set; }
        public DateTime FechaModificacion { get; set; }

        private bool _haCambiado;
        public bool HaCambiado
        {
            get => _haCambiado;
            set => SetProperty(ref _haCambiado, value);
        }
    }

    /// <summary>NestoAPI#423: una opcion del desplegable de audiencia de las campanas.</summary>
    public class AudienciaCampanaOpcion
    {
        public byte Valor { get; set; }
        public string Texto { get; set; }
    }

    /// <summary>
    /// Una oferta "6+2" en la rejilla. A diferencia de las campanas, aqui no hay conversion de
    /// unidades: las cantidades son las que son.
    /// </summary>
    public class OfertaProductoWrapper : BindableBase
    {
        private bool _rastreandoCambios = true;

        public OfertaProductoWrapper() { }

        public OfertaProductoWrapper(OfertaProductoModel model)
        {
            _rastreandoCambios = false;
            Volcar(model);
            _rastreandoCambios = true;
            HaCambiado = false;
        }

        public void ActualizarDesdeServidor(OfertaProductoModel model)
        {
            _rastreandoCambios = false;
            Volcar(model);
            _rastreandoCambios = true;
            HaCambiado = false;
        }

        private void Volcar(OfertaProductoModel model)
        {
            NOrden = model.NOrden;
            Producto = model.Producto;
            ProductoNombre = model.ProductoNombre;
            CantidadConPrecio = model.CantidadConPrecio;
            CantidadRegalo = model.CantidadRegalo;
            Denegar = model.Denegar;
            FiltroProducto = model.FiltroProducto;
            FechaDesde = model.FechaDesde;
            FechaHasta = model.FechaHasta;
            Vigente = model.Vigente;
            Usuario = model.Usuario;
            FechaModificacion = model.FechaModificacion;
        }

        public OfertaProductoModel AModelo()
        {
            return new OfertaProductoModel
            {
                NOrden = NOrden,
                Producto = Producto?.Trim(),
                CantidadConPrecio = CantidadConPrecio,
                CantidadRegalo = CantidadRegalo,
                Denegar = Denegar,
                FiltroProducto = string.IsNullOrWhiteSpace(FiltroProducto) ? null : FiltroProducto.Trim(),
                FechaDesde = FechaDesde,
                FechaHasta = FechaHasta
            };
        }

        public int NOrden { get; set; }

        /// <summary>Como se lee la oferta: "6+2".</summary>
        public string Resumen => $"{CantidadConPrecio}+{CantidadRegalo}";

        private string _producto;
        public string Producto
        {
            get => _producto;
            set { if (SetProperty(ref _producto, value) && _rastreandoCambios) HaCambiado = true; }
        }

        private string _productoNombre;
        public string ProductoNombre
        {
            get => _productoNombre;
            set => SetProperty(ref _productoNombre, value);
        }

        private short _cantidadConPrecio;
        public short CantidadConPrecio
        {
            get => _cantidadConPrecio;
            set { if (SetProperty(ref _cantidadConPrecio, value) && _rastreandoCambios) HaCambiado = true; RaisePropertyChanged(nameof(Resumen)); }
        }

        private short _cantidadRegalo;
        public short CantidadRegalo
        {
            get => _cantidadRegalo;
            set { if (SetProperty(ref _cantidadRegalo, value) && _rastreandoCambios) HaCambiado = true; RaisePropertyChanged(nameof(Resumen)); }
        }

        private bool _denegar;
        public bool Denegar
        {
            get => _denegar;
            set { if (SetProperty(ref _denegar, value) && _rastreandoCambios) HaCambiado = true; }
        }

        private string _filtroProducto;
        public string FiltroProducto
        {
            get => _filtroProducto;
            set { if (SetProperty(ref _filtroProducto, value) && _rastreandoCambios) HaCambiado = true; }
        }

        private DateTime? _fechaDesde;
        public DateTime? FechaDesde
        {
            get => _fechaDesde;
            set { if (SetProperty(ref _fechaDesde, value) && _rastreandoCambios) HaCambiado = true; }
        }

        private DateTime? _fechaHasta;
        public DateTime? FechaHasta
        {
            get => _fechaHasta;
            set { if (SetProperty(ref _fechaHasta, value) && _rastreandoCambios) HaCambiado = true; }
        }

        private bool _vigente;
        public bool Vigente
        {
            get => _vigente;
            set => SetProperty(ref _vigente, value);
        }

        public string Usuario { get; set; }
        public DateTime FechaModificacion { get; set; }

        private bool _haCambiado;
        public bool HaCambiado
        {
            get => _haCambiado;
            set => SetProperty(ref _haCambiado, value);
        }
    }

    #endregion
}
