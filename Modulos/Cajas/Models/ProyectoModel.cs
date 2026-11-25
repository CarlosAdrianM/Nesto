using System;
using System.Collections.Generic;

namespace Nesto.Modulos.Cajas.Models
{
    /// <summary>
    /// Proyectos (gemela de NestoAPI#901, Carlos 25/11/26): un proyecto (convocatoria de un curso del SEPE, feria, evento, campaña)
    /// tal como lo da GET api/Proyectos. Los totales son de solo lectura (los calcula la API con los abonos restados).
    /// </summary>
    public class ProyectoModel
    {
        public int Id { get; set; }
        public string? Empresa { get; set; }
        public string? Codigo { get; set; }
        public string? Nombre { get; set; }
        /// <summary>CursoSEPE, Evento, Feria, Campana u Otro.</summary>
        public string? Tipo { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        /// <summary>Abierto o Cerrado (solo lectura: se cambia con Cerrar y Reabrir).</summary>
        public string? Estado { get; set; }
        public string? Producto { get; set; }
        public int? EventoId { get; set; }
        public decimal? PresupuestoIngresos { get; set; }
        public decimal? PresupuestoGastos { get; set; }
        public string? Comentarios { get; set; }
        public string? Usuario { get; set; }
        public DateTime? FechaAlta { get; set; }
        public string? UsuarioModificacion { get; set; }
        public DateTime? FechaModificacion { get; set; }
        public decimal TotalIngresos { get; set; }
        public decimal TotalGastos { get; set; }
        public decimal Resultado { get; set; }
        public int NumeroImputaciones { get; set; }

        public bool EstaCerrado => string.Equals(Estado?.Trim(), ReglasPantallaProyectos.ESTADO_CERRADO, StringComparison.OrdinalIgnoreCase);

        public string TipoParaMostrar => ReglasPantallaProyectos.NombreTipo(Tipo);

        /// <summary>«IMPE0210-2026-1 · Curso de depilación» para los desplegables.</summary>
        public string CodigoYNombre => string.IsNullOrWhiteSpace(Nombre)
            ? (Codigo?.Trim() ?? string.Empty)
            : $"{Codigo?.Trim()} · {Nombre!.Trim()}";
    }

    /// <summary>Una parte de un apunte imputada a un proyecto (GET api/Proyectos/ApuntesPorImputar y …/Imputaciones).</summary>
    public class ImputacionApunteModel
    {
        public int Id { get; set; }
        public int ProyectoId { get; set; }
        public string? CodigoProyecto { get; set; }
        public string? NombreProyecto { get; set; }
        public string? EstadoProyecto { get; set; }
        /// <summary>Siempre positivo.</summary>
        public decimal Importe { get; set; }
        public decimal Porcentaje { get; set; }
        public string? Comentario { get; set; }
        public string? Usuario { get; set; }
        public DateTime? Fecha { get; set; }
    }

    /// <summary>Un apunte de Contabilidad (cuentas 6 y 7) con lo imputado y lo pendiente.</summary>
    public class ApunteProyectoModel
    {
        public string? Empresa { get; set; }
        public int NumeroOrden { get; set; }
        public DateTime Fecha { get; set; }
        public string? Cuenta { get; set; }
        public string? NombreCuenta { get; set; }
        public string? Concepto { get; set; }
        public string? Documento { get; set; }
        public int? Asiento { get; set; }
        public string? Diario { get; set; }
        public decimal Debe { get; set; }
        public decimal Haber { get; set; }
        /// <summary>|Debe − Haber|: lo que se puede repartir y lo que se manda como ImporteApunte en el PUT.</summary>
        public decimal ImporteApunte { get; set; }
        /// <summary>«Gasto» o «Ingreso».</summary>
        public string? Naturaleza { get; set; }
        public bool EsAbono { get; set; }
        public string? Tercero { get; set; }
        public string? TipoTercero { get; set; }
        public string? NombreTercero { get; set; }
        public decimal Imputado { get; set; }
        public decimal Pendiente { get; set; }
        public decimal PorcentajeImputado { get; set; }
        public List<ImputacionApunteModel> Imputaciones { get; set; } = new List<ImputacionApunteModel>();

