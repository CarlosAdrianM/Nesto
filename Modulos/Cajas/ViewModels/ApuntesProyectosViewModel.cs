using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.ViewModels
{
    /// <summary>
    /// Proyectos (gemela de NestoAPI#901), pestaña «Apuntes por imputar»: los apuntes de las cuentas 6 y 7 con lo imputado y lo
    /// pendiente; al elegir uno, su reparto entre proyectos (el PUT SUSTITUYE el reparto entero); con varios elegidos, imputarlos
    /// enteros a un proyecto (un PUT por apunte).
    /// </summary>
    public class ApuntesProyectosViewModel : ObservableObject
    {
        internal const string TITULO = "Proyectos";

        private readonly IProyectosService _servicio;
        private readonly IServicioDialogos _dialogos;
        private readonly string _empresa;
        private int _pagina;

        public ApuntesProyectosViewModel(IProyectosService servicio, IServicioDialogos dialogos, string empresa, Func<DateTime>? hoy = null)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            _empresa = empresa;
            DateTime dia = (hoy ?? (() => DateTime.Today))().Date;
            _desde = new DateTime(dia.Year, 1, 1);
            _hasta = dia;

            BuscarCommand = new AsyncRelayCommand(BuscarAsync, () => !EstaOcupado);
            CargarMasCommand = new AsyncRelayCommand(CargarMasAsync, () => !EstaOcupado && HayMas);
            AnadirFilaCommand = new RelayCommand(AnadirFila, () => ApunteEnReparto != null && !EstaOcupado);
            QuitarFilaCommand = new RelayCommand<FilaRepartoProyecto>(QuitarFila, f => f != null && !f.ProyectoCerrado && !EstaOcupado);
            TodoAEsteProyectoCommand = new RelayCommand<FilaRepartoProyecto>(TodoAEsteProyecto, f => f != null && f.ProyectoId.HasValue && !f.ProyectoCerrado && !EstaOcupado);
            RepartirAPartesIgualesCommand = new RelayCommand(RepartirAPartesIguales, () => ApunteEnReparto != null && !EstaOcupado);
            GuardarRepartoCommand = new AsyncRelayCommand(GuardarRepartoAsync, PuedeGuardarReparto);
            ImputarSeleccionadosCommand = new AsyncRelayCommand(ImputarSeleccionadosAsync, PuedeImputarSeleccionados);
        }

        // ---------- Filtros ----------

        private DateTime? _desde;
        public DateTime? Desde { get => _desde; set => SetProperty(ref _desde, value); }

        private DateTime? _hasta;
        public DateTime? Hasta { get => _hasta; set => SetProperty(ref _hasta, value); }

        private string? _cuenta;
        /// <summary>Prefijo de cuenta (o varios separados por comas: «623,705»).</summary>
        public string? Cuenta { get => _cuenta; set => SetProperty(ref _cuenta, value); }

        private string? _tercero;
        /// <summary>Proveedor o cliente.</summary>
        public string? Tercero { get => _tercero; set => SetProperty(ref _tercero, value); }

        private string? _documento;
        public string? Documento { get => _documento; set => SetProperty(ref _documento, value); }

        private string? _texto;
        public string? Texto { get => _texto; set => SetProperty(ref _texto, value); }

        private bool _soloPendientes = true;
        public bool SoloPendientes { get => _soloPendientes; set => SetProperty(ref _soloPendientes, value); }

        // ---------- Lista ----------

        public ObservableCollection<FilaApunteProyecto> Apuntes { get; } = new ObservableCollection<FilaApunteProyecto>();

        /// <summary>Los proyectos abiertos (para elegir a cuál imputar). Los pone la ventana.</summary>
        public ObservableCollection<ProyectoModel> ProyectosAbiertos { get; } = new ObservableCollection<ProyectoModel>();

        public void PonerProyectosAbiertos(IEnumerable<ProyectoModel>? proyectos)
        {
            ProyectosAbiertos.Clear();
            foreach (ProyectoModel p in proyectos?.Where(p => p != null && !p.EstaCerrado) ?? Enumerable.Empty<ProyectoModel>())
            {
                ProyectosAbiertos.Add(p);
            }
            if (ApunteEnReparto != null)
            {
                RellenarOpciones(ApunteEnReparto.Apunte);
            }
        }

        private bool _hayMas;
        public bool HayMas
        {
            get => _hayMas;
            private set
            {
                if (SetProperty(ref _hayMas, value))
                {
                    CargarMasCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _resumen = string.Empty;
        public string Resumen { get => _resumen; private set => SetProperty(ref _resumen, value); }

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

        public bool HaBuscado { get; private set; }

        public IAsyncRelayCommand BuscarCommand { get; }
        public IAsyncRelayCommand CargarMasCommand { get; }

        public async Task BuscarAsync()
        {
            _pagina = 0;
            await CargarAsync(anadir: false);
        }

        public async Task CargarMasAsync()
        {
            if (!HayMas)
            {
                return;
            }
            _pagina++;
            await CargarAsync(anadir: true);
        }

        internal FiltroApuntesProyectos Filtro() => new FiltroApuntesProyectos
        {
            Empresa = _empresa,
            Desde = Desde,
            Hasta = Hasta,
            Cuenta = Limpio(Cuenta),
            Proveedor = Limpio(Tercero),
            Documento = Limpio(Documento),
            Texto = Limpio(Texto),
            SoloPendientes = SoloPendientes,
            Pagina = _pagina
        };

        private static string? Limpio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

        private async Task CargarAsync(bool anadir)
        {
            try
            {
                EstaOcupado = true;
                ApuntesPorImputarModel respuesta = await _servicio.ApuntesPorImputar(Filtro());
                if (!anadir)
                {
                    Apuntes.Clear();
                    CambiarSeleccion(null);
                }
                foreach (ApunteProyectoModel a in respuesta?.Apuntes?.Where(a => a != null) ?? Enumerable.Empty<ApunteProyectoModel>())
                {
                    Apuntes.Add(new FilaApunteProyecto(a));
                }
                HayMas = respuesta?.HayMas ?? false;
                HaBuscado = true;
                Resumen = TextoResumen();
            }
            catch (Exception ex)
            {
                if (anadir)
                {
                    _pagina--;
                }
                _dialogos.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        internal string TextoResumen()
        {
            if (Apuntes.Count == 0)
            {
                return "No hay apuntes con esos filtros.";
            }
            decimal pendiente = Apuntes.Sum(a => a.Pendiente);
            string cuantos = Apuntes.Count == 1 ? "1 apunte" : $"{Apuntes.Count:N0} apuntes";
            string texto = $"{cuantos}; pendiente de imputar: {ReglasPantallaProyectos.Euros(pendiente)}.";
            return HayMas ? texto + " Hay más: pulse «Cargar más» o afine los filtros." : texto;
        }

        // ---------- Selección ----------

        private List<FilaApunteProyecto> _seleccionados = new List<FilaApunteProyecto>();
        public IReadOnlyList<FilaApunteProyecto> Seleccionados => _seleccionados;

        /// <summary>La vista pasa lo elegido en la rejilla (SelectedItems no se puede enlazar).</summary>
        public void CambiarSeleccion(IEnumerable<FilaApunteProyecto>? seleccionados)
        {
            _seleccionados = seleccionados?.Where(s => s != null).Distinct().ToList() ?? new List<FilaApunteProyecto>();
            OnPropertyChanged(nameof(Seleccionados));
            OnPropertyChanged(nameof(TextoSeleccion));
            OnPropertyChanged(nameof(HayVariosSeleccionados));
            FilaApunteProyecto? uno = _seleccionados.Count == 1 ? _seleccionados[0] : null;
            if (!ReferenceEquals(uno, ApunteEnReparto))
            {
                ApunteEnReparto = uno;
                CargarReparto(uno?.Apunte);
            }
            NotificarComandos();
        }

        public bool HayVariosSeleccionados => _seleccionados.Count > 1;

        public string TextoSeleccion
        {
            get
            {
                if (_seleccionados.Count == 0)
                {
                    return "Elija un apunte para repartirlo, o varios para imputarlos enteros a un proyecto.";
                }
                if (_seleccionados.Count == 1)
                {
                    return "1 apunte elegido.";
                }
                decimal total = _seleccionados.Sum(s => s.ImporteApunte);
                return $"{_seleccionados.Count} apuntes elegidos ({ReglasPantallaProyectos.Euros(total)}).";
            }
        }

        // ---------- Reparto de un apunte ----------

        private FilaApunteProyecto? _apunteEnReparto;
        public FilaApunteProyecto? ApunteEnReparto
        {
            get => _apunteEnReparto;
            private set
            {
                if (SetProperty(ref _apunteEnReparto, value))
                {
                    OnPropertyChanged(nameof(HayApunteEnReparto));
                }
            }
        }

        public bool HayApunteEnReparto => ApunteEnReparto != null;

        public ObservableCollection<FilaRepartoProyecto> Filas { get; } = new ObservableCollection<FilaRepartoProyecto>();

        /// <summary>Los proyectos que se pueden elegir en una fila: los abiertos y los cerrados que ya tiene el apunte (para enseñarlos).</summary>
        public ObservableCollection<ProyectoModel> OpcionesProyecto { get; } = new ObservableCollection<ProyectoModel>();

        private string _avisosReparto = string.Empty;
        /// <summary>Los avisos de la API al guardar (no bloquean: el reparto ya está guardado).</summary>
        public string AvisosReparto
        {
            get => _avisosReparto;
            private set
            {
                if (SetProperty(ref _avisosReparto, value))
                {
                    OnPropertyChanged(nameof(HayAvisosReparto));
                }
            }
        }

        public bool HayAvisosReparto => !string.IsNullOrWhiteSpace(AvisosReparto);

        private void CargarReparto(ApunteProyectoModel? apunte)
        {
            foreach (FilaRepartoProyecto f in Filas)
            {
                f.Cambiada -= AlCambiarFila;
            }
            Filas.Clear();
            AvisosReparto = string.Empty;
            if (apunte != null)
            {
                RellenarOpciones(apunte);
                foreach (ImputacionApunteModel i in apunte.Imputaciones?.Where(i => i != null) ?? Enumerable.Empty<ImputacionApunteModel>())
                {
                    AnadirFila(new FilaRepartoProyecto
                    {
                        ProyectoId = i.ProyectoId,
                        Importe = i.Importe,
                        Comentario = i.Comentario,
                        ProyectoCerrado = string.Equals(i.EstadoProyecto?.Trim(), ReglasPantallaProyectos.ESTADO_CERRADO, StringComparison.OrdinalIgnoreCase)
                    });
                }
                if (Filas.Count == 0)
                {
                    AnadirFila(new FilaRepartoProyecto());
                }
            }
            RecalcularReparto();
        }

        private void RellenarOpciones(ApunteProyectoModel apunte)
        {
            OpcionesProyecto.Clear();
            foreach (ProyectoModel p in ProyectosAbiertos)
            {
                OpcionesProyecto.Add(p);
            }
            foreach (ImputacionApunteModel i in apunte.Imputaciones?.Where(i => i != null) ?? Enumerable.Empty<ImputacionApunteModel>())
            {
                if (!OpcionesProyecto.Any(p => p.Id == i.ProyectoId))
                {
                    OpcionesProyecto.Add(new ProyectoModel
                    {
                        Id = i.ProyectoId,
                        Codigo = i.CodigoProyecto,
                        Nombre = i.NombreProyecto,
                        Estado = i.EstadoProyecto
                    });
                }
            }
        }

        private void AnadirFila(FilaRepartoProyecto fila)
        {
            fila.Cambiada += AlCambiarFila;
            Filas.Add(fila);
        }

        private void AlCambiarFila(object? sender, EventArgs e) => RecalcularReparto();

        public IRelayCommand AnadirFilaCommand { get; }

        private void AnadirFila()
        {
            if (ApunteEnReparto == null)
            {
                return;
            }
            decimal pendiente = CalculoRepartoApunte.Pendiente(ApunteEnReparto.ImporteApunte, Partes());
            AnadirFila(new FilaRepartoProyecto { Importe = pendiente > 0M ? pendiente : (decimal?)null });
            RecalcularReparto();
        }

        public IRelayCommand<FilaRepartoProyecto> QuitarFilaCommand { get; }

        private void QuitarFila(FilaRepartoProyecto? fila)
        {
            if (fila == null || fila.ProyectoCerrado)
            {
                return;
            }
            fila.Cambiada -= AlCambiarFila;
            Filas.Remove(fila);
            RecalcularReparto();
        }

        public IRelayCommand<FilaRepartoProyecto> TodoAEsteProyectoCommand { get; }

        /// <summary>Todo el apunte (menos lo que ya está en proyectos cerrados, que no se puede tocar) a ese proyecto; las demás filas se quitan.</summary>
        internal void TodoAEsteProyecto(FilaRepartoProyecto? fila)
        {
            if (fila == null || ApunteEnReparto == null || !fila.ProyectoId.HasValue || fila.ProyectoCerrado)
            {
                return;
            }
            foreach (FilaRepartoProyecto otra in Filas.Where(f => !ReferenceEquals(f, fila) && !f.ProyectoCerrado).ToList())
            {
                otra.Cambiada -= AlCambiarFila;
                Filas.Remove(otra);
            }
            decimal fijo = CalculoRepartoApunte.Imputado(Filas.Where(f => f.ProyectoCerrado).Select(f => f.Parte));
            fila.Importe = CalculoRepartoApunte.Redondear(ApunteEnReparto.ImporteApunte - fijo);
            RecalcularReparto();
        }

        public IRelayCommand RepartirAPartesIgualesCommand { get; }

        /// <summary>El apunte (menos lo de proyectos cerrados) a partes iguales entre las filas con proyecto; la última lleva el céntimo de ajuste.</summary>
        internal void RepartirAPartesIguales()
        {
            if (ApunteEnReparto == null)
            {
                return;
            }
            List<FilaRepartoProyecto> elegidas = Filas.Where(f => f.ProyectoId.HasValue && !f.ProyectoCerrado).ToList();
            if (elegidas.Count == 0)
            {
                _dialogos.ShowNotification(TITULO, "Elija antes en las filas los proyectos entre los que repartir.");
                return;
            }
            decimal fijo = CalculoRepartoApunte.Imputado(Filas.Where(f => f.ProyectoCerrado).Select(f => f.Parte));
            List<decimal> partes = CalculoRepartoApunte.PartesIguales(ApunteEnReparto.ImporteApunte - fijo, elegidas.Count);
            for (int i = 0; i < elegidas.Count; i++)
            {
                elegidas[i].Importe = partes[i];
            }
            RecalcularReparto();
        }

        private List<ParteReparto> Partes() => Filas.Select(f => f.Parte).ToList();

        private string _textoPendiente = string.Empty;
        public string TextoPendiente { get => _textoPendiente; private set => SetProperty(ref _textoPendiente, value); }

        private string _motivosReparto = string.Empty;
        /// <summary>Lo que impide guardar el reparto (vacío si se puede).</summary>
        public string MotivosReparto
        {
            get => _motivosReparto;
            private set
            {
                if (SetProperty(ref _motivosReparto, value))
                {
                    OnPropertyChanged(nameof(HayMotivosReparto));
                }
            }
        }

        public bool HayMotivosReparto => !string.IsNullOrWhiteSpace(MotivosReparto);

        private void RecalcularReparto()
        {
            if (ApunteEnReparto == null)
            {
                TextoPendiente = string.Empty;
                MotivosReparto = string.Empty;
            }
            else
            {
                List<ParteReparto> partes = Partes();
                TextoPendiente = CalculoRepartoApunte.TextoPendiente(ApunteEnReparto.ImporteApunte, partes);
                MotivosReparto = string.Join(Environment.NewLine, CalculoRepartoApunte.Motivos(ApunteEnReparto.ImporteApunte, partes));
            }
            GuardarRepartoCommand.NotifyCanExecuteChanged();
            TodoAEsteProyectoCommand.NotifyCanExecuteChanged();
        }

        public IAsyncRelayCommand GuardarRepartoCommand { get; }

        private bool PuedeGuardarReparto() => ApunteEnReparto != null && !EstaOcupado && !HayMotivosReparto;

        public async Task GuardarRepartoAsync()
        {
            FilaApunteProyecto? fila = ApunteEnReparto;
            if (fila == null || !PuedeGuardarReparto())
            {
                return;
            }
            var peticion = new PeticionImputacionesApunte
            {
                Empresa = fila.Apunte.Empresa?.Trim() ?? _empresa,
                ImporteApunte = fila.ImporteApunte,
                Imputaciones = CalculoRepartoApunte.ParaEnviar(Partes())
            };
            try
            {
                EstaOcupado = true;
                ImputacionesApunteModel respuesta = await _servicio.GuardarImputaciones(fila.NumeroOrden, peticion);
                AplicarRespuesta(fila, respuesta);
                AvisosReparto = string.Join(Environment.NewLine, respuesta?.Avisos?.Where(a => !string.IsNullOrWhiteSpace(a)) ?? Enumerable.Empty<string>());
                Resumen = TextoResumen();
            }
            catch (ProyectosApiException ex) when (ex.Codigo == ProyectosService.CODIGO_APUNTE_CAMBIADO)
            {
                bool recargado = await RecargarApunteAsync(fila);
                _dialogos.ShowError(ex.Message + Environment.NewLine + Environment.NewLine + (recargado
                    ? "Se ha vuelto a leer el apunte: revise el reparto y guárdelo otra vez."
                    : "No se ha podido volver a leer el apunte: pulse «Buscar»."));
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

        private void AplicarRespuesta(FilaApunteProyecto fila, ImputacionesApunteModel? respuesta)
        {
            if (respuesta?.Apunte != null)
            {
                fila.Actualizar(respuesta.Apunte);
            }
            else if (respuesta != null)
            {
                fila.Apunte.Imputaciones = respuesta.Imputaciones ?? new List<ImputacionApunteModel>();
                fila.Actualizar(fila.Apunte);
            }
            if (ReferenceEquals(fila, ApunteEnReparto))
            {
                CargarReparto(fila.Apunte);
            }
        }

        /// <summary>Vuelve a leer el apunte (su importe y su reparto); si ya no está en la contabilidad, lo quita de la lista.</summary>
        private async Task<bool> RecargarApunteAsync(FilaApunteProyecto fila)
        {
            try
            {
                ImputacionesApunteModel leido = await _servicio.LeerImputaciones(fila.Apunte.Empresa?.Trim() ?? _empresa, fila.NumeroOrden);
                if (leido?.Apunte == null)
                {
                    Apuntes.Remove(fila);
                    CambiarSeleccion(_seleccionados.Where(s => !ReferenceEquals(s, fila)).ToList());
                    Resumen = TextoResumen();
                    return true;
                }
                AplicarRespuesta(fila, leido);
                Resumen = TextoResumen();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ---------- Varios apuntes enteros a un proyecto ----------

        private ProyectoModel? _proyectoParaSeleccionados;
        public ProyectoModel? ProyectoParaSeleccionados
        {
            get => _proyectoParaSeleccionados;
            set
            {
                if (SetProperty(ref _proyectoParaSeleccionados, value))
                {
                    ImputarSeleccionadosCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public IAsyncRelayCommand ImputarSeleccionadosCommand { get; }

        private bool PuedeImputarSeleccionados() => _seleccionados.Count > 0 && ProyectoParaSeleccionados != null && !EstaOcupado;

        public async Task ImputarSeleccionadosAsync()
        {
            ProyectoModel? proyecto = ProyectoParaSeleccionados;
            List<FilaApunteProyecto> apuntes = _seleccionados.ToList();
            if (proyecto == null || apuntes.Count == 0 || EstaOcupado)
            {
                return;
            }
            if (!_dialogos.ShowConfirmationAnswer(TITULO, PreguntaImputarSeleccionados(apuntes, proyecto), porDefectoNo: true))
            {
                return;
            }
            int bien = 0;
            var mal = new List<string>();
            var avisos = new List<string>();
            try
            {
                EstaOcupado = true;
                foreach (FilaApunteProyecto fila in apuntes)
                {
                    var peticion = new PeticionImputacionesApunte
                    {
                        Empresa = fila.Apunte.Empresa?.Trim() ?? _empresa,
                        ImporteApunte = fila.ImporteApunte,
                        Imputaciones = new List<ImputacionPedida> { new ImputacionPedida { ProyectoId = proyecto.Id, Importe = fila.ImporteApunte } }
                    };
                    try
                    {
                        ImputacionesApunteModel respuesta = await _servicio.GuardarImputaciones(fila.NumeroOrden, peticion);
                        AplicarRespuesta(fila, respuesta);
                        bien++;
                        avisos.AddRange(respuesta?.Avisos?.Where(a => !string.IsNullOrWhiteSpace(a)) ?? Enumerable.Empty<string>());
                    }
                    catch (Exception ex)
                    {
                        mal.Add($"Apunte {fila.NumeroOrden} ({fila.Fecha:dd/MM/yyyy}, {ReglasPantallaProyectos.Euros(fila.ImporteApunte)}): {ex.Message}");
                    }
                }
            }
            finally
            {
                EstaOcupado = false;
            }
            Resumen = TextoResumen();
            _dialogos.ShowNotification(TITULO, ResumenImputarSeleccionados(bien, mal, avisos, proyecto));
        }

        internal static string PreguntaImputarSeleccionados(IReadOnlyList<FilaApunteProyecto> apuntes, ProyectoModel proyecto)
        {
            decimal total = apuntes.Sum(a => a.ImporteApunte);
            int conReparto = apuntes.Count(a => a.Apunte.Imputaciones?.Count > 0);
            string cuantos = apuntes.Count == 1 ? "el apunte elegido entero" : $"los {apuntes.Count} apuntes elegidos enteros";
            string texto = $"¿Imputar {cuantos} ({ReglasPantallaProyectos.Euros(total)}) a {proyecto.CodigoYNombre}?";
            if (conReparto > 0)
            {
                texto += Environment.NewLine + (conReparto == 1
                    ? "1 ya tiene reparto: se sustituye por este."
                    : $"{conReparto} ya tienen reparto: se sustituye por este.");
            }
            return texto;
        }

        internal static string ResumenImputarSeleccionados(int bien, IReadOnlyList<string> mal, IReadOnlyList<string> avisos, ProyectoModel proyecto)
        {
            var lineas = new List<string>
            {
                bien == 1 ? $"1 apunte imputado a {proyecto.Codigo?.Trim()}." : $"{bien} apuntes imputados a {proyecto.Codigo?.Trim()}."
            };
            if (mal.Count > 0)
            {
                lineas.Add(mal.Count == 1 ? "No se ha podido imputar 1:" : $"No se han podido imputar {mal.Count}:");
                lineas.AddRange(mal);
            }
            List<string> distintos = avisos.Distinct().ToList();
            if (distintos.Count > 0)
            {
                lineas.Add("Avisos:");
                lineas.AddRange(distintos);
            }
            return string.Join(Environment.NewLine, lineas);
        }

        private void NotificarComandos()
        {
            BuscarCommand.NotifyCanExecuteChanged();
            CargarMasCommand.NotifyCanExecuteChanged();
            AnadirFilaCommand.NotifyCanExecuteChanged();
            QuitarFilaCommand.NotifyCanExecuteChanged();
            TodoAEsteProyectoCommand.NotifyCanExecuteChanged();
            RepartirAPartesIgualesCommand.NotifyCanExecuteChanged();
            GuardarRepartoCommand.NotifyCanExecuteChanged();
            ImputarSeleccionadosCommand.NotifyCanExecuteChanged();
        }
    }
}
