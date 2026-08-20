using ControlesUsuario.Models;
using ControlesUsuario.Services;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Caso real 20/08/26: el usuario de Tienda Online que factura FBA (almacén AMZ) cubre
    /// rutas por vacaciones y necesita alternar AMZ/ALG. La ventana muestra la información de
    /// siempre (solo lectura) y, debajo, los parámetros que el SERVIDOR declare editables para
    /// este usuario, con su combo de valores permitidos. Guardar valida server-side.
    /// </summary>
    public class ParametrosUsuarioDialogViewModel : BindableBase, IDialogAware
    {
        private readonly IServicioParametrosEditables _servicio;

        public ParametrosUsuarioDialogViewModel(IServicioParametrosEditables servicio)
        {
            _servicio = servicio;
        }

        public string Title => "Parámetros de usuario";

        private string _informacion;
        public string Informacion
        {
            get => _informacion;
            set => SetProperty(ref _informacion, value);
        }

        private ObservableCollection<ParametroEditableItem> _editables = new ObservableCollection<ParametroEditableItem>();
        public ObservableCollection<ParametroEditableItem> Editables
        {
            get => _editables;
            set => SetProperty(ref _editables, value);
        }

        private string _mensaje;
        public string Mensaje
        {
            get => _mensaje;
            set => SetProperty(ref _mensaje, value);
        }

        private bool _guardando;

        private DelegateCommand _guardarCommand;
        public DelegateCommand GuardarCommand => _guardarCommand ??
            (_guardarCommand = new DelegateCommand(OnGuardar, () => !_guardando && Editables.Any(e => e.TieneCambios)));

        private DelegateCommand _closeDialogCommand;
        public DelegateCommand CloseDialogCommand => _closeDialogCommand ??
            (_closeDialogCommand = new DelegateCommand(() => RequestClose?.Invoke(new DialogResult(ButtonResult.OK))));

        public event Action<IDialogResult> RequestClose;

        public bool CanCloseDialog() => true;

        public void OnDialogClosed() { }

        public async void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.ContainsKey("informacion"))
            {
                Informacion = parameters.GetValue<string>("informacion");
            }
            try
            {
                var editables = await _servicio.LeerEditables();
                Editables = new ObservableCollection<ParametroEditableItem>(
                    editables.Select(e => new ParametroEditableItem(e, () => GuardarCommand.RaiseCanExecuteChanged())));
            }
            catch (Exception ex)
            {
                Mensaje = $"No se pudieron cargar los parámetros editables: {ex.Message}";
            }
        }

        private async void OnGuardar()
        {
            _guardando = true;
            GuardarCommand.RaiseCanExecuteChanged();
            try
            {
                foreach (ParametroEditableItem item in Editables.Where(e => e.TieneCambios).ToList())
                {
                    ParametroEditableModel resultado = await _servicio.Cambiar(item.Clave, item.ValorSeleccionado);
                    item.AplicarGuardado(resultado);
                }
                Mensaje = "Cambios guardados. Ya están activos para los próximos pedidos.";
            }
            catch (Exception ex)
            {
                // El BadRequest del servidor trae el motivo legible (grupo, valor no admitido...)
                Mensaje = ex.Message;
            }
            finally
            {
                _guardando = false;
                GuardarCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Un parámetro editable en la ventana: combo de opciones + rastro del titular.</summary>
    public class ParametroEditableItem : BindableBase
    {
        private readonly Action _alCambiar;
        private string _valorGuardado;

        public ParametroEditableItem(ParametroEditableModel modelo, Action alCambiar)
        {
            Clave = modelo.Clave;
            Descripcion = modelo.Descripcion;
            Opciones = modelo.Opciones;
            ValorTitular = modelo.ValorTitular;
            _valorGuardado = modelo.ValorActual;
            _valorSeleccionado = modelo.ValorActual;
            _alCambiar = alCambiar;
        }

        public string Clave { get; }
        public string Descripcion { get; }
        public System.Collections.Generic.List<OpcionParametroModel> Opciones { get; }
        public string ValorTitular { get; private set; }

        private string _valorSeleccionado;
        public string ValorSeleccionado
        {
            get => _valorSeleccionado;
            set
            {
                if (SetProperty(ref _valorSeleccionado, value))
                {
                    _alCambiar?.Invoke();
                }
            }
        }

        public bool TieneCambios => ValorSeleccionado != _valorGuardado;

        public string TextoTitular => $"Titular: {ValorTitular} (al arrancar Nesto se ofrece volver a él)";
        public Visibility VisibilidadTitular =>
            string.IsNullOrWhiteSpace(ValorTitular) ? Visibility.Collapsed : Visibility.Visible;

        public void AplicarGuardado(ParametroEditableModel resultado)
        {
            _valorGuardado = resultado.ValorActual;
            ValorTitular = resultado.ValorTitular;
            RaisePropertyChanged(nameof(TextoTitular));
            RaisePropertyChanged(nameof(VisibilidadTitular));
        }
    }
}
