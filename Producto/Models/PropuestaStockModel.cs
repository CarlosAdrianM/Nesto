using System;
using System.Collections.Generic;

namespace Nesto.Modules.Producto.Models
{
    /// <summary>
    /// Nesto#529 (gemela de NestoAPI#631): los valores fijos de las propuestas de stock, en un solo sitio. Los motivos de
    /// silencio los manda la API (GET api/PropuestasStock/MotivosSilencio); <see cref="MOTIVOS_SILENCIO_RESERVA"/> solo se
    /// usan si la API todavía no los da.
    /// </summary>
    public static class PropuestasStockConstantes
    {
        public const string DECISION_APLICAR = "Aplicar";
        public const string DECISION_RECHAZAR = "Rechazar";
        public const string DECISION_POSPONER = "Posponer";
        public const string DECISION_SILENCIAR = "Silenciar";
        /// <summary>Nesto#535 (NestoAPI#655): darle un plazo para vender más, con objetivo (uds/mes) y fecha de revisión.</summary>
        public const string DECISION_POTENCIAR = "Potenciar";

        public const string ESTADO_PENDIENTE = "Pendiente";
        public const string ESTADO_APLICADA = "Aplicada";
        public const string ESTADO_RECHAZADA = "Rechazada";
        public const string ESTADO_POSPUESTA = "Pospuesta";
        public const string ESTADO_SILENCIADA = "Silenciada";
        public const string ESTADO_SUPERADA = "Superada";
        public const string ESTADO_SIN_ACCION = "SinAccion";
        /// <summary>NestoAPI#655: con una potenciación de ventas vigente (objetivo y fecha de revisión).</summary>
        public const string ESTADO_EN_POTENCIACION = "EnPotenciacion";

        public static readonly IReadOnlyList<string> ESTADOS = new[]
        {
            ESTADO_PENDIENTE, ESTADO_APLICADA, ESTADO_RECHAZADA, ESTADO_POSPUESTA, ESTADO_SILENCIADA, ESTADO_SUPERADA, ESTADO_SIN_ACCION,
            ESTADO_EN_POTENCIACION
        };

        public const string ACCION_AJUSTAR = "Ajustar máx/mín";
        public const string ACCION_LIQUIDAR = "Liquidar";
        public const string ACCION_MANTENER = "Mantener";
        public const string ACCION_NADA = "Nada";

        public const string CANAL_OUTLET = "Outlet";
        public const string CANAL_GANAVISIONES = "Ganavisiones";
        public static readonly IReadOnlyList<string> CANALES_LIQUIDACION = new[] { CANAL_OUTLET, CANAL_GANAVISIONES };

        /// <summary>Por si la API aún no tiene GET api/PropuestasStock/MotivosSilencio (o falla): los tres de la issue.</summary>
        public static readonly IReadOnlyList<string> MOTIVOS_SILENCIO_RESERVA = new[] { "Nuevo", "Estacional", "Aparato" };

        /// <summary>Un silencio no puede durar más (lo comprueba también la API).</summary>
        public const int MESES_MAXIMOS_SILENCIO = 24;
        /// <summary>La fecha de caducidad que se propone al silenciar.</summary>
        public const int MESES_SILENCIO_POR_DEFECTO = 3;
        /// <summary>Nesto#535: la fecha de revisión que se propone al potenciar (la misma que pone la API si no se dice).</summary>
        public const int MESES_POTENCIACION_POR_DEFECTO = 6;
        /// <summary>Nesto#535: una potenciación no puede durar más (lo comprueba también la API).</summary>
        public const int MESES_MAXIMOS_POTENCIACION = 24;

        /// <summary>
        /// Nesto#535: lo que se enseña si la API publicada aún no tiene la decisión «Potenciar» (anterior a NestoAPI#655): contesta
        /// 400 con la lista de decisiones sin «Potenciar».
        /// </summary>
        public const string MENSAJE_API_SIN_POTENCIAR = "El servidor todavía no admite «Potenciar ventas»: hay que publicar la versión nueva de NestoAPI. " +
            "Mientras tanto puede aplicar, rechazar, posponer o silenciar la propuesta.";

