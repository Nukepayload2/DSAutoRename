Option Strict On

Imports System.IO
Imports System.Text

Public Class RawFileRenamer
    Sub renamefiles(dirname As String)
        For Each f As String In Directory.GetFiles(dirname)
            Try
                auto_rename(f)
            Catch ex As Exception
                If System.Windows.MessageBox.Show("error:" + ex.ToString + vbLf + f, "err", System.Windows.MessageBoxButton.YesNo) = MessageBoxResult.No Then Return
            End Try
        Next
    End Sub
    Function ByteToStr(b() As Byte) As String
        Dim sb As New StringBuilder
        For i As Integer = 0 To b.Length - 1
            sb.Append(Chr(b(i)))
        Next
        Return sb.ToString
    End Function
    Public Shared Sub ChangeExt(fn As String, s As String)
        If Path.GetFileName(fn).Contains(".") Then
            Dim f As String = fn
            f = Path.GetDirectoryName(f) + "\" + Path.GetFileNameWithoutExtension(f) + "."
            File.Move(fn, f + s)
        Else
            File.Move(fn, fn + "." + s)
        End If
    End Sub
    Sub auto_rename(fn As String)
        If Not File.Exists(fn) Then Return
        Dim fs As New FileStream(fn, FileMode.Open)
        If fs.Length = 0 Then
            fs.Close()
            ChangeExt(fn, "empty")
            Return
        End If
        If fs.Length < 4 Then Return
        Dim first4(3) As Byte
        fs.Read(first4, 0, 4)
        Dim head = ByteToStr(first4)
        Dim REVntable As New List(Of String)({"NCGR", "NCER", "NCLR", "NSCR", "SWAR", "SWAV", "NANR"})
        Dim ntable As New List(Of String)({"SBIN", "TERM", "SSEQ", "SBNK", "STRM", "SDAT", "NARC"})
        Dim revhead = StrReverse(head)
        'littleendian
        If REVntable.Contains(revhead) Then
            fs.Close()
            ChangeExt(fn, revhead)
            Return
        End If
        'bigendian
        If ntable.Contains(head) Then
            fs.Close()
            ChangeExt(fn, head)
            Return
        End If
        '3d模型
        Dim ext1 As String = ""
        Select Case head
            Case "BCA0"
                ext1 = "NSBCA"
            Case "BTX0"
                ext1 = "NSBTX"
            Case "BTA0"
                ext1 = "NSBTA"
            Case "BMD0"
                ext1 = "NSBMD"
            Case "BMA0"
                ext1 = "NSBMA"
        End Select
        If ext1.Length <> 0 Then
            fs.Close()
            ChangeExt(fn, ext1)
            Return
        End If
        'LZ77和其他
        fs.Seek(0, SeekOrigin.Begin)
        Dim r As Integer = fs.ReadByte
        If r = &H10 Then
            fs.Close()
            ChangeExt(fn, "LZ77")
            Return
        ElseIf r = &H11 Then
            fs.Close()
            ChangeExt(fn, "LZ11")
            Return
        ElseIf r = &H18 AndAlso fs.ReadByte = &H56 AndAlso fs.ReadByte = 1 Then
            fs.Close()
            ChangeExt(fn, "arc")
            Return
        Else
            fs.Seek(0, SeekOrigin.Begin)
            Dim re As New BinaryReader(fs)
            Dim filecount As Integer = re.ReadInt32()
            Dim ls As New List(Of Mix3DInfo)
            If filecount <= 255 AndAlso filecount > 0 Then '可能是需要拆的3d模型
                Dim filenameblocksize As Integer = re.ReadInt32
                If filenameblocksize - 8 <= 0 Then Return
                If filenameblocksize > 255 Then Return
                For i As Integer = 1 To filecount
                    If re.BaseStream.Position > re.BaseStream.Length Then Return
                    Dim inf As New Mix3DInfo
                    inf.offset = re.ReadInt32
                    inf.length = re.ReadInt32
                    If (inf.length = 0 OrElse inf.length = 1) AndAlso (inf.offset = 0 OrElse inf.offset = 1) Then
                        Dim name(15) As Byte
                        re.BaseStream.Read(name, 0, name.Length)
                        inf.name = ByteToStr(name)
                        inf.offset = re.ReadInt32
                        inf.length = re.ReadInt32
                        re.BaseStream.Position += 8
                    Else
                        Dim name(filenameblocksize - 9) As Byte
                        re.BaseStream.Read(name, 0, name.Length)
                        inf.name = ByteToStr(name)
                    End If
                    ls.Add(inf)
                Next
                For Each inf In ls
                    re.BaseStream.Seek(inf.offset, 0)
                    Dim data(inf.length - 1) As Byte
                    re.BaseStream.Read(data, 0, data.Length)
                    Dim fn1 As String = Path.GetDirectoryName(fn) + "\" + inf.name.Substring(0, inf.name.IndexOf(Chr(0)))
                    File.WriteAllBytes(fn1, data)
                Next
                fs.Close()
                If fn.LastIndexOf("\") > fn.LastIndexOf(".") Then fn += "."
                File.Move(fn, fn.Substring(0, fn.IndexOf(".") + 1) + "nsbarc")
                Return
            End If
        End If
        fs.Close()
    End Sub
    Structure Mix3DInfo
        Dim offset As Integer
        Dim length As Integer
        Dim name As String
    End Structure

End Class
