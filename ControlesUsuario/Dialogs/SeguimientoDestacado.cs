using System;

namespace ControlesUsuario.Dialogs
{
    /// <summary>Lo que la vista tiene que hacer en un paso del seguimiento.</summary>
    internal readonly struct PasoSeguimiento
    {
        public PasoSeguimiento(bool colocar, bool darFoco, bool terminar)
        {
            Colocar = colocar;
            DarFoco = darFoco;
            Terminar = terminar;
        }

        /// <summary>Mover la lista para que el destacado quede centrado (o arriba, si no cabe).</summary>
        public bool Colocar { get; }
        /// <summary>Llevarle el foco (solo al comentario, y una vez).</summary>
        public bool DarFoco { get; }
        /// <summary>Dejar de seguirlo.</summary>
        public bool Terminar { get; }
    }

    /// <summary>
    /// Nesto#516: lleva a la vista la novedad o el comentario destacado (Nesto#477/#487) y lo mantiene ahí
    /// mientras la lista se asienta. Por qué no bastaba un <c>BringIntoView</c>:
    /// <list type="bullet">
    /// <item>el contenedor puede no existir aún (comentarios recién cargados, lista de Sugerencias o
    /// «Algo no funciona» recién cambiada): se reintenta, con tope;</item>
    /// <item>las miniaturas de los comentarios y la captura de la novedad llegan después y empujan el
    /// comentario fuera de la vista: se vuelve a colocar hasta que la lista está quieta;</item>
    /// <item>la novedad y el comentario se pedían por separado y la de la novedad podía pisar la del
    /// comentario: hay un único objetivo y el comentario manda;</item>
    /// <item><c>BringIntoView</c> solo lo hace asomar por el borde: se centra (o arriba si no cabe).</item>
    /// </list>
    /// Si el usuario mueve la lista, se deja de seguir. Sin WPF: la vista le pregunta en cada paso.
    /// </summary>
    internal sealed class SeguimientoDestacado
    {
        /// <summary>Pasos de 100 ms: unos 6 s para que aparezca y se asiente.</summary>
        public const int MAX_INTENTOS = 60;
        /// <summary>Pasos seguidos sin que se mueva para darlo por colocado.</summary>
        public const int PASOS_QUIETO = 10;
        /// <summary>Lo que se deja por encima cuando no cabe entero.</summary>
        public const double MARGEN_SUPERIOR = 8;

        private int _intentos;
        private int _quieto;
        private bool _focoDado;

        public object Objetivo { get; private set; }
        public bool EsComentario { get; private set; }
        public bool Activo { get; private set; }

        /// <summary>La novedad destacada. Si ya se está siguiendo un comentario, no lo pisa.</summary>
        public void SeguirNovedad(object novedad)
        {
            if (novedad == null || (Activo && EsComentario))
            {
                return;
            }
            Empezar(novedad, esComentario: false);
        }

        /// <summary>El comentario destacado: manda sobre la novedad.</summary>
        public void SeguirComentario(object comentario)
        {
            if (comentario == null)
            {
                return;
            }
            Empezar(comentario, esComentario: true);
        }

        /// <summary>El usuario ha movido la lista o ha pulsado algo: no se le vuelve a llevar.</summary>
        public void Interrumpir() => Activo = false;

        /// <param name="encontrado">El contenedor del objetivo ya existe y está pintado.</param>
        /// <param name="yaColocado">Ya está donde se quiere (la lista no se ha movido desde el último paso).</param>
        public PasoSeguimiento Paso(bool encontrado, bool yaColocado)
        {
            if (!Activo)
            {
                return new PasoSeguimiento(false, false, true);
            }
            _intentos++;
            bool agotado = _intentos >= MAX_INTENTOS;
            if (!encontrado)
            {
                Activo = !agotado;
                return new PasoSeguimiento(false, false, agotado);
            }

            bool darFoco = EsComentario && !_focoDado;
            _focoDado |= darFoco;
            _quieto = yaColocado ? _quieto + 1 : 0;
            bool terminar = agotado || _quieto >= PASOS_QUIETO;
            Activo = !terminar;
            return new PasoSeguimiento(!yaColocado, darFoco, terminar);
        }

        /// <summary>
        /// Desplazamiento vertical que deja el elemento centrado en el área visible o, si es más alto que
        /// ella, con su principio arriba (para leerlo desde el comienzo).
        /// </summary>
        /// <param name="yElemento">Dónde empieza el elemento respecto al borde superior del área visible.</param>
        public static double CalcularDesplazamiento(double desplazamientoActual, double yElemento, double altoElemento, double altoVisible)
        {
            double hueco = altoElemento <= altoVisible
                ? (altoVisible - altoElemento) / 2
                : MARGEN_SUPERIOR;
            return Math.Max(0, desplazamientoActual + yElemento - hueco);
        }

        private void Empezar(object objetivo, bool esComentario)
        {
            Objetivo = objetivo;
            EsComentario = esComentario;
            Activo = true;
            _intentos = 0;
            _quieto = 0;
            _focoDado = false;
        }
    }
}
