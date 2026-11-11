using System;

namespace ControlesUsuario.Notificaciones.Menciones
{
    /// <summary>
    /// Sugerencia 1183 (Laura Magán, 11/11/26): enseña la ventanita discreta de «le han mencionado» (en la esquina, sin sonido, sin
    /// robar el foco, se cierra sola). Si llega otra con una abierta, la sustituye (no se apilan). Lo usa
    /// <see cref="CampanaNotificacionesViewModel"/>; la de verdad es <see cref="PresentadorAvisoMencionWpf"/>.
    /// </summary>
    public interface IPresentadorAvisoMencion
    {
        void Mostrar(AvisoMencion aviso);
    }

    /// <summary>Sugerencia 1183: lo que enseña la ventanita y lo que hace al pulsarla (lo mismo que pulsar la notificación en la campana).</summary>
    public sealed class AvisoMencion
    {
        public AvisoMencion(int notificacionId, string titulo, string cuerpo, int otras, Action alPulsar)
        {
            NotificacionId = notificacionId;
            Titulo = string.IsNullOrWhiteSpace(titulo) ? "Le han mencionado en Novedades" : titulo.Trim();
            Cuerpo = cuerpo?.Trim() ?? string.Empty;
            Otras = Math.Max(0, otras);
            AlPulsar = alPulsar;
        }

        public int NotificacionId { get; }
        public string Titulo { get; }
        public string Cuerpo { get; }
        /// <summary>Cuántas menciones nuevas más han llegado a la vez (se enseña la más reciente).</summary>
        public int Otras { get; }
        public Action AlPulsar { get; }

        public bool HayCuerpo => !string.IsNullOrWhiteSpace(Cuerpo);
        public bool HayOtras => Otras > 0;
        public string TextoOtras => Otras == 1 ? "Y 1 mención más en la campana." : $"Y {Otras} menciones más en la campana.";
    }
}
