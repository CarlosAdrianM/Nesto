using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Services
{
    public class ClientesService : IClientesService
    {
        private readonly IConfiguracion _configuracion;

        public ClientesService(IConfiguracion configuracion)
        {
            _configuracion = configuracion;
        }

        public async Task<List<ExtractoClienteDTO>> LeerDeudas(string cliente)
        {            
            List<ExtractoClienteDTO> deudas;
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string urlConsulta = $"ExtractosCliente?cliente={cliente}";

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        deudas = JsonConvert.DeserializeObject<List<ExtractoClienteDTO>>(resultado);
                    }
                    else
                    {
                        throw new Exception("Las deudas no se han podido cargar correctamente");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("Las deudas no se han podido cargar correctamente", ex);
                }
            }

            return deudas;
        }
    }
}
