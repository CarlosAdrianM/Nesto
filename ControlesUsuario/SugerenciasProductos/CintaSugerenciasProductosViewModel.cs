using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ControlesUsuario.SugerenciasProductos
{
    /// <summary>
    /// Nesto#537 (épica NestoAPI#669, API NestoAPI#670) y Nesto#544: la cinta «Puede ofrecer» con los productos que el vendedor
    /// puede ofrecer a un cliente y lo que contesta en cada ficha. La comparten la plantilla de venta y el rapport; cada pantalla
    /// solo dice qué se manda (cliente, contacto, vendedor y carrito) y, si lo hay, cómo se añade el producto («✓ Añadir»).
    /// <para>
    /// La API lo decide todo: qué sugerir, solo del grupo del vendedor, nada de lo que ya está en el carrito, y mantiene cada
    /// ficha en su hueco mientras siga valiendo. Aquí solo se pregunta con un respiro cuando cambia lo que se manda (un único
    /// temporizador que se reprograma, nunca uno continuo) y se manda lo que contesta el vendedor. Si la API falla o no hay
    /// nada que ofrecer, la cinta no se ve y no se molesta a nadie.
    /// </para>
    /// </summary>
    public class CintaSugerenciasProductosViewModel : ObservableObject
    {
        public const int RETARDO_MS = 1000;
        public const int MAXIMO_SUGERENCIAS = 4;

        private readonly Func<CalcularSugerenciasProductosDTO> _peticion;
        private readonly Func<List<ProductoCarritoSugerenciaDTO>> _carrito;
        private readonly Func<Task<string>> _leerVendedor;
        private readonly Func<SugerenciaProductoDTO, Task<bool>> _anadir;
        private readonly bool _apuntarAnadido;
        private readonly IConfiguracion _configuracion;
        private readonly ProgramadorPeticionesConHuella _peticiones;
        private readonly ObservableCollection<SugerenciaProductoDTO> _sugerencias = new ObservableCollection<SugerenciaProductoDTO>();
        private readonly HashSet<int> _respuestasEnCurso = new HashSet<int>();
        private string _clienteActual;
        private string _vendedor;
        private bool _plegadas;

        /// <param name="peticion">
        /// Lo que se manda a Calcular (empresa, cliente, contacto, vendedor y carrito), o null si todavía no hay cliente. La sesión
        /// la pone la cinta. También sirve para saber si una respuesta que llega tarde es todavía del cliente de la pantalla.
        /// </param>
        /// <param name="carrito">Lo que ya lleva el pedido (no se enseña). Null: sin carrito.</param>
        /// <param name="leerVendedor">El vendedor del usuario si la petición no lo trae (se lee una vez). Null: la API enseña todo.</param>
        /// <param name="configuracion">Para recordar por usuario si la cinta va plegada. Null: no se recuerda.</param>
        /// <param name="anadir">
        /// «✓ Añadir»: mete el producto en el pedido y devuelve true si ha entrado. Null: la pantalla no vende y el botón no sale.
        /// </param>
        /// <param name="apuntarAnadido">
        /// True (la plantilla): al entrar el producto, la cinta apunta «Anadido». False (el rapport, Nesto#544 corte 2): lo apunta
        /// la plantilla que lo recibe, con su carrito.
        /// </param>
        public CintaSugerenciasProductosViewModel(Func<CalcularSugerenciasProductosDTO> peticion,
            Func<List<ProductoCarritoSugerenciaDTO>> carrito = null, Func<Task<string>> leerVendedor = null,
            IConfiguracion configuracion = null, Func<SugerenciaProductoDTO, Task<bool>> anadir = null, bool apuntarAnadido = true)
        {
            _apuntarAnadido = apuntarAnadido;
            _peticion = peticion ?? throw new ArgumentNullException(nameof(peticion));
            _carrito = carrito;
            _leerVendedor = leerVendedor;
            _configuracion = configuracion;
            _anadir = anadir;
            _peticiones = new ProgramadorPeticionesConHuella(RETARDO_MS,
                () => Application.Current?.Dispatcher?.InvokeAsync(async () => await RefrescarAsync()));

            AnadirCommand = new RelayCommand<SugerenciaProductoDTO>(OnAnadir, s => s != null && PuedeAnadir);
            RechazarCommand = new RelayCommand<SugerenciaProductoDTO>(OnRechazar);
            QuitarCommand = new RelayCommand<SugerenciaProductoDTO>(OnQuitar);
            PlegarCommand = new RelayCommand(() => Plegadas = !Plegadas);
        }

        /// <summary>El cliente de la API (sustituible en los tests). Sin él no se pregunta nada.</summary>
        public IServicioSugerenciasProductos Servicio { get; set; }

        /// <summary>Las fichas (3, como mucho 4), en el orden de sus huecos.</summary>
        public ObservableCollection<SugerenciaProductoDTO> Sugerencias => _sugerencias;

        public bool HaySugerencias => _sugerencias.Any();

        /// <summary>La sesión de sugerencias de esta pantalla (la crea la API en la primera respuesta).</summary>
        public Guid? IdSesion { get; set; }

        /// <summary>«✓ Añadir» solo sale donde se puede vender (la plantilla). En el rapport, no (Nesto#544, corte 1).</summary>
        public bool PuedeAnadir => _anadir != null;

        /// <summary>La ayuda de «✓ Añadir»: en la plantilla entra en el pedido; desde el rapport, en una plantilla del cliente.</summary>
        public string AyudaAnadir => _apuntarAnadido
            ? "Añadir al pedido (1 unidad; luego puede cambiar la cantidad)"
            : "Añadir a la plantilla de este cliente (la que tenga abierta o una nueva), con 1 unidad";

        public RelayCommand<SugerenciaProductoDTO> AnadirCommand { get; }
        /// <summary>«✗ No le interesa»: se lo ha ofrecido y el cliente no lo quiere.</summary>
        public RelayCommand<SugerenciaProductoDTO> RechazarCommand { get; }
        /// <summary>«🚫 Quitar»: no se lo ofrezco (sé que no lo quiere o no se lo puedo vender). Sale otra en su lugar.</summary>
        public RelayCommand<SugerenciaProductoDTO> QuitarCommand { get; }
        public RelayCommand PlegarCommand { get; }

        /// <summary>La cinta plegada deja solo la cabecera. Se recuerda por usuario (parámetro SugerenciasPlantillaPlegadas).</summary>
        public bool Plegadas
        {
            get => _plegadas;
            set
            {
                if (SetProperty(ref _plegadas, value))
                {
                    OnPropertyChanged(nameof(Desplegadas));
                    OnPropertyChanged(nameof(TextoPlegar));
                    GuardarPreferenciaPlegadas(value);
                }
            }
        }

        public bool Desplegadas => !_plegadas;

        public string TextoPlegar => _plegadas ? "▸" : "▾";

        /// <summary>Cuántas veces se ha preguntado de verdad a la API (para los tests).</summary>
        public int PeticionesEnviadas => _peticiones.PeticionesEnviadas;

        /// <summary>Pregunta tras el respiro (cada llamada reinicia el reloj).</summary>
        public void Programar()
        {
            _peticiones.Programar();
        }

        /// <summary>
        /// Con otro cliente, lo de antes no vale: sesión nueva, cinta vacía y se vuelve a preguntar. Volver a elegir el mismo
        /// cliente no reinicia nada (las fichas no bailan). La clave es la que la pantalla use para el cliente (empresa|cliente).
        /// </summary>
        public void ReiniciarSiCambiaElCliente(string clave)
        {
            if (string.Equals(clave, _clienteActual, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            _clienteActual = clave;
            IdSesion = null;
            Limpiar();
            _peticiones.OlvidarHuella();
            Programar();
        }

        /// <summary>Pregunta ya, sin esperar al reloj (si ha cambiado algo). Los fallos no se propagan.</summary>
        public Task RefrescarAsync()
        {
            return _peticiones.EjecutarAsync<CalcularSugerenciasProductosDTO>(_peticion, Huella, Limpiar, PedirAlServidorAsync);
        }

        /// <summary>
        /// Solo lo que cambia la respuesta: cliente, contacto y QUÉ productos hay (no cuántos). Cambiar una cantidad no vuelve a
        /// preguntar; meter o quitar un producto, sí. La sesión no entra: si no, la propia respuesta provocaría otra petición.
        /// </summary>
        public static string Huella(CalcularSugerenciasProductosDTO peticion)
        {
            if (peticion == null)
            {
                return null;
            }
            IEnumerable<string> productos = (peticion.Productos ?? new List<ProductoCarritoSugerenciaDTO>())
                .Select(p => p.Producto?.Trim().ToUpperInvariant())
                .OrderBy(p => p, StringComparer.Ordinal);
            return string.Join("|", peticion.Empresa, peticion.Cliente, peticion.Contacto, string.Join(",", productos));
        }

        private List<ProductoCarritoSugerenciaDTO> Carrito()
        {
            return _carrito?.Invoke() ?? new List<ProductoCarritoSugerenciaDTO>();
        }

        private HashSet<string> ProductosEnCarrito()
        {
            return new HashSet<string>(Carrito().Where(p => !string.IsNullOrWhiteSpace(p.Producto)).Select(p => p.Producto.Trim()),
                StringComparer.OrdinalIgnoreCase);
        }

        private async Task PedirAlServidorAsync(CalcularSugerenciasProductosDTO peticion)
        {
            if (Servicio == null)
            {
                return;
            }
            peticion.IdSesion = IdSesion;
            if (string.IsNullOrWhiteSpace(peticion.Vendedor))
            {
                peticion.Vendedor = await VendedorAsync().ConfigureAwait(true);
            }
            SugerenciasProductosDTO resultado;
            try
            {
                resultado = await Servicio.Calcular(peticion).ConfigureAwait(true);
            }
            catch (Exception)
            {
                resultado = null;
            }
            // Si mientras tanto han cambiado de cliente, esta respuesta es de otro
            CalcularSugerenciasProductosDTO ahora = _peticion();
            if (ahora == null || !string.Equals(ahora.Cliente?.Trim(), peticion.Cliente?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            if (resultado == null)
            {
                Limpiar(); // la API no contesta: la cinta no se ve y la pantalla sigue igual
                return;
            }
            if (resultado.IdSesion != Guid.Empty)
            {
                IdSesion = resultado.IdSesion;
            }
            Aplicar(resultado.Sugerencias);
        }

        /// <summary>El vendedor del usuario, leído una vez. Vacío si no se puede leer: la API enseña todo.</summary>
        private async Task<string> VendedorAsync()
        {
            if (_vendedor == null)
            {
                try
                {
                    _vendedor = _leerVendedor == null ? string.Empty : ((await _leerVendedor().ConfigureAwait(true)) ?? string.Empty).Trim();
                }
                catch (Exception)
                {
                    _vendedor = string.Empty;
                }
            }
            return _vendedor;
        }

        /// <summary>
        /// Pone las fichas que manda la API, en el orden de sus huecos, sin las que ya estén en el carrito (por si se ha metido
        /// a mano mientras se preguntaba). Si son las mismas que ya se ven, no se toca nada.
        /// </summary>
        public void Aplicar(IEnumerable<SugerenciaProductoDTO> sugerencias)
        {
            HashSet<string> enCarrito = ProductosEnCarrito();
            List<SugerenciaProductoDTO> nuevas = (sugerencias ?? Enumerable.Empty<SugerenciaProductoDTO>())
                .Where(s => s != null && !string.IsNullOrWhiteSpace(s.Producto) && !enCarrito.Contains(s.Producto.Trim()))
                .OrderBy(s => s.Posicion)
                .Take(MAXIMO_SUGERENCIAS)
                .ToList();
            if (nuevas.Select(s => s.IdSugerencia).SequenceEqual(_sugerencias.Select(s => s.IdSugerencia)))
            {
                return;
            }
            _sugerencias.Clear();
            foreach (SugerenciaProductoDTO sugerencia in nuevas)
            {
                _sugerencias.Add(sugerencia);
            }
            OnPropertyChanged(nameof(HaySugerencias));
        }

        /// <summary>Vacía la cinta (deja de verse).</summary>
        public void Limpiar()
        {
            if (_sugerencias.Any())
            {
                _sugerencias.Clear();
            }
            OnPropertyChanged(nameof(HaySugerencias));
        }

        private async void OnAnadir(SugerenciaProductoDTO sugerencia)
        {
            try
            {
                await AnadirAsync(sugerencia);
            }
            catch (Exception)
            {
                // Lo que falle al meterlo en el pedido lo cuenta la pantalla (su delegado); esto es una ayuda
            }
        }

        private async void OnRechazar(SugerenciaProductoDTO sugerencia)
        {
            try
            {
                await RechazarAsync(sugerencia);
            }
            catch (Exception)
            {
                // Es una ayuda: si falla, la ficha se queda y no se molesta al vendedor
            }
        }

        private async void OnQuitar(SugerenciaProductoDTO sugerencia)
        {
            try
            {
                await QuitarAsync(sugerencia);
            }
            catch (Exception)
            {
                // Es una ayuda: si falla, la ficha se queda y no se molesta al vendedor
            }
        }

        /// <summary>
        /// «✓ Añadir»: la pantalla mete el producto en el pedido (por su misma ruta que si lo tecleara el vendedor). Si entra, la
        /// ficha sale de la cinta, se apunta «Anadido» y, como el carrito ha cambiado, su hueco se rellena al volver a preguntar.
        /// </summary>
        public async Task AnadirAsync(SugerenciaProductoDTO sugerencia)
        {
            if (sugerencia == null || _anadir == null || !_respuestasEnCurso.Add(sugerencia.IdSugerencia))
            {
                return;
            }
            try
            {
                if (!await _anadir(sugerencia).ConfigureAwait(true))
                {
                    return;
                }
                if (_sugerencias.Remove(sugerencia))
                {
                    OnPropertyChanged(nameof(HaySugerencias));
                }
                if (_apuntarAnadido)
                {
                    await ResponderAsync(sugerencia.IdSugerencia, RespuestasSugerenciaProducto.ANADIDO).ConfigureAwait(true);
                }
            }
            finally
            {
                _respuestasEnCurso.Remove(sugerencia.IdSugerencia);
            }
        }

        /// <summary>
        /// Nesto#544 (corte 2): apunta «Anadido» de una sugerencia que llega de otra pantalla (el rapport), con el carrito de
        /// esta. La sesión es la de la cinta (quien la llama ya se ha quedado con la de origen). Los fallos no se propagan.
        /// </summary>
        public async Task ApuntarAnadidoAsync(int idSugerencia)
        {
            await ResponderAsync(idSugerencia, RespuestasSugerenciaProducto.ANADIDO).ConfigureAwait(true);
        }

        /// <summary>Nesto#544 (corte 2): quita la ficha de un producto que ya ha entrado en el pedido por otro camino.</summary>
        public void OlvidarProducto(string producto)
        {
            if (string.IsNullOrWhiteSpace(producto))
            {
                return;
            }
            bool quitada = false;
            foreach (SugerenciaProductoDTO ficha in _sugerencias.Where(s => string.Equals(s.Producto?.Trim(), producto.Trim(), StringComparison.OrdinalIgnoreCase)).ToList())
            {
                quitada |= _sugerencias.Remove(ficha);
            }
            if (quitada)
            {
                OnPropertyChanged(nameof(HaySugerencias));
            }
        }

        /// <summary>
        /// «✗ No le interesa»: se apunta el rechazo y la sustituta que manda la API ocupa el mismo hueco. Si no hay sustituta
        /// (o la API no contesta), la ficha simplemente se va.
        /// </summary>
        public Task RechazarAsync(SugerenciaProductoDTO sugerencia)
        {
            return SustituirAsync(sugerencia, RespuestasSugerenciaProducto.NO_LE_INTERESA);
        }

        /// <summary>
        /// «🚫 Quitar» (no se lo ofrezco: sé que no lo quiere o no se lo puedo vender): igual que «✗ No le interesa» en pantalla
        /// (sale otra en su hueco y no se le vuelve a sugerir a este cliente en 60 días), pero la API lo registra aparte
        /// («DescartadoVendedor»): no es un rechazo del cliente.
        /// </summary>
        public Task QuitarAsync(SugerenciaProductoDTO sugerencia)
        {
            return SustituirAsync(sugerencia, RespuestasSugerenciaProducto.DESCARTADO_VENDEDOR);
        }

        /// <summary>La ficha se va y, si la API manda sustituta (y vale), esta ocupa el mismo hueco.</summary>
        private async Task SustituirAsync(SugerenciaProductoDTO sugerencia, string respuesta)
        {
            if (sugerencia == null || !_respuestasEnCurso.Add(sugerencia.IdSugerencia))
            {
                return;
            }
            try
            {
                ResultadoRespuestaSugerenciaProductoDTO resultado = await ResponderAsync(sugerencia.IdSugerencia, respuesta).ConfigureAwait(true);
                int indice = _sugerencias.IndexOf(sugerencia);
                if (indice < 0)
                {
                    return; // mientras tanto se ha recalculado la cinta
                }
                SugerenciaProductoDTO sustituta = resultado?.Sustituta;
                HashSet<string> enCarrito = ProductosEnCarrito();
                bool valeLaSustituta = sustituta != null && !string.IsNullOrWhiteSpace(sustituta.Producto) &&
                    !enCarrito.Contains(sustituta.Producto.Trim()) &&
                    !_sugerencias.Any(s => s.IdSugerencia == sustituta.IdSugerencia ||
                        string.Equals(s.Producto?.Trim(), sustituta.Producto.Trim(), StringComparison.OrdinalIgnoreCase));
                if (valeLaSustituta)
                {
                    _sugerencias[indice] = sustituta;
                }
                else
                {
                    _sugerencias.RemoveAt(indice);
                }
                OnPropertyChanged(nameof(HaySugerencias));
            }
            finally
            {
                _respuestasEnCurso.Remove(sugerencia.IdSugerencia);
            }
        }

        private async Task<ResultadoRespuestaSugerenciaProductoDTO> ResponderAsync(int idSugerencia, string respuesta)
        {
            if (Servicio == null)
            {
                return null;
            }
            try
            {
                return await Servicio.Responder(idSugerencia, respuesta, Carrito()).ConfigureAwait(true);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>"1" = plegada. Sin fila, otro valor o si no se puede leer: desplegada.</summary>
        public async void CargarPreferenciaPlegadas()
        {
            if (_configuracion == null)
            {
                return;
            }
            try
            {
                string valor = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.SugerenciasPlantillaPlegadas);
                if (SetProperty(ref _plegadas, valor?.Trim() == "1", nameof(Plegadas)))
                {
                    OnPropertyChanged(nameof(Desplegadas));
                    OnPropertyChanged(nameof(TextoPlegar));
                }
            }
            catch (Exception)
            {
                // Sin preferencia, desplegada
            }
        }

        private async void GuardarPreferenciaPlegadas(bool plegadas)
        {
            if (_configuracion == null)
            {
                return;
            }
            try
            {
                await _configuracion.GuardarParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.SugerenciasPlantillaPlegadas, plegadas ? "1" : "0");
            }
            catch (Exception)
            {
                // Es una comodidad: si no se puede guardar, la próxima vez sale desplegada
            }
        }
    }
}
