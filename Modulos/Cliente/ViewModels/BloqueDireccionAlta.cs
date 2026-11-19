using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.VisualBasic;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cliente.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Una dirección del alta de un cliente: la fiscal o la de entrega (19/11/26: «La entrega es en otra dirección»). El mismo núcleo para las
    /// dos: buscador de Google (Nesto#409), dirección elegida y no tecleada (Nesto#480), CP de Google (novedad 482 y corte 1 del 19/11/26),
    /// número propio y «¿Qué es?» (Nesto#643, NestoAPI#874), y la validación contra la API (ValidarDatosGenerales). Lo que solo es de la
    /// fiscal (vendedores, NIF, pasos del asistente) lo hace CrearClienteViewModel con lo que este bloque le devuelve.
    /// </summary>
    public class BloqueDireccionAlta : ObservableObject
    {
        private readonly IClienteService servicio;
        private readonly Func<bool> esAlta;
        private readonly string prefijoMotivo;

        /// <param name="esAlta">Si se está creando (no modificando): cambia la regla del código postal y si se pide «¿Qué es?».</param>
        /// <param name="direccionObligatoria">La de entrega: si se ha marcado, tiene que tener dirección (la fiscal puede no tenerla: contactos de cobro).</param>
        /// <param name="prefijoMotivo">Con qué empieza lo que le falta («Dirección de entrega: »), para saber de qué dirección se habla.</param>
        public BloqueDireccionAlta(IClienteService servicio, Func<bool> esAlta, bool direccionObligatoria = false, string prefijoMotivo = null)
        {
            this.servicio = servicio;
            this.esAlta = esAlta ?? (() => true);
            DireccionObligatoria = direccionObligatoria;
            this.prefijoMotivo = prefijoMotivo;
            LimpiarDireccionCommand = new RelayCommand(() => Direccion = string.Empty);
            LimpiarDireccionCalleNumeroCommand = new RelayCommand(LimpiarCalleNumero);
        }

        public bool DireccionObligatoria { get; }

        /// <summary>Se ven los comentarios de ruta y de picking en el propio bloque (la de entrega; los de la fiscal van en «Datos de contacto»).</summary>
        public bool MostrarComentarios { get; set; }

        public bool EsAlta => esAlta();

        /// <summary>Quien manda sobre <see cref="EsAlta"/> avisa cuando cambia.</summary>
        public void NotificarEsAlta()
        {
            OnPropertyChanged(nameof(EsAlta));
            OnPropertyChanged(nameof(CodigoPostalIsEnabled));
            OnPropertyChanged(nameof(AyudaCodigoPostal));
            NotificarMotivo();
        }

        #region Dirección elegida de Google
        private string calleNumero;
        /// <summary>Lo que se busca en Google y, elegida, la calle y el número que dio.</summary>
        public string CalleNumero
        {
            get => calleNumero;
            set
            {
                if (SetProperty(ref calleNumero, value) && !aplicandoSugerencia)
                {
                    // Editado a mano: la pareja dirección+CP ya no es la que dio Google
                    DireccionVerificadaPorGoogle = false;
                    BuscarSugerenciasConDebounce(value);
                }
                NotificarMotivo(); // Nesto#480
            }
        }

        private string direccion;
        /// <summary>La dirección tal como se guardará (la formatea la API al validar; al modificar, la que tenía).</summary>
        public string Direccion
        {
            get => direccion;
            set
            {
                SetProperty(ref direccion, value);
                OnPropertyChanged(nameof(TieneDireccion));
                OnPropertyChanged(nameof(NoTieneDireccion));
                NotificarMotivo();
            }
        }

        public bool TieneDireccion => !string.IsNullOrEmpty(Direccion);
        public bool NoTieneDireccion => !TieneDireccion;

        private bool direccionVerificadaPorGoogle;
        /// <summary>Nesto#409: dirección y CP salieron JUNTOS del combo de Places, así que son consistentes entre sí.</summary>
        public bool DireccionVerificadaPorGoogle
        {
            get => direccionVerificadaPorGoogle;
            set
            {
                if (SetProperty(ref direccionVerificadaPorGoogle, value))
                {
                    OnPropertyChanged(nameof(CodigoPostalIsEnabled));
                    OnPropertyChanged(nameof(AyudaCodigoPostal));
                    OnPropertyChanged(nameof(DireccionEsSoloLectura));
                    NotificarMotivo();
                    if (!value)
                    {
                        // Sin verificación no hay datos de Google que preferir
                        PoblacionGoogle = null;
                        ProvinciaGoogle = null;
                        CodigoPostalSinDarPorGoogle = false;
                        NumeroSinDarPorGoogle = false; // Nesto#643
                        NumeroCalle = null;
                    }
                }
            }
        }

        /// <summary>Nesto#480: la dirección de Google se SELECCIONA, no se escribe: elegida, queda de solo lectura (se puede leer y copiar).</summary>
        public bool DireccionEsSoloLectura => DireccionVerificadaPorGoogle;

        // Nesto#409: población/provincia que dio Google junto a la dirección (en mayúsculas). Un CP puede cubrir varias poblaciones y
        // nuestra tabla solo tiene una: la de Google es la correcta para la ficha. La ruta sigue saliendo de nuestra tabla de CPs.
        private string poblacionGoogle, provinciaGoogle;
        public string PoblacionGoogle { get => poblacionGoogle; private set => SetProperty(ref poblacionGoogle, value); }
        public string ProvinciaGoogle { get => provinciaGoogle; private set => SetProperty(ref provinciaGoogle, value); }

        private string paisDireccion = "ES";
        /// <summary>Nesto#436: país ISO-2 de la DIRECCIÓN (puede diferir del fiscal). Al cambiarlo se relanza la búsqueda.</summary>
        public string PaisDireccion
        {
            get => paisDireccion;
            set
            {
                if (SetProperty(ref paisDireccion, value) && !aplicandoSugerencia)
                {
                    CerrarSugerenciasDireccion();
                    if (!string.IsNullOrWhiteSpace(CalleNumero))
                    {
                        DireccionVerificadaPorGoogle = false;
                        BuscarSugerenciasConDebounce(CalleNumero);
                    }
                }
            }
        }

        public ICommand LimpiarDireccionCommand { get; }
        public ICommand LimpiarDireccionCalleNumeroCommand { get; }

        /// <summary>
        /// Nesto#480: borra la dirección elegida para poder buscar otra. Reabre el campo y pide el foco a la vista para seguir tecleando.
        /// </summary>
        private void LimpiarCalleNumero()
        {
            CerrarSugerenciasDireccion();
            CalleNumero = string.Empty;
            // Si el campo ya estaba vacío el setter no salta, así que nos aseguramos aquí.
            DireccionVerificadaPorGoogle = false;
            NumeroSinDarPorGoogle = false; // Nesto#643
            NumeroCalle = null;
            FocoEnDireccionSolicitado?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Nesto#480: la vista devuelve el foco al campo de dirección al borrarla.</summary>
        public event EventHandler FocoEnDireccionSolicitado;
        #endregion

        #region Código postal (novedad 482; corte 1 del 19/11/26)
        private string codigoPostal;
        public string CodigoPostal
        {
            get => codigoPostal;
            set
            {
                if (SetProperty(ref codigoPostal, value) && !aplicandoSugerencia)
                {
                    if (CodigoPostalSinDarPorGoogle)
                    {
                        // Novedad 482: Google dio la dirección sin CP; teclearlo es lo que se espera
                        NotificarMotivo();
                        return;
                    }
                    // Editado a mano: la pareja dirección+CP ya no es la que dio Google
                    DireccionVerificadaPorGoogle = false;
                }
            }
        }

        // Novedad 482 (Laura, 05/10/26): para algunas calles Google da calle, número y población pero NO el código postal. La dirección
        // sí se ha elegido de la lista (cuenta como verificada) y el CP se teclea. El servidor lo comprueba como siempre.
        private bool codigoPostalSinDarPorGoogle;
        public bool CodigoPostalSinDarPorGoogle
        {
            get => codigoPostalSinDarPorGoogle;
            private set
            {
                if (SetProperty(ref codigoPostalSinDarPorGoogle, value))
                {
                    OnPropertyChanged(nameof(CodigoPostalIsEnabled));
                    OnPropertyChanged(nameof(AyudaCodigoPostal));
                    NotificarMotivo();
                }
            }
        }

        /// <summary>
        /// Corte 1 (Carlos 19/11/26): al CREAR, el código postal sale SIEMPRE de Google; solo se teclea si Google no lo da para la dirección
        /// elegida (novedad 482). Al modificar, como antes: abierto mientras la dirección no venga de Google.
        /// </summary>
        public bool CodigoPostalIsEnabled => EsAlta
            ? DireccionVerificadaPorGoogle && CodigoPostalSinDarPorGoogle
            : !DireccionVerificadaPorGoogle || CodigoPostalSinDarPorGoogle;

        /// <summary>La ayuda del código postal, según de dónde sale.</summary>
        public string AyudaCodigoPostal => !DireccionVerificadaPorGoogle
            ? EsAlta
                ? "Código postal de la dirección: sale solo al elegir la dirección en la lista de Google (no se escribe a mano)"
                : "Código postal de la dirección. Se rellena solo al elegir la dirección en la lista de Google"
            : CodigoPostalSinDarPorGoogle
                ? "Google no tiene el código postal de esta dirección: escríbalo usted"
                : "El código postal viene de la dirección seleccionada en el combo de Google; para cambiarlo, modifique la dirección";

        /// <summary>
        /// NestoAPI#596: el CP en el formato canónico (las mismas reglas que la API). Solo cambia el formato, no el código, así que no
        /// quita la verificación de Google.
        /// </summary>
        public void NormalizarCodigoPostal()
        {
            string canonico = Nesto.Infrastructure.Shared.CodigoPostal.Normalizar(codigoPostal, PaisDireccion);
            if (canonico == null || canonico == codigoPostal)
            {
                return;
            }
            codigoPostal = canonico;
            OnPropertyChanged(nameof(CodigoPostal));
            NotificarMotivo();
        }
        #endregion

        #region Nesto#643 (NestoAPI#874): número propio y «¿Qué es?» (Dirección 2)
        private bool numeroSinDarPorGoogle;
        /// <summary>Google dio la calle SIN número: se pide en su campo y la API lo vuelve a comprobar con Google.</summary>
        public bool NumeroSinDarPorGoogle
        {
            get => numeroSinDarPorGoogle;
            private set
            {
                if (SetProperty(ref numeroSinDarPorGoogle, value))
                {
                    NotificarMotivo();
                }
            }
        }

        private string numeroCalle;
        /// <summary>El número de la calle cuando Google no lo da: cifras o «S/N».</summary>
        public string NumeroCalle
        {
            get => numeroCalle;
            set
            {
                if (SetProperty(ref numeroCalle, value))
                {
                    NumeroConfirmadoSinGoogle = false; // otro número: hay que volver a preguntar a Google
                    NotificarMotivo();
                }
            }
        }

        private bool numeroConfirmadoSinGoogle;
        /// <summary>Google no encuentra el número tecleado (válido) y el usuario ha dicho que se guarde igualmente (Carlos 17/11/26).</summary>
        public bool NumeroConfirmadoSinGoogle { get => numeroConfirmadoSinGoogle; private set => SetProperty(ref numeroConfirmadoSinGoogle, value); }

        public IReadOnlyList<TipoLugarModel> TiposLugar => Direccion2Cliente.TIPOS;

        private string tipoLugar;
        /// <summary>«¿Qué es?»: Piso, Local, Casa, Nave u Otro. Obligatorio en el alta y sin valor por defecto.</summary>
        public string TipoLugar
        {
            get => tipoLugar;
            set
            {
                if (SetProperty(ref tipoLugar, value))
                {
                    OnPropertyChanged(nameof(EsPiso));
                    OnPropertyChanged(nameof(EsLocal));
                    OnPropertyChanged(nameof(EsNave));
                    OnPropertyChanged(nameof(EsOtro));
                    NotificarDireccion2();
                }
            }
        }
        public bool EsPiso => TipoLugar == Direccion2Cliente.PISO;
        public bool EsLocal => TipoLugar == Direccion2Cliente.LOCAL;
        public bool EsNave => TipoLugar == Direccion2Cliente.NAVE;
        public bool EsOtro => TipoLugar == Direccion2Cliente.OTRO;

        private string piso, puerta, numeroLocal, rotulo, numeroNave, otroTexto;
        public string Piso { get => piso; set { if (SetProperty(ref piso, value)) NotificarDireccion2(); } }
        public string Puerta { get => puerta; set { if (SetProperty(ref puerta, value)) NotificarDireccion2(); } }
        public string NumeroLocal { get => numeroLocal; set { if (SetProperty(ref numeroLocal, value)) NotificarDireccion2(); } }
        /// <summary>Lo que pone en el rótulo. Obligatorio en un local y SIN valor por defecto: el nombre fiscal no es lo que pone en la puerta.</summary>
        public string Rotulo { get => rotulo; set { if (SetProperty(ref rotulo, value)) NotificarDireccion2(); } }
        public string NumeroNave { get => numeroNave; set { if (SetProperty(ref numeroNave, value)) NotificarDireccion2(); } }
        public string OtroTexto { get => otroTexto; set { if (SetProperty(ref otroTexto, value)) NotificarDireccion2(); } }

        /// <summary>Cómo quedará la Dirección 2 («1º D», «LOCAL 6 · RÓTULO: EL EDÉN»…); null mientras falte algo.</summary>
        public string Direccion2Compuesta => Direccion2Cliente.Componer(TipoLugar, Piso, Puerta, NumeroLocal, Rotulo, NumeroNave, OtroTexto, out _);

        /// <summary>Qué le falta a «¿Qué es?» (de usted), o null si está completo.</summary>
        public string MotivoDireccion2
        {
            get
            {
                Direccion2Cliente.Componer(TipoLugar, Piso, Puerta, NumeroLocal, Rotulo, NumeroNave, OtroTexto, out string motivo);
                return motivo;
            }
        }

        public const string MOTIVO_SIN_NUMERO = "Google no tiene el número de esta calle: escríbalo en «Número» (cifras, o «S/N» si no tiene).";

        private void NotificarDireccion2()
        {
            OnPropertyChanged(nameof(Direccion2Compuesta));
            OnPropertyChanged(nameof(MotivoDireccion2));
            OnPropertyChanged(nameof(AvisoDireccion2));
            NotificarMotivo();
        }

        /// <summary>
        /// NestoAPI#874 (corte 2): el aviso rojo de «¿Qué es?». En el alta, siempre; al modificar, solo si se ha elegido un tipo (sin
        /// tipo no se toca lo guardado y no hay nada que avisar).
        /// </summary>
        public string AvisoDireccion2 => EsAlta || !string.IsNullOrEmpty(TipoLugar) ? MotivoDireccion2 : null;

        /// <summary>NestoAPI#874 (corte 2): el contacto que se modifica ya tenía Dirección 2 (la que trae GetClienteCrear).</summary>
        public bool TeniaDireccion2 { get; private set; }

        /// <summary>NestoAPI#874 (corte 2): al abrir la modificación, «¿Qué es?» y sus campos con lo guardado (ya descompuesto por la API).</summary>
        public void CargarQueEs(IDireccion2Alta origen, string direccion2Guardada)
        {
            TeniaDireccion2 = !string.IsNullOrWhiteSpace(direccion2Guardada);
            Piso = origen?.Piso;
            Puerta = origen?.Puerta;
            NumeroLocal = origen?.NumeroLocal;
            Rotulo = origen?.Rotulo;
            NumeroNave = origen?.NumeroNave;
            OtroTexto = origen?.OtroTexto;
            TipoLugar = origen?.TipoLugar;
        }

        /// <summary>
        /// NestoAPI#874 (corte 2): lo que viaja al MODIFICAR: solo «¿Qué es?» (el número propio es del alta). Sin tipo, nada (la API conserva
        /// lo guardado). Devuelve el motivo si el tipo elegido está incompleto (de usted), o null.
        /// </summary>
        public string RellenarQueEsAlModificar(ClienteCrear destino)
        {
            if (string.IsNullOrEmpty(TipoLugar))
            {
                return null;
            }
            string motivo = MotivoDireccion2;
            if (motivo != null)
            {
                return motivo;
            }
            RellenarNumeroYQueEs(destino);
            destino.Numero = null;
            destino.NumeroConfirmadoSinGoogle = false;
            return null;
        }

        /// <summary>Lo que viaja a la API del número propio y de «¿Qué es?» (solo los campos del tipo elegido).</summary>
        public void RellenarNumeroYQueEs(IDireccion2Alta destino)
        {
            destino.Numero = NumeroSinDarPorGoogle ? NumeroCalle?.Trim() : null;
            destino.NumeroConfirmadoSinGoogle = NumeroSinDarPorGoogle && NumeroConfirmadoSinGoogle;
            destino.TipoLugar = TipoLugar;
            destino.Piso = EsPiso ? Piso : null;
            destino.Puerta = EsPiso ? Puerta : null;
            destino.NumeroLocal = EsLocal ? NumeroLocal : null;
            destino.Rotulo = EsLocal ? Rotulo : null;
            destino.NumeroNave = EsNave ? NumeroNave : null;
            destino.OtroTexto = EsOtro ? OtroTexto : null;
        }
        #endregion

        #region Lo demás de la dirección
        private string poblacion, provincia, ruta, telefono, comentariosRuta, comentariosPicking;
        public string Poblacion { get => poblacion; set => SetProperty(ref poblacion, value); }
        public string Provincia { get => provincia; set => SetProperty(ref provincia, value); }
        public string Ruta { get => ruta; set => SetProperty(ref ruta, value); }
        public string Telefono { get => telefono; set => SetProperty(ref telefono, value); }
        public string ComentariosRuta { get => comentariosRuta; set => SetProperty(ref comentariosRuta, value); }
        public string ComentariosPicking { get => comentariosPicking; set => SetProperty(ref comentariosPicking, value); }

        private bool direccionValidada;
        /// <summary>La API ha dado por buena la dirección (ValidarDatosGenerales) y ya están la dirección formateada, la población y la ruta.</summary>
        public bool DireccionValidada { get => direccionValidada; set => SetProperty(ref direccionValidada, value); }
        #endregion

        #region Lo que falta para crear
        /// <summary>Hay algo escrito o elegido en la dirección.</summary>
        public bool HayDireccion => !string.IsNullOrWhiteSpace(Direccion) || !string.IsNullOrWhiteSpace(CalleNumero);

        public const string MOTIVO_DIRECCION_A_MANO = "La dirección tiene que elegirse de las que propone Google, no escribirse a mano. " +
            "Vuelva a «Datos generales», borre la dirección con el botón de al lado, búsquela y " +
            "selecciónela en la lista. Si Google no la encuentra, avise para darla de alta desde el Nesto viejo.";
        public const string MOTIVO_SIN_CODIGO_POSTAL = "Google no tiene el código postal de esta dirección: escríbalo en «Código postal» (en «Datos generales»).";
        public const string MOTIVO_SIN_DIRECCION = "busque la dirección y elíjala en la lista de Google.";

        /// <summary>
        /// Nesto#480 y Nesto#643: qué le falta a esta dirección para crear el cliente (espejo de la API: NestoAPI#499 y #874), o null.
        /// Solo en las altas; sin dirección no hay nada que verificar, salvo que sea obligatoria (la de entrega marcada).
        /// </summary>
        public string MotivoNoSePuedeCrear
        {
            get
            {
                if (!EsAlta)
                {
                    return null;
                }
                if (!HayDireccion)
                {
                    return DireccionObligatoria ? Prefijar(MOTIVO_SIN_DIRECCION) : null;
                }
                if (!DireccionVerificadaPorGoogle)
                {
                    return Prefijar(MOTIVO_DIRECCION_A_MANO);
                }
                if (CodigoPostalSinDarPorGoogle && string.IsNullOrWhiteSpace(CodigoPostal))
                {
                    return Prefijar(MOTIVO_SIN_CODIGO_POSTAL);
                }
                if (NumeroSinDarPorGoogle && string.IsNullOrWhiteSpace(NumeroCalle))
                {
                    return Prefijar(MOTIVO_SIN_NUMERO);
                }
                if (NumeroSinDarPorGoogle && !NumeroPortal.FormatoValido(NumeroCalle))
                {
                    return Prefijar(NumeroPortal.MOTIVO_FORMATO);
                }
                return Prefijar(MotivoDireccion2);
            }
        }

        private string Prefijar(string motivo) => motivo == null || prefijoMotivo == null ? motivo : prefijoMotivo + motivo;

        private void NotificarMotivo() => OnPropertyChanged(nameof(MotivoNoSePuedeCrear));
        #endregion

        #region Nesto#409: autocompletado de direcciones (Google Places vía NestoAPI)
        // El usuario va tecleando y sale el combo de sugerencias de Google; al seleccionar una se rellenan calle+número y código postal ya
        // normalizados. Best-effort: cualquier fallo deja el combo vacío. El sessionToken agrupa autocomplete+detalle en una sesión de
        // facturación y se renueva tras cada selección.
        private CancellationTokenSource ctsSugerencias;
        private string sessionToken = Guid.NewGuid().ToString();
        private bool aplicandoSugerencia;

        public ObservableCollection<SugerenciaDireccionModel> SugerenciasDireccion { get; } = new ObservableCollection<SugerenciaDireccionModel>();

        private bool haySugerenciasDireccion;
        public bool HaySugerenciasDireccion { get => haySugerenciasDireccion; set => SetProperty(ref haySugerenciasDireccion, value); }

        // Solo RESALTA (flechas del teclado o hover); aplicar es cosa de Intro o del clic.
        private SugerenciaDireccionModel sugerenciaDireccionSeleccionada;
        public SugerenciaDireccionModel SugerenciaDireccionSeleccionada
        {
            get => sugerenciaDireccionSeleccionada;
            set => SetProperty(ref sugerenciaDireccionSeleccionada, value);
        }

        /// <summary>Mueve el resaltado con las flechas (delta ±1), sin salirse de la lista.</summary>
        public void MoverSeleccionSugerencias(int delta)
        {
            if (!HaySugerenciasDireccion || SugerenciasDireccion.Count == 0)
            {
                return;
            }
            int indice = SugerenciaDireccionSeleccionada == null ? -1 : SugerenciasDireccion.IndexOf(SugerenciaDireccionSeleccionada);
            int nuevo = Math.Max(0, Math.Min(SugerenciasDireccion.Count - 1, indice + delta));
            SugerenciaDireccionSeleccionada = SugerenciasDireccion[nuevo];
        }

        /// <summary>Aplica la sugerencia resaltada (Intro o clic). Devuelve si había algo que aplicar.</summary>
        public async Task<bool> AplicarSugerenciaSeleccionadaAsync()
        {
            if (!HaySugerenciasDireccion || SugerenciaDireccionSeleccionada == null)
            {
                return false;
            }
            await AplicarSugerenciaDireccionAsync(SugerenciaDireccionSeleccionada);
            return true;
        }

        /// <summary>Cierra el combo sin aplicar nada (Escape o pérdida de foco).</summary>
        public void CerrarSugerenciasDireccion()
        {
            SugerenciasDireccion.Clear();
            HaySugerenciasDireccion = false;
            SugerenciaDireccionSeleccionada = null;
        }

        private async void BuscarSugerenciasConDebounce(string texto)
        {
            ctsSugerencias?.Cancel();
            ctsSugerencias = new CancellationTokenSource();
            CancellationToken token = ctsSugerencias.Token;
            try
            {
                await Task.Delay(350, token); // debounce: solo busca cuando el usuario para de teclear
                if (string.IsNullOrWhiteSpace(texto) || texto.Trim().Length < 3)
                {
                    SugerenciasDireccion.Clear();
                    HaySugerenciasDireccion = false;
                    return;
                }
                var sugerencias = await servicio.BuscarSugerenciasDireccion(texto.Trim(), sessionToken, PaisDireccion);
                if (token.IsCancellationRequested)
                {
                    return; // el usuario siguió tecleando: esta respuesta ya está obsoleta
                }
                SugerenciasDireccion.Clear();
                foreach (var sugerencia in sugerencias)
                {
                    SugerenciasDireccion.Add(sugerencia);
                }
                HaySugerenciasDireccion = SugerenciasDireccion.Count > 0;
            }
            catch
            {
                // Cancelado o fallo de red/Places: el autocompletado nunca molesta al alta
            }
        }

        public async Task AplicarSugerenciaDireccionAsync(SugerenciaDireccionModel sugerencia)
        {
            try
            {
                DireccionDetalleModel detalle = await servicio.LeerDetalleDireccion(sugerencia.PlaceId, sessionToken);
                if (detalle == null)
                {
                    return;
                }
                aplicandoSugerencia = true;
                CalleNumero = detalle.CalleNumero; // Nesto#627: en el formato del país
                NumeroCalle = null;
                if (!string.IsNullOrWhiteSpace(detalle.CodigoPostal))
                {
                    CodigoPostal = detalle.CodigoPostal;
                    // Dirección y CP vienen juntos de Google: pareja verificada → CP bloqueado y la validación se salta el geocoding
                    DireccionVerificadaPorGoogle = true;
                    CodigoPostalSinDarPorGoogle = false;
                    PoblacionGoogle = detalle.Poblacion?.ToUpper().Trim();
                    ProvinciaGoogle = detalle.Provincia?.ToUpper().Trim();
                }
                else if (!string.IsNullOrWhiteSpace(detalle.Calle) && !string.IsNullOrWhiteSpace(detalle.Poblacion))
                {
                    // Novedad 482: elegida de Google, pero sin CP → verificada y el CP se teclea
                    CodigoPostal = string.Empty;
                    DireccionVerificadaPorGoogle = true;
                    CodigoPostalSinDarPorGoogle = true;
                    PoblacionGoogle = detalle.Poblacion.ToUpper().Trim();
                    ProvinciaGoogle = detalle.Provincia?.ToUpper().Trim();
                }
                if (!string.IsNullOrWhiteSpace(detalle.PaisIso))
                {
                    // Nesto#436: el país real de la dirección elegida manda (el flag de aplicando evita que el setter relance la búsqueda)
                    PaisDireccion = detalle.PaisIso.Trim().ToUpper();
                }
                // Nesto#643 (NestoAPI#874): elegida de Google pero sin número → se pide aparte y se comprueba al validar.
                NumeroSinDarPorGoogle = DireccionVerificadaPorGoogle && string.IsNullOrWhiteSpace(detalle.Numero);
            }
            catch
            {
                // best-effort: si el detalle falla, el usuario sigue con lo tecleado
            }
            finally
            {
                aplicandoSugerencia = false;
                SugerenciasDireccion.Clear();
                HaySugerenciasDireccion = false;
                sessionToken = Guid.NewGuid().ToString(); // nueva sesión de facturación
            }
        }
        #endregion

        #region Validar con la API
        public const string TITULO_NUMERO_SIN_GOOGLE = "Número de la calle";

        /// <summary>
        /// Valida la dirección con la API (ValidarDatosGenerales) y, si vale, deja puestas la dirección formateada, población, provincia, ruta
        /// y teléfono (<see cref="DireccionValidada"/>). Devuelve la respuesta de la API para lo que no es de la dirección (vendedores), o
        /// null si no se ha preguntado (ya tenía dirección y no se ha buscado otra) o si no vale (el error ya se ha enseñado).
        /// </summary>
        /// <param name="clienteNumero">El cliente que se está tocando: no cuenta como «otro cliente con el mismo teléfono».</param>
        /// <param name="avisarMismoTelefono">Enseñar los clientes con el mismo teléfono (la de entrega no lo repite si es el de la fiscal).</param>
        public async Task<RespuestaDatosGeneralesClientes> ValidarAsync(IServicioDialogos dialogos, string clienteNumero, bool avisarMismoTelefono = true)
        {
            if (telefono == "undefined")
            {
                telefono = string.Empty;
            }

            if (!string.IsNullOrEmpty(Direccion) && string.IsNullOrEmpty(calleNumero))
            {
                DireccionValidada = true;
                return null;
            }

            // Nesto#643 (NestoAPI#874): sin el número que Google no dio no se sigue (la API lo comprueba con Google).
            if (NumeroSinDarPorGoogle && string.IsNullOrWhiteSpace(NumeroCalle))
            {
                dialogos.ShowError(Prefijar(MOTIVO_SIN_NUMERO));
                return null;
            }
            if (NumeroSinDarPorGoogle && !NumeroPortal.FormatoValido(NumeroCalle))
            {
                dialogos.ShowError(Prefijar(NumeroPortal.MOTIVO_FORMATO));
                return null;
            }

            NormalizarCodigoPostal(); // NestoAPI#596
            try
            {
                RespuestaDatosGeneralesClientes respuesta = NumeroSinDarPorGoogle
                    ? await servicio.ValidarDatosGenerales(CalleNumero, CodigoPostal, Telefono, DireccionVerificadaPorGoogle, PaisDireccion, NumeroCalle.Trim())
                    : await servicio.ValidarDatosGenerales(CalleNumero, CodigoPostal, Telefono, DireccionVerificadaPorGoogle, PaisDireccion);
                // Nesto#643 (NestoAPI#874): Google no encuentra el número (válido). No se bloquea: se pregunta (Carlos 17/11/26).
                if (NumeroSinDarPorGoogle && respuesta.NumeroSinConfirmarPorGoogle && !NumeroConfirmadoSinGoogle)
                {
                    if (!dialogos.ShowConfirmationAnswer(TITULO_NUMERO_SIN_GOOGLE, Prefijar(respuesta.AvisoNumero)))
                    {
                        return null; // se queda en «Datos generales» para corregir el número
                    }
                    NumeroConfirmadoSinGoogle = true;
                }
                respuesta.ClientesMismoTelefono = (respuesta.ClientesMismoTelefono ?? new List<ClienteTelefonoLookup>())
                    .Where(c => c.Cliente != clienteNumero).ToList();
                if (avisarMismoTelefono && respuesta.ClientesMismoTelefono.Count > 0)
                {
                    dialogos.ShowDialog("NotificacionTelefonoView", new ParametrosDialogo { { "clientesMismoTelefono", respuesta.ClientesMismoTelefono } }, null);
                }

                // Nesto#643: solo calle y número (lo demás va en «¿Qué es?», que es la Dirección 2).
                Direccion = Strings.Left(respuesta.DireccionFormateada, 50);
                // Nesto#409: si la dirección viene verificada de Google, SU población/provincia son las correctas para la ficha. Nesto#436:
                // en direcciones extranjeras la tabla de CPs no da población/provincia: se conserva lo que hubiera.
                Poblacion = Strings.Left(
                    DireccionVerificadaPorGoogle && !string.IsNullOrWhiteSpace(PoblacionGoogle)
                        ? PoblacionGoogle
                        : respuesta.Poblacion ?? Poblacion, 30);
                Provincia = DireccionVerificadaPorGoogle && !string.IsNullOrWhiteSpace(ProvinciaGoogle)
                    ? ProvinciaGoogle
                    : respuesta.Provincia ?? Provincia;
                Ruta = respuesta.Ruta ?? Ruta;
                Telefono = respuesta.TelefonoFormateado;
                DireccionValidada = true;
                return respuesta;
            }
            catch (Exception ex)
            {
                dialogos.ShowError(Prefijar(ex.Message));
                return null;
            }
        }
        #endregion

        /// <summary>La dirección de entrega tal como viaja a la API (ClienteCrear.DireccionEntrega).</summary>
        public DireccionEntregaCrear ParaEntrega()
        {
            NormalizarCodigoPostal(); // NestoAPI#596
            var entrega = new DireccionEntregaCrear
            {
                Direccion = Direccion,
                DireccionVerificada = DireccionVerificadaPorGoogle,
                CodigoPostal = CodigoPostal,
                Poblacion = Poblacion,
                Provincia = Provincia,
                Ruta = Ruta,
                Telefono = Telefono,
                ComentariosRuta = ComentariosRuta,
                ComentariosPicking = ComentariosPicking
            };
            RellenarNumeroYQueEs(entrega);
            return entrega;
        }
    }
}
