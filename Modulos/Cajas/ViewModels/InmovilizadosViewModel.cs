using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.ViewModels
{
    /// <summary>
    /// Inmovilizados (Carlos 25/11/26): tras el inventario se inmovilizan máquinas y hay que pegarles una etiqueta
    /// («INMOVILIZADO · NO ES STOCK», su código «I/1823» y el código de barras con ese mismo texto). Se buscan (número, «I/1823»
    /// o descripción), se eligen una o varias y se imprime su etiqueta (POST api/Inmovilizados/Etiquetas/Imprimir). Sin impresora,
    /// la API elige la de la delegación del inmovilizado.
    /// </summary>
    public class InmovilizadosViewModel : ObservableObject, IReceptorNavegacion
    {
        internal const string TITULO = "Etiquetas de inmovilizado";
        internal const string TODAS = "Todas";
        internal const int COPIAS_MAXIMAS = 10;
        /// <summary>Pausa al escribir en el buscador antes de buscar solo (Intro busca en el acto).</summary>
        internal int MilisegundosPausaBusqueda { get; set; } = 500;

        private static readonly CultureInfo Espanol = new CultureInfo("es-ES");

        private readonly IInmovilizadosService _servicio;
        private readonly IServicioDialogos _dialogos;
        private CancellationTokenSource? _pausa;
        private int _busquedaEnCurso;

        public InmovilizadosViewModel(IInmovilizadosService servicio, IServicioDialogos dialogos)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            Titulo = "Inmovilizados";
            BuscarCommand = new AsyncRelayCommand(BuscarAsync);
            ImprimirCommand = new AsyncRelayCommand(ImprimirAsync, PuedeImprimir);
        }

        public string Titulo { get; }

        public List<string> Delegaciones { get; } = new List<string> { TODAS, "ALG", "REI", "ALC", "VAR" };

        public List<int> OpcionesCopias { get; } = Enumerable.Range(1, COPIAS_MAXIMAS).ToList();

        private string? _filtro;
        /// <summary>Número, «I/1823» o parte de la descripción. Al escribir, se busca tras una pausa.</summary>
        public string? Filtro
        {
            get => _filtro;
            set
            {
                if (SetProperty(ref _filtro, value))
                {
                    _ = BuscarTrasPausaAsync();
                }
            }
        }

        private string _delegacion = TODAS;
        public string Delegacion
        {
            get => _delegacion;
            set
            {
                if (SetProperty(ref _delegacion, string.IsNullOrWhiteSpace(value) ? TODAS : value) && _haBuscado)
                {
                    _ = BuscarAsync();
                }
            }
        }

        private bool _verAnulados;
        /// <summary>«Ver también los anulados» (soloVivos = false).</summary>
        public bool VerAnulados
        {
            get => _verAnulados;
            set
            {
                if (SetProperty(ref _verAnulados, value) && _haBuscado)
                {
                    _ = BuscarAsync();
                }
            }
        }

        private int _copias = 1;
        public int Copias
        {
            get => _copias;
            set => SetProperty(ref _copias, Math.Max(1, Math.Min(COPIAS_MAXIMAS, value)));
        }

        private ObservableCollection<InmovilizadoModel> _inmovilizados = new ObservableCollection<InmovilizadoModel>();
        public ObservableCollection<InmovilizadoModel> Inmovilizados
        {
            get => _inmovilizados;
            private set => SetProperty(ref _inmovilizados, value);
        }

        private List<InmovilizadoModel> _seleccionados = new List<InmovilizadoModel>();
        /// <summary>Los elegidos en la lista (selección múltiple: la vista los pasa con <see cref="CambiarSeleccion"/>).</summary>
        public IReadOnlyList<InmovilizadoModel> Seleccionados => _seleccionados;

        public void CambiarSeleccion(IEnumerable<InmovilizadoModel>? seleccionados)
        {
            _seleccionados = seleccionados?.Where(s => s != null).Distinct().ToList() ?? new List<InmovilizadoModel>();
            OnPropertyChanged(nameof(Seleccionados));
            OnPropertyChanged(nameof(TextoSeleccion));
            ImprimirCommand.NotifyCanExecuteChanged();
        }

        public string TextoSeleccion => _seleccionados.Count switch
        {
            0 => "Elija uno o varios inmovilizados de la lista.",
            1 => "1 inmovilizado elegido.",
            _ => $"{_seleccionados.Count} inmovilizados elegidos."
        };

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    ImprimirCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private bool _haBuscado;

        private string _resumen = string.Empty;
        /// <summary>Cuántos salen (tras buscar) o lo impreso (tras imprimir).</summary>
        public string Resumen
        {
            get => _resumen;
            private set => SetProperty(ref _resumen, value);
        }

        public IAsyncRelayCommand BuscarCommand { get; }

        private async Task BuscarTrasPausaAsync()
        {
            _pausa?.Cancel();
            var pausa = new CancellationTokenSource();
            _pausa = pausa;
            try
            {
                await Task.Delay(MilisegundosPausaBusqueda, pausa.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
            if (!pausa.IsCancellationRequested)
            {
                await BuscarAsync();
            }
        }

        public async Task BuscarAsync()
        {
            _pausa?.Cancel();
            // Si se cruzan dos búsquedas (pausa e Intro), solo cuenta la última.
            int esta = Interlocked.Increment(ref _busquedaEnCurso);
            try
            {
                EstaOcupado = true;
                string? delegacion = string.Equals(Delegacion, TODAS, StringComparison.OrdinalIgnoreCase) ? null : Delegacion;
                List<InmovilizadoModel> lista = await _servicio.Buscar(Constantes.Empresas.EMPRESA_DEFECTO, Filtro?.Trim(), delegacion, !VerAnulados);
                if (esta != _busquedaEnCurso)
                {
                    return;
                }
                _haBuscado = true;
                Inmovilizados = new ObservableCollection<InmovilizadoModel>(lista ?? new List<InmovilizadoModel>());
                CambiarSeleccion(null);
                Resumen = Inmovilizados.Count switch
                {
                    0 => "No hay inmovilizados con esos filtros.",
                    1 => "1 inmovilizado.",
                    _ => $"{Inmovilizados.Count} inmovilizados."
                };
            }
            catch (Exception ex)
            {
                if (esta == _busquedaEnCurso)
                {
                    _dialogos.ShowError(ex.Message);
                }
            }
            finally
            {
                if (esta == _busquedaEnCurso)
                {
                    EstaOcupado = false;
                }
            }
        }

        public IAsyncRelayCommand ImprimirCommand { get; }

        private bool PuedeImprimir() => _seleccionados.Count > 0 && !EstaOcupado;

        public async Task ImprimirAsync()
        {
            if (!PuedeImprimir())
            {
                return;
            }
            var peticion = new PeticionImprimirEtiquetasInmovilizado
            {
                Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                Numeros = _seleccionados.Select(s => s.Numero?.Trim()).Where(n => !string.IsNullOrEmpty(n)).Select(n => n!).Distinct().ToList(),
                Copias = Copias,
                Impresora = null
            };
            if (peticion.Numeros.Count == 0)
            {
                return;
            }
            try
            {
                EstaOcupado = true;
                RespuestaImprimirEtiquetasInmovilizado respuesta = await _servicio.ImprimirEtiquetas(peticion);
                string texto = ResumenImpresion(respuesta, Copias);
                Resumen = texto;
                _dialogos.ShowNotification(TITULO, texto);
            }
            catch (Exception ex)
            {
                _dialogos.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        /// <summary>
        /// LA FRASE (pura) de lo impreso: «2 etiquetas enviadas a \\RDS2016\etiquetasreina» por impresora y, de las que no
        /// salieron, cuál y por qué («No ha salido la I/1823 (COMPRESOR): sin impresora para VAR.»).
        /// </summary>
        internal static string ResumenImpresion(RespuestaImprimirEtiquetasInmovilizado? respuesta, int copias)
        {
            List<EtiquetaInmovilizadoImpresa> etiquetas = respuesta?.Etiquetas?.Where(e => e != null).ToList()
                ?? new List<EtiquetaInmovilizadoImpresa>();
            var lineas = new List<string>();

            string deCadaUna = copias > 1 ? $" ({copias} copias de cada una)" : string.Empty;
            foreach (var grupo in etiquetas.Where(e => e.Ok).GroupBy(e => e.Impresora?.Trim() ?? string.Empty))
            {
                int cuantas = grupo.Count();
                string que = cuantas == 1 ? "1 etiqueta enviada" : $"{cuantas.ToString("N0", Espanol)} etiquetas enviadas";
                lineas.Add(string.IsNullOrEmpty(grupo.Key) ? $"{que}{deCadaUna} a la impresora." : $"{que}{deCadaUna} a {grupo.Key}");
            }

            foreach (var fallo in etiquetas.Where(e => !e.Ok))
            {
                string codigo = !string.IsNullOrWhiteSpace(fallo.Codigo) ? fallo.Codigo!.Trim() : CodigoInmovilizado.Formatear(fallo.Numero);
                string descripcion = string.IsNullOrWhiteSpace(fallo.Descripcion) ? string.Empty : $" ({fallo.Descripcion!.Trim()})";
                string motivo = string.IsNullOrWhiteSpace(fallo.Motivo) ? "sin motivo." : fallo.Motivo!.Trim();
                lineas.Add($"No ha salido la {codigo}{descripcion}: {motivo}");
            }

            if (lineas.Count == 0)
            {
                int impresas = respuesta?.Impresas ?? 0;
                return impresas > 0
                    ? (impresas == 1 ? "1 etiqueta enviada a la impresora." : $"{impresas.ToString("N0", Espanol)} etiquetas enviadas a la impresora.")
                    : "No se ha impreso ninguna etiqueta.";
            }
            return string.Join(Environment.NewLine, lineas);
        }

        private bool _cargadoAlAbrir;

        public void AlLlegar(ParametrosNavegacion parametros)
        {
            if (!_cargadoAlAbrir)
            {
                _cargadoAlAbrir = true;
                _ = BuscarAsync();
            }
        }
    }
}
