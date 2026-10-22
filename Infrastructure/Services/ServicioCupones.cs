using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    /// <summary>
    /// Nesto#518 (NestoAPI#613/#614): los cupones de descuento de los eventos. La API es la dueña de las reglas; Nesto
    /// consulta, emite (manuales, solo Dirección), anula y manda la línea con el código.
    /// </summary>
    public interface IServicioCupones
    {
        /// <summary>
        /// GET api/Clientes/{empresa}/{cliente}/Cupones?todos=…  Con <paramref name="todos"/> false, los que se pueden usar
        /// (Activo) y los que valdrán (Emitido); con true, también los usados, anulados y caducados. Es una ayuda: nunca
        /// lanza. Lista vacía si no tiene (o con una API anterior a #613, que da 404); null si no se ha podido leer.
        /// </summary>
        Task<List<CuponDTO>> LeerDelCliente(string empresa, string cliente, bool todos);

        /// <summary>GET api/Cupones/{codigo}. Null si no existe (404), si la API falla o si es anterior a #613: nunca lanza.</summary>
        Task<CuponDTO> LeerPorCodigo(string codigo);

        /// <summary>POST api/Cupones: cupón manual. Lanza con el motivo de la API (403 si no es de Dirección, 400 sin motivo…).</summary>
        Task<CuponDTO> CrearManual(CrearCuponManualDTO peticion);

        /// <summary>POST api/Cupones/{codigo}/Anular. Lanza con el motivo de la API (409 si ya se usó, 403…).</summary>
        Task<CuponDTO> Anular(string codigo, string motivo);

        /// <summary>
        /// NestoAPI#614 (pendiente en la API): POST api/Cupones/{codigo}/Usar para decir a NestoAPI que el cupón se ha gastado en
        /// la tienda y que el pedido ha entrado sin él (ya estaba usado o anulado en Nesto): la API avisa a Administración.
        /// Nunca lanza. Devuelve false si la API no lo tiene todavía (404) o falla.
        /// </summary>
        Task<bool> AvisarUsoEnTienda(string codigo, UsoCuponTiendaDTO uso);
    }

    public class ServicioCupones : IServicioCupones
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioCupones(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory;
        }

        public async Task<List<CuponDTO>> LeerDelCliente(string empresa, string cliente, bool todos)
        {
            if (string.IsNullOrWhiteSpace(cliente))
            {
                return new List<CuponDTO>();
            }
            try
            {
                string empresaConsulta = string.IsNullOrWhiteSpace(empresa) ? Constantes.Empresas.EMPRESA_DEFECTO : empresa.Trim();
                using (HttpClient client = _clienteApiFactory.Crear())
                {
                    HttpResponseMessage response = await client.GetAsync(
                        $"Clientes/{Uri.EscapeDataString(empresaConsulta)}/{Uri.EscapeDataString(cliente.Trim())}/Cupones?todos={todos.ToString().ToLowerInvariant()}")
                        .ConfigureAwait(false);
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return new List<CuponDTO>(); // API anterior a NestoAPI#613
                    }
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return (string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<List<CuponDTO>>(json)) ?? new List<CuponDTO>();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<CuponDTO> LeerPorCodigo(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
            {
                return null;
            }
            try
            {
                using (HttpClient client = _clienteApiFactory.Crear())
                {
                    HttpResponseMessage response = await client.GetAsync($"Cupones/{Uri.EscapeDataString(codigo.Trim())}").ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    CuponDTO cupon = string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<CuponDTO>(json);
                    return string.IsNullOrWhiteSpace(cupon?.Codigo) ? null : cupon;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<CuponDTO> CrearManual(CrearCuponManualDTO peticion)
        {
            if (peticion == null)
            {
                throw new ArgumentNullException(nameof(peticion));
            }
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.PostAsync("Cupones", Json(peticion)).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(await NovedadesService.MensajeDeError(response, "emitir el cupón").ConfigureAwait(false));
                }
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<CuponDTO>(json);
            }
        }

        public async Task<CuponDTO> Anular(string codigo, string motivo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
            {
                throw new ArgumentException("Falta el código del cupón.", nameof(codigo));
            }
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.PostAsync($"Cupones/{Uri.EscapeDataString(codigo.Trim())}/Anular",
                    Json(new AnularCuponDTO { Motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim() })).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(await NovedadesService.MensajeDeError(response, "anular el cupón").ConfigureAwait(false));
                }
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<CuponDTO>(json);
            }
        }

        public async Task<bool> AvisarUsoEnTienda(string codigo, UsoCuponTiendaDTO uso)
        {
            if (string.IsNullOrWhiteSpace(codigo) || uso == null)
            {
                return false;
            }
            try
            {
                using (HttpClient client = _clienteApiFactory.Crear())
                {
                    HttpResponseMessage response = await client.PostAsync($"Cupones/{Uri.EscapeDataString(codigo.Trim())}/Usar", Json(uso)).ConfigureAwait(false);
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static StringContent Json(object objeto) => new(JsonConvert.SerializeObject(objeto), Encoding.UTF8, "application/json");
    }
}
