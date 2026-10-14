using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using System;

namespace ControlesUsuario.Tests.Dialogs
{
    /// <summary>
    /// Nesto#490 (4C.2): servicio de diálogos que no abre nada, para probar lo que compone
    /// <see cref="ServicioDialogosBase"/> (avisos, errores, confirmaciones, importes, textos, fechas).
    /// Apunta qué diálogo se pidió y con qué parámetros, y contesta con <see cref="Respuesta"/>.
    /// (Antes estas pruebas usaban ServicioDialogosPrism con un IDialogService falso.)
    /// </summary>
    internal class ServicioDialogosDePrueba : ServicioDialogosBase
    {
        public string Nombre { get; private set; }
        public ParametrosDialogo Parametros { get; private set; }
        public int VecesModal { get; private set; }
        public int VecesNoModal { get; private set; }

        /// <summary>Lo que "pulsa" el usuario (por defecto, Aceptar sin parámetros).</summary>
        public ResultadoDialogo Respuesta { get; set; } = new ResultadoDialogo(ResultadoBoton.OK);

        public override void ShowDialog(string name, ParametrosDialogo parameters, Action<ResultadoDialogo> callback)
        {
            Nombre = name;
            Parametros = parameters;
            VecesModal++;
            callback?.Invoke(Respuesta);
        }

        public override void Show(string name, ParametrosDialogo parameters, Action<ResultadoDialogo> callback)
        {
            Nombre = name;
            Parametros = parameters;
            VecesNoModal++;
        }
    }
}
