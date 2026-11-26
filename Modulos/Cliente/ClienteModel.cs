using System.Collections.Generic;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// El número propio y «¿Qué es?» (Nesto#643, NestoAPI#874) de una dirección del alta: los tienen la fiscal (<see cref="ClienteCrear"/>)
    /// y la de entrega (<see cref="DireccionEntregaCrear"/>), y los rellena el mismo bloque (BloqueDireccionAlta).
    /// </summary>
    public interface IDireccion2Alta
    {
        string Numero { get; set; }
        bool NumeroConfirmadoSinGoogle { get; set; }
        string TipoLugar { get; set; }
        string Piso { get; set; }
        string Puerta { get; set; }
        string NumeroLocal { get; set; }
        string Rotulo { get; set; }
        string NumeroNave { get; set; }
        string OtroTexto { get; set; }
    }

    /// <summary>
    /// Alta con dirección de entrega distinta (19/11/26, gemela de NestoAPI ClienteCrear.DireccionEntrega): la API crea además un contacto
    /// con esta dirección y deja la entrega por defecto en él. Lo comercial y las personas de contacto los hereda del principal.
    /// </summary>
    public class DireccionEntregaCrear : IDireccion2Alta
    {
        public string Direccion { get; set; }
        public bool DireccionVerificada { get; set; }
        public string Numero { get; set; }
        public bool NumeroConfirmadoSinGoogle { get; set; }
        public string TipoLugar { get; set; }
        public string Piso { get; set; }
        public string Puerta { get; set; }
        public string NumeroLocal { get; set; }
        public string Rotulo { get; set; }
        public string NumeroNave { get; set; }
        public string OtroTexto { get; set; }
        public string CodigoPostal { get; set; }
        public string Poblacion { get; set; }
        public string Provincia { get; set; }
        public string Ruta { get; set; }
        public string Telefono { get; set; }
        public string ComentariosRuta { get; set; }
        public string ComentariosPicking { get; set; }
    }

    public class ClienteCrear : IDireccion2Alta
    {
        /// <summary>Alta con dirección de entrega distinta (19/11/26). Null = el alta de siempre (no se serializa).</summary>
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public DireccionEntregaCrear DireccionEntrega { get; set; }

        public string Cliente { get; set; }
        public string CodigoPostal { get; set; }
        public string Comentarios { get; set; }
        public string ComentariosPicking { get; set; }
        public string ComentariosRuta { get; set; }
        public string Contacto { get; set; }
        public string Direccion { get; set; }
        // Nesto#480 / NestoAPI#499: la dirección la ELIGIÓ el usuario de las que propone Google, no la
        // tecleó. Con el parámetro ExigirDireccionVerificadaAlta encendido, un alta con dirección y
        // este flag a false la rechaza el servidor. En las modificaciones no se mira.
        public bool DireccionVerificada { get; set; }
        // Nesto#643 (NestoAPI#874): el número que se tecleó porque Google dio la calle sin él (cifras o «S/N»). Ya va dentro de
        // Direccion; la API lo vuelve a comprobar con Google. Null si Google dio el número.
        public string Numero { get; set; }
        // Nesto#643 (NestoAPI#874): el usuario aceptó guardar un número que Google no encuentra (sin esto, la API da 400).
        public bool NumeroConfirmadoSinGoogle { get; set; }
        // Nesto#643 (NestoAPI#874): «¿Qué es?» (Piso, Local, Casa, Nave u Otro) y sus datos; la API compone la Dirección 2.
        public string TipoLugar { get; set; }
        public string Piso { get; set; }
        public string Puerta { get; set; }
        public string NumeroLocal { get; set; }
        public string Rotulo { get; set; }
        public string NumeroNave { get; set; }
        public string OtroTexto { get; set; }
        // NestoAPI#874 (corte 2), solo al modificar: borrar la Dirección 2 guardada (sin tipo y sin esto, la API la conserva).
        public bool QuitarDireccion2 { get; set; }
        // NestoAPI#874 (corte 2): la Dirección 2 guardada tal cual (la trae GetClienteCrear; el PUT la ignora).
        public string Direccion2 { get; set; }
        public string Empresa { get; set; }
        public bool EsContacto { get; set; }
        public short? Estado { get; set; }
        public bool Estetica { get; set; }
        // Nesto#555 (sugerencia 770): alumna de cursos y eventos → la API la graba con estado 10 y vendedor NV.
        public bool Alumna { get; set; }
        // Nesto#555 (Laura, 411): público final (otro sector, pide factura) → estado 8 y vendedor NV. Excluyente con Alumna.
        public bool Publico { get; set; }
        public string FormaPago { get; set; }
        public string Iban { get; set; }
        public string Nif { get; set; }
        // NestoAPI#355: país ISO-2 del cliente (default ES lo pone el servidor si viene vacío).
        public string Pais { get; set; }
        public string Nombre { get; set; }
        public bool Peluqueria { get; set; }
        public string PlazosPago { get; set; }
        public string Poblacion { get; set; }
        public string Provincia { get; set; }
        public string Ruta { get; set; }
        public string Telefono { get; set; }
        public string VendedorEstetica { get; set; }
        public string VendedorPeluqueria { get; set; }
        // Nesto#432: 5 posiciones lunes..viernes, '1' abre y '0' cierra. Null = no tocar en la API.
        public string DiasEnServir { get; set; }
        /// <summary>
        /// NestoAPI#541: solo en el reenvío tras aceptar «¿Avisamos a almacén?». Si el cambio de días cierra un día
        /// y el contacto tiene pedidos con picking, la API rechaza el PUT (DIAS_CON_PICKING) hasta que vaya a true.
        /// </summary>
        public bool ConfirmarDiasEnServirConPicking { get; set; }

        /// <summary>
        /// Nesto#523 (NestoAPI#623): código del origen del cliente (campaña), p. ej. «META-HARMONIA-0926». Opcional; null = sin origen.
        /// </summary>
        public string Origen { get; set; }
        /// <summary>
        /// NestoAPI#876: con origen CAMBIO-FISCAL, el número de cliente que tenía antes (obligatorio en el alta; la API comprueba que existe
        /// y no es él mismo). El PUT no lo toca; GetClienteCrear lo trae. No viaja si es null.
        /// </summary>
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string ClienteAnterior { get; set; }
        /// <summary>
        /// NestoAPI#876 (corte nuevo): qué bloques copiar del cliente anterior tras el alta (Comercial, Fiscalidad, CondicionesPago,
        /// Descuentos, Ofertas, Ficha). Null = no copiar nada (no viaja). OJO: en la API una lista vacía es «todos».
        /// </summary>
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public List<string> CopiarDelAnterior { get; set; }

        public string Usuario { get; set; }

        /// <summary>
        /// Nesto#558 (NestoAPI#716): en el PUT Clientes/DejarDeVisitar, por qué se deja de visitar (un código de
        /// Nesto.Infrastructure.Shared.MotivoCambioEstadoCliente). Opcional: sin él la API anota «DejarDeVisitar».
        /// </summary>
        public string MotivoCambioEstado { get; set; }

        /// <summary>Nesto#558 (NestoAPI#716): comentario libre del cambio de estado (200 caracteres). Opcional.</summary>
        public string ComentarioCambioEstado { get; set; }


        public virtual ICollection<PersonaContactoDTO> PersonasContacto { get; set; }
    }
    public class PersonaContactoDTO
    {
        public int Numero { get; set; }
        public string Nombre { get; set; }
        public string CorreoElectronico { get; set; }
        public bool FacturacionElectronica { get; set; }
    }
    public class RespuestaNifNombreCliente
    {
        public bool NifValidado { get; set; }
        public string NifFormateado { get; set; }
        public string NombreFormateado { get; set; }
        public bool ExisteElCliente { get; set; }
        public string NumeroCliente { get; set; }
        public short? EstadoCliente { get; set; }
        public string Empresa { get; set; }
        public string Contacto { get; set; }
        // NestoAPI#388: true = el NIF NO se ha contrastado con el censo de la AEAT (certificado
        // caducado); solo se ha comprobado el formato. Nada se cachea como validado.
        public bool SinVerificar { get; set; }
        // NestoAPI#388: true = el nombre lo iba a poner el censo (persona jurídica) y no se ha
        // podido consultar. Hay que DESBLOQUEAR el campo y que lo escriba el usuario.
        public bool NombreLoDebeEscribirElUsuario { get; set; }
    }
    public class RespuestaDatosGeneralesClientes
    {
        public string CodigoPostal { get; set; }
        public string DireccionFormateada { get; set; }
        public string Poblacion { get; set; }
        public string Provincia { get; set; }
        public string Ruta { get; set; }
        public string TelefonoFormateado { get; set; }
        public string VendedorEstetica { get; set; }
        public string VendedorPeluqueria { get; set; }
        public List<ClienteTelefonoLookup> ClientesMismoTelefono { get; set; }
        // Nesto#643 (NestoAPI#874): el número propio es válido pero Google no lo encuentra; AvisoNumero es la pregunta al usuario.
        public bool NumeroSinConfirmarPorGoogle { get; set; }
        public string AvisoNumero { get; set; }
    }

    public class ClienteTelefonoLookup
    {
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string Nombre { get; set; }
    }
    public class RespuestaDatosBancoCliente
    {
        public bool DatosPagoValidos { get; set; }
        public bool IbanValido { get; set; }
        public string Iban { get; set; }
        public string IbanFormateado { get; set; }
    }
    
}
