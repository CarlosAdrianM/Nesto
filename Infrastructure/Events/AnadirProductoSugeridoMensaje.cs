using CommunityToolkit.Mvvm.Messaging.Messages;
using System;

namespace Nesto.Infrastructure.Events
{
    /// <summary>
    /// Nesto#544 (corte 2): el producto que el vendedor añade desde la cinta «Puede ofrecer» de otra pantalla (el rapport) y
    /// tiene que entrar en una plantilla de venta de ese cliente. Va en el mensaje <see cref="AnadirProductoSugeridoMensaje"/>
    /// (si hay una plantilla abierta del cliente) o en los parámetros de la navegación a una plantilla nueva.
    /// </summary>
    public sealed class ProductoSugeridoParaPlantilla
    {
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string Producto { get; set; }
        /// <summary>La sugerencia de la cinta de origen: contra ella se apunta «Anadido».</summary>
        public int IdSugerencia { get; set; }
        /// <summary>La sesión de la cinta de origen: la plantilla se queda con ella (la API enlaza el pedido con la sugerencia).</summary>
        public Guid? IdSesion { get; set; }

        /// <summary>Las claves de la navegación a una plantilla nueva (PlantillaVentaView).</summary>
        public const string PARAMETRO_EMPRESA = "empresaSugerencia";
        public const string PARAMETRO_CLIENTE = "clienteSugerencia";
        public const string PARAMETRO_CONTACTO = "contactoSugerencia";
        public const string PARAMETRO_PRODUCTO = "productoSugerido";
        public const string PARAMETRO_ID_SUGERENCIA = "idSugerencia";
        public const string PARAMETRO_ID_SESION = "idSesionSugerencias";
    }

    /// <summary>
    /// Nesto#544 (corte 2): «¿hay una plantilla abierta de este cliente?». La primera plantilla abierta (de verdad, en su
    /// pestaña) de ese cliente contesta: se trae al frente, mete el producto y responde si ha entrado. Si nadie contesta,
    /// quien pregunta abre una plantilla nueva con los parámetros de <see cref="ProductoSugeridoParaPlantilla"/>.
    /// </summary>
    public sealed class AnadirProductoSugeridoMensaje : AsyncRequestMessage<bool>
    {
        public AnadirProductoSugeridoMensaje(ProductoSugeridoParaPlantilla producto)
        {
            Producto = producto ?? throw new ArgumentNullException(nameof(producto));
        }

        public ProductoSugeridoParaPlantilla Producto { get; }
    }
}
