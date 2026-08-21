Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Models
Imports Nesto.ViewModels
Imports Newtonsoft.Json

''' <summary>
''' Nesto#340 (slice A3): el POCO que sustituye a la entidad CabPedidoVta en Agencias.
'''
''' Lo que se fija aquí es el CONTRATO con el endpoint api/PedidosVenta/ParaAgencia: si alguien
''' renombra una propiedad del DTO del servidor, o quita un JsonProperty de aquí, el dato llega
''' vacío y en WPF eso NO da error, solo deja de verse. Estos tests son la red.
''' </summary>
<TestClass()>
Public Class PedidoAgenciaModelTests

    ' JSON tal y como lo devuelve el endpoint, con el padding de la BD incluido.
    Private Const JSON_PEDIDO As String = "{""empresa"":""1  "",""numero"":924645,""cliente"":""22709     "",""contacto"":""0  "",""fecha"":""2026-08-21T00:00:00"",""vendedor"":""NV"",""comentarios"":""Llamar antes"",""comentarioPicking"":""Siempre por GLS"",""clienteFicha"":{""nombre"":""SARA VILLEGAS SERRANO"",""direccion"":""CALLE DE LA REINA, 5"",""codPostal"":""28110"",""poblacion"":""ALGETE"",""provincia"":""MADRID"",""telefono"":""916280000"",""personasContacto"":[{""cargo"":1,""correoElectronico"":""gerente@ejemplo.es""},{""cargo"":26,""correoElectronico"":""agencia@ejemplo.es""}]}}"

    <TestMethod()>
    Public Sub PedidoAgenciaModel_DelJsonDelEndpoint_MapeaTodosLosCampos()
        Dim pedido = JsonConvert.DeserializeObject(Of PedidoAgenciaModel)(JSON_PEDIDO)

        Assert.AreEqual(924645, pedido.Número)
        Assert.AreEqual(New Date(2026, 8, 21), pedido.Fecha)
        Assert.AreEqual("NV", pedido.Vendedor)
        Assert.AreEqual("Llamar antes", pedido.Comentarios)
        Assert.AreEqual("Siempre por GLS", pedido.ComentarioPicking)
        Assert.IsNotNull(pedido.Clientes)
        Assert.AreEqual("SARA VILLEGAS SERRANO", pedido.Clientes.Nombre)
        Assert.AreEqual("CALLE DE LA REINA, 5", pedido.Clientes.Dirección)
        Assert.AreEqual("28110", pedido.Clientes.CodPostal)
        Assert.AreEqual("ALGETE", pedido.Clientes.Población)
        Assert.AreEqual("MADRID", pedido.Clientes.Provincia)
        Assert.AreEqual("916280000", pedido.Clientes.Teléfono)
    End Sub

    ''' <summary>
    ''' EL RIESGO Nº 1 DEL SLICE. Agencias hace listaEmpresas.Single(e =&gt; e.Número = pedido.Empresa)
    ''' contra listas que siguen viniendo de EF CON padding. Si el padding se pierde por el camino,
    ''' ese Single lanza InvalidOperationException y se rompe la selección de empresa y de agencia.
    ''' </summary>
    <TestMethod()>
    Public Sub PedidoAgenciaModel_CamposChar_LleganConElPaddingDeLaBd()
        Dim pedido = JsonConvert.DeserializeObject(Of PedidoAgenciaModel)(JSON_PEDIDO)

        Assert.AreEqual("1  ", pedido.Empresa, "Sin padding se rompen los Single de empresa y agencia")
        Assert.AreEqual("22709     ", pedido.Nº_Cliente, "Va sin Trim a EnviosAgencia.Cliente")
        Assert.AreEqual("0  ", pedido.Contacto)
    End Sub

    ''' <summary>
    ''' Agencias usa "pedido sin ficha" como señal para revertir al pedido anterior, así que la
    ''' ficha tiene que poder llegar a Nothing.
    ''' </summary>
    <TestMethod()>
    Public Sub PedidoAgenciaModel_SinFichaDeCliente_LaFichaEsNothing()
        Dim pedido = JsonConvert.DeserializeObject(Of PedidoAgenciaModel)("{""empresa"":""1  "",""numero"":1,""clienteFicha"":null}")

        Assert.IsNotNull(pedido)
        Assert.IsNull(pedido.Clientes, "Sin ficha = señal para Agencias, no un objeto vacío")
    End Sub

    ''' <summary>
    ''' Agencias hace .Any y .ToList sobre las personas de contacto sin comprobar Nothing (con EF
    ''' era un HashSet vacío).
    ''' </summary>
    <TestMethod()>
    Public Sub PedidoAgenciaModel_ClienteSinPersonasDeContacto_LaListaNoEsNothing()
        Dim pedido = JsonConvert.DeserializeObject(Of PedidoAgenciaModel)("{""numero"":1,""clienteFicha"":{""nombre"":""X""}}")

        Assert.IsNotNull(pedido.Clientes.PersonasContactoCliente)
        Assert.AreEqual(0, pedido.Clientes.PersonasContactoCliente.Count)
    End Sub

    ''' <summary>
    ''' El criterio de elección del correo de agencia (cargo 26) sigue viviendo en CorreoCliente:
    ''' aquí solo se comprueba que el modelo sin EF entra por el constructor nuevo y da lo mismo.
    ''' </summary>
    <TestMethod()>
    Public Sub CorreoCliente_ConElModeloSinEf_EligeElCorreoDeCargoAgencia()
        Dim pedido = JsonConvert.DeserializeObject(Of PedidoAgenciaModel)(JSON_PEDIDO)
        Dim personas = pedido.Clientes.PersonasContactoCliente.
            Select(Function(p) New PersonaContactoCorreo(p.Cargo, p.CorreoElectrónico))

        Dim correo As New CorreoCliente(personas)

        Assert.AreEqual("agencia@ejemplo.es", correo.CorreoAgencia())
    End Sub

    <TestMethod()>
    Public Sub CorreoCliente_ConElModeloSinEfYSinCargoAgencia_CaeAlPrimerCorreo()
        Dim personas = {New PersonaContactoCorreo(1S, "gerente@ejemplo.es")}

        Dim correo As New CorreoCliente(personas)

        Assert.AreEqual("gerente@ejemplo.es", correo.CorreoAgencia())
    End Sub

    <TestMethod()>
    Public Sub CorreoCliente_SinPersonas_DevuelveCadenaVacia()
        Dim correo As New CorreoCliente(New List(Of PersonaContactoCorreo))

        Assert.AreEqual(String.Empty, correo.CorreoAgencia())
    End Sub

End Class
