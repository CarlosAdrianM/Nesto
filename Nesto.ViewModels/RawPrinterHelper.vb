Imports System.IO
Imports System.Drawing.Printing
Imports System.Runtime.InteropServices

Public Class RawPrinterHelper
    ' Structure and API declarions: 
    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Unicode)>
    Structure DOCINFOW
        <MarshalAs(UnmanagedType.LPWStr)> Public pDocName As String
        <MarshalAs(UnmanagedType.LPWStr)> Public pOutputFile As String
        <MarshalAs(UnmanagedType.LPWStr)> Public pDataType As String
    End Structure

    <DllImport("winspool.Drv", EntryPoint:="OpenPrinterW",
        SetLastError:=True, CharSet:=CharSet.Unicode,
        ExactSpelling:=True, CallingConvention:=CallingConvention.StdCall)>
    Public Shared Function OpenPrinter(ByVal src As String, ByRef hPrinter As IntPtr, ByVal pd As Long) As Boolean
    End Function
    <DllImport("winspool.Drv", EntryPoint:="ClosePrinter",
        SetLastError:=True, CharSet:=CharSet.Unicode,
        ExactSpelling:=True, CallingConvention:=CallingConvention.StdCall)>
    Public Shared Function ClosePrinter(ByVal hPrinter As IntPtr) As Boolean
    End Function
    <DllImport("winspool.Drv", EntryPoint:="StartDocPrinterW",
        SetLastError:=True, CharSet:=CharSet.Unicode,
        ExactSpelling:=True, CallingConvention:=CallingConvention.StdCall)>
    Public Shared Function StartDocPrinter(ByVal hPrinter As IntPtr, ByVal level As Int32, ByRef pDI As DOCINFOW) As Boolean
    End Function
    <DllImport("winspool.Drv", EntryPoint:="EndDocPrinter",
        SetLastError:=True, CharSet:=CharSet.Unicode,
        ExactSpelling:=True, CallingConvention:=CallingConvention.StdCall)>
    Public Shared Function EndDocPrinter(ByVal hPrinter As IntPtr) As Boolean
    End Function
    <DllImport("winspool.Drv", EntryPoint:="StartPagePrinter",
        SetLastError:=True, CharSet:=CharSet.Unicode,
        ExactSpelling:=True, CallingConvention:=CallingConvention.StdCall)>
    Public Shared Function StartPagePrinter(ByVal hPrinter As IntPtr) As Boolean
    End Function
    <DllImport("winspool.Drv", EntryPoint:="EndPagePrinter",
        SetLastError:=True, CharSet:=CharSet.Unicode,
        ExactSpelling:=True, CallingConvention:=CallingConvention.StdCall)>
    Public Shared Function EndPagePrinter(ByVal hPrinter As IntPtr) As Boolean
    End Function
    <DllImport("winspool.Drv", EntryPoint:="WritePrinter",
        SetLastError:=True, CharSet:=CharSet.Unicode,
        ExactSpelling:=True, CallingConvention:=CallingConvention.StdCall)>
    Public Shared Function WritePrinter(ByVal hPrinter As IntPtr, ByVal pBytes As IntPtr, ByVal dwCount As Int32, ByRef dwWritten As Int32) As Boolean
    End Function

    ' SendBytesToPrinter() 
    ' When the function is given a printer name and an unmanaged array of    
    ' bytes, the function sends those bytes to the print queue. 
    ' Returns True on success or False on failure. 
    Public Shared Function SendBytesToPrinter(ByVal szPrinterName As String, ByVal pBytes As IntPtr, ByVal dwCount As Int32) As Boolean
        Return EnviarBytes(szPrinterName, pBytes, dwCount).Exito
    End Function ' SendBytesToPrinter() 

    ''' <summary>
    ''' NestoAPI#847: como SendBytesToPrinter, pero dice en qué paso falló y con qué código de Windows. El código se lee
    ''' JUSTO tras el paso que falla (antes se leía después de ClosePrinter, que lo pisaba, y además no se devolvía).
    ''' Cuenta también que WritePrinter escriba menos bytes de los mandados.
    ''' </summary>
    Public Shared Function EnviarBytes(ByVal szPrinterName As String, ByVal pBytes As IntPtr, ByVal dwCount As Int32) As ResultadoEnvioImpresora
        Dim hPrinter As IntPtr
        Dim di As New DOCINFOW With {.pDocName = "Nesto - etiqueta", .pDataType = "RAW"}
        Dim dwWritten As Int32
        Dim codigo As Integer = 0
        Dim paso As String = Nothing

        If Not OpenPrinter(szPrinterName, hPrinter, 0) Then
            codigo = Marshal.GetLastWin32Error()
            Return Fallo("no se ha podido abrir la impresora", codigo)
        End If
        Try
            If Not StartDocPrinter(hPrinter, 1, di) Then
                codigo = Marshal.GetLastWin32Error()
                paso = "no se ha podido empezar el documento"
            Else
                Try
                    If Not StartPagePrinter(hPrinter) Then
                        codigo = Marshal.GetLastWin32Error()
                        paso = "no se ha podido empezar la página"
                    Else
                        If Not WritePrinter(hPrinter, pBytes, dwCount, dwWritten) Then
                            codigo = Marshal.GetLastWin32Error()
                            paso = "no se ha podido escribir en la impresora"
                        ElseIf dwWritten <> dwCount Then
                            paso = $"la impresora solo ha recibido {dwWritten} de {dwCount} bytes"
                        End If
                        EndPagePrinter(hPrinter)
                    End If
                Finally
                    If Not EndDocPrinter(hPrinter) AndAlso paso Is Nothing Then
                        codigo = Marshal.GetLastWin32Error()
                        paso = "no se ha podido cerrar el documento"
                    End If
                End Try
            End If
        Finally
            ClosePrinter(hPrinter)
        End Try

        If paso Is Nothing Then
            Return ResultadoEnvioImpresora.Correcto()
        End If
        Return Fallo(paso, codigo)
    End Function

    Private Shared Function Fallo(paso As String, codigo As Integer) As ResultadoEnvioImpresora
        Dim detalle = paso
        If codigo <> 0 Then
            detalle &= ": " & New System.ComponentModel.Win32Exception(codigo).Message
        End If
        Return New ResultadoEnvioImpresora With {.Exito = False, .CodigoError = codigo, .Detalle = detalle}
    End Function

    ' SendFileToPrinter() 
    ' When the function is given a file name and a printer name, 
    ' the function reads the contents of the file and sends the 
    ' contents to the printer. 
    ' Presumes that the file contains printer-ready data. 
    ' Shows how to use the SendBytesToPrinter function. 
    ' Returns True on success or False on failure. 
    Public Shared Function SendFileToPrinter(ByVal szPrinterName As String, ByVal szFileName As String) As Boolean
        ' Open the file. 
        Dim fs As New FileStream(szFileName, FileMode.Open)
        ' Create a BinaryReader on the file. 
        Dim br As New BinaryReader(fs)
        ' Dim an array of bytes large enough to hold the file's contents. 
        Dim bytes(fs.Length) As Byte
        Dim bSuccess As Boolean
        ' Your unmanaged pointer. 
        Dim pUnmanagedBytes As IntPtr

        ' Read the contents of the file into the array. 
        bytes = br.ReadBytes(fs.Length)
        ' Allocate some unmanaged memory for those bytes. 
        pUnmanagedBytes = Marshal.AllocCoTaskMem(fs.Length)
        ' Copy the managed byte array into the unmanaged array. 
        Marshal.Copy(bytes, 0, pUnmanagedBytes, fs.Length)
        ' Send the unmanaged bytes to the printer. 
        bSuccess = SendBytesToPrinter(szPrinterName, pUnmanagedBytes, fs.Length)
        ' Free the unmanaged memory that you allocated earlier. 
        Marshal.FreeCoTaskMem(pUnmanagedBytes)
        Return bSuccess
    End Function ' SendFileToPrinter() 

    ' When the function is given a string and a printer name, 
    ' the function sends the string to the printer as raw bytes. 
    ' NestoAPI#847: antes no devolvía nada (quien llamaba no podía saber si había salido). Ahora True/False.
    Public Shared Function SendStringToPrinter(ByVal szPrinterName As String, ByVal szString As String) As Boolean
        Return EnviarTexto(szPrinterName, szString).Exito
    End Function

    ''' <summary>NestoAPI#847: manda el texto en ANSI y dice qué pasó (paso y código de Windows si falla).</summary>
    Public Shared Function EnviarTexto(ByVal szPrinterName As String, ByVal szString As String) As ResultadoEnvioImpresora
        ' Assume that the printer is expecting ANSI text, and then convert the string to ANSI text. 
        Dim texto As String = If(szString, String.Empty)
        Dim dwCount As Int32 = texto.Length()
        Dim pBytes As IntPtr = Marshal.StringToCoTaskMemAnsi(texto)
        Try
            Return EnviarBytes(szPrinterName, pBytes, dwCount)
        Finally
            Marshal.FreeCoTaskMem(pBytes)
        End Try
    End Function
End Class
