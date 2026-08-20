using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// Caso real 20/08/26: el usuario de Tienda Online que factura FBA (almacén AMZ) necesita
    /// pasar a ALG los días que cubre rutas y volver. El servidor decide qué parámetros puede
    /// editar cada usuario y con qué valores (cero hard-coding aquí); el usuario del cambio
    /// sale del JWT, nunca del cliente.
    /// </summary>
    public class ServicioParametrosEditables : IServicioParametrosEditables
    {
        private readonly IClienteApiFactory _clienteApiFactory;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public ServicioParametrosEditables(IClienteApiFactory clienteApiFactory, IServicioAutenticacion servicioAutenticacion)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
            _servicioAutenticacion = servicioAutenticacion ?? throw new ArgumentNullException(nameof(servicioAutenticacion));
        }

        public async Task<List<ParametroEditableModel>> LeerEditables()
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización contra NestoAPI.");
                }
                HttpResponseMessage response = await client.GetAsync(
                    $"ParametrosUsuario/Editables?empresa={Constantes.Empresas.EMPRESA_DEFECTO}");
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"No se pudieron leer los parámetros editables ({(int)response.StatusCode}): {body}");
                }
                return JsonConvert.DeserializeObject<List<ParametroEditableModel>>(body)
                    ?? new List<ParametroEditableModel>();
            }
        }

        public async Task<ParametroEditableModel> Cambiar(string clave, string valor)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización contra NestoAPI.");
                }
                var contenido = new StringContent(JsonConvert.SerializeObject(new
                {
                    Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                    Clave = clave,
                    Valor = valor
                }), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("ParametrosUsuario/Editables", contenido);
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    // El BadRequest del servidor trae el motivo legible (grupo, valor no admitido...)
                    throw new Exception(ExtraerMensaje(body));
                }
                return JsonConvert.DeserializeObject<ParametroEditableModel>(body);
            }
        }

        private static string ExtraerMensaje(string body)
        {
            try
            {
                var error = JsonConvert.DeserializeAnonymousType(body, new { Message = string.Empty });
                return string.IsNullOrWhiteSpace(error?.Message) ? body : error.Message;
            }
            catch
            {
                return body;
            }
        }
    }
}
