﻿Imports System.Collections.ObjectModel
Imports Nesto.Models
Imports System.Windows
Imports System.Net
Imports System.IO
Imports System.Text.RegularExpressions
Imports Microsoft.Practices.Prism.Interactivity.InteractionRequest
Imports System.Transactions
Imports Nesto.Contratos

Public Class AgenciaASM
    Implements IAgencia

    Private Const EMPRESA_ESPEJO As String = "3  "

    ' Propiedades de Prism
    Private _NotificationRequest As InteractionRequest(Of INotification)
    Public Property NotificationRequest As InteractionRequest(Of INotification)
        Get
            Return _NotificationRequest
        End Get
        Private Set(value As InteractionRequest(Of INotification))
            _NotificationRequest = value
        End Set
    End Property

    'Private agenciaSeleccionada As AgenciasTransporte
    Private agenciaVM As AgenciasViewModel

    Public Sub New(agencia As AgenciasViewModel)
        If Not IsNothing(agencia) Then

            NotificationRequest = New InteractionRequest(Of INotification)
            'ConfirmationRequest = New InteractionRequest(Of IConfirmation)

            ListaTiposRetorno = New ObservableCollection(Of tipoIdDescripcion) From {
                New tipoIdDescripcion(0, "Sin Retorno"),
                New tipoIdDescripcion(1, "Con Retorno"),
                New tipoIdDescripcion(2, "Retorno Opcional")
            }
            ListaServicios = New ObservableCollection(Of tipoIdDescripcion) From {
                New tipoIdDescripcion(1, "Courier"),
                New tipoIdDescripcion(37, "Economy"),
                New tipoIdDescripcion(54, "EuroEstándar"),
                New tipoIdDescripcion(74, "EuroBusiness Parcel"),
                New tipoIdDescripcion(76, "EuroBusiness Small Parcel"),
                New tipoIdDescripcion(6, "Carga")
            }
            ListaHorarios = New ObservableCollection(Of tipoIdDescripcion) From {
                New tipoIdDescripcion(3, "ASM24"),
                New tipoIdDescripcion(2, "ASM14"),
                New tipoIdDescripcion(10, "Marítimo"),
                New tipoIdDescripcion(18, "Economy")
            }

            ListaPaises = rellenarPaises()

            'agenciaSeleccionada = agencia.agenciaSeleccionada
            agenciaVM = agencia
        End If



    End Sub

    ' Funciones
    Public Function cargarEstado(envio As EnviosAgencia) As XDocument Implements IAgencia.cargarEstado
        If IsNothing(envio) Then
            NotificationRequest.Raise(New Notification() With {
                 .Title = "Error",
                .Content = "No hay ningún envío seleccionado, no se puede cargar el estado"
            })
            Return Nothing
        End If
        Dim myUri As New Uri("http://www.asmred.com/WebSrvs/MiraEnvios.asmx/GetExpCli?codigo=" + envio.CodigoBarras + "&uid=" + agenciaVM.agenciaSeleccionada.Identificador)
        If myUri.Scheme = Uri.UriSchemeHttp Then
            'Dim myRequest As HttpWebRequest = HttpWebRequest.Create(myUri)
            Dim myRequest As HttpWebRequest = CType(WebRequest.Create(myUri), HttpWebRequest)
            myRequest.Method = WebRequestMethods.Http.Get

            Dim myResponse As HttpWebResponse = myRequest.GetResponse()
            If myResponse.StatusCode = HttpStatusCode.OK Then

                Dim reader As New StreamReader(myResponse.GetResponseStream())
                Dim responseData As String = reader.ReadToEnd()
                myResponse.Close()
                Return XDocument.Parse(responseData)
            End If
        End If
        Return Nothing
    End Function
    Public Function transformarXMLdeEstado(envio As XDocument) As estadoEnvio Implements IAgencia.transformarXMLdeEstado
        Dim estado As New estadoEnvio
        Dim expedicion As New expedicion
        Dim trackinglistxml As XElement
        Dim tracking As tracking
        Dim digitalizacionesxml As XElement
        Dim digitalizacion As digitalizacion

        If IsNothing(envio) Then
            Return Nothing
        End If

        For Each nodo In envio.Root.Descendants("exp")
            expedicion.numeroExpedicion = nodo.Descendants("expedicion").FirstOrDefault.Value
            expedicion.fecha = nodo.Descendants("fecha").FirstOrDefault.Value
            expedicion.fechaEstimada = nodo.Descendants("FPEntrega").FirstOrDefault.Value

            trackinglistxml = nodo.Descendants("tracking_list").FirstOrDefault
            For Each track In trackinglistxml.Descendants("tracking")
                tracking = New tracking
                tracking.estadoTracking = track.Descendants("evento").FirstOrDefault.Value
                tracking.fechaTracking = track.Descendants("fecha").FirstOrDefault.Value
                expedicion.listaTracking.Add(tracking)
                tracking = Nothing
            Next

            digitalizacionesxml = nodo.Descendants("digitalizaciones").FirstOrDefault
            For Each dig In digitalizacionesxml.Descendants("digitalizacion")
                digitalizacion = New digitalizacion
                digitalizacion.tipo = dig.Descendants("tipo").FirstOrDefault.Value
                digitalizacion.urlDigitalizacion = New Uri(dig.Descendants("imagen").FirstOrDefault.Value)
                estado.listaDigitalizaciones.Add(digitalizacion)
                digitalizacion = Nothing
            Next
        Next
        agenciaVM.digitalizacionActual = estado.listaDigitalizaciones.LastOrDefault
        estado.listaExpediciones.Add(expedicion)
        agenciaVM.cmdDescargarImagen.RaiseCanExecuteChanged()
        Return estado
    End Function
    Private Function calcularMensajeError(numeroError As Integer) As String 'Implements IAgencia.calcularMensajeError
        Select Case numeroError
            Case -33
                Return "Ya existe el código de barras de la expedición"
            Case -69
                Return "No se pudo canalizar el envío"
            Case -70
                Return "Ya existe se ha enviado este pedido para esta fecha y cliente"
            Case -108
                Return "El nombre del remitente debe tener al menos tres caracteres"
            Case -109
                Return "La dirección del remitente debe tener al menos tres caracteres"
            Case -110
                Return "La población del remitente debe tener al menos tres caracteres"
            Case -111
                Return "El código postal del remitente debe tener al menos cuatro caracteres"
            Case -111
                Return "La referencia del cliente está duplicada"
            Case -119
                Return "Error no controlado por el webservice de la agencia"
            Case -128
                Return "El nombre del destinatario debe tener al menos tres caracteres"
            Case -129
                Return "La dirección del destinatario debe tener al menos tres caracteres"
            Case -130
                Return "La población del destinatario debe tener al menos tres caracteres"
            Case -131
                Return "El código postal del destinatario debe tener al menos cuatro caracteres"
            Case Else
                Return "El código de error " + numeroError.ToString() + " no está controlado por Nesto"
        End Select
    End Function
    Public Function calcularCodigoBarras(agenciaVM As AgenciasViewModel) As String Implements IAgencia.calcularCodigoBarras
        Return agenciaVM.agenciaSeleccionada.PrefijoCodigoBarras.ToString + agenciaVM.envioActual.Numero.ToString("D7")
    End Function
    Public Sub calcularPlaza(ByVal codPostal As String, ByRef nemonico As String, ByRef nombrePlaza As String, ByRef telefonoPlaza As String, ByRef emailPlaza As String) Implements IAgencia.calcularPlaza
        'Comenzamos la llamada
        Dim soap As String = "<?xml version=""1.0"" encoding=""utf-8""?>" &
             "<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" " &
              "xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" " &
              "xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">" &
              "<soap:Body>" &
                    "<GetPlazaXCP xmlns=""http://www.asmred.com/"">" &
                        "<codPais>" + agenciaVM.paisActual.id.ToString + "</codPais>" &
                        "<cp>" + codPostal + "</cp>" &
                    "</GetPlazaXCP>" &
                "</soap:Body>" &
             "</soap:Envelope>"

        Dim req As HttpWebRequest = WebRequest.Create("http://www.asmred.com/WebSrvs/b2b.asmx?op=GetPlazaXCP")
        req.Headers.Add("SOAPAction", """http://www.asmred.com/GetPlazaXCP""")
        req.ContentType = "text/xml; charset=""utf-8"""
        req.Accept = "text/xml"
        req.Method = "POST"

        Using stm As Stream = req.GetRequestStream()
            Using stmw As StreamWriter = New StreamWriter(stm)
                stmw.Write(soap)
            End Using
        End Using

        Dim response As WebResponse = req.GetResponse()
        Dim responseStream As New StreamReader(response.GetResponseStream())
        soap = responseStream.ReadToEnd

        Dim respuestaXML As XDocument
        respuestaXML = XDocument.Parse(soap)

        Dim elementoXML As XElement
        Dim Xns As XNamespace = XNamespace.Get("http://www.asmred.com/")
        elementoXML = respuestaXML.Descendants(Xns + "GetPlazaXCPResult").First().FirstNode
        respuestaXML = New XDocument
        respuestaXML.AddFirst(elementoXML)

        'Debug.Print(respuestaXML.ToString)
        If Not IsNothing(elementoXML.Element("Nemonico")) Then
            nemonico = elementoXML.Element("Nemonico").Value
            nombrePlaza = elementoXML.Element("Nombre").Value
            telefonoPlaza = elementoXML.Element("Telefono").Value
            telefonoPlaza = Regex.Replace(telefonoPlaza, "([^0-9])", "")
            'telefonoPlaza = elementoXML.Element("Telefono").Value.Replace(" "c, String.Empty)
            emailPlaza = elementoXML.Element("Mail").Value
        End If
    End Sub
    Private Function construirXMLdeSalida(servicio As IAgenciaService) As XDocument 'Implements IAgencia.construirXMLdeSalida
        Dim empresa = servicio.CargarListaEmpresas().Single(Function(e) e.Número = agenciaVM.envioActual.Empresa)
        Dim xml As New XDocument
        'xml = XDocument.Load("C:\Users\Carlos.NUEVAVISION\Desktop\ASM\webservice\XML-IN-B.xml")
        'xml.Descendants("Servicios").FirstOrDefault().Add(New XElement("Servicios", New XAttribute("uidcliente", ""), New XAttribute("xmlns", "http://www.asmred.com/")))

        ' Si no hay envioActual devolvemos el xml vacío
        If IsNothing(agenciaVM.envioActual) Then
            Return xml
        End If

        'Añadimos el nodo raíz (Servicios)
        xml.AddFirst(
            <Servicios uidcliente=<%= agenciaVM.envioActual.AgenciasTransporte.Identificador %> xmlns="http://www.asmred.com/">
                <Envio codbarras=<%= agenciaVM.envioActual.CodigoBarras %>>
                    <Fecha><%= agenciaVM.envioActual.Fecha.ToShortDateString %></Fecha>
                    <Portes>P</Portes>
                    <Servicio><%= agenciaVM.envioActual.Servicio %></Servicio>
                    <Horario><%= agenciaVM.envioActual.Horario %></Horario>
                    <Bultos><%= agenciaVM.envioActual.Bultos %></Bultos>
                    <Peso>1</Peso>
                    <Retorno><%= agenciaVM.envioActual.Retorno %></Retorno>
                    <Pod>N</Pod>
                    <Remite>
                        <Plaza></Plaza>
                        <Nombre><%= If(empresa.Número = EMPRESA_ESPEJO, "Nueva Visión", empresa.Nombre.Trim) %></Nombre>
                        <Direccion><%= If(empresa.Número = EMPRESA_ESPEJO, "c/ Río Tiétar, 11", empresa.Dirección.Trim) %></Direccion>
                        <Poblacion><%= If(empresa.Número = EMPRESA_ESPEJO, "Algete", empresa.Población.Trim) %></Poblacion>
                        <Provincia><%= If(empresa.Número = EMPRESA_ESPEJO, "Madrid", empresa.Provincia.Trim) %></Provincia>
                        <Pais>34</Pais>
                        <CP><%= If(empresa.Número = EMPRESA_ESPEJO, "28110", empresa.CodPostal.Trim) %></CP>
                        <Telefono><%= If(empresa.Número = EMPRESA_ESPEJO, "916281914", empresa.Teléfono.Trim) %></Telefono>
                        <Movil></Movil>
                        <Email><%= If(empresa.Número = EMPRESA_ESPEJO, "logistica@nuevavision.es", empresa.Email.Trim) %></Email>
                        <Observaciones></Observaciones>
                    </Remite>
                    <Destinatario>
                        <Codigo></Codigo>
                        <Plaza></Plaza>
                        <Nombre><%= agenciaVM.envioActual.Nombre.Normalize %></Nombre>
                        <Direccion><%= agenciaVM.envioActual.Direccion %></Direccion>
                        <Poblacion><%= agenciaVM.envioActual.Poblacion %></Poblacion>
                        <Provincia><%= agenciaVM.envioActual.Provincia %></Provincia>
                        <Pais><%= agenciaVM.envioActual.Pais %></Pais>
                        <CP><%= agenciaVM.envioActual.CodPostal %></CP>
                        <Telefono><%= agenciaVM.envioActual.Telefono %></Telefono>
                        <Movil><%= agenciaVM.envioActual.Movil %></Movil>
                        <Email><%= agenciaVM.envioActual.Email %></Email>
                        <Observaciones><%= agenciaVM.envioActual.Observaciones %></Observaciones>
                        <ATT><%= agenciaVM.envioActual.Atencion %></ATT>
                    </Destinatario>
                    <Referencias><!-- cualquier numero, siempre distinto a cada prueba-->
                        <Referencia tipo="C"><%= agenciaVM.envioActual.Cliente.Trim %>/<%= agenciaVM.envioActual.Pedido %></Referencia>
                    </Referencias>
                    <Importes>
                        <Debidos>0</Debidos>
                        <Reembolso><%= agenciaVM.envioActual.Reembolso %></Reembolso>
                    </Importes>
                    <Seguro tipo="">
                        <Descripcion></Descripcion>
                        <Importe></Importe>
                    </Seguro>
                    <DevuelveAdicionales>
                        <PlazaDestino/>
                    </DevuelveAdicionales>
                </Envio>
            </Servicios>
        )

        'xml.Root.Attribute("xmlns").Value = "http://www.asmred.com/"
        'Debug.Print(xml.ToString)
        Return xml
    End Function
    Public Sub llamadaWebService(servicio As IAgenciaService) Implements IAgencia.llamadaWebService
        agenciaVM.XMLdeSalida = construirXMLdeSalida(servicio)

        'Comenzamos la llamada
        Dim soap As String = "<?xml version=""1.0"" encoding=""utf-8""?>" &
             "<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" " &
              "xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" " &
              "xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">" &
              "<soap:Body>" &
                    "<GrabaServicios xmlns=""http://www.asmred.com/"">" &
                        "<docIn>" & agenciaVM.XMLdeSalida.ToString & "</docIn>" &
                    "</GrabaServicios>" &
                "</soap:Body>" &
             "</soap:Envelope>"

        Dim req As HttpWebRequest = WebRequest.Create("http://www.asmred.com/WebSrvs/b2b.asmx?op=GrabaServicios")
        req.Headers.Add("SOAPAction", """http://www.asmred.com/GrabaServicios""")
        req.ContentType = "text/xml; charset=""utf-8"""
        req.Accept = "text/xml"
        req.Method = "POST"

        Try
            Using stm As Stream = req.GetRequestStream()
                Using stmw As StreamWriter = New StreamWriter(stm)
                    stmw.Write(soap)
                End Using
            End Using

            Dim response As WebResponse = req.GetResponse()
            Dim responseStream As New StreamReader(response.GetResponseStream())
            soap = responseStream.ReadToEnd
            agenciaVM.XMLdeEntrada = XDocument.Parse(soap)
        Catch ex As Exception
            agenciaVM.mensajeError = "El servidor de la agencia no está respondiendo"
            Return
        End Try


        Dim elementoXML As XElement
        Dim Xns As XNamespace = XNamespace.Get("http://www.asmred.com/")
        elementoXML = agenciaVM.XMLdeEntrada.Descendants(Xns + "GrabaServiciosResult").First().FirstNode
        agenciaVM.XMLdeEntrada = New XDocument
        agenciaVM.XMLdeEntrada.AddFirst(elementoXML)

        If elementoXML.Element("Envio").Element("Resultado").Attribute("return").Value <> "0" Then
            If elementoXML.Element("Envio").Element("Errores").HasElements Then
                agenciaVM.mensajeError = elementoXML.Element("Envio").Element("Errores").Element("Error").Value
            Else
                agenciaVM.mensajeError = calcularMensajeError(elementoXML.Element("Envio").Element("Resultado").Attribute("return").Value)
            End If
        Else
            agenciaVM.mensajeError = servicio.TramitarEnvio(agenciaVM.envioActual)
            agenciaVM.listaEnvios = servicio.CargarListaEnvios(agenciaVM.agenciaSeleccionada.Numero)
            agenciaVM.envioActual = agenciaVM.listaEnvios.LastOrDefault ' lo pongo para que no se vaya al último
        End If


        'Debug.Print(XMLdeEntrada.ToString)

    End Sub
    Public Async Sub imprimirEtiqueta() Implements IAgencia.imprimirEtiqueta
        If IsNothing(agenciaVM.envioActual.CodigoBarras) OrElse agenciaVM.envioActual.CodigoBarras.Trim = "" Then
            Throw New Exception("El envío debe tener un código de barras asignada para poder imprimir la etiqueta")
        End If

        Dim mainViewModel As New MainViewModel
        Dim puerto As String = Await mainViewModel.leerParametro(agenciaVM.envioActual.Empresa, "ImpresoraBolsas")

        Dim objFSO
        Dim objStream
        objFSO = CreateObject("Scripting.FileSystemObject")
        objStream = objFSO.CreateTextFile(puerto) 'Puerto al cual se envía la impresión  
        Dim i As Integer



        Try
            For i = 1 To agenciaVM.bultos
                objStream.Writeline("I8,A,034")
                objStream.Writeline("N")
                objStream.Writeline("A40,10,0,4,1,1,N,""" + agenciaVM.envioActual.Nombre + """")
                objStream.Writeline("A40,50,0,4,1,1,N,""" + agenciaVM.envioActual.Direccion + """")
                objStream.Writeline("A40,90,0,4,1,1,N,""" + agenciaVM.envioActual.CodPostal + " " + agenciaVM.envioActual.Poblacion + """")
                objStream.Writeline("A40,130,0,4,1,1,N,""" + agenciaVM.envioActual.Provincia + """")
                objStream.Writeline("A40,170,0,4,1,1,N,""Bulto: " + i.ToString + "/" + agenciaVM.bultos.ToString _
                                    + ". Cliente: " + agenciaVM.envioActual.Cliente.Trim + ". Fecha: " + agenciaVM.envioActual.Fecha + """")
                objStream.Writeline("B40,210,0,2C,4,8,200,B,""" + agenciaVM.envioActual.CodigoBarras + i.ToString("D3") + """")
                objStream.Writeline("A40,450,0,4,1,2,N,""" + agenciaVM.envioActual.Nemonico + " " + agenciaVM.envioActual.NombrePlaza + """")
                objStream.Writeline("A40,510,0,4,1,2,N,""" + agenciaVM.listaHorarios.Where(Function(x) x.id = agenciaVM.envioActual.Horario).FirstOrDefault.descripcion + """")
                objStream.Writeline("A590,265,0,5,2,2,N,""" + agenciaVM.envioActual.Nemonico + """")
                objStream.Writeline("P1")
                objStream.Writeline("")
            Next

            '' Insertamos la etiqueta en la tabla
            'Dim etiqueta As New EtiquetasPicking With { _
            '.Empresa = envioActual.Empresa,
            '.Número = envioActual.Pedido,
            '.Picking = envioActual.Empresas.MaxPickingListado, 'esto está mal
            '.NºBultos = envioActual.Bultos,
            '.UsuarioQuePrepara = multiusuario.Número,
            '.NºCliente = envioActual.Cliente,
            '.Contacto = envioActual.Contacto}
            'DbContext.AddToEtiquetasPicking(etiqueta)
            'DbContext.SaveChanges()


        Catch ex As Exception
            agenciaVM.mensajeError = ex.InnerException.Message
        Finally
            objStream.Close()
            objFSO = Nothing
            objStream = Nothing
        End Try
    End Sub
    Public ReadOnly Property visibilidadSoloImprimir As Visibility Implements IAgencia.visibilidadSoloImprimir
        Get
            Return Visibility.Hidden
        End Get
    End Property
    Public ReadOnly Property retornoSoloCobros As Integer Implements IAgencia.retornoSoloCobros
        Get
            Return 0 ' Sin retorno
        End Get
    End Property
    Public ReadOnly Property servicioSoloCobros As Integer Implements IAgencia.servicioSoloCobros
        Get
            Return 1 ' Courier
        End Get
    End Property
    Public ReadOnly Property horarioSoloCobros As Integer Implements IAgencia.horarioSoloCobros
        Get
            Return 3 ' ASM24
        End Get
    End Property
    Public ReadOnly Property retornoSinRetorno As Integer Implements IAgencia.retornoSinRetorno
        Get
            Return 0 ' Sin Retorno
        End Get
    End Property
    Public ReadOnly Property paisDefecto As Integer Implements IAgencia.paisDefecto
        Get
            Return 34
        End Get
    End Property
    Public ReadOnly Property retornoObligatorio As Integer Implements IAgencia.retornoObligatorio
        Get
            Return 1 ' Retorno obligatorio
        End Get
    End Property


    Private Function rellenarPaises() As ObservableCollection(Of tipoIdIntDescripcion)
        Return New ObservableCollection(Of tipoIdIntDescripcion) From {
            New tipoIdIntDescripcion(34, "ESPAÑA"),
            New tipoIdIntDescripcion(351, "PORTUGAL"),
            New tipoIdIntDescripcion(49, "ALEMANIA"),
            New tipoIdIntDescripcion(966, "ARABIA SAUDITA"),
            New tipoIdIntDescripcion(213, "ARGELIA"),
            New tipoIdIntDescripcion(54, "ARGENTINA"),
            New tipoIdIntDescripcion(61, "AUSTRALIA"),
            New tipoIdIntDescripcion(43, "AUSTRIA"),
            New tipoIdIntDescripcion(32, "BELGICA"),
            New tipoIdIntDescripcion(591, "BOLIVIA"),
            New tipoIdIntDescripcion(387, "BOSNIA-HEZERGOVINA"),
            New tipoIdIntDescripcion(55, "BRASIL"),
            New tipoIdIntDescripcion(359, "BULGARIA"),
            New tipoIdIntDescripcion(11, "CANADA"),
            New tipoIdIntDescripcion(57, "COLOMBIA"),
            New tipoIdIntDescripcion(82, "COREA DEL SUR"),
            New tipoIdIntDescripcion(506, "COSTA RICA"),
            New tipoIdIntDescripcion(42, "REPUBLICA CHECA"),
            New tipoIdIntDescripcion(56, "CHILE"),
            New tipoIdIntDescripcion(86, "CHINA"),
            New tipoIdIntDescripcion(385, "CROACIA"),
            New tipoIdIntDescripcion(45, "DINAMARCA"),
            New tipoIdIntDescripcion(593, "ECUADOR"),
            New tipoIdIntDescripcion(20, "EGIPTO"),
            New tipoIdIntDescripcion(503, "EL SALVADOR"),
            New tipoIdIntDescripcion(421, "REPUBLICA ESLOVACA"),
            New tipoIdIntDescripcion(386, "ESLOVENIA"),
            New tipoIdIntDescripcion(1, "ESTADOS UNIDOS"),
            New tipoIdIntDescripcion(63, "FILIPINAS"),
            New tipoIdIntDescripcion(358, "FINLANDIA"),
            New tipoIdIntDescripcion(33, "FRANCIA"),
            New tipoIdIntDescripcion(30, "GRECIA"),
            New tipoIdIntDescripcion(502, "GUATEMALA"),
            New tipoIdIntDescripcion(504, "HONDURAS"),
            New tipoIdIntDescripcion(852, "HONG KONG"),
            New tipoIdIntDescripcion(36, "HUNGRIA"),
            New tipoIdIntDescripcion(91, "INDIA"),
            New tipoIdIntDescripcion(353, "IRLANDA"),
            New tipoIdIntDescripcion(354, "ISLANDIA"),
            New tipoIdIntDescripcion(972, "ISRAEL"),
            New tipoIdIntDescripcion(39, "ITALIA"),
            New tipoIdIntDescripcion(81, "JAPON"),
            New tipoIdIntDescripcion(41, "LIECHTENSTEIN"),
            New tipoIdIntDescripcion(352, "LUXEMBURGO"),
            New tipoIdIntDescripcion(389, "MACEDONIA"),
            New tipoIdIntDescripcion(212, "MARRUECOS"),
            New tipoIdIntDescripcion(52, "MEJICO"),
            New tipoIdIntDescripcion(331, "MONACO"),
            New tipoIdIntDescripcion(505, "NICARAGUA"),
            New tipoIdIntDescripcion(47, "NORUEGA"),
            New tipoIdIntDescripcion(64, "NUEVA ZELANDA"),
            New tipoIdIntDescripcion(31, "HOLANDA"),
            New tipoIdIntDescripcion(507, "PANAMA"),
            New tipoIdIntDescripcion(595, "PARAGUAY"),
            New tipoIdIntDescripcion(51, "PERU"),
            New tipoIdIntDescripcion(48, "POLONIA"),
            New tipoIdIntDescripcion(1809, "PUERTO RICO"),
            New tipoIdIntDescripcion(44, "REINO UNIDO"),
            New tipoIdIntDescripcion(391, "SAN MARINO"),
            New tipoIdIntDescripcion(46, "SUECIA"),
            New tipoIdIntDescripcion(411, "SUIZA"),
            New tipoIdIntDescripcion(886, "TAIWAN"),
            New tipoIdIntDescripcion(66, "THAILANDIA"),
            New tipoIdIntDescripcion(216, "TUNEZ"),
            New tipoIdIntDescripcion(90, "TURQUIA"),
            New tipoIdIntDescripcion(7, "RUSIA"),
            New tipoIdIntDescripcion(598, "URUGUAY"),
            New tipoIdIntDescripcion(396, "VATICANO"),
            New tipoIdIntDescripcion(58, "VENEZUELA"),
            New tipoIdIntDescripcion(381, "YUGOSLAVIA"),
            New tipoIdIntDescripcion(441, "GIBRALTAR"),
            New tipoIdIntDescripcion(360, "ESTONIA"),
            New tipoIdIntDescripcion(40, "RUMANIA"),
            New tipoIdIntDescripcion(234, "NIGERIA"),
            New tipoIdIntDescripcion(380, "UCRANIA"),
            New tipoIdIntDescripcion(833, "VIETNAM"),
            New tipoIdIntDescripcion(53, "CUBA"),
            New tipoIdIntDescripcion(1242, "BAHAMAS"),
            New tipoIdIntDescripcion(691, "MICRONESIA"),
            New tipoIdIntDescripcion(301, "CHIPRE"),
            New tipoIdIntDescripcion(18091, "REPUBLICA DOMINICANA"),
            New tipoIdIntDescripcion(2341, "CABO VERDE"),
            New tipoIdIntDescripcion(911, "SRI LANKA"),
            New tipoIdIntDescripcion(1243, "BERMUDA"),
            New tipoIdIntDescripcion(811, "SINGAPUR"),
            New tipoIdIntDescripcion(9918, "MADEIRA/AZORES"),
            New tipoIdIntDescripcion(692, "INDONESIA"),
            New tipoIdIntDescripcion(693, "ANTILLA HOLANDESA"),
            New tipoIdIntDescripcion(2342, "IVORY COAST"),
            New tipoIdIntDescripcion(77, "LITUANIA"),
            New tipoIdIntDescripcion(2344, "MOZAMBIQUE"),
            New tipoIdIntDescripcion(8111, "MALASIA"),
            New tipoIdIntDescripcion(9666, "LIBANO"),
            New tipoIdIntDescripcion(233, "SUDAFRICA"),
            New tipoIdIntDescripcion(963, "SIRIA"),
            New tipoIdIntDescripcion(2343, "SENEGAL"),
            New tipoIdIntDescripcion(78, "LETONIA(LATVIA)"),
            New tipoIdIntDescripcion(443, "MALTA"),
            New tipoIdIntDescripcion(2345, "KENYA"),
            New tipoIdIntDescripcion(2346, "LIBIA"),
            New tipoIdIntDescripcion(2347, "ANGOLA"),
            New tipoIdIntDescripcion(241, "REPUBLICA GABONESA"),
            New tipoIdIntDescripcion(9633, "JORDANIA")
        }
    End Function

    Private Function IAgencia_EnlaceSeguimiento(envio As EnviosAgencia) As String Implements IAgencia.EnlaceSeguimiento
        Return "http://m.asmred.com/e/" + envio.CodigoBarras + "/" + envio.CodPostal
    End Function

    Public ReadOnly Property ListaPaises As ObservableCollection(Of tipoIdIntDescripcion) Implements IAgencia.ListaPaises
    Public ReadOnly Property ListaTiposRetorno As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaTiposRetorno
    Public ReadOnly Property ListaServicios As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaServicios
    Public ReadOnly Property ListaHorarios As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaHorarios

    Public ReadOnly Property ServicioDefecto As Integer Implements IAgencia.ServicioDefecto
        Get
            Return 1 'Sin retorno
        End Get
    End Property

    Public ReadOnly Property HorarioDefecto As Integer Implements IAgencia.HorarioDefecto
        Get
            Return 3 ' ASM24
        End Get
    End Property
End Class