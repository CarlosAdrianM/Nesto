using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto
{
    /// <summary>
    /// NestoAPI#906: las fotos que se hacen a cada bulto al enviar una reposición («evitará disputas»). Qué bultos tienen foto sale del
    /// seguimiento (GET api/Reposiciones/{traspaso}/Seguimiento, <c>TieneFoto</c> en cada bulto) y la foto, de un enlace de 15 minutos
    /// (GET …/Bultos/{bulto}/Foto?soloEnlace=true).
    /// </summary>
    public interface IServicioFotosBultosReposicion
    {
        /// <summary>Los bultos con foto. Null si no se puede saber (la API falla o es anterior).</summary>
        Task<List<int>> BultosConFoto(string empresa, int traspaso);

        /// <summary>El enlace de la foto. Lanza una excepción con el mensaje de la API si no se puede.</summary>
        Task<string> EnlaceFoto(string empresa, int traspaso, int bulto);
    }

    public class ServicioFotosBultosReposicion : IServicioFotosBultosReposicion
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioFotosBultosReposicion(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        private static string Empresa(string empresa) => Uri.EscapeDataString(string.IsNullOrWhiteSpace(empresa) ? Constantes.Empresas.EMPRESA_DEFECTO : empresa.Trim());

        internal static string UrlSeguimiento(string empresa, int traspaso) => $"Reposiciones/{traspaso}/Seguimiento?empresa={Empresa(empresa)}";

        internal static string UrlFoto(string empresa, int traspaso, int bulto) => $"Reposiciones/{traspaso}/Bultos/{bulto}/Foto?empresa={Empresa(empresa)}&soloEnlace=true";

        public async Task<List<int>> BultosConFoto(string empresa, int traspaso)
        {
            try
            {
                using (HttpClient client = _clienteApiFactory.Crear())
                {
                    HttpResponseMessage response = await client.GetAsync(UrlSeguimiento(empresa, traspaso)).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }
                    SeguimientoReposicion seguimiento = JsonConvert.DeserializeObject<SeguimientoReposicion>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                    return (seguimiento?.Bultos ?? new List<BultoSeguimiento>()).Where(b => b.TieneFoto).Select(b => b.Bulto).Distinct().OrderBy(b => b).ToList();
                }
            }
            catch (Exception)
            {
                return null; // es información de apoyo: sin ella, no sale el botón
            }
        }

        public async Task<string> EnlaceFoto(string empresa, int traspaso, int bulto)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync(UrlFoto(empresa, traspaso, bulto)).ConfigureAwait(false);
                string cuerpo = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(HttpErrorHelper.ParsearErrorHttp(cuerpo));
                }
                return JObject.Parse(cuerpo)["Url"]?.ToString();
            }
        }
    }

    /// <summary>
    /// NestoAPI#906: el botón «Ver fotos de los bultos» de «Diferencias de reposición» y «Recibir reposición» (núcleo común de las dos):
    /// al elegir una reposición se mira si algún bulto tiene foto y, al pulsarlo, se abre en el navegador la de cada uno. Una
    /// comprobación que llega cuando ya se ha elegido otra se descarta.
    /// </summary>
    public class VisorFotosBultosReposicion : ObservableObject
    {
        private readonly IServicioFotosBultosReposicion _servicio;
        private int _testigo;
        private string _empresa;
        private int? _traspaso;
        private List<int> _bultos = new List<int>();

        public VisorFotosBultosReposicion(IServicioFotosBultosReposicion servicio, Action<string> abrirEnlace = null)
        {
            _servicio = servicio;
            AbrirEnlace = abrirEnlace ?? AbrirEnElNavegador;
            VerCommand = new AsyncRelayCommand(VerAsync, () => HayFotos);
        }

        /// <summary>Abre el enlace (los tests lo cambian). Solo http y https.</summary>
        public Action<string> AbrirEnlace { get; set; }

        public IAsyncRelayCommand VerCommand { get; }

        private bool _hayFotos;
        public bool HayFotos
        {
            get => _hayFotos;
            private set
            {
                if (SetProperty(ref _hayFotos, value))
                {
                    VerCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _mensaje;
        public string Mensaje { get => _mensaje; private set => SetProperty(ref _mensaje, value); }

        /// <summary>Mira si algún bulto de la reposición tiene foto. Sin reposición, ninguno. Nunca lanza.</summary>
        public async Task ComprobarAsync(string empresa, int? traspaso)
        {
            int testigo = ++_testigo;
            _empresa = empresa;
            _traspaso = traspaso;
            _bultos = new List<int>();
            HayFotos = false;
            Mensaje = null;
            if (_servicio == null || !traspaso.HasValue || traspaso.Value <= 0)
            {
                return;
            }
            List<int> bultos = await _servicio.BultosConFoto(empresa, traspaso.Value);
            if (testigo != _testigo)
            {
                return;
            }
            _bultos = bultos ?? new List<int>();
            HayFotos = _bultos.Count > 0;
        }

        /// <summary>Comprobar a partir del número de documento en texto («81463»).</summary>
        public Task ComprobarAsync(string empresa, string documento)
            => ComprobarAsync(empresa, int.TryParse(documento?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : (int?)null);

        public async Task VerAsync()
        {
            if (!HayFotos || !_traspaso.HasValue)
            {
                return;
            }
            int traspaso = _traspaso.Value;
            List<int> abiertos = new List<int>();
            List<string> fallos = new List<string>();
            foreach (int bulto in _bultos)
            {
                try
                {
                    string url = await _servicio.EnlaceFoto(_empresa, traspaso, bulto);
                    if (!EsEnlaceWeb(url))
                    {
                        fallos.Add($"bulto {bulto}: el servidor no ha dado un enlace válido");
                        continue;
                    }
                    AbrirEnlace(url);
                    abiertos.Add(bulto);
                }
                catch (Exception ex)
                {
                    fallos.Add($"bulto {bulto}: {ex.Message}");
                }
            }
            Mensaje = TextoResultado(traspaso, abiertos, fallos);
        }

        /// <summary>«Abiertas las fotos de los bultos 1 y 3 de la reposición 81463.» y lo que no se ha podido.</summary>
        public static string TextoResultado(int traspaso, IReadOnlyList<int> abiertos, IReadOnlyList<string> fallos)
        {
            string texto = abiertos.Count switch
            {
                0 => $"No se ha podido abrir ninguna foto de la reposición {traspaso}.",
                1 => $"Abierta la foto del bulto {abiertos[0]} de la reposición {traspaso}.",
                _ => $"Abiertas las fotos de los bultos {string.Join(", ", abiertos.Take(abiertos.Count - 1))} y {abiertos.Last()} de la reposición {traspaso}."
            };
            if (fallos.Count > 0)
            {
                texto += " No se ha podido: " + string.Join("; ", fallos) + ".";
            }
            return texto;
        }

        internal static bool EsEnlaceWeb(string url)
            => Uri.TryCreate(url, UriKind.Absolute, out Uri uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

        private static void AbrirEnElNavegador(string url) => _ = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
