using CommunityToolkit.Mvvm.Input;
using ControlesUsuario.Services;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#652 (NestoAPI#903): «Descargar para FACe (Facturae)» de una factura emitida. Al abrirse comprueba (GET
    /// api/Facturas/Facturae?comprobar=true) y enseña receptor, centros DIR3, «Su pedido», expediente, contrato, base, impuestos,
    /// total y vencimientos, con las faltas en rojo y los avisos en amarillo. Deja escribir el P.O., el expediente, el contrato y
    /// los DIR3 (se mandan en la llamada y ganan sobre lo guardado; los DIR3 se pueden además guardar en la ficha del cliente).
    /// «Descargar» solo con la comprobación al día y sin faltas: guarda el XML (por defecto en Descargas) y abre la carpeta.
    /// Nunca lanza: los errores de la API salen en <see cref="Error"/>.
    /// </summary>
    public class FacturaeDialogViewModel : DialogoNestoBase
    {
        public const string NOMBRE = "FacturaeDialog";
        public const string PARAMETRO_EMPRESA = "empresa";
        public const string PARAMETRO_FACTURA = "factura";

        private readonly IServicioFacturae _servicio;
        private readonly IGuardadorFicherosDescarga _guardador;
        private string _empresa;
        private string _factura;
        private ComprobacionFacturaeModel _comprobacion;
        private ReferenciasFacturae _comprobadoCon;

        public FacturaeDialogViewModel(IServicioFacturae servicio, IGuardadorFicherosDescarga guardador)
        {
            _servicio = servicio;
            _guardador = guardador;
            Title = ReglasFacturae.TITULO;
            ComprobarCommand = new AsyncRelayCommand(ComprobarAsync, () => !Trabajando);
            DescargarCommand = new AsyncRelayCommand(DescargarAsync, () => PuedeDescargar);
            GuardarDir3EnFichaCommand = new AsyncRelayCommand(GuardarDir3EnFichaAsync, () => PuedeGuardarDir3EnFicha);
            CerrarCommand = new RelayCommand(() => RaiseRequestClose(Descargado != null ? ResultadoBoton.OK : ResultadoBoton.Cancel));
        }

        public AsyncRelayCommand ComprobarCommand { get; }
        public AsyncRelayCommand DescargarCommand { get; }
        public AsyncRelayCommand GuardarDir3EnFichaCommand { get; }
        public RelayCommand CerrarCommand { get; }

        public string AyudaB2Brouter => ReglasFacturae.AYUDA_B2BROUTER;

        #region Estado
        private bool _trabajando;
        public bool Trabajando
        {
            get => _trabajando;
            private set
            {
                if (SetProperty(ref _trabajando, value))
                {
                    NotificarComandos();
                }
            }
        }

        private string _error;
        /// <summary>Lo que ha contestado la API cuando algo falla (o lo que no vale de lo escrito).</summary>
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

        private string _mensaje;
        /// <summary>Lo bueno: «Guardado en …», «Códigos guardados en la ficha…».</summary>
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

        private string _descargado;
        /// <summary>La ruta donde se ha guardado el último fichero (null si aún no).</summary>
        public string Descargado
        {
            get => _descargado;
            private set => SetProperty(ref _descargado, value);
        }
        #endregion

        #region Lo que dice la comprobación
        public ComprobacionFacturaeModel Comprobacion
        {
            get => _comprobacion;
            private set
            {
                if (SetProperty(ref _comprobacion, value))
                {
                    OnPropertyChanged(nameof(HayComprobacion));
                    OnPropertyChanged(nameof(Cabecera));
                    OnPropertyChanged(nameof(Receptor));
                    OnPropertyChanged(nameof(TextoSuPedido));
                    OnPropertyChanged(nameof(TextoExpedienteContrato));
                    OnPropertyChanged(nameof(TextoOrigenDir3));
                    OnPropertyChanged(nameof(BaseImponible));
                    OnPropertyChanged(nameof(TotalImpuestos));
                    OnPropertyChanged(nameof(Total));
                    OnPropertyChanged(nameof(Listo));
                    Faltas = new ObservableCollection<string>((value?.Faltas ?? new List<string>()).Where(f => !string.IsNullOrWhiteSpace(f)));
                    Avisos = new ObservableCollection<string>((value?.Avisos ?? new List<string>()).Where(a => !string.IsNullOrWhiteSpace(a)));
                    Centros = new ObservableCollection<string>((value?.Centros ?? new List<CentroFacturaeModel>()).Select(ReglasFacturae.TextoCentro));
                    Impuestos = new ObservableCollection<string>((value?.Impuestos ?? new List<ImpuestoFacturaeModel>()).Select(ReglasFacturae.TextoImpuesto));
                    Vencimientos = new ObservableCollection<string>((value?.Vencimientos ?? new List<VencimientoFacturaeModel>()).Select(ReglasFacturae.TextoVencimiento));
                    NotificarComandos();
                }
            }
        }

        public bool HayComprobacion => Comprobacion != null;
        public bool Listo => Comprobacion?.Listo == true;

        public string Cabecera
        {
            get
            {
                if (Comprobacion == null)
                {
                    return $"Factura {_factura}";
                }
                string texto = $"Factura {Comprobacion.Factura?.Trim() ?? _factura} · cliente {Comprobacion.Cliente?.Trim()}/{Comprobacion.Contacto?.Trim()}";
                if (Comprobacion.EsRectificativa)
                {
                    texto += $" · rectificativa de la {Comprobacion.FacturaRectificada?.Trim()}";
                }
                return texto;
            }
        }

        public string Receptor => Comprobacion == null ? null
            : $"{Comprobacion.NombreReceptor?.Trim()} (NIF {Comprobacion.NifReceptor?.Trim()})";

        public string TextoSuPedido
        {
            get
            {
                if (Comprobacion == null)
                {
                    return null;
                }
                if (string.IsNullOrWhiteSpace(Comprobacion.SuPedido))
                {
                    return "Su pedido: (ninguno)";
                }
                string origen = ReglasFacturae.TextoOrigenSuPedido(Comprobacion.OrigenSuPedido);
                return $"Su pedido: {Comprobacion.SuPedido.Trim()}" + (origen == null ? string.Empty : $" ({origen})");
            }
        }

        public string TextoExpedienteContrato => Comprobacion == null ? null
            : $"Expediente: {Vacio(Comprobacion.Expediente)} · Contrato: {Vacio(Comprobacion.Contrato)}";

        public string TextoOrigenDir3 => ReglasFacturae.TextoOrigenDir3(Comprobacion?.OrigenDir3);
        public string BaseImponible => Comprobacion == null ? null : ReglasFacturae.Euros(Comprobacion.BaseImponible);
        public string TotalImpuestos => Comprobacion == null ? null : ReglasFacturae.Euros(Comprobacion.TotalImpuestos);
        public string Total => Comprobacion == null ? null : ReglasFacturae.Euros(Comprobacion.Total);

        private ObservableCollection<string> _faltas = new ObservableCollection<string>();
        public ObservableCollection<string> Faltas
        {
            get => _faltas;
            private set
            {
                if (SetProperty(ref _faltas, value))
                {
                    OnPropertyChanged(nameof(HayFaltas));
                }
            }
        }
        public bool HayFaltas => Faltas.Any();

        private ObservableCollection<string> _avisos = new ObservableCollection<string>();
        public ObservableCollection<string> Avisos
        {
            get => _avisos;
            private set
            {
                if (SetProperty(ref _avisos, value))
                {
                    OnPropertyChanged(nameof(HayAvisos));
                }
            }
        }
        public bool HayAvisos => Avisos.Any();

        private ObservableCollection<string> _centros = new ObservableCollection<string>();
        public ObservableCollection<string> Centros { get => _centros; private set => SetProperty(ref _centros, value); }

        private ObservableCollection<string> _impuestos = new ObservableCollection<string>();
        public ObservableCollection<string> Impuestos { get => _impuestos; private set => SetProperty(ref _impuestos, value); }

        private ObservableCollection<string> _vencimientos = new ObservableCollection<string>();
        public ObservableCollection<string> Vencimientos { get => _vencimientos; private set => SetProperty(ref _vencimientos, value); }
        #endregion

        #region Lo que se escribe
        private string _suPedido;
        public string SuPedido { get => _suPedido; set => CambiarEntrada(ref _suPedido, value); }

        private string _expediente;
        public string Expediente { get => _expediente; set => CambiarEntrada(ref _expediente, value); }

        private string _contrato;
        public string Contrato { get => _contrato; set => CambiarEntrada(ref _contrato, value); }

        private string _oficinaContable;
        public string OficinaContable { get => _oficinaContable; set => CambiarEntrada(ref _oficinaContable, value); }

        private string _organoGestor;
        public string OrganoGestor { get => _organoGestor; set => CambiarEntrada(ref _organoGestor, value); }

        private string _unidadTramitadora;
        public string UnidadTramitadora { get => _unidadTramitadora; set => CambiarEntrada(ref _unidadTramitadora, value); }

        private string _organoProponente;
        public string OrganoProponente { get => _organoProponente; set => CambiarEntrada(ref _organoProponente, value); }

        private void CambiarEntrada(ref string campo, string valor)
        {
            if (SetProperty(ref campo, valor))
            {
                OnPropertyChanged(nameof(HayCambiosSinComprobar));
                NotificarComandos();
            }
        }

        /// <summary>Lo que se manda en la llamada (solo lo escrito; los DIR3 iguales a los de la comprobación no se mandan).</summary>
        internal ReferenciasFacturae Referencias() => new ReferenciasFacturae
        {
            SuPedido = Limpio(SuPedido),
            Expediente = Limpio(Expediente),
            Contrato = Limpio(Contrato),
            OficinaContable = SiNoEsElDeLaFicha(ReglasFacturae.ROL_OFICINA_CONTABLE, OficinaContable),
            OrganoGestor = SiNoEsElDeLaFicha(ReglasFacturae.ROL_ORGANO_GESTOR, OrganoGestor),
            UnidadTramitadora = SiNoEsElDeLaFicha(ReglasFacturae.ROL_UNIDAD_TRAMITADORA, UnidadTramitadora),
            OrganoProponente = SiNoEsElDeLaFicha(ReglasFacturae.ROL_ORGANO_PROPONENTE, OrganoProponente)
        };

        // Los DIR3 que ha dado la API sin mandárselos (los de la ficha): esos no se vuelven a mandar en la llamada
        private readonly Dictionary<string, string> _dir3DeLaFicha = new Dictionary<string, string>();

        private string SiNoEsElDeLaFicha(string rol, string escrito)
        {
            string limpio = ReglasFacturae.LimpiarDir3(escrito);
            return _dir3DeLaFicha.TryGetValue(rol, out string deLaFicha) && deLaFicha == limpio ? string.Empty : limpio;
        }

        private void ApuntarDir3DeLaFicha(string rol, string mandado, ComprobacionFacturaeModel comprobacion)
        {
            if (string.IsNullOrWhiteSpace(mandado))
            {
                _dir3DeLaFicha[rol] = ReglasFacturae.CodigoDelRol(comprobacion, rol);
            }
        }

        /// <summary>Se ha cambiado algo desde la última comprobación: hay que volver a comprobar antes de descargar.</summary>
        public bool HayCambiosSinComprobar => _comprobadoCon == null || !Iguales(_comprobadoCon, Referencias());

        private static bool Iguales(ReferenciasFacturae a, ReferenciasFacturae b) =>
            a.SuPedido == b.SuPedido && a.Expediente == b.Expediente && a.Contrato == b.Contrato &&
            a.OficinaContable == b.OficinaContable && a.OrganoGestor == b.OrganoGestor &&
            a.UnidadTramitadora == b.UnidadTramitadora && a.OrganoProponente == b.OrganoProponente;

        /// <summary>Lo escrito que no vale (largo de las referencias y formato de los DIR3). Vacío: se puede mandar.</summary>
        internal List<string> MotivosEntradaNoValida()
        {
            var motivos = new[]
            {
                ReglasFacturae.MotivoReferenciaLarga(SuPedido, "Su pedido"),
                ReglasFacturae.MotivoReferenciaLarga(Expediente, "El expediente"),
                ReglasFacturae.MotivoReferenciaLarga(Contrato, "El contrato"),
                ReglasFacturae.MotivoDir3NoValido(OficinaContable, "Oficina contable"),
                ReglasFacturae.MotivoDir3NoValido(OrganoGestor, "Órgano gestor"),
                ReglasFacturae.MotivoDir3NoValido(UnidadTramitadora, "Unidad tramitadora"),
                ReglasFacturae.MotivoDir3NoValido(OrganoProponente, "Órgano proponente")
            };
            return motivos.Where(m => m != null).ToList();
        }
        #endregion

        public bool PuedeDescargar => !Trabajando && Listo && !HayCambiosSinComprobar;

        public bool PuedeGuardarDir3EnFicha => !Trabajando && Comprobacion != null && !string.IsNullOrWhiteSpace(Comprobacion.Cliente)
            && !ReglasFacturae.SonTodosVacios(OficinaContable, OrganoGestor, UnidadTramitadora, OrganoProponente);

        private void NotificarComandos()
        {
            OnPropertyChanged(nameof(PuedeDescargar));
            OnPropertyChanged(nameof(PuedeGuardarDir3EnFicha));
            ComprobarCommand?.NotifyCanExecuteChanged();
            DescargarCommand?.NotifyCanExecuteChanged();
            GuardarDir3EnFichaCommand?.NotifyCanExecuteChanged();
        }

        public override void OnDialogOpened(ParametrosDialogo parameters)
        {
            _empresa = parameters.ContainsKey(PARAMETRO_EMPRESA) ? parameters.GetValue<string>(PARAMETRO_EMPRESA)?.Trim() : null;
            if (string.IsNullOrWhiteSpace(_empresa))
            {
                _empresa = Constantes.Empresas.EMPRESA_DEFECTO;
            }
            _factura = parameters.ContainsKey(PARAMETRO_FACTURA) ? parameters.GetValue<string>(PARAMETRO_FACTURA)?.Trim() : null;
            Title = $"{ReglasFacturae.TITULO}: factura {_factura}";
            OnPropertyChanged(nameof(Cabecera));
            _ = ComprobarAsync();
        }

        /// <summary>«Volver a comprobar» (y al abrir): con lo escrito. Los DIR3 que vengan se ponen en los campos.</summary>
        public async Task ComprobarAsync()
        {
            if (_servicio == null || string.IsNullOrWhiteSpace(_factura) || Trabajando)
            {
                return;
            }
            List<string> noValen = MotivosEntradaNoValida();
            if (noValen.Any())
            {
                Error = string.Join(Environment.NewLine, noValen);
                return;
            }
            ReferenciasFacturae referencias = Referencias();
            Error = null;
            Mensaje = null;
            Trabajando = true;
            try
            {
                ComprobacionFacturaeModel comprobacion = await _servicio.Comprobar(_empresa, _factura, referencias);
                Comprobacion = comprobacion;
                ApuntarDir3DeLaFicha(ReglasFacturae.ROL_OFICINA_CONTABLE, referencias.OficinaContable, comprobacion);
                ApuntarDir3DeLaFicha(ReglasFacturae.ROL_ORGANO_GESTOR, referencias.OrganoGestor, comprobacion);
                ApuntarDir3DeLaFicha(ReglasFacturae.ROL_UNIDAD_TRAMITADORA, referencias.UnidadTramitadora, comprobacion);
                ApuntarDir3DeLaFicha(ReglasFacturae.ROL_ORGANO_PROPONENTE, referencias.OrganoProponente, comprobacion);
                // Los campos se quedan con lo que va en el fichero: lo escrito o, si no, lo de la ficha / la factura
                _oficinaContable = Elegir(referencias.OficinaContable, ReglasFacturae.CodigoDelRol(comprobacion, ReglasFacturae.ROL_OFICINA_CONTABLE));
                _organoGestor = Elegir(referencias.OrganoGestor, ReglasFacturae.CodigoDelRol(comprobacion, ReglasFacturae.ROL_ORGANO_GESTOR));
                _unidadTramitadora = Elegir(referencias.UnidadTramitadora, ReglasFacturae.CodigoDelRol(comprobacion, ReglasFacturae.ROL_UNIDAD_TRAMITADORA));
                _organoProponente = Elegir(referencias.OrganoProponente, ReglasFacturae.CodigoDelRol(comprobacion, ReglasFacturae.ROL_ORGANO_PROPONENTE));
                OnPropertyChanged(nameof(OficinaContable));
                OnPropertyChanged(nameof(OrganoGestor));
                OnPropertyChanged(nameof(UnidadTramitadora));
                OnPropertyChanged(nameof(OrganoProponente));
                _comprobadoCon = Referencias();
            }
            catch (Exception ex)
            {
                Comprobacion = null;
                _comprobadoCon = null;
                Error = $"No se ha podido comprobar la factura {_factura}: {ex.InnerException?.Message ?? ex.Message}";
            }
            finally
            {
                Trabajando = false;
                OnPropertyChanged(nameof(HayCambiosSinComprobar));
                NotificarComandos();
            }
        }

        /// <summary>
        /// «Descargar»: la misma llamada sin comprobar. Propone guardarlo en Descargas con el nombre de la API y, guardado, abre la
        /// carpeta con el fichero marcado.
        /// </summary>
        public async Task DescargarAsync()
        {
            if (!PuedeDescargar || _guardador == null)
            {
                return;
            }
            Error = null;
            Mensaje = null;
            Trabajando = true;
            FicheroFacturae fichero;
            try
            {
                fichero = await _servicio.Descargar(_empresa, _factura, _comprobadoCon);
            }
            catch (Exception ex)
            {
                Error = $"No se ha podido descargar la factura {_factura}: {ex.InnerException?.Message ?? ex.Message}";
                return;
            }
            finally
            {
                Trabajando = false;
            }

            string ruta = _guardador.ElegirDondeGuardar("Guardar la factura para FACe", fichero.NombreFichero,
                ReglasFacturae.CarpetaDescargas(), "Factura electrónica Facturae (*.xml)|*.xml", ".xml");
            if (string.IsNullOrWhiteSpace(ruta))
            {
                return;
            }
            try
            {
                _guardador.Guardar(ruta, fichero.Contenido);
            }
            catch (Exception ex)
            {
                Error = $"No se ha podido guardar el fichero en {ruta}: {ex.Message}";
                return;
            }
            Descargado = ruta;
            Mensaje = $"Guardado en {ruta}. Ahora impórtelo en B2Brouter (los pasos, abajo).";
            _guardador.EnsenarEnCarpeta(ruta);
        }

        /// <summary>«Guardar en la ficha del cliente»: los DIR3 escritos, al cliente y contacto de la factura (PUT).</summary>
        public async Task GuardarDir3EnFichaAsync()
        {
            if (!PuedeGuardarDir3EnFicha)
            {
                return;
            }
            List<string> motivos = ReglasFacturae.MotivosNoSePuedeGuardarDir3(OficinaContable, OrganoGestor, UnidadTramitadora, OrganoProponente);
            if (motivos.Any())
            {
                Error = string.Join(Environment.NewLine, motivos);
                return;
            }
            Dictionary<string, string> nombres = (Comprobacion.Centros ?? new List<CentroFacturaeModel>())
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Rol))
                .GroupBy(c => c.Rol.Trim())
                .ToDictionary(g => g.Key, g => g.First().Nombre?.Trim());
            var datos = new Dir3ClienteModel
            {
                Empresa = string.IsNullOrWhiteSpace(Comprobacion.Empresa) ? _empresa : Comprobacion.Empresa.Trim(),
                Cliente = Comprobacion.Cliente?.Trim(),
                Contacto = Comprobacion.Contacto?.Trim(),
                OficinaContable = ReglasFacturae.LimpiarDir3(OficinaContable),
                OrganoGestor = ReglasFacturae.LimpiarDir3(OrganoGestor),
                UnidadTramitadora = ReglasFacturae.LimpiarDir3(UnidadTramitadora),
                OrganoProponente = NuloSiVacio(ReglasFacturae.LimpiarDir3(OrganoProponente)),
                NombreOficinaContable = NombreSiMismoCodigo(nombres, ReglasFacturae.ROL_OFICINA_CONTABLE, OficinaContable),
                NombreOrganoGestor = NombreSiMismoCodigo(nombres, ReglasFacturae.ROL_ORGANO_GESTOR, OrganoGestor),
                NombreUnidadTramitadora = NombreSiMismoCodigo(nombres, ReglasFacturae.ROL_UNIDAD_TRAMITADORA, UnidadTramitadora),
                NombreOrganoProponente = NombreSiMismoCodigo(nombres, ReglasFacturae.ROL_ORGANO_PROPONENTE, OrganoProponente)
            };
            Error = null;
            Mensaje = null;
            Trabajando = true;
            try
            {
                await _servicio.GuardarDir3(datos);
                Mensaje = $"Códigos DIR3 guardados en la ficha del cliente {datos.Cliente}/{datos.Contacto}.";
            }
            catch (Exception ex)
            {
                Error = $"No se han podido guardar los códigos DIR3 en la ficha: {ex.InnerException?.Message ?? ex.Message}";
            }
            finally
            {
                Trabajando = false;
            }
        }

        private string NombreSiMismoCodigo(Dictionary<string, string> nombres, string rol, string codigo)
            => ReglasFacturae.CodigoDelRol(Comprobacion, rol) == ReglasFacturae.LimpiarDir3(codigo) && nombres.TryGetValue(rol, out string nombre)
                ? NuloSiVacio(nombre) : null;

        private static string Elegir(string escrito, string deLaComprobacion) => string.IsNullOrWhiteSpace(escrito) ? deLaComprobacion : escrito;
        private static string Limpio(string texto) => texto?.Trim() ?? string.Empty;
        private static string NuloSiVacio(string texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        private static string Vacio(string texto) => string.IsNullOrWhiteSpace(texto) ? "(ninguno)" : texto.Trim();
    }
}
