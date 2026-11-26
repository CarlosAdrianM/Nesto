using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlesUsuario.Dialogs;
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
    /// NestoAPI#876 (corte nuevo, Carlos 26/11/26): los bloques que se pueden copiar del cliente anterior al nuevo cuando un cliente
    /// cambia de forma fiscal, con lo que copia cada uno. Los códigos son los de la API (BloquesCambioFiscal).
    /// </summary>
    public static class BloquesCambioFiscal
    {
        public const string COMERCIAL = "Comercial";
        public const string FISCALIDAD = "Fiscalidad";
        public const string CONDICIONES_PAGO = "CondicionesPago";
        public const string DESCUENTOS = "Descuentos";
        public const string OFERTAS = "Ofertas";
        public const string FICHA = "Ficha";

        public static readonly IReadOnlyList<string> TODOS = new[] { COMERCIAL, FISCALIDAD, CONDICIONES_PAGO, DESCUENTOS, OFERTAS, FICHA };

        public static string Nombre(string bloque) => bloque switch
        {
            COMERCIAL => "Estado y vendedores",
            FISCALIDAD => "Tipo de IVA",
            CONDICIONES_PAGO => "Forma y plazos de pago",
            DESCUENTOS => "Descuentos pactados",
            OFERTAS => "Ofertas permitidas",
            FICHA => "Comentarios, días de servir, ruta y facturación",
            _ => bloque
        };

        /// <summary>Una línea de lo que copia cada bloque (para la casilla).</summary>
        public static string Descripcion(string bloque) => bloque switch
        {
            COMERCIAL => "El estado (salvo que el anterior esté de baja), el vendedor y los vendedores por grupo de producto.",
            FISCALIDAD => "Solo el tipo de IVA o el recargo de equivalencia de la ficha. Nunca el NIF ni el nombre fiscal.",
            CONDICIONES_PAGO => "Las formas y plazos de pago por importe. Ni la cuenta bancaria ni el mandato: esos hay que darlos de nuevo.",
            DESCUENTOS => "Los descuentos y precios especiales del cliente, sin los caducados ni los que el nuevo ya tenga iguales.",
            OFERTAS => "Las ofertas permitidas propias del cliente, sin las caducadas.",
            FICHA => "Comentarios (si el nuevo no tiene), días de servir, ruta, periodo de facturación, «no comisiona» y «mantener junto».",
            _ => string.Empty
        };
    }

    /// <summary>NestoAPI#876: LO QUE DECIDE LA PANTALLA (puro). Las reglas de verdad están en la API.</summary>
    public static class ReglasPantallaCambioFiscal
    {
        public const string MOTIVO_NINGUN_BLOQUE = "Marque al menos un bloque para copiar.";
        public const string SIN_PEDIDOS = "El cliente anterior no tiene pedidos pendientes.";
        private static readonly CultureInfo ES = CultureInfo.GetCultureInfo("es-ES");

        /// <summary>
        /// Lo que viaja en el alta (<c>CopiarDelAnterior</c>): los bloques marcados, en su orden; ninguno = null (no copiar). Nunca una
        /// lista vacía: para la API es «todos».
        /// </summary>
        public static List<string> CopiarDelAnterior(IEnumerable<FilaBloqueCambioFiscal> filas)
        {
            List<string> marcados = (filas ?? Enumerable.Empty<FilaBloqueCambioFiscal>()).Where(f => f != null && f.Marcado).Select(f => f.Bloque).ToList();
            return marcados.Any() ? marcados : null;
        }

        /// <summary>Lo copiado y los avisos, para enseñarlo tras el alta. Vacío si no hay nada.</summary>
        public static string TextoCopiaEnElAlta(IEnumerable<string> copiado, string aviso)
        {
            List<string> lineas = (copiado ?? Enumerable.Empty<string>()).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => "· " + c.Trim()).ToList();
            string texto = lineas.Any() ? "\n\nCopiado del cliente anterior:\n" + string.Join("\n", lineas) : string.Empty;
            if (!string.IsNullOrWhiteSpace(aviso))
            {
                texto += "\n\n" + aviso.Trim();
            }
            return texto;
        }

        /// <summary>El resumen de una copia de verdad (la respuesta de Copiar).</summary>
        public static string TextoResultadoCopia(CopiaCambioFiscalModel copia)
        {
            if (copia == null)
            {
                return "No se ha copiado nada.";
            }
            List<string> cambios = (copia.Bloques ?? new List<BloqueCambioFiscalModel>()).SelectMany(b => b.Cambios ?? new List<string>()).ToList();
            List<string> avisos = (copia.Bloques ?? new List<BloqueCambioFiscalModel>()).SelectMany(b => b.Avisos ?? new List<string>())
                .Concat(copia.Avisos ?? new List<string>()).Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
            string texto = cambios.Any()
                ? "Se ha copiado del cliente anterior:\n" + string.Join("\n", cambios.Select(c => "· " + c))
                : "No había nada que copiar: el cliente nuevo ya lo tenía igual.";
            if (avisos.Any())
            {
                texto += "\n\n" + string.Join("\n", avisos);
            }
            if (!string.IsNullOrWhiteSpace(copia.Mensaje))
            {
                texto += "\n\n" + copia.Mensaje.Trim();
            }
            return texto;
        }

        /// <summary>«931691 · contacto 0 · 26/11/2026 · 3 líneas · 123,45 € (con picking 100123)».</summary>
        public static string TextoPedido(PedidoPendienteCambioFiscalModel p)
        {
            if (p == null)
            {
                return string.Empty;
            }
            var partes = new List<string> { p.Pedido.ToString(ES) };
            if (p.NotaEntrega)
            {
                partes[0] += " (nota de entrega)";
            }
            if (!string.IsNullOrWhiteSpace(p.Contacto))
            {
                partes.Add("contacto " + p.Contacto.Trim());
            }
            if (p.Fecha.HasValue)
            {
                partes.Add(p.Fecha.Value.ToString("dd/MM/yyyy", ES));
            }
            partes.Add(p.Lineas == 1 ? "1 línea" : p.Lineas.ToString(ES) + " líneas");
            partes.Add(p.Importe.ToString("N2", ES) + " €");
            string texto = string.Join(" · ", partes);
            if (p.TienePicking)
            {
                List<int> pickings = p.Pickings ?? new List<int>();
                texto += pickings.Any() ? $" (con picking {string.Join(", ", pickings)})" : " (con picking)";
            }
            return texto;
        }

        /// <summary>El resumen de PasarPedidos de verdad.</summary>
        public static string TextoResultadoPasar(PasarPedidosCambioFiscalModel resultado, string clienteNuevo)
        {
            List<PedidoPasadoCambioFiscalModel> pedidos = resultado?.Pedidos ?? new List<PedidoPasadoCambioFiscalModel>();
            List<int> pasados = pedidos.Where(p => p.Pasado).Select(p => p.Pedido).ToList();
            List<PedidoPasadoCambioFiscalModel> rechazados = pedidos.Where(p => !p.Pasado).ToList();
            string texto = pasados.Count switch
            {
                0 => "No se ha pasado ningún pedido.",
                1 => $"Se ha pasado el pedido {pasados[0]} al cliente {clienteNuevo?.Trim()}.",
                _ => $"Se han pasado los pedidos {string.Join(", ", pasados)} al cliente {clienteNuevo?.Trim()}."
            };
            if (rechazados.Any())
            {
                texto += "\n\nNo se han podido pasar:\n" + string.Join("\n", rechazados.Select(r => $"· {r.Pedido}: {r.Mensaje}"));
            }
            return texto;
        }
    }

    /// <summary>NestoAPI#876: la casilla de un bloque (en el alta, solo el nombre y lo que copia; en la ficha, también lo que cambiaría).</summary>
    public class FilaBloqueCambioFiscal : ObservableObject
    {
        public FilaBloqueCambioFiscal(string bloque, bool marcado = true)
        {
            Bloque = bloque;
            Nombre = BloquesCambioFiscal.Nombre(bloque);
            Descripcion = BloquesCambioFiscal.Descripcion(bloque);
            _marcado = marcado;
        }

        public string Bloque { get; }
        public string Nombre { get; set; }
        public string Descripcion { get; }

        private bool _marcado;
        public bool Marcado { get => _marcado; set => SetProperty(ref _marcado, value); }

        public string Anterior { get; set; }
        public string Nuevo { get; set; }
        public string Cambios { get; set; }
        public string Avisos { get; set; }
        public bool HayCambios { get; set; }

        /// <summary>Desde la previa: lo que tiene cada uno, lo que cambiaría y los avisos. Marcada si hay algo que copiar.</summary>
        public static FilaBloqueCambioFiscal DeLaPrevia(BloqueCambioFiscalModel b)
        {
            var cambios = b?.Cambios ?? new List<string>();
            var fila = new FilaBloqueCambioFiscal(b?.Bloque, cambios.Any())
            {
                Anterior = Unir(b?.Anterior),
                Nuevo = Unir(b?.Nuevo),
                Cambios = cambios.Any() ? Unir(cambios) : "Nada que copiar: el cliente nuevo ya lo tiene igual.",
                Avisos = Unir(b?.Avisos),
                HayCambios = cambios.Any()
            };
            if (!string.IsNullOrWhiteSpace(b?.Nombre))
            {
                fila.Nombre = b.Nombre.Trim();
            }
            return fila;
        }

        private static string Unir(IEnumerable<string> lineas)
        {
            List<string> l = (lineas ?? Enumerable.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            return l.Any() ? string.Join("\n", l) : null;
        }
    }

    /// <summary>NestoAPI#876: un pedido pendiente del anterior, con su casilla (sin casilla si no se puede pasar, con el porqué).</summary>
    public class FilaPedidoCambioFiscal : ObservableObject
    {
        public FilaPedidoCambioFiscal(PedidoPendienteCambioFiscalModel pedido)
        {
            Pedido = pedido?.Pedido ?? 0;
            Texto = ReglasPantallaCambioFiscal.TextoPedido(pedido);
            _sePuedeMarcar = pedido?.SePuedePasar == true;
            _marcado = _sePuedeMarcar;
            _motivo = string.IsNullOrWhiteSpace(pedido?.Motivo) ? null : pedido.Motivo.Trim();
        }

        public int Pedido { get; }
        public string Texto { get; }

        private bool _marcado;
        public bool Marcado { get => _marcado; set => SetProperty(ref _marcado, value && SePuedeMarcar); }

        private bool _sePuedeMarcar;
        public bool SePuedeMarcar
        {
            get => _sePuedeMarcar;
            set
            {
                if (SetProperty(ref _sePuedeMarcar, value) && !value)
                {
                    Marcado = false;
                }
            }
        }

        private string _motivo;
        /// <summary>Por qué no se puede pasar, lo que cambiaría (ensayo) o cómo ha ido.</summary>
        public string Motivo { get => _motivo; set => SetProperty(ref _motivo, value); }
    }

    /// <summary>
    /// NestoAPI#876: pasar los pedidos pendientes del anterior al nuevo. Primero el ensayo (qué cambiaría en cada uno; los de picking
    /// que cambiarían se quedan sin casilla, con su motivo) y después, los marcados. Lo usan la ficha y el diálogo del alta.
    /// </summary>
    public class PasoPedidosCambioFiscal : ObservableObject
    {
        private readonly IServicioCambioFiscal _servicio;
        private readonly Func<string> _usuario;

        public PasoPedidosCambioFiscal(IServicioCambioFiscal servicio, Func<string> usuario = null)
        {
            _servicio = servicio;
            _usuario = usuario;
            PasarCommand = new AsyncRelayCommand(PasarAsync, () => PuedePasar);
        }

        public string Empresa { get; private set; }
        public string ClienteAnterior { get; private set; }
        public string ClienteNuevo { get; private set; }

        public ObservableCollection<FilaPedidoCambioFiscal> Filas { get; } = new();
        public AsyncRelayCommand PasarCommand { get; }

        public bool HayPedidos => Filas.Any();

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    PasarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private bool _ensayoHecho;
        /// <summary>Sin ensayo (falló) no se pasa nada: no se sabría qué cambia en los de picking.</summary>
        public bool EnsayoHecho
        {
            get => _ensayoHecho;
            private set
            {
                if (SetProperty(ref _ensayoHecho, value))
                {
                    PasarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _mensaje;
        public string Mensaje { get => _mensaje; private set => SetProperty(ref _mensaje, value); }

        private bool _pasados;
        /// <summary>Ya se ha hecho el paso de verdad.</summary>
        public bool Pasados
        {
            get => _pasados;
            private set
            {
                if (SetProperty(ref _pasados, value))
                {
                    PasarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool PuedePasar => EnsayoHecho && !EstaOcupado && !Pasados && Filas.Any(f => f.Marcado);

        /// <summary>Pone los pedidos y hace el ensayo. Nunca lanza: lo que falle va a <see cref="Mensaje"/>.</summary>
        public async Task CargarAsync(string empresa, string clienteAnterior, string clienteNuevo, IEnumerable<PedidoPendienteCambioFiscalModel> pendientes)
        {
            Empresa = empresa?.Trim();
            ClienteAnterior = clienteAnterior?.Trim();
            ClienteNuevo = clienteNuevo?.Trim();
            foreach (FilaPedidoCambioFiscal vieja in Filas)
            {
                vieja.PropertyChanged -= AlCambiarFila;
            }
            Filas.Clear();
            foreach (PedidoPendienteCambioFiscalModel p in (pendientes ?? Enumerable.Empty<PedidoPendienteCambioFiscalModel>()).Where(p => p != null))
            {
                var fila = new FilaPedidoCambioFiscal(p);
                fila.PropertyChanged += AlCambiarFila;
                Filas.Add(fila);
            }
            OnPropertyChanged(nameof(HayPedidos));
            Pasados = false;
            EnsayoHecho = false;
            Mensaje = Filas.Any() ? null : ReglasPantallaCambioFiscal.SIN_PEDIDOS;
            List<int> aEnsayar = Filas.Where(f => f.SePuedeMarcar).Select(f => f.Pedido).ToList();
            if (!aEnsayar.Any())
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                PasarPedidosCambioFiscalModel ensayo = await _servicio.PasarPedidos(Peticion(aEnsayar), ensayo: true);
                foreach (PedidoPasadoCambioFiscalModel r in ensayo?.Pedidos ?? new List<PedidoPasadoCambioFiscalModel>())
                {
                    FilaPedidoCambioFiscal fila = Filas.FirstOrDefault(f => f.Pedido == r.Pedido);
                    if (fila == null)
                    {
                        continue;
                    }
                    if (!r.Pasado)
                    {
                        fila.SePuedeMarcar = false; // p. ej. con picking y cambiaría al recalcular
                        fila.Motivo = string.IsNullOrWhiteSpace(r.Mensaje) ? "No se puede pasar." : r.Mensaje.Trim();
                    }
                    else
                    {
                        List<string> cambios = (r.Cambios ?? new List<string>()).Concat(r.Avisos ?? new List<string>()).Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
                        fila.Motivo = cambios.Any() ? "Al pasarlo: " + string.Join("; ", cambios) : (r.SinCambiosDeImportes ? "Al pasarlo no cambia ningún importe." : fila.Motivo);
                    }
                }
                EnsayoHecho = true;
            }
            catch (Exception ex)
            {
                Mensaje = "No se ha podido comprobar qué cambiaría al pasar los pedidos: " + ex.Message;
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        private void AlCambiarFila(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FilaPedidoCambioFiscal.Marcado))
            {
                PasarCommand.NotifyCanExecuteChanged();
            }
        }

        private PasarPedidosCambioFiscalPeticion Peticion(List<int> pedidos) => new()
        {
            Empresa = Empresa,
            ClienteAnterior = ClienteAnterior,
            ClienteNuevo = ClienteNuevo,
            Pedidos = pedidos,
            Usuario = _usuario?.Invoke()
        };

        /// <summary>Pasa los marcados de verdad. Nunca lanza.</summary>
        internal async Task PasarAsync()
        {
            List<int> marcados = Filas.Where(f => f.Marcado).Select(f => f.Pedido).ToList();
            if (!marcados.Any() || !EnsayoHecho)
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                PasarPedidosCambioFiscalModel resultado = await _servicio.PasarPedidos(Peticion(marcados), ensayo: false);
                foreach (PedidoPasadoCambioFiscalModel r in resultado?.Pedidos ?? new List<PedidoPasadoCambioFiscalModel>())
                {
                    FilaPedidoCambioFiscal fila = Filas.FirstOrDefault(f => f.Pedido == r.Pedido);
                    if (fila == null)
                    {
                        continue;
                    }
                    fila.SePuedeMarcar = false;
                    fila.Motivo = r.Pasado ? $"Pasado al cliente {ClienteNuevo}." : (string.IsNullOrWhiteSpace(r.Mensaje) ? "No se ha podido pasar." : r.Mensaje.Trim());
                }
                Mensaje = ReglasPantallaCambioFiscal.TextoResultadoPasar(resultado, ClienteNuevo);
                Pasados = true;
            }
            catch (Exception ex)
            {
                Mensaje = ex.Message;
            }
            finally
            {
                EstaOcupado = false;
            }
        }
    }

    /// <summary>
    /// NestoAPI#876: en la ficha del cliente (si tiene «Cliente anterior»): «Copiar del cliente anterior…» (previa por bloques y copiar los
    /// marcados) y «Pasar pedidos pendientes…». Los 403/409 de la API se enseñan con su mensaje.
    /// </summary>
    public class CambioFiscalClienteViewModel : ObservableObject
    {
        private readonly IServicioCambioFiscal _servicio;
        private readonly IServicioDialogos _dialogos;
        private readonly IConfiguracion _configuracion;
        private List<PedidoPendienteCambioFiscalModel> _pendientesDeLaPrevia;

        public CambioFiscalClienteViewModel(IServicioCambioFiscal servicio, IServicioDialogos dialogos, IConfiguracion configuracion)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            _configuracion = configuracion;
            Pedidos = new PasoPedidosCambioFiscal(servicio, () => _configuracion?.usuario);
            Pedidos.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PasoPedidosCambioFiscal.EstaOcupado))
                {
                    NotificarComandos();
                }
            };
            VerCopiaCommand = new AsyncRelayCommand(VerCopiaAsync, () => Visible && !EstaOcupado);
            CopiarCommand = new AsyncRelayCommand(CopiarAsync, () => Visible && !EstaOcupado && Bloques.Any() && SePuedeCopiar);
            VerPedidosCommand = new AsyncRelayCommand(VerPedidosAsync, () => Visible && !EstaOcupado && !Pedidos.EstaOcupado);
        }

        public string Empresa { get; private set; }
        public string Cliente { get; private set; }

        private string _clienteAnterior;
        public string ClienteAnterior { get => _clienteAnterior; private set => SetProperty(ref _clienteAnterior, value); }

        /// <summary>Solo con cliente anterior.</summary>
        public bool Visible => !string.IsNullOrWhiteSpace(ClienteAnterior) && !string.IsNullOrWhiteSpace(Cliente);

        public ObservableCollection<FilaBloqueCambioFiscal> Bloques { get; } = new();
        public PasoPedidosCambioFiscal Pedidos { get; }

        public AsyncRelayCommand VerCopiaCommand { get; }
        public AsyncRelayCommand CopiarCommand { get; }
        public AsyncRelayCommand VerPedidosCommand { get; }

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

        private bool _sePuedeCopiar = true;
        public bool SePuedeCopiar
        {
            get => _sePuedeCopiar;
            private set
            {
                if (SetProperty(ref _sePuedeCopiar, value))
                {
                    NotificarComandos();
                }
            }
        }

        private string _mensaje;
        public string Mensaje { get => _mensaje; private set => SetProperty(ref _mensaje, value); }

        private bool _mostrandoCopia;
        public bool MostrandoCopia { get => _mostrandoCopia; private set => SetProperty(ref _mostrandoCopia, value); }

        private bool _mostrandoPedidos;
        public bool MostrandoPedidos { get => _mostrandoPedidos; private set => SetProperty(ref _mostrandoPedidos, value); }

        /// <summary>El cliente de la ficha (al enseñarse o al cambiar de cliente). No llama a la API.</summary>
        public void Cargar(string empresa, string cliente, string clienteAnterior)
        {
            Empresa = string.IsNullOrWhiteSpace(empresa) ? "1" : empresa.Trim();
            Cliente = cliente?.Trim();
            ClienteAnterior = string.IsNullOrWhiteSpace(clienteAnterior) ? null : clienteAnterior.Trim();
            Bloques.Clear();
            _pendientesDeLaPrevia = null;
            Mensaje = null;
            SePuedeCopiar = true;
            MostrandoCopia = false;
            MostrandoPedidos = false;
            OnPropertyChanged(nameof(Visible));
            NotificarComandos();
        }

        private void NotificarComandos()
        {
            VerCopiaCommand?.NotifyCanExecuteChanged();
            CopiarCommand?.NotifyCanExecuteChanged();
            VerPedidosCommand?.NotifyCanExecuteChanged();
        }

        private async Task<CopiaCambioFiscalModel> LeerPreviaAsync()
        {
            CopiaCambioFiscalModel previa = await _servicio.Previa(Empresa, ClienteAnterior, Cliente);
            _pendientesDeLaPrevia = previa?.PedidosPendientes ?? new List<PedidoPendienteCambioFiscalModel>();
            return previa;
        }

        /// <summary>«Copiar del cliente anterior…»: la previa por bloques (marcados los que tienen algo que copiar).</summary>
        internal async Task VerCopiaAsync()
        {
            if (!Visible)
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                CopiaCambioFiscalModel previa = await LeerPreviaAsync();
                PonerBloques(previa);
                MostrandoCopia = true;
            }
            catch (Exception ex)
            {
                Mensaje = ex.Message;
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        private void PonerBloques(CopiaCambioFiscalModel previa)
        {
            Bloques.Clear();
            foreach (BloqueCambioFiscalModel b in previa?.Bloques ?? new List<BloqueCambioFiscalModel>())
            {
                Bloques.Add(FilaBloqueCambioFiscal.DeLaPrevia(b));
            }
            SePuedeCopiar = string.IsNullOrWhiteSpace(previa?.MotivoNoSePuedeCopiar);
            List<string> mensaje = new();
            if (!SePuedeCopiar)
            {
                mensaje.Add(previa.MotivoNoSePuedeCopiar.Trim());
            }
            else if (!Bloques.Any(b => b.HayCambios))
            {
                mensaje.Add("No hay nada que copiar: el cliente nuevo ya tiene lo mismo que el anterior.");
            }
            if (!string.IsNullOrWhiteSpace(previa?.Mensaje))
            {
                mensaje.Add(previa.Mensaje.Trim());
            }
            mensaje.AddRange((previa?.Avisos ?? new List<string>()).Where(a => !string.IsNullOrWhiteSpace(a)));
            Mensaje = mensaje.Any() ? string.Join("\n", mensaje) : null;
            NotificarComandos();
        }

        /// <summary>Copia los bloques marcados (con confirmación) y vuelve a leer la previa.</summary>
        internal async Task CopiarAsync()
        {
            List<string> marcados = ReglasPantallaCambioFiscal.CopiarDelAnterior(Bloques);
            if (marcados == null)
            {
                _dialogos.ShowError(ReglasPantallaCambioFiscal.MOTIVO_NINGUN_BLOQUE);
                return;
            }
            string nombres = string.Join(", ", Bloques.Where(b => b.Marcado).Select(b => b.Nombre));
            if (!_dialogos.ShowConfirmationAnswer("Copiar del cliente anterior",
                $"Se va a copiar del cliente {ClienteAnterior} al {Cliente}: {nombres}. ¿Desea continuar?"))
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                CopiaCambioFiscalModel copia = await _servicio.Copiar(new CopiaCambioFiscalPeticion
                {
                    Empresa = Empresa,
                    ClienteAnterior = ClienteAnterior,
                    ClienteNuevo = Cliente,
                    Bloques = marcados,
                    Usuario = _configuracion?.usuario
                }, ensayo: false);
                _dialogos.ShowNotification("Copiar del cliente anterior", ReglasPantallaCambioFiscal.TextoResultadoCopia(copia));
                PonerBloques(await LeerPreviaAsync()); // ya copiado: lo que queda
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

        /// <summary>«Pasar pedidos pendientes…»: la lista con el ensayo.</summary>
        internal async Task VerPedidosAsync()
        {
            if (!Visible)
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                if (_pendientesDeLaPrevia == null)
                {
                    await LeerPreviaAsync();
                }
            }
            catch (Exception ex)
            {
                Mensaje = ex.Message;
                EstaOcupado = false;
                return;
            }
            EstaOcupado = false;
            MostrandoPedidos = true;
            await Pedidos.CargarAsync(Empresa, ClienteAnterior, Cliente, _pendientesDeLaPrevia);
            _pendientesDeLaPrevia = null; // la próxima vez se vuelve a leer (ya pueden haberse pasado)
        }
    }

    /// <summary>
    /// NestoAPI#876: tras el alta de un cliente que cambia de forma fiscal, si el anterior tiene pedidos pendientes se pregunta si se
    /// pasan al nuevo. Parámetros: «empresa», «clienteAnterior», «clienteNuevo», «pedidos» (List&lt;PedidoPendienteCambioFiscalModel&gt;).
    /// </summary>
    public class CambioFiscalPedidosDialogViewModel : DialogoNestoBase
    {
        public const string NOMBRE = "CambioFiscalPedidosDialog";

        public CambioFiscalPedidosDialogViewModel(IServicioCambioFiscal servicio, IConfiguracion configuracion)
        {
            Title = "Pasar los pedidos pendientes al cliente nuevo";
            Paso = new PasoPedidosCambioFiscal(servicio, () => configuracion?.usuario);
            CerrarCommand = new RelayCommand(() => RaiseRequestClose(Paso.Pasados ? ResultadoBoton.OK : ResultadoBoton.Cancel));
        }

        public PasoPedidosCambioFiscal Paso { get; }
        public RelayCommand CerrarCommand { get; }

        private string _texto;
        public string Texto { get => _texto; private set => SetProperty(ref _texto, value); }

        /// <summary>Para las pruebas: la carga lanzada al abrir.</summary>
        internal Task CargaEnCurso { get; private set; } = Task.CompletedTask;

        public override void OnDialogOpened(ParametrosDialogo parameters)
        {
            string empresa = parameters.GetValue<string>("empresa");
            string anterior = parameters.GetValue<string>("clienteAnterior");
            string nuevo = parameters.GetValue<string>("clienteNuevo");
            var pedidos = parameters.GetValue<List<PedidoPendienteCambioFiscalModel>>("pedidos");
            Texto = $"El cliente anterior {anterior?.Trim()} tiene pedidos pendientes. Marque los que quiere pasar al cliente {nuevo?.Trim()}: " +
                    "se recalculan con las condiciones del cliente nuevo. Los que tienen picking solo se pasan si no cambia nada.";
            CargaEnCurso = Paso.CargarAsync(empresa, anterior, nuevo, pedidos);
        }
    }
}
