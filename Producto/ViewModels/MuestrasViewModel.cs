using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlesUsuario.Services;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto.ViewModels
{
    /// <summary>Un almacén que se puede elegir para las etiquetas de muestras.</summary>
    public class OpcionAlmacenMuestras
    {
        public OpcionAlmacenMuestras(string almacen, string texto)
        {
            Almacen = almacen;
            Texto = texto;
        }

        public string Almacen { get; }
        public string Texto { get; }
        public override string ToString() => Texto;
    }

    /// <summary>Una impresora que se puede elegir; <see cref="Impresora"/> null = la del almacén (la decide la API).</summary>
    public class OpcionImpresoraMuestras
    {
        public OpcionImpresoraMuestras(string impresora, string texto)
        {
            Impresora = impresora;
            Texto = texto;
        }

        public string Impresora { get; }
        public string Texto { get; }
        public override string ToString() => Texto;
    }

    /// <summary>Una fila de la lista: la etiqueta del ensayo y si se quiere imprimir.</summary>
    public class FilaEtiquetaMuestra : ObservableObject
    {
        public const string TEXTO_SIN_CODIGO = "Sin código (se le dará el interno al imprimir)";

        public FilaEtiquetaMuestra(EtiquetaMuestra etiqueta)
        {
            Etiqueta = etiqueta ?? new EtiquetaMuestra();
        }

        public EtiquetaMuestra Etiqueta { get; private set; }

        public int Orden => Etiqueta.Orden;
        public string Producto => Etiqueta.Producto?.Trim();
        public string Nombre => Etiqueta.Nombre?.Trim();
        public string Familia => Etiqueta.Familia?.Trim();
        public string Hueco => Etiqueta.Hueco?.Trim();
        public int Stock => Etiqueta.Stock;
        public bool SinCodigo => string.IsNullOrWhiteSpace(Etiqueta.CodigoBarras);
        public string TextoCodigo => SinCodigo
            ? TEXTO_SIN_CODIGO
            : Etiqueta.CodigoBarras.Trim() + (Etiqueta.CodigoAsignado ? " (dado ahora)" : Etiqueta.CodigoInterno ? " (interno)" : string.Empty);

        private bool _marcada = true;
        public bool Marcada { get => _marcada; set => SetProperty(ref _marcada, value); }

        /// <summary>Tras imprimir: el código que le ha dado la API.</summary>
        internal void ActualizarCodigo(EtiquetaMuestra impresa)
        {
            if (impresa == null)
            {
                return;
            }
            Etiqueta.CodigoBarras = impresa.CodigoBarras;
            Etiqueta.CodigoInterno = impresa.CodigoInterno;
            Etiqueta.CodigoAsignado = impresa.CodigoAsignado;
            OnPropertyChanged(nameof(SinCodigo));
            OnPropertyChanged(nameof(TextoCodigo));
        }
    }

    /// <summary>
    /// Nesto#650 (gemela de NestoAPI#884): las muestras (subgrupo MMP) sin código de fabricante llevan UNA etiqueta por referencia
    /// en el cajón del mostrador (REI, ALC) o en la gaveta / el hueco (ALG), con el EAN-13 interno del 29. Desde aquí: ver qué etiquetas
    /// saldrían (ensayo), imprimirlas todas o solo las marcadas, dar el código interno en bloque a las muestras sin código, y sacar la
    /// hoja de muestras por marca en PDF (plan B del mostrador). Todo pasa antes por el ensayo de la API y se confirma con sus cifras
    /// (con «No» por defecto). La lógica está en la API; aquí solo se pregunta y se enseña lo que contesta.
    /// </summary>
    public class MuestrasViewModel : ObservableObject, IReceptorNavegacion
    {
        public const string TITULO = "Muestras";
        public const string OPCION_IMPRESORA_DEL_ALMACEN = "La del almacén";
        public const string FILTRO_PDF = "Documento PDF (*.pdf)|*.pdf";
        /// <summary>Cuántas muestras se enseñan una a una en la pregunta de dar el código.</summary>
        public const int MAXIMO_EN_LA_PREGUNTA = 25;

        private readonly IServicioMuestras _servicio;
        private readonly IConfiguracion _configuracion;
        private readonly IServicioDialogos _dialogos;
        private readonly IGuardadorFicherosDescarga _guardador;
        private readonly Action<string> _abrir;
        private bool _preparado;

        public MuestrasViewModel(IServicioMuestras servicio, IConfiguracion configuracion, IServicioDialogos dialogos,
            IGuardadorFicherosDescarga guardador)
            : this(servicio, configuracion, dialogos, guardador, null)
        {
        }

        /// <param name="abrir">Cómo se abre el PDF guardado (los tests no abren nada); por defecto, el visor del sistema.</param>
        internal MuestrasViewModel(IServicioMuestras servicio, IConfiguracion configuracion, IServicioDialogos dialogos,
            IGuardadorFicherosDescarga guardador, Action<string> abrir)
        {
            _servicio = servicio;
            _configuracion = configuracion;
            _dialogos = dialogos;
            _guardador = guardador;
            _abrir = abrir ?? AbridorFicheros.AbrirConElVisorDelSistema;

            Almacenes = new List<OpcionAlmacenMuestras>
            {
                new OpcionAlmacenMuestras(Constantes.Almacenes.ALMACEN_ALGETE, "Algete (ALG)"),
                new OpcionAlmacenMuestras(Constantes.Almacenes.ALMACEN_REINA, "Reina (REI)"),
                new OpcionAlmacenMuestras(Constantes.Almacenes.ALMACEN_ALCOBENDAS, "Alcobendas (ALC)")
            };
            _almacen = Almacenes[0];
            Impresoras.Add(new OpcionImpresoraMuestras(null, OPCION_IMPRESORA_DEL_ALMACEN));
            _impresora = Impresoras[0];
            PuedeDarCodigo = PuedeDarCodigoInterno(configuracion);

            VerCommand = new AsyncRelayCommand(VerAsync, () => !EstaOcupado);
            ImprimirCommand = new AsyncRelayCommand(ImprimirAsync, () => !EstaOcupado);
            DarCodigoCommand = new AsyncRelayCommand(DarCodigoAsync, () => !EstaOcupado && PuedeDarCodigo);
            HojaCommand = new AsyncRelayCommand(HojaAsync, () => !EstaOcupado);
            MarcarTodasCommand = new RelayCommand(() => Marcar(true), () => !EstaOcupado);
            DesmarcarTodasCommand = new RelayCommand(() => Marcar(false), () => !EstaOcupado);
        }

        public string Titulo => TITULO;
        public string Empresa { get; set; } = Constantes.Empresas.EMPRESA_DEFECTO;

        /// <summary>Quién abre la ventana (los mismos que la etiqueta de producto; la API lo vuelve a comprobar).</summary>
        public static bool PuedeUsarMuestras(IConfiguracion configuracion)
            => configuracion != null
                && (configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)
                    || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.COMPRAS)
                    || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)
                    || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)
                    || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA));

        /// <summary>Dar el código interno en bloque: Almacén, Compras, Dirección e Informática (a los demás no se les enseña).</summary>
        public static bool PuedeDarCodigoInterno(IConfiguracion configuracion)
            => configuracion != null
                && (configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)
                    || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.COMPRAS)
                    || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)
                    || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA));

        public bool PuedeDarCodigo { get; }

        public List<OpcionAlmacenMuestras> Almacenes { get; }

        private OpcionAlmacenMuestras _almacen;
        public OpcionAlmacenMuestras Almacen { get => _almacen; set => SetProperty(ref _almacen, value); }

        private string _familia;
        /// <summary>La marca (código o nombre); vacía = todas.</summary>
        public string Familia { get => _familia; set => SetProperty(ref _familia, value); }

        private bool _soloConStock = true;
        public bool SoloConStock { get => _soloConStock; set => SetProperty(ref _soloConStock, value); }

        private bool _conFotos = true;
        /// <summary>La hoja con la foto de la tienda (sin ellas sale antes).</summary>
        public bool ConFotos { get => _conFotos; set => SetProperty(ref _conFotos, value); }

        public ObservableCollection<OpcionImpresoraMuestras> Impresoras { get; } = new ObservableCollection<OpcionImpresoraMuestras>();

        private OpcionImpresoraMuestras _impresora;
        public OpcionImpresoraMuestras Impresora { get => _impresora; set => SetProperty(ref _impresora, value); }

        private bool _puedeElegirImpresora;
        /// <summary>False con una API que todavía no da la lista de impresoras: se imprime en la del almacén.</summary>
        public bool PuedeElegirImpresora { get => _puedeElegirImpresora; private set => SetProperty(ref _puedeElegirImpresora, value); }

        public ObservableCollection<FilaEtiquetaMuestra> Etiquetas { get; } = new ObservableCollection<FilaEtiquetaMuestra>();

        /// <summary>Errores uno a uno de la última operación (las muestras a las que no se ha podido dar el código).</summary>
        public ObservableCollection<string> Errores { get; } = new ObservableCollection<string>();

        private string _mensaje;
        public string Mensaje { get => _mensaje; set => SetProperty(ref _mensaje, value); }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    VerCommand.NotifyCanExecuteChanged();
                    ImprimirCommand.NotifyCanExecuteChanged();
                    DarCodigoCommand.NotifyCanExecuteChanged();
                    HojaCommand.NotifyCanExecuteChanged();
                    MarcarTodasCommand.NotifyCanExecuteChanged();
                    DesmarcarTodasCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public IAsyncRelayCommand VerCommand { get; }
        public IAsyncRelayCommand ImprimirCommand { get; }
        public IAsyncRelayCommand DarCodigoCommand { get; }
        public IAsyncRelayCommand HojaCommand { get; }
        public IRelayCommand MarcarTodasCommand { get; }
        public IRelayCommand DesmarcarTodasCommand { get; }

        public void AlLlegar(ParametrosNavegacion parametros)
        {
            _ = PrepararAsync();
        }

        /// <summary>Una vez: el almacén del usuario (su AlmacénPedidoVta) y las impresoras que se pueden elegir.</summary>
        internal async Task PrepararAsync()
        {
            if (_preparado)
            {
                return;
            }
            _preparado = true;
            await LeerAlmacenDelUsuarioAsync().ConfigureAwait(true);
            await LeerImpresorasAsync().ConfigureAwait(true);
        }

        private async Task LeerAlmacenDelUsuarioAsync()
        {
            if (_configuracion == null)
            {
                return;
            }
            try
            {
                string almacen = (await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenPedidoVta)
                    .ConfigureAwait(true))?.Trim();
                OpcionAlmacenMuestras suyo = Almacenes.FirstOrDefault(a => string.Equals(a.Almacen, almacen, StringComparison.OrdinalIgnoreCase));
                if (suyo != null)
                {
                    Almacen = suyo;
                }
            }
            catch (Exception)
            {
                // Solo es el almacén que sale elegido: se queda Algete
            }
        }

        private async Task LeerImpresorasAsync()
        {
            List<ImpresoraEtiquetasMuestras> lista;
            try
            {
                lista = await _servicio.LeerImpresoras().ConfigureAwait(true);
            }
            catch (Exception)
            {
                lista = null;
            }
            Impresoras.Clear();
            Impresoras.Add(new OpcionImpresoraMuestras(null, OPCION_IMPRESORA_DEL_ALMACEN));
            foreach (ImpresoraEtiquetasMuestras impresora in (lista ?? new List<ImpresoraEtiquetasMuestras>())
                .Where(i => !string.IsNullOrWhiteSpace(i?.Impresora)))
            {
                string nombre = string.IsNullOrWhiteSpace(impresora.Nombre) ? impresora.Almacen?.Trim() : impresora.Nombre.Trim();
                string texto = string.IsNullOrWhiteSpace(nombre)
                    ? impresora.Impresora.Trim()
                    : $"{nombre} ({impresora.Impresora.Trim()})";
                if (impresora.EsLaDelUsuario)
                {
                    texto += " · la suya";
                }
                Impresoras.Add(new OpcionImpresoraMuestras(impresora.Impresora.Trim(), texto));
            }
            Impresora = Impresoras[0];
            PuedeElegirImpresora = Impresoras.Count > 1;
        }

        /// <summary>La petición con los filtros de arriba; <paramref name="productos"/> solo para imprimir unas pocas.</summary>
        internal PeticionEtiquetasMuestras Peticion(List<string> productos)
            => new PeticionEtiquetasMuestras
            {
                Almacen = Almacen?.Almacen,
                Familia = string.IsNullOrWhiteSpace(Familia) ? null : Familia.Trim(),
                SoloConStock = SoloConStock,
                Productos = productos,
                Impresora = string.IsNullOrWhiteSpace(Impresora?.Impresora) ? null : Impresora.Impresora
            };

        private async Task VerAsync()
        {
            EstaOcupado = true;
            try
            {
                Errores.Clear();
                ResultadoEtiquetasMuestras resultado = await _servicio.Etiquetas(Empresa, Peticion(null), ensayo: true).ConfigureAwait(true);
                PintarLista(resultado);
                Mensaje = resultado?.Mensaje;
            }
            catch (Exception ex) when (EsErrorConocido(ex))
            {
                EnsenarError(ex, "ver las etiquetas de muestras");
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        private void PintarLista(ResultadoEtiquetasMuestras resultado)
        {
            Etiquetas.Clear();
            foreach (EtiquetaMuestra etiqueta in (resultado?.Etiquetas ?? new List<EtiquetaMuestra>()).OrderBy(e => e.Orden))
            {
                Etiquetas.Add(new FilaEtiquetaMuestra(etiqueta));
            }
        }

        private void Marcar(bool marcada)
        {
            foreach (FilaEtiquetaMuestra fila in Etiquetas)
            {
                fila.Marcada = marcada;
            }
        }

        /// <summary>
        /// Las referencias marcadas, o null si son todas (o no se ha pulsado «Ver»): entonces salen todas las de los filtros. Lista
        /// vacía si hay lista y no hay ninguna marcada.
        /// </summary>
        internal List<string> ProductosMarcados()
        {
            if (Etiquetas.Count == 0 || Etiquetas.All(f => f.Marcada))
            {
                return null;
            }
            return Etiquetas.Where(f => f.Marcada && !string.IsNullOrWhiteSpace(f.Producto))
                .Select(f => f.Producto)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task ImprimirAsync()
        {
            List<string> productos = ProductosMarcados();
            if (productos != null && productos.Count == 0)
            {
                Mensaje = "No hay ninguna etiqueta marcada: marque las que quiere imprimir.";
                return;
            }
            PeticionEtiquetasMuestras peticion = Peticion(productos);
            EstaOcupado = true;
            try
            {
                Errores.Clear();
                // Primero el ensayo: las cifras de verdad antes de gastar etiquetas o dar códigos
                ResultadoEtiquetasMuestras previa = await _servicio.Etiquetas(Empresa, peticion, ensayo: true).ConfigureAwait(true);
                if (previa == null || previa.Total <= 0)
                {
                    Mensaje = previa?.Mensaje ?? "No sale ninguna etiqueta con esos filtros.";
                    return;
                }
                if (!_dialogos.ShowConfirmationAnswer("Imprimir etiquetas de muestras", TextoConfirmarImpresion(previa, peticion.Impresora), porDefectoNo: true))
                {
                    Mensaje = "No se ha impreso nada.";
                    return;
                }
                ResultadoEtiquetasMuestras impresas = await _servicio.Etiquetas(Empresa, peticion, ensayo: false).ConfigureAwait(true);
                ActualizarCodigos(impresas);
                Mensaje = TextoResumenImpresion(impresas);
            }
            catch (Exception ex) when (EsErrorConocido(ex))
            {
                EnsenarError(ex, "imprimir las etiquetas de muestras");
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        /// <summary>«Van a salir 70 etiquetas en \\RDS2016\etiquetasalcobendas; a 10 sin código se les dará el interno. ¿Imprimir?»</summary>
        internal static string TextoConfirmarImpresion(ResultadoEtiquetasMuestras previa, string impresoraElegida)
        {
            string impresora = !string.IsNullOrWhiteSpace(impresoraElegida) ? impresoraElegida.Trim()
                : !string.IsNullOrWhiteSpace(previa.Impresora) ? previa.Impresora.Trim()
                : "la impresora del almacén";
            var texto = new StringBuilder();
            texto.Append(previa.Total == 1 ? "Va a salir 1 etiqueta" : $"Van a salir {previa.Total} etiquetas");
            texto.Append($" en {impresora}");
            if (previa.SinCodigo > 0)
            {
                texto.Append(previa.SinCodigo == 1
                    ? "; a 1 sin código se le dará el interno"
                    : $"; a {previa.SinCodigo} sin código se les dará el interno");
            }
            texto.Append('.');
            if (previa.NoSonMuestras?.Count > 0)
            {
                texto.Append($" No son muestras activas y no salen: {string.Join(", ", previa.NoSonMuestras.Select(p => p?.Trim()))}.");
            }
            texto.Append(" ¿Imprimir?");
            return texto.ToString();
        }

        internal static string TextoResumenImpresion(ResultadoEtiquetasMuestras impresas)
        {
            if (impresas == null)
            {
                return "La API no ha dicho qué ha impreso.";
            }
            var texto = new StringBuilder();
            texto.Append(impresas.Impresas == 1 ? "Impresa 1 etiqueta" : $"Impresas {impresas.Impresas} etiquetas");
            if (!string.IsNullOrWhiteSpace(impresas.Impresora))
            {
                texto.Append($" en {impresas.Impresora.Trim()}");
            }
            texto.Append('.');
            if (impresas.CodigosAsignados > 0)
            {
                texto.Append(impresas.CodigosAsignados == 1
                    ? " Se ha dado el código interno a 1 muestra."
                    : $" Se ha dado el código interno a {impresas.CodigosAsignados} muestras.");
            }
            if (!string.IsNullOrWhiteSpace(impresas.Mensaje))
            {
                texto.Append(' ').Append(impresas.Mensaje.Trim());
            }
            return texto.ToString();
        }

        private void ActualizarCodigos(ResultadoEtiquetasMuestras impresas)
        {
            foreach (EtiquetaMuestra impresa in impresas?.Etiquetas ?? new List<EtiquetaMuestra>())
            {
                foreach (FilaEtiquetaMuestra fila in Etiquetas.Where(f => string.Equals(f.Producto, impresa.Producto?.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    fila.ActualizarCodigo(impresa);
                }
            }
        }

        private async Task DarCodigoAsync()
        {
            if (!PuedeDarCodigo)
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                Errores.Clear();
                ResultadoCodigoInternoMuestras previa = await _servicio.DarCodigoInterno(Empresa, ensayo: true).ConfigureAwait(true);
                if (previa == null || previa.Total <= 0)
                {
                    Mensaje = previa?.Mensaje ?? "No hay ninguna muestra sin código.";
                    return;
                }
                if (!_dialogos.ShowConfirmationAnswer("Dar código a las muestras sin código", TextoConfirmarCodigos(previa), porDefectoNo: true))
                {
                    Mensaje = "No se ha dado ningún código.";
                    return;
                }
                ResultadoCodigoInternoMuestras hecho = await _servicio.DarCodigoInterno(Empresa, ensayo: false).ConfigureAwait(true);
                foreach (MuestraCodigoInterno conError in (hecho?.Muestras ?? new List<MuestraCodigoInterno>()).Where(m => !string.IsNullOrWhiteSpace(m.Error)))
                {
                    Errores.Add($"{conError.Producto?.Trim()} {conError.Nombre?.Trim()}: {conError.Error.Trim()}");
                }
                Mensaje = hecho?.Mensaje ?? $"Se ha dado el código interno a {hecho?.Asignados ?? 0} muestras.";
                if (Errores.Count > 0)
                {
                    _dialogos.ShowError($"{Mensaje}\n\nNo se ha podido con {Errores.Count} (se puede volver a lanzar):\n{string.Join("\n", Errores)}");
                }
            }
            catch (Exception ex) when (EsErrorConocido(ex))
            {
                EnsenarError(ex, "dar código a las muestras");
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        internal static string TextoConfirmarCodigos(ResultadoCodigoInternoMuestras previa)
        {
            var texto = new StringBuilder();
            texto.AppendLine(string.IsNullOrWhiteSpace(previa.Mensaje)
                ? (previa.Total == 1 ? "Hay 1 muestra sin código." : $"Hay {previa.Total} muestras sin código.")
                : previa.Mensaje.Trim());
            texto.AppendLine();
            List<MuestraCodigoInterno> muestras = previa.Muestras ?? new List<MuestraCodigoInterno>();
            foreach (MuestraCodigoInterno muestra in muestras.Take(MAXIMO_EN_LA_PREGUNTA))
            {
                string linea = $"{muestra.Producto?.Trim()} {muestra.Nombre?.Trim()}";
                if (!string.IsNullOrWhiteSpace(muestra.Familia))
                {
                    linea += $" ({muestra.Familia.Trim()})";
                }
                if (!string.IsNullOrWhiteSpace(muestra.Codigo))
                {
                    linea += $" → {muestra.Codigo.Trim()}";
                }
                texto.AppendLine(linea);
            }
            if (muestras.Count > MAXIMO_EN_LA_PREGUNTA)
            {
                texto.AppendLine($"… y {muestras.Count - MAXIMO_EN_LA_PREGUNTA} más.");
            }
            texto.AppendLine();
            texto.Append(previa.Total == 1 ? "¿Le doy el código?" : "¿Les doy el código?");
            return texto.ToString();
        }

        private async Task HojaAsync()
        {
            if (_guardador == null)
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                Errores.Clear();
                string almacen = Almacen?.Almacen;
                string familia = string.IsNullOrWhiteSpace(Familia) ? null : Familia.Trim();
                FicheroMuestras hoja = await _servicio.Hoja(Empresa, almacen, familia, SoloConStock, ConFotos).ConfigureAwait(true);
                string nombre = NombreHoja(hoja?.Nombre, familia, almacen);
                string ruta = _guardador.ElegirDondeGuardar("Guardar la hoja de muestras", nombre, CarpetaDescargas(), FILTRO_PDF, ".pdf");
                if (string.IsNullOrWhiteSpace(ruta))
                {
                    Mensaje = "No se ha guardado la hoja.";
                    return;
                }
                _guardador.Guardar(ruta, hoja?.Contenido);
                Mensaje = $"Hoja de muestras guardada en {ruta}.";
                try
                {
                    _abrir(ruta);
                }
                catch (Exception ex)
                {
                    Mensaje += $" No se ha podido abrir: {ex.Message}";
                }
            }
            catch (Exception ex) when (EsErrorConocido(ex) || ex is IOException || ex is UnauthorizedAccessException)
            {
                EnsenarError(ex, "sacar la hoja de muestras");
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        /// <summary>El nombre que da la API («Muestras Lisap REI.pdf») o, si no viene, uno parecido.</summary>
        internal static string NombreHoja(string nombreApi, string familia, string almacen)
        {
            if (!string.IsNullOrWhiteSpace(nombreApi))
            {
                return Path.GetFileName(nombreApi.Trim());
            }
            string nombre = string.Join(" ", new[] { "Muestras", familia, almacen }.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
            foreach (char invalido in Path.GetInvalidFileNameChars())
            {
                nombre = nombre.Replace(invalido, '_');
            }
            return nombre + ".pdf";
        }

        /// <summary>La carpeta Descargas del usuario (en el escritorio remoto, la suya del servidor).</summary>
        private static string CarpetaDescargas()
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        private static bool EsErrorConocido(Exception ex)
            => ex is MuestrasException || ex is HttpRequestException || ex is TaskCanceledException;

        private void EnsenarError(Exception ex, string queSeHacia)
        {
            string texto = ex is MuestrasException
                ? ex.Message
                : $"No se puede conectar con el servidor para {queSeHacia}: {ex.Message}";
            Mensaje = texto;
            _dialogos.ShowError(texto);
        }
    }
}
