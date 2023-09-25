using Nesto.Models.Nesto.Models;
using QRCoder;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
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



        public static async Task<List<FilaEtiquetasModel>> CargarDatos(List<string> productos, int etiquetaPrimera)
        {
            List<EtiquetasTiendaModel> lista;
            using (NestoEntities db = new NestoEntities())
            {
                // Crear una lista de parámetros para los productos
                var parametrosProductos = string.Join(",", productos.Select((_, index) => $"@producto{index}"));

                // Construir la consulta SQL con parámetros dinámicos
                string consulta = $"select CONVERT(INT, ROW_NUMBER() OVER (ORDER BY p.Número)) +{etiquetaPrimera - 1} AS NumeroEtiqueta," +
                    $"rtrim(p.Número) ProductoId, rtrim(Nombre) Nombre, isnull(Tamaño,0) Tamanno, rtrim(isnull(UnidadMedida,'')) UnidadMedida, PVP PrecioProfesional, rtrim(f.Descripción) Familia " +
                    $"from Productos p inner join Familias f " +
                    $"on p.Empresa = f.Empresa and p.Familia = f.Número " +
                    $"where p.Empresa = '1' and p.Número in ({parametrosProductos})";

                // Crear parámetros para los productos y asignar valores
                var parametros = productos.Select((producto, index) => new SqlParameter($"@producto{index}", producto)).ToArray();

                // Ejecutar la consulta con los parámetros
                lista = await db.Database.SqlQuery<EtiquetasTiendaModel>(consulta, parametros).ToListAsync();
            };

            if (etiquetaPrimera > 1)
            {
                for (int i = 0; i < etiquetaPrimera - 1; i++)
                {
                    EtiquetasTiendaModel nuevaEtiqueta = new EtiquetasTiendaModel {
                        NumeroEtiqueta = etiquetaPrimera - 1 - i
                    }; // Crea una nueva instancia en blanco
                    lista.Insert(0, nuevaEtiqueta); // Inserta la nueva instancia al principio de la lista
                }
            }

            List<FilaEtiquetasModel> listaFilas = new List<FilaEtiquetasModel>();
            int ultimaFila = 0;
            FilaEtiquetasModel fila = new FilaEtiquetasModel();
            foreach (var etiqueta in lista.OrderBy(l => l.Fila))
            {
                if (etiqueta.Fila != ultimaFila && ultimaFila != 0)
                {
                    listaFilas.Add(fila);
                    fila = new FilaEtiquetasModel();
                }

                string urlProducto = await CalcularUrlProducto(etiqueta.ProductoId);
                etiqueta.UrlProducto = urlProducto;
                // Generar el código QR
                QRCodeGenerator qrGenerator = new QRCodeGenerator();
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(urlProducto, QRCodeGenerator.ECCLevel.Q);
                QRCode qrCode = new QRCode(qrCodeData);
                Bitmap qrCodeImage = qrCode.GetGraphic(20);

                // Convertir la imagen a base64
                string base64Image = ImageToBase64(qrCodeImage);


                string precioEnString = PasarDecimalAString(etiqueta.PrecioProfesional);
                if (etiqueta.Columna == 1)
                {
                    fila.NombreConTamanno1 = etiqueta.NombreConTamanno;
                    fila.Familia1 = etiqueta.Familia;
                    fila.PrecioPublico1 = CalcularPrecioPublico(etiqueta.PrecioProfesional);
                    fila.ReferenciaProfesional1 = $"{etiqueta.ProductoId}{precioEnString}";
                    fila.Url1 = etiqueta.UrlProducto;
                    fila.CodigoQR1 = base64Image;
                }else if (etiqueta.Columna == 2)
                {
                    fila.NombreConTamanno2 = etiqueta.NombreConTamanno;
                    fila.Familia2 = etiqueta.Familia;
                    fila.PrecioPublico2 = CalcularPrecioPublico(etiqueta.PrecioProfesional);
                    fila.ReferenciaProfesional2 = $"{etiqueta.ProductoId}{precioEnString}";
                    fila.Url2 = etiqueta.UrlProducto;
                    fila.CodigoQR2 = base64Image;
                }
                else
                {
                    fila.NombreConTamanno3 = etiqueta.NombreConTamanno;
                    fila.Familia3 = etiqueta.Familia;
                    fila.PrecioPublico3 = CalcularPrecioPublico(etiqueta.PrecioProfesional);
                    fila.ReferenciaProfesional3 = $"{etiqueta.ProductoId}{precioEnString}";
                    fila.Url3 = etiqueta.UrlProducto;
                    fila.CodigoQR3 = base64Image;
                }
                ultimaFila = etiqueta.Fila;
            }
            listaFilas.Add(fila);

            return listaFilas;
        }

        private static decimal CalcularPrecioPublico(decimal precioProfesional)
        {
            return Math.Round(precioProfesional * 2 * .65M * 1.21M, 2, MidpointRounding.AwayFromZero);
            //return precioProfesional / .7M * 1.21M;
        }

        private static string PasarDecimalAString(decimal numeroDecimal)
        {
            // Convertir a string con formato personalizado y redondeo a dos decimales
            string cadena = Math.Round(numeroDecimal, 2).ToString("0.00");

            // Eliminar puntos y comas
            cadena = cadena.Replace(".", "").Replace(",", "");

            // Asegurarse de que la cadena tenga al menos 7 caracteres
            while (cadena.Length < 7)
            {
                cadena = "0" + cadena;
            }

            // Obtener los últimos dos caracteres (parte decimal)
            string parteDecimal = cadena.Substring(cadena.Length - 2);

            // Obtener los primeros cinco caracteres (parte entera)
            string parteEntera = cadena.Substring(0, 5);

            // La cadena final que buscas
            string resultadoFinal = parteEntera + parteDecimal;

            return resultadoFinal; 
        }

        public static string ImageToBase64(Bitmap image)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                image.Save(memoryStream, ImageFormat.Png); // Puedes cambiar el formato si lo necesitas
                byte[] byteImage = memoryStream.ToArray();
                return Convert.ToBase64String(byteImage);
            }
        }

        public static async Task<string> CalcularUrlProducto(string producto)
        {
            using (var client = new HttpClient())
            {
                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                client.BaseAddress = new Uri("http://www.productosdeesteticaypeluqueriaprofesional.com/enlacePorReferencia.php");
                client.DefaultRequestHeaders.Accept.Clear();
                try
                {
                    string parametros = "?producto=" + producto;
                    HttpResponseMessage response = await client.GetAsync(parametros).ConfigureAwait(false);


                    string rutaEnlace = string.Empty;
                    if (response.IsSuccessStatusCode)
                    {
                        rutaEnlace = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        rutaEnlace += "?utm_source=nuevavision&utm_campaign=tienda_alcobendas";
                    }

                    return rutaEnlace;
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
        public int Columna => (NumeroEtiqueta  - 1) % NUMERO_COLUMNAS + 1;
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
