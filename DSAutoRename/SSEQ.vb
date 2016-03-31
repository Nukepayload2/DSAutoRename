Public Class SSEQ

    Public Structure sSSEQ
        Public generic As NitroHeader
        Public data As sData
        Public Structure NitroHeader
            Public id As Char()
            Public endianess As UShort
            Public constant As UShort
            Public filesize As UInteger
            Public headersize As UShort
            Public nSection As UShort
        End Structure
        Public Structure sData
            Public type As Char()
            Public size As UInteger
            Public offset As UInteger
            Public events As List(Of sEvent)
        End Structure
        Public Structure sEvent
            Public status As Byte
            Public parameters As Byte()
        End Structure
    End Structure
End Class
