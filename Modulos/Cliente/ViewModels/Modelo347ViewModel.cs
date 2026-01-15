using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static Nesto.Infrastructure.Shared.Constantes;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// ViewModel para la vista de descarga del certificado Modelo 347.
    /// Issue #270: Nueva vista Clientes -> Modelo 347 para descargar certificado.
    /// </summary>
    public class Modelo347ViewModel : BindableBase, INavigationAware
    {
        private readonly IConfiguracion _configuracion;
        private readonly IDialogService _dialogService;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public Modelo347ViewModel(
            IConfiguracion configuracion,
            IDialogService dialogService,
            IServicioAutenticacion servicioAutenticacion)
        {
            _configuracion = configuracion ?? throw new ArgumentNullException(nameof(configuracion));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _servicioAutenticacion = servicioAutenticacion ?? throw new ArgumentNullException(nameof(servicioAutenticacion));

            Debug.WriteLine("Modelo347ViewModel: Constructor iniciado");

            Titulo = "Modelo 347";
            DescargarPdfCommand = new DelegateCommand(OnDescargarPdfSync, CanDescargarPdf);

            // Inicializar lista de ejercicios (últimos 3 años)
            var annoActual = DateTime.Today.Year;
            Ejercicios = new ObservableCollection<int> { annoActual - 1, annoActual - 2, annoActual - 3 };

            // Por defecto: año anterior si estamos en enero/febrero, año actual si estamos de marzo en adelante
            // Pero para el 347 siempre se declara el año anterior, así que:
            // En enero-febrero se declara el año anterior (ej: en feb 2026 se declara 2025)
            // De marzo en adelante también se puede necesitar el año anterior
            EjercicioSeleccionado = annoActual - 1;

            Debug.WriteLine($"Modelo347ViewModel: Ejercicios inicializados. Seleccionado: {EjercicioSeleccionado}");
        }

        #region Propiedades

        public string Titulo { get; private set; }

        private string _clienteSeleccionado;
        public string ClienteSeleccionado
        {
            get => _clienteSeleccionado;
            set
            {
                Debug.WriteLine($"Modelo347ViewModel: ClienteSeleccionado cambiado a '{value}'");
                if (SetProperty(ref _clienteSeleccionado, value))
                {
                    DescargarPdfCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private int _ejercicioSeleccionado;
        public int EjercicioSeleccionado
        {
            get => _ejercicioSeleccionado;
            set
            {
                Debug.WriteLine($"Modelo347ViewModel: EjercicioSeleccionado cambiado a {value}");
                SetProperty(ref _ejercicioSeleccionado, value);
            }
        }

        public ObservableCollection<int> Ejercicios { get; }

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

        public DelegateCommand DescargarPdfCommand { get; }

        private bool CanDescargarPdf()
        {
            var canExecute = !string.IsNullOrWhiteSpace(ClienteSeleccionado) && EjercicioSeleccionado > 0;
            Debug.WriteLine($"Modelo347ViewModel: CanDescargarPdf = {canExecute} (Cliente: '{ClienteSeleccionado}', Ejercicio: {EjercicioSeleccionado})");
            return canExecute;
        }

        private void OnDescargarPdfSync()
        {
            Debug.WriteLine("Modelo347ViewModel: OnDescargarPdfSync iniciado");
            _ = OnDescargarPdfAsync();
        }

        private async Task OnDescargarPdfAsync()
        {
            try
            {
                Debug.WriteLine("Modelo347ViewModel: OnDescargarPdfAsync iniciado");
                EstaOcupado = true;
                MensajeEstado = "Descargando certificado...";

                using var client = new HttpClient();
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                client.Timeout = TimeSpan.FromSeconds(60);

                Debug.WriteLine($"Modelo347ViewModel: BaseAddress = {_configuracion.servidorAPI}");

                // Configurar autorización JWT
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    Debug.WriteLine("Modelo347ViewModel: Error configurando autorización");
                    _dialogService.ShowError("No se pudo configurar la autorización. Por favor, reinicie la aplicación.");
                    return;
                }

                // Construir URL
                var url = $"ExtractosCliente/Modelo347Pdf?empresa={Empresas.EMPRESA_DEFECTO}&cliente={ClienteSeleccionado}&anno={EjercicioSeleccionado}";
                Debug.WriteLine($"Modelo347ViewModel: URL = {url}");

                var response = await client.GetAsync(url);
                Debug.WriteLine($"Modelo347ViewModel: Response status = {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var pdfBytes = await response.Content.ReadAsByteArrayAsync();
                    Debug.WriteLine($"Modelo347ViewModel: PDF recibido, {pdfBytes?.Length ?? 0} bytes");

                    if (pdfBytes == null || pdfBytes.Length == 0)
                    {
                        _dialogService.ShowError("El servidor devolvió un PDF vacío.");
                        return;
                    }

                    // Guardar en carpeta Descargas
                    var rutaDescargas = ObtenerRutaDescargas();
                    var nombreArchivo = $"Modelo347_{ClienteSeleccionado}_{EjercicioSeleccionado}.pdf";
                    var rutaCompleta = Path.Combine(rutaDescargas, nombreArchivo);

                    Debug.WriteLine($"Modelo347ViewModel: Guardando en {rutaCompleta}");
                    await File.WriteAllBytesAsync(rutaCompleta, pdfBytes);

                    // Abrir la carpeta con el archivo seleccionado
                    Process.Start("explorer.exe", $"/select,\"{rutaCompleta}\"");

                    MensajeEstado = $"Certificado descargado: {nombreArchivo}";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"Modelo347ViewModel: Error response = {errorContent}");
                    _dialogService.ShowError($"Error al descargar el certificado: {response.StatusCode}\n{errorContent}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Modelo347ViewModel: Exception = {ex}");
                _dialogService.ShowError($"Error al descargar el certificado: {ex.Message}");
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        #endregion

        #region Métodos auxiliares

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
            Debug.WriteLine("Modelo347ViewModel: OnNavigatedTo");
            ClienteSeleccionado = null;
            MensajeEstado = null;
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext) { }

        #endregion
    }
}
