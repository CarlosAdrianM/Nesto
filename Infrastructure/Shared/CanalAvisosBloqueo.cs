using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>El diagnóstico de un bloqueo de la base de datos, tal cual lo da la API (ya decodificado).</summary>
    public sealed class AvisoBloqueoEventArgs : EventArgs
    {
        public AvisoBloqueoEventArgs(string diagnostico)
        {
            Diagnostico = diagnostico;
        }

        public string Diagnostico { get; }
    }

    /// <summary>
    /// Aviso vertical de bloqueos (13/11/26): el canal por el que <see cref="AvisoBloqueosHandler"/> cuenta a la aplicación que la
    /// API ha dicho que la base de datos está bloqueada (cabecera X-Nesto-Bloqueo) y quién la bloquea. Lo escucha la ventana
    /// principal (<see cref="AvisoBloqueoViewModel"/>), que lo enseña en una banda para todas las pantallas.
    /// <para>
    /// Es un objeto corriente con un evento y hay UNO común para toda la aplicación (<see cref="Comun"/>): las factorías de
    /// clientes de la API no salen todas del contenedor (varios ViewModel hacen <c>New ClienteApiFactory(...)</c>), así que un
    /// servicio inyectado no llegaría a todas. Sin tipos de vista: el evento llega en el hilo de quien publica (uno de fondo).
    /// </para>
    /// <para>
    /// Antirrebote: el mismo texto no se vuelve a publicar hasta pasado <see cref="ANTIRREBOTE"/> desde la última vez que se
    /// publicó (durante un bloqueo, una pantalla puede fallar diez veces seguidas).
    /// </para>
    /// </summary>
    public sealed class CanalAvisosBloqueo
    {
        public static readonly TimeSpan ANTIRREBOTE = TimeSpan.FromSeconds(60);

        /// <summary>El canal de la aplicación: el que usan por defecto el manejador y la ventana principal.</summary>
        public static CanalAvisosBloqueo Comun { get; } = new CanalAvisosBloqueo();

        private readonly object _cerrojo = new object();
        private readonly Dictionary<string, DateTime> _publicados = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private readonly Func<DateTime> _ahoraUtc;
        private readonly TimeSpan _antirrebote;

        public CanalAvisosBloqueo() : this(() => DateTime.UtcNow, ANTIRREBOTE)
        {
        }

        /// <summary>Con el reloj y el antirrebote inyectados (tests).</summary>
        internal CanalAvisosBloqueo(Func<DateTime> ahoraUtc, TimeSpan antirrebote)
        {
            _ahoraUtc = ahoraUtc ?? (() => DateTime.UtcNow);
            _antirrebote = antirrebote;
        }

        /// <summary>Hay un bloqueo. Llega en el hilo de quien publica: quien lo enseñe tiene que pasar al de la interfaz.</summary>
        public event EventHandler<AvisoBloqueoEventArgs> BloqueoDetectado;

        /// <summary>
        /// Publica el diagnóstico salvo que esté vacío o que ese mismo texto se haya publicado hace menos del antirrebote.
        /// Nunca lanza (ni aunque falle quien escucha). Devuelve si se ha publicado.
        /// </summary>
        public bool Publicar(string diagnostico)
        {
            try
            {
                string texto = diagnostico?.Trim();
                if (string.IsNullOrEmpty(texto))
                {
                    return false;
                }

                lock (_cerrojo)
                {
                    DateTime ahora = _ahoraUtc();
                    // Un reloj que va hacia atrás (cambio de hora del equipo) no debe callar el aviso para siempre
                    if (_publicados.TryGetValue(texto, out DateTime ultima) && ahora >= ultima && ahora - ultima < _antirrebote)
                    {
                        return false;
                    }
                    foreach (string viejo in _publicados.Where(p => ahora < p.Value || ahora - p.Value >= _antirrebote).Select(p => p.Key).ToList())
                    {
                        _ = _publicados.Remove(viejo);
                    }
                    _publicados[texto] = ahora;
                }

                Delegate[] oyentes = BloqueoDetectado?.GetInvocationList();
                if (oyentes != null)
                {
                    var args = new AvisoBloqueoEventArgs(texto);
                    foreach (EventHandler<AvisoBloqueoEventArgs> oyente in oyentes)
                    {
                        try
                        {
                            oyente(this, args);
                        }
                        catch (Exception)
                        {
                            // Un oyente que falla no deja sin aviso a los demás ni rompe la llamada a la API
                        }
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
