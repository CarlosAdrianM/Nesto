using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    /// <summary>Nesto#507: cliente de api/Almacen para los bultos del packing de Ariadna.</summary>
    public class ServicioBultosAriadna : IServicioBultosAriadna
    {
        private readonly IClienteApiFactory _clienteApiFactory;
        private readonly Func<HttpClient> _crearClienteDescargas;

        public ServicioBultosAriadna(IClienteApiFactory clienteApiFactory) : this(clienteApiFactory, null)
        {
        }

        /// <param name="crearClienteDescargas">Para los tests: el cliente con el que se baja la imagen del enlace temporal.</param>
        internal ServicioBultosAriadna(IClienteApiFactory clienteApiFactory, Func<HttpClient> crearClienteDescargas)
        {
            _clienteApiFactory = clienteApiFactory;
            _crearClienteDescargas = crearClienteDescargas ?? (() => new HttpClient());
        }

        public async Task<List<BultoAriadna>> LeerBultosDelPedido(string empresa, int pedido)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                string url = $"Almacen/Pedidos/{pedido}/Bultos?empresa={Uri.EscapeDataString(empresa?.Trim() ?? string.Empty)}";
                HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return new List<BultoAriadna>();
                }
                _ = response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<List<BultoAriadna>>(json) ?? new List<BultoAriadna>();
            }
        }

        public async Task<string> EnlaceFoto(int idBulto)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync($"Almacen/Bultos/{idBulto}/Foto").ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                _ = response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<EnlaceFotoBulto>(json)?.Url;
            }
        }

        public async Task<byte[]> DescargarFoto(int idBulto)
        {
            string enlace = await EnlaceFoto(idBulto).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(enlace))
            {
                return null;
            }
            // El enlace temporal (SAS de Azure) ya lleva su permiso: se baja con un cliente sin el token de la API,
            // que el almacén de fotos rechazaría.
            using (HttpClient client = _crearClienteDescargas())
            {
                HttpResponseMessage response = await client.GetAsync(enlace).ConfigureAwait(false);
                _ = response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }

        private sealed class EnlaceFotoBulto
        {
            public string Url { get; set; }
        }
    }

    /// <summary>
    /// Nesto#522: el enlace completo de la foto de un bulto para el cliente o la agencia (no pide usuario).
    /// </summary>
    public static class EnlacePublicoFotoBulto
    {
        /// <summary>
        /// El servidor de la API que usa Nesto («http://api.nuevavision.es/api/») sin su «api/» final más la ruta pública
        /// que manda la API («api/Almacen/Fotos/{token}»). Null si falta alguna de las dos o el servidor no es una
        /// dirección válida. Una ruta que ya sea una dirección completa se devuelve tal cual.
        /// </summary>
        public static string Componer(string servidorApi, string rutaPublica)
        {
            if (string.IsNullOrWhiteSpace(rutaPublica))
            {
                return null;
            }
            string ruta = rutaPublica.Trim();
            if (Uri.TryCreate(ruta, UriKind.Absolute, out Uri absoluta) && (absoluta.Scheme == Uri.UriSchemeHttp || absoluta.Scheme == Uri.UriSchemeHttps))
            {
                return absoluta.AbsoluteUri;
            }
            if (string.IsNullOrWhiteSpace(servidorApi) || !Uri.TryCreate(servidorApi.Trim(), UriKind.Absolute, out Uri servidor))
            {
                return null;
            }
            string raiz = servidor.GetLeftPart(UriPartial.Path).TrimEnd('/');
            if (raiz.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
            {
                raiz = raiz.Substring(0, raiz.Length - "/api".Length);
            }
            return $"{raiz}/{ruta.TrimStart('/')}";
        }
    }

    /// <summary>
    /// Nesto#507: qué número de bultos proponer en Agencias a partir del packing de Ariadna. Sin bultos de Ariadna
    /// todo sigue como hoy. Si el usuario ya ha tecleado otro número, no se pisa: se le avisa de la diferencia.
    /// </summary>
    public static class PropuestaBultosAriadna
    {
        /// <summary>El valor con el que nace el envío al elegir el pedido.</summary>
        public const int BULTOS_POR_DEFECTO = 1;

        public static int NumeroDeBultos(IEnumerable<BultoAriadna> bultos)
            => (bultos ?? Enumerable.Empty<BultoAriadna>()).Select(b => b.Id).Distinct().Count();

        /// <summary>Los bultos que hay que poner y, si no se cambian, el aviso para el usuario (null si no hay nada que decir).</summary>
        public static (int Bultos, string Aviso) Proponer(int bultosActuales, IEnumerable<BultoAriadna> bultos)
        {
            int deAriadna = NumeroDeBultos(bultos);
            if (deAriadna == 0 || deAriadna == bultosActuales)
            {
                return (bultosActuales, null);
            }
            if (bultosActuales == BULTOS_POR_DEFECTO)
            {
                return (deAriadna, null);
            }
            return (bultosActuales, $"En el packing de Ariadna el pedido tiene {deAriadna} {(deAriadna == 1 ? "bulto" : "bultos")}, " +
                $"pero el envío lleva {bultosActuales}. Revisa cuál es el bueno.");
        }

        /// <summary>El resumen para la pantalla: «Ariadna: 2 bultos (2 con foto)». Vacío sin bultos de Ariadna.</summary>
        public static string Texto(IEnumerable<BultoAriadna> bultos)
        {
            List<BultoAriadna> distintos = (bultos ?? Enumerable.Empty<BultoAriadna>()).GroupBy(b => b.Id).Select(g => g.First()).ToList();
            if (distintos.Count == 0)
            {
                return string.Empty;
            }
            int conFoto = distintos.Count(b => b.TieneFoto);
            return $"Ariadna: {distintos.Count} {(distintos.Count == 1 ? "bulto" : "bultos")} ({conFoto} con foto)";
        }
    }
}
