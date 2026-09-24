using CommunityToolkit.Mvvm.ComponentModel;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ControlesUsuario.Dialogs
{
    /// <summary>La @ que se está escribiendo: dónde empieza, hasta dónde llega la palabra y lo escrito hasta el cursor.</summary>
    public sealed class MencionEnCurso
    {
        public MencionEnCurso(int inicio, int fin, string filtro)
        {
            Inicio = inicio;
            Fin = fin;
            Filtro = filtro;
        }

        /// <summary>Posición de la @.</summary>
        public int Inicio { get; }
        /// <summary>Fin de la palabra (aunque siga tras el cursor): hasta aquí se sustituye.</summary>
        public int Fin { get; }
        /// <summary>Lo escrito entre la @ y el cursor (puede ser vacío).</summary>
        public string Filtro { get; }
    }

    /// <summary>Un trozo del texto de un comentario: texto normal o una @mención (se pinta resaltada).</summary>
    public sealed class TrozoTexto
    {
        public TrozoTexto(string texto, bool esMencion)
        {
            Texto = texto;
            EsMencion = esMencion;
        }

        public string Texto { get; }
        public bool EsMencion { get; }
    }

    /// <summary>
    /// Nesto#491 (NestoAPI#537): lógica pura de las @menciones en Novedades, con las mismas reglas que la API
    /// (ReglasMenciones): la @ va al principio o tras algo que no sea letra, dígito, punto o @ (así un correo no
    /// es una mención), el nombre empieza por letra y se compara sin tildes ni mayúsculas.
    /// </summary>
    public static class Menciones
    {
        private static readonly Regex PATRON = new Regex(@"(?<![\p{L}\p{Nd}.@])@[\p{L}][\p{L}\p{Nd}_]*", RegexOptions.Compiled);

        private static bool EsDeNombre(char c) => char.IsLetterOrDigit(c) || c == '_';

        /// <summary>
        /// La @mención que se está escribiendo en <paramref name="cursor"/>, o null si el cursor no está justo en
        /// una (tras la @ o dentro del nombre).
        /// </summary>
        public static MencionEnCurso Detectar(string texto, int cursor)
        {
            if (string.IsNullOrEmpty(texto) || cursor <= 0 || cursor > texto.Length)
            {
                return null;
            }
            int arroba = cursor - 1;
            while (arroba >= 0 && EsDeNombre(texto[arroba]))
            {
                arroba--;
            }
            if (arroba < 0 || texto[arroba] != '@')
            {
                return null;
            }
            if (arroba > 0)
            {
                char antes = texto[arroba - 1];
                if (char.IsLetterOrDigit(antes) || antes == '.' || antes == '@')
                {
                    return null;
                }
            }
            string filtro = texto.Substring(arroba + 1, cursor - arroba - 1);
            if (filtro.Length > 0 && !char.IsLetter(filtro[0]))
            {
                return null;
            }
            int fin = cursor;
            while (fin < texto.Length && EsDeNombre(texto[fin]))
            {
                fin++;
            }
            return new MencionEnCurso(arroba, fin, filtro);
        }

        /// <summary>Sin tildes y en mayúsculas: «maría» casa con «Maria».</summary>
        public static string Normalizar(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return string.Empty;
            }
            string descompuesto = texto.Trim().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in descompuesto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    _ = sb.Append(c);
                }
            }
            return sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        }

        /// <summary>
        /// Los que casan con <paramref name="filtro"/> (sin tildes ni mayúsculas): primero los que empiezan por él
        /// y luego los que lo contienen, cada grupo por orden alfabético. Filtro vacío: todos.
        /// </summary>
        public static List<Mencionable> Filtrar(IEnumerable<Mencionable> mencionables, string filtro)
        {
            string buscado = Normalizar(filtro);
            return (mencionables ?? Enumerable.Empty<Mencionable>())
                .Where(m => m != null && !string.IsNullOrWhiteSpace(m.Nombre))
                .Select(m => new { Mencionable = m, Nombre = Normalizar(m.Nombre) })
                .Where(x => x.Nombre.Contains(buscado))
                .OrderBy(x => x.Nombre.StartsWith(buscado, StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(x => x.Nombre, StringComparer.Ordinal)
                .Select(x => x.Mencionable)
                .ToList();
        }

        /// <summary>
        /// Sustituye la @ y lo escrito tras ella por «@Nombre » y devuelve el texto nuevo y dónde queda el cursor
        /// (tras el espacio). Si ya había un espacio detrás, no se duplica.
        /// </summary>
        public static (string Texto, int Cursor) Sustituir(string texto, MencionEnCurso mencion, string nombre)
        {
            if (texto == null || mencion == null || string.IsNullOrWhiteSpace(nombre))
            {
                return (texto, mencion?.Fin ?? 0);
            }
            string insertado = "@" + nombre.Trim();
            string resto = texto.Substring(mencion.Fin);
            if (!resto.StartsWith(" ", StringComparison.Ordinal))
            {
                insertado += " ";
                return (texto.Substring(0, mencion.Inicio) + insertado + resto, mencion.Inicio + insertado.Length);
            }
            return (texto.Substring(0, mencion.Inicio) + insertado + resto, mencion.Inicio + insertado.Length + 1);
        }

        /// <summary>Parte el texto en trozos normales y @menciones (para pintarlas resaltadas sin tocar el resto).</summary>
        public static List<TrozoTexto> Trocear(string texto)
        {
            var trozos = new List<TrozoTexto>();
            if (string.IsNullOrEmpty(texto))
            {
                return trozos;
            }
            int posicion = 0;
            foreach (Match m in PATRON.Matches(texto))
            {
                if (m.Index > posicion)
                {
                    trozos.Add(new TrozoTexto(texto.Substring(posicion, m.Index - posicion), false));
                }
                trozos.Add(new TrozoTexto(m.Value, true));
                posicion = m.Index + m.Length;
            }
            if (posicion < texto.Length)
            {
                trozos.Add(new TrozoTexto(texto.Substring(posicion), false));
            }
            return trozos;
        }
    }

    /// <summary>
    /// Nesto#491: la lista de mencionables, pedida a la API UNA vez por ventana y compartida por todos los cuadros.
    /// Si falla, lista vacía (sin desplegable y sin error) y no se reintenta.
    /// </summary>
    public class ListaMencionables
    {
        private readonly INovedadesService _servicio;
        private Task<List<Mencionable>> _carga;

        public ListaMencionables(INovedadesService servicio)
        {
            _servicio = servicio;
        }

        public Task<List<Mencionable>> Obtener() => _carga ?? (_carga = Cargar());

        private async Task<List<Mencionable>> Cargar()
        {
            if (_servicio == null)
            {
                return new List<Mencionable>();
            }
            try
            {
                return await _servicio.LeerMencionables() ?? new List<Mencionable>();
            }
            catch (Exception)
            {
                return new List<Mencionable>();
            }
        }
    }

    /// <summary>
    /// Nesto#491: el desplegable de @menciones de un cuadro de texto. La vista le cuenta el texto y el cursor
    /// (<see cref="Actualizar"/>) y las teclas; él decide si se abre, qué se ofrece y qué texto queda al elegir.
    /// </summary>
    public class AutocompletadoMenciones : ObservableObject
    {
        private readonly ListaMencionables _lista;
        private string _texto;
        private int _cursor;
        private MencionEnCurso _mencion;
        // La @ en la que se pulsó Esc: no se vuelve a abrir hasta salir de ella.
        private int? _inicioDescartado;

        public AutocompletadoMenciones(ListaMencionables lista)
        {
            _lista = lista;
        }

        private List<Mencionable> _sugerencias = new List<Mencionable>();
        public List<Mencionable> Sugerencias { get => _sugerencias; private set => SetProperty(ref _sugerencias, value); }

        private Mencionable _seleccionada;
        public Mencionable Seleccionada { get => _seleccionada; set => SetProperty(ref _seleccionada, value); }

        private bool _abierto;
        public bool Abierto { get => _abierto; private set => SetProperty(ref _abierto, value); }

        /// <summary>El texto o el cursor han cambiado: abre, filtra o cierra el desplegable.</summary>
        public async Task Actualizar(string texto, int cursor)
        {
            _texto = texto;
            _cursor = cursor;
            if (_lista == null || Menciones.Detectar(texto, cursor) == null)
            {
                _inicioDescartado = null;
                Cerrar();
                return;
            }
            List<Mencionable> todos = await _lista.Obtener();
            // Mientras se cargaba la lista se ha podido seguir escribiendo: manda lo último.
            _mencion = Menciones.Detectar(_texto, _cursor);
            if (_mencion == null || _mencion.Inicio == _inicioDescartado)
            {
                Cerrar();
                return;
            }
            _inicioDescartado = null;
            List<Mencionable> filtrados = Menciones.Filtrar(todos, _mencion.Filtro);
            Sugerencias = filtrados;
            if (filtrados.Count == 0)
            {
                Cerrar();
                return;
            }
            if (Seleccionada == null || !filtrados.Contains(Seleccionada))
            {
                Seleccionada = filtrados[0];
            }
            Abierto = true;
        }

        /// <summary>Flechas: mueve la selección (sin salirse de la lista).</summary>
        public void Mover(int delta)
        {
            if (!Abierto || Sugerencias.Count == 0)
            {
                return;
            }
            int actual = Seleccionada == null ? -1 : Sugerencias.IndexOf(Seleccionada);
            int nuevo = Math.Max(0, Math.Min(Sugerencias.Count - 1, actual + delta));
            Seleccionada = Sugerencias[nuevo];
        }

        /// <summary>Esc: se cierra y no se reabre mientras se siga en la misma @.</summary>
        public void Descartar()
        {
            if (_mencion != null)
            {
                _inicioDescartado = _mencion.Inicio;
            }
            Cerrar();
        }

        public void Cerrar()
        {
            Abierto = false;
            Seleccionada = null;
            Sugerencias = new List<Mencionable>();
            _mencion = null;
        }

        /// <summary>
        /// Enter, Tab o clic: el texto con «@Nombre » en lugar de lo escrito tras la @, y dónde dejar el cursor.
        /// Null si no hay nada que elegir (la tecla sigue su curso normal).
        /// </summary>
        public (string Texto, int Cursor)? Elegir(Mencionable elegido = null)
        {
            Mencionable mencionable = elegido ?? Seleccionada;
            if (!Abierto || mencionable == null || _mencion == null)
            {
                return null;
            }
            (string Texto, int Cursor) resultado = Menciones.Sustituir(_texto, _mencion, mencionable.Nombre);
            Cerrar();
            _texto = resultado.Texto;
            _cursor = resultado.Cursor;
            return resultado;
        }
    }
}
