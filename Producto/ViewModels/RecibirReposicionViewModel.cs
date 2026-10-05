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
    /// <summary>Una línea de la reposición: lo enviado y lo que se ha leído al recibirla.</summary>
    public class LineaRecibirReposicion : ObservableObject
    {
        public string Producto { get; set; }
        public string Descripcion { get; set; }
        public string CodigoBarras { get; set; }
        public bool SinCodigo { get; set; }
        public int Enviado { get; set; }
        /// <summary>Se ha leído y no venía en la reposición.</summary>
        public bool NoVenia { get; set; }

        private int _leido;
        /// <summary>Lo leído (o tecleado, en los productos sin código).</summary>
        public int Leido
        {
            get => _leido;
            set
            {
                if (SetProperty(ref _leido, Math.Max(0, value)))
                {
                    OnPropertyChanged(nameof(Diferencia));
                    OnPropertyChanged(nameof(Estado));
                }
            }
        }

        public int Diferencia => Leido - Enviado;

        public string Estado => NoVenia ? "No venía"
            : Diferencia == 0 ? "Bien"
            : Diferencia < 0 ? $"Faltan {-Diferencia}"
            : $"Sobran {Diferencia}";

        internal bool TieneCodigo(string codigo)
            => (!SinCodigo && string.Equals(CodigoBarras?.Trim(), codigo, StringComparison.OrdinalIgnoreCase))
               || string.Equals(Producto?.Trim(), codigo, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// NestoAPI#553: la tienda recibe la reposición que sale de Algete con la misma API que Ariadna. Se elige la
    /// reposición, se lee lo que llega (el lector suma una unidad; los productos sin código se teclean en la columna
    /// «Leído») y al terminar entra LO LEÍDO; el servidor informa de las diferencias a quien creó el traspaso.
    /// </summary>
    public class RecibirReposicionViewModel : ObservableObject
    {
        private readonly IServicioRecepcionReposiciones _servicio;
        private readonly IServicioDialogos _dialogos;
        private readonly IConfiguracion _configuracion;

        /// <summary>Uno por reposición abierta: si la respuesta se pierde y se reintenta, no se recibe dos veces.</summary>
        private Guid _idRecepcion;

        public RecibirReposicionViewModel(IServicioRecepcionReposiciones servicio, IServicioDialogos dialogos, IConfiguracion configuracion)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            _configuracion = configuracion;
            CargarCommand = new AsyncRelayCommand(CargarAsync);
            ElegirCommand = new AsyncRelayCommand<RecepcionPendiente>(ElegirAsync);
            LeerCommand = new RelayCommand<string>(Leer);
            LeerLecturaCommand = new RelayCommand(() =>
            {
                Leer(Lectura);
                Lectura = string.Empty;
            });
            TerminarCommand = new AsyncRelayCommand(TerminarAsync, () => PuedeTerminar && !EstaOcupado);
        }

        public string Titulo => "Recibir reposición";

        public string Empresa { get; set; } = Constantes.Empresas.EMPRESA_DEFECTO;

        private string _almacen;
        /// <summary>El almacén del usuario (AlmacénPedidoVta): el de destino de las reposiciones que puede recibir.</summary>
        public string Almacen { get => _almacen; private set => SetProperty(ref _almacen, value); }

        public ObservableCollection<RecepcionPendiente> Pendientes { get; } = new ObservableCollection<RecepcionPendiente>();
        public ObservableCollection<LineaRecibirReposicion> Lineas { get; } = new ObservableCollection<LineaRecibirReposicion>();

        private RecepcionPendiente _seleccionada;
        public RecepcionPendiente Seleccionada { get => _seleccionada; private set => SetProperty(ref _seleccionada, value); }

        private RecepcionReposicion _recepcion;

        private bool _puedeTerminar;
        public bool PuedeTerminar
        {
            get => _puedeTerminar;
            private set
            {
                if (SetProperty(ref _puedeTerminar, value))
                {
                    TerminarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _mensaje;
        public string Mensaje { get => _mensaje; set => SetProperty(ref _mensaje, value); }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    TerminarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public IAsyncRelayCommand CargarCommand { get; }
        public IAsyncRelayCommand<RecepcionPendiente> ElegirCommand { get; }
        public IRelayCommand<string> LeerCommand { get; }

        private string _lectura;
        /// <summary>El cuadro del lector: Intro lo lee (<see cref="LeerLecturaCommand"/>) y lo vacía.</summary>
        public string Lectura { get => _lectura; set => SetProperty(ref _lectura, value); }

        public IRelayCommand LeerLecturaCommand { get; }
        public IAsyncRelayCommand TerminarCommand { get; }

        public async Task CargarAsync()
        {
            Mensaje = null;
            await Ocupado(async () =>
            {
                string almacen = (await _configuracion.leerParametro(Empresa, Parametros.Claves.AlmacenPedidoVta).ConfigureAwait(true))?.Trim();
                if (string.IsNullOrWhiteSpace(almacen))
                {
                    Mensaje = "Tu usuario no tiene almacén (parámetro AlmacénPedidoVta): pide a la oficina que te lo pongan.";
                    return;
                }
                Almacen = almacen.ToUpperInvariant();
                List<RecepcionPendiente> pendientes = await _servicio.LeerPendientes(Empresa, Almacen).ConfigureAwait(true);
                Pendientes.Clear();
                foreach (RecepcionPendiente pendiente in pendientes ?? new List<RecepcionPendiente>())
                {
                    Pendientes.Add(pendiente);
                }
                if (Pendientes.Count == 0)
                {
                    Mensaje = $"No hay ninguna reposición pendiente de recibir en {Almacen}.";
                }
            }).ConfigureAwait(true);
        }

        private async Task ElegirAsync(RecepcionPendiente pendiente)
        {
            if (pendiente == null)
            {
                return;
            }
            Mensaje = null;
            await Ocupado(async () =>
            {
                RecepcionReposicion recepcion = await _servicio.LeerRecepcion(Empresa, Almacen, pendiente.Documento).ConfigureAwait(true);
                if (recepcion == null)
                {
                    Mensaje = $"La reposición {pendiente.Documento} ya no está pendiente.";
                    return;
                }
                _recepcion = recepcion;
                _idRecepcion = Guid.NewGuid();
                Seleccionada = pendiente;
                Lineas.Clear();
                foreach (LineaRecepcionReposicion linea in recepcion.Lineas ?? new List<LineaRecepcionReposicion>())
                {
                    Lineas.Add(new LineaRecibirReposicion
                    {
                        Producto = linea.Producto?.Trim(),
                        Descripcion = linea.Descripcion?.Trim(),
                        CodigoBarras = linea.CodigoBarras?.Trim(),
                        SinCodigo = linea.SinCodigo,
                        Enviado = linea.Cantidad
                    });
                }
                PuedeTerminar = recepcion.PuedeTerminar && recepcion.SeTerminaDesdeAqui;
                if (!PuedeTerminar)
                {
                    Mensaje = $"Esta reposición se recibe en {Almacen}: con tu usuario puedes leerla, pero no puedes terminarla.";
                }
            }).ConfigureAwait(true);
        }

        /// <summary>Lo que manda el lector (o se teclea): un código de barras o la referencia suma una unidad.</summary>
        private void Leer(string lectura)
        {
            string codigo = lectura?.Trim();
            if (string.IsNullOrEmpty(codigo) || Seleccionada == null)
            {
                return;
            }
            List<LineaRecibirReposicion> candidatas = Lineas.Where(l => !l.NoVenia && l.TieneCodigo(codigo)).ToList();
            if (candidatas.Count > 1)
            {
                Mensaje = $"El código {codigo} lo comparten varios productos: escribe la cantidad en la columna «Leído» del que es.";
                return;
            }
            LineaRecibirReposicion linea = candidatas.FirstOrDefault()
                ?? Lineas.FirstOrDefault(l => l.NoVenia && string.Equals(l.Producto, codigo, StringComparison.OrdinalIgnoreCase));
            if (linea == null)
            {
                // Entra lo leído, también lo que no venía (04/10): se apunta aparte y el servidor lo informa
                linea = new LineaRecibirReposicion { Producto = codigo, Descripcion = "(no venía en la reposición)", NoVenia = true };
                Lineas.Add(linea);
            }
            linea.Leido++;
            Mensaje = linea.NoVenia ? $"El código {codigo} no venía en esta reposición: se apunta aparte." : null;
        }

        private async Task TerminarAsync()
        {
            if (_recepcion == null || Seleccionada == null)
            {
                return;
            }
            if (!await _dialogos.ShowConfirmationAsync($"¿Terminar la reposición {Seleccionada.Documento}?", ResumenParaConfirmar()).ConfigureAwait(true))
            {
                return;
            }
            var terminar = new TerminarRecepcionReposicion
            {
                IdRecepcion = _idRecepcion,
                Dispositivo = Environment.MachineName,
                Lecturas = Lineas.Where(l => l.Leido > 0)
                    .Select(l => new LecturaRecepcionReposicion { Producto = l.Producto, Cantidad = l.Leido })
                    .ToList()
            };
            await Ocupado(async () =>
            {
                ResultadoRecepcionReposicion resultado = await _servicio.Terminar(Empresa, Almacen, Seleccionada.Documento, terminar).ConfigureAwait(true);
                Mensaje = TextoResultado(Seleccionada.Documento, resultado);
                Pendientes.Remove(Seleccionada);
                Seleccionada = null;
                _recepcion = null;
                Lineas.Clear();
                PuedeTerminar = false;
            }).ConfigureAwait(true);
        }

        internal string ResumenParaConfirmar()
        {
            int faltan = Lineas.Where(l => !l.NoVenia && l.Diferencia < 0).Sum(l => -l.Diferencia);
            int sobran = Lineas.Where(l => !l.NoVenia && l.Diferencia > 0).Sum(l => l.Diferencia);
            int noVenian = Lineas.Where(l => l.NoVenia && l.Leido > 0).Sum(l => l.Leido);
            var partes = new List<string>();
            if (faltan == 0 && sobran == 0 && noVenian == 0)
            {
                partes.Add("Todo coincide con lo enviado.");
            }
            else
            {
                if (faltan > 0)
                {
                    partes.Add($"Faltan {faltan} ud. de lo enviado.");
                }
                if (sobran > 0)
                {
                    partes.Add($"Sobran {sobran} ud. de lo enviado.");
                }
                if (noVenian > 0)
                {
                    partes.Add($"Han llegado {noVenian} ud. que no venían en la reposición.");
                }
                partes.Add("Se informará de las diferencias a quien hizo la reposición.");
            }
            partes.Add("Ojo: entra lo leído, no lo enviado.");
            return string.Join(Environment.NewLine, partes);
        }

        internal static string TextoResultado(string documento, ResultadoRecepcionReposicion resultado)
        {
            var partes = new List<string>
            {
                resultado?.YaEstabaTerminada == true ? $"La reposición {documento} ya estaba recibida." : $"Reposición {documento} recibida."
            };
            List<DiferenciaRecepcionReposicion> diferencias = resultado?.Diferencias ?? new List<DiferenciaRecepcionReposicion>();
            if (diferencias.Count > 0)
            {
                partes.Add("No coincide con lo enviado (ha entrado lo leído):");
                partes.AddRange(diferencias.Select(d => d.Ajeno
                    ? $"  {d.Producto?.Trim()} {d.Descripcion?.Trim()}: no venía, han entrado {d.Leido}."
                    : $"  {d.Producto?.Trim()} {d.Descripcion?.Trim()}: enviadas {d.Esperado}, han entrado {d.Leido}."));
            }
            if (!string.IsNullOrWhiteSpace(resultado?.AvisadoA))
            {
                string quien = resultado.AvisadoA.Contains("\\") ? resultado.AvisadoA.Substring(resultado.AvisadoA.LastIndexOf('\\') + 1) : resultado.AvisadoA;
                partes.Add($"Se ha informado a {quien.Trim()}.");
            }
            partes.AddRange((resultado?.Avisos ?? new List<string>()).Where(a => !string.IsNullOrWhiteSpace(a)));
            return string.Join(Environment.NewLine, partes);
        }

        private async Task Ocupado(Func<Task> accion)
        {
            EstaOcupado = true;
            try
            {
                await accion().ConfigureAwait(true);
            }
            catch (RecepcionReposicionException ex)
            {
                Mensaje = ex.Message;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                Mensaje = "No se puede conectar con el servidor. Vuelve a intentarlo; lo leído no se pierde.";
            }
            finally
            {
                EstaOcupado = false;
            }
        }
    }
}
