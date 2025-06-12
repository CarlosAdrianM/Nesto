Imports System.Collections.ObjectModel
Imports System.IO
Imports System.Net
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Windows
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Shared
Imports Nesto.Infrastructure.Shared.FuncionesAuxiliares
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models

Public Class AgenciaASM
    Implements IAgencia

    Private Const EMPRESA_ESPEJO As String = "3  "
    Private Const IDENTIFICADOR_BUSINESSPARCEL As String = "6fb665f2-15a2-4478-9804-c1556fc1f272"
    Private Const PREFIJOCODIGOBARRAS_BUSINESSPARCEL As String = "6119714"


    Private ReadOnly agenciaVM As AgenciasViewModel

    Public Sub New(agencia As AgenciasViewModel)
        If Not IsNothing(agencia) Then
            ListaTiposRetorno = New ObservableCollection(Of tipoIdDescripcion) From {
                New tipoIdDescripcion(0, "Sin Retorno"),
                New tipoIdDescripcion(1, "Con Retorno"),
                New tipoIdDescripcion(2, "Retorno Opcional")
            }
            ListaServicios = New ObservableCollection(Of ITarifaAgencia) From {
                New TarifaGLSBaleares(),
                New TarifaGLSBusinessParcel()
            }
            'New TarifaGLSBusinessParcel(),

            'ListaServicios = New ObservableCollection(Of tipoIdDescripcion) From {
            '    New tipoIdDescripcion(1, "Courier"),
            '    New tipoIdDescripcion(37, "Economy"),
            '    New tipoIdDescripcion(54, "EuroEstándar"),
            '    New tipoIdDescripcion(74, "EuroBusiness Parcel"),
            '    New tipoIdDescripcion(76, "EuroBusiness Small Parcel"),
            '    New tipoIdDescripcion(6, "Carga")
            '}
            ListaHorarios = New ObservableCollection(Of tipoIdDescripcion) From {
                New tipoIdDescripcion(10, "Marítimo"),
                New tipoIdDescripcion(18, "Economy")
            }
            'New tipoIdDescripcion(3, "ASM24"),
            'New tipoIdDescripcion(2, "ASM14"),


            ListaPaises = rellenarPaises()

            agenciaVM = agencia
        End If



    End Sub

    ' Funciones
    Public Function cargarEstado(envio As EnviosAgencia) As XDocument Implements IAgencia.cargarEstado
        If IsNothing(envio) Then
            agenciaVM._dialogService.ShowError("No hay ningún envío seleccionado, no se puede cargar el estado")
            Return Nothing
        End If
        Identificador = If(envio.Servicio = 96, IDENTIFICADOR_BUSINESSPARCEL, envio.AgenciasTransporte.Identificador)
        Dim myUri As New Uri("https://www.asmred.com/WebSrvs/MiraEnvios.asmx/GetExpCli?codigo=" + envio.CodigoBarras + "&uid=" + Identificador)
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
                tracking = New tracking With {
                    .estadoTracking = track.Descendants("evento").FirstOrDefault.Value,
                    .fechaTracking = track.Descendants("fecha").FirstOrDefault.Value
                }
                expedicion.listaTracking.Add(tracking)
                tracking = Nothing
            Next

            digitalizacionesxml = nodo.Descendants("digitalizaciones").FirstOrDefault
            For Each dig In digitalizacionesxml.Descendants("digitalizacion")
                digitalizacion = New digitalizacion With {
                    .tipo = dig.Descendants("tipo").FirstOrDefault.Value,
                    .urlDigitalizacion = New Uri(dig.Descendants("imagen").FirstOrDefault.Value)
                }
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
        PrefijoCodigoBarras = If(agenciaVM.envioActual.Servicio = 96,
            PREFIJOCODIGOBARRAS_BUSINESSPARCEL,
            agenciaVM.agenciaSeleccionada.PrefijoCodigoBarras.ToString)
        Return PrefijoCodigoBarras + agenciaVM.envioActual.Numero.ToString("D7")
    End Function
    Public Sub calcularPlaza(ByVal codPostal As String, ByRef nemonico As String, ByRef nombrePlaza As String, ByRef telefonoPlaza As String, ByRef emailPlaza As String) Implements IAgencia.calcularPlaza
        'Comenzamos la llamada
        Dim soap As String = "<?xml version=""1.0"" encoding=""utf-8""?>" &
             "<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" " &
              "xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" " &
              "xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">" &
              "<soap:Body>" &
                    "<GetPlazaXCP xmlns=""http://www.asmred.com/"">" &
                        ("<codPais>" + agenciaVM.paisActual.Id.ToString + "</codPais>") &
                        ("<cp>" + codPostal + "</cp>") &
                    "</GetPlazaXCP>" &
                "</soap:Body>" &
             "</soap:Envelope>"

        Dim req As HttpWebRequest = WebRequest.Create("https://www.asmred.com/WebSrvs/b2b.asmx?op=GetPlazaXCP")
        req.Headers.Add("SOAPAction", """http://www.asmred.com/GetPlazaXCP""")
        req.ContentType = "text/xml; charset=""utf-8"""
        req.Accept = "text/xml"
        req.Method = "POST"

        Using stm As Stream = req.GetRequestStream()
            Using stmw As New StreamWriter(stm)
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

        Identificador = If(envio.Servicio = 96, IDENTIFICADOR_BUSINESSPARCEL, envio.AgenciasTransporte.Identificador)

        'Añadimos el nodo raíz (Servicios)
        xml.AddFirst(
            <Servicios uidcliente=<%= Identificador %> xmlns="http://www.asmred.com/">
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
                        <CP><%= If(empresa.Número = EMPRESA_ESPEJO, "28119", empresa.CodPostal.Trim) %></CP>
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
    Public Async Function LlamadaWebService(envio As EnviosAgencia, servicio As IAgenciaService) As Task(Of RespuestaAgencia) Implements IAgencia.LlamadaWebService
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

        Dim req As HttpWebRequest = WebRequest.Create("https://www.asmred.com/WebSrvs/b2b.asmx?op=GrabaServicios")
        req.Headers.Add("SOAPAction", """http://www.asmred.com/GrabaServicios""")
        req.ContentType = "text/xml; charset=""utf-8"""
        req.Accept = "text/xml"
        req.Method = "POST"

        Dim respuesta As New RespuestaAgencia With {
            .Agencia = "ASM",
            .Fecha = Date.Now,
            .CuerpoLlamada = soap,
            .UrlLlamada = req.Address.ToString
        }

        Try
            Using stm As Stream = Await req.GetRequestStreamAsync()
                Using stmw As New StreamWriter(stm)
                    stmw.Write(soap)
                End Using
            End Using

            Dim response As WebResponse = req.GetResponse()
            Dim responseStream As New StreamReader(response.GetResponseStream())
            soap = responseStream.ReadToEnd
            XMLdeEntrada = XDocument.Parse(soap)
            respuesta.CuerpoRespuesta = soap
        Catch ex As Exception
            respuesta.Exito = False
            respuesta.TextoRespuestaError = ex.Message
            If IsNothing(respuesta.CuerpoRespuesta) Then
                respuesta.CuerpoRespuesta = String.Empty
            End If
            Return respuesta
        End Try


        Dim elementoXML As XElement
        Dim Xns As XNamespace = XNamespace.Get("http://www.asmred.com/")
        elementoXML = XMLdeEntrada.Descendants(Xns + "GrabaServiciosResult").First().FirstNode
        XMLdeEntrada = New XDocument
        XMLdeEntrada.AddFirst(elementoXML)

        If elementoXML.Element("Envio").Element("Resultado").Attribute("return").Value <> "0" Then
            respuesta.Exito = False
            If elementoXML.Element("Envio").Element("Errores").HasElements Then
                respuesta.TextoRespuestaError = elementoXML.Element("Envio").Element("Errores").Element("Error").Value
                Return respuesta
            Else
                respuesta.TextoRespuestaError = calcularMensajeError(elementoXML.Element("Envio").Element("Resultado").Attribute("return").Value)
                Return respuesta
            End If
        Else
            respuesta.Exito = True
            respuesta.TextoRespuestaError = "OK"
            Return respuesta
        End If
    End Function
    Public Async Sub imprimirEtiqueta(envio As EnviosAgencia) Implements IAgencia.imprimirEtiqueta
        If IsNothing(envio.CodigoBarras) OrElse envio.CodigoBarras.Trim = "" Then
            Throw New Exception("El envío debe tener un código de barras asignada para poder imprimir la etiqueta")
        End If

        Dim mainViewModel As New MainViewModel
        Dim puerto As String = Await mainViewModel.leerParametro(envio.Empresa, Parametros.Claves.ImpresoraAgenciaGLS)

        'Dim objFSO
        'objFSO = CreateObject("Scripting.FileSystemObject")
        'objStream = objFSO.CreateTextFile(puerto) 'Puerto al cual se envía la impresión  

        Dim i As Integer

        Try
            Dim builder As New StringBuilder

            Dim nombreRemitente As String = "NUEVA VISIÓN, S.A."
            Dim direccionRemitente As String = "c/ Río Tiétar, 11"
            Dim codPostalRemitente As String = "28119"
            Dim poblacionRemitente As String = "Algete"
            Dim provinciaRemitente As String = "Madrid"
            Dim codigoRemitente As String = "791/1664"
            Dim codigoTSP = Await GetTSPAsync("34", "28119", envio.Pais, envio.CodPostal, envio.Servicio, envio.Horario)
            Dim inicioCodBarras = Truncar(envio.CodigoBarras, 14)
            calcularPlaza(envio.CodPostal, envio.Nemonico, envio.NombrePlaza, envio.TelefonoPlaza, envio.EmailPlaza)

            For i = 1 To envio.Bultos
                'builder.AppendLine("I8,A,034")
                'builder.AppendLine("N")

                'builder.AppendLine("A40,10,0,4,1,1,N,""" + envio.Nombre + """")
                'builder.AppendLine("A40,50,0,4,1,1,N,""" + envio.Direccion + """")
                'builder.AppendLine("A40,90,0,4,1,1,N,""" + envio.CodPostal + " " + envio.Poblacion + """")
                'builder.AppendLine("A40,130,0,4,1,1,N,""" + envio.Provincia + """")
                'builder.AppendLine("A40,170,0,4,1,1,N,""Bulto: " + i.ToString + "/" + envio.Bultos.ToString _
                '                    + ". Cliente: " + envio.Cliente.Trim + ". Fecha: " + envio.Fecha + """")
                'builder.AppendLine("A40,210,0,4,1,1,N,""Pedido: " + envio.Pedido.ToString + """")
                'builder.AppendLine("B40,250,0,2C,4,8,200,B,""" + envio.CodigoBarras + i.ToString("D3") + """")
                'builder.AppendLine("A40,490,0,4,1,2,N,""" + envio.Nemonico + " " + envio.NombrePlaza + """")
                'builder.AppendLine("A40,550,0,4,1,2,N,""" + ListaHorarios.Where(Function(x) x.id = envio.Horario).FirstOrDefault.descripcion + """")
                'builder.AppendLine("A590,305,0,5,2,2,N,""" + envio.Nemonico + """")
                'builder.AppendLine("P1")
                'builder.AppendLine("")                
                Dim unused42 = builder.AppendLine("^XA")
                Dim unused41 = builder.AppendLine("^CI27")  ' Configura la impresora para aceptar caracteres especiales en UTF-8
                Dim unused40 = builder.AppendLine("^BY3")
                Dim unused39 = builder.AppendLine("^LL900")
                Dim unused38 = builder.AppendLine("^FO10,50^AUN,50,50^FDGLS^FS")
                Dim unused37 = builder.AppendLine("^FO200,50^AUN,50,50^FD" & inicioCodBarras & "^FS") ' 14 primeros dígitos del código de barras
                Dim unused36 = builder.AppendLine("^FO200,130^GB775,0,2,B,3^FS")
                Dim unused35 = builder.AppendLine("^FO170,125^AQB,1,1^FDDESTINATARIO^FS")
                Dim unused34 = builder.AppendLine("^FO200,130^ARN,1,1^FD" & envio.Nombre & "^FS")
                Dim unused33 = builder.AppendLine("^FO200,160^ARN,1,1^FD" & envio.Direccion & "^FS")
                Dim unused32 = builder.AppendLine("^FO200,190^ARN,1,1^FD" & envio.CodPostal & ". " & envio.Poblacion & "^FS")
                Dim unused31 = builder.AppendLine("^FO200,220^ARN,1,1^FD" & envio.Provincia & "^FS")
                Dim unused30 = builder.AppendLine("^FO600,220^ARN,1,1^FD" & envio.Telefono & "^FS")
                Dim unused29 = builder.AppendLine("^FO10,160^ARN,1,1^FD" & envio.Fecha.ToString("dd/MM/yyyy") & "^FS")
                Dim unused28 = builder.AppendLine("^FO90,230^AUN,50,50^FD" & codigoTSP & "^FS")  ' Centramos el TSP entre la fecha y el código de barras, ajustando la posición horizontal
                Dim unused27 = builder.AppendLine("^FO300,260^GB775,0,2,B,3^FS")
                Dim unused26 = builder.AppendLine("^FO270,265^AQB,1,1^FDREMITENTE^FS")
                Dim unused25 = builder.AppendLine("^FO300,270^APN,1,1^FD" & nombreRemitente & "^FS")
                Dim unused24 = builder.AppendLine("^FO300,290^APN,1,1^FD" & direccionRemitente & "^FS")
                Dim unused23 = builder.AppendLine("^FO300,310^APN,1,1^FD" & codPostalRemitente & ". " & poblacionRemitente & "^FS")
                Dim unused22 = builder.AppendLine("^FO300,330^APN,1,1^FD" & provinciaRemitente & "^FS")
                Dim unused21 = builder.AppendLine("^FO600,350^APN,1,1^FD(" & codigoRemitente & ")^FS")
                Dim unused20 = builder.AppendLine("^FO300,380^GB775,0,2,B,3^FS")
                Dim unused19 = builder.AppendLine("^FO270,390^AQB,1,1^FDOBSERV^FS")
                Dim unused18 = builder.AppendLine("^FO300,390^APN,1,1^FD" & envio.Observaciones & "^FS")
                Dim unused17 = builder.AppendLine("^FO300,410^APN,1,1^FD-----^FS")
                Dim unused16 = builder.AppendLine("^FO300,430^APN,1,1^FD-----^FS")
                Dim unused15 = builder.AppendLine("^FO300,450^APN,1,1^FD-----^FS")
                Dim unused14 = builder.AppendLine("^FO300,470^GB775,0,2,B,3^FS")
                Dim unused13 = builder.AppendLine("^FO300,475^ATN,10,10^FDCOURIER^FS")
                Dim unused12 = builder.AppendLine("^FO300,510^ATN,10,10^FD" & ListaServicios.Single(Function(x) x.ServicioId = envio.Servicio).NombreServicio & " (" & envio.Horario.ToString & ")^FS")
                Dim unused11 = builder.AppendLine("^FO300,530^AVN,200,200^FD" & envio.Nemonico & "^FS")
                Dim unused10 = builder.AppendLine("^FO300,720^AUN,50,50^FD" & envio.NombrePlaza & "^FS")
                Dim unused9 = builder.AppendLine("^FO300,770^AUN,50,50^FD" & envio.CodPostal & "^FS")
                Dim unused8 = builder.AppendLine("^FO600,770^AUN,50,50^FD" & i.ToString & "/" & envio.Bultos.ToString() & "^FS")
                Dim unused7 = builder.AppendLine("^FO300,820^GB775,0,2,B,3^FS")
                Dim unused6 = builder.AppendLine("^FO10,300^B2B,200,Y,N,Y^FD" & envio.CodigoBarras & i.ToString("D3") & "^FS")
                Dim unused5 = builder.AppendLine("^FO10,860^ADN,1,1^FDAlb.Cli.^FS")
                Dim unused4 = builder.AppendLine("^FO110,850^ASN,1,1^FD" & envio.Cliente.Trim & envio.Pedido.ToString & "^FS")
                Dim unused3 = builder.AppendLine("^FO10,900^ADN,1,1^FDRef.Cli.^FS")
                Dim unused2 = builder.AppendLine("^FO110,890^ASN,1,1^FD" & envio.Cliente.Trim & "/" & envio.Pedido.ToString & "^FS")
                Dim unused1 = builder.AppendLine("^XZ")
            Next

            Dim unused = RawPrinterHelper.SendStringToPrinter(puerto, builder.ToString)
        Catch ex As Exception
            agenciaVM._dialogService.ShowError("Se ha producido un error y no se han grabado los datos:" + vbCr + ex.ToString)
        End Try
    End Sub
    Public ReadOnly Property visibilidadSoloImprimir As Visibility Implements IAgencia.visibilidadSoloImprimir
        Get
            Return Visibility.Hidden
        End Get
    End Property
    Public ReadOnly Property retornoSoloCobros As Byte Implements IAgencia.retornoSoloCobros
        Get
            Return 0 ' Sin retorno
        End Get
    End Property
    Public ReadOnly Property servicioSoloCobros As Byte Implements IAgencia.servicioSoloCobros
        Get
            Return 1 ' Courier
        End Get
    End Property
    Public ReadOnly Property horarioSoloCobros As Byte Implements IAgencia.horarioSoloCobros
        Get
            Return 3 ' ASM24
        End Get
    End Property
    Public ReadOnly Property retornoSinRetorno As Byte Implements IAgencia.retornoSinRetorno
        Get
            Return 0 ' Sin Retorno
        End Get
    End Property
    Public ReadOnly Property paisDefecto As Integer Implements IAgencia.paisDefecto
        Get
            Return 34
        End Get
    End Property
    Public ReadOnly Property retornoObligatorio As Byte Implements IAgencia.retornoObligatorio
        Get
            Return 1 ' Retorno obligatorio
        End Get
    End Property


    Private Function rellenarPaises() As ObservableCollection(Of Pais)
        Return New ObservableCollection(Of Pais) From {
            New Pais(34, "ESPAÑA", "ES"),
            New Pais(351, "PORTUGAL", "PT"),
            New Pais(49, "ALEMANIA", "DE"),
            New Pais(966, "ARABIA SAUDITA", "SA"),
            New Pais(213, "ARGELIA", "DZ"),
            New Pais(54, "ARGENTINA", "AR"),
            New Pais(61, "AUSTRALIA", "AU"),
            New Pais(43, "AUSTRIA", "AT"),
            New Pais(32, "BELGICA", "BE"),
            New Pais(591, "BOLIVIA", "BO"),
            New Pais(387, "BOSNIA-HEZERGOVINA", "BA"),
            New Pais(55, "BRASIL", "BR"),
            New Pais(359, "BULGARIA", "BG"),
            New Pais(11, "CANADA", "CA"),
            New Pais(57, "COLOMBIA", "CO"),
            New Pais(82, "COREA DEL SUR"),
            New Pais(506, "COSTA RICA"),
            New Pais(42, "REPUBLICA CHECA"),
            New Pais(56, "CHILE"),
            New Pais(86, "CHINA"),
            New Pais(385, "CROACIA", "HR"),
            New Pais(45, "DINAMARCA", "DK"),
            New Pais(593, "ECUADOR"),
            New Pais(20, "EGIPTO"),
            New Pais(503, "EL SALVADOR"),
            New Pais(421, "REPUBLICA ESLOVACA", "SK"),
            New Pais(386, "ESLOVENIA", "SI"),
            New Pais(1, "ESTADOS UNIDOS", "US"),
            New Pais(63, "FILIPINAS"),
            New Pais(358, "FINLANDIA", "FI"),
            New Pais(33, "FRANCIA", "FR"),
            New Pais(30, "GRECIA", "GR"),
            New Pais(502, "GUATEMALA"),
            New Pais(504, "HONDURAS"),
            New Pais(852, "HONG KONG"),
            New Pais(36, "HUNGRIA", "HU"),
            New Pais(91, "INDIA"),
            New Pais(353, "IRLANDA", "IE"),
            New Pais(354, "ISLANDIA", "IS"),
            New Pais(972, "ISRAEL"),
            New Pais(39, "ITALIA", "IT"),
            New Pais(81, "JAPON"),
            New Pais(41, "LIECHTENSTEIN"),
            New Pais(352, "LUXEMBURGO", "LU"),
            New Pais(389, "MACEDONIA"),
            New Pais(212, "MARRUECOS", "MA"),
            New Pais(52, "MEJICO"),
            New Pais(331, "MONACO", "MC"),
            New Pais(505, "NICARAGUA"),
            New Pais(47, "NORUEGA", "NO"),
            New Pais(64, "NUEVA ZELANDA"),
            New Pais(31, "HOLANDA", "NL"),
            New Pais(507, "PANAMA"),
            New Pais(595, "PARAGUAY"),
            New Pais(51, "PERU"),
            New Pais(48, "POLONIA", "PL"),
            New Pais(1809, "PUERTO RICO"),
            New Pais(44, "REINO UNIDO", "GB"),
            New Pais(391, "SAN MARINO"),
            New Pais(46, "SUECIA", "SE"),
            New Pais(411, "SUIZA", "SH"),
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
            New Pais(40, "RUMANIA", "RO"),
            New Pais(234, "NIGERIA"),
            New Pais(380, "UCRANIA", "UA"),
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
        Return "https://mygls.gls-spain.es/e/" + envio.CodigoBarras + "/" + envio.CodPostal
    End Function

    Public Function RespuestaYaTramitada(respuesta As String) As Boolean Implements IAgencia.RespuestaYaTramitada
        Return False
    End Function

    Public ReadOnly Property ListaPaises As ObservableCollection(Of Pais) Implements IAgencia.ListaPaises
    Public ReadOnly Property ListaTiposRetorno As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaTiposRetorno
    Public ReadOnly Property ListaServicios As ObservableCollection(Of ITarifaAgencia) Implements IAgencia.ListaServicios
    Public ReadOnly Property ListaHorarios As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaHorarios

    Public Property Identificador As String
    Public Property PrefijoCodigoBarras As String


    Public ReadOnly Property ServicioDefecto As Byte Implements IAgencia.ServicioDefecto
        Get
            Return 96 ' BusinessParcel
        End Get
    End Property

    Public ReadOnly Property HorarioDefecto As Byte Implements IAgencia.HorarioDefecto
        Get
            Return 18 ' Economy
        End Get
    End Property

    Public Property XMLdeSalida As XDocument

    Public Property XMLdeEntrada As XDocument

    Public ReadOnly Property ServicioAuxiliar As Byte Implements IAgencia.ServicioAuxiliar
        Get
            Return Byte.MaxValue ' no existe servicio auxiliar
        End Get
    End Property

    Public ReadOnly Property ServicioCreaEtiquetaRetorno As Byte Implements IAgencia.ServicioCreaEtiquetaRetorno
        Get
            Return Byte.MaxValue ' ningún servicio imprime etiqueta de retorno
        End Get
    End Property

    Public ReadOnly Property NumeroCliente As String Implements IAgencia.NumeroCliente
        Get
            Return "20244"
        End Get
    End Property

    Public Async Function GetTSPAsync(codPaisOrg As String, cpOrg As String, codPaisDst As String, cpDst As String, codServicio As String, codHorario As String) As Task(Of String)
        Dim soapEnvelope As String = "<?xml version=""1.0"" encoding=""utf-8""?>" &
                                 "<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" " &
                                 "xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" " &
                                 "xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">" &
                                 "<soap:Body>" &
                                 "<GetTSP xmlns=""http://www.asmred.com/"">" &
                                 "<Request>" &
                                 $"<CodPaisOrg>{codPaisOrg}</CodPaisOrg>" &
                                 $"<CPOrg>{cpOrg}</CPOrg>" &
                                 $"<CodPaisDst>{codPaisDst}</CodPaisDst>" &
                                 $"<CPDst>{cpDst}</CPDst>" &
                                 $"<CodServicio>{codServicio}</CodServicio>" &
                                 $"<CodHorario>{codHorario}</CodHorario>" &
                                 "</Request>" &
                                 "</GetTSP>" &
                                 "</soap:Body>" &
                                 "</soap:Envelope>"

        Dim req As HttpWebRequest = CType(WebRequest.Create("https://wsclientes.asmred.com/b2b.asmx?op=GetTSP"), HttpWebRequest)
        req.Headers.Add("SOAPAction", """http://www.asmred.com/GetTSP""")
        req.ContentType = "text/xml; charset=utf-8"
        req.Method = "POST"

        Using stm As Stream = Await req.GetRequestStreamAsync()
            Using stmw As New StreamWriter(stm)
                Await stmw.WriteAsync(soapEnvelope)
            End Using
        End Using

        Try
            Using response As WebResponse = Await req.GetResponseAsync()
                Using responseStream As New StreamReader(response.GetResponseStream())
                    Dim responseXml As String = Await responseStream.ReadToEndAsync()
                    Return ParseCodTSP(responseXml)
                End Using
            End Using
        Catch ex As WebException
            Dim errorResponse As String = String.Empty
            Using response As WebResponse = ex.Response
                Using responseStream As New StreamReader(response.GetResponseStream())
                    errorResponse = "Error" 'Await responseStream.ReadToEndAsync()
                End Using
            End Using
            Throw New Exception("Error en la llamada al servicio: " & errorResponse)
        End Try
    End Function


    Private Function ParseCodTSP(responseXml As String) As String
        Dim xdoc As XDocument = XDocument.Parse(responseXml)
        Dim Xns As XNamespace = "http://www.asmred.com/"
        Dim codTSP As String = xdoc.Descendants(Xns + "CodTSP").FirstOrDefault()?.Value
        Return codTSP
    End Function

End Class
