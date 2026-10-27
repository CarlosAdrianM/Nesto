using ControlesUsuario.Models;
using ControlesUsuario.Services;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Nesto.Infrastructure.Contracts;
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
    public class ParametrosUsuarioDialogViewModel : DialogoNestoBase
    {
        private readonly IServicioParametrosEditables _servicio;

        public ParametrosUsuarioDialogViewModel(IServicioParametrosEditables servicio)
        {
            _servicio = servicio;
            Title = "Parámetros de usuario";
        }

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

        private RelayCommand _guardarCommand;
        public RelayCommand GuardarCommand => _guardarCommand ??
            (_guardarCommand = new RelayCommand(OnGuardar, () => !_guardando && Editables.Any(e => e.TieneCambios)));

        private RelayCommand _closeDialogCommand;
        public RelayCommand CloseDialogCommand => _closeDialogCommand ??
            (_closeDialogCommand = new RelayCommand(() => RaiseRequestClose(ResultadoBoton.OK)));

        public override async void OnDialogOpened(ParametrosDialogo parameters)
        {
            if (parameters.ContainsKey("informacion"))
            {
                Informacion = parameters.GetValue<string>("informacion");
            }
            try
            {
                var editables = await _servicio.LeerEditables();
                Editables = new ObservableCollection<ParametroEditableItem>(
                    editables.Select(e => new ParametroEditableItem(e, () => GuardarCommand.NotifyCanExecuteChanged())));
            }
            catch (Exception ex)
            {
                Mensaje = $"No se pudieron cargar los parámetros editables: {ex.Message}";
            }
        }

        /// <summary>
        /// Nesto#490: lo que se dice al guardar. Si alguno de los guardados no se nota hasta reiniciar Nesto (el menú
        /// lateral), se avisa de que hay que cerrar y volver a abrir; si no, lo de siempre.
        /// </summary>
        internal static string MensajeAlGuardar(System.Collections.Generic.IReadOnlyCollection<ParametroEditableItem> guardados)
        {
            var conReinicio = guardados.Where(g => g.RequiereReiniciar).ToList();
            if (conReinicio.Count == 0)
            {
                return "Cambios guardados. Ya están activos para los próximos pedidos.";
            }
            string cuales = string.Join(", ", conReinicio.Select(g => $"«{g.Descripcion}»"));
            return $"Cambios guardados. Para que {cuales} surta efecto, cierre Nesto y vuelva a abrirlo.";
        }

        private async void OnGuardar()
        {
            _guardando = true;
            GuardarCommand.NotifyCanExecuteChanged();
            try
            {
                var guardados = new System.Collections.Generic.List<ParametroEditableItem>();
                foreach (ParametroEditableItem item in Editables.Where(e => e.TieneCambios).ToList())
                {
                    ParametroEditableModel resultado = await _servicio.Cambiar(item.Clave, item.ValorSeleccionado);
                    item.AplicarGuardado(resultado);
                    guardados.Add(item);
                }
                Mensaje = MensajeAlGuardar(guardados);
            }
            catch (Exception ex)
            {
                // El BadRequest del servidor trae el motivo legible (grupo, valor no admitido...)
                Mensaje = ex.Message;
            }
            finally
            {
                _guardando = false;
                GuardarCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Un parámetro editable en la ventana: combo de opciones + rastro del titular.</summary>
    public class ParametroEditableItem : ObservableObject
    {
        private readonly Action _alCambiar;
        private string _valorGuardado;

        public ParametroEditableItem(ParametroEditableModel modelo, Action alCambiar)
        {
            Clave = modelo.Clave;
            Descripcion = modelo.Descripcion;
            Opciones = modelo.Opciones;
            ValorTitular = modelo.ValorTitular;
            RequiereReiniciar = modelo.RequiereReiniciar;
            _valorGuardado = modelo.ValorActual;
            _valorSeleccionado = modelo.ValorActual;
            _alCambiar = alCambiar;
        }

        public string Clave { get; }
        public string Descripcion { get; }
        public System.Collections.Generic.List<OpcionParametroModel> Opciones { get; }
        public string ValorTitular { get; private set; }
        public bool RequiereReiniciar { get; }

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
            OnPropertyChanged(nameof(TextoTitular));
            OnPropertyChanged(nameof(VisibilidadTitular));
        }
    }
}
