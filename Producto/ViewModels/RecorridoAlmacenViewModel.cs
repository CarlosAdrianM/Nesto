using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto.ViewModels
{
    /// <summary>Nesto#528: un sentido de calle que se puede elegir.</summary>
    public class OpcionSentidoCalle
    {
        public OpcionSentidoCalle(string valor, string texto)
        {
            Valor = valor;
            Texto = texto;
        }

        public string Valor { get; }
        public string Texto { get; }
    }

    /// <summary>Nesto#528: un pasillo del recorrido (en una calle o sin calle). Solo se cambia su nombre.</summary>
    public class PasilloRecorridoEditable : ObservableObject
    {
        public PasilloRecorridoEditable(string pasillo, string nombre, int huecos)
        {
            Pasillo = pasillo;
            _nombre = nombre;
            Huecos = huecos;
        }

        public string Pasillo { get; }
        public int Huecos { get; }

        private string _nombre;
        /// <summary>Para que alguien nuevo lo encuentre («Esmaltes»); opcional.</summary>
        public string Nombre { get => _nombre; set => SetProperty(ref _nombre, value); }
    }

    /// <summary>Nesto#528: una calle del recorrido (pasillos que se recorren a la vez), con sus pasillos en orden.</summary>
    public class CalleRecorridoEditable : ObservableObject
    {
        public CalleRecorridoEditable(string calle, string sentido, string descripcion, IEnumerable<PasilloRecorridoEditable> pasillos)
        {
            _calle = calle;
            _sentido = RecorridoAlmacenViewModel.NormalizarSentido(sentido);
            _descripcion = descripcion;
            Pasillos = new ObservableCollection<PasilloRecorridoEditable>(pasillos ?? Enumerable.Empty<PasilloRecorridoEditable>());
            Pasillos.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ResumenPasillos));
        }

        private string _calle;
        public string Calle { get => _calle; set => SetProperty(ref _calle, value); }

        private string _sentido;
        /// <summary>«A» (ida: columnas ascendentes) o «D» (vuelta: descendentes).</summary>
        public string Sentido { get => _sentido; set => SetProperty(ref _sentido, RecorridoAlmacenViewModel.NormalizarSentido(value)); }

        private string _descripcion;
        public string Descripcion { get => _descripcion; set => SetProperty(ref _descripcion, value); }

        public ObservableCollection<PasilloRecorridoEditable> Pasillos { get; }

        /// <summary>«001, 002, 005» (para la lista de calles).</summary>
        public string ResumenPasillos => Pasillos.Count == 0 ? "(sin pasillos)" : string.Join(", ", Pasillos.Select(p => p.Pasillo));
    }

    /// <summary>Nesto#528: un tramo del recorrido como se enseña (pasillo + columna + nivel, con sus filas).</summary>
    public class TramoRecorridoFila
    {
        public int Orden { get; set; }
        public string Calle { get; set; }
        public string Pasillo { get; set; }
        public string Columna { get; set; }
        public string Nivel { get; set; }
        public string Filas { get; set; }
    }

    /// <summary>
    /// Nesto#528 (corte 4 de NestoAPI#639; API en NestoAPI#642): la ventana «Recorrido del almacén». Mantiene las calles del recorrido de
    /// preparación de Algete: su orden, su sentido (A ida / D vuelta), su descripción y los pasillos de cada una en orden; los pasillos
    /// que no van en ninguna calle (al final del recorrido) y la fila desde la que se llega con la mano.
    /// <list type="bullet">
    /// <item>Se edita todo en local; «Vista previa» manda lo que hay con ensayo=true y enseña los tramos que saldrían, sin guardar;
    /// «Guardar» sustituye el recorrido entero (con confirmación) y vuelve a pintar lo que devuelve la API.</item>
    /// <item>Solo Almacén y Dirección guardan (la API da 403 al resto): para los demás la ventana es de solo lectura.</item>
    /// <item>Sin las tablas en el servidor (TablasCreadas false) o con 503, se avisa y no se puede editar nada.</item>
    /// <item>Los 400 de la API se enseñan tal cual. No hay control de concurrencia: gana el último que guarda.</item>
    /// </list>
    /// </summary>
    public class RecorridoAlmacenViewModel : ObservableObject, IReceptorNavegacion, ITabCloseConfirmation
    {
        public const string SENTIDO_IDA = "A";
        public const string SENTIDO_VUELTA = "D";
        public const string MENSAJE_SIN_TABLAS = "En el servidor aún no están las tablas del recorrido: los pasillos se recorren en el orden de " +
            "siempre y desde aquí no se puede cambiar nada. Avise a Informática.";
        public const string MENSAJE_SOLO_LECTURA = "Solo Almacén y Dirección pueden cambiar el recorrido: usted lo puede ver, pero no guardar.";
        public const string MENSAJE_FILA_A_MANO = "La fila a mano tiene que ser un número (de 1 a 255).";

        private readonly IServicioRecorridoAlmacen _servicio;
        private readonly IServicioDialogos _dialogos;
        private string _huellaCargada;
        private bool _cargando;

        public RecorridoAlmacenViewModel(IServicioRecorridoAlmacen servicio, IConfiguracion configuracion, IServicioDialogos dialogos)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            PuedeGuardarPorGrupo = configuracion != null
                && (configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)
                    || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION));
            Calles.CollectionChanged += AlCambiarCalles;
            PasillosSinCalle.CollectionChanged += AlCambiarColeccion;

            CargarCommand = new AsyncRelayCommand(RecargarAsync, () => !EstaOcupado);
            SubirCalleCommand = new RelayCommand(SubirCalle, () => Editable && CalleSeleccionada != null && Calles.IndexOf(CalleSeleccionada) > 0);
            BajarCalleCommand = new RelayCommand(BajarCalle, () => Editable && CalleSeleccionada != null && Calles.IndexOf(CalleSeleccionada) < Calles.Count - 1);
            NuevaCalleCommand = new RelayCommand(NuevaCalle, () => Editable);
            BorrarCalleCommand = new RelayCommand(BorrarCalle, () => Editable && CalleSeleccionada != null);
            SerpentinaCommand = new RelayCommand(Serpentina, () => Editable && Calles.Count > 0);
            SubirPasilloCommand = new RelayCommand(SubirPasillo, () => Editable && PasilloSeleccionado != null
                && CalleSeleccionada.Pasillos.IndexOf(PasilloSeleccionado) > 0);
            BajarPasilloCommand = new RelayCommand(BajarPasillo, () => Editable && PasilloSeleccionado != null
                && CalleSeleccionada.Pasillos.IndexOf(PasilloSeleccionado) < CalleSeleccionada.Pasillos.Count - 1);
            QuitarPasilloCommand = new RelayCommand(QuitarPasillo, () => Editable && PasilloSeleccionado != null);
            MoverPasilloCommand = new RelayCommand(MoverPasillo, () => Editable && PasilloSeleccionado != null
                && CalleDestino != null && CalleDestino != CalleSeleccionada);
            AnadirPasilloSinCalleCommand = new RelayCommand(AnadirPasilloSinCalle, () => Editable && CalleSeleccionada != null
                && PasilloSinCalleSeleccionado != null);
            AnadirPasilloCommand = new RelayCommand(AnadirPasillo, () => Editable && CalleSeleccionada != null);
            VistaPreviaCommand = new AsyncRelayCommand(VistaPreviaAsync, () => Editable);
            GuardarCommand = new AsyncRelayCommand(GuardarAsync, () => Editable && HayCambios);
        }

        public string Titulo => "Recorrido del almacén";
        public string Ayuda => "El orden en que se recorren los pasillos al preparar: las calles van una detrás de otra, cada una con sus pasillos " +
            "en orden y su sentido (A: columnas de menor a mayor; D: de mayor a menor). Los pasillos sin calle van al final. " +
            "«Vista previa» enseña los tramos que saldrían sin guardar nada.";

        /// <summary>Siempre la empresa 1 (la API también sirve a la 3 con el almacén compartido).</summary>
        public string Empresa => Constantes.Empresas.EMPRESA_DEFECTO;
        public string Almacen => Constantes.Almacenes.ALMACEN_ALGETE;

        public IReadOnlyList<OpcionSentidoCalle> Sentidos { get; } = new List<OpcionSentidoCalle>
        {
            new OpcionSentidoCalle(SENTIDO_IDA, "A (ida)"),
            new OpcionSentidoCalle(SENTIDO_VUELTA, "D (vuelta)")
        };

        /// <summary>Almacén y Dirección (los que la API deja guardar).</summary>
        public bool PuedeGuardarPorGrupo { get; }

        private bool _sinPermisoEnLaApi;
        /// <summary>La API ha dicho 403 al guardar: se queda en solo lectura aunque el grupo pareciera valer.</summary>
        public bool SoloLectura => !PuedeGuardarPorGrupo || _sinPermisoEnLaApi;

        private bool _disponible;
        /// <summary>Leído, con las tablas en el servidor y sin 503: se puede trabajar con él.</summary>
        public bool Disponible
        {
            get => _disponible;
            private set
            {
                if (SetProperty(ref _disponible, value))
                {
                    NotificarEstado();
                }
            }
        }

        /// <summary>Se puede tocar algo: disponible, con permiso y sin una llamada en curso.</summary>
        public bool Editable => Disponible && !SoloLectura && !EstaOcupado;
        public bool NoEditable => !Editable;

        private string _aviso;
        /// <summary>El aviso fijo de arriba: sin tablas, sin servicio o solo lectura.</summary>
        public string Aviso
        {
            get => _aviso;
            private set
            {
                if (SetProperty(ref _aviso, value))
                {
                    OnPropertyChanged(nameof(HayAviso));
                }
            }
        }

        public bool HayAviso => !string.IsNullOrWhiteSpace(Aviso);

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    NotificarEstado();
                }
            }
        }

        private string _mensaje;
        public string Mensaje { get => _mensaje; private set => SetProperty(ref _mensaje, value); }

        public ObservableCollection<CalleRecorridoEditable> Calles { get; } = new ObservableCollection<CalleRecorridoEditable>();
        public ObservableCollection<PasilloRecorridoEditable> PasillosSinCalle { get; } = new ObservableCollection<PasilloRecorridoEditable>();

        private CalleRecorridoEditable _calleSeleccionada;
        public CalleRecorridoEditable CalleSeleccionada
        {
            get => _calleSeleccionada;
            set
            {
                if (SetProperty(ref _calleSeleccionada, value))
                {
                    PasilloSeleccionado = null;
                    NotificarComandos();
                }
            }
        }

        private PasilloRecorridoEditable _pasilloSeleccionado;
        /// <summary>Un pasillo de la calle seleccionada.</summary>
        public PasilloRecorridoEditable PasilloSeleccionado
        {
            get => _pasilloSeleccionado;
            set
            {
                if (SetProperty(ref _pasilloSeleccionado, value))
                {
                    NotificarComandos();
                }
            }
        }

        private PasilloRecorridoEditable _pasilloSinCalleSeleccionado;
        public PasilloRecorridoEditable PasilloSinCalleSeleccionado
        {
            get => _pasilloSinCalleSeleccionado;
            set
            {
                if (SetProperty(ref _pasilloSinCalleSeleccionado, value))
                {
                    NotificarComandos();
                }
            }
        }

        private CalleRecorridoEditable _calleDestino;
        /// <summary>A qué calle se lleva el pasillo seleccionado con «Mover a».</summary>
        public CalleRecorridoEditable CalleDestino
        {
            get => _calleDestino;
            set
            {
                if (SetProperty(ref _calleDestino, value))
                {
                    NotificarComandos();
                }
            }
        }

        private string _filaAMano;
        /// <summary>Desde qué fila se llega con la mano (las de arriba son de escalera). Vacío: no se toca.</summary>
        public string FilaAMano
        {
            get => _filaAMano;
            set
            {
                if (SetProperty(ref _filaAMano, value))
                {
                    ActualizarCambios();
                }
            }
        }

        private IReadOnlyList<TramoRecorridoFila> _tramos = Array.Empty<TramoRecorridoFila>();
        public IReadOnlyList<TramoRecorridoFila> Tramos { get => _tramos; private set => SetProperty(ref _tramos, value); }

        private string _textoTramos;
        /// <summary>Encima de los tramos: si son los guardados o la vista previa, y cuántos.</summary>
        public string TextoTramos { get => _textoTramos; private set => SetProperty(ref _textoTramos, value); }

        private bool _hayCambios;
        public bool HayCambios
        {
            get => _hayCambios;
            private set
            {
                if (SetProperty(ref _hayCambios, value))
                {
                    GuardarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public IAsyncRelayCommand CargarCommand { get; }
        public IRelayCommand SubirCalleCommand { get; }
        public IRelayCommand BajarCalleCommand { get; }
        public IRelayCommand NuevaCalleCommand { get; }
        public IRelayCommand BorrarCalleCommand { get; }
        public IRelayCommand SerpentinaCommand { get; }
        public IRelayCommand SubirPasilloCommand { get; }
        public IRelayCommand BajarPasilloCommand { get; }
        public IRelayCommand QuitarPasilloCommand { get; }
        public IRelayCommand MoverPasilloCommand { get; }
        public IRelayCommand AnadirPasilloSinCalleCommand { get; }
        public IRelayCommand AnadirPasilloCommand { get; }
        public IAsyncRelayCommand VistaPreviaCommand { get; }
        public IAsyncRelayCommand GuardarCommand { get; }

        // ------------------------------------------------------------------ reglas puras

        internal static string NormalizarSentido(string sentido)
            => string.Equals(sentido?.Trim(), SENTIDO_VUELTA, StringComparison.OrdinalIgnoreCase) ? SENTIDO_VUELTA : SENTIDO_IDA;

        internal static string SentidoContrario(string sentido) => NormalizarSentido(sentido) == SENTIDO_IDA ? SENTIDO_VUELTA : SENTIDO_IDA;

        /// <summary>Como la API: «4» es el pasillo «004». Lo que no es un número se deja como está (sin espacios, en mayúsculas).</summary>
        internal static string NormalizarPasillo(string pasillo)
        {
            string texto = pasillo?.Trim().ToUpperInvariant() ?? string.Empty;
            return texto.Length > 0 && texto.Length < 3 && texto.All(char.IsDigit) ? texto.PadLeft(3, '0') : texto;
        }

        // ------------------------------------------------------------------ leer

        public void AlLlegar(ParametrosNavegacion parametros)
        {
            if (_huellaCargada == null && !EstaOcupado)
            {
                _ = CargarAsync();
            }
        }

        /// <summary>«Recargar»: si hay cambios sin guardar, pregunta antes de perderlos.</summary>
        internal async Task RecargarAsync()
        {
            if (HayCambios && _dialogos != null && !_dialogos.ShowConfirmationAnswer("Cambios sin guardar",
                "Hay cambios en el recorrido sin guardar y se perderán. ¿Quiere volver a leerlo igualmente?", true))
            {
                return;
            }
            await CargarAsync().ConfigureAwait(true);
        }

        internal async Task CargarAsync()
        {
            Mensaje = null;
            EstaOcupado = true;
            try
            {
                RecorridoAlmacen recorrido = await _servicio.Leer(Empresa, Almacen).ConfigureAwait(true);
                Aplicar(recorrido);
            }
            catch (RecorridoAlmacenException ex)
            {
                if (ex.Codigo == 503)
                {
                    NoDisponible(ex.Message);
                }
                else
                {
                    Mensaje = ex.Message;
                }
            }
            catch (Exception ex)
            {
                Mensaje = "No se ha podido leer el recorrido: " + ex.Message;
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        private void NoDisponible(string motivo)
        {
            Disponible = false;
            Aviso = string.IsNullOrWhiteSpace(motivo) ? MENSAJE_SIN_TABLAS : motivo.Trim() + " Desde aquí no se puede cambiar nada.";
        }

        /// <summary>Pinta lo que devuelve la API (al leer y al guardar) y lo toma como «sin cambios».</summary>
        private void Aplicar(RecorridoAlmacen recorrido)
        {
            recorrido ??= new RecorridoAlmacen();
            _cargando = true;
            try
            {
                string calleSeleccionada = CalleSeleccionada?.Calle;
                Calles.Clear();
                foreach (CalleRecorrido calle in recorrido.Calles ?? new List<CalleRecorrido>())
                {
                    Calles.Add(new CalleRecorridoEditable(calle.Calle?.Trim(), calle.Sentido, calle.Descripcion?.Trim(),
                        (calle.Pasillos ?? new List<PasilloRecorrido>()).Select(Pasillo)));
                }
                PasillosSinCalle.Clear();
                foreach (PasilloRecorrido pasillo in recorrido.PasillosSinCalle ?? new List<PasilloRecorrido>())
                {
                    PasillosSinCalle.Add(Pasillo(pasillo));
                }
                _filaAMano = recorrido.FilaAMano > 0 ? recorrido.FilaAMano.ToString(CultureInfo.InvariantCulture) : null;
                OnPropertyChanged(nameof(FilaAMano));
                CalleSeleccionada = Calles.FirstOrDefault(c => c.Calle == calleSeleccionada) ?? Calles.FirstOrDefault();
                CalleDestino = null;
                PasilloSinCalleSeleccionado = null;
                PonerTramos(recorrido.Tramos, false);
            }
            finally
            {
                _cargando = false;
            }
            _huellaCargada = Huella();
            HayCambios = false;
            if (!recorrido.TablasCreadas)
            {
                NoDisponible(null);
            }
            else
            {
                Disponible = true;
                Aviso = SoloLectura ? MENSAJE_SOLO_LECTURA : null;
            }
        }

        private static PasilloRecorridoEditable Pasillo(PasilloRecorrido pasillo)
            => new PasilloRecorridoEditable(pasillo.Pasillo?.Trim(), pasillo.Nombre?.Trim(), pasillo.Huecos);

        private void PonerTramos(List<TramoRecorrido> tramos, bool vistaPrevia)
        {
            Tramos = (tramos ?? new List<TramoRecorrido>()).Select(t => new TramoRecorridoFila
            {
                Orden = t.Orden,
                Calle = string.IsNullOrWhiteSpace(t.Calle) ? "(sin calle)" : t.Calle.Trim(),
                Pasillo = t.Pasillo?.Trim(),
                Columna = t.Columna?.Trim(),
                Nivel = t.Nivel == "AMano" ? "A mano" : t.Nivel,
                Filas = string.Join(", ", (t.Filas ?? new List<string>()).Select(f => f?.Trim()))
            }).ToList();
            TextoTramos = vistaPrevia
                ? $"Vista previa (sin guardar): {TextoNumeroTramos(Tramos.Count)} en este orden."
                : $"Recorrido guardado: {TextoNumeroTramos(Tramos.Count)} en este orden.";
        }

        private static string TextoNumeroTramos(int n) => n == 1 ? "1 tramo" : $"{n} tramos";

        // ------------------------------------------------------------------ cambios

        private void AlCambiarCalles(object sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (CalleRecorridoEditable calle in e.NewItems?.OfType<CalleRecorridoEditable>() ?? Enumerable.Empty<CalleRecorridoEditable>())
            {
                calle.PropertyChanged -= AlCambiarElemento;
                calle.PropertyChanged += AlCambiarElemento;
                calle.Pasillos.CollectionChanged -= AlCambiarColeccion;
                calle.Pasillos.CollectionChanged += AlCambiarColeccion;
                foreach (PasilloRecorridoEditable pasillo in calle.Pasillos)
                {
                    Enganchar(pasillo);
                }
            }
            AlCambiarColeccion(sender, e);
        }

        private void AlCambiarColeccion(object sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (PasilloRecorridoEditable pasillo in e.NewItems?.OfType<PasilloRecorridoEditable>() ?? Enumerable.Empty<PasilloRecorridoEditable>())
            {
                Enganchar(pasillo);
            }
            ActualizarCambios();
        }

        private void Enganchar(PasilloRecorridoEditable pasillo)
        {
            pasillo.PropertyChanged -= AlCambiarElemento;
            pasillo.PropertyChanged += AlCambiarElemento;
        }

        private void AlCambiarElemento(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(CalleRecorridoEditable.ResumenPasillos))
            {
                ActualizarCambios();
            }
        }

        private void ActualizarCambios()
        {
            if (_cargando || _huellaCargada == null)
            {
                return;
            }
            HayCambios = Huella() != _huellaCargada;
            NotificarComandos();
        }

        /// <summary>Todo lo que se manda al guardar, en texto, para saber si algo ha cambiado desde que se leyó.</summary>
        private string Huella()
        {
            var texto = new StringBuilder();
            texto.Append("F:").Append(FilaAMano?.Trim()).Append('|');
            foreach (CalleRecorridoEditable calle in Calles)
            {
                texto.Append("C:").Append(calle.Calle?.Trim()).Append('/').Append(calle.Sentido).Append('/').Append(calle.Descripcion?.Trim()).Append('[');
                foreach (PasilloRecorridoEditable pasillo in calle.Pasillos)
                {
                    texto.Append(pasillo.Pasillo).Append('=').Append(pasillo.Nombre?.Trim()).Append(';');
                }
                texto.Append(']');
            }
            texto.Append("|S:");
            foreach (PasilloRecorridoEditable pasillo in PasillosSinCalle)
            {
                texto.Append(pasillo.Pasillo).Append('=').Append(pasillo.Nombre?.Trim()).Append(';');
            }
            return texto.ToString();
        }

        private void NotificarEstado()
        {
            OnPropertyChanged(nameof(Editable));
            OnPropertyChanged(nameof(NoEditable));
            OnPropertyChanged(nameof(SoloLectura));
            CargarCommand?.NotifyCanExecuteChanged();
            NotificarComandos();
        }

        private void NotificarComandos()
        {
            SubirCalleCommand?.NotifyCanExecuteChanged();
            BajarCalleCommand?.NotifyCanExecuteChanged();
            NuevaCalleCommand?.NotifyCanExecuteChanged();
            BorrarCalleCommand?.NotifyCanExecuteChanged();
            SerpentinaCommand?.NotifyCanExecuteChanged();
            SubirPasilloCommand?.NotifyCanExecuteChanged();
            BajarPasilloCommand?.NotifyCanExecuteChanged();
            QuitarPasilloCommand?.NotifyCanExecuteChanged();
            MoverPasilloCommand?.NotifyCanExecuteChanged();
            AnadirPasilloSinCalleCommand?.NotifyCanExecuteChanged();
            AnadirPasilloCommand?.NotifyCanExecuteChanged();
            VistaPreviaCommand?.NotifyCanExecuteChanged();
            GuardarCommand?.NotifyCanExecuteChanged();
        }

        // ------------------------------------------------------------------ calles

        internal void SubirCalle() => MoverCalle(-1);
        internal void BajarCalle() => MoverCalle(1);

        private void MoverCalle(int desplazamiento)
        {
            if (!Editable || CalleSeleccionada == null)
            {
                return;
            }
            int indice = Calles.IndexOf(CalleSeleccionada);
            int destino = indice + desplazamiento;
            if (indice < 0 || destino < 0 || destino >= Calles.Count)
            {
                return;
            }
            Calles.Move(indice, destino);
            NotificarComandos();
        }

        /// <summary>Una calle nueva después de la seleccionada (o al final), con el nombre que diga el usuario.</summary>
        internal void NuevaCalle()
        {
            if (!Editable)
            {
                return;
            }
            string nombre = _dialogos?.GetText("Nueva calle", "Nombre corto de la calle (hasta 10 caracteres, sin repetir):", NombreLibre())?.Trim();
            if (string.IsNullOrEmpty(nombre))
            {
                return;
            }
            if (Calles.Any(c => string.Equals(c.Calle?.Trim(), nombre, StringComparison.OrdinalIgnoreCase)))
            {
                Mensaje = $"Ya hay una calle «{nombre}».";
                return;
            }
            var calle = new CalleRecorridoEditable(nombre, SENTIDO_IDA, null, null);
            int indice = CalleSeleccionada == null ? Calles.Count : Calles.IndexOf(CalleSeleccionada) + 1;
            Calles.Insert(indice, calle);
            CalleSeleccionada = calle;
            Mensaje = null;
        }

        /// <summary>La primera letra (a, b, c…) que no tiene ya una calle.</summary>
        internal string NombreLibre()
        {
            for (char letra = 'a'; letra <= 'z'; letra++)
            {
                string nombre = letra.ToString();
                if (!Calles.Any(c => string.Equals(c.Calle?.Trim(), nombre, StringComparison.OrdinalIgnoreCase)))
                {
                    return nombre;
                }
            }
            return string.Empty;
        }

        /// <summary>Borra la calle seleccionada; sus pasillos pasan a «sin calle» (al final del recorrido).</summary>
        internal void BorrarCalle()
        {
            CalleRecorridoEditable calle = CalleSeleccionada;
            if (!Editable || calle == null)
            {
                return;
            }
            if (calle.Pasillos.Count > 0 && _dialogos != null && !_dialogos.ShowConfirmationAnswer("Borrar calle",
                $"La calle «{calle.Calle}» tiene {calle.Pasillos.Count} pasillo(s): pasarán a «sin calle», al final del recorrido. ¿La borra?"))
            {
                return;
            }
            int indice = Calles.IndexOf(calle);
            foreach (PasilloRecorridoEditable pasillo in calle.Pasillos.ToList())
            {
                calle.Pasillos.Remove(pasillo);
                MeterSinCalle(pasillo);
            }
            Calles.Remove(calle);
            if (CalleDestino == calle)
            {
                CalleDestino = null;
            }
            CalleSeleccionada = Calles.Count == 0 ? null : Calles[Math.Min(indice, Calles.Count - 1)];
        }

        /// <summary>Alterna el sentido de las calles en su orden (A, D, A…) empezando por el que tenga la primera.</summary>
        internal void Serpentina()
        {
            if (!Editable || Calles.Count == 0)
            {
                return;
            }
            string primero = NormalizarSentido(Calles[0].Sentido);
            for (int i = 0; i < Calles.Count; i++)
            {
                Calles[i].Sentido = i % 2 == 0 ? primero : SentidoContrario(primero);
            }
        }

        // ------------------------------------------------------------------ pasillos

        internal void SubirPasillo() => MoverPasilloEnSuCalle(-1);
        internal void BajarPasillo() => MoverPasilloEnSuCalle(1);

        private void MoverPasilloEnSuCalle(int desplazamiento)
        {
            if (!Editable || CalleSeleccionada == null || PasilloSeleccionado == null)
            {
                return;
            }
            ObservableCollection<PasilloRecorridoEditable> pasillos = CalleSeleccionada.Pasillos;
            int indice = pasillos.IndexOf(PasilloSeleccionado);
            int destino = indice + desplazamiento;
            if (indice < 0 || destino < 0 || destino >= pasillos.Count)
            {
                return;
            }
            pasillos.Move(indice, destino);
            NotificarComandos();
        }

        /// <summary>Saca el pasillo seleccionado de su calle: pasa a «sin calle».</summary>
        internal void QuitarPasillo()
        {
            PasilloRecorridoEditable pasillo = PasilloSeleccionado;
            if (!Editable || CalleSeleccionada == null || pasillo == null)
            {
                return;
            }
            CalleSeleccionada.Pasillos.Remove(pasillo);
            PasilloSeleccionado = null;
            MeterSinCalle(pasillo);
            PasilloSinCalleSeleccionado = pasillo;
        }

        /// <summary>Lleva el pasillo seleccionado al final de <see cref="CalleDestino"/>.</summary>
        internal void MoverPasillo()
        {
            PasilloRecorridoEditable pasillo = PasilloSeleccionado;
            CalleRecorridoEditable destino = CalleDestino;
            if (!Editable || CalleSeleccionada == null || pasillo == null || destino == null || destino == CalleSeleccionada)
            {
                return;
            }
            CalleSeleccionada.Pasillos.Remove(pasillo);
            destino.Pasillos.Add(pasillo);
            CalleSeleccionada = destino;
            PasilloSeleccionado = pasillo;
        }

        /// <summary>Lleva el pasillo sin calle seleccionado al final de la calle seleccionada.</summary>
        internal void AnadirPasilloSinCalle()
        {
            PasilloRecorridoEditable pasillo = PasilloSinCalleSeleccionado;
            if (!Editable || CalleSeleccionada == null || pasillo == null)
            {
                return;
            }
            PasillosSinCalle.Remove(pasillo);
            PasilloSinCalleSeleccionado = null;
            CalleSeleccionada.Pasillos.Add(pasillo);
            PasilloSeleccionado = pasillo;
        }

        /// <summary>
        /// Un pasillo tecleado al final de la calle seleccionada (por ejemplo, uno que aún no tiene nada ubicado y no sale en
        /// «sin calle»). Si está sin calle, se mueve; si ya está en otra calle, se dice y no se toca.
        /// </summary>
        internal void AnadirPasillo()
        {
            if (!Editable || CalleSeleccionada == null)
            {
                return;
            }
            string pasillo = NormalizarPasillo(_dialogos?.GetText("Añadir pasillo", $"Número del pasillo que añade a la calle «{CalleSeleccionada.Calle}»:"));
            if (string.IsNullOrEmpty(pasillo))
            {
                return;
            }
            CalleRecorridoEditable donde = Calles.FirstOrDefault(c => c.Pasillos.Any(p => p.Pasillo == pasillo));
            if (donde != null)
            {
                Mensaje = $"El pasillo {pasillo} ya está en la calle «{donde.Calle}»: muévalo desde allí.";
                return;
            }
            PasilloRecorridoEditable sinCalle = PasillosSinCalle.FirstOrDefault(p => p.Pasillo == pasillo);
            if (sinCalle != null)
            {
                PasillosSinCalle.Remove(sinCalle);
            }
            PasilloRecorridoEditable nuevo = sinCalle ?? new PasilloRecorridoEditable(pasillo, null, 0);
            CalleSeleccionada.Pasillos.Add(nuevo);
            PasilloSeleccionado = nuevo;
            Mensaje = null;
        }

        /// <summary>Los pasillos sin calle van ordenados por pasillo, como los da la API.</summary>
        private void MeterSinCalle(PasilloRecorridoEditable pasillo)
        {
            int indice = 0;
            while (indice < PasillosSinCalle.Count && string.CompareOrdinal(PasillosSinCalle[indice].Pasillo, pasillo.Pasillo) < 0)
            {
                indice++;
            }
            PasillosSinCalle.Insert(indice, pasillo);
        }

        // ------------------------------------------------------------------ vista previa y guardar

        /// <summary>El cuerpo del PUT con lo que hay en pantalla; null y <paramref name="error"/> si la fila a mano no es un número.</summary>
        internal PeticionRecorridoAlmacen Peticion(out string error)
        {
            error = null;
            int? filaAMano = null;
            if (!string.IsNullOrWhiteSpace(FilaAMano))
            {
                if (!int.TryParse(FilaAMano.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int fila))
                {
                    error = MENSAJE_FILA_A_MANO;
                    return null;
                }
                filaAMano = fila;
            }
            return new PeticionRecorridoAlmacen
            {
                FilaAMano = filaAMano,
                Calles = Calles.Select(c => new PeticionCalleRecorrido
                {
                    Calle = c.Calle?.Trim(),
                    Sentido = NormalizarSentido(c.Sentido),
                    Descripcion = string.IsNullOrWhiteSpace(c.Descripcion) ? null : c.Descripcion.Trim(),
                    Pasillos = c.Pasillos.Select(PasilloPeticion).ToList()
                }).ToList(),
                PasillosSinCalle = PasillosSinCalle.Select(PasilloPeticion).ToList()
            };
        }

        private static PeticionPasilloRecorrido PasilloPeticion(PasilloRecorridoEditable pasillo) => new PeticionPasilloRecorrido
        {
            Pasillo = pasillo.Pasillo,
            Nombre = string.IsNullOrWhiteSpace(pasillo.Nombre) ? null : pasillo.Nombre.Trim()
        };

        /// <summary>PUT con ensayo=true: enseña los tramos que saldrían sin guardar nada (lo de la pantalla no cambia).</summary>
        internal async Task VistaPreviaAsync()
        {
            if (!Editable)
            {
                return;
            }
            PeticionRecorridoAlmacen peticion = Peticion(out string error);
            if (peticion == null)
            {
                Mensaje = error;
                return;
            }
            Mensaje = null;
            EstaOcupado = true;
            try
            {
                RecorridoAlmacen recorrido = await _servicio.Guardar(Empresa, Almacen, peticion, true).ConfigureAwait(true);
                PonerTramos(recorrido?.Tramos, true);
                Mensaje = "Vista previa: así quedaría el recorrido. No se ha guardado nada.";
            }
            catch (Exception ex)
            {
                TratarError(ex, "No se ha podido hacer la vista previa: ");
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        /// <summary>PUT de verdad (con confirmación): sustituye el recorrido entero y pinta lo que queda guardado.</summary>
        internal async Task GuardarAsync()
        {
            if (!Editable)
            {
                return;
            }
            PeticionRecorridoAlmacen peticion = Peticion(out string error);
            if (peticion == null)
            {
                Mensaje = error;
                return;
            }
            if (_dialogos != null && !_dialogos.ShowConfirmationAnswer("Guardar el recorrido",
                $"Se va a sustituir el recorrido entero del almacén {Almacen} ({peticion.Calles.Count} calles). Los mozos lo verán en su " +
                "próxima ruta de preparación. ¿Lo guarda?"))
            {
                return;
            }
            Mensaje = null;
            EstaOcupado = true;
            try
            {
                RecorridoAlmacen recorrido = await _servicio.Guardar(Empresa, Almacen, peticion, false).ConfigureAwait(true);
                Aplicar(recorrido);
                Mensaje = "Recorrido guardado.";
            }
            catch (Exception ex)
            {
                TratarError(ex, "No se ha podido guardar el recorrido: ");
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        /// <summary>400: el motivo de la API tal cual. 403: además, solo lectura. 503: sin servicio, nada editable.</summary>
        private void TratarError(Exception ex, string prefijo)
        {
            if (ex is RecorridoAlmacenException api)
            {
                Mensaje = api.Message;
                if (api.Codigo == 403)
                {
                    _sinPermisoEnLaApi = true;
                    Aviso = api.Message;
                    NotificarEstado();
                }
                else if (api.Codigo == 503)
                {
                    NoDisponible(api.Message);
                }
            }
            else
            {
                Mensaje = prefijo + ex.Message;
            }
            _dialogos?.ShowError(Mensaje);
        }

        /// <summary>Al cerrar la pestaña con cambios sin guardar, pregunta (por defecto, no cerrar).</summary>
        public bool ConfirmTabClose()
        {
            if (!HayCambios || _dialogos == null)
            {
                return true;
            }
            return _dialogos.ShowConfirmationAnswer("Cambios sin guardar",
                "Hay cambios en el recorrido sin guardar y se perderán. ¿Quiere cerrar igualmente?", true);
        }
    }
}
