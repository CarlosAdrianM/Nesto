using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto.ViewModels
{
    /// <summary>Una opción de un filtro o un botón de resolución: el valor que va a la API y el texto que se enseña.</summary>
    public class OpcionDiferencia
    {
        public OpcionDiferencia(string valor, string texto)
        {
            Valor = valor;
            Texto = texto;
        }

        public string Valor { get; }
        public string Texto { get; }
    }

    /// <summary>Una fila de la lista: la diferencia de la API con los textos ya preparados para la vista.</summary>
    public class FilaDiferenciaReposicion
    {
        public FilaDiferenciaReposicion(DiferenciaReposicion diferencia)
        {
            Diferencia = diferencia ?? throw new ArgumentNullException(nameof(diferencia));
            Opciones = (diferencia.ResolucionesPosibles ?? new List<string>())
                .Select(r => new OpcionDiferencia(r, DiferenciasReposicionViewModel.TextoResolucion(r)))
                .ToList();
        }

        public DiferenciaReposicion Diferencia { get; }
        public int Id => Diferencia.Id;
        public int Traspaso => Diferencia.Traspaso;
        public string Ruta => $"{Diferencia.Origen?.Trim()} → {Diferencia.Destino?.Trim()}";
        public string Producto => Diferencia.Producto?.Trim();
        public string Descripcion => Diferencia.Descripcion?.Trim();
        public int Enviado => Diferencia.Enviado;
        public int Leido => Diferencia.Leido;
        /// <summary>«−1» o «+1».</summary>
        public string TextoDiferencia => Diferencia.Diferencia > 0
            ? "+" + Diferencia.Diferencia.ToString(CultureInfo.InvariantCulture)
            : Diferencia.Diferencia < 0 ? "−" + (-Diferencia.Diferencia).ToString(CultureInfo.InvariantCulture) : "0";
        public string Tipo => !string.IsNullOrWhiteSpace(Diferencia.Tipo) ? Diferencia.Tipo : Diferencia.Diferencia < 0 ? "Falta" : "Sobra";
        public string QuePaso => Diferencia.QuePaso;
        public string QueComprobar => Diferencia.QueComprobar;
        public string HuecoSalida => Diferencia.HuecoSalida?.Trim();
        public string Huecos => string.IsNullOrWhiteSpace(Diferencia.Huecos) ? "Ahora no hay stock libre en ningún hueco" : Diferencia.Huecos.Trim();
        public string Recepcion => $"{Diferencia.FechaRecepcion:dd/MM/yyyy HH:mm} · {Diferencia.UsuarioRecepcion?.Trim()}";
        public DateTime FechaRecepcion => Diferencia.FechaRecepcion;
        public string UsuarioRecepcion => Diferencia.UsuarioRecepcion?.Trim();
        public string Preparador => Diferencia.Preparador?.Trim();
        public string TextoAjuste => DiferenciasReposicionViewModel.TextoAjuste(Diferencia);
        public bool EstaPendiente => string.Equals(Diferencia.Estado, DiferenciaReposicion.ESTADO_PENDIENTE, StringComparison.OrdinalIgnoreCase);
        public string Estado => EstaPendiente ? "Pendiente" : "Resuelta";
        public string TextoResolucion => string.IsNullOrWhiteSpace(Diferencia.Resolucion)
            ? null
            : DiferenciasReposicionViewModel.TextoResolucion(Diferencia.Resolucion);
        /// <summary>«Comprobada, cuadra · Carlos, 09/10/2026 10:15».</summary>
        public string Resuelta => EstaPendiente
            ? null
            : $"{TextoResolucion} · {Diferencia.UsuarioResolucion?.Trim()}" +
              (Diferencia.FechaResolucion.HasValue ? $", {Diferencia.FechaResolucion.Value:dd/MM/yyyy HH:mm}" : string.Empty);
        public string Comentario => Diferencia.Comentario?.Trim();
        public bool MostrarComentarioResuelto => !EstaPendiente && !string.IsNullOrWhiteSpace(Comentario);
        public IReadOnlyList<OpcionDiferencia> Opciones { get; }
        public bool PuedeResolverse => EstaPendiente && Opciones.Count > 0;
    }

    /// <summary>
    /// Nesto#526 (NestoAPI#634): las diferencias entre lo enviado y lo recibido en las reposiciones. Al terminar la
    /// recepción la API ya ha dejado constancia y ha ajustado el stock del origen; aquí se ve qué queda por comprobar en el
    /// origen y se resuelve con su motivo. Qué se puede elegir y quién puede lo decide la API (su mensaje se enseña tal cual).
    /// </summary>
    public class DiferenciasReposicionViewModel : ObservableObject, IReceptorNavegacion
    {
        public const int LONGITUD_COMENTARIO = 500;

        private readonly IServicioDiferenciasReposicion _servicio;
        private readonly IServicioDialogos _dialogos;
        private readonly IServicioNavegacion _navegacion;
        private bool _filtrosListos;

        /// <param name="navegacion">Sugerencia 790 (Paloma): para abrir la ficha del producto con doble clic. Sin él, el doble clic no hace nada.</param>
        public DiferenciasReposicionViewModel(IServicioDiferenciasReposicion servicio, IConfiguracion configuracion, IServicioDialogos dialogos,
            IServicioNavegacion navegacion)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            _navegacion = navegacion;
            PuedeElegirAlmacen = configuracion != null
                && (configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN) || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION));
            Almacenes = PuedeElegirAlmacen
                ? new List<OpcionDiferencia>
                {
                    new OpcionDiferencia(null, "Todos los almacenes"),
                    new OpcionDiferencia(Constantes.Almacenes.ALMACEN_ALGETE, "Algete (ALG)"),
                    new OpcionDiferencia(Constantes.Almacenes.ALMACEN_REINA, "Reina (REI)"),
                    new OpcionDiferencia(Constantes.Almacenes.ALMACEN_ALCOBENDAS, "Alcobendas (ALC)")
                }
                : new List<OpcionDiferencia> { new OpcionDiferencia(null, "Su almacén") };
            CargarCommand = new AsyncRelayCommand(CargarAsync);
            ResolverCommand = new AsyncRelayCommand<string>(ResolverAsync, PuedeResolver);
            // Sugerencia 790 (Paloma): doble clic en el producto abre su ficha, como en «Recibir reposición» (sugerencia 599)
            AbrirFichaProductoCommand = new RelayCommand<string>(AbrirFichaProducto);
            _filtrosListos = true;
        }

        public static IReadOnlyList<OpcionDiferencia> Estados { get; } = new List<OpcionDiferencia>
        {
            new OpcionDiferencia(DiferenciaReposicion.ESTADO_PENDIENTE, "Pendientes"),
            new OpcionDiferencia(DiferenciaReposicion.ESTADO_RESUELTA, "Resueltas"),
            new OpcionDiferencia(DiferenciaReposicion.ESTADO_TODAS, "Todas")
        };

        /// <summary>Almacén y Dirección eligen el almacén de origen; los demás ven las del suyo (lo decide la API).</summary>
        public IReadOnlyList<OpcionDiferencia> Almacenes { get; }
        public bool PuedeElegirAlmacen { get; }

        public string Titulo => "Diferencias de reposición";
        public string Ayuda => "Cuando lo recibido no coincide con lo enviado, el stock del origen ya se ha ajustado al terminar la recepción. " +
            "Compruebe en el origen que cuadra y elija qué ha pasado.";
        public string Empresa { get; set; } = Constantes.Empresas.EMPRESA_DEFECTO;

        public ObservableCollection<FilaDiferenciaReposicion> Diferencias { get; } = new ObservableCollection<FilaDiferenciaReposicion>();

        private string _estado = DiferenciaReposicion.ESTADO_PENDIENTE;
        /// <summary>Pendiente (por defecto), Resuelta o Todas. Al cambiarlo se vuelve a leer.</summary>
        public string Estado
        {
            get => _estado;
            set
            {
                if (SetProperty(ref _estado, value) && _filtrosListos)
                {
                    _ = CargarAsync();
                }
            }
        }

        private string _almacen;
        /// <summary>El almacén de origen (donde se comprueba); null, todos los que puede ver. Al cambiarlo se vuelve a leer.</summary>
        public string Almacen
        {
            get => _almacen;
            set
            {
                if (SetProperty(ref _almacen, value) && _filtrosListos)
                {
                    _ = CargarAsync();
                }
            }
        }

        private FilaDiferenciaReposicion _seleccionada;
        public FilaDiferenciaReposicion Seleccionada
        {
            get => _seleccionada;
            set
            {
                if (SetProperty(ref _seleccionada, value))
                {
                    Comentario = null;
                    OnPropertyChanged(nameof(HaySeleccionada));
                    ResolverCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool HaySeleccionada => Seleccionada != null;

        private string _comentario;
        public string Comentario { get => _comentario; set => SetProperty(ref _comentario, value); }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    ResolverCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private bool _cargada;
        /// <summary>Ya se ha leído al menos una vez (para no volver a leer cada vez que la vista se engancha).</summary>
        public bool Cargada { get => _cargada; private set => SetProperty(ref _cargada, value); }

        private string _mensaje;
        public string Mensaje { get => _mensaje; set => SetProperty(ref _mensaje, value); }

        /// <summary>«3 pendientes», «Ninguna pendiente»…</summary>
        public string Resumen
        {
            get
            {
                int pendientes = Diferencias.Count(d => d.EstaPendiente);
                int resueltas = Diferencias.Count - pendientes;
                switch (Estado)
                {
                    case DiferenciaReposicion.ESTADO_PENDIENTE:
                        return pendientes == 0 ? "No queda ninguna diferencia por comprobar." : pendientes == 1 ? "1 pendiente de comprobar" : $"{pendientes} pendientes de comprobar";
                    case DiferenciaReposicion.ESTADO_RESUELTA:
                        return resueltas == 1 ? "1 resuelta" : $"{resueltas} resueltas";
                    default:
                        return $"{pendientes} pendientes y {resueltas} resueltas";
                }
            }
        }

        public IAsyncRelayCommand CargarCommand { get; }
        public IAsyncRelayCommand<string> ResolverCommand { get; }

        /// <summary>
        /// Sugerencia 790 (Paloma): abre la ficha del producto en la pestaña de productos (esta se queda como estaba), igual
        /// que en «Recibir reposición» (sugerencia 599). Recibe la referencia; vacía, no hace nada.
        /// </summary>
        public IRelayCommand<string> AbrirFichaProductoCommand { get; }

        private void AbrirFichaProducto(string producto)
        {
            if (string.IsNullOrWhiteSpace(producto) || _navegacion == null)
            {
                return;
            }
            var parameters = new ParametrosNavegacion
            {
                { "numeroProductoParameter", producto.Trim() }
            };
            _navegacion.RequestNavigate("MainRegion", "ProductoView", parameters);
        }

        /// <summary>
        /// Cada vez que se llega (la cinta o el aviso de la campana, con la pestaña nueva o la ya abierta) se vuelve a leer:
        /// otra persona puede haber resuelto alguna entretanto.
        /// </summary>
        public void AlLlegar(ParametrosNavegacion parametros)
        {
            if (!EstaOcupado)
            {
                _ = CargarAsync();
            }
        }

        public async Task CargarAsync()
        {
            Mensaje = null;
            await Ocupado(async () => Poner(await _servicio.Leer(Empresa, Estado, Almacen).ConfigureAwait(true))).ConfigureAwait(true);
            Cargada = true;
        }

        private bool PuedeResolver(string resolucion)
            => !EstaOcupado && Seleccionada != null && Seleccionada.PuedeResolverse
               && Seleccionada.Opciones.Any(o => string.Equals(o.Valor, resolucion, StringComparison.OrdinalIgnoreCase));

        private async Task ResolverAsync(string resolucion)
        {
            FilaDiferenciaReposicion fila = Seleccionada;
            if (fila == null || string.IsNullOrWhiteSpace(resolucion))
            {
                return;
            }
            string comentario = string.IsNullOrWhiteSpace(Comentario) ? null : Comentario.Trim();
            if (comentario != null && comentario.Length > LONGITUD_COMENTARIO)
            {
                Mensaje = $"El comentario no puede pasar de {LONGITUD_COMENTARIO} caracteres (lleva {comentario.Length}).";
                return;
            }
            string pregunta = PreguntaAntesDeResolver(fila, resolucion);
            if (pregunta != null && _dialogos != null && !await _dialogos.ShowConfirmationAsync(Titulo, pregunta).ConfigureAwait(true))
            {
                return;
            }

            Mensaje = null;
            ResultadoResolverDiferenciaReposicion resultado = null;
            bool yaResuelta = false;
            EstaOcupado = true;
            try
            {
                resultado = await _servicio.Resolver(Empresa, fila.Id, new ResolverDiferenciaReposicion { Resolucion = resolucion, Comentario = comentario })
                    .ConfigureAwait(true);
            }
            catch (DiferenciasReposicionException ex)
            {
                Mensaje = ex.Message;
                yaResuelta = ex.Codigo == 409;
            }
            catch (Exception ex)
            {
                Mensaje = "No se ha podido hablar con el servidor: " + ex.Message;
            }
            finally
            {
                EstaOcupado = false;
            }

            if (yaResuelta)
            {
                // La ha resuelto otra persona entretanto: se vuelve a leer sin perder el motivo
                string motivo = Mensaje;
                await CargarAsync().ConfigureAwait(true);
                Mensaje = Mensaje == null ? motivo : motivo + " " + Mensaje;
                return;
            }
            if (resultado == null)
            {
                return;
            }

            int indice = Diferencias.IndexOf(fila);
            Seleccionada = null;
            if (indice >= 0)
            {
                Diferencias.RemoveAt(indice);
                if (resultado.Diferencia != null && Estado != DiferenciaReposicion.ESTADO_PENDIENTE)
                {
                    Diferencias.Insert(indice, new FilaDiferenciaReposicion(resultado.Diferencia));
                }
            }
            OnPropertyChanged(nameof(Resumen));
            Mensaje = string.IsNullOrWhiteSpace(resultado.Mensaje)
                ? $"Resuelta la diferencia de {fila.Producto} de la reposición {fila.Traspaso}: {TextoResolucion(resolucion)}."
                : resultado.Mensaje;
        }

        /// <summary>Las que mueven stock se confirman antes; «Comprobada, cuadra» solo cierra y no pregunta.</summary>
        internal static string PreguntaAntesDeResolver(FilaDiferenciaReposicion fila, string resolucion)
        {
            string que = $"{fila.Producto} de la reposición {fila.Traspaso} ({fila.Ruta})";
            switch (resolucion)
            {
                case DiferenciaReposicion.RESOLUCION_NO_ESTABA:
                    return $"{que}: ¿confirma que no está en el origen? Se dará de baja (merma o inventario).";
                case DiferenciaReposicion.RESOLUCION_SOBRABA:
                    return $"{que}: ¿confirma que sigue en el origen? Se dará de alta por inventario.";
                case DiferenciaReposicion.RESOLUCION_ERROR_RECEPCION:
                    return $"{que}: ¿confirma que fue un error al recibir? Se deshará el ajuste del origen y se corregirá el destino.";
                default:
                    return null;
            }
        }

        private void Poner(IEnumerable<DiferenciaReposicion> diferencias)
        {
            int? seleccionada = Seleccionada?.Id;
            Seleccionada = null;
            Diferencias.Clear();
            foreach (DiferenciaReposicion diferencia in diferencias ?? Enumerable.Empty<DiferenciaReposicion>())
            {
                if (diferencia != null)
                {
                    Diferencias.Add(new FilaDiferenciaReposicion(diferencia));
                }
            }
            if (seleccionada.HasValue)
            {
                Seleccionada = Diferencias.FirstOrDefault(d => d.Id == seleccionada.Value);
            }
            OnPropertyChanged(nameof(Resumen));
        }

        /// <summary>Lo que falla al hablar con la API se enseña en <see cref="Mensaje"/> tal cual (lo escribe el servidor).</summary>
        private async Task Ocupado(Func<Task> accion)
        {
            EstaOcupado = true;
            try
            {
                await accion().ConfigureAwait(true);
            }
            catch (DiferenciasReposicionException ex)
            {
                Mensaje = ex.Message;
            }
            catch (Exception ex)
            {
                Mensaje = "No se ha podido hablar con el servidor: " + ex.Message;
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        public static string TextoResolucion(string resolucion)
        {
            switch (resolucion?.Trim())
            {
                case DiferenciaReposicion.RESOLUCION_CONFIRMADA:
                    return "Comprobada, cuadra";
                case DiferenciaReposicion.RESOLUCION_NO_ESTABA:
                    return "No estaba (merma o inventario)";
                case DiferenciaReposicion.RESOLUCION_SOBRABA:
                    return "Sobraba (alta por inventario)";
                case DiferenciaReposicion.RESOLUCION_ERROR_RECEPCION:
                    return "Error al recibir (se corrige)";
                default:
                    return resolucion?.Trim();
            }
        }

        public static string TextoAjuste(DiferenciaReposicion diferencia)
        {
            switch (diferencia?.Ajuste?.Trim())
            {
                case DiferenciaReposicion.AJUSTE_HECHO:
                    return diferencia.NumOrdenAjuste.HasValue ? $"Ajustado (orden {diferencia.NumOrdenAjuste.Value})" : "Ajustado";
                case DiferenciaReposicion.AJUSTE_APAGADO:
                    return "Sin ajustar (el ajuste automático estaba apagado)";
                case DiferenciaReposicion.AJUSTE_FALLIDO:
                    return string.IsNullOrWhiteSpace(diferencia.AjusteMensaje) ? "Falló el ajuste" : "Falló el ajuste: " + diferencia.AjusteMensaje.Trim();
                case null:
                case "":
                    return "Por hacer";
                default:
                    return diferencia.Ajuste.Trim();
            }
        }
    }
}