        public string CuentaYNombre => string.IsNullOrWhiteSpace(NombreCuenta)
            ? (Cuenta?.Trim() ?? string.Empty)
            : $"{Cuenta?.Trim()} {NombreCuenta!.Trim()}";

        public string TerceroParaMostrar => string.IsNullOrWhiteSpace(NombreTercero)
            ? (Tercero?.Trim() ?? string.Empty)
            : $"{Tercero?.Trim()} {NombreTercero!.Trim()}";

        /// <summary>«Gasto», «Ingreso» y, si es abono, «(abono: resta)».</summary>
        public string NaturalezaParaMostrar => EsAbono
            ? $"{Naturaleza?.Trim()} (abono: resta)"
            : Naturaleza?.Trim() ?? string.Empty;

        /// <summary>«IMPE0210-2026-1 (60,00 €), FERIA-2026 (40,00 €)».</summary>
        public string ProyectosParaMostrar => ReglasPantallaProyectos.TextoImputaciones(Imputaciones);
    }

    /// <summary>GET api/Proyectos/ApuntesPorImputar.</summary>
    public class ApuntesPorImputarModel
    {
        public string? Empresa { get; set; }
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
        public int Pagina { get; set; }
        public int Tope { get; set; }
        public bool HayMas { get; set; }
        public List<ApunteProyectoModel> Apuntes { get; set; } = new List<ApunteProyectoModel>();
    }

    /// <summary>Los filtros de «Apuntes por imputar» (los parámetros de GET api/Proyectos/ApuntesPorImputar).</summary>
    public class FiltroApuntesProyectos
    {
        public string Empresa { get; set; } = "1";
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
        /// <summary>Prefijo de cuenta (o varios separados por comas: «623,705»).</summary>
        public string? Cuenta { get; set; }
        public string? Proveedor { get; set; }
        public string? Documento { get; set; }
        public string? Texto { get; set; }
        public bool SoloPendientes { get; set; } = true;
        public int Pagina { get; set; }
        public int? Tope { get; set; }
    }

    /// <summary>GET/PUT api/Proyectos/Apuntes/{nº orden}/Imputaciones.</summary>
    public class ImputacionesApunteModel
    {
        public string? Empresa { get; set; }
        public int NumeroOrden { get; set; }
        /// <summary>Null si el apunte ya no está en la contabilidad.</summary>
        public ApunteProyectoModel? Apunte { get; set; }
        public List<ImputacionApunteModel> Imputaciones { get; set; } = new List<ImputacionApunteModel>();
        public bool Cambiado { get; set; }
        public List<string> Avisos { get; set; } = new List<string>();
    }

    /// <summary>Cuerpo del PUT api/Proyectos/Apuntes/{nº orden}/Imputaciones: SUSTITUYE el reparto (lista vacía = quitarlo).</summary>
    public class PeticionImputacionesApunte
    {
        public string Empresa { get; set; } = "1";
        /// <summary>El importe del apunte que se ha visto: si ya no es ese, la API da 409 APUNTE_CAMBIADO.</summary>
        public decimal? ImporteApunte { get; set; }
        public List<ImputacionPedida> Imputaciones { get; set; } = new List<ImputacionPedida>();
    }

    public class ImputacionPedida
    {
        public int ProyectoId { get; set; }
        /// <summary>Positivo: el signo lo da el apunte.</summary>
        public decimal Importe { get; set; }
        public string? Comentario { get; set; }
    }

    public class LineaCuentaExplotacionModel
    {
        public string? Cuenta { get; set; }
        public string? NombreCuenta { get; set; }
        public decimal Importe { get; set; }
        public int NumeroApuntes { get; set; }
    }

