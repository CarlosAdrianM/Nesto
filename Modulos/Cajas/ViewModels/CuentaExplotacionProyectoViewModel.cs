using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlesUsuario.Services;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.ViewModels
{
    /// <summary>
    /// Proyectos (gemela de NestoAPI#901), pestaña «Cuenta de explotación»: ingresos y gastos por cuenta de un proyecto, totales,
    /// resultado y margen, presupuesto frente a real y el detalle; su Excel y la comparativa con los demás proyectos del mismo producto.
    /// </summary>
    public class CuentaExplotacionProyectoViewModel : ObservableObject
    {
        internal const string FILTRO_EXCEL = "Libro de Excel (*.xlsx)|*.xlsx";

        private readonly IProyectosService _servicio;
        private readonly IServicioDialogos _dialogos;
        private readonly IGuardadorFicherosDescarga? _guardador;
        private readonly Action<string> _abrir;
        private readonly string _empresa;
        private int _lecturaEnCurso;

        public CuentaExplotacionProyectoViewModel(IProyectosService servicio, IServicioDialogos dialogos, IGuardadorFicherosDescarga? guardador,
            string empresa, Action<string>? abrir = null)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            _guardador = guardador;
            _empresa = empresa;
            _abrir = abrir ?? AbrirConElProgramaDelSistema;
            ExcelCommand = new AsyncRelayCommand(ExcelAsync, () => Cuenta?.Proyecto != null && !EstaOcupado && _guardador != null);
            CompararCommand = new AsyncRelayCommand(CompararAsync, () => ProyectoElegido != null && !EstaOcupado);
            RecargarCommand = new AsyncRelayCommand(CargarCuentaAsync, () => ProyectoElegido != null && !EstaOcupado);
        }

        private static void AbrirConElProgramaDelSistema(string ruta)
            => _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true });

        /// <summary>Todos los proyectos (abiertos y cerrados) para elegir. Los pone la ventana.</summary>
        public ObservableCollection<ProyectoModel> Proyectos { get; } = new ObservableCollection<ProyectoModel>();

        public void PonerProyectos(IEnumerable<ProyectoModel>? proyectos)
        {
            int? elegido = ProyectoElegido?.Id;
            Proyectos.Clear();
            foreach (ProyectoModel p in proyectos?.Where(p => p != null) ?? Enumerable.Empty<ProyectoModel>())
            {
                Proyectos.Add(p);
            }
            if (elegido.HasValue)
            {
                _proyectoElegido = Proyectos.FirstOrDefault(p => p.Id == elegido.Value) ?? _proyectoElegido;
                OnPropertyChanged(nameof(ProyectoElegido));
            }
        }

        private ProyectoModel? _proyectoElegido;
        public ProyectoModel? ProyectoElegido
        {
            get => _proyectoElegido;
            set
            {
                if (SetProperty(ref _proyectoElegido, value))
                {
                    Comparativa = null;
                    NotificarComandos();
                    if (value != null)
                    {
                        _ = CargarCuentaAsync();
                    }
                    else
                    {
                        Cuenta = null;
                    }
                }
            }
        }

        /// <summary>Elige el proyecto (por su Id) y carga su cuenta; si no está en la lista, lo añade.</summary>
        public async Task ElegirAsync(ProyectoModel proyecto)
        {
            if (proyecto == null)
            {
                return;
            }
            ProyectoModel? enLista = Proyectos.FirstOrDefault(p => p.Id == proyecto.Id);
            if (enLista == null)
            {
                Proyectos.Add(proyecto);
                enLista = proyecto;
            }
            if (ReferenceEquals(_proyectoElegido, enLista))
            {
                await CargarCuentaAsync();
                return;
            }
            _proyectoElegido = enLista;
            OnPropertyChanged(nameof(ProyectoElegido));
            Comparativa = null;
            NotificarComandos();
            await CargarCuentaAsync();
        }

        private CuentaExplotacionProyectoModel? _cuenta;
        public CuentaExplotacionProyectoModel? Cuenta
        {
            get => _cuenta;
            private set
            {
                if (SetProperty(ref _cuenta, value))
                {
                    OnPropertyChanged(nameof(HayCuenta));
                    OnPropertyChanged(nameof(TextoTotales));
                    OnPropertyChanged(nameof(TextoMargen));
                    OnPropertyChanged(nameof(HayPresupuesto));
                    OnPropertyChanged(nameof(LineasPresupuesto));
                    NotificarComandos();
                }
            }
        }

        public bool HayCuenta => Cuenta != null;

        /// <summary>«Ingresos 1.000,00 € · Gastos 650,00 € · Resultado 350,00 €».</summary>
        public string TextoTotales => Cuenta == null
            ? string.Empty
            : $"Ingresos {ReglasPantallaProyectos.Euros(Cuenta.TotalIngresos)} · Gastos {ReglasPantallaProyectos.Euros(Cuenta.TotalGastos)} · " +
              $"Resultado {ReglasPantallaProyectos.Euros(Cuenta.Resultado)}";

        public string TextoMargen => Cuenta == null ? string.Empty : "Margen sobre ingresos: " + ReglasPantallaProyectos.TextoMargen(Cuenta.Margen);

        public bool HayPresupuesto => Cuenta?.Presupuesto != null;

        /// <summary>Presupuesto frente a real en tres filas (Ingresos, Gastos, Resultado).</summary>
        public List<LineaPresupuestoProyecto> LineasPresupuesto => Cuenta?.Presupuesto == null
            ? new List<LineaPresupuestoProyecto>()
            : LineaPresupuestoProyecto.Construir(Cuenta);

        private DataView? _comparativa;
        /// <summary>La comparativa (columnas P0, P1… con el código del proyecto en Caption).</summary>
        public DataView? Comparativa
        {
            get => _comparativa;
            private set
            {
                if (SetProperty(ref _comparativa, value))
                {
                    OnPropertyChanged(nameof(HayComparativa));
                }
            }
        }

        public bool HayComparativa => Comparativa != null;

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    NotificarComandos();
                }
            }
        }

        private string _mensaje = string.Empty;
        public string Mensaje { get => _mensaje; private set => SetProperty(ref _mensaje, value); }

        public IAsyncRelayCommand RecargarCommand { get; }

        public async Task CargarCuentaAsync()
        {
            ProyectoModel? proyecto = ProyectoElegido;
            if (proyecto == null)
            {
                return;
            }
            int esta = System.Threading.Interlocked.Increment(ref _lecturaEnCurso);
            try
            {
                EstaOcupado = true;
                CuentaExplotacionProyectoModel cuenta = await _servicio.CuentaExplotacion(proyecto.Id);
                if (esta != _lecturaEnCurso)
                {
                    return;
                }
                Cuenta = cuenta;
                Mensaje = cuenta.Detalle?.Count > 0 ? string.Empty : "Este proyecto todavía no tiene nada imputado.";
            }
            catch (Exception ex)
            {
                if (esta == _lecturaEnCurso)
                {
                    Cuenta = null;
                    _dialogos.ShowError(ex.Message);
                }
            }
            finally
            {
                if (esta == _lecturaEnCurso)
                {
                    EstaOcupado = false;
                }
            }
        }

        public IAsyncRelayCommand ExcelCommand { get; }

        public async Task ExcelAsync()
        {
            ProyectoModel? proyecto = Cuenta?.Proyecto ?? ProyectoElegido;
            if (proyecto == null || _guardador == null)
            {
                return;
            }
            try
            {
                EstaOcupado = true;
                FicheroProyecto fichero = await _servicio.CuentaExplotacionExcel(proyecto.Id);
                string nombre = NombreExcel(fichero?.Nombre, proyecto.Codigo);
                string? ruta = _guardador.ElegirDondeGuardar("Guardar la cuenta de explotación", nombre, GuardadorRutas.CarpetaDescargas(), FILTRO_EXCEL, ".xlsx");
                if (string.IsNullOrWhiteSpace(ruta))
                {
                    Mensaje = "No se ha guardado el Excel.";
                    return;
                }
                _guardador.Guardar(ruta, fichero?.Contenido);
                Mensaje = $"Excel guardado en {ruta}.";
                try
                {
                    _abrir(ruta);
                }
                catch (Exception ex)
                {
                    Mensaje += $" No se ha podido abrir: {ex.Message}";
                }
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

        /// <summary>El nombre que da la API o, si no viene, «Cuenta de explotación CODIGO.xlsx» sin caracteres que no valgan en un fichero.</summary>
        internal static string NombreExcel(string? nombreApi, string? codigo)
        {
            if (!string.IsNullOrWhiteSpace(nombreApi))
            {
                return Path.GetFileName(nombreApi.Trim());
            }
            string nombre = "Cuenta de explotación " + (codigo?.Trim() ?? string.Empty);
            foreach (char invalido in Path.GetInvalidFileNameChars())
            {
                nombre = nombre.Replace(invalido, '_');
            }
            return nombre.Trim() + ".xlsx";
        }

        public IAsyncRelayCommand CompararCommand { get; }

        /// <summary>«Comparar con…»: los proyectos del mismo producto lado a lado (GET api/Proyectos/Comparativa?producto=).</summary>
        public async Task CompararAsync()
        {
            ProyectoModel? proyecto = Cuenta?.Proyecto ?? ProyectoElegido;
            if (proyecto == null)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(proyecto.Producto))
            {
                _dialogos.ShowNotification(ApuntesProyectosViewModel.TITULO,
                    $"El proyecto {proyecto.Codigo?.Trim()} no tiene producto: póngaselo (pestaña «Proyectos», Editar) para compararlo con los demás de ese curso.");
                return;
            }
            try
            {
                EstaOcupado = true;
                ComparativaProyectosModel comparativa = await _servicio.Comparativa(null, proyecto.Producto.Trim(), _empresa);
                if (comparativa?.Proyectos == null || comparativa.Proyectos.Count < 2)
                {
                    Comparativa = null;
                    _dialogos.ShowNotification(ApuntesProyectosViewModel.TITULO,
                        $"No hay otros proyectos del producto {proyecto.Producto.Trim()} con los que comparar.");
                    return;
                }
                Comparativa = ReglasPantallaProyectos.TablaComparativa(comparativa).DefaultView;
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

        private void NotificarComandos()
        {
            ExcelCommand.NotifyCanExecuteChanged();
            CompararCommand.NotifyCanExecuteChanged();
            RecargarCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>La carpeta Descargas del usuario (en el escritorio remoto, la suya del servidor).</summary>
    internal static class GuardadorRutas
    {
        internal static string CarpetaDescargas()
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    /// <summary>Una fila de «presupuesto frente a real».</summary>
    public class LineaPresupuestoProyecto
    {
        public string Concepto { get; set; } = string.Empty;
        public string Presupuesto { get; set; } = string.Empty;
        public string Real { get; set; } = string.Empty;
        public string Desviacion { get; set; } = string.Empty;

        internal static List<LineaPresupuestoProyecto> Construir(CuentaExplotacionProyectoModel cuenta)
        {
            PresupuestoFrenteARealModel p = cuenta.Presupuesto!;
            static string Euros(decimal? importe) => importe.HasValue ? ReglasPantallaProyectos.Euros(importe.Value) : "—";
            return new List<LineaPresupuestoProyecto>
            {
                new LineaPresupuestoProyecto { Concepto = "Ingresos", Presupuesto = Euros(p.PresupuestoIngresos), Real = Euros(cuenta.TotalIngresos), Desviacion = Euros(p.DesviacionIngresos) },
                new LineaPresupuestoProyecto { Concepto = "Gastos", Presupuesto = Euros(p.PresupuestoGastos), Real = Euros(cuenta.TotalGastos), Desviacion = Euros(p.DesviacionGastos) },
                new LineaPresupuestoProyecto { Concepto = "Resultado", Presupuesto = Euros(p.PresupuestoResultado), Real = Euros(cuenta.Resultado), Desviacion = Euros(p.DesviacionResultado) }
            };
        }
    }
}
