Imports DSAutoRename.SDAT
Imports System.IO

Public Class SSAR
    Public Structure sSSAR
        Dim Header As NitroHeader
        Dim Data As sData
        Structure sData
            Dim Data As Char()
            Dim Size As Integer
            Dim DataOffset As Integer
            Dim Count As Integer
            Dim Records As SSARREC()
        End Structure
        Structure SSARREC
            Dim Offset As Integer
            Dim Bank As Short
            Dim Volume As Byte
            Dim ChannelPressure As Byte
            Dim PolyphonicPressure As Byte
            Dim Play As Byte
            Dim Reserved As Short
        End Structure
    End Structure
    Dim fs As Stream
    Private Sub LoadSSAR()

    End Sub
    Sub New(FileName As String)
        fs = File.OpenRead(FileName)
        LoadSSAR()
    End Sub
    Sub New(Strm As Stream)
        fs = Strm
        LoadSSAR()
    End Sub

End Class
