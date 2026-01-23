using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static Nesto.Infrastructure.Shared.Constantes;

namespace Nesto.Modulos.Cajas.ViewModels
{
    /// <summary>
    /// ViewModel para la vista de Mayor de Clientes/Proveedores.
    /// Issue #275: Nueva vista para consultar el Mayor de una cuenta.
    /// </summary>
    public class MayorCuentaViewModel : BindableBase, INavigationAware
    {
        private readonly IConfiguracion _configuracion;
        private readonly IDialogService _dialogService;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public MayorCuentaViewModel(
            IConfiguracion configuracion,
            IDialogService dialogService,
            IServicioAutenticacion servicioAutenticacion)
        {
            _configuracion = configuracion ?? throw new ArgumentNullException(nameof(configuracion));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _servicioAutenticacion = servicioAutenticacion ?? throw new ArgumentNullException(nameof(servicioAutenticacion));

            Titulo = "Mayor de Cuenta";
            VerMayorCommand = new DelegateCommand(OnVerMayorSync, CanVerMayor);

            // Valores por defecto
            EsCliente = true;
            FechaDesde = new DateTime(DateTime.Today.Year, 1, 1);
            FechaHasta = DateTime.Today;
            SoloFacturas = false;
            EliminarPasoACartera = true;
        }

        #region Propiedades

        public string Titulo { get; private set; }

        private bool _esCliente;
        public bool EsCliente
        {
            get => _esCliente;
            set
            {
                if (SetProperty(ref _esCliente, value))
                {
                    RaisePropertyChanged(nameof(EsProveedor));
                    // Limpiar el numero de cuenta al cambiar el tipo
                    NumeroCuenta = null;
                    VerMayorCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool EsProveedor
        {
            get => !_esCliente;
            set => EsCliente = !value;
        }

        private string _numeroCuenta;
        public string NumeroCuenta
        {
            get => _numeroCuenta;
            set
            {
                if (SetProperty(ref _numeroCuenta, value))
                {
                    VerMayorCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private DateTime _fechaDesde;
        public DateTime FechaDesde
        {
            get => _fechaDesde;
            set => SetProperty(ref _fechaDesde, value);
        }

        private DateTime _fechaHasta;
        public DateTime FechaHasta
        {
            get => _fechaHasta;
            set => SetProperty(ref _fechaHasta, value);
        }

        private bool _soloFacturas;
        public bool SoloFacturas
        {
            get => _soloFacturas;
            set => SetProperty(ref _soloFacturas, value);
        }

        private bool _eliminarPasoACartera;
        public bool EliminarPasoACartera
        {
            get => _eliminarPasoACartera;
            set => SetProperty(ref _eliminarPasoACartera, value);
        }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            set => SetProperty(ref _estaOcupado, value);
        }

        private string _mensajeEstado;
        public string MensajeEstado
        {
            get => _mensajeEstado;
            set => SetProperty(ref _mensajeEstado, value);
        }

        #endregion

        #region Comandos

        public DelegateCommand VerMayorCommand { get; }

        private bool CanVerMayor()
        {
            return !string.IsNullOrWhiteSpace(NumeroCuenta) && FechaDesde <= FechaHasta;
        }

        private void OnVerMayorSync()
        {
            _ = OnVerMayorAsync();
        }

        private async Task OnVerMayorAsync()
        {
            try
            {
                EstaOcupado = true;
                MensajeEstado = "Generando Mayor...";

                using var client = new HttpClient();
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                client.Timeout = TimeSpan.FromSeconds(120);

                // Configurar autorizacion JWT
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    _dialogService.ShowError("No se pudo configurar la autorizacion. Por favor, reinicie la aplicacion.");
                    return;
                }

                // Construir URL
                var tipoCuenta = EsCliente ? "cliente" : "proveedor";
                var url = $"Contabilidades/MayorPdf" +
                          $"?empresa={Empresas.EMPRESA_DEFECTO}" +
                          $"&tipoCuenta={tipoCuenta}" +
                          $"&cuenta={NumeroCuenta}" +
                          $"&fechaDesde={FechaDesde:yyyy-MM-dd}" +
                          $"&fechaHasta={FechaHasta:yyyy-MM-dd}" +
                          $"&soloFacturas={SoloFacturas.ToString().ToLower()}" +
                          $"&eliminarPasoACartera={EliminarPasoACartera.ToString().ToLower()}";

                Debug.WriteLine($"MayorCuentaViewModel: URL = {url}");

                var response = await client.GetAsync(url);
                Debug.WriteLine($"MayorCuentaViewModel: Response status = {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var pdfBytes = await response.Content.ReadAsByteArrayAsync();
                    Debug.WriteLine($"MayorCuentaViewModel: PDF recibido, {pdfBytes?.Length ?? 0} bytes");

                    if (pdfBytes == null || pdfBytes.Length == 0)
                    {
                        _dialogService.ShowError("El servidor devolvio un PDF vacio.");
                        return;
                    }

                    // Guardar en carpeta Descargas
                    var rutaDescargas = ObtenerRutaDescargas();
                    var sufijo = "";
                    if (SoloFacturas) sufijo += "_Facturas";
                    if (EliminarPasoACartera) sufijo += "_SinPasoCartera";
                    var nombreArchivo = $"Mayor_{tipoCuenta}_{NumeroCuenta}_{FechaDesde:yyyyMMdd}_{FechaHasta:yyyyMMdd}{sufijo}.pdf";
                    var rutaCompleta = Path.Combine(rutaDescargas, nombreArchivo);

                    Debug.WriteLine($"MayorCuentaViewModel: Guardando en {rutaCompleta}");
                    await File.WriteAllBytesAsync(rutaCompleta, pdfBytes);

                    // Abrir la carpeta con el archivo seleccionado
                    Process.Start("explorer.exe", $"/select,\"{rutaCompleta}\"");

                    MensajeEstado = $"Mayor descargado: {nombreArchivo}";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"MayorCuentaViewModel: Error response = {errorContent}");
                    _dialogService.ShowError($"Error al generar el Mayor: {response.StatusCode}\n{errorContent}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MayorCuentaViewModel: Exception = {ex}");
                _dialogService.ShowError($"Error al generar el Mayor: {ex.Message}");
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        #endregion

        #region Metodos auxiliares

        [DllImport("shell32.dll")]
        private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);

        private static string ObtenerRutaDescargas()
        {
            var downloadsFolderGuid = new Guid("374DE290-123F-4565-9164-39C4925E467B");
            SHGetKnownFolderPath(downloadsFolderGuid, 0, IntPtr.Zero, out IntPtr pathPtr);
            var path = Marshal.PtrToStringUni(pathPtr);
            Marshal.FreeCoTaskMem(pathPtr);
            return path;
        }

        #endregion

        #region INavigationAware

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            Debug.WriteLine("MayorCuentaViewModel: OnNavigatedTo");
            NumeroCuenta = null;
            MensajeEstado = null;
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext) { }

        #endregion
    }
}
