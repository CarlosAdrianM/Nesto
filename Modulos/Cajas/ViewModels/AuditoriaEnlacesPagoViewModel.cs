using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.ViewModels
{
    /// <summary>
    /// Nesto#261: consulta (solo lectura) de los enlaces de pago para administración: quién y cuándo creó cada
    /// enlace, para qué cliente, por cuánto y adónde se envió. Se busca por el identificador del enlace (el que
    /// ve el cliente, p. ej. B9BC22C32366) o por fechas, cliente, usuario y estado. La API solo deja consultarlo
    /// a Administración y Dirección.
    /// </summary>
    public class AuditoriaEnlacesPagoViewModel : ObservableObject, INavigationAware
    {
        private readonly IAuditoriaEnlacesPagoService _servicio;
        private readonly IServicioDialogos _dialogService;

        /// <summary>Días hacia atrás que se enseñan al abrir la ventana.</summary>
        internal const int DIAS_POR_DEFECTO = 30;

        public AuditoriaEnlacesPagoViewModel(IAuditoriaEnlacesPagoService servicio, IServicioDialogos dialogService)
        {
            _servicio = servicio;
            _dialogService = dialogService;
            Titulo = "Enlaces de pago";
            FechaHasta = DateTime.Today;
            FechaDesde = DateTime.Today.AddDays(-DIAS_POR_DEFECTO);
            BuscarCommand = new AsyncRelayCommand(BuscarAsync);
            LimpiarFiltrosCommand = new RelayCommand(LimpiarFiltros);
        }

        public string Titulo { get; }

        /// <summary>Estados que puede tener un enlace ("" = todos).</summary>
        public List<string> Estados { get; } = new List<string> { string.Empty, "Pendiente", "Enviado", "Autorizado", "Denegado" };

        private string? _numeroOrden;
        /// <summary>Identificador del enlace. Si se rellena, las fechas no cuentan.</summary>
        public string? NumeroOrden
        {
            get => _numeroOrden;
            set => SetProperty(ref _numeroOrden, value);
        }

        private DateTime? _fechaDesde;
        public DateTime? FechaDesde
        {
            get => _fechaDesde;
            set => SetProperty(ref _fechaDesde, value);
        }

        private DateTime? _fechaHasta;
        public DateTime? FechaHasta
        {
            get => _fechaHasta;
            set => SetProperty(ref _fechaHasta, value);
        }

        private string? _cliente;
        public string? Cliente
        {
            get => _cliente;
            set => SetProperty(ref _cliente, value);
        }

        private string? _usuario;
        public string? Usuario
        {
            get => _usuario;
            set => SetProperty(ref _usuario, value);
        }

        private string? _estado;
        public string? Estado
        {
            get => _estado;
            set => SetProperty(ref _estado, value);
        }

        private ObservableCollection<EnlacePagoAuditoriaModel> _enlaces = new ObservableCollection<EnlacePagoAuditoriaModel>();
        public ObservableCollection<EnlacePagoAuditoriaModel> Enlaces
        {
            get => _enlaces;
            private set
            {
                if (SetProperty(ref _enlaces, value))
                {
                    OnPropertyChanged(nameof(Resumen));
                }
            }
        }

        private EnlacePagoAuditoriaModel? _enlaceSeleccionado;
        public EnlacePagoAuditoriaModel? EnlaceSeleccionado
        {
            get => _enlaceSeleccionado;
            set
            {
                if (SetProperty(ref _enlaceSeleccionado, value))
                {
                    OnPropertyChanged(nameof(HayEnlaceSeleccionado));
                    OnPropertyChanged(nameof(DetalleSeleccionado));
                }
            }
        }

        public bool HayEnlaceSeleccionado => EnlaceSeleccionado != null;

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set => SetProperty(ref _estaOcupado, value);
        }

        private bool _haBuscado;

        /// <summary>Cuántos enlaces salen y por cuánto (la cabecera de la ventana).</summary>
        public string Resumen
        {
            get
            {
                if (!_haBuscado)
                {
                    return string.Empty;
                }
                int total = Enlaces.Count;
                if (total == 0)
                {
                    return "No hay enlaces de pago con esos filtros.";
                }
                decimal importe = Enlaces.Sum(e => e.Importe);
                return (total == 1 ? "1 enlace" : $"{total} enlaces") + $" por {importe:N2} €.";
            }
        }

        /// <summary>
        /// El enlace seleccionado contado en frase, como lo pide administración cuando llama un cliente:
        /// «Creado por DOMINIO\JuanPerez el 15/01/2025 a las 10:30. Enviado a cliente@email.com».
        /// </summary>
        public string DetalleSeleccionado => Describir(EnlaceSeleccionado);

        internal static string Describir(EnlacePagoAuditoriaModel? enlace)
        {
            if (enlace == null)
            {
                return string.Empty;
            }
            string usuario = string.IsNullOrWhiteSpace(enlace.Usuario) ? "un usuario desconocido" : enlace.Usuario!.Trim();
            string texto = $"Enlace {enlace.NumeroOrden}: creado por {usuario} el {enlace.FechaCreacion:dd/MM/yyyy} a las {enlace.FechaCreacion:HH:mm}";
            string cliente = string.IsNullOrWhiteSpace(enlace.NombreCliente)
                ? $"cliente {enlace.Cliente}"
                : $"cliente {enlace.Cliente} ({enlace.NombreCliente!.Trim()})";
            texto += $" para el {cliente}, por {enlace.Importe:N2} €.";

            var envios = new List<string>();
            if (!string.IsNullOrWhiteSpace(enlace.Correo))
            {
                envios.Add($"por correo a {enlace.Correo!.Trim()}");
            }
            if (!string.IsNullOrWhiteSpace(enlace.Movil))
            {
                envios.Add($"por SMS al {enlace.Movil!.Trim()}");
            }
            texto += envios.Any()
                ? $" Enviado {string.Join(" y ", envios)}."
                : " No consta que se enviara por correo ni por SMS.";

            texto += $" Estado: {enlace.Estado}";
            if (enlace.FechaActualizacion.HasValue)
            {
                texto += $" (último cambio el {enlace.FechaActualizacion.Value:dd/MM/yyyy} a las {enlace.FechaActualizacion.Value:HH:mm})";
            }
            return texto + ".";
        }

        public IAsyncRelayCommand BuscarCommand { get; }

        public async Task BuscarAsync()
        {
            bool porIdentificador = !string.IsNullOrWhiteSpace(NumeroOrden);
            if (!porIdentificador && FechaDesde.HasValue && FechaHasta.HasValue && FechaDesde.Value.Date > FechaHasta.Value.Date)
            {
                _dialogService.ShowError("La fecha desde no puede ser posterior a la fecha hasta.");
                return;
            }
            var filtro = new FiltroAuditoriaEnlacesPago
            {
                NumeroOrden = NumeroOrden?.Trim(),
                // Con identificador, las fechas sobran (el enlace puede ser antiguo)
                FechaDesde = porIdentificador ? null : FechaDesde,
                FechaHasta = porIdentificador ? null : FechaHasta,
                Cliente = Cliente?.Trim(),
                Usuario = Usuario?.Trim(),
                Estado = string.IsNullOrWhiteSpace(Estado) ? null : Estado
            };
            try
            {
                EstaOcupado = true;
                List<EnlacePagoAuditoriaModel> lista = await _servicio.Buscar(filtro);
                _haBuscado = true;
                Enlaces = new ObservableCollection<EnlacePagoAuditoriaModel>(lista ?? new List<EnlacePagoAuditoriaModel>());
                OnPropertyChanged(nameof(Resumen));
                // Buscando un enlace concreto, se enseña directamente
                EnlaceSeleccionado = porIdentificador && Enlaces.Count == 1 ? Enlaces[0] : null;
            }
            catch (Exception ex)
            {
                Enlaces = new ObservableCollection<EnlacePagoAuditoriaModel>();
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        public IRelayCommand LimpiarFiltrosCommand { get; }

        private void LimpiarFiltros()
        {
            NumeroOrden = null;
            Cliente = null;
            Usuario = null;
            Estado = null;
            FechaHasta = DateTime.Today;
            FechaDesde = DateTime.Today.AddDays(-DIAS_POR_DEFECTO);
        }

        private bool _cargadoAlAbrir;

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            // Al abrir, los últimos días; si ya estaba abierta, se respeta lo que se hubiera buscado.
            if (!_cargadoAlAbrir)
            {
                _cargadoAlAbrir = true;
                _ = BuscarAsync();
            }
        }

        /// <summary>Una sola pestaña: si ya está abierta, se reutiliza.</summary>
        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
        }
    }
}
