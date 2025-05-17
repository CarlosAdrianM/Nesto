Imports System.Collections.ObjectModel
Imports System.Windows
Imports System.Net
Imports System.IO
Imports Nesto.Models.Nesto.Models
Imports System.Globalization
Imports System.Text
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models

Public Class AgenciaSending
    Implements IAgencia

    Private Const EMPRESA_ESPEJO As String = "3  "
    Private Const DELEGACION_ORIGEN As String = "280"

    Public Sub New()
        ListaTiposRetorno = New ObservableCollection(Of tipoIdDescripcion) From {
            New tipoIdDescripcion(0, "Sin Retorno"),
            New tipoIdDescripcion(1, "Con Retorno")
        }
        ListaServicios = New ObservableCollection(Of ITarifaAgencia) From {
            New TarifaSendingExpress(),
            New TarifaSendingMaritimo()
        }
        ListaHorarios = New ObservableCollection(Of tipoIdDescripcion) From {
            New tipoIdDescripcion(1, "Normal")
        }

        ListaPaises = rellenarPaises()
    End Sub

    Public ReadOnly Property NumeroCliente As String Implements IAgencia.NumeroCliente
        Get
            Return "41094"
        End Get
    End Property

    ' Funciones
    Public Function cargarEstado(envio As EnviosAgencia) As XDocument Implements IAgencia.cargarEstado
        If IsNothing(envio) Then
            Throw New Exception("No hay ningún envío seleccionado, no se puede cargar el estado")
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
        'agenciaVM.digitalizacionActual = estado.listaDigitalizaciones.LastOrDefault
        estado.listaExpediciones.Add(expedicion)
        'agenciaVM.cmdDescargarImagen.RaiseCanExecuteChanged()
        Return estado
    End Function
    Public Function calcularCodigoBarras(agenciaVM As AgenciasViewModel) As String Implements IAgencia.calcularCodigoBarras
        Return agenciaVM.agenciaSeleccionada.PrefijoCodigoBarras.ToString + agenciaVM.envioActual.Numero.ToString("D8")
    End Function
    Private Function CalcularCodigoBarrasBulto(envio As EnviosAgencia, codigoZonaRuta As CodigoRutaZona, bulto As Integer) As String
        Dim resultado As String = String.Format("{0}{1}{2}{3}{4}{5}",
                                                envio.CodigoBarras, DELEGACION_ORIGEN, codigoZonaRuta.CodigoDelegacion,
                                                codigoZonaRuta.CodigoZona, envio.Servicio.ToString("D2"), bulto.ToString("D3")
                                                )
        If resultado.Length <> 26 Then
            Throw New Exception("Longitud del código de barras errónea")
        End If
        Return resultado
    End Function
    Public Sub calcularPlaza(ByVal codPostal As String, ByRef nemonico As String, ByRef nombrePlaza As String, ByRef telefonoPlaza As String, ByRef emailPlaza As String) Implements IAgencia.calcularPlaza
        nemonico = "SD"
        nombrePlaza = "Sending"
        telefonoPlaza = ""
        emailPlaza = ""
    End Sub

    Private Function construirXMLdeSalida(envio As EnviosAgencia, servicio As IAgenciaService) As XDocument 'Implements IAgencia.construirXMLdeSalida
        Dim empresa = servicio.CargarListaEmpresas().Single(Function(e) e.Número = envio.Empresa)
        Dim pais = ListaPaises.Where(Function(p) p.Id = envio.Pais).Single()
        Dim xml As New XDocument

        ' Si no hay envioActual devolvemos el xml vacío
        If IsNothing(envio) Then
            Return xml
        End If

        Dim envioTramitar As String = IIf(String.IsNullOrWhiteSpace(envio.Movil), envio.Telefono, envio.Movil)

        'Añadimos el nodo raíz (Servicios)
        xml.AddFirst(
        <Expediciones>
            <Expedicion>
                <NumeroEnvio><%= envio.AgenciasTransporte.PrefijoCodigoBarras + envio.Numero.ToString("D8") %></NumeroEnvio>
                <Fecha><%= envio.Fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) %></Fecha>
                <ClienteRemitente><%= envio.AgenciasTransporte.Identificador %>-01</ClienteRemitente>
                <NombreRemitente>NUEVA VISION, S.A.</NombreRemitente>
                <DireccionRemitente>CALLE RIO TIETAR, 11 - P.I. EL NOGAL</DireccionRemitente>
                <PaisRemitente>034</PaisRemitente>
                <CodigoPostalRemitente>28110</CodigoPostalRemitente>
                <PoblacionRemitente>ALGETE</PoblacionRemitente>
                <NombreDestinatario><%= envio.Nombre.Normalize %></NombreDestinatario>
                <DireccionDestinatario><%= envio.Direccion %></DireccionDestinatario>
                <PaisDestinatario><%= pais.CodigoAlfa %></PaisDestinatario>
                <CodigoPostalDestinatario><%= envio.CodPostal %></CodigoPostalDestinatario>
                <PoblacionDestinatario><%= envio.Poblacion %></PoblacionDestinatario>
                <PersonaContactoDestinatario><%= envio.Atencion %></PersonaContactoDestinatario>
                <TelefonoContactoDestinatario><%= envioTramitar %></TelefonoContactoDestinatario>
                <EnviarMail>S</EnviarMail>
                <MailDestinatario><%= envio.Email %></MailDestinatario>
                <ProductoServicio>01</ProductoServicio>
                <Observaciones1><%= envio.Observaciones %></Observaciones1>
                <Kilos>1</Kilos>
                <Volumen>0.00</Volumen>
                <ReferenciaCliente><%= envio.Cliente.Trim %>/<%= envio.Pedido %></ReferenciaCliente>
                <TipoPortes>P</TipoPortes>
                <EntregaSabado>N</EntregaSabado>
                <Retorno><%= IIf(envio.Retorno = 1, "S", "N") %></Retorno>
                <Bultos><%= envio.Bultos %></Bultos>
                <ImporteReembolso><%= Decimal.Round(envio.Reembolso, 2, MidpointRounding.AwayFromZero) %></ImporteReembolso>
                <ImporteValorDeclarado><%= Decimal.Round(envio.ImporteAsegurado, 2, MidpointRounding.AwayFromZero) %></ImporteValorDeclarado>
            </Expedicion>
        </Expediciones>
        )

        Return xml
    End Function
    Public Async Function LlamadaWebService(envio As EnviosAgencia, servicio As IAgenciaService) As Task(Of RespuestaAgencia) Implements IAgencia.LlamadaWebService
        XMLdeSalida = construirXMLdeSalida(envio, servicio)

        'Comenzamos la llamada
        Dim soap As String = "<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:web=""http://webservices.sending.alerce.es/"">" &
            "<soapenv:Header/>" &
            "<soapenv:Body>" &
                "<web:entrada_expediciones>" &
                "<cliente>" & envio.AgenciasTransporte.Identificador & "-01</cliente>" &
                "<fichero><![CDATA[<?xml version=""1.0"" encoding=""ISO-8859-1""?>" & XMLdeSalida.ToString & "]]></fichero>" &
                "<formato>xml</formato>" &
                "<param1>bb42d7bae8744006999576f1adc7ae4f</param1>" &
                "</web:entrada_expediciones>" &
            "</soapenv:Body>" &
        "</soapenv:Envelope>"

        Dim req As HttpWebRequest = WebRequest.Create("https://wssending.alertran.net/sending/ws_clientes?wsdl")
        'Dim req As HttpWebRequest = WebRequest.Create("http://padua.sending.es/sending/ws_clientes?wsdl")
        req.Headers.Add("SOAPAction", """entrada_expediciones""")
        req.ContentType = "text/xml; charset=""ISO-8859-1"""
        req.Accept = "text/xml"
        req.Method = "POST"

        Dim respuesta As New RespuestaAgencia With {
            .Agencia = "Sending",
            .Fecha = DateTime.Now,
            .UrlLlamada = req.Address.ToString,
            .CuerpoLlamada = soap,
            .CuerpoRespuesta = String.Empty
        }

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
            respuesta.CuerpoRespuesta = soap
        Catch ex As Exception
            respuesta.Exito = False
            respuesta.TextoRespuestaError = "El servidor de la agencia no está respondiendo"
            Return respuesta
        End Try


        Dim elementoXML As XElement
        Dim Xns As XNamespace = XNamespace.Get("http://webservices.sending.alerce.es/")
        elementoXML = XMLdeEntrada.Descendants(Xns + "entrada_expedicionesResponse").First().FirstNode
        XMLdeEntrada = New XDocument
        XMLdeEntrada.AddFirst(elementoXML)

        If elementoXML.Value().StartsWith("OK ") OrElse elementoXML.Value().StartsWith("TERMINADO CON ERRORES") Then
            respuesta.Exito = True
            respuesta.TextoRespuestaError = "OK"
        Else
            respuesta.TextoRespuestaError = elementoXML.Value
        End If
        Return respuesta
    End Function

    Public Async Sub imprimirEtiqueta(envio As EnviosAgencia) Implements IAgencia.imprimirEtiqueta
        If String.IsNullOrEmpty(envio.CodigoBarras) Then
            Throw New Exception("El envío debe tener un código de barras asignada para poder imprimir la etiqueta")
        End If

        Dim mainViewModel As New MainViewModel
        Dim puerto As String = Await mainViewModel.leerParametro(envio.Empresa, Parametros.Claves.ImpresoraAgencia)

        'Dim objFSO
        'Dim objStream
        'objFSO = CreateObject("Scripting.FileSystemObject")
        'objStream = objFSO.CreateTextFile(puerto) 'Puerto al cual se envía la impresión  

        Dim i As Integer

        Try
            Dim rutaZona As New CodigoRutaZona(envio.CodPostal, envio.Poblacion)
            Dim builder As New StringBuilder
            For i = 1 To envio.Bultos
                Dim codigoBarrasBulto As String = CalcularCodigoBarrasBulto(envio, rutaZona, i)
                builder.AppendLine("^FX --- FORMATO DE LA ETIQUETA --- ^FS")
                builder.AppendLine("^XA^LRY^FWN^CFD,24^PW839^LH0,0^CI27^PR4^MNY^MTD^MD10^PON^PMN^LRY^XZ")
                builder.AppendLine("^XA^MCY^XZ^XA")
                builder.AppendLine("^DFSENDEXP1^FS")
                builder.AppendLine("")

                ' Comentar este bloque para que no imprima el logo
                builder.AppendLine("^FO12,2^GFA,04448,04448,00032,")
                builder.AppendLine("0000000000000000000000000000000000000000000E,0000000000000000000000000000000000")
                builder.AppendLine("000000000E,0000000000000000000000000000000000000000007E,0000000000000000000000")
                builder.AppendLine("00000000000000000000FF80,000000000000000000000000000000000000000000FF80,000000")
                builder.AppendLine("000000000000000000000000000000000003FF80,00000000000000000000000000000000000000")
                builder.AppendLine("0007FFC0,000000000000000000000000000000000000000007FFC0,0000000000000000000000")
                builder.AppendLine("00000000000000000007FFF0,000000000000000000000000000000000000000003FFF0,000000")
                builder.AppendLine("000000000000000000000000000000000003FFF0,00000000000000000000000000000000000000")
                builder.AppendLine("0000FFC0,0000000000000000000000000000000000000000007FC8,0000000000000000000000")
                builder.AppendLine("000000000000000000007FC8,0000000000000000000000000000000000000000001F88,000000")
                builder.AppendLine("0000000000000000000000000000000000000E38,00000000000000000000000000000000000000")
                builder.AppendLine("00000E38,0000000000000000000000000000000000000000000078,0000000000000000000000")
                builder.AppendLine("000000000000000000000078,0000000000000000000000000000000000000000000078,000000")
                builder.AppendLine("00000000000000000000000000000000000001F8,00000000000000000000000000000000000001")
                builder.AppendLine("800003F8,00000000000000000000000000000000000001800003F8,0000000000000000000000")
                builder.AppendLine("000000000000000FF00003F8,0000000000000000000000000000000000001FF8000FF8,000000")
                builder.AppendLine("0000000000000000000000000000001FF8000FF8,0000000000000000000000000000000000007F")
                builder.AppendLine("FE001FF0,0000000000000000000000000000000000007FFE007FF0,0000000000000000000000")
                builder.AppendLine("000000000000007FFE007FF0,0000000000000000000000000000000000007FFE00FFF0,000000")
                builder.AppendLine("0000000000000000000000000000007FFE00FFF0,0000000000000000000000000000000000007F")
                builder.AppendLine("FE03FFF0,0000000000000000000000000000000000007FFE1FFFF0,0000000000000000000000")
                builder.AppendLine("000000000000007FFE1FFFF0,0000000000000000000000000000000000001FF83FFFC0,000000")
                builder.AppendLine("0000000000000000000000000000000FF0FFFFC0,0000000000000000000000000000000000000F")
                builder.AppendLine("F0FFFFC0,0000000000000000000000000000000000000007FFFFC0,0000000000000000000000")
                builder.AppendLine("00000000000000003FFFFF80,000000000000000000000000000000000000003FFFFF80,000000")
                builder.AppendLine("00000000000000000000000000000001FFFFFF80,0000000000000000000000000000000000000F")
                builder.AppendLine("FFFFFE,0000000000000000000000000000000000000FFFFFFE,00000000000000000000000000")
                builder.AppendLine("00000000007FFFFFFE,00000000000000000000000000000000001FFFFFFFFE,00000000000000")
                builder.AppendLine("000000000000000000001FFFFFFFFE,000000000000000000000000000000003FFFFFFFFFFC,00")
                builder.AppendLine("00000000000000000000000000000FFFFFFFFFFFF0,0000000000000000000000000000000FFFFF")
                builder.AppendLine("FFFFFFF0,00000000000000000000000000000001FFFFFFFFFFF0,000000000000000000000000")
                builder.AppendLine("000000000FFFFFFFFFE0,000000000000000000000000000000000FFFFFFFFFE0,000000000000")
                builder.AppendLine("00000000000000000000003FFFFFFFE0,0000000000000000000000000000000000000FFFFF80,00")
                builder.AppendLine("00000000000000000000000000000000000FFFFF80,000000000000000000000000000000000000")
                builder.AppendLine("03FFFF1C,00000000000000000000000000000000000003FFFF1C,000000000000000000000000")
                builder.AppendLine("00000000000003FFFC7C,0000000000000000000000000000000000000FFFF8FE,000000000000")
                builder.AppendLine("0000000000000000000000000FFFF8FE,0000000000000000000000000000000000000FFFE3FE,00")
                builder.AppendLine("00000000000000000000000000000000001FFFC7FF80,0000000000000000000000000000000000")
                builder.AppendLine("001FFFC7FF80,0000000000000000000000000000000000001FFF07FF80,000000000000000000")
                builder.AppendLine("0000000000001FF8007FFE1FFFC0,0000000000000000000000000000001FF8007FFE1FFFC0,00")
                builder.AppendLine("00000000000000000000000000001FF8007FF8001FC0,0000000000000000000000000000001FF8")
                builder.AppendLine("00FFF00003C0,0000000000000000000000000000001FF800FFF00003C0,000000000000000000")
                builder.AppendLine("0000000000001FF800FF800001F0,0000000000000000000000000000001FF803FE00000030,00")
                builder.AppendLine("00000000000000000000000000001FF803FE00000030,0000000000000000000000000000001FF8")
                builder.AppendLine("07F0,0000000000000000000000000000001FF81F80,0000000000000000000000000000001FF8")
                builder.AppendLine("1F80,0000000000000000000000000000001FF81C,0000000000000000000000000000001FF8,00")
                builder.AppendLine("00000000000000000000000000001FF8,0000000000000000000000000000001FF80060,000000")
                builder.AppendLine("0000000000000000000000001FF803F0,0000000000000000000000000000001FF803F0,03FFE0")
                builder.AppendLine("000FFF8007FC03FF00000FFF1FF81FF07FC07FF000003FFFFF80,1FFFFF007FFFF007FC1FFFE000")
                builder.AppendLine("7FFFFFF83FF1FFC3FFF8000FFFFFFFC0,1FFFFF007FFFF007FC1FFFE0007FFFFFF83FF1FFC3FFF8")
                builder.AppendLine("000FFFFFFFC0,7FFFFF01FFFFFC07FC7FFFF803FFFFFFF83FF1FFC7FFFF001FFFFFFFC0,7FFFFF")
                builder.AppendLine("01FFFFFC07FC7FFFF803FFFFFFF83FF1FFC7FFFF001FFFFFFFC0,FFFFFF03FFFFFE07FCFFFFFC0F")
                builder.AppendLine("FFFFFFF83FF1FFDFFFFF007FFFFFFFC0,FF803F0FFE00FF87FFFC3FFC0FFFC3FFF83FF1FFFF0FFF")
                builder.AppendLine("C0FFF0FFFFC0,FF803F0FFE00FF87FFFC3FFC0FFFC3FFF83FF1FFFF0FFFC0FFF0FFFFC0,FE0003")
                builder.AppendLine("1FF8007FC7FFE00FFF1FFE001FF83FF1FFFC01FFC3FF801FFC,FE00001FF0001FC7FF8007FF1FF8")
                builder.AppendLine("001FF83FF1FFF801FFC3FF8007FC,FE00001FF0001FC7FF8007FF1FF8001FF83FF1FFF801FFC3FF")
                builder.AppendLine("8007FC,FF80007FF0001FF7FF0007FF7FF0001FF83FF1FFE0007FE3FE0007FC,FFF0007FF0000F")
                builder.AppendLine("F7FF0007FF7FF0001FF83FF1FFC0007FE3FE0007FE,FFF0007FF0000FF7FF0007FF7FF0001FF83F")
                builder.AppendLine("F1FFC0007FE3FE0007FE,FFFF007FF0001FFFFC0007FF7FF0001FF83FF1FFC0007FE3FE0007FE,FF")
                builder.AppendLine("FFE0FFFFFFFFFFFC0007FF7FC0001FF83FF1FFC0007FE3FE0007FC,FFFFE0FFFFFFFFFFFC0007FF")
                builder.AppendLine("7FC0001FF83FF1FFC0007FE3FE0007FC,7FFFF8FFFFFFFFFFFC0007FFFFC0001FF83FF1FFC0007F")
                builder.AppendLine("E3FF8007FC,1FFFFCFFFFFFFFFFFC0007FFFFC0001FF83FF1FFC0007FE0FF801FFC,1FFFFCFFFF")
                builder.AppendLine("FFFFFFFC0007FFFFC0001FF83FF1FFC0007FE0FF801FFC,03FFFFFFC0000007FC0007FFFFC0001F")
                builder.AppendLine("F83FF1FFC0007FE0FFF0FFF0,007FFFFFF0000007FC0007FF7FF0001FF83FF1FFC0007FE07FFFFF")
                builder.AppendLine("E0,007FFFFFF0000007FC0007FF7FF0001FF83FF1FFC0007FE07FFFFFE0,000FFFFFF0000007FC")
                builder.AppendLine("0007FF7FF0001FF83FF1FFC0007FE00FFFFF80,0001FFFFF0000007FC0007FF7FF0001FF83FF1FF")
                builder.AppendLine("C0007FE001FFFC,0001FFFFF0000007FC0007FF7FF0001FF83FF1FFC0007FE001FFFC,0000FFFF")
                builder.AppendLine("F8000007FC0007FF7FF8001FF83FF1FFC0007FE00FF8,0000FF9FF8000037FC0007FF1FF8001FF8")
                builder.AppendLine("3FF1FFC0007FE01FC0,0000FF9FF8000037FC0007FF1FF8001FF83FF1FFC0007FE01FC0,F001FF")
                builder.AppendLine("9FFF0001F7FC0007FF1FFF00FFF83FF1FFC0007FE07FC0,F001FF9FFF0001F7FC0007FF1FFF00FF")
                builder.AppendLine("F83FF1FFC0007FE07FC0,FFFFFF0FFFF81FF7FC0007FF0FFFFFFFF83FF1FFC0007FE07FF0,FFFF")
                builder.AppendLine("FF03FFFFFFF7FC0007FF03FFFFFFF83FF1FFC0007FE07FFFF8,FFFFFF03FFFFFFF7FC0007FF03FF")
                builder.AppendLine("FFFFF83FF1FFC0007FE07FFFF8,FFFFFC007FFFFFC7FC0007FF01FFFF9FF83FF1FFC0007FE00FFF")
                builder.AppendLine("FFE0,7FFFE0000FFFFC07FC0007FF003FFF1FF83FF1FFC0007FE003FFFFFC,7FFFE0000FFFFC07")
                builder.AppendLine("FC0007FF003FFF1FF83FF1FFC0007FE003FFFFFC,00780000001C0000000000000001C000000000")
                builder.AppendLine("00000000007FFFFFFE,000000000000000000000000000000000000000000000000FFFFFFFE,00")
                builder.AppendLine("0000000000000000000000000000000000000000000000FFFFFFFE,000000000000000000000000")
                builder.AppendLine("000000000000000000000003FF8007FF80,00000000000000000000000000000000000000000000")
                builder.AppendLine("0007FC0000FF80,000000000000000000000000000000000000000000000007FC0000FF80,0000")
                builder.AppendLine("00000000000000000000000000000000000000000007FC0000FF80,000000000000000000000000")
                builder.AppendLine("00000000000000000000001FFC0000FF80,00000000000000000000000000000000000000000000")
                builder.AppendLine("001FFC0000FF80,FDF81C1DC7C3E0783C7F8F001187C0F07E3B1FCFC0000007FC0000FF80,61CE")
                builder.AppendLine("1C1FC6037188270C08001186E380403B020E00000007FE0007FF80,61CE1C1FC6037188270C0800")
                builder.AppendLine("1186E380403B020E00000007FE0007FF80,61CE3C1FC703118E270C0E001186E300783F020F0000")
                builder.AppendLine("0007FFFFFFFE,61F83F1FC1C3F18E3C0C0E001187C300783F020F00000003FFFFFFFC,61F83F1F")
                builder.AppendLine("C1C3F18E3C0C0E001187C300783F020F00000003FFFFFFFC,61F83F1FC0E301883C0C08001187C3")
                builder.AppendLine("9C4027020E000000007FFFFFF0,61CEE31DC7E301F8270C0F001F86E0FC7E23020FC000000003FF")
                builder.AppendLine("FF,61CEE31DC7E301F8270C0F001F86E0FC7E23020FC000000003FFFF,0000000001C000700000")
                builder.AppendLine("00000E000070,00,00,")
                ' Fin del logo


                builder.AppendLine("^FO0,271^GB840,2,2,B,0^FS")
                builder.AppendLine("^FO0,938^GB839,2,2,B,0^FS")
                builder.AppendLine("^FO375,667^GB2,354,2,B,0^FS")
                builder.AppendLine("^FO1,842^GB375,2,2,B,0^FS")
                builder.AppendLine("^FO1,968^GB682,2,2,B,0^FS")
                builder.AppendLine("^FO2,842^GB377,48,48,B,0^FS")
                builder.AppendLine("^FO2,890^GB377,48,48,B,0^FS")
                builder.AppendLine("^FO115,943^GB2,91,2,B,0^FS")
                builder.AppendLine("^FO233,943^GB2,92,2,B,0^FS")
                builder.AppendLine("^FO681,943^GB2,93,2,B,0^FS")
                builder.AppendLine("^FO0,666^GB797,1,1,B,0^FS")
                builder.AppendLine("^AEN,28,15^FO310,12^FDORG:^FS")
                builder.AppendLine("^ADN,36,20^FO287,65^FDEnvio:^FS")
                builder.AppendLine("^AFN,26,14^FO631,500^FDDESTINATARIO^FS")
                builder.AppendLine("^AFN,26,14^FO3,671^FDOBSERVACIONES^FS")
                builder.AppendLine("^AFN,26,14^FO391,671^FDTEL:^FS")
                builder.AppendLine("^AFN,26,14^FO391,703^FDCONTACTO:^FS")
                builder.AppendLine("^ADN,18,10^FO20,948^FDBULTOS^FS")
                builder.AppendLine("^ADN,18,10^FO145,948^FDKILOS^FS")
                builder.AppendLine("^ADN,18,10^FO276,948^FDFECHA^FS")
                builder.AppendLine("^ADN,18,10^FO388,948^FDREFERENCIA^FS")
                builder.AppendLine("^AFN,27,15^FO410,12^FN1^FS")
                builder.AppendLine("^AFN,27,10^FO312,40^FN2^FS")
                builder.AppendLine("^AFN,45,25^FO435,65^FN3^FS")
                builder.AppendLine("^AFN,26,14^FO287,115^FN33^FS")
                builder.AppendLine("^AQN,200,140^FO15,118^FN4^FS")
                builder.AppendLine("^AQN,200,145^FO238,118^FN5^FS")
                builder.AppendLine("^ADN,28,20^FO25,530^FN8^FS")
                builder.AppendLine("^ADN,28,14^FO28,573^FN9^FS")
                builder.AppendLine("^BY2^FO400,780^BCN,70,Y,N,N^FN251^FA20^FS")
                builder.AppendLine("^BY4^FO50,296^BCN,180,N,N,N^FN6^FA180^FS")
                builder.AppendLine("^ADN,28,15^FO28,612^FN10^FS")
                builder.AppendLine("^AQN,26,14^FO5,696^FN11^FS")
                builder.AppendLine("^AQN,26,14^FO5,720^FN12^FS")
                builder.AppendLine("^AQN,26,14^FO5,745^FN13^FS")
                builder.AppendLine("^AQN,26,14^FO5,766^FN131^FS")
                builder.AppendLine("^AQN,26,14^FO5,789^FN14^FS")
                builder.AppendLine("^AQN,26,14^FO5,811^FN141^FS")
                builder.AppendLine("^AFN,26,14^FO470,670^FN15^FS")
                builder.AppendLine("^AFN,26,14^FO535,703^FN16^FS")
                builder.AppendLine("^AQN,58,45^FO391,755^FN17^FS")
                builder.AppendLine("^AQN,58,65^FO391,795^FN18^FS")
                builder.AppendLine("^AQN,58,85^FO391,850^FN19^FS")
                builder.AppendLine("^AQN,58,65^FO391,893^FN20^FS")
                builder.AppendLine("^AQN,150,58^FO9,830^FN21^FS")
                builder.AppendLine("^AQN,50,30^FO30,970^FN22^FS")
                builder.AppendLine("^AQN,50,30^FO63,970^FD/^FS")
                builder.AppendLine("^AQN,50,30^FO80,970^FN27^FS")
                builder.AppendLine("^AQN,50,30^FO147,970^FN23^FS")
                builder.AppendLine("^AQN,50,30^FO240,970^FN24^FS")
                builder.AppendLine("^AQN,50,40^FO385,970^FN25^FS")
                builder.AppendLine("^AQN,106,106^FO710,938^FN26^FS")
                builder.AppendLine("^ADN,100,50^FO590,575^FN28^FS")
                builder.AppendLine("^AQN,1000,1000^FO15,730^FN30^FS")
                builder.AppendLine("^AQN,100,100^FO15,680^FN31^FS")
                builder.AppendLine("^AQN,200,200^FO15,930^FN32^FS")
                builder.AppendLine("^FO579,563^GB253,105,2,B,0^FS")
                builder.AppendLine("^PQ1,0,1,Y")
                builder.AppendLine("^XZ")
                builder.AppendLine("^FX --- DATOS DE LAS ETIQUETAS A IMPRIMIR --- ^FS")
                builder.AppendLine("")
                builder.AppendLine("")
                builder.AppendLine("")
                builder.AppendLine("")
                builder.AppendLine("^XA^XFSENDEXP1^FS""")
                builder.AppendLine("^FN1^FH\^FD" + DELEGACION_ORIGEN + " ALGETE^FS")
                builder.AppendLine("^FN2^FH\^FDNUEVA VISION, S.A.^FS")
                builder.AppendLine("^FN3^FD" + envio.CodigoBarras.Trim + "^FS")
                builder.AppendLine("^FN61^FD^FS")
                builder.AppendLine("")
                builder.AppendLine("^FN4^FD" + rutaZona.CodigoDelegacion + "^FS")
                builder.AppendLine("^FN5^FH\^FD" + rutaZona.NombreDelegacion + "^FS")
                builder.AppendLine("")
                builder.AppendLine("^FN6^SN>;" + codigoBarrasBulto + ",1,Y^FS")
                builder.AppendLine("")
                builder.AppendLine("^FN8^FH\^FD" + envio.Nombre.ToUpper.Trim + "^FS")
                builder.AppendLine("^FN9^FH\^FD" + envio.Direccion.ToUpper.Trim + "^FS")
                builder.AppendLine("")
                builder.AppendLine("^FN10^FH\^FD" + envio.CodPostal.ToUpper.Trim + " " + envio.Poblacion.ToUpper.Trim + "^FS")
                builder.AppendLine("")
                builder.AppendLine("^FN11^FH\^FD" + envio.Observaciones?.Trim + "^FS")
                builder.AppendLine("")
                builder.AppendLine("^FN12^FH\^FD^FS")
                builder.AppendLine("")
                builder.AppendLine("^FN131^FH\^FD^FS")
                builder.AppendLine("^FN14^FH\^FD^FS")
                builder.AppendLine("^FN141^FH\^FD ^FS")
                builder.AppendLine("^FN15^FD" + IIf(envio.Telefono.ToUpper.Trim <> "", envio.Telefono.ToUpper.Trim, envio.Movil.ToUpper.Trim) + "^FS")
                builder.AppendLine("")
                builder.AppendLine("^FN16^FH\^FD" + envio.Atencion.ToUpper.Trim + "^FS")
                builder.AppendLine("")
                builder.AppendLine("^FN21^FD01 SEND EXPRES^FS")
                builder.AppendLine("^FN22^SN" + i.ToString("D3") + ",1,Y^FS")
                builder.AppendLine("^FN23^FD1^FS") 'kilos
                builder.AppendLine("^FN24^FD" + envio.Fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) + "^FS")
                builder.AppendLine("^FN25^FH\^FD" + envio.Cliente.Trim + "/" + envio.Pedido.ToString + "^FS")
                builder.AppendLine("^FN251^FD^FS")
                builder.AppendLine("^FN26^FDP^FS")
                builder.AppendLine("^FN27^FD" + envio.Bultos.ToString + "^FS") ' Total de bultos
                builder.AppendLine("^FN28^FD" + rutaZona.CodigoZona + "^FS")
                builder.AppendLine("")
                builder.AppendLine("^FN52^FH\^FD" + envio.CodigoBarras + "VCIW^FS")
                builder.AppendLine("^PQ1,0,1,Y")
                builder.AppendLine("^XZ")
            Next
            RawPrinterHelper.SendStringToPrinter(puerto, builder.ToString)
        Catch ex As Exception
            Throw New Exception("Se ha producido un error y no se han imprimido las etiquetas:" + vbCr + ex.Message)
            'Finally
            '    objStream.Close()
        End Try
    End Sub
    Public ReadOnly Property visibilidadSoloImprimir As Visibility Implements IAgencia.visibilidadSoloImprimir
        Get
            Return Visibility.Visible
        End Get
    End Property
    Public ReadOnly Property retornoSoloCobros As Byte Implements IAgencia.retornoSoloCobros
        Get
            Return 0 ' Sin retorno
        End Get
    End Property
    Public ReadOnly Property servicioSoloCobros As Byte Implements IAgencia.servicioSoloCobros
        Get
            Return 1 ' Normal
        End Get
    End Property
    Public ReadOnly Property horarioSoloCobros As Byte Implements IAgencia.horarioSoloCobros
        Get
            Return 1 ' Normal
        End Get
    End Property
    Public ReadOnly Property retornoSinRetorno As Byte Implements IAgencia.retornoSinRetorno
        Get
            Return 0 ' Sin Retorno
        End Get
    End Property
    Public ReadOnly Property paisDefecto As Integer Implements IAgencia.paisDefecto
        Get
            Return 1
        End Get
    End Property
    Public ReadOnly Property retornoObligatorio As Byte Implements IAgencia.retornoObligatorio
        Get
            Return 1 ' Retorno obligatorio
        End Get
    End Property


    Private Function rellenarPaises() As ObservableCollection(Of Pais)
        Return New ObservableCollection(Of Pais) From {
            New Pais(1, "ESPAÑA", "034"),
            New Pais(2, "PORTUGAL", "035")
        }
    End Function

    Private Function IAgencia_EnlaceSeguimiento(envio As EnviosAgencia) As String Implements IAgencia.EnlaceSeguimiento
        If IsNothing(envio) OrElse IsNothing(envio.AgenciasTransporte) Then
            Return String.Empty
        End If
        Return String.Format("https://info.sending.es/fgts/pub/locNumServ.seam?cliente={0}&localizador={1}", envio.AgenciasTransporte.Identificador, envio.CodigoBarras)
    End Function

    Public Function RespuestaYaTramitada(respuesta As String) As Boolean Implements IAgencia.RespuestaYaTramitada
        Return False
    End Function


    Public ReadOnly Property ListaPaises As ObservableCollection(Of Pais) Implements IAgencia.ListaPaises
    Public ReadOnly Property ListaTiposRetorno As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaTiposRetorno
    Public ReadOnly Property ListaServicios As ObservableCollection(Of ITarifaAgencia) Implements IAgencia.ListaServicios
    Public ReadOnly Property ListaHorarios As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaHorarios

    Public ReadOnly Property ServicioDefecto As Byte Implements IAgencia.ServicioDefecto
        Get
            Return 1 ' Normal
        End Get
    End Property

    Public ReadOnly Property HorarioDefecto As Byte Implements IAgencia.HorarioDefecto
        Get
            Return 1 ' Normal
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


