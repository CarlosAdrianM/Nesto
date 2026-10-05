using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto.ViewModels
{
    /// <summary>
    /// Etiquetas de hueco (30×20 mm: «Pasillo / Fila / Columna» y el código PPPFFFCCC que lee Ariadna). Se piden por un
    /// rango de un pasillo o por huecos sueltos; «Ver» enseña cuáles saldrían y por qué impresora, e «Imprimir» las
    /// manda (con confirmación si son muchas). Quien imprime es la API, por la impresora de etiquetas de producto del
    /// usuario (ImpresoraCodBarras): las mismas etiquetas que pide Ariadna al ubicar.
    /// </summary>
    public class EtiquetasHuecoViewModel : ObservableObject
    {
        /// <summary>A partir de cuántas etiquetas se pide confirmación antes de imprimir.</summary>
        public const int MAXIMO_SIN_CONFIRMAR = 20;

        /// <summary>La etiqueta de prueba, para ajustar tamaño y posición en la impresora.</summary>
        public const string HUECO_DE_PRUEBA = "002004001";

        private readonly IServicioEtiquetasHueco _servicio;
        private readonly IServicioDialogos _dialogos;

        public EtiquetasHuecoViewModel(IServicioEtiquetasHueco servicio, IServicioDialogos dialogos)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            VerCommand = new AsyncRelayCommand(VerAsync);
            ImprimirCommand = new AsyncRelayCommand(ImprimirAsync);
            PruebaCommand = new AsyncRelayCommand(PruebaAsync);
        }

        public string Titulo => "Etiquetas de hueco";

        public string Empresa { get; set; } = Constantes.Empresas.EMPRESA_DEFECTO;
        public string Almacen { get; set; } = Constantes.Almacenes.ALMACEN_ALGETE;

        private bool _modoSueltos;
        /// <summary>False: un rango de un pasillo. True: huecos sueltos, uno por línea.</summary>
        public bool ModoSueltos
        {
            get => _modoSueltos;
            set
            {
                if (SetProperty(ref _modoSueltos, value))
                {
                    OnPropertyChanged(nameof(ModoRango));
                }
            }
        }

        public bool ModoRango
        {
            get => !ModoSueltos;
            set => ModoSueltos = !value;
        }

        private string _pasillo;
        public string Pasillo { get => _pasillo; set => SetProperty(ref _pasillo, value); }

        private string _filaDesde = "001";
        public string FilaDesde { get => _filaDesde; set => SetProperty(ref _filaDesde, value); }

        private string _filaHasta = "001";
        public string FilaHasta { get => _filaHasta; set => SetProperty(ref _filaHasta, value); }

        private string _columnaDesde = "001";
        public string ColumnaDesde { get => _columnaDesde; set => SetProperty(ref _columnaDesde, value); }

        private string _columnaHasta = "001";
        public string ColumnaHasta { get => _columnaHasta; set => SetProperty(ref _columnaHasta, value); }

        private bool _soloEnUso;
        /// <summary>Del rango, solo los huecos que tienen algo ubicado.</summary>
        public bool SoloEnUso { get => _soloEnUso; set => SetProperty(ref _soloEnUso, value); }

        private string _huecosSueltos;
        /// <summary>Uno por línea, como 002004001 o 002/004/001 (se pueden leer con el lector).</summary>
        public string HuecosSueltos { get => _huecosSueltos; set => SetProperty(ref _huecosSueltos, value); }

        /// <summary>Los huecos de la última consulta, como en la etiqueta: 002/004/001.</summary>
        public ObservableCollection<string> Huecos { get; } = new ObservableCollection<string>();

        private string _mensaje;
        public string Mensaje { get => _mensaje; set => SetProperty(ref _mensaje, value); }

        private bool _estaOcupado;
        public bool EstaOcupado { get => _estaOcupado; set => SetProperty(ref _estaOcupado, value); }

        public IAsyncRelayCommand VerCommand { get; }
        public IAsyncRelayCommand ImprimirCommand { get; }
        public IAsyncRelayCommand PruebaCommand { get; }

        private async Task VerAsync()
        {
            PeticionEtiquetasHueco peticion = Peticion();
            if (peticion == null)
            {
                return;
            }
            await LlamarAsync(peticion, ensayo: true).ConfigureAwait(true);
        }

        private async Task ImprimirAsync()
        {
            PeticionEtiquetasHueco peticion = Peticion();
            if (peticion == null)
            {
                return;
            }
            // Primero el ensayo: así se sabe cuántas son antes de gastar etiquetas
            ResultadoEtiquetasHueco previa = await LlamarAsync(peticion, ensayo: true).ConfigureAwait(true);
            if (previa == null || previa.Huecos.Count == 0)
            {
                return;
            }
            if (previa.Huecos.Count > MAXIMO_SIN_CONFIRMAR &&
                !await _dialogos.ShowConfirmationAsync("Etiquetas de hueco",
                    $"Vas a imprimir {previa.Huecos.Count} etiquetas por {previa.Impresora}. ¿Seguimos?").ConfigureAwait(true))
            {
                return;
            }
            _ = await LlamarAsync(peticion, ensayo: false).ConfigureAwait(true);
        }

        private Task PruebaAsync()
            => LlamarAsync(new PeticionEtiquetasHueco { Huecos = new List<string> { HUECO_DE_PRUEBA } }, ensayo: false);

        /// <summary>La petición según el modo; null (con el motivo en <see cref="Mensaje"/>) si falta algo.</summary>
        private PeticionEtiquetasHueco Peticion()
        {
            if (ModoSueltos)
            {
                List<string> huecos = (HuecosSueltos ?? string.Empty)
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(h => h.Trim())
                    .Where(h => h.Length > 0)
                    .ToList();
                if (huecos.Count == 0)
                {
                    Mensaje = "Escribe (o lee) algún hueco, uno por línea.";
                    return null;
                }
                return new PeticionEtiquetasHueco { Huecos = huecos };
            }
            if (string.IsNullOrWhiteSpace(Pasillo))
            {
                Mensaje = "Falta el pasillo.";
                return null;
            }
            return new PeticionEtiquetasHueco
            {
                Pasillo = TresCifras(Pasillo),
                FilaDesde = TresCifras(FilaDesde),
                FilaHasta = TresCifras(FilaHasta),
                ColumnaDesde = TresCifras(ColumnaDesde),
                ColumnaHasta = TresCifras(ColumnaHasta),
                SoloEnUso = SoloEnUso
            };
        }

        /// <summary>«2» → «002». Lo que no es un número se manda tal cual: la API dice qué está mal.</summary>
        private static string TresCifras(string valor)
        {
            string limpio = (valor ?? string.Empty).Trim();
            return limpio.Length > 0 && limpio.Length < 3 && limpio.All(char.IsDigit) ? limpio.PadLeft(3, '0') : limpio;
        }

        private async Task<ResultadoEtiquetasHueco> LlamarAsync(PeticionEtiquetasHueco peticion, bool ensayo)
        {
            EstaOcupado = true;
            try
            {
                ResultadoEtiquetasHueco resultado = await _servicio.Imprimir(Empresa, Almacen, peticion, ensayo).ConfigureAwait(true);
                Huecos.Clear();
                foreach (string hueco in resultado.Huecos ?? new List<string>())
                {
                    Huecos.Add(ConBarras(hueco));
                }
                Mensaje = string.IsNullOrWhiteSpace(resultado.Impresora)
                    ? resultado.Mensaje
                    : $"{resultado.Mensaje} (impresora {resultado.Impresora})";
                return resultado;
            }
            catch (EtiquetasHuecoException ex)
            {
                Mensaje = ex.Message;
                _dialogos.ShowError(ex.Message);
                return null;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                Mensaje = "No se puede conectar con el servidor.";
                _dialogos.ShowError("No se puede conectar con el servidor para las etiquetas de hueco: " + ex.Message);
                return null;
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        /// <summary>002004001 → 002/004/001, como sale en la etiqueta.</summary>
        private static string ConBarras(string hueco)
            => hueco != null && hueco.Length == 9 && hueco.All(char.IsDigit)
                ? $"{hueco.Substring(0, 3)}/{hueco.Substring(3, 3)}/{hueco.Substring(6, 3)}"
                : hueco;
    }
}
