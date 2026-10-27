using System.Windows.Controls;

namespace ControlesUsuario.Dialogs
{
    /// <summary>Nesto#558: motivo del cambio de estado del cliente (ver <see cref="MotivoCambioEstadoDialogViewModel"/>).</summary>
    public partial class MotivoCambioEstadoDialog : UserControl
    {
        public MotivoCambioEstadoDialog()
        {
            InitializeComponent();
            // La combo enfocada al abrir: flechas para elegir, Tab al comentario, Intro para aceptar.
            Loaded += (_, __) => ComboMotivo?.Focus();
        }
    }
}
