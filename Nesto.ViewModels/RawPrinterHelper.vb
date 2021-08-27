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
        Dim hPrinter As IntPtr         ' The printer handle. 
        Dim dwError As Int32            ' Last error - in case there was trouble. 
        Dim di As DOCINFOW               ' Describes your document (name, port, data type). 
        Dim dwWritten As Int32         ' The number of bytes written by WritePrinter(). 
        Dim bSuccess As Boolean       ' Your success code. 

        ' Set up the DOCINFO structure. 
        With di
            .pDocName = "My Visual Basic .NET RAW Document"
            .pDataType = "RAW"
        End With
        ' Assume failure unless you specifically succeed. 
        bSuccess = False
        If OpenPrinter(szPrinterName, hPrinter, 0) Then
            If StartDocPrinter(hPrinter, 1, di) Then
                If StartPagePrinter(hPrinter) Then
                    ' Write your printer-specific bytes to the printer. 
                    bSuccess = WritePrinter(hPrinter, pBytes, dwCount, dwWritten)
                    EndPagePrinter(hPrinter)
                End If
                EndDocPrinter(hPrinter)
            End If
            ClosePrinter(hPrinter)
        End If
        ' If you did not succeed, GetLastError may give more information 
        ' about why not. 
        If bSuccess = False Then
            dwError = Marshal.GetLastWin32Error()
        End If
        Return bSuccess
    End Function ' SendBytesToPrinter() 

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
    Public Shared Function SendStringToPrinter(ByVal szPrinterName As String, ByVal szString As String)
        Dim pBytes As IntPtr
        Dim dwCount As Int32
        ' How many characters are in the string? 
        dwCount = szString.Length()
        ' Assume that the printer is expecting ANSI text, and then convert 
        ' the string to ANSI text. 
        pBytes = Marshal.StringToCoTaskMemAnsi(szString)
        ' Send the converted ANSI string to the printer. 
        SendBytesToPrinter(szPrinterName, pBytes, dwCount)
        Marshal.FreeCoTaskMem(pBytes)
    End Function
End Class