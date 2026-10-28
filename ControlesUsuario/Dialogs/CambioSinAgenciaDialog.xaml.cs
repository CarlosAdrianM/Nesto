using System.Windows.Controls;

namespace ControlesUsuario.Dialogs
{
    /// <summary>Nesto#564: aviso en rojo del cambio que no llega a la agencia (ver <see cref="CambioSinAgenciaDialogViewModel"/>).</summary>
    public partial class CambioSinAgenciaDialog : UserControl
    {
        public CambioSinAgenciaDialog()
        {
            InitializeComponent();
            // Con el cuadro de texto, el foco en él; sin él, en el botón por defecto («No»).
            Loaded += (_, __) =>
            {
                if (TextoConfirmacion?.IsVisible == true)
                {
                    _ = TextoConfirmacion.Focus();
                }
            };
        }
    }
}