End Class

Friend Class CodigoRutaZona
    Private Property pueblo As AgenciaSendingPueblo
    Private Property delegacion As AgenciaSendingDelegacion

    Public Sub New(codPostal As String, poblacion As String)
        Using db As New NestoEntities
            Dim codigoPostalFormateado = Replace(codPostal, "-", "")
            codigoPostalFormateado = Replace(codigoPostalFormateado, " ", "")
            Dim pueblos = db.AgenciaSendingPueblos.Where(Function(p) p.CODP_CODIGO = codigoPostalFormateado)
            pueblo = pueblos.FirstOrDefault
            If pueblos.Count > 1 Then
                pueblos = pueblos.Where(Function(p) p.POBL_NOMBRE = poblacion)
                If pueblos.Any Then
                    pueblo = pueblos.FirstOrDefault
                End If
            End If
            delegacion = db.AgenciaSendingDelegaciones.Single(Function(d) d.Codigo = pueblo.DELE_CODIGO)
        End Using
    End Sub

    Public Function NombreDelegacion() As String
        Return delegacion.Nombre
    End Function

    Public Function CodigoZona() As String
        Return pueblo.ZONA_CODIGO.PadLeft(3, "0")
    End Function
    Public Function CodigoDelegacion()
        Return delegacion.Codigo.PadLeft(3, "0")
    End Function
End Class