        /// <summary>El nombre que se ve de cada estado («SinAccion» → «Sin acción»).</summary>
        public static string NombreEstado(string estado)
            => estado == ESTADO_SIN_ACCION ? "Sin acción"
            : estado == ESTADO_EN_POTENCIACION ? "En potenciación"
            : estado;
        /// <summary>Cuántas filas se piden a la API de una vez.</summary>
        public const int TOP = 2000;
    }

    /// <summary>Una fila de GET api/PropuestasStock (PropuestaStockConDecision de la API).</summary>
    public class PropuestaStockModel
    {
        public int Id { get; set; }
        public DateTime Mes { get; set; }
        public DateTime FechaCalculo { get; set; }
        public string Producto { get; set; }
        public string Nombre { get; set; }
        public string Familia { get; set; }
        public string Grupo { get; set; }
        public string Proveedor { get; set; }
        public short? EstadoProducto { get; set; }
        public string Almacen { get; set; }
        public string Fase { get; set; }
        public int Stock { get; set; }
        public decimal PrecioMedio { get; set; }
        public int Ventas12Meses { get; set; }
        public decimal VentaMensualReferencia { get; set; }
        public decimal? CoberturaMeses { get; set; }
        public string Tendencia { get; set; }
        public int PedidoCompraAbierto { get; set; }
        public int ExcesoUnidades { get; set; }
        public decimal ExcesoValorado { get; set; }
        public string Accion { get; set; }
        public int? StockMaximoActual { get; set; }
        public int? StockMinimoActual { get; set; }
        public int? StockMaximoPropuesto { get; set; }
        public int? StockMinimoPropuesto { get; set; }
        /// <summary>La trazabilidad de cada máximo y mínimo (de dónde sale cada número).</summary>
        public string Explicacion { get; set; }
        /// <summary>
        /// Sugerencia 644: qué pasa exactamente al aplicar la propuesta («El máximo de ALG pasa de 59 a 88 y el mínimo de 39 a
        /// 68»). La calcula la API (un solo sitio para todos los clientes); con una API anterior llega vacía.
        /// </summary>
        public string Consecuencia { get; set; }

        public string Estado { get; set; }
        public string Decision { get; set; }
        public string DecisionUsuario { get; set; }
        public DateTime? DecisionFecha { get; set; }
        public string DecisionMotivo { get; set; }
        public string MotivoSilencio { get; set; }
        public DateTime? SilenciadaHasta { get; set; }
        public string CanalLiquidacion { get; set; }
        public int? StockMaximoAnterior { get; set; }
        public int? StockMinimoAnterior { get; set; }
        public int? StockMaximoAplicado { get; set; }
        public int? StockMinimoAplicado { get; set; }
        public DateTime? SilencioHasta { get; set; }
        public string SilencioMotivo { get; set; }
        public DateTime? MesMasReciente { get; set; }

        // Nesto#535 (NestoAPI#655): potenciar ventas. Con una API anterior (o sin su script) llegan vacíos.
        public decimal VentaMedia6Meses { get; set; }
        /// <summary>Lo decidido en esta fila (solo si su decisión es «Potenciar»).</summary>
        public int? ObjetivoVentaMensual { get; set; }
        public DateTime? PotenciarHasta { get; set; }
        /// <summary>La última potenciación del producto y almacén (de esta fila o de una anterior).</summary>
        public int? PotenciacionPropuestaId { get; set; }
        public int? PotenciacionObjetivo { get; set; }
        public DateTime? PotenciacionHasta { get; set; }
        public bool PotenciacionVigente { get; set; }
        /// <summary>Cómo va la potenciación (venta media frente al objetivo), como lo escribe la API.</summary>
        public string NotaPotenciacion { get; set; }
        /// <summary>El objetivo que propone la API al potenciar (venta media × 1,5); null si no lo trae.</summary>
        public int? ObjetivoVentaSugerido { get; set; }

        public string NombreEstado => PropuestasStockConstantes.NombreEstado(Estado);
        public bool EstaEnPotenciacion => Estado == PropuestasStockConstantes.ESTADO_EN_POTENCIACION;

