Imports System.IO
Public Class TxrcSbinDecryptor
    Dim infile As String
    Sub New(fn As String)
        infile = fn
    End Sub
    Public Sub Decrypt(OutPath As String)
        Dim indata = File.ReadAllBytes(infile)
        Dim outdata(indata.Length - 1) As Byte
        Decrypt(indata, outdata)
        File.WriteAllBytes(OutPath, outdata)
    End Sub
    Private Sub Decrypt(ifp As Byte(), ByRef ofp As Byte())
        Dim r0 As Integer = 0
        Dim r1 As Integer = 0
        Dim r2 As Integer = 0
        Dim r3 As Integer = 0
        Dim r4 As Integer = 0
        Dim r5 As Integer = 0
        For r4 = 0 To 7
            ofp(r4) = ifp(r4)
        Next
        r2 = ifp(7)
        For r5 = 8 To ifp.Length - 1
            r0 = r5 \ 3
            r4 = r5 Mod 3
            r3 = ifp(r4 + 4)
            r1 = ifp(r5)
            r0 = r2 * r0 + r3
            r0 = r0 And 255
            r0 = r0 Xor r1
            r0 = r0 And 255
            ofp(r5) = r0
        Next
    End Sub

End Class