    public class DetalleCuentaExplotacionModel
    {
        public int IdImputacion { get; set; }
        public int NumeroOrdenApunte { get; set; }
        public DateTime Fecha { get; set; }
        public string? Cuenta { get; set; }
        public string? NombreCuenta { get; set; }
        public string? Naturaleza { get; set; }
        public string? Documento { get; set; }
        public string? Concepto { get; set; }
        public int? Asiento { get; set; }
        public string? Tercero { get; set; }
        public string? TipoTercero { get; set; }
        public string? NombreTercero { get; set; }
        public decimal Debe { get; set; }
        public decimal Haber { get; set; }
        public decimal ImporteApunte { get; set; }
        public decimal Importe { get; set; }
        /// <summary>Lo que suma (o resta, si es abono) a los ingresos o a los gastos.</summary>
        public decimal Efecto { get; set; }
        public decimal Porcentaje { get; set; }
        public string? Comentario { get; set; }
        public string? Usuario { get; set; }
        public DateTime FechaImputacion { get; set; }

        public string TerceroParaMostrar => string.IsNullOrWhiteSpace(NombreTercero)
            ? (Tercero?.Trim() ?? string.Empty)
            : $"{Tercero?.Trim()} {NombreTercero!.Trim()}";
    }

    public class PresupuestoFrenteARealModel
    {
        public decimal? PresupuestoIngresos { get; set; }
        public decimal? PresupuestoGastos { get; set; }
        public decimal PresupuestoResultado { get; set; }
        public decimal? DesviacionIngresos { get; set; }
        public decimal? DesviacionGastos { get; set; }
        public decimal DesviacionResultado { get; set; }
    }

    /// <summary>GET api/Proyectos/{id}/CuentaExplotacion.</summary>
    public class CuentaExplotacionProyectoModel
    {
        public ProyectoModel? Proyecto { get; set; }
        public List<LineaCuentaExplotacionModel> Ingresos { get; set; } = new List<LineaCuentaExplotacionModel>();
        public List<LineaCuentaExplotacionModel> Gastos { get; set; } = new List<LineaCuentaExplotacionModel>();
        public decimal TotalIngresos { get; set; }
        public decimal TotalGastos { get; set; }
        public decimal Resultado { get; set; }
        /// <summary>En %; null sin ingresos.</summary>
        public decimal? Margen { get; set; }
        /// <summary>Solo si el proyecto tiene presupuesto.</summary>
        public PresupuestoFrenteARealModel? Presupuesto { get; set; }
        public List<DetalleCuentaExplotacionModel> Detalle { get; set; } = new List<DetalleCuentaExplotacionModel>();
    }

    public class FilaComparativaProyectosModel
    {
        public string? Cuenta { get; set; }
        public string? NombreCuenta { get; set; }
        /// <summary>En el orden de <see cref="ComparativaProyectosModel.Proyectos"/>.</summary>
        public List<decimal> Importes { get; set; } = new List<decimal>();
    }

    /// <summary>GET api/Proyectos/Comparativa.</summary>
    public class ComparativaProyectosModel
    {
        public List<ProyectoModel> Proyectos { get; set; } = new List<ProyectoModel>();
        public List<FilaComparativaProyectosModel> Ingresos { get; set; } = new List<FilaComparativaProyectosModel>();
        public List<FilaComparativaProyectosModel> Gastos { get; set; } = new List<FilaComparativaProyectosModel>();
        public List<decimal> TotalesIngresos { get; set; } = new List<decimal>();
        public List<decimal> TotalesGastos { get; set; } = new List<decimal>();
        public List<decimal> Resultados { get; set; } = new List<decimal>();
        public List<decimal?> Margenes { get; set; } = new List<decimal?>();
    }

    /// <summary>Un fichero que manda la API (el Excel de la cuenta de explotación).</summary>
    public class FicheroProyecto
    {
        public string? Nombre { get; set; }
        public byte[] Contenido { get; set; } = Array.Empty<byte>();
    }
}
