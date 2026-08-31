using System;

namespace Nesto.Modulos.OfertasCombinadas.Models
{
    /// <summary>
    /// NestoAPI#423: una campaña comercial. Hasta ahora las campañas vivían SOLO en las reglas de
    /// catálogo de PrestaShop, con dos consecuencias: el profesional no se llevaba el descuento de
    /// campaña (de 1.666 productos en las rebajas de verano de 2026, 1.649 profesionales se
    /// quedaron fuera, y en 502 el profesional acababa pagando MÁS que el público), y la única
    /// forma de meterlas en Nesto era teclear INSERTs a mano.
    ///
    /// Por debajo una campaña ES una fila de DescuentosProducto de tarifa, no una tabla nueva: así
    /// el descuento que anuncia la tienda y el que cobra el pedido salen del mismo sitio por
    /// construcción. Lo que la distingue de un descuento de siempre es que lleva fechas, audiencia,
    /// o las dos cosas.
    /// </summary>
    public class CampanaModel
    {
        /// <summary>Nº Orden de la fila en DescuentosProducto. 0 mientras no está guardada.</summary>
        public int Id { get; set; }

        /// <summary>Uno de los dos, nunca los dos: la campaña es de un producto O de una familia.</summary>
        public string Producto { get; set; }
        public string Familia { get; set; }

        /// <summary>
        /// Solo junto a una familia. NO existe una campaña solo por grupo: el motor de precios no
        /// tiene ningún nivel de tarifa que mire únicamente el grupo, así que no se la cobraría a
        /// nadie. El servidor la rechaza.
        /// </summary>
        public string Grupo { get; set; }

        /// <summary>En tanto por uno, como en la tabla: 0,20 = 20 %.</summary>
        public decimal Descuento { get; set; }

        /// <summary>Nulo = el público hereda el mismo porcentaje que el profesional.</summary>
        public decimal? DescuentoPublico { get; set; }

        /// <summary>
        /// 0 = no va a la web (solo la cobra Nesto), 1 = solo profesionales, 2 = ambos.
        /// El 3 ("solo público") está prohibido en la base de datos: el motor de precios no mira
        /// la audiencia, así que le descontaría igual al profesional en el pedido y la tienda
        /// estaría diciendo una cosa mientras Nesto cobra otra.
        /// </summary>
        public byte AudienciaOferta { get; set; }

        /// <summary>Nulas = sin límite por ese lado. Inclusivas: hasta el 31/08 vale todo el 31.</summary>
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }

        /// <summary>
        /// Nombre de la campaña a la que pertenece ("Rebajas verano 2026", "Black Friday 2025"...).
        ///
        /// Es una ETIQUETA, no una entidad: una campaña es "todas las filas que comparten este
        /// texto". Sin ella, la única forma de saber que 2.017 filas eran las rebajas de verano era
        /// buscarlas por una ventana de cinco minutos en el reloj del día que se metieron.
        /// Null = no pertenece a ninguna campaña (los descuentos de siempre).
        /// </summary>
        public string Campana { get; set; }

        /// <summary>Lo calcula el servidor: si la campaña está corriendo hoy.</summary>
        public bool Vigente { get; set; }

        public string Usuario { get; set; }
        public DateTime FechaModificacion { get; set; }
    }

    /// <summary>
    /// NestoAPI#423: una campaña vista por encima. Llena el desplegable del filtro y, sobre todo,
    /// enseña los números ANTES de operar en bloque: nadie debería cerrar o borrar una campaña sin
    /// ver antes cuántas filas se lleva por delante.
    /// </summary>
    public class ResumenCampanaModel
    {
        public string Campana { get; set; }
        public int Filas { get; set; }

        /// <summary>
        /// De esas, cuántas se anuncian de verdad en la tienda. Puede ser 0 sin que sea un error:
        /// las rebajas de verano de 2026 son 2.017 filas y ninguna viaja, porque se metieron antes
        /// de que existiera la audiencia.
        /// </summary>
        public int FilasQueViajan { get; set; }

        public int Vigentes { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }

        /// <summary>Lo que se ve en el desplegable.</summary>
        public string Descripcion => $"{Campana}  ({Filas} filas, {FilasQueViajan} en la web)";
    }

    /// <summary>
    /// NestoAPI#423: lo que devuelve una operación en bloque. Los dos números son distintos y los
    /// dos importan: filas tocadas y productos que hay que republicar en la tienda.
    /// </summary>
    public class ResultadoOperacionCampanaModel
    {
        public string Campana { get; set; }
        public int FilasAfectadas { get; set; }
        public int ProductosEncolados { get; set; }
    }
}
