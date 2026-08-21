Imports Newtonsoft.Json

''' <summary>
''' Nesto#340 (slice A3): el pedido que necesita el módulo de Agencias, SIN Entity Framework.
''' Sustituye a la entidad CabPedidoVta que devolvían los 4 métodos CargarPedido* de
''' AgenciaService.
'''
''' Los nombres de las propiedades son DELIBERADAMENTE los mismos que los de la entidad
''' (Nº_Cliente, Comentarios, Clientes...): el XAML de Agencias bindea contra
''' pedidoSeleccionado.Nº_Cliente, .Contacto, .Comentarios y .ComentarioPicking, y un binding roto
''' en WPF no da error, solo deja de mostrar el dato. Manteniendo los nombres, al cablear esto no
''' hay que tocar ni la vista ni el ViewModel: solo cambia el tipo.
'''
''' El mapeo con JsonProperty traduce los nombres limpios del DTO de la API a los de la entidad.
''' </summary>
Public Class PedidoAgenciaModel

    ''' <summary>
    ''' ⚠️ CON EL PADDING DE LA BD ("1  "). Agencias compara este campo SIN Trim contra
    ''' listaEmpresas y listaAgencias, que siguen viniendo de EF con padding. Recortarlo aquí
    ''' rompería esos Single con InvalidOperationException.
    ''' </summary>
    <JsonProperty("empresa")>
    Public Property Empresa As String

    <JsonProperty("numero")>
    Public Property Número As Integer

    ''' <summary>Con el padding de la BD: va sin Trim a EnviosAgencia.Cliente y a búsquedas por igualdad exacta.</summary>
    <JsonProperty("cliente")>
    Public Property Nº_Cliente As String

    ''' <summary>Con el padding de la BD, igual que Nº_Cliente.</summary>
    <JsonProperty("contacto")>
    Public Property Contacto As String

    <JsonProperty("fecha")>
    Public Property Fecha As Date?

    <JsonProperty("vendedor")>
    Public Property Vendedor As String

    <JsonProperty("comentarios")>
    Public Property Comentarios As String

    <JsonProperty("comentarioPicking")>
    Public Property ComentarioPicking As String

    ''' <summary>
    ''' Ficha del cliente. PUEDE SER NOTHING, y eso importa: Agencias lo usa como señal de pedido
    ''' no utilizable y revierte al pedido anterior. No sustituir por un objeto vacío.
    ''' </summary>
    <JsonProperty("clienteFicha")>
    Public Property Clientes As ClienteAgenciaModel

End Class

''' <summary>Nesto#340 (A3): la ficha del cliente que usa Agencias, sin EF.</summary>
Public Class ClienteAgenciaModel

    <JsonProperty("nombre")>
    Public Property Nombre As String

    <JsonProperty("direccion")>
    Public Property Dirección As String

    <JsonProperty("codPostal")>
    Public Property CodPostal As String

    <JsonProperty("poblacion")>
    Public Property Población As String

    <JsonProperty("provincia")>
    Public Property Provincia As String

    <JsonProperty("telefono")>
    Public Property Teléfono As String

    ''' <summary>
    ''' Nunca Nothing: Agencias hace .Any y .ToList sin comprobarlo (con EF era un HashSet vacío).
    ''' </summary>
    <JsonProperty("personasContacto")>
    Public Property PersonasContactoCliente As List(Of PersonaContactoAgenciaModel) = New List(Of PersonaContactoAgenciaModel)

End Class

''' <summary>
''' Nesto#340 (A3): lo único que Agencias lee de una persona de contacto es el cargo y el correo,
''' para elegir el correo de agencia. El criterio de elección sigue viviendo en CorreoCliente.
''' </summary>
Public Class PersonaContactoAgenciaModel

    <JsonProperty("cargo")>
    Public Property Cargo As Short

    <JsonProperty("correoElectronico")>
    Public Property CorreoElectrónico As String

End Class
