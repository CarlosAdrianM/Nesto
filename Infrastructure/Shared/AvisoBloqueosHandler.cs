using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// Aviso vertical de bloqueos (13/11/26): cuando un error de la API es por bloqueo o interbloqueo de la base de datos, la
    /// respuesta trae la cabecera <c>X-Nesto-Bloqueo</c> con el diagnóstico («Puede estar bloqueado por: …») pasado por
    /// <c>Uri.EscapeDataString</c>. Este manejador la mira en CADA respuesta y, si viene, publica el texto en
    /// <see cref="CanalAvisosBloqueo"/> para que la ventana principal lo enseñe, sea cual sea la pantalla y aunque su servicio
    /// no lea el mensaje del error (caso real del 13/11/26).
    /// <para>
    /// Va el MÁS EXTERNO de la cadena de <see cref="ClienteApiFactory"/>, por fuera de los reintentos: ve una sola respuesta por
    /// llamada (la definitiva), así que un GET reintentado tres veces da un aviso y no tres, y un GET que acaba saliendo bien al
    /// reintentar no da ninguno.
    /// </para>
    /// <para>Nunca cambia la respuesta ni lanza por su cuenta: cualquier fallo al leer o publicar se traga.</para>
    /// </summary>
    public class AvisoBloqueosHandler : DelegatingHandler
    {
        public const string CABECERA = "X-Nesto-Bloqueo";

        /// <summary>Tope de lo que se enseña: el diagnóstico de la API son dos frases.</summary>
        internal const int LONGITUD_MAXIMA = 1000;

        private readonly CanalAvisosBloqueo _canal;

        public AvisoBloqueosHandler() : this(null)
        {
        }

        public AvisoBloqueosHandler(CanalAvisosBloqueo canal)
        {
            _canal = canal ?? CanalAvisosBloqueo.Comun;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            try
            {
                string diagnostico = LeerDiagnostico(response);
                if (diagnostico != null)
                {
                    _ = _canal.Publicar(diagnostico);
                }
            }
            catch (Exception)
            {
                // El aviso es una ayuda: nunca estropea la llamada
            }
            return response;
        }

        /// <summary>El diagnóstico de la cabecera, decodificado, o null si no viene, viene vacía o no se puede leer.</summary>
        internal static string LeerDiagnostico(HttpResponseMessage response)
        {
            try
            {
                if (response == null || !response.Headers.TryGetValues(CABECERA, out IEnumerable<string> valores) || valores == null)
                {
                    return null;
                }
                foreach (string valor in valores)
                {
                    string texto = Decodificar(valor);
                    if (texto != null)
                    {
                        return texto;
                    }
                }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Deshace el <c>Uri.EscapeDataString</c> de la API. Un valor mal codificado no se tira: se enseña lo que se pueda leer
        /// (lo que no se deja decodificar se queda como viene). Sin saltos ni caracteres de control, y con tope de longitud.
        /// </summary>
        internal static string Decodificar(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                return null;
            }
            string texto;
            try
            {
                texto = Uri.UnescapeDataString(valor);
            }
            catch (Exception)
            {
                texto = valor;
            }

            var limpio = new StringBuilder(texto.Length);
            foreach (char c in texto)
            {
                _ = limpio.Append(char.IsControl(c) ? ' ' : c);
            }
            texto = limpio.ToString().Trim();
            if (texto.Length == 0)
            {
                return null;
            }
            return texto.Length > LONGITUD_MAXIMA ? texto.Substring(0, LONGITUD_MAXIMA).TrimEnd() + "…" : texto;
        }
    }
}