        /// <summary>Para la columna: «40 uds/mes hasta 15/04/2027» si está en potenciación; vacío si no.</summary>
        public string ObjetivoYFechaPotenciacion
        {
            get
            {
                int? objetivo = PotenciacionObjetivo ?? ObjetivoVentaMensual;
                DateTime? hasta = PotenciacionHasta ?? PotenciarHasta;
                if (!EstaEnPotenciacion || !objetivo.HasValue)
                {
                    return null;
                }
                return hasta.HasValue ? $"{objetivo} uds/mes hasta {hasta:dd/MM/yyyy}" : $"{objetivo} uds/mes";
            }
        }

        /// <summary>Para el panel: la nota de la API; si no la trae, el objetivo y la fecha.</summary>
        public string TextoPotenciacion
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(NotaPotenciacion))
                {
                    return NotaPotenciacion.Trim();
                }
                string objetivoYFecha = ObjetivoYFechaPotenciacion;
                return objetivoYFecha == null ? null : "En potenciación: objetivo " + objetivoYFecha + ".";
            }
        }

        public string ProductoYNombre => $"{Producto?.Trim()} {Nombre?.Trim()}".Trim();
        public string MaxMinActual => Pareja(StockMaximoActual, StockMinimoActual);
        public string MaxMinPropuesto => Pareja(StockMaximoPropuesto, StockMinimoPropuesto);
        public bool EsAjuste => Accion == PropuestasStockConstantes.ACCION_AJUSTAR;
        public bool EsLiquidar => Accion == PropuestasStockConstantes.ACCION_LIQUIDAR;

        /// <summary>
        /// Lo que dice la API que no se puede decidir (ya aplicada, superada por otra más reciente o sin nada que hacer). El
        /// resto lo decide la API.
        /// </summary>
        public bool SePuedeDecidir => Estado != PropuestasStockConstantes.ESTADO_APLICADA
            && Estado != PropuestasStockConstantes.ESTADO_SUPERADA
            && Estado != PropuestasStockConstantes.ESTADO_SIN_ACCION
            && Accion != PropuestasStockConstantes.ACCION_MANTENER
            && Accion != PropuestasStockConstantes.ACCION_NADA;

        /// <summary>La decisión tomada, legible (para el panel de detalle).</summary>
        public string TextoDecision
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Decision))
                {
                    return SilencioHasta.HasValue
                        ? $"Silenciado hasta el {SilencioHasta:dd/MM/yyyy} ({SilencioMotivo?.Trim()})."
                        : null;
                }
                string texto = $"{Decision.Trim()} por {DecisionUsuario?.Trim()} el {DecisionFecha:dd/MM/yyyy HH:mm}";
                if (!string.IsNullOrWhiteSpace(DecisionMotivo))
                {
                    texto += $": {DecisionMotivo.Trim()}";
                }
                if (SilenciadaHasta.HasValue)
                {
                    texto += $" (silenciado hasta el {SilenciadaHasta:dd/MM/yyyy}, {MotivoSilencio?.Trim()})";
                }
                if (!string.IsNullOrWhiteSpace(CanalLiquidacion))
                {
                    texto += $" (liquidar en {CanalLiquidacion.Trim()})";
                }
                if (ObjetivoVentaMensual.HasValue)
                {
                    texto += PotenciarHasta.HasValue
                        ? $" (objetivo {ObjetivoVentaMensual} uds/mes, revisión el {PotenciarHasta:dd/MM/yyyy})"
                        : $" (objetivo {ObjetivoVentaMensual} uds/mes)";
                }
                if (StockMaximoAplicado.HasValue || StockMinimoAplicado.HasValue)
                {
                    texto += $". Máximo/mínimo {Pareja(StockMaximoAnterior, StockMinimoAnterior)} → {Pareja(StockMaximoAplicado, StockMinimoAplicado)}";
                }
                return texto + ".";
            }
        }

        private static string Pareja(int? maximo, int? minimo)
            => maximo.HasValue || minimo.HasValue ? $"{maximo?.ToString() ?? "-"}/{minimo?.ToString() ?? "-"}" : "-";
    }

    /// <summary>
    /// Sugerencia 644: los filtros de la pantalla que van a la API (GET api/PropuestasStock y GET api/PropuestasStock/Resumen),
    /// además del mes y el estado. Vacíos, sin filtro.
    /// </summary>
    public class FiltroPropuestasStock
    {
        public string Familia { get; set; }
        public string Grupo { get; set; }
        public string Accion { get; set; }
        /// <summary>Parte del código o del nombre del producto.</summary>
        public string Texto { get; set; }

        public bool EstaVacio => string.IsNullOrWhiteSpace(Familia) && string.IsNullOrWhiteSpace(Grupo)
            && string.IsNullOrWhiteSpace(Accion) && string.IsNullOrWhiteSpace(Texto);

        /// <summary>Los parámetros de la URL («familia=Lisap», …), escapados; solo los que tienen valor.</summary>
        public List<string> ParametrosUrl()
        {
            var parametros = new List<string>();
            Anadir(parametros, "familia", Familia);
            Anadir(parametros, "grupo", Grupo);
            Anadir(parametros, "accion", Accion);
            Anadir(parametros, "texto", Texto);
            return parametros;
        }

        /// <summary>Para la cabecera: «familia Lisap · acción Liquidar · "champú"»; null sin filtros.</summary>
        public string Descripcion()
        {
            var partes = new List<string>();
            if (!string.IsNullOrWhiteSpace(Familia)) partes.Add("familia " + Familia.Trim());
            if (!string.IsNullOrWhiteSpace(Grupo)) partes.Add("grupo " + Grupo.Trim());
            if (!string.IsNullOrWhiteSpace(Accion)) partes.Add("acción " + Accion.Trim());
            if (!string.IsNullOrWhiteSpace(Texto)) partes.Add($"«{Texto.Trim()}»");
            return partes.Count == 0 ? null : string.Join(" · ", partes);
        }

        private static void Anadir(List<string> parametros, string nombre, string valor)
        {
            if (!string.IsNullOrWhiteSpace(valor))
            {
                parametros.Add(nombre + "=" + Uri.EscapeDataString(valor.Trim()));
            }
        }
    }

    /// <summary>Sugerencia 644: GET api/PropuestasStock/Filtros: lo que hay en las propuestas del mes para los desplegables.</summary>
    public class OpcionesFiltroPropuestasModel
    {
        public List<string> Familias { get; set; } = new List<string>();
        public List<string> Grupos { get; set; } = new List<string>();
        public List<string> Acciones { get; set; } = new List<string>();
    }

    /// <summary>Una fila de GET api/PropuestasStock/Resumen.</summary>
    public class ResumenEstadoPropuestasModel
    {
        public DateTime Mes { get; set; }
        public string Estado { get; set; }
        public int Propuestas { get; set; }
        public decimal ExcesoValorado { get; set; }
    }

    /// <summary>Cuerpo de POST api/PropuestasStock/{id}/Decision.</summary>
    public class DecisionPropuestaStockPeticion
    {
        public string Decision { get; set; }
        public string Motivo { get; set; }
        public int? StockMaximo { get; set; }
        public int? StockMinimo { get; set; }
        public string MotivoSilencio { get; set; }
        public DateTime? SilenciarHasta { get; set; }
        public string CanalLiquidacion { get; set; }
        /// <summary>Nesto#535: solo al potenciar (obligatorio, más de 0): unidades al mes.</summary>
        public int? ObjetivoVentaMensual { get; set; }
        /// <summary>Nesto#535: solo al potenciar: la fecha de revisión (incluida).</summary>
        public DateTime? PotenciarHasta { get; set; }
    }

    /// <summary>Respuesta de POST api/PropuestasStock/{id}/Decision.</summary>
    public class ResultadoDecisionPropuestaStock
    {
        public int Id { get; set; }
        public string Producto { get; set; }
        public string Decision { get; set; }
        public string Estado { get; set; }
        public List<string> Cambios { get; set; } = new List<string>();
        public string Mensaje { get; set; }
    }

    /// <summary>Un «no» de la API (400, 403, 404, 409…) con su <c>Message</c>, para enseñarlo tal cual.</summary>
    public class PropuestasStockException : Exception
    {
        public PropuestasStockException(string mensaje, int codigo) : base(mensaje)
        {
            Codigo = codigo;
        }

        public int Codigo { get; }
        public bool SinPermiso => Codigo == 403;
    }
}
