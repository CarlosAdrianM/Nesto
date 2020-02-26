Imports System.Collections.ObjectModel
Imports Nesto.Models
Imports System.Windows
Imports System.Net
Imports System.IO
Imports System.Text.RegularExpressions
Imports Microsoft.Practices.Prism.Interactivity.InteractionRequest
Imports System.Transactions
Imports Nesto.Contratos
Imports Nesto.Models.Nesto.Models
Imports System.Threading.Tasks

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
        Dim myUri As New Uri("http://www.asmred.com/WebSrvs/MiraEnvios.asmx/GetExpCli?codigo=" + envio.CodigoBarras + "&uid=" + envio.AgenciasTransporte.Identificador)
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
                        "<codPais>" + agenciaVM.paisActual.Id.ToString + "</codPais>" &
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
    Private Function construirXMLdeSalida(envio As EnviosAgencia, servicio As IAgenciaService) As XDocument 'Implements IAgencia.construirXMLdeSalida
        Dim empresa = servicio.CargarListaEmpresas().Single(Function(e) e.Número = envio.Empresa)
        Dim xml As New XDocument
        'xml = XDocument.Load("C:\Users\Carlos.NUEVAVISION\Desktop\ASM\webservice\XML-IN-B.xml")
        'xml.Descendants("Servicios").FirstOrDefault().Add(New XElement("Servicios", New XAttribute("uidcliente", ""), New XAttribute("xmlns", "http://www.asmred.com/")))

        ' Si no hay envioActual devolvemos el xml vacío
        If IsNothing(envio) Then
            Return xml
        End If

        'Añadimos el nodo raíz (Servicios)
        xml.AddFirst(
            <Servicios uidcliente=<%= envio.AgenciasTransporte.Identificador %> xmlns="http://www.asmred.com/">
                <Envio codbarras=<%= envio.CodigoBarras %>>
                    <Fecha><%= envio.Fecha.ToShortDateString %></Fecha>
                    <Portes>P</Portes>
                    <Servicio><%= envio.Servicio %></Servicio>
                    <Horario><%= envio.Horario %></Horario>
                    <Bultos><%= envio.Bultos %></Bultos>
                    <Peso>1</Peso>
                    <Retorno><%= envio.Retorno %></Retorno>
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
                        <Nombre><%= envio.Nombre.Normalize %></Nombre>
                        <Direccion><%= envio.Direccion %></Direccion>
                        <Poblacion><%= envio.Poblacion %></Poblacion>
                        <Provincia><%= envio.Provincia %></Provincia>
                        <Pais><%= envio.Pais %></Pais>
                        <CP><%= envio.CodPostal %></CP>
                        <Telefono><%= envio.Telefono %></Telefono>
                        <Movil><%= envio.Movil %></Movil>
                        <Email><%= envio.Email %></Email>
                        <Observaciones><%= envio.Observaciones %></Observaciones>
                        <ATT><%= envio.Atencion %></ATT>
                    </Destinatario>
                    <Referencias><!-- cualquier numero, siempre distinto a cada prueba-->
                        <Referencia tipo="C"><%= envio.Cliente.Trim %>/<%= envio.Pedido %></Referencia>
                    </Referencias>
                    <Importes>
                        <Debidos>0</Debidos>
                        <Reembolso><%= envio.Reembolso %></Reembolso>
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

        Return xml
    End Function
    Public Async Function LlamadaWebService(envio As EnviosAgencia, servicio As IAgenciaService) As Task(Of String) Implements IAgencia.LlamadaWebService
        XMLdeSalida = construirXMLdeSalida(envio, servicio)

        'Comenzamos la llamada
        Dim soap As String = "<?xml version=""1.0"" encoding=""utf-8""?>" &
             "<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" " &
              "xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" " &
              "xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">" &
              "<soap:Body>" &
                    "<GrabaServicios xmlns=""http://www.asmred.com/"">" &
                        "<docIn>" & XMLdeSalida.ToString & "</docIn>" &
                    "</GrabaServicios>" &
                "</soap:Body>" &
             "</soap:Envelope>"

        Dim req As HttpWebRequest = WebRequest.Create("http://www.asmred.com/WebSrvs/b2b.asmx?op=GrabaServicios")
        req.Headers.Add("SOAPAction", """http://www.asmred.com/GrabaServicios""")
        req.ContentType = "text/xml; charset=""utf-8"""
        req.Accept = "text/xml"
        req.Method = "POST"

        Try
            Using stm As Stream = Await req.GetRequestStreamAsync()
                Using stmw As StreamWriter = New StreamWriter(stm)
                    stmw.Write(soap)
                End Using
            End Using

            Dim response As WebResponse = req.GetResponse()
            Dim responseStream As New StreamReader(response.GetResponseStream())
            soap = responseStream.ReadToEnd
            XMLdeEntrada = XDocument.Parse(soap)
        Catch ex As Exception
            Return "El servidor de la agencia no está respondiendo"
        End Try


        Dim elementoXML As XElement
        Dim Xns As XNamespace = XNamespace.Get("http://www.asmred.com/")
        elementoXML = XMLdeEntrada.Descendants(Xns + "GrabaServiciosResult").First().FirstNode
        XMLdeEntrada = New XDocument
        XMLdeEntrada.AddFirst(elementoXML)

        If elementoXML.Element("Envio").Element("Resultado").Attribute("return").Value <> "0" Then
            If elementoXML.Element("Envio").Element("Errores").HasElements Then
                Return elementoXML.Element("Envio").Element("Errores").Element("Error").Value
            Else
                Return calcularMensajeError(elementoXML.Element("Envio").Element("Resultado").Attribute("return").Value)
            End If
        Else
            Return "OK"
        End If
    End Function
    Public Async Sub imprimirEtiqueta(envio As EnviosAgencia) Implements IAgencia.imprimirEtiqueta
        If IsNothing(envio.CodigoBarras) OrElse envio.CodigoBarras.Trim = "" Then
            Throw New Exception("El envío debe tener un código de barras asignada para poder imprimir la etiqueta")
        End If

        Dim mainViewModel As New MainViewModel
        Dim puerto As String = Await mainViewModel.leerParametro(envio.Empresa, "ImpresoraBolsas")

        Dim objFSO
        Dim objStream
        objFSO = CreateObject("Scripting.FileSystemObject")
        objStream = objFSO.CreateTextFile(puerto) 'Puerto al cual se envía la impresión  
        Dim i As Integer



        Try
            For i = 1 To envio.Bultos
                objStream.Writeline("I8,A,034")
                objStream.Writeline("N")
                objStream.Writeline("A40,10,0,4,1,1,N,""" + envio.Nombre + """")
                objStream.Writeline("A40,50,0,4,1,1,N,""" + envio.Direccion + """")
                objStream.Writeline("A40,90,0,4,1,1,N,""" + envio.CodPostal + " " + envio.Poblacion + """")
                objStream.Writeline("A40,130,0,4,1,1,N,""" + envio.Provincia + """")
                objStream.Writeline("A40,170,0,4,1,1,N,""Bulto: " + i.ToString + "/" + envio.Bultos.ToString _
                                    + ". Cliente: " + envio.Cliente.Trim + ". Fecha: " + envio.Fecha + """")
                objStream.Writeline("B40,210,0,2C,4,8,200,B,""" + envio.CodigoBarras + i.ToString("D3") + """")
                objStream.Writeline("A40,450,0,4,1,2,N,""" + envio.Nemonico + " " + envio.NombrePlaza + """")
                objStream.Writeline("A40,510,0,4,1,2,N,""" + ListaHorarios.Where(Function(x) x.id = envio.Horario).FirstOrDefault.descripcion + """")
                objStream.Writeline("A590,265,0,5,2,2,N,""" + envio.Nemonico + """")
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
            NotificationRequest.Raise(New Notification() With {
                    .Title = "¡Error! Se ha producido un error y no se han grabado los datos",
                .Content = ex.InnerException.Message
            })
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


    Private Function rellenarPaises() As ObservableCollection(Of Pais)
        Return New ObservableCollection(Of Pais) From {
            New Pais(34, "ESPAÑA"),
            New Pais(351, "PORTUGAL"),
            New Pais(49, "ALEMANIA"),
            New Pais(966, "ARABIA SAUDITA"),
            New Pais(213, "ARGELIA"),
            New Pais(54, "ARGENTINA"),
            New Pais(61, "AUSTRALIA"),
            New Pais(43, "AUSTRIA"),
            New Pais(32, "BELGICA"),
            New Pais(591, "BOLIVIA"),
            New Pais(387, "BOSNIA-HEZERGOVINA"),
            New Pais(55, "BRASIL"),
            New Pais(359, "BULGARIA"),
            New Pais(11, "CANADA"),
            New Pais(57, "COLOMBIA"),
            New Pais(82, "COREA DEL SUR"),
            New Pais(506, "COSTA RICA"),
            New Pais(42, "REPUBLICA CHECA"),
            New Pais(56, "CHILE"),
            New Pais(86, "CHINA"),
            New Pais(385, "CROACIA"),
            New Pais(45, "DINAMARCA"),
            New Pais(593, "ECUADOR"),
            New Pais(20, "EGIPTO"),
            New Pais(503, "EL SALVADOR"),
            New Pais(421, "REPUBLICA ESLOVACA"),
            New Pais(386, "ESLOVENIA"),
            New Pais(1, "ESTADOS UNIDOS"),
            New Pais(63, "FILIPINAS"),
            New Pais(358, "FINLANDIA"),
            New Pais(33, "FRANCIA"),
            New Pais(30, "GRECIA"),
            New Pais(502, "GUATEMALA"),
            New Pais(504, "HONDURAS"),
            New Pais(852, "HONG KONG"),
            New Pais(36, "HUNGRIA"),
            New Pais(91, "INDIA"),
            New Pais(353, "IRLANDA"),
            New Pais(354, "ISLANDIA"),
            New Pais(972, "ISRAEL"),
            New Pais(39, "ITALIA"),
            New Pais(81, "JAPON"),
            New Pais(41, "LIECHTENSTEIN"),
            New Pais(352, "LUXEMBURGO"),
            New Pais(389, "MACEDONIA"),
            New Pais(212, "MARRUECOS"),
            New Pais(52, "MEJICO"),
            New Pais(331, "MONACO"),
            New Pais(505, "NICARAGUA"),
            New Pais(47, "NORUEGA"),
            New Pais(64, "NUEVA ZELANDA"),
            New Pais(31, "HOLANDA"),
            New Pais(507, "PANAMA"),
            New Pais(595, "PARAGUAY"),
            New Pais(51, "PERU"),
            New Pais(48, "POLONIA"),
            New Pais(1809, "PUERTO RICO"),
            New Pais(44, "REINO UNIDO"),
            New Pais(391, "SAN MARINO"),
            New Pais(46, "SUECIA"),
            New Pais(411, "SUIZA"),
            New Pais(886, "TAIWAN"),
            New Pais(66, "THAILANDIA"),
            New Pais(216, "TUNEZ"),
            New Pais(90, "TURQUIA"),
            New Pais(7, "RUSIA"),
            New Pais(598, "URUGUAY"),
            New Pais(396, "VATICANO"),
            New Pais(58, "VENEZUELA"),
            New Pais(381, "YUGOSLAVIA"),
            New Pais(441, "GIBRALTAR"),
            New Pais(360, "ESTONIA"),
            New Pais(40, "RUMANIA"),
            New Pais(234, "NIGERIA"),
            New Pais(380, "UCRANIA"),
            New Pais(833, "VIETNAM"),
            New Pais(53, "CUBA"),
            New Pais(1242, "BAHAMAS"),
            New Pais(691, "MICRONESIA"),
            New Pais(301, "CHIPRE"),
            New Pais(18091, "REPUBLICA DOMINICANA"),
            New Pais(2341, "CABO VERDE"),
            New Pais(911, "SRI LANKA"),
            New Pais(1243, "BERMUDA"),
            New Pais(811, "SINGAPUR"),
            New Pais(9918, "MADEIRA/AZORES"),
            New Pais(692, "INDONESIA"),
            New Pais(693, "ANTILLA HOLANDESA"),
            New Pais(2342, "IVORY COAST"),
            New Pais(77, "LITUANIA"),
            New Pais(2344, "MOZAMBIQUE"),
            New Pais(8111, "MALASIA"),
            New Pais(9666, "LIBANO"),
            New Pais(233, "SUDAFRICA"),
            New Pais(963, "SIRIA"),
            New Pais(2343, "SENEGAL"),
            New Pais(78, "LETONIA(LATVIA)"),
            New Pais(443, "MALTA"),
            New Pais(2345, "KENYA"),
            New Pais(2346, "LIBIA"),
            New Pais(2347, "ANGOLA"),
            New Pais(241, "REPUBLICA GABONESA"),
            New Pais(9633, "JORDANIA")
        }
    End Function

    Private Function IAgencia_EnlaceSeguimiento(envio As EnviosAgencia) As String Implements IAgencia.EnlaceSeguimiento
        Return "http://m.gls-spain.es/e/" + envio.CodigoBarras + "/" + envio.CodPostal
    End Function

    Public ReadOnly Property ListaPaises As ObservableCollection(Of Pais) Implements IAgencia.ListaPaises
    Public ReadOnly Property ListaTiposRetorno As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaTiposRetorno
    Public ReadOnly Property ListaServicios As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaServicios
    Public ReadOnly Property ListaHorarios As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaHorarios

    Public ReadOnly Property ServicioDefecto As Integer Implements IAgencia.ServicioDefecto
        Get
            Return 1 ' Courier
        End Get
    End Property

    Public ReadOnly Property HorarioDefecto As Integer Implements IAgencia.HorarioDefecto
        Get
            Return 3 ' ASM24
        End Get
    End Property



    Private _XMLdeSalida As XDocument
    Public Property XMLdeSalida As XDocument
        Get
            Return _XMLdeSalida
        End Get
        Set(value As XDocument)
            _XMLdeSalida = value
        End Set
    End Property

    Private _XMLdeEntrada As XDocument
    Public Property XMLdeEntrada As XDocument
        Get
            Return _XMLdeEntrada
        End Get
        Set(value As XDocument)
            _XMLdeEntrada = value
        End Set
    End Property

    Public ReadOnly Property ServicioAuxiliar As Integer Implements IAgencia.ServicioAuxiliar
        Get
            Return Integer.MaxValue ' no existe servicio auxiliar
        End Get
    End Property

    Public ReadOnly Property ServicioCreaEtiquetaRetorno As Integer Implements IAgencia.ServicioCreaEtiquetaRetorno
        Get
            Return Integer.MaxValue ' ningún servicio imprime etiqueta de retorno
        End Get
    End Property
End Class
