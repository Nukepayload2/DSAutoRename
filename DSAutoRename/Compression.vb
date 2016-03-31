Imports System.IO.Compression
Imports System.IO
Imports System.Runtime
Imports System.Runtime.InteropServices


Public Class Compression
    Public Shared Function CompressAllBytes(data As Byte()) As Byte()
        Dim ms As New MemoryStream()
        Dim strm As New DeflateStream(ms, CompressionMode.Compress)
        strm.Write(data, 0, data.Length)
        strm.Close()
        Return ms.ToArray
    End Function
    Public Shared Function DecompressAllBytes(data As Byte(), OriginalSize As Integer) As Byte()
        Dim ms As New MemoryStream(data)
        Dim strm As New DeflateStream(ms, CompressionMode.Decompress)
        Dim dat(OriginalSize - 1) As Byte
        strm.Read(dat, 0, dat.Length)
        strm.Close()
        Return dat
    End Function

End Class


