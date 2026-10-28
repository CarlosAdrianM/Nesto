using System.Windows.Controls;

namespace ControlesUsuario.Dialogs
{
    /// <summary>Nesto#563: enviar un documento por correo (ver <see cref="EnviarPorCorreoDialogViewModel"/>).</summary>
    public partial class EnviarPorCorreoDialog : UserControl
    {
        public EnviarPorCorreoDialog()
        {
            InitializeComponent();
            // Los destinatarios enfocados al abrir, con el cursor al final para añadir otro.
            Loaded += (_, __) =>
            {
                TextoDestinatarios?.Focus();
                if (TextoDestinatarios != null)
                {
                    TextoDestinatarios.CaretIndex = TextoDestinatarios.Text?.Length ?? 0;
                }
            };
        }
    }
}
