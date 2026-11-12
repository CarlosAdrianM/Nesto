using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto.ViewModels
{
    /// <summary>Una tienda que se puede elegir: el almacén que va a la API (null: la del usuario) y el texto que se enseña.</summary>
    public class OpcionTiendaFacturacion
    {
        public OpcionTiendaFacturacion(string almacen, string texto)
        {
            Almacen = almacen;
            Texto = texto;
        }

        public string Almacen { get; }
        public string Texto { get; }
    }

    /// <summary>Nesto#559: un tipo de informe que se puede elegir.</summary>
    public class OpcionTipoFacturacion
    {
        public OpcionTipoFacturacion(TipoInformeFacturacionTienda tipo, string texto)
        {
            Tipo = tipo;
            Texto = texto;
        }

        public TipoInformeFacturacionTienda Tipo { get; }
        public string Texto { get; }
    }

    /// <summary>Nesto#559: un año anterior del comparativo en una fila (las columnas «Base», «Var. €» y «Var. %» de ese año).</summary>
    public class AnteriorFilaFacturacion
    {
        public int Anio { get; set; }
        public int NumeroFacturas { get; set; }
        public decimal BaseImponible { get; set; }
        public decimal VariacionImporte { get; set; }
        /// <summary>Null si la base de ese año es 0 (no hay porcentaje).</summary>
        public decimal? VariacionPorcentaje { get; set; }
    }

    /// <summary>Nesto#559: una fila de la tabla del acumulado o del comparativo (un mes, o el total al final).</summary>
    public class FilaFacturacionTiendaAnual
    {
        public string Mes { get; set; }
        public bool EsTotal { get; set; }
        public int NumeroFacturas { get; set; }
        public decimal BaseImponible { get; set; }
        public decimal BaseImponibleCursos { get; set; }
        public decimal Acumulado { get; set; }
        /// <summary>En el orden de <see cref="FacturacionTiendaViewModel.AniosAnteriores"/> (vacío en el acumulado).</summary>
        public List<AnteriorFilaFacturacion> Anteriores { get; set; } = new List<AnteriorFilaFacturacion>();
    }

    /// <summary>
    /// Sugerencia 648 (Paloma, NestoAPI#658): el Excel con lo facturado en la tienda (cursos incluidos) para el «informe de cifras»
    /// de cada semana: por factura, cliente, dirección, comercial, forma de venta e importe, con los totales. Por defecto, la semana
    /// anterior de lunes a domingo. Dirección, Administración e Informática eligen la tienda; los demás sacan la suya (lo decide la
    /// API con su AlmacénPedidoVta, y su mensaje se enseña tal cual).
    /// El Excel NO se abre aquí: Nesto corre en el escritorio remoto, donde Office no tiene licencia (y no se quiere Excel consumiendo
    /// memoria en el servidor). La API lo manda por correo al usuario (a su CorreoDefecto, o al que escriba), que lo abre en su
    /// ordenador o en el móvil.
    /// <para>Nesto#559 (sugerencia 792 de Paloma, NestoAPI#719): además de la semana, el «Acumulado del año» (de enero a «hasta», mes a
    /// mes) y el «Comparativo» con los N años anteriores (2 por defecto) con la variación en € y en %. Esos dos se ven en pantalla y su
    /// Excel se guarda donde diga el usuario (tampoco se abre); el correo sigue siendo solo de la semana, como en la API.</para>
    /// </summary>
    public class FacturacionTiendaViewModel : ObservableObject, IReceptorNavegacion
    {
        public const string OPCION_SU_TIENDA = "Su tienda";
        public const string MENSAJE_SIN_CORREO = "No tiene puesto su correo en Nesto: escriba a qué correo se lo mandamos.";
        public const int ANIOS_POR_DEFECTO = 2;
        public const int ANIOS_MAXIMO = 10;
        public const string FILTRO_EXCEL = "Libro de Excel (*.xlsx)|*.xlsx";

        private readonly IServicioFacturacionTienda _servicio;
        private readonly IConfiguracion _configuracion;
        private readonly IServicioDialogos _dialogos;
        private readonly IGuardadorFicheros _guardador;
        private readonly Func<DateTime> _ahora;
        private bool _tiendaLeida;
        private bool _correoLeido;

        public FacturacionTiendaViewModel(IServicioFacturacionTienda servicio, IConfiguracion configuracion, IServicioDialogos dialogos,
            IGuardadorFicheros guardador)
            : this(servicio, configuracion, dialogos, null, guardador)
        {
        }

        /// <param name="ahora">El reloj (los tests lo fijan); por defecto, DateTime.Now.</param>
        internal FacturacionTiendaViewModel(IServicioFacturacionTienda servicio, IConfiguracion configuracion, IServicioDialogos dialogos,
            Func<DateTime> ahora, IGuardadorFicheros guardador = null)
        {
            _servicio = servicio;
            _configuracion = configuracion;
            _dialogos = dialogos;
            _guardador = guardador;
            _ahora = ahora ?? (() => DateTime.Now);
            PuedeElegirTienda = configuracion != null
                && (configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)
                    || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)
                    || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA));
            Tiendas = PuedeElegirTienda
                ? new List<OpcionTiendaFacturacion>
                {
                    new OpcionTiendaFacturacion(Constantes.Almacenes.ALMACEN_REINA, "Reina (REI)"),
                    new OpcionTiendaFacturacion(Constantes.Almacenes.ALMACEN_ALCOBENDAS, "Alcobendas (ALC)")
                }
                : new List<OpcionTiendaFacturacion> { new OpcionTiendaFacturacion(null, OPCION_SU_TIENDA) };
            _tienda = Tiendas[0];
            Tipos = new List<OpcionTipoFacturacion>
            {
                new OpcionTipoFacturacion(TipoInformeFacturacionTienda.Semana, "Semana (facturas, por correo)"),
                new OpcionTipoFacturacion(TipoInformeFacturacionTienda.Acumulado, "Acumulado del año"),
                new OpcionTipoFacturacion(TipoInformeFacturacionTienda.Comparativo, "Comparativo con años anteriores")
            };
            _tipo = Tipos[0];
            AniosPosibles = Enumerable.Range(1, ANIOS_MAXIMO).ToList();
            MandarCommand = new AsyncRelayCommand(MandarAsync, PuedeMandar);
            SemanaAnteriorCommand = new RelayCommand(PonerSemanaAnterior);
            VerCommand = new AsyncRelayCommand(VerAsync, PuedeVer);
            GuardarExcelCommand = new AsyncRelayCommand(GuardarExcelAsync, PuedeGuardarExcel);
            PonerSemanaAnterior();
        }

        public string Titulo => "Facturado en la tienda";
        public string Ayuda => "Semana: todo lo facturado con el almacén de la tienda en esas fechas (cursos incluidos), factura a factura y por " +
            "cliente, con su dirección, su comercial, la forma de venta y el importe; se lo mandamos en Excel a su correo. Acumulado del año " +
            "y comparativo: la base imponible de enero hasta la fecha que diga, mes a mes, y en el comparativo el mismo periodo de los años " +
            "anteriores con la diferencia en euros y en porcentaje.";

        /// <summary>Dirección, Administración e Informática eligen la tienda; los demás, la suya.</summary>
        public bool PuedeElegirTienda { get; }
        public IReadOnlyList<OpcionTiendaFacturacion> Tiendas { get; }

        private OpcionTiendaFacturacion _tienda;
        public OpcionTiendaFacturacion Tienda
        {
            get => _tienda;
            set
            {
                if (SetProperty(ref _tienda, value))
                {
                    LimpiarResultado();
                }
            }
        }

        /// <summary>Nesto#559: semana, acumulado del año o comparativo.</summary>
        public IReadOnlyList<OpcionTipoFacturacion> Tipos { get; }

        private OpcionTipoFacturacion _tipo;
        public OpcionTipoFacturacion Tipo
        {
            get => _tipo;
            set
            {
                if (value == null || !SetProperty(ref _tipo, value))
                {
                    return;
                }
                // La semana empieza en la semana anterior; el acumulado y el comparativo, hasta hoy
                if (EsSemana)
                {
                    PonerSemanaAnterior();
                }
                else
                {
                    Hasta = _ahora().Date;
                }
                LimpiarResultado();
                Mensaje = null;
                OnPropertyChanged(nameof(EsSemana));
                OnPropertyChanged(nameof(EsAnual));
                OnPropertyChanged(nameof(EsComparativo));
                NotificarComandos();
            }
        }

        public TipoInformeFacturacionTienda TipoInforme => Tipo?.Tipo ?? TipoInformeFacturacionTienda.Semana;
        public bool EsSemana => TipoInforme == TipoInformeFacturacionTienda.Semana;
        /// <summary>Acumulado o comparativo: sin «desde» (empieza el 1 de enero) y con la tabla en pantalla.</summary>
        public bool EsAnual => !EsSemana;
        public bool EsComparativo => TipoInforme == TipoInformeFacturacionTienda.Comparativo;

        public IReadOnlyList<int> AniosPosibles { get; }

        private int _anios = ANIOS_POR_DEFECTO;
        /// <summary>Cuántos años anteriores se comparan (de 1 a 10).</summary>
        public int Anios
        {
            get => _anios;
            set
            {
                if (SetProperty(ref _anios, value))
                {
                    LimpiarResultado();
                    NotificarComandos();
                }
            }
        }

        private DateTime _desde;
        public DateTime Desde
        {
            get => _desde;
            set
            {
                if (SetProperty(ref _desde, value.Date))
                {
                    NotificarComandos();
                }
            }
        }

        private DateTime _hasta;
        public DateTime Hasta
        {
            get => _hasta;
            set
            {
                if (SetProperty(ref _hasta, value.Date))
                {
                    LimpiarResultado();
                    NotificarComandos();
                }
            }
        }

        private string _correo;
        /// <summary>A qué correo se manda: empieza con el del usuario (su CorreoDefecto) y se puede cambiar. Vacío: el suyo.</summary>
        public string Correo
        {
            get => _correo;
            set => SetProperty(ref _correo, value);
        }

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

        private FacturacionTiendaAnual _resultado;
        /// <summary>Lo último que ha devuelto la API para el acumulado o el comparativo (null si no se ha pedido).</summary>
        public FacturacionTiendaAnual Resultado { get => _resultado; private set => SetProperty(ref _resultado, value); }

        private IReadOnlyList<FilaFacturacionTiendaAnual> _filas = Array.Empty<FilaFacturacionTiendaAnual>();
        /// <summary>Los meses de enero al de «hasta» y, al final, el total.</summary>
        public IReadOnlyList<FilaFacturacionTiendaAnual> Filas
        {
            get => _filas;
            private set
            {
                if (SetProperty(ref _filas, value))
                {
                    OnPropertyChanged(nameof(HayResultado));
                }
            }
        }

        public bool HayResultado => Filas.Count > 0;

        private IReadOnlyList<int> _aniosAnteriores = Array.Empty<int>();
        /// <summary>Los años del comparativo que se ven en la tabla, del más reciente al más antiguo (la vista pone sus columnas).</summary>
        public IReadOnlyList<int> AniosAnteriores { get => _aniosAnteriores; private set => SetProperty(ref _aniosAnteriores, value); }

        private string _resumen;
        /// <summary>Encima de la tabla: tienda, periodo y total.</summary>
        public string Resumen { get => _resumen; private set => SetProperty(ref _resumen, value); }

        public IAsyncRelayCommand MandarCommand { get; }
        public IRelayCommand SemanaAnteriorCommand { get; }
        /// <summary>Nesto#559: pide el acumulado o el comparativo y lo enseña en la tabla.</summary>
        public IAsyncRelayCommand VerCommand { get; }
        /// <summary>Nesto#559: descarga el Excel de lo elegido (cualquier tipo) y lo guarda donde diga el usuario.</summary>
        public IAsyncRelayCommand GuardarExcelCommand { get; }

        /// <summary>La última semana cerrada, de lunes a domingo, anterior a <paramref name="hoy"/> (un lunes da la semana pasada).</summary>
        public static (DateTime Desde, DateTime Hasta) SemanaAnterior(DateTime hoy)
        {
            int diasDesdeLunes = ((int)hoy.DayOfWeek + 6) % 7;
            DateTime lunesDeEstaSemana = hoy.Date.AddDays(-diasDesdeLunes);
            return (lunesDeEstaSemana.AddDays(-7), lunesDeEstaSemana.AddDays(-1));
        }

        private void PonerSemanaAnterior()
        {
            (DateTime desde, DateTime hasta) = SemanaAnterior(_ahora());
            Desde = desde;
            Hasta = hasta;
        }

        private void NotificarComandos()
        {
            MandarCommand?.NotifyCanExecuteChanged();
            VerCommand?.NotifyCanExecuteChanged();
            GuardarExcelCommand?.NotifyCanExecuteChanged();
        }

        private void LimpiarResultado()
        {
            Resultado = null;
            Filas = Array.Empty<FilaFacturacionTiendaAnual>();
            AniosAnteriores = Array.Empty<int>();
            Resumen = null;
        }

        public void AlLlegar(ParametrosNavegacion parametros)
        {
            _ = LeerTiendaDelUsuarioAsync();
            _ = LeerCorreoDelUsuarioAsync();
        }

        /// <summary>Quien elige tienda empieza con la suya (su AlmacénPedidoVta) si es una de las tiendas.</summary>
        internal async Task LeerTiendaDelUsuarioAsync()
        {
            if (_tiendaLeida || !PuedeElegirTienda || _configuracion == null)
            {
                return;
            }
            _tiendaLeida = true;
            try
            {
                string almacen = (await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenPedidoVta)
                    .ConfigureAwait(true))?.Trim();
                OpcionTiendaFacturacion suya = Tiendas.FirstOrDefault(t => string.Equals(t.Almacen, almacen, StringComparison.OrdinalIgnoreCase));
                if (suya != null)
                {
                    Tienda = suya;
                }
            }
            catch (Exception)
            {
                // Es solo la tienda que sale elegida: se queda la primera
            }
        }

        /// <summary>El correo al que va el Excel empieza siendo el del usuario (lo dice la API: su CorreoDefecto).</summary>
        internal async Task LeerCorreoDelUsuarioAsync()
        {
            if (_correoLeido || _servicio == null)
            {
                return;
            }
            _correoLeido = true;
            try
            {
                string correo = await _servicio.LeerCorreoUsuario().ConfigureAwait(true);
                if (string.IsNullOrWhiteSpace(Correo))
                {
                    Correo = correo;
                }
                if (string.IsNullOrWhiteSpace(Correo) && EsSemana)
                {
                    Mensaje = MENSAJE_SIN_CORREO;
                }
            }
            catch (Exception)
            {
                // Es solo el correo que sale escrito: si se deja vacío, la API usa el suyo (o dice que no lo tiene)
            }
        }

        private bool AniosValidos => Anios >= 1 && Anios <= ANIOS_MAXIMO;

        private bool PuedeMandar() => !EstaOcupado && EsSemana && Desde <= Hasta;

        private bool PuedeVer() => !EstaOcupado && EsAnual && (!EsComparativo || AniosValidos);

        private bool PuedeGuardarExcel() => !EstaOcupado && (EsSemana ? Desde <= Hasta : (!EsComparativo || AniosValidos));

        internal async Task MandarAsync()
        {
            if (!PuedeMandar())
            {
                return;
            }
            Mensaje = null;
            EstaOcupado = true;
            try
            {
                string correo = string.IsNullOrWhiteSpace(Correo) ? null : Correo.Trim();
                ResultadoCorreoFacturacionTienda resultado = await _servicio
                    .MandarExcelPorCorreo(Tienda?.Almacen, Desde, Hasta, correo)
                    .ConfigureAwait(true);
                string destinatario = string.IsNullOrWhiteSpace(resultado?.Correo) ? correo : resultado.Correo.Trim();
                if (!string.IsNullOrWhiteSpace(destinatario))
                {
                    Correo = destinatario;
                }
                Mensaje = $"Se lo hemos mandado a {destinatario}: del {Desde:dd/MM/yyyy} al {Hasta:dd/MM/yyyy}" +
                    (resultado == null ? "." : $", {TextoFacturas(resultado.NumeroFacturas)} por {resultado.Total:N2} € (IVA incluido).");
                _dialogos?.ShowNotification("Facturado en la tienda", Mensaje);
            }
            catch (FacturacionTiendaException ex)
            {
                Mensaje = ex.Message;
                _dialogos?.ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                Mensaje = "No se ha podido mandar el Excel: " + ex.Message;
                _dialogos?.ShowError(Mensaje);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        /// <summary>Nesto#559: el acumulado o el comparativo, en la tabla (los meses y el total al final).</summary>
        internal async Task VerAsync()
        {
            if (!PuedeVer())
            {
                return;
            }
            Mensaje = null;
            LimpiarResultado();
            EstaOcupado = true;
            try
            {
                FacturacionTiendaAnual informe = await _servicio
                    .LeerAnual(Tienda?.Almacen, TipoInforme, Hasta, Anios)
                    .ConfigureAwait(true);
                Mostrar(informe);
            }
            catch (FacturacionTiendaException ex)
            {
                Mensaje = ex.Message;
                _dialogos?.ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                Mensaje = "No se ha podido leer el informe: " + ex.Message;
                _dialogos?.ShowError(Mensaje);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        private void Mostrar(FacturacionTiendaAnual informe)
        {
            informe ??= new FacturacionTiendaAnual();
            List<int> anteriores = informe.AniosAnteriores ?? new List<int>();
            var filas = (informe.Meses ?? new List<MesFacturacionTienda>())
                .Select(m => Fila(m, NombreMes(m, informe.Hasta), false, anteriores))
                .ToList();
            if (informe.Total != null)
            {
                filas.Add(Fila(informe.Total, "Total", true, anteriores));
            }
            Resultado = informe;
            AniosAnteriores = anteriores;
            Filas = filas;
            decimal total = informe.Total?.BaseImponible ?? filas.Sum(f => f.BaseImponible);
            string tienda = string.IsNullOrWhiteSpace(informe.Tienda) ? Tienda?.Texto : informe.Tienda.Trim();
            Resumen = $"{tienda}: del {informe.Desde:dd/MM/yyyy} al {informe.Hasta:dd/MM/yyyy}, {total:N2} € de base imponible" +
                (anteriores.Count == 0 ? "." : $", comparado con {string.Join(", ", anteriores)} (mismos días de cada año).");
        }

        private static FilaFacturacionTiendaAnual Fila(MesFacturacionTienda mes, string nombre, bool esTotal, List<int> anteriores)
        {
            var porAnio = (mes.Anteriores ?? new List<ComparativoMesFacturacionTienda>()).ToDictionary(a => a.Anio);
            return new FilaFacturacionTiendaAnual
            {
                Mes = nombre,
                EsTotal = esTotal,
                NumeroFacturas = mes.NumeroFacturas,
                BaseImponible = mes.BaseImponible,
                BaseImponibleCursos = mes.BaseImponibleCursos,
                Acumulado = mes.Acumulado,
                // Una columna por cada año del informe, en su orden (si un mes no trae un año, va vacío)
                Anteriores = anteriores.Select(anio => porAnio.TryGetValue(anio, out ComparativoMesFacturacionTienda a)
                    ? new AnteriorFilaFacturacion
                    {
                        Anio = a.Anio,
                        NumeroFacturas = a.NumeroFacturas,
                        BaseImponible = a.BaseImponible,
                        VariacionImporte = a.VariacionImporte,
                        VariacionPorcentaje = a.VariacionPorcentaje
                    }
                    : new AnteriorFilaFacturacion { Anio = anio }).ToList()
            };
        }

        /// <summary>Como el Excel de la API: el mes a medias sale «Noviembre (hasta el 12)».</summary>
        internal static string NombreMes(MesFacturacionTienda mes, DateTime hasta)
        {
            string nombre = string.IsNullOrWhiteSpace(mes.NombreMes)
                ? CultureInfo.GetCultureInfo("es-ES").DateTimeFormat.GetMonthName(Math.Max(1, Math.Min(12, mes.Mes)))
                : mes.NombreMes.Trim();
            bool parcial = mes.Mes != 0 && mes.Hasta.Date == hasta.Date && mes.Hasta.AddDays(1).Month == mes.Hasta.Month;
            return parcial ? $"{nombre} (hasta el {mes.Hasta.Day})" : nombre;
        }

        /// <summary>
        /// Nesto#559: el Excel de lo elegido (semana, acumulado o comparativo) se guarda donde diga el usuario. No se abre: en el
        /// escritorio remoto Office no tiene licencia.
        /// </summary>
        internal async Task GuardarExcelAsync()
        {
            if (!PuedeGuardarExcel() || _guardador == null)
            {
                return;
            }
            Mensaje = null;
            EstaOcupado = true;
            try
            {
                ExcelFacturacionTienda excel = await _servicio
                    .DescargarExcel(Tienda?.Almacen, TipoInforme, Desde, Hasta, Anios)
                    .ConfigureAwait(true);
                string nombre = string.IsNullOrWhiteSpace(excel?.NombreFichero) ? NombrePropuesto() : excel.NombreFichero;
                string ruta = _guardador.ElegirDondeGuardar("Guardar el Excel de lo facturado en la tienda", nombre, FILTRO_EXCEL, ".xlsx");
                if (string.IsNullOrWhiteSpace(ruta))
                {
                    return;
                }
                _guardador.Guardar(ruta, excel?.Contenido);
                Mensaje = $"Excel guardado en {ruta}. Ábralo en su ordenador (aquí no hay Excel).";
            }
            catch (FacturacionTiendaException ex)
            {
                Mensaje = ex.Message;
                _dialogos?.ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                Mensaje = "No se ha podido guardar el Excel: " + ex.Message;
                _dialogos?.ShowError(Mensaje);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        /// <summary>Como el de la API (FacturacionTienda_{Tienda}_{Tipo}_{yyyyMMdd}.xlsx), por si no lo manda.</summary>
        internal string NombrePropuesto()
        {
            string tienda = string.IsNullOrWhiteSpace(Tienda?.Almacen) ? "Tienda" : Tienda.Almacen.Trim();
            return $"FacturacionTienda_{tienda}_{TipoInforme}_{Hasta:yyyyMMdd}.xlsx";
        }

        private static string TextoFacturas(int numero) => numero == 1 ? "1 factura" : $"{numero} facturas";
    }
}
