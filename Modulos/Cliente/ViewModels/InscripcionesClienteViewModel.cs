using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cliente.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Nesto#517: la pestaña «Eventos» de la ficha del cliente: sus inscripciones (GET api/Clientes/{empresa}/{cliente}/
    /// Inscripciones) y el botón para inscribirlo en un evento (el mismo diálogo que el mantenimiento de eventos). La ficha
    /// le dice qué cliente enseña con <see cref="CambiarCliente"/>; solo se carga cuando la pestaña está a la vista.
    /// </summary>
    public class InscripcionesClienteViewModel : ObservableObject
    {
        private readonly IEventosService _servicio;
        private readonly IServicioDialogos _dialogService;

        public InscripcionesClienteViewModel(IEventosService servicio, IServicioDialogos dialogService)
        {
            _servicio = servicio;
            _dialogService = dialogService;
            CargarCommand = new AsyncRelayCommand(CargarAsync, () => TieneCliente);
            InscribirCommand = new AsyncRelayCommand(InscribirAsync, () => TieneCliente);
        }

        public string Empresa { get; private set; }
        public string Cliente { get; private set; }
        public string Contacto { get; private set; }
        public string Nombre { get; private set; }

        public bool TieneCliente => !string.IsNullOrWhiteSpace(Cliente);

        /// <summary>De quién se cargó la lista (para no repetir la llamada al volver a la pestaña).</summary>
        private string _cargadoDe;

        private bool _aLaVista;
        /// <summary>La pestaña se ve: si el cliente ha cambiado, carga.</summary>
        public bool ALaVista
        {
            get => _aLaVista;
            set
            {
                if (SetProperty(ref _aLaVista, value) && value && _cargadoDe != Clave)
                {
                    _ = CargarAsync();
                }
            }
        }

        private string Clave => $"{Empresa?.Trim()}|{Cliente?.Trim()}";

        public void CambiarCliente(string empresa, string cliente, string contacto, string nombre)
        {
            Empresa = empresa?.Trim();
            Cliente = cliente?.Trim();
            Contacto = contacto?.Trim();
            Nombre = nombre?.Trim();
            OnPropertyChanged(nameof(TieneCliente));
            CargarCommand.NotifyCanExecuteChanged();
            InscribirCommand.NotifyCanExecuteChanged();
            if (_cargadoDe != Clave)
            {
                Inscripciones = new ObservableCollection<InscripcionEventoModel>();
                _cargadoDe = null;
                if (ALaVista)
                {
                    _ = CargarAsync();
                }
            }
        }

        private ObservableCollection<InscripcionEventoModel> _inscripciones = new();
        public ObservableCollection<InscripcionEventoModel> Inscripciones
        {
            get => _inscripciones;
            private set => SetProperty(ref _inscripciones, value);
        }

        private InscripcionEventoModel _seleccionada;
        public InscripcionEventoModel Seleccionada { get => _seleccionada; set => SetProperty(ref _seleccionada, value); }

        private bool _estaOcupado;
        public bool EstaOcupado { get => _estaOcupado; private set => SetProperty(ref _estaOcupado, value); }

        public AsyncRelayCommand CargarCommand { get; }

        public async Task CargarAsync()
        {
            if (!TieneCliente)
            {
                return;
            }
            string clave = Clave;
            try
            {
                EstaOcupado = true;
                List<InscripcionEventoModel> lista = await _servicio.LeerInscripcionesCliente(Empresa, Cliente) ?? new List<InscripcionEventoModel>();
                if (clave != Clave)
                {
                    return; // Ha cambiado de cliente mientras cargaba.
                }
                Inscripciones = new ObservableCollection<InscripcionEventoModel>(lista.OrderByDescending(i => i.FechaEvento));
                _cargadoDe = clave;
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        public AsyncRelayCommand InscribirCommand { get; }

        public async Task InscribirAsync()
        {
            if (!TieneCliente)
            {
                return;
            }
            try
            {
                ResultadoDialogo resultado = await _dialogService.ShowDialogAsync(InscribirEventoDialogViewModel.NOMBRE, new ParametrosDialogo
                {
                    { "empresa", Empresa },
                    { "cliente", Cliente },
                    { "contacto", Contacto },
                    { "nombre", Nombre }
                });
                if (resultado?.Result == ResultadoBoton.OK)
                {
                    await CargarAsync();
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }
    }
}
