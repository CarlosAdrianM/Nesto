using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ControlesUsuario.BultosPedido
{
    /// <summary>
    /// Nesto#522: los bultos del packing de Ariadna de un pedido, con su foto, para el detalle del pedido (junto al
    /// seguimiento de la agencia): ver la foto, descargarla y copiar el enlace para el cliente. Sin bultos (pedidos de
    /// antes de Ariadna o de tienda) no se enseña nada. Si la API falla al cargar, tampoco: es una ayuda.
    /// </summary>
    public class BultosPedidoViewModel : ObservableObject
    {
        internal const string AVISO_ENLACE_PUBLICO = "Enlace copiado. Ojo: cualquiera que tenga el enlace puede ver la foto.";

        private readonly IServicioBultosAriadna _servicio;
        private readonly string _servidorApi;
        private readonly IAccionesFotoBulto _acciones;
        private int _version;
        private int _pedido;

        public BultosPedidoViewModel(IServicioBultosAriadna servicio, string servidorApi)
            : this(servicio, servidorApi, new AccionesFotoBultoSistema())
        {
        }

        internal BultosPedidoViewModel(IServicioBultosAriadna servicio, string servidorApi, IAccionesFotoBulto acciones)
        {
            _servicio = servicio;
            _servidorApi = servidorApi;
            _acciones = acciones ?? new AccionesFotoBultoSistema();
        }

        /// <summary>Los bultos por envío (uno solo casi siempre).</summary>
        public ObservableCollection<GrupoBultosEnvio> Grupos { get; } = new ObservableCollection<GrupoBultosEnvio>();

        public bool HayBultos => Grupos.Count > 0;

        /// <summary>El pedido salió en varias entregas: se pone el título de cada envío.</summary>
        public bool HayVariosEnvios => Grupos.Count > 1;

        private string _resumen = string.Empty;
        /// <summary>«Ariadna: 2 bultos (2 con foto)».</summary>
        public string Resumen
        {
            get => _resumen;
            private set => SetProperty(ref _resumen, value);
        }

        private string _mensaje;
        /// <summary>Lo último que ha pasado al ver, descargar o copiar (o su error).</summary>
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

        /// <summary>
        /// Lee los bultos del pedido. Sin esperar desde el detalle: no lanza nunca. Un pedido sin grabar no tiene
        /// bultos; si mientras tanto se abre otro pedido, la respuesta del anterior se descarta.
        /// </summary>
        public async Task Cargar(string empresa, int pedido)
        {
            int version = ++_version;
            _pedido = pedido;
            Limpiar();
            if (pedido <= 0 || string.IsNullOrWhiteSpace(empresa) || _servicio == null)
            {
                return;
            }
            List<BultoAriadna> bultos;
            try
            {
                bultos = await _servicio.LeerBultosDelPedido(empresa, pedido);
            }
            catch (Exception)
            {
                bultos = null; // es una ayuda: sin respuesta no se enseña nada
            }
            if (version != _version || bultos == null)
            {
                return;
            }
            // NestoAPI#771: las fotos del packing sin caja (desde el 31/10/26). Si fallan, los bultos salen igual.
            List<FotoPackingAriadna> fotos;
            try
            {
                fotos = await _servicio.LeerFotosPackingDelPedido(empresa, pedido) ?? new List<FotoPackingAriadna>();
            }
            catch (Exception)
            {
                fotos = new List<FotoPackingAriadna>();
            }
            if (version != _version)
            {
                return;
            }
            List<BultoAriadna> distintos = bultos.Where(b => b != null).GroupBy(b => b.Id).Select(g => g.First()).ToList();
            foreach (IGrouping<int?, BultoAriadna> envio in distintos.GroupBy(b => b.NumeroEnvio).OrderBy(g => g.Key ?? int.MaxValue))
            {
                Grupos.Add(new GrupoBultosEnvio(envio.Key, envio.OrderBy(b => b.Bulto).Select(b => new BultoPedidoItem(b, this)).ToList()));
            }
            List<FotoPackingAriadna> fotosDistintas = fotos.Where(f => f != null).GroupBy(f => f.Id).Select(g => g.First())
                .OrderBy(f => f.FechaFoto ?? DateTime.MaxValue).ThenBy(f => f.Id).ToList();
            if (fotosDistintas.Count > 0)
            {
                Grupos.Add(new GrupoBultosEnvio(null, fotosDistintas.Select((f, i) => new BultoPedidoItem(f, i + 1, this)).ToList(), TITULO_FOTOS_PACKING));
            }
            Resumen = PropuestaBultosAriadna.Texto(distintos)
                + (fotosDistintas.Count == 0 ? string.Empty
                    : (distintos.Count > 0 ? " · " : "Ariadna: ")
                      + (fotosDistintas.Count == 1 ? "1 foto del packing" : $"{fotosDistintas.Count} fotos del packing"));
            NotificarGrupos();
        }

        /// <summary>NestoAPI#771: el título del grupo de las fotos del packing (sin caja).</summary>
        internal const string TITULO_FOTOS_PACKING = "Fotos del packing";

        private void Limpiar()
        {
            Grupos.Clear();
            Resumen = string.Empty;
            Mensaje = null;
            NotificarGrupos();
        }

        private void NotificarGrupos()
        {
            OnPropertyChanged(nameof(HayBultos));
            OnPropertyChanged(nameof(HayVariosEnvios));
        }

        /// <summary>«Pedido_928123_bulto_2.jpg» (NestoAPI#771: «Pedido_928123_packing_1.jpg» para una foto del packing).</summary>
        internal string NombreFichero(BultoPedidoItem bulto)
            => bulto.EsFotoDelPacking ? $"Pedido_{_pedido}_packing_{bulto.Bulto}.jpg" : $"Pedido_{_pedido}_bulto_{bulto.Bulto}.jpg";

        /// <summary>NestoAPI#771: la imagen, por el endpoint de los bultos o por el de las fotos del packing.</summary>
        private Task<byte[]> BajarImagen(BultoPedidoItem bulto)
            => bulto.EsFotoDelPacking ? _servicio.DescargarFotoPacking(bulto.Id) : _servicio.DescargarFoto(bulto.Id);

        /// <summary>La baja (con un enlace temporal recién pedido) a la carpeta temporal y la abre con el visor del sistema.</summary>
        internal async Task VerFoto(BultoPedidoItem bulto)
        {
            if (bulto == null || !bulto.TieneFoto || _servicio == null)
            {
                return;
            }
            Mensaje = null;
            try
            {
                byte[] foto = await BajarImagen(bulto);
                if (foto == null)
                {
                    Mensaje = $"{bulto.Nombre} ya no tiene foto.";
                    return;
                }
                // Con la hora en el nombre: si la anterior sigue abierta en el visor, no se puede sobrescribir
                string ruta = Path.Combine(_acciones.CarpetaTemporal,
                    $"{Path.GetFileNameWithoutExtension(NombreFichero(bulto))}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
                _acciones.Guardar(ruta, foto);
                _acciones.Abrir(ruta);
            }
            catch (Exception ex)
            {
                Mensaje = $"No se ha podido abrir la foto {bulto.DeQue}: {ex.Message}";
            }
        }

        /// <summary>Pregunta dónde guardarla (con el nombre «Pedido_928123_bulto_2.jpg») y la guarda.</summary>
        internal async Task Descargar(BultoPedidoItem bulto)
        {
            if (bulto == null || !bulto.TieneFoto || _servicio == null)
            {
                return;
            }
            Mensaje = null;
            try
            {
                string ruta = _acciones.ElegirDondeGuardar(NombreFichero(bulto));
                if (string.IsNullOrWhiteSpace(ruta))
                {
                    return;
                }
                byte[] foto = await BajarImagen(bulto);
                if (foto == null)
                {
                    Mensaje = $"{bulto.Nombre} ya no tiene foto.";
                    return;
                }
                _acciones.Guardar(ruta, foto);
                Mensaje = $"Foto guardada en {ruta}";
            }
            catch (Exception ex)
            {
                Mensaje = $"No se ha podido descargar la foto {bulto.DeQue}: {ex.Message}";
            }
        }

        /// <summary>El enlace completo (servidor de la API + ruta pública), que no pide usuario.</summary>
        internal string EnlacePublico(BultoPedidoItem bulto) => EnlacePublicoFotoBulto.Componer(_servidorApi, bulto?.RutaFotoPublica);

        internal void CopiarEnlace(BultoPedidoItem bulto)
        {
            string enlace = EnlacePublico(bulto);
            if (enlace == null)
            {
                return;
            }
            try
            {
                _acciones.CopiarAlPortapapeles(enlace);
                Mensaje = AVISO_ENLACE_PUBLICO;
            }
            catch (Exception ex)
            {
                Mensaje = $"No se ha podido copiar el enlace: {ex.Message}";
            }
        }
    }

    /// <summary>Nesto#522: los bultos que salieron en un mismo envío de la agencia.</summary>
    public class GrupoBultosEnvio
    {
        public GrupoBultosEnvio(int? numeroEnvio, IReadOnlyList<BultoPedidoItem> bultos, string titulo = null)
        {
            NumeroEnvio = numeroEnvio;
            Bultos = bultos ?? Array.Empty<BultoPedidoItem>();
            _titulo = titulo;
        }

        private readonly string _titulo;

        public int? NumeroEnvio { get; }
        public IReadOnlyList<BultoPedidoItem> Bultos { get; }
        public string Titulo => _titulo ?? (NumeroEnvio.HasValue ? $"Envío {NumeroEnvio}" : "Sin envío todavía");
    }

    /// <summary>Nesto#522: un bulto del packing en el detalle del pedido.</summary>
    public class BultoPedidoItem
    {
        private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-ES");
        private readonly BultoAriadna _bulto;

        internal BultoPedidoItem(BultoAriadna bulto, BultosPedidoViewModel padre)
        {
            _bulto = bulto;
            VerFotoCommand = new AsyncRelayCommand(() => padre.VerFoto(this), () => TieneFoto);
            DescargarCommand = new AsyncRelayCommand(() => padre.Descargar(this), () => TieneFoto);
            CopiarEnlaceCommand = new RelayCommand(() => padre.CopiarEnlace(this), () => TieneEnlacePublico);
            TieneEnlacePublico = TieneFoto && padre.EnlacePublico(this) != null;
        }

        /// <summary>
        /// NestoAPI#771: una foto del packing SIN caja (desde el 31/10/26), con las mismas acciones que la de un bulto. <paramref name="orden"/>
        /// es su número entre las fotos del pedido («Foto del packing 2»).
        /// </summary>
        internal BultoPedidoItem(FotoPackingAriadna foto, int orden, BultosPedidoViewModel padre)
            : this(new BultoAriadna
            {
                Id = foto.Id,
                Pedido = foto.Pedido,
                Picking = foto.Picking,
                Bulto = orden,
                TieneFoto = true,
                Usuario = foto.Usuario,
                FechaFoto = foto.FechaFoto,
                RutaFotoPublica = foto.RutaFotoPublica
            }, padre, esFotoDelPacking: true)
        {
        }

        private BultoPedidoItem(BultoAriadna bulto, BultosPedidoViewModel padre, bool esFotoDelPacking) : this(bulto, padre)
        {
            EsFotoDelPacking = esFotoDelPacking;
        }

        public int Id => _bulto.Id;
        public int Bulto => _bulto.Bulto;
        public bool TieneFoto => _bulto.TieneFoto;
        public string RutaFotoPublica => _bulto.RutaFotoPublica;
        public bool TieneEnlacePublico { get; }
        /// <summary>NestoAPI#771: es una foto del packing sin caja (no un bulto).</summary>
        public bool EsFotoDelPacking { get; }
        /// <summary>«El bulto 2» / «La foto del packing 1».</summary>
        internal string Nombre => EsFotoDelPacking ? $"La foto del packing {Bulto}" : $"El bulto {Bulto}";
        /// <summary>«del bulto 2» / «del packing 1».</summary>
        internal string DeQue => EsFotoDelPacking ? $"del packing {Bulto}" : $"del bulto {Bulto}";

        /// <summary>«Bulto 2 · 3,5 kg · cerrado por juan el 09/10/26 10:32» (NestoAPI#771: «Foto del packing 1 · hecha por juan el …»).</summary>
        public string Texto
        {
            get
            {
                if (EsFotoDelPacking)
                {
                    string quien = UsuarioSinDominio(_bulto.Usuario);
                    string cuando = _bulto.FechaFoto?.ToString("dd/MM/yy HH:mm", Espanol);
                    return $"Foto del packing {Bulto}" + (quien != null || cuando != null
                        ? " · hecha" + (quien != null ? $" por {quien}" : string.Empty) + (cuando != null ? $" el {cuando}" : string.Empty)
                        : string.Empty);
                }
                var partes = new List<string> { $"Bulto {Bulto}" };
                if (_bulto.Peso.HasValue && _bulto.Peso.Value > 0)
                {
                    partes.Add($"{_bulto.Peso.Value.ToString("0.##", Espanol)} kg");
                }
                string usuario = UsuarioSinDominio(_bulto.Usuario);
                string fecha = _bulto.FechaFoto?.ToString("dd/MM/yy HH:mm", Espanol);
                if (usuario != null || fecha != null)
                {
                    partes.Add("cerrado" + (usuario != null ? $" por {usuario}" : string.Empty) + (fecha != null ? $" el {fecha}" : string.Empty));
                }
                if (!TieneFoto)
                {
                    partes.Add("sin foto");
                }
                return string.Join(" · ", partes);
            }
        }

        public IAsyncRelayCommand VerFotoCommand { get; }
        public IAsyncRelayCommand DescargarCommand { get; }
        public IRelayCommand CopiarEnlaceCommand { get; }

        private static string UsuarioSinDominio(string usuario)
        {
            if (string.IsNullOrWhiteSpace(usuario))
            {
                return null;
            }
            string limpio = usuario.Trim();
            int barra = limpio.LastIndexOf('\\');
            return barra >= 0 ? limpio.Substring(barra + 1) : limpio;
        }
    }
}
