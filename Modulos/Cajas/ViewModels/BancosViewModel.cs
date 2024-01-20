using ControlesUsuario.Dialogs;
using Microsoft.Win32;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Prism.Commands;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Nesto.Modulos.Cajas.ViewModels
{
    public class BancosViewModel : ViewModelBase
    {
        private readonly IBancosService _bancosService;
        private readonly IConfiguracion _configuracion;
        private readonly IDialogService _dialogService;
        

        public BancosViewModel(IBancosService bancosService, IConfiguracion configuracion, IDialogService dialogService)
        {
            _bancosService = bancosService;
            _configuracion = configuracion;
            _dialogService = dialogService;

            CargarArchivoCommand = new DelegateCommand(OnCargarArchivo);

            Titulo = "Bancos";
        }

        public ICommand CargarArchivoCommand { get; private set; }
        private async void OnCargarArchivo()
        {
            var ruta = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.PathNorma43);
            // Configura el diálogo para abrir ficheros
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = ruta;
            openFileDialog.Filter = "Archivos de texto (*.txt)|*.txt|Todos los archivos (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                // Obtiene la ruta del fichero seleccionado
                string filePath = openFileDialog.FileName;

                try
                {
                    // Lee el contenido del fichero
                    string fileContent = File.ReadAllText(filePath);

                    var movimientosDia = await _bancosService.CargarFicheroCuaderno43(fileContent);
                }
                catch (Exception ex)
                {
                    _dialogService.ShowError("Error al leer el fichero: " + ex.Message);
                }
            }
        }
    }
}
