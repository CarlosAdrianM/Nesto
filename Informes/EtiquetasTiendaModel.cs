using QRCoder;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Informes
{
    public class FilaEtiquetasModel
    {
        public string NombreConTamanno1 { get; set; }
        public string NombreConTamanno2 { get; set; }
        public string NombreConTamanno3 { get; set; }
        public string Familia1 { get; set; }
        public string Familia2 { get; set; }
        public string Familia3 { get; set; }
        public decimal PrecioPublico1 { get; set; }
        public decimal PrecioPublico2 { get; set; }
        public decimal PrecioPublico3 { get; set; }
        public string ReferenciaProfesional1 { get; set; }
        public string ReferenciaProfesional2 { get; set; }
        public string ReferenciaProfesional3 { get; set; }
        public string Url1 { get; set; }
        public string Url2 { get; set; }
        public string Url3 { get; set; }
        public string CodigoQR1 { get; set; }
        public string CodigoQR2 { get; set; }
        public string CodigoQR3 { get; set; }

        /// <summary>
        /// Recibe la lista de etiquetas ya cargada (vía API) y la transforma en filas de
        /// 3 columnas resolviendo la URL pública de cada producto y generando el QR.
        /// La carga de datos vive en InformesService; aquí sólo hay lógica de formato.
        /// </summary>
        public static async Task<List<FilaEtiquetasModel>> ComponerAsync(
            List<EtiquetasTiendaModel> etiquetas, int etiquetaPrimera)
        {
            var lista = etiquetas != null
                ? new List<EtiquetasTiendaModel>(etiquetas)
                : new List<EtiquetasTiendaModel>();

            int offset = etiquetaPrimera > 0 ? etiquetaPrimera - 1 : 0;
            for (int i = 0; i < lista.Count; i++)
            {
                lista[i].NumeroEtiqueta = i + 1 + offset;
            }

            if (offset > 0)
            {
                for (int i = 0; i < offset; i++)
                {
                    lista.Insert(0, new EtiquetasTiendaModel { NumeroEtiqueta = offset - i });
                }
            }

            var listaFilas = new List<FilaEtiquetasModel>();
            int ultimaFila = 0;
            var fila = new FilaEtiquetasModel();
            foreach (var etiqueta in lista.OrderBy(l => l.Fila))
            {
                if (etiqueta.Fila != ultimaFila && ultimaFila != 0)
                {
                    listaFilas.Add(fila);
                    fila = new FilaEtiquetasModel();
                }

                if (!string.IsNullOrEmpty(etiqueta.ProductoId))
                {
                    etiqueta.UrlProducto = await CalcularUrlProducto(etiqueta.ProductoId);
                    string codigoQr = GenerarQrBase64(etiqueta.UrlProducto);
                    string precioFormato = PasarDecimalAString(etiqueta.PrecioProfesional);
                    decimal precioPublico = CalcularPrecioPublico(etiqueta.PrecioProfesional);
                    string referencia = $"{etiqueta.ProductoId}{precioFormato}";

                    if (etiqueta.Columna == 1)
                    {
                        fila.NombreConTamanno1 = etiqueta.NombreConTamanno;
                        fila.Familia1 = etiqueta.Familia;
                        fila.PrecioPublico1 = precioPublico;
                        fila.ReferenciaProfesional1 = referencia;
                        fila.Url1 = etiqueta.UrlProducto;
                        fila.CodigoQR1 = codigoQr;
                    }
                    else if (etiqueta.Columna == 2)
                    {
                        fila.NombreConTamanno2 = etiqueta.NombreConTamanno;
                        fila.Familia2 = etiqueta.Familia;
                        fila.PrecioPublico2 = precioPublico;
                        fila.ReferenciaProfesional2 = referencia;
                        fila.Url2 = etiqueta.UrlProducto;
                        fila.CodigoQR2 = codigoQr;
                    }
                    else
                    {
                        fila.NombreConTamanno3 = etiqueta.NombreConTamanno;
                        fila.Familia3 = etiqueta.Familia;
                        fila.PrecioPublico3 = precioPublico;
                        fila.ReferenciaProfesional3 = referencia;
                        fila.Url3 = etiqueta.UrlProducto;
                        fila.CodigoQR3 = codigoQr;
                    }
                }
                ultimaFila = etiqueta.Fila;
            }
            listaFilas.Add(fila);

            return listaFilas;
        }

        private static decimal CalcularPrecioPublico(decimal precioProfesional)
        {
            return Math.Round(precioProfesional * 2 * .65M * 1.21M, 2, MidpointRounding.AwayFromZero);
        }

        private static string PasarDecimalAString(decimal numeroDecimal)
        {
            string cadena = Math.Round(numeroDecimal, 2).ToString("0.00");
            cadena = cadena.Replace(".", "").Replace(",", "");
            while (cadena.Length < 7)
            {
                cadena = "0" + cadena;
            }
            string parteDecimal = cadena.Substring(cadena.Length - 2);
            string parteEntera = cadena.Substring(0, 5);
            return parteEntera + parteDecimal;
        }

        private static string GenerarQrBase64(string contenido)
        {
            if (string.IsNullOrEmpty(contenido)) return null;
            var qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(contenido, QRCodeGenerator.ECCLevel.Q);
            using (var qrCode = new QRCode(qrCodeData))
            using (Bitmap bitmap = qrCode.GetGraphic(20))
            using (var memoryStream = new MemoryStream())
            {
                bitmap.Save(memoryStream, ImageFormat.Png);
                return Convert.ToBase64String(memoryStream.ToArray());
            }
        }

        private static async Task<string> CalcularUrlProducto(string producto)
        {
            // Chapuza heredada: PHP custom de la tienda online que resuelve la URL pública
            // desde la referencia. Dejar por ahora; sustituir por API nativa de Prestashop
            // cuando se consolide el servicio Prestashop.
            using (var client = new HttpClient())
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                client.BaseAddress = new Uri("http://www.productosdeesteticaypeluqueriaprofesional.com/enlacePorReferencia.php");
                client.DefaultRequestHeaders.Accept.Clear();
                try
                {
                    HttpResponseMessage response = await client.GetAsync("?producto=" + producto).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode) return string.Empty;
                    string rutaEnlace = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return rutaEnlace + "?utm_source=nuevavision&utm_campaign=tienda_alcobendas";
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }
    }

    public class EtiquetasTiendaModel
    {
        private const int NUMERO_COLUMNAS = 3;
        public int NumeroEtiqueta { get; set; }
        public int Fila => (NumeroEtiqueta - 1) / NUMERO_COLUMNAS + 1;
        public int Columna => (NumeroEtiqueta - 1) % NUMERO_COLUMNAS + 1;
        public string ProductoId { get; set; }
        public string Nombre { get; set; }
        public short Tamanno { get; set; }
        public string UnidadMedida { get; set; }
        public decimal PrecioProfesional { get; set; }
        public string Familia { get; set; }
        public string NombreConTamanno => Tamanno == 0 ? Nombre : $"{Nombre} {Tamanno} {UnidadMedida}";
        public string UrlProducto { get; set; }
    }
}
