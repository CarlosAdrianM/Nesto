using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// NestoAPI#591: mantenimiento de eventos (cursos, masterclass…) con señal: título, fecha, importe de la señal y si está
    /// activo.
    /// <para>Nesto#517 (NestoAPI#611, decisiones de Carlos del 14/10/26): cada evento es de una tienda (Reina o Alcobendas) y
    /// lo mantiene esa tienda (grupo Tiendas), Administración y Dirección; los que no tienen tienda, Tienda online. Lleva la
    /// referencia del curso, plazas, lugar y descripción. Del evento seleccionado se ven los inscritos: inscribir (sin pedido),
    /// anular, marcar pagada a mano (tarjeta, transferencia o efectivo), pasar lista y relanzar la facturación del curso. Los
    /// permisos finos (qué tienda puede con qué evento) los vuelve a comprobar la API.</para>
    /// </summary>
    public class MantenimientoEventosViewModel : ObservableObject, IReceptorNavegacion
    {
        private readonly IEventosService _servicio;
        private readonly IServicioDialogos _dialogService;

        public MantenimientoEventosViewModel(IEventosService servicio, IConfiguracion configuracion, IServicioDialogos dialogService)
        {
            _servicio = servicio;
            Configuracion = configuracion;
            _dialogService = dialogService;
            Titulo = "Eventos";
            Tiendas = new List<TiendaEvento>
            {
                new(TiendaEvento.REINA, "Reina"),
                new(TiendaEvento.ALCOBENDAS, "Alcobendas")
            };
            // Carlos 22/10/26: el formato es Presencial u Online (webinar); la tienda es obligatoria en los dos.
            Formatos = new List<OpcionEvento>
            {
                new(EventoModel.FORMATO_PRESENCIAL, "Presencial"),
                new(EventoModel.FORMATO_ONLINE, "Online (webinar)")
            };
            _formatoEdicion = Formatos[0];
            FormasPago = MarcarPagoInscripcionModel.FORMAS_PAGO.ToList();
            CargarCommand = new AsyncRelayCommand(CargarAsync);
            NuevoCommand = new RelayCommand(OnNuevo);
            GuardarCommand = new AsyncRelayCommand(GuardarAsync, () => Editando);
            ComprobarProductoCommand = new AsyncRelayCommand(ComprobarProductoAsync, () => !string.IsNullOrWhiteSpace(ProductoCursoEdicion));
            CrearProductoCursoCommand = new AsyncRelayCommand(CrearProductoCursoAsync, () => Editando && _idEdicion > 0 && !TieneReferenciaForzada);
            InscribirCommand = new AsyncRelayCommand(InscribirAsync, () => Seleccionado?.Id > 0);
            AnularInscripcionCommand = new AsyncRelayCommand(AnularInscripcionAsync, () => InscripcionSeleccionada?.SePuedeAnular == true);
            ConfirmaAsistenciaCommand = new AsyncRelayCommand(() => ConfirmarAsistenciaAsync(AsistenciaConfirmadaModel.CONFIRMA),
                () => InscripcionSeleccionada?.SePuedeConfirmarAsistencia == true);
            NoPodraIrCommand = new AsyncRelayCommand(() => ConfirmarAsistenciaAsync(AsistenciaConfirmadaModel.NO_PODRA_IR),
                () => InscripcionSeleccionada?.SePuedeConfirmarAsistencia == true);
            SubirImagenCommand = new AsyncRelayCommand(SubirImagenAsync, () => Editando && _idEdicion > 0);
            QuitarImagenCommand = new AsyncRelayCommand(QuitarImagenAsync, () => Editando && _idEdicion > 0 && !string.IsNullOrWhiteSpace(ImagenUrl));
            MarcarPagadaCommand = new AsyncRelayCommand(MarcarPagadaAsync, () => InscripcionSeleccionada?.SePuedeMarcarPagada == true);
            FacturarCommand = new AsyncRelayCommand(FacturarAsync, () => InscripcionSeleccionada?.SePuedeFacturar == true);
            GuardarAsistenciaCommand = new AsyncRelayCommand(GuardarAsistenciaAsync, () => SePuedePasarLista);
            RecargarInscripcionesCommand = new AsyncRelayCommand(CargarInscripcionesAsync, () => Seleccionado?.Id > 0);
        }

        public IConfiguracion Configuracion { get; }

        public string Titulo { get; }

        /// <summary>
        /// Quién ve el mantenimiento: Tienda online (eventos sin tienda), las tiendas, Administración, Dirección e Informática.
        /// Qué evento puede tocar cada uno lo decide la API (403 con el motivo).
        /// </summary>
        public static bool PuedeMantener(IConfiguracion configuracion) =>
            configuracion != null && (configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDA_ON_LINE)
                || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)
                || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)
                || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)
                || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA));

        public bool TieneAcceso => PuedeMantener(Configuracion);

        public void AlLlegar(ParametrosNavegacion parametros)
        {
            if (!Eventos.Any())
            {
                _ = CargarAsync();
            }
        }

        /// <summary>El AlmacénPedidoVta del usuario: si es REI o ALC, los eventos nuevos nacen en su tienda.</summary>
        private string _almacenUsuario;

        public List<TiendaEvento> Tiendas { get; }
        public List<OpcionEvento> Formatos { get; }

        private OpcionEvento _formatoEdicion;
        /// <summary>Presencial (por defecto) u Online (webinar).</summary>
        public OpcionEvento FormatoEdicion { get => _formatoEdicion; set => SetProperty(ref _formatoEdicion, value); }

        private OpcionEvento FormatoPorValor(string valor) =>
            Formatos.FirstOrDefault(f => string.Equals(f.Valor, valor?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Formatos[0];
        public List<string> FormasPago { get; }

        private ObservableCollection<EventoModel> _eventos = new();
        public ObservableCollection<EventoModel> Eventos
        {
            get => _eventos;
            private set => SetProperty(ref _eventos, value);
        }

        private EventoModel _seleccionado;
        public EventoModel Seleccionado
        {
            get => _seleccionado;
            set
            {
                if (SetProperty(ref _seleccionado, value))
                {
                    InscribirCommand.NotifyCanExecuteChanged();
                    RecargarInscripcionesCommand.NotifyCanExecuteChanged();
                    OnPropertyChanged(nameof(SePuedePasarLista));
                    OnPropertyChanged(nameof(AvisoListaAsistencia));
                    GuardarAsistenciaCommand.NotifyCanExecuteChanged();
                    if (value != null)
                    {
                        CargarEdicion(value);
                        _ = CargarInscripcionesAsync();
                    }
                    else
                    {
                        Inscripciones = new ObservableCollection<InscripcionEventoModel>();
                    }
                }
            }
        }

        // ---------------- Edición del evento ----------------
        // Una copia del seleccionado (o un evento nuevo); la fila no se toca hasta guardar con éxito.
        private int _idEdicion;

        private bool _editando;
        public bool Editando
        {
            get => _editando;
            private set
            {
                if (SetProperty(ref _editando, value))
                {
                    GuardarCommand.NotifyCanExecuteChanged();
                    SubirImagenCommand?.NotifyCanExecuteChanged();
                    CrearProductoCursoCommand?.NotifyCanExecuteChanged();
                    QuitarImagenCommand?.NotifyCanExecuteChanged();
                }
            }
        }

        public bool EsNuevo => Editando && _idEdicion == 0;

        private string _tituloEdicion;
        public string TituloEdicion { get => _tituloEdicion; set => SetProperty(ref _tituloEdicion, value); }

        private DateTime? _fechaEdicion;
        public DateTime? FechaEdicion { get => _fechaEdicion; set => SetProperty(ref _fechaEdicion, value); }

        private decimal _importeSenalEdicion;
        public decimal ImporteSenalEdicion { get => _importeSenalEdicion; set => SetProperty(ref _importeSenalEdicion, value); }

        private bool _activoEdicion = true;
        public bool ActivoEdicion { get => _activoEdicion; set => SetProperty(ref _activoEdicion, value); }

        private TiendaEvento _tiendaEdicion;
        public TiendaEvento TiendaEdicion { get => _tiendaEdicion; set => SetProperty(ref _tiendaEdicion, value); }

        private string _productoCursoEdicion;
        public string ProductoCursoEdicion
        {
            get => _productoCursoEdicion;
            set
            {
                if (SetProperty(ref _productoCursoEdicion, value))
                {
                    NombreProductoCurso = null;
                    ComprobarProductoCommand.NotifyCanExecuteChanged();
                    CrearProductoCursoCommand?.NotifyCanExecuteChanged();
                    OnPropertyChanged(nameof(TieneReferenciaForzada));
                }
            }
        }

        /// <summary>«EVENTO00007»: la referencia propia del evento (reembolsable); con «NR», la no reembolsable.</summary>
        public static string ReferenciaPropia(int idEvento) => $"EVENTO{idEvento:D5}";

        /// <summary>
        /// Hay escrita una referencia de curso que no es la propia del evento (p. ej. 90077, que pueden compartir varios
        /// eventos): esa sirve para todas las modalidades, así que «Crear referencia» no hace falta (ni crea la NR).
        /// </summary>
        public bool TieneReferenciaForzada
        {
            get
            {
                string referencia = Limpiar(ProductoCursoEdicion);
                if (referencia == null)
                {
                    return false;
                }
                string propia = ReferenciaPropia(_idEdicion);
                return _idEdicion <= 0
                    || (!string.Equals(referencia, propia, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(referencia, propia + "NR", StringComparison.OrdinalIgnoreCase));
            }
        }

        private string _nombreProductoCurso;
        /// <summary>Lo que se enseña al comprobar la referencia: nombre e IVA, o que no existe.</summary>
        public string NombreProductoCurso { get => _nombreProductoCurso; private set => SetProperty(ref _nombreProductoCurso, value); }

        private int? _plazasEdicion;
        /// <summary>Vacío = sin límite.</summary>
        public int? PlazasEdicion { get => _plazasEdicion; set => SetProperty(ref _plazasEdicion, value); }

        private string _lugarEdicion;
        public string LugarEdicion { get => _lugarEdicion; set => SetProperty(ref _lugarEdicion, value); }

        private string _descripcionLargaEdicion;
        public string DescripcionLargaEdicion { get => _descripcionLargaEdicion; set => SetProperty(ref _descripcionLargaEdicion, value); }

        // NestoAPI#690 (corte 1b, contrato provisional): horario, ponente, imagen y la señal no reembolsable.

        /// <summary>NestoAPI#690: por defecto, la reembolsable (con cupón) es de 100 € y la no reembolsable de 60 €.</summary>
        public const decimal SENAL_REEMBOLSABLE_DEFECTO = 100;
        public const decimal SENAL_NO_REEMBOLSABLE_DEFECTO = 60;

        private string _horaInicioEdicion;
        /// <summary>«10:00» (vacío = sin hora).</summary>
        public string HoraInicioEdicion { get => _horaInicioEdicion; set => SetProperty(ref _horaInicioEdicion, value); }

        private string _horaFinEdicion;
        public string HoraFinEdicion { get => _horaFinEdicion; set => SetProperty(ref _horaFinEdicion, value); }

        private string _ponenteEdicion;
        public string PonenteEdicion { get => _ponenteEdicion; set => SetProperty(ref _ponenteEdicion, value); }

        private decimal? _importeSenalNoReembolsableEdicion;
        /// <summary>Sin cupón. Las dos señales a 0: evento gratuito.</summary>
        public decimal? ImporteSenalNoReembolsableEdicion
        {
            get => _importeSenalNoReembolsableEdicion;
            set => SetProperty(ref _importeSenalNoReembolsableEdicion, value);
        }

        private string _imagenUrl;
        /// <summary>NestoAPI#690: la imagen del evento seleccionado (solo se enseña; la subida está pendiente del contrato).</summary>
        public string ImagenUrl
        {
            get => _imagenUrl;
            private set
            {
                if (SetProperty(ref _imagenUrl, value))
                {
                    QuitarImagenCommand?.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>Elige el fichero de la imagen (null = cancelado). Sustituible en los tests.</summary>
        public Func<string> ElegirImagen { get; set; } = ElegirImagenWpf;

        private static string ElegirImagenWpf()
        {
            var dialogo = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Imagen del evento",
                Filter = "Imágenes (*.png;*.jpg;*.jpeg;*.gif;*.webp)|*.png;*.jpg;*.jpeg;*.gif;*.webp"
            };
            return dialogo.ShowDialog() == true ? dialogo.FileName : null;
        }

        public AsyncRelayCommand SubirImagenCommand { get; }

        /// <summary>NestoAPI#690: sube (o sustituye) la imagen; pueden la tienda del evento, Tienda online y Dirección (la API lo comprueba).</summary>
        public async Task SubirImagenAsync()
        {
            if (_idEdicion <= 0)
            {
                return;
            }
            string ruta = ElegirImagen?.Invoke();
            if (string.IsNullOrWhiteSpace(ruta))
            {
                return;
            }
            try
            {
                EstaOcupado = true;
                EventoModel evento = await _servicio.SubirImagen(_idEdicion, ruta);
                ImagenUrl = string.IsNullOrWhiteSpace(evento?.ImagenUrl) ? null : evento.ImagenUrl.Trim();
                if (Seleccionado != null && Seleccionado.Id == _idEdicion)
                {
                    Seleccionado.ImagenUrl = ImagenUrl;
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        public AsyncRelayCommand QuitarImagenCommand { get; }

        public async Task QuitarImagenAsync()
        {
            if (_idEdicion <= 0 || string.IsNullOrWhiteSpace(ImagenUrl)
                || !_dialogService.ShowConfirmationAnswer("Quitar imagen", "¿Quita la imagen del evento?"))
            {
                return;
            }
            try
            {
                EstaOcupado = true;
                await _servicio.BorrarImagen(_idEdicion);
                ImagenUrl = null;
                if (Seleccionado != null && Seleccionado.Id == _idEdicion)
                {
                    Seleccionado.ImagenUrl = null;
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        /// <summary>«10:00», «9:30» → la hora; vacío → null. False si no se entiende.</summary>
        public static bool LeerHora(string texto, out TimeSpan? hora)
        {
            hora = null;
            if (string.IsNullOrWhiteSpace(texto))
            {
                return true;
            }
            string limpio = texto.Trim().Replace('.', ':');
            if (TimeSpan.TryParseExact(limpio, new[] { @"h\:mm", @"hh\:mm" }, System.Globalization.CultureInfo.InvariantCulture, out TimeSpan leida)
                && leida >= TimeSpan.Zero && leida < TimeSpan.FromDays(1))
            {
                hora = leida;
                return true;
            }
            return false;
        }

        private bool _estaOcupado;
        public bool EstaOcupado { get => _estaOcupado; set => SetProperty(ref _estaOcupado, value); }

        public AsyncRelayCommand CargarCommand { get; }

        public async Task CargarAsync()
        {
            try
            {
                EstaOcupado = true;
                if (_almacenUsuario == null && Configuracion != null)
                {
                    try
                    {
                        _almacenUsuario = (await Configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenPedidoVta))?.Trim() ?? string.Empty;
                    }
                    catch
                    {
                        _almacenUsuario = string.Empty;
                    }
                }
                int? idSeleccionado = Seleccionado?.Id;
                List<EventoModel> lista = await _servicio.LeerEventos(false);
                Eventos = new ObservableCollection<EventoModel>(lista);
                Seleccionado = Eventos.FirstOrDefault(e => e.Id == idSeleccionado);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        public RelayCommand NuevoCommand { get; }

        private void OnNuevo()
        {
            Seleccionado = null;
            _idEdicion = 0;
            TituloEdicion = string.Empty;
            FechaEdicion = DateTime.Today;
            ImporteSenalEdicion = SENAL_REEMBOLSABLE_DEFECTO;
            ImporteSenalNoReembolsableEdicion = SENAL_NO_REEMBOLSABLE_DEFECTO;
            HoraInicioEdicion = null;
            HoraFinEdicion = null;
            PonenteEdicion = null;
            ImagenUrl = null;
            ActivoEdicion = true;
            TiendaEdicion = TiendaPorCodigo(TiendaEvento.EsTiendaDeEventos(_almacenUsuario) ? _almacenUsuario : null);
            FormatoEdicion = Formatos[0];
            ProductoCursoEdicion = null;
            PlazasEdicion = null;
            LugarEdicion = null;
            DescripcionLargaEdicion = null;
            Editando = true;
            OnPropertyChanged(nameof(EsNuevo));
            SubirImagenCommand.NotifyCanExecuteChanged();
            QuitarImagenCommand.NotifyCanExecuteChanged();
            CrearProductoCursoCommand.NotifyCanExecuteChanged();
        }

        /// <summary>Null si el evento no tiene tienda (los de antes): hay que elegirla para guardar.</summary>
        private TiendaEvento TiendaPorCodigo(string codigo) =>
            Tiendas.FirstOrDefault(t => string.Equals(t.Codigo, codigo?.Trim(), StringComparison.OrdinalIgnoreCase));

        private void CargarEdicion(EventoModel evento)
        {
            _idEdicion = evento.Id;
            TituloEdicion = evento.Titulo;
            FechaEdicion = evento.Fecha;
            ImporteSenalEdicion = evento.ImporteSenal;
            ActivoEdicion = evento.Activo;
            TiendaEdicion = TiendaPorCodigo(evento.Almacen);
            FormatoEdicion = FormatoPorValor(evento.Formato);
            ProductoCursoEdicion = evento.ProductoCurso?.Trim();
            PlazasEdicion = evento.Plazas;
            LugarEdicion = evento.Lugar;
            DescripcionLargaEdicion = evento.DescripcionLarga;
            HoraInicioEdicion = evento.HoraInicio?.Trim();
            HoraFinEdicion = evento.HoraFin?.Trim();
            PonenteEdicion = evento.Ponente;
            ImporteSenalNoReembolsableEdicion = evento.ImporteSenalNoReembolsable;
            ImagenUrl = string.IsNullOrWhiteSpace(evento.ImagenUrl) ? null : evento.ImagenUrl.Trim();
            Editando = true;
            OnPropertyChanged(nameof(EsNuevo));
            SubirImagenCommand.NotifyCanExecuteChanged();
            QuitarImagenCommand.NotifyCanExecuteChanged();
            CrearProductoCursoCommand.NotifyCanExecuteChanged();
        }

        public AsyncRelayCommand GuardarCommand { get; }

        public async Task GuardarAsync()
        {
            if (!Editando)
            {
                return;
            }
            if (!TieneAcceso)
            {
                _dialogService.ShowError("Los eventos los mantiene cada tienda (los de Reina, Reina; los de Alcobendas, Alcobendas), Administración y Dirección.");
                return;
            }
            if (string.IsNullOrWhiteSpace(TituloEdicion))
            {
                _dialogService.ShowError("El evento tiene que tener un título.");
                return;
            }
            if (FechaEdicion == null)
            {
                _dialogService.ShowError("El evento tiene que tener fecha.");
                return;
            }
            if (!TiendaEvento.EsTiendaDeEventos(TiendaEdicion?.Codigo))
            {
                _dialogService.ShowError("Elija la tienda del evento (Reina o Alcobendas). También en los online: el ponente emite desde una de las dos tiendas y la factura lleva su almacén y su delegación.");
                return;
            }
            if (ImporteSenalEdicion < 0)
            {
                _dialogService.ShowError("El importe de la señal no puede ser negativo.");
                return;
            }
            if (ImporteSenalNoReembolsableEdicion < 0)
            {
                _dialogService.ShowError("El importe de la señal no reembolsable no puede ser negativo.");
                return;
            }
            if (!LeerHora(HoraInicioEdicion, out TimeSpan? horaInicio) || !LeerHora(HoraFinEdicion, out TimeSpan? horaFin))
            {
                _dialogService.ShowError("Escriba las horas como 10:00.");
                return;
            }
            if (horaInicio.HasValue && horaFin.HasValue && horaFin.Value <= horaInicio.Value)
            {
                _dialogService.ShowError("La hora de fin tiene que ser posterior a la de inicio.");
                return;
            }
            if (PlazasEdicion.HasValue && PlazasEdicion.Value <= 0)
            {
                _dialogService.ShowError("Las plazas tienen que ser más de cero (o ninguna, si no hay límite).");
                return;
            }
            try
            {
                EstaOcupado = true;
                EventoModel guardado = await _servicio.GuardarEvento(new EventoModel
                {
                    Id = _idEdicion,
                    Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                    Titulo = TituloEdicion.Trim(),
                    Fecha = FechaEdicion.Value.Date,
                    ImporteSenal = ImporteSenalEdicion,
                    Activo = ActivoEdicion,
                    Almacen = TiendaEdicion?.Codigo,
                    Formato = FormatoEdicion?.Valor ?? EventoModel.FORMATO_PRESENCIAL,
                    ProductoCurso = Limpiar(ProductoCursoEdicion),
                    Plazas = PlazasEdicion,
                    Lugar = Limpiar(LugarEdicion),
                    DescripcionLarga = Limpiar(DescripcionLargaEdicion),
                    HoraInicio = horaInicio.HasValue ? horaInicio.Value.ToString(@"hh\:mm") : null,
                    HoraFin = horaFin.HasValue ? horaFin.Value.ToString(@"hh\:mm") : null,
                    Ponente = Limpiar(PonenteEdicion),
                    ImporteSenalNoReembolsable = ImporteSenalNoReembolsableEdicion
                });
                List<EventoModel> lista = await _servicio.LeerEventos(false);
                Eventos = new ObservableCollection<EventoModel>(lista);
                Seleccionado = Eventos.FirstOrDefault(e => e.Id == guardado?.Id);
                _dialogService.ShowNotification("Evento guardado", $"Guardado el evento «{guardado?.Titulo}» del {guardado?.Fecha:dd/MM/yyyy}.");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        private static string Limpiar(string texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

        public AsyncRelayCommand ComprobarProductoCommand { get; }

        /// <summary>Enseña el nombre y el IVA de la referencia del curso (los cursos se facturan exentos, EX).</summary>
        public async Task ComprobarProductoAsync()
        {
            string referencia = Limpiar(ProductoCursoEdicion);
            if (referencia == null)
            {
                return;
            }
            try
            {
                EstaOcupado = true;
                ProductoCursoModel producto = await _servicio.LeerProductoCurso(referencia);
                if (producto == null)
                {
                    NombreProductoCurso = $"No existe el producto {referencia}: dé de alta antes la referencia del curso (exenta de IVA).";
                    return;
                }
                string grupo = producto.Grupo?.Trim();
                if (!string.IsNullOrEmpty(grupo) && !string.Equals(grupo, ProductoCursoModel.GRUPO_CURSOS, StringComparison.OrdinalIgnoreCase))
                {
                    NombreProductoCurso = $"La referencia {referencia} ({producto.Nombre?.Trim()}) es del grupo {grupo}, no de cursos ({ProductoCursoModel.GRUPO_CURSOS}): escriba una referencia de curso o pulse «Crear referencia».";
                    return;
                }
                string iva = producto.Iva?.Trim();
                NombreProductoCurso = string.Equals(iva, "EX", StringComparison.OrdinalIgnoreCase)
                    ? $"{producto.Nombre?.Trim()} (exento)"
                    : $"{producto.Nombre?.Trim()}. Atención: tiene IVA {iva}; los cursos se facturan exentos (EX).";
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        public AsyncRelayCommand CrearProductoCursoCommand { get; }

        /// <summary>
        /// Nesto#517 (Carlos 22/10/26): crea en la API la referencia del curso de cada modalidad con importe (la reembolsable,
        /// «EVENTO» + Id a 5 cifras; la no reembolsable, con «NR») y la pone en el evento: la reembolsable si la hay y, si
        /// no, la no reembolsable (el evento tiene un solo campo de curso). Hay que guardar el evento después. Solo con el
        /// evento ya guardado (con Id) y sin una referencia forzada (<see cref="TieneReferenciaForzada"/>: esa sirve para
        /// todas las modalidades, así que no se crea la NR).
        /// </summary>
        public async Task CrearProductoCursoAsync()
        {
            if (!Editando || _idEdicion <= 0 || TieneReferenciaForzada)
            {
                return;
            }
            List<string> modalidades = new();
            if (ImporteSenalEdicion > 0)
            {
                modalidades.Add(InscribirEventoModel.MODALIDAD_REEMBOLSABLE);
            }
            if (ImporteSenalNoReembolsableEdicion > 0)
            {
                modalidades.Add(InscribirEventoModel.MODALIDAD_NO_REEMBOLSABLE);
            }
            if (!modalidades.Any())
            {
                _dialogService.ShowError("El evento es gratuito (las dos señales a 0): no necesita referencia de curso.");
                return;
            }
            int idEvento = _idEdicion;
            List<ProductoCursoCreadoModel> creados = new();
            try
            {
                EstaOcupado = true;
                foreach (string modalidad in modalidades)
                {
                    ProductoCursoCreadoModel creado = await _servicio.CrearProductoCurso(idEvento, modalidad);
                    if (creado != null && !string.IsNullOrWhiteSpace(creado.Producto))
                    {
                        creados.Add(creado);
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message); // «No se pudo crear la referencia del curso: …» (EventosService)
            }
            finally
            {
                EstaOcupado = false;
            }
            if (!creados.Any() || _idEdicion != idEvento)
            {
                return;
            }
            ProductoCursoEdicion = creados.First().Producto.Trim();
            NombreProductoCurso = string.Join(" ", creados.Select(c =>
                $"{c.Producto.Trim()} ({c.Nombre?.Trim()}) {(c.Creado ? "creada" : "ya existía")}.")) + " Guarde el evento para que quede asociada.";
        }

        // ---------------- Nesto#517: inscritos del evento seleccionado ----------------

        private ObservableCollection<InscripcionEventoModel> _inscripciones = new();
        public ObservableCollection<InscripcionEventoModel> Inscripciones
        {
            get => _inscripciones;
            private set
            {
                if (SetProperty(ref _inscripciones, value))
                {
                    OnPropertyChanged(nameof(ResumenInscripciones));
                    OnPropertyChanged(nameof(SePuedePasarLista));
                    OnPropertyChanged(nameof(AvisoListaAsistencia));
                    GuardarAsistenciaCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private InscripcionEventoModel _inscripcionSeleccionada;
        public InscripcionEventoModel InscripcionSeleccionada
        {
            get => _inscripcionSeleccionada;
            set
            {
                if (SetProperty(ref _inscripcionSeleccionada, value))
                {
                    if (value != null)
                    {
                        FormaPagoPago = value.FormaPago != null && FormasPago.Contains(value.FormaPago.Trim())
                            ? value.FormaPago.Trim()
                            : MarcarPagoInscripcionModel.TRANSFERENCIA;
                        ImportePago = Seleccionado?.ImporteSenal;
                        FechaPagoEdicion = DateTime.Today;
                    }
                    NotificarComandosInscripcion();
                    OnPropertyChanged(nameof(MensajeFacturacion));
                }
            }
        }

        /// <summary>El motivo de la facturación en Error, NoAutomatizable o SinCobro de la inscripción seleccionada.</summary>
        public string MensajeFacturacion => InscripcionSeleccionada?.Facturacion?.Mensaje;

        /// <summary>«8 inscritos: 2 reservadas, 5 pagadas, 1 asistió».</summary>
        public string ResumenInscripciones
        {
            get
            {
                List<InscripcionEventoModel> vivas = Inscripciones?
                    .Where(i => i.Estado != InscripcionEventoModel.CADUCADA && i.Estado != InscripcionEventoModel.ANULADA).ToList()
                    ?? new List<InscripcionEventoModel>();
                if (!vivas.Any())
                {
                    return "Sin inscritos.";
                }
                IEnumerable<string> partes = vivas.GroupBy(i => (i.EstadoTexto ?? i.Estado)?.ToLowerInvariant())
                    .Select(g => $"{g.Count()} {g.Key}");
                return $"{vivas.Count} inscritos: {string.Join(", ", partes)}.";
            }
        }

        private bool _cargandoInscripciones;

        public AsyncRelayCommand RecargarInscripcionesCommand { get; }

        public async Task CargarInscripcionesAsync()
        {
            EventoModel evento = Seleccionado;
            if (evento == null || evento.Id <= 0 || _cargandoInscripciones)
            {
                return;
            }
            try
            {
                _cargandoInscripciones = true;
                List<InscripcionEventoModel> lista = await _servicio.LeerInscripciones(evento.Id) ?? new List<InscripcionEventoModel>();
                if (Seleccionado?.Id != evento.Id)
                {
                    return; // Ha cambiado de evento mientras cargaba.
                }
                PonerInscripciones(lista);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                _cargandoInscripciones = false;
            }
        }

        private void PonerInscripciones(IEnumerable<InscripcionEventoModel> lista)
        {
            int? anterior = InscripcionSeleccionada?.Id;
            foreach (InscripcionEventoModel inscripcion in lista)
            {
                inscripcion.Asistio = inscripcion.Estado == InscripcionEventoModel.ASISTIO;
            }
            Inscripciones = new ObservableCollection<InscripcionEventoModel>(lista);
            InscripcionSeleccionada = Inscripciones.FirstOrDefault(i => i.Id == anterior);
        }

        /// <summary>Sustituye en la lista la inscripción que devuelve la API (estado nuevo) sin recargar todo.</summary>
        private void Reemplazar(InscripcionEventoModel nueva)
        {
            if (nueva == null)
            {
                return;
            }
            List<InscripcionEventoModel> lista = Inscripciones.ToList();
            int posicion = lista.FindIndex(i => i.Id == nueva.Id);
            if (posicion >= 0)
            {
                lista[posicion] = nueva;
            }
            else
            {
                lista.Add(nueva);
            }
            InscripcionSeleccionada = null;
            PonerInscripciones(lista);
            InscripcionSeleccionada = Inscripciones.FirstOrDefault(i => i.Id == nueva.Id);
        }

        private void NotificarComandosInscripcion()
        {
            AnularInscripcionCommand.NotifyCanExecuteChanged();
            ConfirmaAsistenciaCommand.NotifyCanExecuteChanged();
            NoPodraIrCommand.NotifyCanExecuteChanged();
            MarcarPagadaCommand.NotifyCanExecuteChanged();
            FacturarCommand.NotifyCanExecuteChanged();
        }

        public AsyncRelayCommand InscribirCommand { get; }

        /// <summary>Abre el diálogo de inscribir con el evento seleccionado; al volver, recarga los inscritos.</summary>
        public async Task InscribirAsync()
        {
            if (Seleccionado == null || Seleccionado.Id <= 0)
            {
                return;
            }
            try
            {
                ResultadoDialogo resultado = await _dialogService.ShowDialogAsync(InscribirEventoDialogViewModel.NOMBRE,
                    new ParametrosDialogo { { "eventoId", Seleccionado.Id } });
                if (resultado?.Result == ResultadoBoton.OK)
                {
                    await CargarInscripcionesAsync();
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        public AsyncRelayCommand AnularInscripcionCommand { get; }

        public async Task AnularInscripcionAsync()
        {
            InscripcionEventoModel inscripcion = InscripcionSeleccionada;
            if (inscripcion == null || !inscripcion.SePuedeAnular)
            {
                return;
            }
            if (!_dialogService.ShowConfirmationAnswer("Anular inscripción",
                $"¿Anula la inscripción de {inscripcion.Nombre?.Trim()} ({inscripcion.ClienteContacto}) en «{inscripcion.Evento}»? Libera la plaza."))
            {
                return;
            }
            try
            {
                EstaOcupado = true;
                Reemplazar(await _servicio.AnularInscripcion(inscripcion.Id));
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        // Marcar pagada a mano (Carlos, 14/10 y 15/10/26): la API no contabiliza nada; el efectivo lo mete cada usuario en su caja.

        private string _formaPagoPago = MarcarPagoInscripcionModel.TRANSFERENCIA;
        public string FormaPagoPago { get => _formaPagoPago; set => SetProperty(ref _formaPagoPago, value); }

        private decimal? _importePago;
        /// <summary>Vacío = el importe de la señal del evento.</summary>
        public decimal? ImportePago { get => _importePago; set => SetProperty(ref _importePago, value); }

        private DateTime? _fechaPagoEdicion = DateTime.Today;
        public DateTime? FechaPagoEdicion { get => _fechaPagoEdicion; set => SetProperty(ref _fechaPagoEdicion, value); }

        public AsyncRelayCommand MarcarPagadaCommand { get; }

        public async Task MarcarPagadaAsync()
        {
            InscripcionEventoModel inscripcion = InscripcionSeleccionada;
            if (inscripcion == null || !inscripcion.SePuedeMarcarPagada)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(FormaPagoPago))
            {
                _dialogService.ShowError("Indique cómo ha pagado: Tarjeta, Transferencia o Efectivo.");
                return;
            }
            if (FechaPagoEdicion.HasValue && FechaPagoEdicion.Value.Date > DateTime.Today)
            {
                _dialogService.ShowError("La fecha del pago no puede ser futura.");
                return;
            }
            string aviso = FormaPagoPago == MarcarPagoInscripcionModel.EFECTIVO
                ? " Recuerde meter el cobro en la caja de la tienda: Nesto no lo contabiliza."
                : string.Empty;
            if (!_dialogService.ShowConfirmationAnswer("Marcar como pagada",
                $"¿Marca como pagada ({FormaPagoPago.ToLowerInvariant()}, {(ImportePago ?? Seleccionado?.ImporteSenal ?? 0):C}) la inscripción de {inscripcion.Nombre?.Trim()} ({inscripcion.ClienteContacto})?{aviso}"))
            {
                return;
            }
            try
            {
                EstaOcupado = true;
                Reemplazar(await _servicio.MarcarPagada(inscripcion.Id, new MarcarPagoInscripcionModel
                {
                    FormaPago = FormaPagoPago,
                    Importe = ImportePago,
                    // Hoy = ahora (la API pone la hora); otro día, ese día.
                    Fecha = FechaPagoEdicion.HasValue && FechaPagoEdicion.Value.Date != DateTime.Today ? FechaPagoEdicion.Value.Date : null
                }));
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        public AsyncRelayCommand ConfirmaAsistenciaCommand { get; }
        public AsyncRelayCommand NoPodraIrCommand { get; }

        /// <summary>NestoAPI#690: la tienda apunta lo que le ha dicho el cliente («Confirma» o «No podrá ir»; no anula nada).</summary>
        public async Task ConfirmarAsistenciaAsync(string respuesta)
        {
            InscripcionEventoModel inscripcion = InscripcionSeleccionada;
            if (inscripcion == null || !inscripcion.SePuedeConfirmarAsistencia)
            {
                return;
            }
            try
            {
                EstaOcupado = true;
                Reemplazar(await _servicio.ConfirmarAsistencia(inscripcion.Id, respuesta));
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        public AsyncRelayCommand FacturarCommand { get; }

        /// <summary>NestoAPI#612: relanza la facturación del curso (pedido, factura exenta, liquidación) desde donde se quedó.</summary>
        public async Task FacturarAsync()
        {
            InscripcionEventoModel inscripcion = InscripcionSeleccionada;
            if (inscripcion == null || !inscripcion.SePuedeFacturar)
            {
                return;
            }
            try
            {
                EstaOcupado = true;
                InscripcionEventoModel facturada = await _servicio.FacturarCurso(inscripcion.Id);
                Reemplazar(facturada);
                if (!string.IsNullOrWhiteSpace(facturada?.Facturacion?.Factura))
                {
                    _dialogService.ShowNotification("Curso facturado", $"El curso de {facturada.Nombre?.Trim()} está en la factura {facturada.Facturacion.Factura.Trim()}.");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        // Lista de asistencia: desde el día del evento; las pagadas marcadas pasan a Asistió y el resto a No asistió (pierden el cupón).

        public bool SePuedePasarLista => Seleccionado != null && Seleccionado.Fecha.Date <= DateTime.Today
            && (Inscripciones?.Any(i => i.EntraEnListaAsistencia) ?? false);

        public string AvisoListaAsistencia
        {
            get
            {
                if (Seleccionado == null)
                {
                    return string.Empty;
                }
                if (Seleccionado.Fecha.Date > DateTime.Today)
                {
                    return $"La lista se pasa desde el día del evento ({Seleccionado.Fecha:dd/MM/yyyy}).";
                }
                string pasada = Seleccionado.FechaListaAsistencia.HasValue
                    ? $"Lista pasada el {Seleccionado.FechaListaAsistencia.Value:dd/MM/yyyy HH:mm}; se puede corregir. "
                    : string.Empty;
                return pasada + "Marque quién ha venido: los que no marque pasan a «No asistió» y pierden el cupón.";
            }
        }

        public AsyncRelayCommand GuardarAsistenciaCommand { get; }

        /// <summary>
        /// «Las siguientes personas pagaron y no están marcadas: Ana P., Lucía R. Quedarán como ‘No asistió’ y perderán su
        /// cupón. ¿Desea guardar la lista?»; null si todas las que pagaron están marcadas (las gratuitas no cuentan).
        /// </summary>
        public static string MensajePagadasSinMarcar(IEnumerable<InscripcionEventoModel> enLista)
        {
            List<string> nombres = enLista?
                .Where(i => i.EntraEnListaAsistencia && !i.EsGratuita && !i.Asistio)
                .Select(i => string.IsNullOrWhiteSpace(i.Nombre) ? i.ClienteContacto : i.Nombre.Trim())
                .ToList() ?? new List<string>();
            if (!nombres.Any())
            {
                return null;
            }
            string lista = string.Join(", ", nombres);
            return $"Las siguientes personas pagaron y no están marcadas: {lista}{(lista.EndsWith(".") ? string.Empty : ".")} Quedarán como ‘No asistió’ y perderán su cupón. ¿Desea guardar la lista?";
        }

        public async Task GuardarAsistenciaAsync()
        {
            EventoModel evento = Seleccionado;
            if (evento == null || !SePuedePasarLista)
            {
                return;
            }
            List<InscripcionEventoModel> enLista = Inscripciones.Where(i => i.EntraEnListaAsistencia).ToList();
            List<int> asistentes = enLista.Where(i => i.Asistio).Select(i => i.Id).ToList();
            // Carlos 22/10/26: solo se pregunta si alguien que pagó se queda sin marcar (con sus nombres y «No» por defecto);
            // si no, se guarda sin preguntar.
            string mensaje = MensajePagadasSinMarcar(enLista);
            if (mensaje != null && !_dialogService.ShowConfirmationAnswer("Lista de asistencia", mensaje, porDefectoNo: true))
            {
                return;
            }
            try
            {
                EstaOcupado = true;
                List<InscripcionEventoModel> resultado = await _servicio.PasarLista(evento.Id, asistentes);
                await CargarAsync(); // FechaListaAsistencia del evento y los inscritos al día
                _dialogService.ShowNotification("Lista de asistencia", $"Guardada la lista de «{evento.Titulo}» ({resultado?.Count ?? 0} inscritos en la lista).");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }
    }
}
