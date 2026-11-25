using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Rellenar el inventario sin querer (Paloma, 25/11/26: 3.032 filas de Alcobendas por error, y no era la primera vez): una
    /// confirmación que obliga a ESCRIBIR algo (el nombre o el código del almacén) para activar el botón de hacerlo. «Cancelar» es
    /// el botón por defecto: Intro y Esc cancelan. Reutilizable para cualquier cosa con consecuencias: se abre con
    /// <see cref="ConfirmacionEscribiendo.Pedir"/>.
    /// </summary>
    public class ConfirmacionEscribiendoDialogViewModel : DialogoNestoBase
    {
        public const string NOMBRE = "ConfirmacionEscribiendoDialog";
        public const string PARAMETRO_INSTRUCCION = "instruccion";
        public const string PARAMETRO_ACEPTABLES = "aceptables";
        public const string PARAMETRO_TEXTO_ACEPTAR = "textoAceptar";

        private IReadOnlyList<string> _aceptables = Array.Empty<string>();

        public ConfirmacionEscribiendoDialogViewModel()
        {
            Title = "Confirmar";
            AceptarCommand = new RelayCommand(Aceptar, () => PuedeAceptar);
            CancelarCommand = new RelayCommand(() => RaiseRequestClose(ResultadoBoton.Cancel));
        }

        private string _message;
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        private string _instruccion;
        public string Instruccion
        {
            get => _instruccion;
            set => SetProperty(ref _instruccion, value);
        }

        private string _textoAceptar = "Aceptar";
        public string TextoAceptar
        {
            get => _textoAceptar;
            set => SetProperty(ref _textoAceptar, value);
        }

        private string _texto;
        public string Texto
        {
            get => _texto;
            set
            {
                if (SetProperty(ref _texto, value))
                {
                    OnPropertyChanged(nameof(PuedeAceptar));
                    AceptarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool PuedeAceptar => ConfirmacionEscribiendo.Coincide(Texto, _aceptables);

        public RelayCommand AceptarCommand { get; }
        public RelayCommand CancelarCommand { get; }

        private void Aceptar()
        {
            if (!PuedeAceptar)
            {
                return;
            }
            RaiseRequestClose(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo { { "text", Texto?.Trim() ?? string.Empty } }));
        }

        public override void OnDialogOpened(ParametrosDialogo parameters)
        {
            if (parameters.TryGetValue("title", out string titulo) && !string.IsNullOrWhiteSpace(titulo))
            {
                Title = titulo;
            }
            if (parameters.TryGetValue("message", out string mensaje))
            {
                Message = mensaje;
            }
            if (parameters.TryGetValue(PARAMETRO_INSTRUCCION, out string instruccion))
            {
                Instruccion = instruccion;
            }
            if (parameters.TryGetValue(PARAMETRO_TEXTO_ACEPTAR, out string textoAceptar) && !string.IsNullOrWhiteSpace(textoAceptar))
            {
                TextoAceptar = textoAceptar;
            }
            if (parameters.TryGetValue(PARAMETRO_ACEPTABLES, out IEnumerable<string> aceptables) && aceptables != null)
            {
                _aceptables = aceptables.ToList();
            }
            OnPropertyChanged(nameof(PuedeAceptar));
            AceptarCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>La confirmación escribiendo: LA REGLA (pura) de qué vale y cómo se pide.</summary>
    public static class ConfirmacionEscribiendo
    {
        /// <summary>
        /// True si lo escrito es alguno de los aceptables, sin distinguir mayúsculas ni tildes y sin los espacios de alrededor
        /// («alcobendas», « ALC », «Alcobendás»). Nada aceptable o nada escrito: false.
        /// </summary>
        public static bool Coincide(string escrito, IEnumerable<string> aceptables)
        {
            string normalizado = Normalizar(escrito);
            if (normalizado.Length == 0 || aceptables == null)
            {
                return false;
            }
            return aceptables.Select(Normalizar).Any(a => a.Length > 0 && a == normalizado);
        }

        internal static string Normalizar(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return string.Empty;
            }
            string descompuesto = texto.Trim().Normalize(NormalizationForm.FormD);
            var sinTildes = new StringBuilder(descompuesto.Length);
            foreach (char c in descompuesto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    sinTildes.Append(c);
                }
            }
            return sinTildes.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        }

        /// <summary>
        /// Abre el diálogo y devuelve true solo si el usuario ha escrito uno de los <paramref name="aceptables"/> y ha pulsado
        /// <paramref name="textoAceptar"/>. Cancelar, cerrar, Intro o Esc: false.
        /// </summary>
        public static bool Pedir(IServicioDialogos dialogos, string titulo, string mensaje, string instruccion, string textoAceptar,
            IEnumerable<string> aceptables)
        {
            if (dialogos == null)
            {
                return false;
            }
            var lista = (aceptables ?? Enumerable.Empty<string>()).Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToList();
            bool confirmado = false;
            var parametros = new ParametrosDialogo
            {
                { "title", titulo },
                { "message", mensaje },
                { ConfirmacionEscribiendoDialogViewModel.PARAMETRO_INSTRUCCION, instruccion },
                { ConfirmacionEscribiendoDialogViewModel.PARAMETRO_TEXTO_ACEPTAR, textoAceptar },
                { ConfirmacionEscribiendoDialogViewModel.PARAMETRO_ACEPTABLES, lista }
            };
            dialogos.ShowDialog(ConfirmacionEscribiendoDialogViewModel.NOMBRE, parametros, r =>
            {
                // Por si acaso, se vuelve a comprobar lo escrito: solo vale lo que casa.
                confirmado = r?.Result == ResultadoBoton.OK
                    && r.Parameters != null && r.Parameters.TryGetValue("text", out string texto) && Coincide(texto, lista);
            });
            return confirmado;
        }
    }
}
