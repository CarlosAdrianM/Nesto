using Nesto.Modulos.Cajas.Models;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Nesto.Infrastructure.Contracts;
using System.Globalization;
using System.Collections.ObjectModel;
using Nesto.Models;

namespace Nesto.Modulos.Cajas
{
    public class BancosService : IBancosService
    {
        private readonly IConfiguracion _configuracion;

        public BancosService(IConfiguracion configuracion)
        {
            _configuracion = configuracion;
        }

        public async Task<ContenidoCuaderno43> CargarFicheroCuaderno43(string contenidoFichero)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);

                try
                {
                    string urlConsulta = "Bancos/CargarFichero";

                    // Crear el objeto anónimo con los datos del contenido y el usuario
                    var contenidoUsuario = new
                    {
                        Contenido = contenidoFichero,
                        Usuario = _configuracion.usuario
                    };

                    // Serializar el objeto anónimo a JSON
                    var jsonContent = JsonConvert.SerializeObject(contenidoUsuario);

                    // Crear el contenido de la solicitud
                    HttpContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync(urlConsulta, content);

                    if (response.IsSuccessStatusCode)
                    {
                        string contenido = await response.Content.ReadAsStringAsync();
                        var resultado = JsonConvert.DeserializeObject<ContenidoCuaderno43>(contenido);
                        return resultado;
                    }
                    else
                    {
                        string textoError = await response.Content.ReadAsStringAsync();
                        JObject requestException = JsonConvert.DeserializeObject<JObject>(textoError);

                        string errorMostrar = $"No se ha podido contabilizar\n";
                        if (requestException != null && requestException["Message"] != null)
                        {
                            errorMostrar += requestException["Message"] + "\n";
                        }
                        if (requestException != null && requestException["message"] != null)
                        {
                            errorMostrar += requestException["message"] + "\n";
                        }
                        if (requestException != null && requestException["exceptionMessage"] != null)
                        {
                            errorMostrar += requestException["exceptionMessage"] + "\n";
                        }
                        if (requestException != null && requestException["ModelState"] != null)
                        {
                            var firstError = requestException["ModelState"];
                            var nodoError = firstError.LastOrDefault();
                            errorMostrar += nodoError.FirstOrDefault()[0];
                        }
                        var innerException = requestException != null ? requestException["InnerException"] : null;
                        while (innerException != null)
                        {
                            errorMostrar += "\n" + innerException["ExceptionMessage"];
                            innerException = innerException["InnerException"];
                        }
                        throw new Exception(errorMostrar);
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        public async Task<List<MovimientoTPV>> CargarFicheroTarjetas(string contenidoFichero)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);

                try
                {
                    string urlConsulta = "Bancos/CargarFicheroTarjetas";

                    // Crear el objeto anónimo con los datos del contenido y el usuario
                    var contenidoUsuario = new
                    {
                        Contenido = contenidoFichero,
                        Usuario = _configuracion.usuario
                    };

                    // Serializar el objeto anónimo a JSON
                    var jsonContent = JsonConvert.SerializeObject(contenidoUsuario);

                    // Crear el contenido de la solicitud
                    HttpContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync(urlConsulta, content);

                    if (response.IsSuccessStatusCode)
                    {
                        string contenido = await response.Content.ReadAsStringAsync();
                        var resultado = JsonConvert.DeserializeObject<List<MovimientoTPV>>(contenido);
                        return resultado;
                    }
                    else
                    {
                        string textoError = await response.Content.ReadAsStringAsync();
                        JObject requestException = JsonConvert.DeserializeObject<JObject>(textoError);

                        string errorMostrar = $"No se ha podido cargar los movimientos del TPV\n";
                        if (requestException != null && requestException["Message"] != null)
                        {
                            errorMostrar += requestException["Message"] + "\n";
                        }
                        if (requestException != null && requestException["message"] != null)
                        {
                            errorMostrar += requestException["message"] + "\n";
                        }
                        if (requestException != null && requestException["exceptionMessage"] != null)
                        {
                            errorMostrar += requestException["exceptionMessage"] + "\n";
                        }
                        if (requestException != null && requestException["ModelState"] != null)
                        {
                            var firstError = requestException["ModelState"];
                            var nodoError = firstError.LastOrDefault();
                            errorMostrar += nodoError.FirstOrDefault()[0];
                        }
                        var innerException = requestException != null ? requestException["InnerException"] : null;
                        while (innerException != null)
                        {
                            errorMostrar += "\n" + innerException["ExceptionMessage"];
                            innerException = innerException["InnerException"];
                        }
                        throw new Exception(errorMostrar);
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        public async Task<int> CrearPunteo(int? apunteBancoId, int? apunteContabilidadId, decimal importePunteo, string simboloPunteo, int? grupoPunteo = null)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);

                try
                {
                    string urlConsulta = "Bancos/PuntearApuntes";

                    // Crear el objeto anónimo con los datos del contenido y el usuario
                    var datosPunteo = new
                    {
                        ApunteBancoId = apunteBancoId,
                        ApunteContabilidadId = apunteContabilidadId,
                        ImportePunteo = importePunteo,
                        SimboloPunteo = simboloPunteo,
                        GrupoPunteo = grupoPunteo,
                        Usuario = _configuracion.usuario
                    };

                    // Serializar el objeto anónimo a JSON
                    var jsonContent = JsonConvert.SerializeObject(datosPunteo);

                    // Crear el contenido de la solicitud
                    HttpContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync(urlConsulta, content);

                    if (response.IsSuccessStatusCode)
                    {
                        string contenido = await response.Content.ReadAsStringAsync();
                        var punteoId = JsonConvert.DeserializeObject<int>(contenido);
                        return punteoId;
                    }
                    else
                    {
                        string textoError = await response.Content.ReadAsStringAsync();
                        JObject requestException = JsonConvert.DeserializeObject<JObject>(textoError);

                        string errorMostrar = $"No se ha podido crear el punteo de la conciliación\n";
                        if (requestException != null && requestException["Message"] != null)
                        {
                            errorMostrar += requestException["Message"] + "\n";
                        }
                        if (requestException != null && requestException["message"] != null)
                        {
                            errorMostrar += requestException["message"] + "\n";
                        }
                        if (requestException != null && requestException["exceptionMessage"] != null)
                        {
                            errorMostrar += requestException["exceptionMessage"] + "\n";
                        }
                        if (requestException != null && requestException["ModelState"] != null)
                        {
                            var firstError = requestException["ModelState"];
                            var nodoError = firstError.LastOrDefault();
                            errorMostrar += nodoError.FirstOrDefault()[0];
                        }
                        var innerException = requestException != null ? requestException["InnerException"] : null;
                        while (innerException != null)
                        {
                            errorMostrar += "\n" + innerException["ExceptionMessage"];
                            innerException = innerException["InnerException"];
                        }
                        throw new Exception(errorMostrar);
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        public async Task<List<ApunteBancarioDTO>> LeerApuntesBanco(string empresa, string codigo, DateTime fechaDesde, DateTime fechaHasta)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string fechaDesdeFormateada = fechaDesde.ToString("yyyy-MM-dd");
                    string fechaHastaFormateada = fechaHasta.ToString("yyyy-MM-dd");
                    string urlConsulta = $"Bancos?empresa={empresa}&codigoBanco={codigo}&fechaDesde={fechaDesdeFormateada}&fechaHasta={fechaHastaFormateada}";

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        var apuntes = JsonConvert.DeserializeObject<List<ApunteBancarioDTO>>(resultado);
                        return apuntes;
                    }
                    else
                    {
                        throw new Exception("No se han podido cargar los apuntes del banco");
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        public async Task<BancoDTO> LeerBanco(string entidad, string oficina, string numeroCuenta)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string urlConsulta = $"Bancos?entidad={entidad}&oficina={oficina}&cuenta={numeroCuenta}";

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        var banco = JsonConvert.DeserializeObject<BancoDTO>(resultado);
                        banco.Nombre = banco.Nombre.Trim();
                        return banco;
                    }
                    else
                    {
                        throw new Exception("No se ha podido cargar el banco");
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        public async Task<BancoDTO> LeerBanco(string empresa, string codigo)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string urlConsulta = $"Bancos?empresa={empresa}&codigoBanco={codigo}";

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        var banco = JsonConvert.DeserializeObject<BancoDTO>(resultado);
                        return banco;
                    }
                    else
                    {
                        throw new Exception("No se ha podido cargar el banco");
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        public async Task<List<ExtractoProveedorDTO>> LeerExtractoProveedorAsiento(string empresa, int asiento)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string urlConsulta = $"ExtractoProveedores?empresa={empresa}&asiento={asiento}";

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        var movimientos = JsonConvert.DeserializeObject<List<ExtractoProveedorDTO>>(resultado);
                        return movimientos;
                    }
                    else
                    {
                        throw new Exception("No se han podido cargar los extractos del proveedor para el asiento");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("No se han podido cargar los extractos del proveedor para el asiento", ex);
                }
            }
        }

        public async Task<List<MovimientoTPV>> LeerMovimientosTPV(DateTime fechaCaptura, string tipoDatafono)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string fechaFormateada = fechaCaptura.ToString("yyyy-MM-dd");
                    string urlConsulta = $"Bancos/LeerMovimientosTPV?fechaCaptura={fechaFormateada}&tipoDatafono={tipoDatafono}";

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        var movimientos = JsonConvert.DeserializeObject<List<MovimientoTPV>>(resultado);
                        return movimientos;
                    }
                    else
                    {
                        throw new Exception("No se han podido cargar los movimientos de la tarjeta");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("No se han podido cargar los movimientos relacioados de la tarjeta", ex);
                }
            }
        }

        public async Task<ObservableCollection<PrepagoDTO>> LeerPrepagosPendientes(decimal importe)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    // Para mostrar punto en vez de coma decimal
                    CultureInfo culture = new CultureInfo("en-US");                    
                    string importeFormateado = importe.ToString(culture);
                    string urlConsulta = $"Bancos/LeerPrepagoPendientePorImporte?importe={importeFormateado}";

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        var movimientos = JsonConvert.DeserializeObject<List<PrepagoDTO>>(resultado);
                        return new ObservableCollection<PrepagoDTO>(movimientos);
                    }
                    else
                    {
                        throw new Exception("No se han podido cargar los prepagos pendientes");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("No se han podido cargar los prepagos pendientes", ex);
                }
            }
        }

        public async Task<string> LeerProveedorPorNombre(string nombreProveedor)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string urlConsulta = $"Bancos/LeerProveedorPorNombre?nombreProveedor={nombreProveedor}";

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        var proveedor = JsonConvert.DeserializeObject<string>(resultado);
                        return proveedor;
                    }
                    else
                    {
                        throw new Exception("No se ha podido comprobar si existe un proveedor con ese nombre");
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        public async Task<int> NumeroRecibosRemesa(string remesa)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {                    
                    string urlConsulta = $"Bancos/NumeroRecibosRemesa?remesa={remesa}";

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        var saldo = JsonConvert.DeserializeObject<int>(resultado);
                        return saldo;
                    }
                    else
                    {
                        throw new Exception("No se ha podido calcular el número de recibos de la remesa");
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        public async Task<ExtractoProveedorDTO> PagoPendienteUnico(string proveedor, decimal importe)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    importe = -importe; // el pago en el banco es negativo
                    // Establecer la cultura en-US para asegurar que el separador decimal sea un punto
                    CultureInfo culture = new CultureInfo("en-US");
                    // Formatear la cantidad utilizando la cultura específica
                    string importeFormateado = importe.ToString(culture);

                    string urlConsulta = $"Bancos/PagoPendienteUnico?proveedor={proveedor}&importe={importeFormateado}";

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        var pagoPendiente = JsonConvert.DeserializeObject<ExtractoProveedorDTO>(resultado);
                        return pagoPendiente;
                    }
                    else
                    {
                        throw new Exception("No se ha podido comprobar si existe un pago pendiente a ese proveedor de ese importe");
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        public async Task<decimal> SaldoBancoFinal(string entidad, string oficina, string cuenta, DateTime fecha)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string fechaFormateada = fecha.ToString("yyyy-MM-dd");
                    string urlConsulta = $"Bancos/SaldoFinal?entidad={entidad}&oficina={oficina}&cuenta={cuenta}&fecha={fechaFormateada}";

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        var saldo = JsonConvert.DeserializeObject<decimal>(resultado);
                        return saldo;
                    }
                    else
                    {
                        throw new Exception("No se ha podido calcular el saldo final del banco");
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        public async Task<decimal> SaldoBancoInicial(string entidad, string oficina, string cuenta, DateTime fecha)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string fechaFormateada = fecha.ToString("yyyy-MM-dd");
                    string urlConsulta = $"Bancos/SaldoInicial?entidad={entidad}&oficina={oficina}&cuenta={cuenta}&fecha={fechaFormateada}";

                    response = await client.GetAsync(urlConsulta).ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        var saldo = JsonConvert.DeserializeObject<decimal>(resultado);
                        return saldo;
                    }
                    else
                    {
                        throw new Exception("No se ha podido calcular el saldo inicial del banco");
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }
    }
}
