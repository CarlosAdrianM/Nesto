Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts

''' <summary>NestoAPI#847: lo que pasó al mandar texto en crudo (ZPL/EPL) a una impresora.</summary>
Public Class ResultadoEnvioImpresora
    Public Property Exito As Boolean
    ''' <summary>Marshal.GetLastWin32Error() del paso de winspool que falló (0 si no lo hay o si fue una excepción).</summary>
    Public Property CodigoError As Integer
    ''' <summary>En qué paso falló y la descripción de Windows (o el mensaje de la excepción).</summary>
    Public Property Detalle As String
    Public Property Intentos As Integer

    Public Shared Function Correcto() As ResultadoEnvioImpresora
        Return New ResultadoEnvioImpresora With {.Exito = True, .Detalle = String.Empty}
    End Function
End Class

''' <summary>NestoAPI#847: manda texto en crudo a una impresora (en producción, <see cref="RawPrinterHelper"/>). Sustituible en los tests.</summary>
Public Interface IImpresoraCruda
    Function Enviar(impresora As String, texto As String) As ResultadoEnvioImpresora
End Interface

Public Class ImpresoraCrudaWindows
    Implements IImpresoraCruda

    Public Function Enviar(impresora As String, texto As String) As ResultadoEnvioImpresora Implements IImpresoraCruda.Enviar
        Return RawPrinterHelper.EnviarTexto(impresora, texto)
    End Function
End Class

''' <summary>
''' NestoAPI#847: la primera etiqueta del día a la Zebra «no salía» y nadie se enteraba, porque se ignoraba el resultado
''' del envío en crudo. Aquí: un intento, si falla UNA espera corta (despierta la impresora o la red) y un reintento; si
''' vuelve a fallar, se registra en ELMAH (impresora y código de Windows) y se avisa al usuario de que la reimprima.
''' Nunca lanza: lo que se imprime es de algo ya tramitado y un fallo de la impresora no debe deshacer el flujo.
''' </summary>
Public Class EnviadorEtiquetasImpresora
    Public Shared ReadOnly EsperaAntesDeReintentar As TimeSpan = TimeSpan.FromMilliseconds(1500)

    Private ReadOnly _impresora As IImpresoraCruda
    Private ReadOnly _esperar As Func(Of TimeSpan, Task)
    Private ReadOnly _avisar As Action(Of String)
    Private ReadOnly _registrar As Action(Of String, String)

    Public Sub New(impresora As IImpresoraCruda, esperar As Func(Of TimeSpan, Task), avisar As Action(Of String), registrar As Action(Of String, String))
        _impresora = If(impresora, New ImpresoraCrudaWindows())
        _esperar = If(esperar, Function(t) Task.Delay(t))
        _avisar = avisar
        _registrar = registrar
    End Sub

    ''' <summary>
    ''' El de producción: avisa con <paramref name="dialogos"/> (o, si no se da, con el del contenedor, siempre en el hilo de
    ''' UI) y registra en ELMAH por POST /api/Errores (IServicioRegistroErrores).
    ''' </summary>
    Public Shared Function Predeterminado(Optional dialogos As IServicioDialogos = Nothing) As EnviadorEtiquetasImpresora
        Return New EnviadorEtiquetasImpresora(New ImpresoraCrudaWindows(), Nothing,
            Sub(mensaje)
                Try
                    Dim servicio = dialogos
                    If servicio Is Nothing Then
                        Dim delContenedor = Contenedor.Actual?.Resolver(Of IServicioDialogos)()
                        If delContenedor IsNot Nothing Then
                            servicio = New ServicioDialogosEnHiloUi(delContenedor)
                        End If
                    End If
                    servicio?.ShowError(mensaje)
                Catch
                    ' nunca lanzar
                End Try
            End Sub,
            Sub(mensaje, contexto)
                Try
                    Dim servicioErrores = Contenedor.Actual?.Resolver(Of IServicioRegistroErrores)()
                    Dim unused = servicioErrores?.RegistrarErrorAsync(New Exception(mensaje), contexto)
                Catch
                    ' nunca lanzar
                End Try
            End Sub)
    End Function

    ''' <summary>True si la impresora aceptó el envío (al primer intento o al reintento). Nunca lanza.</summary>
    Public Async Function EnviarAsync(impresora As String, texto As String, Optional contexto As String = Nothing) As Task(Of Boolean)
        Dim resultado = Intentar(impresora, texto)
        Dim intentos = 1
        If Not resultado.Exito Then
            Try
                Await _esperar(EsperaAntesDeReintentar)
            Catch
                ' si la espera falla, se reintenta igual
            End Try
            resultado = Intentar(impresora, texto)
            intentos = 2
        End If
        resultado.Intentos = intentos
        If resultado.Exito Then
            Return True
        End If

        Try
            _registrar?.Invoke(MensajeParaRegistro(impresora, resultado), If(contexto, "EnviadorEtiquetasImpresora"))
        Catch
        End Try
        Try
            _avisar?.Invoke(MensajeParaUsuario(impresora, resultado))
        Catch
        End Try
        Return False
    End Function

    Private Function Intentar(impresora As String, texto As String) As ResultadoEnvioImpresora
        If String.IsNullOrWhiteSpace(impresora) Then
            Return New ResultadoEnvioImpresora With {.Exito = False, .Detalle = "no hay impresora configurada"}
        End If
        Try
            Return If(_impresora.Enviar(impresora, texto),
                      New ResultadoEnvioImpresora With {.Exito = False, .Detalle = "sin resultado"})
        Catch ex As Exception
            Return New ResultadoEnvioImpresora With {.Exito = False, .Detalle = ex.Message}
        End Try
    End Function

    Public Shared Function MensajeParaUsuario(impresora As String, resultado As ResultadoEnvioImpresora) As String
        Dim nombre = If(String.IsNullOrWhiteSpace(impresora), "(sin configurar)", impresora.Trim())
        Dim mensaje = $"No se ha podido enviar la etiqueta a la impresora «{nombre}». Compruebe que está encendida y vuelva a imprimirla."
        Dim detalle = DetalleTecnico(resultado)
        If detalle <> String.Empty Then
            mensaje &= Environment.NewLine & Environment.NewLine & $"Detalle: {detalle}"
        End If
        Return mensaje
    End Function

    Public Shared Function MensajeParaRegistro(impresora As String, resultado As ResultadoEnvioImpresora) As String
        Return $"NestoAPI#847: etiqueta no enviada a la impresora «{impresora}» tras {resultado?.Intentos} intento(s). {DetalleTecnico(resultado)}".Trim()
    End Function

    Private Shared Function DetalleTecnico(resultado As ResultadoEnvioImpresora) As String
        If resultado Is Nothing Then
            Return String.Empty
        End If
        Dim partes As New List(Of String)
        If Not String.IsNullOrWhiteSpace(resultado.Detalle) Then
            partes.Add(resultado.Detalle.Trim())
        End If
        If resultado.CodigoError <> 0 Then
            partes.Add($"(código de Windows {resultado.CodigoError})")
        End If
        Return String.Join(" ", partes)
    End Function
End Class
