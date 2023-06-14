Imports System.Collections.ObjectModel
Imports System.ComponentModel.DataAnnotations
Imports System.Configuration
Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports System.Threading.Tasks
Imports System.Windows
Imports Nesto.Contratos
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Serialization

Public Class AgenciaCorreosExpress
    Implements IAgencia

    Public Sub New()
        ListaTiposRetorno = New ObservableCollection(Of tipoIdDescripcion) From {
            New tipoIdDescripcion(0, "No disponible")
        }
        ListaServicios = New ObservableCollection(Of tipoIdDescripcion) From {
            New tipoIdDescripcion(63, "Paq 24"),
            New tipoIdDescripcion(66, "Baleares"),
            New tipoIdDescripcion(69, "Canarias Marítimo"),
            New tipoIdDescripcion(90, "Internacional Estándar (monobulto)"),
            New tipoIdDescripcion(91, "Internacional Express (multibulto)"),
            New tipoIdDescripcion(54, "EntregaPlus (entrega+recogida)"),
            New tipoIdDescripcion(92, "Paq Empresa 14"),
            New tipoIdDescripcion(93, "EPaq 24")
        }
        ListaHorarios = New ObservableCollection(Of tipoIdDescripcion) From {
            New tipoIdDescripcion(0, "No disponible")
        }

        ListaPaises = rellenarPaises()
    End Sub

    Public ReadOnly Property visibilidadSoloImprimir As Visibility Implements IAgencia.visibilidadSoloImprimir
        Get
            Return Visibility.Hidden
        End Get
    End Property

    Public ReadOnly Property retornoSoloCobros As Byte Implements IAgencia.retornoSoloCobros
        Get
            Return 0 ' No disponible
        End Get
    End Property

    Public ReadOnly Property servicioSoloCobros As Byte Implements IAgencia.servicioSoloCobros
        Get
            Return 93 'NO EXISTE EL SERVICIO SOLO DE COBROS
        End Get
    End Property

    Public ReadOnly Property horarioSoloCobros As Byte Implements IAgencia.horarioSoloCobros
        Get
            Return 0 'No disponible
        End Get
    End Property

    Public ReadOnly Property retornoSinRetorno As Byte Implements IAgencia.retornoSinRetorno
        Get
            Return 0 'No disponible
        End Get
    End Property

    Public ReadOnly Property retornoObligatorio As Byte Implements IAgencia.retornoObligatorio
        Get
            Return 0 'No disponible
        End Get
    End Property

    Public ReadOnly Property paisDefecto As Integer Implements IAgencia.paisDefecto
        Get
            Return 724 ' España
        End Get
    End Property

    Public ReadOnly Property ListaPaises As ObservableCollection(Of Pais) Implements IAgencia.ListaPaises
    Public ReadOnly Property ListaTiposRetorno As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaTiposRetorno
    Public ReadOnly Property ListaServicios As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaServicios
    Public ReadOnly Property ListaHorarios As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaHorarios
    Public ReadOnly Property ServicioDefecto As Byte Implements IAgencia.ServicioDefecto
        Get
            Return 92 ' Paq Empresa 14
        End Get
    End Property

    Public ReadOnly Property HorarioDefecto As Byte Implements IAgencia.HorarioDefecto
        Get
            Return 0 'No disponible
        End Get
    End Property

    Public ReadOnly Property ServicioAuxiliar As Byte Implements IAgencia.ServicioAuxiliar
        Get
            Return 52 ' Código especial para las recogidas de EntregaPlus
        End Get
    End Property

    Public ReadOnly Property ServicioCreaEtiquetaRetorno As Byte Implements IAgencia.ServicioCreaEtiquetaRetorno
        Get
            Return 54 ' EntregaPlus
        End Get
    End Property

    Public Sub calcularPlaza(codPostal As String, ByRef nemonico As String, ByRef nombrePlaza As String, ByRef telefonoPlaza As String, ByRef emailPlaza As String) Implements IAgencia.calcularPlaza
        nemonico = "CE"
        nombrePlaza = "Correos Express"
        telefonoPlaza = "916602444"
        emailPlaza = "am-cabelloandres@correosexpress.com"
    End Sub

    Public Async Function LlamadaWebService(envio As EnviosAgencia, servicio As IAgenciaService) As Task(Of RespuestaAgencia) Implements IAgencia.LlamadaWebService
        Dim empresa = servicio.CargarListaEmpresas().Single(Function(e) e.Número = envio.Empresa)
        Dim envioCEX As New EnvioCEX With {
            .Solicitante = "I" + envio.AgenciasTransporte.Identificador,
            .NumEnvio = envio.CodigoBarras,
            .Ref = envio.Cliente.Trim + "/" + envio.Pedido.ToString,
            .Fecha = Today.ToString("ddMMyyyy"),
            .CodRte = envio.AgenciasTransporte.Identificador,
            .NomRte = Left(empresa.Nombre.ToUpper.Trim, 40),
            .NifRte = empresa.NIF.ToUpper.Trim,
            .DirRte = empresa.Dirección.ToUpper.Trim,
            .PobRte = empresa.Población.ToUpper.Trim,
            .CodPosNacRte = Left(empresa.CodPostal.ToUpper.Trim, 5),
            .TelefRte = empresa.Teléfono.Trim,
            .EmailRte = empresa.Email.Trim,
            .NomDest = Left(envio.Nombre.ToUpper.Trim, 40),
            .DirDest = envio.Direccion.ToUpper.Trim,
            .PobDest = envio.Poblacion.ToUpper.Trim,
            .CodPosNacDest = Left(envio.CodPostal.Trim, 5),
            .ContacDest = envio.Atencion.ToUpper.Trim,
            .TelefDest = IIf(envio.Telefono.Trim <> "", envio.Telefono.Trim, envio.Movil.Trim),
            .EmailDest = envio.Email.Trim,
            .TelefOtrs = IIf(envio.Telefono.Trim <> "", envio.Movil.Trim, ""),
            .Observac = envio.Observaciones?.Substring(0, Math.Min(80, envio.Observaciones.Length)),
            .NumBultos = envio.Bultos.ToString("D2"),
            .Kilos = "00001.00",
            .Producto = envio.Servicio.ToString("D2"),
            .Portes = "P",
            .Reembolso = Replace(envio.Reembolso.ToString("0.##"), ",", "."),
            .Seguro = Replace(envio.ImporteAsegurado.ToString("0.##"), ",", "."),
            .ListaBultos = New List(Of Bulto),
            .ListaInformacionAdicional = New List(Of InformacionAdicional)
        }
        For i = 1 To envio.Bultos
            Dim codigoPostal As String = envio.CodPostal
            If envio.Servicio = 63 AndAlso envio.Pais = 620 Then ' Portugal
                codigoPostal = "8" + envio.CodPostal.Substring(0, 4)
            ElseIf envio.Servicio = 90 OrElse envio.Servicio = 91 Then 'Internacional
                codigoPostal = "99999"
            Else
                codigoPostal = envio.CodPostal.Trim
            End If

            envioCEX.ListaBultos.Add(New Bulto With {
                .CodUnico = CalcularCodigoBarrasBulto(envio.CodigoBarras, i, codigoPostal),
                .Orden = i.ToString("D2")
            })
        Next
        envioCEX.ListaInformacionAdicional.Add(New InformacionAdicional())

        If (envio.Servicio = 90 OrElse envio.Servicio = 91) OrElse (envio.Servicio = 63 AndAlso envio.Pais = 620) Then ' internacional o Portugal
            envioCEX.PaisISODest = ListaPaises.Single(Function(c) c.Id = envio.Pais).CodigoAlfa
            envioCEX.CodPosIntDest = Left(envio.CodPostal, 7)
            envioCEX.CodPosIntDest = envioCEX.CodPosIntDest.Replace("-", "")
            envioCEX.CodPosIntDest = envioCEX.CodPosIntDest.Replace(" ", "")
            envioCEX.CodPosIntDest = envioCEX.CodPosIntDest.Replace(".", "")
            envioCEX.CodPosNacDest = ""
        End If

        Dim usuario As String = ConfigurationManager.AppSettings("CorreosExpressUsuario")
        Dim password As String = ConfigurationManager.AppSettings("CorreosExpressPassword")

        Dim credenciales As New NetworkCredential(usuario, password)
        Dim handler As New HttpClientHandler With {.Credentials = credenciales}

        Using client As New HttpClient(handler)
            client.BaseAddress = New Uri("https://www.cexpr.es/wspsc/apiRestGrabacionEnviok8s/")
            'client.BaseAddress = New Uri("https://www.correosexpress.com/wpsc/apiRestGrabacionEnvio/")
            'https://www.correosexpress.com/wpsc/apiRestGrabacionEnvio/json/grabacionEnvio
            'https://test.correosexpress.com/wspsc/apiRestGrabacionEnvio/json/grabacionEnvio
            Dim response As HttpResponseMessage
            Dim textoRespuesta As String = String.Empty
            Dim respuesta As New RespuestaAgencia With {
                .Agencia = "CorreosExpress",
                .Fecha = DateTime.Now,
                .UrlLlamada = client.BaseAddress.ToString,
                .CuerpoRespuesta = String.Empty
            }

            Try
                Dim urlConsulta As String = "json/grabacionEnvio"
                Dim settings As New JsonSerializerSettings With {
                    .ContractResolver = New CamelCasePropertyNamesContractResolver
                }
                Dim cadenaJson As String = JsonConvert.SerializeObject(envioCEX, Formatting.Indented, settings)
                respuesta.CuerpoLlamada = cadenaJson
                Dim content As HttpContent = New StringContent(cadenaJson, Encoding.UTF8, "application/json")
                response = Await client.PostAsync(urlConsulta, content)

                If response.IsSuccessStatusCode Then
                    textoRespuesta = Await response.Content.ReadAsStringAsync()
                    respuesta.CuerpoRespuesta = textoRespuesta
                    Dim respuestaCEX As RespuestaCEX = JsonConvert.DeserializeObject(Of RespuestaCEX)(textoRespuesta)
                    If respuestaCEX.CodigoRetorno = 0 Then
                        respuesta.Exito = True
                        respuesta.TextoRespuestaError = "OK"
                        Return respuesta
                    Else
                        respuesta.Exito = False
                        respuesta.TextoRespuestaError = respuestaCEX.MensajeRetorno
                        Return respuesta
                    End If
                Else
                    respuesta.Exito = False
                    respuesta.TextoRespuestaError = "Error en la llamada al Webservice"
                    Return respuesta
                End If

            Catch ex As Exception
                respuesta.Exito = False
                respuesta.TextoRespuestaError = ex.Message
                Return respuesta
            Finally

            End Try

            respuesta.Exito = False
            respuesta.TextoRespuestaError = "Nunca debería salir este error"
            Return respuesta

        End Using

    End Function

    Public Async Sub imprimirEtiqueta(envio As EnviosAgencia) Implements IAgencia.imprimirEtiqueta
        If IsNothing(envio.CodigoBarras) OrElse envio.CodigoBarras.Trim = "" Then
            Throw New Exception("El envío debe tener un código de barras asignada para poder imprimir la etiqueta")
        End If

        Dim mainViewModel As New MainViewModel
        Dim puerto As String = Await mainViewModel.leerParametro(envio.Empresa, Parametros.Claves.ImpresoraBolsas)

        Dim i As Integer

        Const ANCHO_OBSERVACIONES As Integer = 46

        Dim codigoPostal As String = envio.CodPostal
        If envio.Servicio = 90 OrElse envio.Servicio = 91 Then 'Internacional
            codigoPostal = "99999"
        ElseIf envio.Servicio = 63 AndAlso envio.Pais = 620 Then ' Portugal
            envio.CodPostal = envio.CodPostal.Replace(" ", "")
            envio.CodPostal = envio.CodPostal.Replace("-", "")
            codigoPostal = "8" + envio.CodPostal.Substring(0, 4)
        End If

        Try
            Dim builder As New StringBuilder
            For i = 1 To envio.Bultos
                Dim codigoBarrasBulto = CalcularCodigoBarrasBulto(envio.CodigoBarras, i, codigoPostal)
                Dim observaciones = envio.Observaciones?.Substring(0, Math.Min(envio.Observaciones.Length, ANCHO_OBSERVACIONES * 2))
                If IsNothing(observaciones) Then
                    observaciones = String.Empty
                End If
                Dim textoServicio = envio.Servicio.ToString + " " + ListaServicios.Single(Function(s) s.id = envio.Servicio).descripcion.ToUpper.Trim
                If textoServicio.Length > 18 Then
                    textoServicio = textoServicio.Substring(0, 18)
                End If
                builder.AppendLine("N")
                builder.AppendLine("OD")
                builder.AppendLine("q816")
                'objStream.AppendLine("I8,1")
                builder.AppendLine("I8,A,034")
                builder.AppendLine("Q1583,24+0")
                builder.AppendLine("S4")
                builder.AppendLine("D13")
                builder.AppendLine("ZT")
                builder.AppendLine("LO5,540,467,4")
                builder.AppendLine("LO93,330,120,4")
                builder.AppendLine("LO93,170,120,4")
                builder.AppendLine("LO352,5,4,535")
                builder.AppendLine("LO150,5,4,535")
                builder.AppendLine("LO210,5,4,535")
                builder.AppendLine("LO90,5,4,1100")
                builder.AppendLine("LO468,540,4,660")
                builder.AppendLine("A25,1100,3,1,2,1,N,""" + envio.Empresas.Nombre.ToUpper.Trim() + """")
                builder.AppendLine("A55,1100,3,1,1,1,N,""" + envio.Empresas.Dirección.ToUpper.Trim() + """")
                builder.AppendLine("A75,1100,3,1,1,1,N,""" + envio.Empresas.Población.ToUpper.Trim() + """")
                builder.AppendLine("A75,830,3,1,1,1,N,""Telf.:  " + envio.Empresas.Teléfono.Trim() + """")
                builder.AppendLine("A270,520,3,3,2,1,N,""COD.BULTO: " + codigoBarrasBulto + """")
                builder.AppendLine("B770,410,1,1,4,2,256,N,""" + codigoBarrasBulto + """")
                builder.AppendLine("A100,1100,3,3,2,1,N,""" + envio.Nombre.ToUpper.Trim + """")
                builder.AppendLine("A435,1150,3,1,2,1,N,""ATT: " + envio.Atencion.ToUpper.Trim + """")
                builder.AppendLine("A435,800,3,1,2,1,N,""TELF.: " + IIf(envio.Telefono.ToUpper.Trim <> "", envio.Telefono.ToUpper.Trim, envio.Movil.ToUpper.Trim) + """")
                builder.AppendLine("A140,1100,3,2,2,1,N,""" + envio.Direccion.ToUpper.Trim + """")
                builder.AppendLine("A250,1100,3,5,2,2,N,""" + envio.CodPostal.ToUpper.Trim + """")
                builder.AppendLine("A390,1150,3,3,2,1,N,""" + envio.Poblacion.ToUpper.Trim + """")
                builder.AppendLine("A220,520,3,3,2,1,N,""REF: " + envio.Cliente.Trim() + "/" + envio.Pedido.ToString.Trim() + """")
                builder.AppendLine("A350,1150,3,3,2,3,N,""  """) ' << DESTINO >>
                builder.AppendLine("A350,1150,3,3,2,3,N,""" + ListaPaises.Single(Function(p) p.Id = envio.Pais).Nombre.ToUpper.Trim + """")
                builder.AppendLine("A20,530,3,2,3,2,N,""EXP:" + envio.CodigoBarras + """")
                builder.AppendLine("A158,320,3,2,1,1,N,""PESO: """)
                builder.AppendLine("A175,320,3,2,2,1,N,""1""") '<< KILOS >> Kgs.
                builder.AppendLine("A98,320,3,2,1,1,N,""BULTOS: """)
                builder.AppendLine("A115,320,3,2,2,1,N,""" + i.ToString + " DE " + envio.Bultos.ToString + """")
                builder.AppendLine("A98,520,3,2,1,1,N,""REEMBOLSO: """)
                builder.AppendLine("A115,520,3,2,2,1,N,""" + envio.Reembolso.ToString("C") + """")
                builder.AppendLine("A158,520,3,2,1,1,N,""TIPO DE PORTES:""")
                builder.AppendLine("A175,520,3,2,2,1,N,""PAGADOS""")
                builder.AppendLine("A70,450,3,1,1,1,N,""Envio retorno: """) ' << RETORNO >> 
                builder.AppendLine("A400,520,3,3,2,2,N,""" + textoServicio + """")
                builder.AppendLine("A180,1100,3,2,2,1,N,""" + observaciones.Substring(0, Math.Min(observaciones.Length, ANCHO_OBSERVACIONES)) + """")
                If observaciones.Length > 46 Then
                    builder.AppendLine("A220,1100,3,2,2,1,N,""" + observaciones.Substring(ANCHO_OBSERVACIONES, Math.Min(observaciones.Length - ANCHO_OBSERVACIONES, ANCHO_OBSERVACIONES)) + """")
                End If
                builder.AppendLine("A115,160,3,2,2,1,N,""" + envio.Fecha.ToString("dd/MM/yyyy") + """")
                builder.AppendLine("A98,160,3,2,1,1,N,""FECHA: """)
                'objStream.AppendLine("b760,340,P,800,600,s1,c0,f0,x2,y5,l5,t0,o2," << PDF417 >> "")
                builder.AppendLine("P1")
                builder.AppendLine("N")
            Next

            If envio.Servicio = ServicioCreaEtiquetaRetorno Then
                envio.CodigoBarras = CalcularCodigoBarrasRetorno(envio.CodigoBarras)
                Dim codigoBarrasBulto = CalcularCodigoBarrasBulto(envio.CodigoBarras, 1, envio.Empresas.CodPostal.Trim)
                Dim observaciones = envio.Observaciones.Substring(0, Math.Min(envio.Observaciones.Length, ANCHO_OBSERVACIONES * 2))
                builder.AppendLine("N")
                builder.AppendLine("OD")
                builder.AppendLine("q816")
                'objStream.AppendLine("I8,1")
                builder.AppendLine("I8,A,034")
                builder.AppendLine("Q1583,24+0")
                builder.AppendLine("S4")
                builder.AppendLine("D13")
                builder.AppendLine("ZT")
                builder.AppendLine("LO5,540,467,4")
                builder.AppendLine("LO93,330,120,4")
                builder.AppendLine("LO93,170,120,4")
                builder.AppendLine("LO352,5,4,535")
                builder.AppendLine("LO150,5,4,535")
                builder.AppendLine("LO210,5,4,535")
                builder.AppendLine("LO90,5,4,1100")
                builder.AppendLine("LO468,540,4,660")
                builder.AppendLine("A25,1100,3,1,2,1,N,""" + envio.Nombre.ToUpper.Trim() + """")
                builder.AppendLine("A55,1100,3,1,1,1,N,""" + envio.Direccion.ToUpper.Trim() + """")
                builder.AppendLine("A75,1100,3,1,1,1,N,""" + envio.Poblacion.ToUpper.Trim() + """")
                builder.AppendLine("A75,830,3,1,1,1,N,""Telf.:  " + IIf(envio.Telefono.ToUpper.Trim <> "", envio.Telefono.ToUpper.Trim, envio.Movil.ToUpper.Trim) + """")
                builder.AppendLine("A270,520,3,3,2,1,N,""COD.BULTO: " + codigoBarrasBulto + """")
                builder.AppendLine("B770,410,1,1,4,2,256,N,""" + codigoBarrasBulto + """")
                builder.AppendLine("A100,1100,3,3,2,1,N,""" + envio.Empresas.Nombre.ToUpper.Trim + """")
                builder.AppendLine("A435,1150,3,1,2,1,N,""ATT: DPTO. ALMACÉN""")
                builder.AppendLine("A435,800,3,1,2,1,N,""TELF.: " + envio.Empresas.Teléfono.Trim() + """")
                builder.AppendLine("A140,1100,3,2,2,1,N,""" + envio.Empresas.Dirección.ToUpper.Trim + """")
                builder.AppendLine("A250,1100,3,5,2,2,N,""" + envio.Empresas.CodPostal.ToUpper.Trim + """")
                builder.AppendLine("A390,1150,3,3,2,1,N,""" + envio.Empresas.Población.ToUpper.Trim + """")
                builder.AppendLine("A220,520,3,3,2,1,N,""REF: " + envio.Cliente.Trim() + "/" + envio.Pedido.ToString.Trim() + "R""")
                builder.AppendLine("A350,1150,3,3,2,3,N,""  """) ' << DESTINO >>
                builder.AppendLine("A350,1150,3,3,2,3,N,""" + ListaPaises.Single(Function(p) p.Id = paisDefecto).Nombre.ToUpper.Trim + """")
                builder.AppendLine("A20,530,3,2,3,2,N,""EXP:" + envio.CodigoBarras + """")
                builder.AppendLine("A158,320,3,2,1,1,N,""PESO: """)
                builder.AppendLine("A175,320,3,2,2,1,N,""1""") '<< KILOS >> Kgs.
                builder.AppendLine("A98,320,3,2,1,1,N,""BULTOS: """)
                builder.AppendLine("A115,320,3,2,2,1,N,""1 DE 1""")
                builder.AppendLine("A98,520,3,2,1,1,N,""REEMBOLSO: """)
                builder.AppendLine("A115,520,3,2,2,1,N,""""")
                builder.AppendLine("A158,520,3,2,1,1,N,""TIPO DE PORTES:""")
                builder.AppendLine("A175,520,3,2,2,1,N,""PAGADOS""")
                builder.AppendLine("A70,450,3,1,1,1,N,""Envio retorno: """) ' << RETORNO >> 
                builder.AppendLine("A400,520,3,3,2,2,N,""52 RETORNO""")
                builder.AppendLine("A180,1100,3,2,2,1,N,""" + observaciones.Substring(0, Math.Min(observaciones.Length, ANCHO_OBSERVACIONES)) + """")
                If observaciones.Length > 46 Then
                    builder.AppendLine("A220,1100,3,2,2,1,N,""" + observaciones.Substring(ANCHO_OBSERVACIONES, Math.Min(observaciones.Length - ANCHO_OBSERVACIONES, ANCHO_OBSERVACIONES)) + """")
                End If
                builder.AppendLine("A115,160,3,2,2,1,N,""" + envio.Fecha.ToString("dd/MM/yyyy") + """")
                builder.AppendLine("A98,160,3,2,1,1,N,""FECHA: """)
                'objStream.AppendLine("b760,340,P,800,600,s1,c0,f0,x2,y5,l5,t0,o2," << PDF417 >> "")
                builder.AppendLine("P1")
                builder.AppendLine("N")
            End If
            RawPrinterHelper.SendStringToPrinter(puerto, builder.ToString)
        Catch ex As Exception
            Throw New Exception("Se ha producido un error y no se han grabado los datos:" + vbCr + ex.InnerException.Message)
            'Finally
            '    objStream.Close()
            '    objFSO = Nothing
            '    objStream = Nothing
        End Try
    End Sub

    Public Function CalcularCodigoBarrasRetorno(codigoBarras As String) As String
        Dim cuerpo As String = (Val(codigoBarras.Substring(2, 13)) + 1).ToString
        Dim pad As Char = "0"
        Dim nuevoCodigoBarras As String = ServicioAuxiliar.ToString("D2") + cuerpo.PadLeft(13, pad)
        Dim digitoControl As Integer = CalcularDigitoControl(nuevoCodigoBarras)
        Return nuevoCodigoBarras + digitoControl.ToString
    End Function

    Public Function cargarEstado(envio As EnviosAgencia) As XDocument Implements IAgencia.cargarEstado
        Throw New NotImplementedException()
    End Function

    Public Function transformarXMLdeEstado(envio As XDocument) As estadoEnvio Implements IAgencia.transformarXMLdeEstado
        Throw New NotImplementedException()
    End Function

    Public Function calcularCodigoBarras(agenciaVM As AgenciasViewModel) As String Implements IAgencia.calcularCodigoBarras
        Dim posiciones(14) As Integer
        Dim numeroEnvioString As String = agenciaVM.envioActual.Numero.ToString("D9")

        ' Posiciones 1 y 2: código del producto (servicio)
        posiciones(0) = agenciaVM.envioActual.Servicio / 10
        posiciones(1) = agenciaVM.envioActual.Servicio Mod 10
        ' Posiciones 3 a 6: código de cliente etiquetador (lo da Correos Express)
        posiciones(2) = Val(agenciaVM.envioActual.AgenciasTransporte.PrefijoCodigoBarras(0))
        posiciones(3) = Val(agenciaVM.envioActual.AgenciasTransporte.PrefijoCodigoBarras(1))
        posiciones(4) = Val(agenciaVM.envioActual.AgenciasTransporte.PrefijoCodigoBarras(2))
        posiciones(5) = Val(agenciaVM.envioActual.AgenciasTransporte.PrefijoCodigoBarras(3))
        ' Posiciones 7 a 15: rango de expediciones (Id de EnviosAgencia)
        posiciones(6) = Val(numeroEnvioString(0))
        posiciones(7) = Val(numeroEnvioString(1))
        posiciones(8) = Val(numeroEnvioString(2))
        posiciones(9) = Val(numeroEnvioString(3))
        posiciones(10) = Val(numeroEnvioString(4))
        posiciones(11) = Val(numeroEnvioString(5))
        posiciones(12) = Val(numeroEnvioString(6))
        posiciones(13) = Val(numeroEnvioString(7))
        posiciones(14) = Val(numeroEnvioString(8))
        ' Posición 16: dígito de control
        Dim digitoControl As Integer = CalcularDigitoControl(posiciones)

        Dim codigoBarras As String = String.Empty
        For Each posicion In posiciones
            codigoBarras += posicion.ToString
        Next
        Return codigoBarras + digitoControl.ToString("D1")
    End Function

    Public Function CalcularCodigoBarrasBulto(codigoBarrasEnvio As String, bulto As Integer, codigoPostal As String) As String
        Dim codigoBarrasString As String = String.Empty
        codigoBarrasString = codigoBarrasEnvio.Substring(0, codigoBarrasEnvio.Length - 1)
        codigoBarrasString += bulto.ToString("D2")
        codigoBarrasString += codigoPostal.Trim
        Dim posiciones(21) As Integer
        For i = 0 To codigoBarrasString.Length - 1
            posiciones(i) = Val(codigoBarrasString(i))
        Next
        Dim digitoControl = CalcularDigitoControl(posiciones)

        Return codigoBarrasString + digitoControl.ToString("D1")
    End Function

    Public Function CalcularDigitoControl(codigoString As String) As Integer
        Dim posiciones(14) As Integer
        For i = 0 To 14
            posiciones(i) = Val(codigoString(i))
        Next
        Return CalcularDigitoControl(posiciones)
    End Function

    Public Function CalcularDigitoControl(posiciones() As Integer) As Integer
        Dim sumaAcumulada As Integer = 0
        For i = 0 To posiciones.Length - 1
            If (i + posiciones.Length) Mod 2 = 0 Then
                sumaAcumulada += posiciones(i)
            Else
                sumaAcumulada += posiciones(i) * 3
            End If
        Next
        Dim superior As Integer = (sumaAcumulada - (sumaAcumulada Mod 10))
        If superior <> sumaAcumulada Then
            superior += 10
        End If
        Return superior - sumaAcumulada
    End Function

    Public Function EnlaceSeguimiento(envio As EnviosAgencia) As String Implements IAgencia.EnlaceSeguimiento
        Return "https://s.correosexpress.com/c?n=" + envio.CodigoBarras
    End Function

    Private Function rellenarPaises() As ObservableCollection(Of Pais)
        Return New ObservableCollection(Of Pais) From {
            New Pais(4, "AFGANISTAN", "AF"),
            New Pais(248, "ÅLAND", "AX"),
            New Pais(8, "ALBANIA", "AL"),
            New Pais(276, "ALEMANIA", "DE"),
            New Pais(20, "ANDORRA", "AD"),
            New Pais(24, "ANGOLA", "AO"),
            New Pais(660, "ANGUILA", "AI"),
            New Pais(10, "ANTARTIDA", "AQ"),
            New Pais(28, "ANTIGUA Y BARBUDA", "AG"),
            New Pais(530, "ANTILLAS HOLANDESAS", "AN"),
            New Pais(682, "ARABIA SAUDITA", "SA"),
            New Pais(12, "ARGELIA", "DZ"),
            New Pais(32, "ARGENTINA", "AR"),
            New Pais(51, "ARMENIA", "AM"),
            New Pais(533, "ARUBA", "AW"),
            New Pais(36, "AUSTRALIA", "AU"),
            New Pais(40, "AUSTRIA", "AT"),
            New Pais(31, "AZERBAIYAN", "AZ"),
            New Pais(44, "BAHAMAS", "BS"),
            New Pais(48, "BAHREIN", "BH"),
            New Pais(50, "BANGLADESH", "BD"),
            New Pais(52, "BARBADOS", "BB"),
            New Pais(112, "BIELORRUSIA", "BY"),
            New Pais(56, "BELGICA", "BE"),
            New Pais(84, "BELICE", "BZ"),
            New Pais(204, "BENIN", "BJ"),
            New Pais(60, "BERMUDAS", "BM"),
            New Pais(64, "BUTAN", "BT"),
            New Pais(68, "BOLIVIA", "BO"),
            New Pais(70, "BOSNIA Y HERZEGOVINA", "BA"),
            New Pais(72, "BOTSUANA", "BW"),
            New Pais(74, "ISLA BOUVET", "BV"),
            New Pais(76, "BRASIL", "BR"),
            New Pais(96, "BRUNEI", "BN"),
            New Pais(100, "BULGARIA", "BG"),
            New Pais(854, "BURKINA FASO", "BF"),
            New Pais(108, "BURUNDI", "BI"),
            New Pais(132, "CABO VERDE", "CV"),
            New Pais(136, "ISLAS CAIMAN", "KY"),
            New Pais(116, "CAMBOYA", "KH"),
            New Pais(120, "CAMERUN", "CM"),
            New Pais(124, "CANADA", "CA"),
            New Pais(140, "REPUBLICA CENTROAFRICANA", "CF"),
            New Pais(148, "CHAD", "TD"),
            New Pais(203, "REPUBLICA CHECA", "CZ"),
            New Pais(152, "CHILE", "CL"),
            New Pais(156, "CHINA", "CN"),
            New Pais(196, "CHIPRE", "CY"),
            New Pais(166, "ISLAS COCOS", "CC"),
            New Pais(170, "COLOMBIA", "CO"),
            New Pais(174, "COMORAS", "KM"),
            New Pais(178, "REPUBLICA DEL CONGO", "CG"),
            New Pais(180, "REPUBLICA DEMOCRATICA DEL CONGO", "CD"),
            New Pais(184, "ISLAS COOK", "CK"),
            New Pais(408, "COREA DEL NORTE", "KP"),
            New Pais(410, "COREA DEL SUR", "KR"),
            New Pais(384, "COSTA DE MARFIL", "CI"),
            New Pais(188, "COSTA RICA", "CR"),
            New Pais(191, "CROACIA", "HR"),
            New Pais(192, "CUBA", "CU"),
            New Pais(208, "DINAMARCA", "DK"),
            New Pais(212, "DOMINICA", "DM"),
            New Pais(214, "REPUBLICA DOMINICANA", "DO"),
            New Pais(218, "ECUADOR", "EC"),
            New Pais(818, "EGIPTO", "EG"),
            New Pais(222, "EL SALVADOR", "SV"),
            New Pais(784, "EMIRATOS ARABES UNIDOS", "AE"),
            New Pais(232, "ERITREA", "ER"),
            New Pais(703, "ESLOVAQUIA", "SK"),
            New Pais(705, "ESLOVENIA", "SI"),
            New Pais(724, "ESPAÑA", "ES"),
            New Pais(840, "ESTADOS UNIDOS", "US"),
            New Pais(581, "ISLAS ULTRAMARINAS DE ESTADOS UNIDOS", "UM"),
            New Pais(233, "ESTONIA", "EE"),
            New Pais(231, "ETIOPIA", "ET"),
            New Pais(234, "ISLAS FEROE", "FO"),
            New Pais(608, "FILIPINAS", "PH"),
            New Pais(246, "FINLANDIA", "FI"),
            New Pais(242, "FIYI", "FJ"),
            New Pais(250, "FRANCIA", "FR"),
            New Pais(266, "GABON", "GA"),
            New Pais(270, "GAMBIA", "GM"),
            New Pais(268, "GEORGIA", "GE"),
            New Pais(239, "ISLAS GEORGIAS DEL SUR Y SANDWICH DEL SUR", "GS"),
            New Pais(288, "GHANA", "GH"),
            New Pais(292, "GIBRALTAR", "GI"),
            New Pais(308, "GRANADA", "GD"),
            New Pais(300, "GRECIA", "GR"),
            New Pais(304, "GROENLANDIA", "GL"),
            New Pais(312, "GUADALUPE", "GP"),
            New Pais(316, "GUAM", "GU"),
            New Pais(320, "GUATEMALA", "GT"),
            New Pais(254, "GUAYANA FRANCESA", "GF"),
            New Pais(831, "GUERNSEY", "GG"),
            New Pais(324, "GUINEA", "GN"),
            New Pais(226, "GUINEA ECUATORIAL", "GQ"),
            New Pais(624, "GUINEA-BISSAU", "GW"),
            New Pais(328, "GUYANA", "GY"),
            New Pais(332, "HAITI", "HT"),
            New Pais(334, "ISLAS HEARD Y MCDONALD", "HM"),
            New Pais(340, "HONDURAS", "HN"),
            New Pais(344, "HONG KONG", "HK"),
            New Pais(348, "HUNGRIA", "HU"),
            New Pais(356, "INDIA", "IN"),
            New Pais(360, "INDONESIA", "ID"),
            New Pais(364, "IRAN", "IR"),
            New Pais(368, "IRAQ", "IQ"),
            New Pais(372, "IRLANDA", "IE"),
            New Pais(352, "ISLANDIA", "IS"),
            New Pais(376, "ISRAEL", "IL"),
            New Pais(380, "ITALIA", "IT"),
            New Pais(388, "JAMAICA", "JM"),
            New Pais(392, "JAPON", "JP"),
            New Pais(832, "JERSEY", "JE"),
            New Pais(400, "JORDANIA", "JO"),
            New Pais(398, "KAZAJISTAN", "KZ"),
            New Pais(404, "KENIA", "KE"),
            New Pais(417, "KIRGUISTAN", "KG"),
            New Pais(296, "KIRIBATI", "KI"),
            New Pais(414, "KUWAIT", "KW"),
            New Pais(418, "LAOS", "LA"),
            New Pais(426, "LESOTO", "LS"),
            New Pais(428, "LETONIA", "LV"),
            New Pais(422, "LIBANO", "LB"),
            New Pais(430, "LIBERIA", "LR"),
            New Pais(434, "LIBIA", "LY"),
            New Pais(438, "LIECHTENSTEIN", "LI"),
            New Pais(440, "LITUANIA", "LT"),
            New Pais(442, "LUXEMBURGO", "LU"),
            New Pais(446, "MACAO", "MO"),
            New Pais(807, "MACEDONIA", "MK"),
            New Pais(450, "MADAGASCAR", "MG"),
            New Pais(458, "MALASIA", "MY"),
            New Pais(454, "MALAWI", "MW"),
            New Pais(462, "MALDIVAS", "MV"),
            New Pais(466, "MALI", "ML"),
            New Pais(470, "MALTA", "MT"),
            New Pais(238, "ISLAS MALVINAS", "FK"),
            New Pais(833, "ISLA DE MAN", "IM"),
            New Pais(580, "ISLAS MARIANAS DEL NORTE", "MP"),
            New Pais(504, "MARRUECOS", "MA"),
            New Pais(584, "ISLAS MARSHALL", "MH"),
            New Pais(474, "MARTINICA", "MQ"),
            New Pais(480, "MAURICIO", "MU"),
            New Pais(478, "MAURITANIA", "MR"),
            New Pais(175, "MAYOTTE", "YT"),
            New Pais(484, "MEXICO", "MX"),
            New Pais(583, "MICRONESIA", "FM"),
            New Pais(498, "MOLDAVIA", "MD"),
            New Pais(492, "MONACO", "MC"),
            New Pais(496, "MONGOLIA", "MN"),
            New Pais(499, "MONTENEGRO", "ME"),
            New Pais(500, "MONTSERRAT", "MS"),
            New Pais(508, "MOZAMBIQUE", "MZ"),
            New Pais(104, "MYANMAR", "MM"),
            New Pais(516, "NAMIBIA", "NA"),
            New Pais(520, "NAURU", "NR"),
            New Pais(162, "ISLA DE NAVIDAD", "CX"),
            New Pais(524, "NEPAL", "NP"),
            New Pais(558, "NICARAGUA", "NI"),
            New Pais(562, "NIGER", "NE"),
            New Pais(566, "NIGERIA", "NG"),
            New Pais(570, "NIUE", "NU"),
            New Pais(574, "NORFOLK", "NF"),
            New Pais(578, "NORUEGA", "NO"),
            New Pais(540, "NUEVA CALEDONIA", "NC"),
            New Pais(554, "NUEVA ZELANDA", "NZ"),
            New Pais(512, "OMAN", "OM"),
            New Pais(528, "HOLANDA", "NL"),
            New Pais(586, "PAKISTAN", "PK"),
            New Pais(585, "PALAOS", "PW"),
            New Pais(275, "PALESTINA (ANP)", "PS"),
            New Pais(591, "PANAMA", "PA"),
            New Pais(598, "PAPUA NUEVA GUINEA", "PG"),
            New Pais(600, "PARAGUAY", "PY"),
            New Pais(604, "PERU", "PE"),
            New Pais(612, "ISLAS PITCAIRN", "PN"),
            New Pais(258, "POLINESIA FRANCESA", "PF"),
            New Pais(616, "POLONIA", "PL"),
            New Pais(620, "PORTUGAL", "PT"),
            New Pais(630, "PUERTO RICO", "PR"),
            New Pais(634, "QATAR", "QA"),
            New Pais(826, "REINO UNIDO", "GB"),
            New Pais(638, "REUNION", "RE"),
            New Pais(646, "RUANDA", "RW"),
            New Pais(642, "RUMANIA", "RO"),
            New Pais(643, "RUSIA", "RU"),
            New Pais(732, "SAHARA OCCIDENTAL", "EH"),
            New Pais(90, "ISLAS SALOMON", "SB"),
            New Pais(882, "SAMOA", "WS"),
            New Pais(16, "SAMOA AMERICANA", "AS"),
            New Pais(659, "SAINT KITTS AND NEVIS", "KN"),
            New Pais(674, "SAN MARINO", "SM"),
            New Pais(666, "SAN PEDRO Y MIQUELON", "PM"),
            New Pais(670, "SAN VICENTE Y LAS GRANADINAS", "VC"),
            New Pais(654, "SANTA HELENA", "SH"),
            New Pais(662, "SANTA LUCIA", "LC"),
            New Pais(678, "SANTO TOME Y PRINCIPE", "ST"),
            New Pais(686, "SENEGAL", "SN"),
            New Pais(688, "SERBIA", "RS"),
            New Pais(690, "SEYCHELLES", "SC"),
            New Pais(694, "SIERRA LEONA", "SL"),
            New Pais(702, "SINGAPUR", "SG"),
            New Pais(760, "SIRIA", "SY"),
            New Pais(706, "SOMALIA", "SO"),
            New Pais(144, "SRI LANKA", "LK"),
            New Pais(748, "SUAZILANDIA", "SZ"),
            New Pais(710, "SUDAFRICA", "ZA"),
            New Pais(736, "SUDAN", "SD"),
            New Pais(752, "SUECIA", "SE"),
            New Pais(756, "SUIZA", "CH"),
            New Pais(740, "SURINAM", "SR"),
            New Pais(744, "SVALBARD Y JAN MAYEN", "SJ"),
            New Pais(764, "TAILANDIA", "TH"),
            New Pais(158, "TAIWAN", "TW"),
            New Pais(834, "TANZANIA", "TZ"),
            New Pais(762, "TAYIKISTAN", "TJ"),
            New Pais(86, "TERRITORIO BRITANICO DEL OCEANO INDICO", "IO"),
            New Pais(260, "TERRITORIOS AUSTRALES FRANCESES", "TF"),
            New Pais(626, "TIMOR ORIENTAL", "TL"),
            New Pais(768, "TOGO", "TG"),
            New Pais(772, "TOKELAU", "TK"),
            New Pais(776, "TONGA", "TO"),
            New Pais(780, "TRINIDAD Y TOBAGO", "TT"),
            New Pais(788, "TUNEZ", "TN"),
            New Pais(796, "ISLAS TURCAS Y CAICOS", "TC"),
            New Pais(795, "TURKMENISTAN", "TM"),
            New Pais(792, "TURQUIA", "TR"),
            New Pais(798, "TUVALU", "TV"),
            New Pais(804, "UCRANIA", "UA"),
            New Pais(800, "UGANDA", "UG"),
            New Pais(858, "URUGUAY", "UY"),
            New Pais(860, "UZBEKISTAN", "UZ"),
            New Pais(548, "VANUATU", "VU"),
            New Pais(336, "CIUDAD DEL VATICANO", "VA"),
            New Pais(862, "VENEZUELA", "VE"),
            New Pais(704, "VIETNAM", "VN"),
            New Pais(92, "ISLAS VIRGENES BRITANICAS", "VG"),
            New Pais(850, "ISLAS VIRGENES ESTADOUNIDENSES", "VI"),
            New Pais(876, "WALLIS Y FUTUNA", "WF"),
            New Pais(887, "YEMEN", "YE"),
            New Pais(262, "YIBUTI", "DJ"),
            New Pais(894, "ZAMBIA", "ZM"),
            New Pais(716, "ZIMBABUE", "ZW")
        }
    End Function

    Public Function RespuestaYaTramitada(respuesta As String) As Boolean Implements IAgencia.RespuestaYaTramitada
        Return respuesta.StartsWith("ENVIO DUPLICADO") OrElse respuesta = "EL ENVIO NO PUEDE SER ACTUALIZADO PORQUE EL CLIENTE NO PERMITE ACTUALIZAR"
    End Function

    Public Class EnvioCEX
        <MaxLength(100)>
        Public Property Solicitante As String
        <MaxLength(30)>
        Public Property CanalEntrada As String = ""
        <MaxLength(16)>
        Public Property NumEnvio As String
        <MaxLength(20)>
        Public Property Ref As String
        <MaxLength(30)>
        Public Property RefCliente As String = ""
        <MaxLength(8)>
        Public Property Fecha As String
        Public Property CodRte As String
        <MaxLength(40)>
        Public Property NomRte As String
        <MaxLength(20)>
        Public Property NifRte As String
        <MaxLength(300)>
        Public Property DirRte As String
        <MaxLength(40)>
        Public Property PobRte As String
        <MaxLength(5)>
        Public Property CodPosNacRte As String
        <MaxLength(2)>
        Public Property PaisISORte As String = ""
        <MaxLength(7)>
        Public Property CodPosIntRte As String = ""
        <MaxLength(40)>
        Public Property ContacRte As String = ""
        <MaxLength(15)>
        Public Property TelefRte As String
        <MaxLength(75)>
        Public Property EmailRte As String
        <MaxLength(9)>
        Public Property CodDest As String = ""
        <MaxLength(40)>
        Public Property NomDest As String
        <MaxLength(20)>
        Public Property NifDest As String = ""
        <MaxLength(300)>
        Public Property DirDest As String
        <MaxLength(40)>
        Public Property PobDest As String
        <MaxLength(5)>
        Public Property CodPosNacDest As String
        <MaxLength(2)>
        Public Property PaisISODest As String
        <MaxLength(7)>
        Public Property CodPosIntDest As String = ""
        <MaxLength(40)>
        Public Property ContacDest As String
        <MaxLength(15)>
        Public Property TelefDest As String
        <MaxLength(75)>
        Public Property EmailDest As String
        <MaxLength(40)>
        Public Property ContacOtrs As String = ""
        <MaxLength(15)>
        Public Property TelefOtrs As String = ""
        <MaxLength(75)>
        Public Property EmailOtrs As String = ""
        <MaxLength(80)>
        Public Property Observac As String
        <MaxLength(2)>
        Public Property NumBultos As String
        <MaxLength(8)>
        Public Property Kilos As String = "1"
        <MaxLength(6)>
        Public Property Volumen As String = ""
        <MaxLength(6)>
        Public Property Alto As String = ""
        <MaxLength(6)>
        Public Property Largo As String = ""
        <MaxLength(6)>
        Public Property Ancho As String = ""
        <MaxLength(2)>
        Public Property Producto As String
        <MaxLength(1)>
        Public Property Portes As String
        <MaxLength(7)>
        Public Property Reembolso As String
        <MaxLength(1)>
        Public Property EntrSabado As String = ""
        <MaxLength(7)>
        Public Property Seguro As String
        <MaxLength(16)>
        Public Property NumEnvioVuelta As String = ""
        <MaxLength(7)>
        Public Property CodDirecDestino As String = ""
        Public Property Password As String = ""

        Public Property ListaBultos As List(Of Bulto)
        Public Property ListaInformacionAdicional As List(Of InformacionAdicional)

    End Class

    Public Class Bulto
        <MaxLength(23)>
        Public Property CodUnico As String
        <MaxLength(2)>
        Public Property Orden As String
        <MaxLength(40)>
        Public Property CodBultoCli As String = ""
        <MaxLength(30)>
        Public Property Referencia As String = ""
        <MaxLength(50)>
        Public Property Descripcion As String = ""
        <MaxLength(50)>
        Public Property Observaciones As String = ""
        <MaxLength(6)>
        Public Property Kilos As String = "1"
        <MaxLength(5)>
        Public Property Volumen As String = ""
        <MaxLength(5)>
        Public Property Alto As String = ""
        <MaxLength(5)>
        Public Property Largo As String = ""
        <MaxLength(5)>
        Public Property Ancho As String = ""
    End Class

    Public Class InformacionAdicional
        <MaxLength(1)>
        Public Property TipoEtiqueta As String = ""
        <MaxLength(1)>
        Public Property EtiquetaPDF As String = ""
        <MaxLength(1)>
        Public Property CreaRecogida As String = ""
        <MaxLength(8)>
        Public Property FechaRecogida As String = ""
        <MaxLength(5)>
        Public Property HoraRecogidaDesde As String = ""
        <MaxLength(5)>
        Public Property HoraRecogidaHasta As String = ""
        <MaxLength(30)>
        Public Property ReferenciaRecogida As String = ""

    End Class

    Public Class RespuestaCEX
        Public Property CodigoRetorno As Integer
        <MaxLength(23)>
        Public Property MensajeRetorno As String
        <MaxLength(16)>
        Public Property DatosResultado As String
        Public Property ListaBultos As List(Of Bulto)
        Public Property Etiqueta As List(Of Object)
        Public Property NumRecogida As Integer?
        <MaxLength(8)>
        Public Property FechaRecogida As String
        <MaxLength(5)>
        Public Property HoraRecogidaDesde As String
        <MaxLength(5)>
        Public Property HoraRecogidaHasta As String
        <MaxLength(300)>
        Public Property DireccionRecogida As String
        <MaxLength(40)>
        Public Property PoblacionRecogida As String
    End Class
End Class
