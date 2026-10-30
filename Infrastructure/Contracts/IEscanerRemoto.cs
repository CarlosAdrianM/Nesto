using System;
using System.Collections.Generic;

namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#589 (NestoAPI#768, Carlos 30/10/26): «escáner remoto». Quien trabaja en Nesto sin lector de códigos de barras lee con
    /// Ariadna en su móvil («Escanear para Nesto», Ariadna#69) y la API le manda lo leído por SignalR (AvisosHub, la misma conexión
    /// que la campana) SOLO a este Nesto si es el del mismo usuario con el foco más reciente. Lo implementa
    /// <see cref="Shared.AvisosEnTiempoRealSignalR"/>; lo recibe <c>ControlesUsuario.EscanerRemoto.ReceptorEscanerRemoto</c>,
    /// que lo escribe en el campo con el foco si acepta códigos de barras.
    /// </summary>
    public interface IEscanerRemoto
    {
        /// <summary>Ha llegado una lectura de Ariadna. Llega en un hilo que no es el de la UI.</summary>
        event EventHandler<LecturaEscanerRemoto> LecturaRecibida;

        /// <summary>
        /// Una ventana de Nesto se ha activado (<paramref name="conFoco"/> true) o Nesto ha dejado de estar delante (false): la
        /// API manda las lecturas al Nesto del usuario con el foco más reciente. Nunca lanza; sin conexión se recuerda y se dice
        /// al conectar.
        /// </summary>
        void NotificarActividad(bool conFoco);
    }

    /// <summary>NestoAPI#768: lo que manda la API por «lecturaEscanerRemoto».</summary>
    public sealed class LecturaEscanerRemoto
    {
        public string Codigo { get; set; }
        public string ClaveIdempotencia { get; set; }
        public DateTime? FechaHora { get; set; }
        /// <summary>Quién lo ha leído (sin dominio): siempre el usuario de este Nesto.</summary>
        public string Usuario { get; set; }
    }

    /// <summary>
    /// Nesto#589: la API no manda dos veces la misma lectura (idempotencia por clave), pero si llegara repetida (un reintento de
    /// SignalR) no se escribe dos veces. Recuerda las últimas <see cref="MAXIMO"/> claves. Sin clave, siempre es nueva.
    /// </summary>
    public sealed class FiltroLecturasRepetidas
    {
        public const int MAXIMO = 100;
        private readonly Queue<string> _orden = new Queue<string>();
        private readonly HashSet<string> _vistas = new HashSet<string>(StringComparer.Ordinal);
        private readonly object _candado = new object();

        public bool EsNueva(string clave)
        {
            string limpia = clave?.Trim();
            if (string.IsNullOrEmpty(limpia))
            {
                return true;
            }
            lock (_candado)
            {
                if (!_vistas.Add(limpia))
                {
                    return false;
                }
                _orden.Enqueue(limpia);
                while (_orden.Count > MAXIMO)
                {
                    _ = _vistas.Remove(_orden.Dequeue());
                }
                return true;
            }
        }
    }
}
