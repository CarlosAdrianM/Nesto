using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cliente.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// NestoAPI#876 (27/11/26): LO QUE DECIDE LA PANTALLA de las fichas equivalentes (puro). Quién puede enlazar, si hace un ciclo, si
    /// se encadena… lo decide la API.
    /// </summary>
    public static class ReglasPantallaEquivalentes
    {
        public const string DUPLICADO = "Duplicado";
        public const string CAMBIO_FISCAL = "CambioFiscal";
        public const string FALTA_OTRA_FICHA = "Escriba el número de la otra ficha.";
        public const string MISMA_FICHA = "La otra ficha no puede ser la misma que esta.";
        public const string FALTA_MOTIVO = "Elija el motivo: «Duplicado» o «Cambio de forma fiscal».";

        /// <summary>Lo que se dice en las filas de otra ficha (extracto, rapports): se ven, pero no se tocan desde aquí.</summary>
        public const string MOTIVO_SOLO_LECTURA = "Es de otra ficha equivalente: solo se puede consultar. Para tocarla, abra esa ficha.";

        private static readonly CultureInfo ES = CultureInfo.GetCultureInfo("es-ES");

        public static string TextoMotivo(string motivo) => motivo?.Trim() switch
        {
            null or "" => null,
            DUPLICADO => "Duplicado",
            CAMBIO_FISCAL => "Cambio de forma fiscal",
            _ => motivo.Trim()
        };

        /// <summary>«De alta» o «De baja», con el estado de la ficha principal.</summary>
        public static string TextoEstado(FichaEquivalenteModel f)
        {
            if (f == null)
            {
                return string.Empty;
            }
            string alta = f.Activa ? "De alta" : "De baja";
            return f.Estado.HasValue ? $"{alta} (estado {f.Estado.Value.ToString(ES)})" : alta;
        }

        /// <summary>«→ 8244 · Duplicado · Carlos, 26/11/2026 · comentario»; vacío si la ficha no apunta a ninguna.</summary>
        public static string TextoEnlace(FichaEquivalenteModel f)
        {
            if (string.IsNullOrWhiteSpace(f?.ClienteAnterior))
            {
                return string.Empty;
            }
            var partes = new List<string> { "→ " + f.ClienteAnterior.Trim() };
            string motivo = TextoMotivo(f.Motivo);
            if (motivo != null)
            {
                partes.Add(motivo);
            }
            string quien = string.Join(", ", new[] { f.Usuario?.Trim(), f.Fecha?.ToString("dd/MM/yyyy", ES) }.Where(x => !string.IsNullOrEmpty(x)));
            if (quien.Length > 0)
            {
                partes.Add(quien);
            }
            if (!string.IsNullOrWhiteSpace(f.Comentario))
            {
                partes.Add(f.Comentario.Trim());
            }
            return string.Join(" · ", partes);
        }

        /// <summary>Hay alguna ficha equivalente además de la consultada.</summary>
        public static bool HayEquivalentes(ClientesEquivalentesModel grupo, string cliente)
            => (grupo?.Fichas ?? new List<FichaEquivalenteModel>()).Any(f => f != null && !string.IsNullOrWhiteSpace(f.Cliente)
                && !f.EsLaConsultada && !MismoCliente(f.Cliente, cliente));

        public static bool MismoCliente(string a, string b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

        /// <summary>La fila es de otra ficha que la abierta (solo tiene sentido con «Incluir fichas equivalentes»).</summary>
        public static bool EsDeOtraFicha(string clienteFila, string clienteAbierto)
            => !string.IsNullOrWhiteSpace(clienteFila) && !string.IsNullOrWhiteSpace(clienteAbierto) && !MismoCliente(clienteFila, clienteAbierto);

        /// <summary>Por qué no se puede pedir el enlace todavía (null si se puede).</summary>
        public static string MotivoNoSePuedeEnlazar(string cliente, string otra, string motivo)
        {
            if (string.IsNullOrWhiteSpace(otra))
            {
                return FALTA_OTRA_FICHA;
            }
            if (MismoCliente(cliente, otra))
            {
                return MISMA_FICHA;
            }
            return motivo == DUPLICADO || motivo == CAMBIO_FISCAL ? null : FALTA_MOTIVO;
        }

        /// <summary>«12345 · NOMBRE · NIF B12345678».</summary>
        public static string TextoFicha(string cliente, FichaEquivalenteModel f)
            => $"{cliente?.Trim()} · {(string.IsNullOrWhiteSpace(f?.Nombre) ? "(sin nombre)" : f.Nombre.Trim())} · NIF {(string.IsNullOrWhiteSpace(f?.Nif) ? "(sin NIF)" : f.Nif.Trim())}";

        public static string TextoConfirmacion(string queda, FichaEquivalenteModel fichaQueda, string otra, FichaEquivalenteModel fichaOtra,
            string motivo, string comentario)
        {
            string texto = "Se van a enlazar como fichas equivalentes:\n\n" +
                $"Queda: {TextoFicha(queda, fichaQueda)}\n" +
                $"La otra: {TextoFicha(otra, fichaOtra)}\n\n" +
                $"Motivo: {TextoMotivo(motivo)}";
            if (!string.IsNullOrWhiteSpace(comentario))
            {
                texto += $"\nComentario: {comentario.Trim()}";
            }
            return texto + "\n\nLo fiscal (facturas, deuda, 347) no se junta. ¿Desea continuar?";
        }

        /// <summary>El Mensaje de la API y, debajo, sus Avisos (que no son errores).</summary>
        public static string TextoResultado(ResultadoEnlaceEquivalenteModel r)
        {
            if (r == null)
            {
                return "Hecho.";
            }
            var lineas = new List<string>();
            if (!string.IsNullOrWhiteSpace(r.Mensaje))
            {
                lineas.Add(r.Mensaje.Trim());
            }
            List<string> avisos = (r.Avisos ?? new List<string>()).Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => "· " + a.Trim()).ToList();
            if (avisos.Any())
            {
                lineas.Add(string.Empty);
                lineas.Add("Avisos:");
                lineas.AddRange(avisos);
            }
            return lineas.Any() ? string.Join("\n", lineas) : "Hecho.";
        }
    }

    /// <summary>NestoAPI#876: una ficha del grupo de equivalentes, como se pinta.</summary>
    public class FilaFichaEquivalente
    {
        public FilaFichaEquivalente(FichaEquivalenteModel f)
        {
            Ficha = f ?? new FichaEquivalenteModel();
            Cliente = Ficha.Cliente?.Trim();
            Nombre = Ficha.Nombre?.Trim();
            Nif = Ficha.Nif?.Trim();
            Estado = ReglasPantallaEquivalentes.TextoEstado(Ficha);
            Enlace = ReglasPantallaEquivalentes.TextoEnlace(Ficha);
            ClienteAnterior = string.IsNullOrWhiteSpace(Ficha.ClienteAnterior) ? null : Ficha.ClienteAnterior.Trim();
            EsLaConsultada = Ficha.EsLaConsultada;
        }

        public FichaEquivalenteModel Ficha { get; }
        public string Cliente { get; }
        public string Nombre { get; }
        public string Nif { get; }
        public string Estado { get; }
        public string Enlace { get; }
        public string ClienteAnterior { get; }
        public bool EsLaConsultada { get; }
        public bool TieneEnlace => ClienteAnterior != null;
        public string Marca => EsLaConsultada ? "Esta ficha" : string.Empty;
    }

    /// <summary>
    /// NestoAPI#876 (27/11/26): la pestaña «Fichas equivalentes» de la ficha del cliente: el grupo (número, nombre, NIF, estado, motivo,
    /// quién y cuándo), «Abrir» otra ficha, «Enlazar con otra ficha…» y «Deshacer enlace». Los 403/404/409 se enseñan con el mensaje de
    /// la API; con 503 (el servidor no tiene la tabla) se oculta todo (<see cref="Disponible"/>). Quién puede, lo decide la API.
    /// </summary>
    public class FichasEquivalentesViewModel : ObservableObject
    {
        public const string REGION_PRINCIPAL = "MainRegion";
        public const string VISTA_FICHA_CLIENTE = "CrearClienteView";

        private readonly IServicioClientesEquivalentes _servicio;
        private readonly IServicioDialogos _dialogos;
        private readonly IServicioNavegacion _navegacion;
        private int _testigo;

        public FichasEquivalentesViewModel(IServicioClientesEquivalentes servicio, IServicioDialogos dialogos, IServicioNavegacion navegacion)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            _navegacion = navegacion;
            AbrirCommand = new RelayCommand<FilaFichaEquivalente>(Abrir, f => f != null && !f.EsLaConsultada && _navegacion != null);
            DeshacerCommand = new AsyncRelayCommand<FilaFichaEquivalente>(DeshacerAsync, f => f?.TieneEnlace == true && !EstaOcupado);
            EmpezarEnlaceCommand = new RelayCommand(EmpezarEnlace, () => HayCliente && !EstaOcupado);
            CancelarEnlaceCommand = new RelayCommand(() => MostrandoEnlace = false);
            EnlazarCommand = new AsyncRelayCommand(EnlazarAsync, () => HayCliente && !EstaOcupado);
        }

        public string Empresa { get; private set; }

        private string _cliente;
        public string Cliente { get => _cliente; private set => SetProperty(ref _cliente, value); }

        public bool HayCliente => !string.IsNullOrWhiteSpace(Cliente);

        public ObservableCollection<FilaFichaEquivalente> Fichas { get; } = new();

        private bool _disponible = true;
        /// <summary>False con 503 (el servidor aún no tiene la tabla): no se enseña nada.</summary>
        public bool Disponible { get => _disponible; private set => SetProperty(ref _disponible, value); }

        private bool _hayEquivalentes;
        public bool HayEquivalentes { get => _hayEquivalentes; private set => SetProperty(ref _hayEquivalentes, value); }

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

        private string _mensaje;
        public string Mensaje { get => _mensaje; private set => SetProperty(ref _mensaje, value); }

        // ------------------------------------------------ «Enlazar con otra ficha…»

        private bool _mostrandoEnlace;
        public bool MostrandoEnlace { get => _mostrandoEnlace; set => SetProperty(ref _mostrandoEnlace, value); }

        private string _otraFicha;
        public string OtraFicha { get => _otraFicha; set => SetProperty(ref _otraFicha, value); }

        private string _motivo;
        /// <summary>«Duplicado», «CambioFiscal» o null (sin elegir).</summary>
        public string Motivo
        {
            get => _motivo;
            set
            {
                if (SetProperty(ref _motivo, value))
                {
                    OnPropertyChanged(nameof(MotivoDuplicado));
                    OnPropertyChanged(nameof(MotivoCambioFiscal));
                }
            }
        }

        public bool MotivoDuplicado
        {
            get => Motivo == ReglasPantallaEquivalentes.DUPLICADO;
            set
            {
                if (value)
                {
                    Motivo = ReglasPantallaEquivalentes.DUPLICADO;
                }
            }
        }

        public bool MotivoCambioFiscal
        {
            get => Motivo == ReglasPantallaEquivalentes.CAMBIO_FISCAL;
            set
            {
                if (value)
                {
                    Motivo = ReglasPantallaEquivalentes.CAMBIO_FISCAL;
                }
            }
        }

        private bool _quedaEsta = true;
        /// <summary>La ficha que queda (la que apunta a la otra) es la abierta; si no, la otra.</summary>
        public bool QuedaEsta
        {
            get => _quedaEsta;
            set
            {
                if (SetProperty(ref _quedaEsta, value))
                {
                    OnPropertyChanged(nameof(QuedaLaOtra));
                }
            }
        }

        public bool QuedaLaOtra
        {
            get => !QuedaEsta;
            set => QuedaEsta = !value;
        }

        private string _comentario;
        public string Comentario { get => _comentario; set => SetProperty(ref _comentario, value); }

        public RelayCommand<FilaFichaEquivalente> AbrirCommand { get; }
        public AsyncRelayCommand<FilaFichaEquivalente> DeshacerCommand { get; }
        public RelayCommand EmpezarEnlaceCommand { get; }
        public RelayCommand CancelarEnlaceCommand { get; }
        public AsyncRelayCommand EnlazarCommand { get; }

        private void NotificarComandos()
        {
            DeshacerCommand?.NotifyCanExecuteChanged();
            EmpezarEnlaceCommand?.NotifyCanExecuteChanged();
            EnlazarCommand?.NotifyCanExecuteChanged();
        }

        /// <summary>El cliente de la ficha (al enseñarse o al cambiar de cliente): lee su grupo. Nunca lanza.</summary>
        public async Task CargarAsync(string empresa, string cliente)
        {
            Empresa = ServicioClientesEquivalentes.Empresa(empresa);
            Cliente = cliente?.Trim();
            OnPropertyChanged(nameof(HayCliente));
            MostrandoEnlace = false;
            NotificarComandos();
            await LeerAsync();
        }

        private async Task LeerAsync()
        {
            int testigo = ++_testigo;
            Fichas.Clear();
            HayEquivalentes = false;
            Mensaje = null;
            if (!HayCliente || !Disponible || _servicio == null)
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                ClientesEquivalentesModel grupo = await _servicio.Leer(Empresa, Cliente);
                if (testigo != _testigo)
                {
                    return; // ya se ha pedido otro cliente
                }
                PonerFichas(grupo?.Fichas);
            }
            catch (ClientesEquivalentesApiException ex) when (ex.SinTabla)
            {
                Disponible = false;
            }
            catch (Exception ex)
            {
                if (testigo == _testigo)
                {
                    Mensaje = ex.Message;
                }
            }
            finally
            {
                if (testigo == _testigo)
                {
                    EstaOcupado = false;
                }
            }
        }

        private void PonerFichas(IEnumerable<FichaEquivalenteModel> fichas)
        {
            Fichas.Clear();
            foreach (FichaEquivalenteModel f in (fichas ?? Enumerable.Empty<FichaEquivalenteModel>()).Where(f => f != null))
            {
                Fichas.Add(new FilaFichaEquivalente(f));
            }
            HayEquivalentes = ReglasPantallaEquivalentes.HayEquivalentes(new ClientesEquivalentesModel { Fichas = Fichas.Select(f => f.Ficha).ToList() }, Cliente);
            Mensaje = HayEquivalentes ? null : "Esta ficha no tiene ninguna ficha equivalente.";
        }

        private void Abrir(FilaFichaEquivalente fila)
        {
            if (fila == null || fila.EsLaConsultada || _navegacion == null)
            {
                return;
            }
            _navegacion.RequestNavigate(REGION_PRINCIPAL, VISTA_FICHA_CLIENTE, new ParametrosNavegacion
            {
                { "empresaParameter", Empresa },
                { "clienteParameter", fila.Cliente },
                { "contactoParameter", "0" }
            });
        }

        private void EmpezarEnlace()
        {
            OtraFicha = null;
            Motivo = null;
            QuedaEsta = true;
            Comentario = null;
            MostrandoEnlace = true;
        }

        /// <summary>«Enlazar»: comprueba la otra ficha, enseña nombre y NIF de las dos, pide confirmación y llama a la API.</summary>
        internal async Task EnlazarAsync()
        {
            string otra = OtraFicha?.Trim();
            string motivoNo = ReglasPantallaEquivalentes.MotivoNoSePuedeEnlazar(Cliente, otra, Motivo);
            if (motivoNo != null)
            {
                _dialogos.ShowError(motivoNo);
                return;
            }
            EstaOcupado = true;
            try
            {
                FichaEquivalenteModel fichaEsta = Fichas.FirstOrDefault(f => f.EsLaConsultada)?.Ficha
                    ?? Fichas.FirstOrDefault(f => ReglasPantallaEquivalentes.MismoCliente(f.Cliente, Cliente))?.Ficha;
                ClientesEquivalentesModel grupoOtra = await _servicio.Leer(Empresa, otra); // 404 si no existe
                FichaEquivalenteModel fichaOtra = grupoOtra?.Fichas?.FirstOrDefault(f => f.EsLaConsultada)
                    ?? grupoOtra?.Fichas?.FirstOrDefault(f => ReglasPantallaEquivalentes.MismoCliente(f.Cliente, otra));
                string queda = QuedaEsta ? Cliente : otra;
                string anterior = QuedaEsta ? otra : Cliente;
                FichaEquivalenteModel fichaQueda = QuedaEsta ? fichaEsta : fichaOtra;
                FichaEquivalenteModel fichaAnterior = QuedaEsta ? fichaOtra : fichaEsta;
                EstaOcupado = false;
                if (!_dialogos.ShowConfirmationAnswer("Enlazar fichas equivalentes",
                    ReglasPantallaEquivalentes.TextoConfirmacion(queda, fichaQueda, anterior, fichaAnterior, Motivo, Comentario)))
                {
                    return;
                }
                EstaOcupado = true;
                ResultadoEnlaceEquivalenteModel r = await _servicio.Enlazar(new PeticionEnlaceEquivalenteModel
                {
                    Empresa = Empresa,
                    Cliente = queda,
                    ClienteAnterior = anterior,
                    Motivo = Motivo,
                    Comentario = string.IsNullOrWhiteSpace(Comentario) ? null : Comentario.Trim()
                });
                MostrandoEnlace = false;
                _dialogos.ShowNotification("Fichas equivalentes", ReglasPantallaEquivalentes.TextoResultado(r));
            }
            catch (ClientesEquivalentesApiException ex) when (ex.SinTabla)
            {
                Disponible = false;
                _dialogos.ShowError(ex.Message);
                return;
            }
            catch (Exception ex)
            {
                _dialogos.ShowError(ex.Message);
                return;
            }
            finally
            {
                EstaOcupado = false;
            }
            await LeerAsync();
        }

        /// <summary>«Deshacer enlace» de una fila (su enlace a su anterior), con confirmación.</summary>
        internal async Task DeshacerAsync(FilaFichaEquivalente fila)
        {
            if (fila?.TieneEnlace != true)
            {
                return;
            }
            if (!_dialogos.ShowConfirmationAnswer("Deshacer enlace",
                $"¿Deshacer el enlace del {fila.Cliente} con el {fila.ClienteAnterior}? Las dos fichas dejarán de ser equivalentes " +
                "(si no hay otro enlace que las una).", porDefectoNo: true))
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                ResultadoEnlaceEquivalenteModel r = await _servicio.Deshacer(Empresa, fila.Cliente, fila.ClienteAnterior);
                _dialogos.ShowNotification("Fichas equivalentes", ReglasPantallaEquivalentes.TextoResultado(r));
            }
            catch (ClientesEquivalentesApiException ex) when (ex.SinTabla)
            {
                Disponible = false;
                _dialogos.ShowError(ex.Message);
                return;
            }
            catch (Exception ex)
            {
                _dialogos.ShowError(ex.Message);
                return;
            }
            finally
            {
                EstaOcupado = false;
            }
            await LeerAsync();
        }
    }

    /// <summary>
    /// NestoAPI#876 (27/11/26): la casilla «Incluir fichas equivalentes» del extracto y de los rapports. Solo se ve si el cliente tiene
    /// alguna ficha equivalente (según el GET); apagada por defecto y al cambiar de cliente. Si no se puede leer (403, 503, red), no se ve.
    /// </summary>
    public class CasillaFichasEquivalentes : ObservableObject
    {
        private readonly IServicioClientesEquivalentes _servicio;
        private int _testigo;

        public CasillaFichasEquivalentes(IServicioClientesEquivalentes servicio)
        {
            _servicio = servicio;
        }

        /// <summary>Se llama al marcar o desmarcar (para recargar).</summary>
        public event EventHandler IncluirCambiado;

        public string Cliente { get; private set; }

        private bool _visible;
        public bool Visible { get => _visible; private set => SetProperty(ref _visible, value); }

        private bool _incluir;
        public bool Incluir
        {
            get => _incluir;
            set
            {
                bool nuevo = value && Visible;
                if (SetProperty(ref _incluir, nuevo))
                {
                    IncluirCambiado?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private string _texto;
        /// <summary>«Incluir fichas equivalentes (8244, 39570)».</summary>
        public string Texto { get => _texto; private set => SetProperty(ref _texto, value); }

        /// <summary>Lee si el cliente tiene equivalentes; apaga la casilla sin avisar (el que llama ya recarga lo suyo). Nunca lanza.</summary>
        public async Task ComprobarAsync(string empresa, string cliente)
        {
            int testigo = ++_testigo;
            Cliente = cliente?.Trim();
            _incluir = false;
            OnPropertyChanged(nameof(Incluir));
            Visible = false;
            Texto = null;
            if (_servicio == null || string.IsNullOrWhiteSpace(Cliente))
            {
                return;
            }
            try
            {
                ClientesEquivalentesModel grupo = await _servicio.Leer(empresa, Cliente);
                if (testigo != _testigo)
                {
                    return;
                }
                List<string> otras = (grupo?.Fichas ?? new List<FichaEquivalenteModel>())
                    .Where(f => f != null && !f.EsLaConsultada && ReglasPantallaEquivalentes.EsDeOtraFicha(f.Cliente, Cliente))
                    .Select(f => f.Cliente.Trim()).ToList();
                Visible = otras.Any();
                Texto = Visible ? $"Incluir fichas equivalentes ({string.Join(", ", otras)})" : null;
            }
            catch
            {
                // Sin equivalentes que enseñar: la casilla no se ve
            }
        }
    }
}
