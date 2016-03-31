Imports System.Runtime.InteropServices
Imports System.IO
Imports System.IO.Compression
Imports System.Text
''' <summary>
''' 3DS的Pack
''' </summary>
Public Class Pack
    <StructLayout(LayoutKind.Sequential, charset:=CharSet.Ansi, pack:=1)>
    Structure Img
        Dim ImgHeader As Header
        Dim Zeros As Byte()
        'FieldOffset &H810
        Dim Records As Record()
        Dim IDTables As IDTable()
        Dim Packages As sPack()
        Property FilePath As String
        Structure Header
            Dim Unk1 As Integer
            Dim Unk2 As Integer
            Dim Unk3 As Integer
            Dim Padding1 As Integer
            Dim RecordCount As Integer
            Dim CompSubFileCount As Integer
            Dim Unk5 As Integer
            Dim Padding2 As Integer
            Dim Padding3 As Integer
            Dim SubFileCount As Integer
        End Structure
        '<StructLayout(LayoutKind.Sequential, charset:=CharSet.Ansi, pack:=1)>
        Structure Record
            Dim Flags As Integer '是一串记录的开头的话&H8000000中间有的是&H20000000, 空记录和某些小Pak文件是&H30800000
            '<MarshalAs(UnmanagedType.ByValTStr, sizeconst:=4)>
            Dim FileType As Integer 'Char() '全是0的话就说明是空记录,不要继续读下去了。
            Dim Padding As Integer '0
            Dim DecompressedPackSize As Integer
            Dim DataOffsetInPack As Integer
            'Property IsValid As Boolean
        End Structure
        Structure IDTable
            Dim Key As Integer
            Dim ID As Integer
        End Structure
        Sub New(FileName As String)
            FilePath = Path.GetFullPath(FileName)
            With File.OpenRead(FileName)
                Dim buf(Marshal.SizeOf(ImgHeader) - 1) As Byte
                .Read(buf, 0, buf.Length)
                Dim p = New PinnedPointer(Of Byte)(buf).SwitchType(Of Header)()
                ImgHeader = p.Target
                p.Dispose()
                Dim RecordLength = ImgHeader.RecordCount * (20 + 8)
                Dim PackOfs = &H810 '+ RecordLength
                .Position = PackOfs
                ReDim buf(RecordLength - 1)
                .Read(buf, 0, buf.Length)
                Dim pr = New PinnedPointer(Of Byte)(buf).SwitchType(Of Record)()
                ReDim Records(ImgHeader.RecordCount - 1)
                'Dim sb As New StringBuilder

                For i As Integer = 0 To ImgHeader.RecordCount - 1
                    Records(i) = pr(i)
                     
                    'sb.AppendFormat("DataOffsetInPack={0},DecompressedPackSize={1},FileType={2},Flags={3},Padding={4}" + vbCrLf, Records(i).DataOffsetInPack, Records(i).DecompressedPackSize, Int32MultiByteBitToString(Records(i).FileType), "&H" + Records(i).Flags.ToString("X"), Records(i).Padding)
                Next
                pr.Dispose()
                'IO.File.WriteAllText("z:\pack.txt", sb.ToString)
                'Stop
                '拆包的话实现到这里就够了
                .Close()
            End With
        End Sub
        Public Sub DumpPacks(Folder As String)
            If Not Directory.Exists(Folder) Then
                Directory.CreateDirectory(Folder)
            End If
            Dim Idx As Integer = 0
            Dim PackOfs = &H28000
            Dim re = File.OpenRead(FilePath)
            Dim br = New BinaryReader(re)
            With re
                .Position = PackOfs
                Dim buf(32 * 1024 - 1) As Byte
                Dim Remain = .Length - .Position
                Dim positions As New List(Of Long)

                Dim ValidRecords = From r In Records Where r.FileType <> 0
                '扫描pak文件
                While Remain >= buf.Length
                    .Read(buf, 0, buf.Length)
                    For i As Integer = 0 To buf.Length - 5
                        If BitConverter.ToInt32(buf, i) = &H4B434150 Then
                            positions.Add(.Position - buf.Length + i)
                        End If
                    Next
                    .Seek(-3, SeekOrigin.Current)
                    Remain = .Length - .Position
                End While

                '最后一个是pak，不用过多处理
                ReDim buf(Remain)
                .Read(buf, 0, buf.Length)
                For i As Integer = 0 To buf.Length - 5
                    If BitConverter.ToInt32(buf, i) = &H4B434150 Then
                        positions.Add(.Position - buf.Length + i)
                    End If
                Next
                '多少个非pak文件？
                Debug.WriteLine(ValidRecords.Count - positions.Count)

                '将其余文件的记录插入
                Dim cac As New List(Of Integer)
                For i As Integer = 0 To ValidRecords.Count - 2
                    If ValidRecords(i).DataOffsetInPack = 0 Then

                        Debug.WriteLineIf(cac.Count = 0, "Ins:" & positions(i) - positions(i - 1))
                        cac.Add(ValidRecords(i).DecompressedPackSize)
                    Else
                        Debug.WriteLineIf(cac.Count > 0, "Count:" & cac.Count)
                        If cac.Count > 0 Then
                            For a As Integer = cac.Count - 1 To 0 Step -1
                                positions.Insert(i - cac.Count, positions(i - cac.Count) - cac(a))
                            Next
                            cac.Clear()
                        End If
                    End If
                Next
                .Position = 0
                Dim length As Integer
                For i As Integer = 0 To positions.Count - 2
                    length = positions(i + 1) - positions(i)
                    ReDim buf(length - 1)
                    .Position = positions(i)
                    .Read(buf, 0, buf.Length)
                    File.WriteAllBytes(Folder + "\" + positions(i).ToString("X") + "." + Int32MultiByteBitToString(ValidRecords(i).FileType).Replace(Chr(0), ""), buf) 'Todo ext "." + Records(i).FileType.ToString.Trim
                    ' Debug.WriteLine(buf(0))
                Next
                length = .Length - positions(positions.Count - 1)
                ReDim buf(length - 1)
                .Position = positions(positions.Count - 1)
                .Read(buf, 0, buf.Length)
                File.WriteAllBytes(Folder + "\" + positions(positions.Count - 1).ToString("X") + ".pak", buf) 'Todo ext "." + Records(i).FileType.ToString.Trim
                .Close()
            End With
        End Sub
    End Structure

    Structure ZlibPack
        Dim MagicID As Short '78 9c = &H9c78
        Dim Data As Byte()
        Dim Checksum As Integer
    End Structure
    <StructLayout(LayoutKind.Sequential, charset:=CharSet.Ansi, pack:=1)>
    Structure sPack
        '<MarshalAs(UnmanagedType.ByValTStr, sizeconst:=4)>
        Dim PackHeader As Header
        Dim Records As Record()
        Dim FileNames As List(Of String)
        Dim FileNamesOffsetsTable As Integer()
        Dim Data As Byte()
        Property FilePath As String
        <StructLayout(LayoutKind.Sequential, pack:=1)>
        Structure Header
            Dim FileType As Integer 'Char()
            Dim TypeFlag1 As Short 'Jpeg:0A 20,Tex:0A 30
            Dim RecordCount As Short
            Dim FileNamesLengthTableOffset As Integer
            Dim FileNamesOffset As Integer
            Dim DataOffset As Integer
            Dim TotalDecompressedSize As Integer
            Dim TotalSize As Integer
            Dim Padding1 As Integer '0
        End Structure
        Structure Record
            '<MarshalAs(UnmanagedType.ByValTStr, sizeconst:=4)>
            Dim FileType As Integer 'Char()
            Dim Padding1 As Integer '0
            Dim DecompressedLength As Integer
            Dim DecompressedOffset As Integer '没多大用...
            Dim Option1 As Integer 'HasFile 0,Empty &H80000
            Dim Compressed As Integer 'Compressed:1,Empty 0
            Dim Length As Integer
            Dim Offset As Integer
        End Structure
        Sub New(FileName As String)
            FilePath = Path.GetFullPath(FileName)
            Dim f = File.OpenRead(FilePath)
            With f
                Dim buf(Marshal.SizeOf(PackHeader) - 1) As Byte
                .Read(buf, 0, buf.Length)
                Dim pt As New PinnedPointer(Of Byte)(buf)
                PackHeader = pt.SwitchType(Of Header).Target
                pt.Dispose()
                ReDim buf(Marshal.SizeOf(GetType(Record)) * PackHeader.RecordCount - 1)
                .Read(buf, 0, buf.Length)
                Dim p = New PinnedPointer(Of Byte)(buf).SwitchType(Of Record)()
                ReDim Records(PackHeader.RecordCount - 1)
                For i As Integer = 0 To Records.Length - 1
                    Records(i) = p(i)
                Next
                p.Dispose()
                FileNames = New List(Of String)
                ReDim buf(PackHeader.FileNamesLengthTableOffset - PackHeader.FileNamesOffset - 1)
                .Read(buf, 0, buf.Length)
                ReDim FileNamesOffsetsTable(Records.Length - 1)
                Dim sr As New BinaryReader(f)
                For i As Integer = 0 To FileNamesOffsetsTable.Length - 1
                    FileNamesOffsetsTable(i) = sr.ReadInt32
                Next
                Dim pos As Integer = 0
                Dim sb As New Text.StringBuilder
                For i As Integer = 0 To FileNamesOffsetsTable.Length - 1
                    pos = FileNamesOffsetsTable(i)
                    Do
                        If buf(pos) Then
                            sb.Append(Chr(buf(pos)))
                        Else
                            Dim s = sb.ToString
                            'If Not String.IsNullOrWhiteSpace(s) Then
                            FileNames.Add(s)
                            'End If
                            sb.Clear()
                            Exit Do
                        End If
                        pos += 1
                    Loop
                Next
                '解压文件的话实现到这里就够了
                sr.Close()
            End With
        End Sub
       
        Public Sub DecomressAllFiles()
            With File.OpenRead(FilePath)
                Debug.WriteLine("File:" & Path.GetFileName(FilePath))
                Dim lg As New Text.StringBuilder
                Dim idx As Integer = -1
                Dim pos As Long
                Dim buf As Byte()
                Dim fn As String = ""
                Dim di = Path.Combine(Path.GetDirectoryName(FilePath), Path.GetFileNameWithoutExtension(FilePath))
                If Not Directory.Exists(di) Then
                    Directory.CreateDirectory(di)
                End If
                For Each r In Records
                    idx += 1
                    Dim nam As String = Path.ChangeExtension(FileNames(idx), Int32MultiByteBitToString(r.FileType).TrimEnd)
                    If r.FileType = 0 Then Continue For
                    fn = di + "\" + nam
                    If File.Exists(fn) Then
                        fn = Path.Combine(Path.GetDirectoryName(fn), Path.GetFileNameWithoutExtension(fn)) & .Position.ToString("X") & Path.GetExtension(fn)
                    End If
                    If r.Compressed Then
                        .Position = r.Offset + 2 'Header
                        ReDim buf(r.Length - 6 - 1) 'Header And Checksum
                        .Read(buf, 0, buf.Length)
                        File.WriteAllBytes(fn, Compression.DecompressAllBytes(buf, r.DecompressedLength))
                    Else
                        .Position = r.DecompressedOffset
                        ReDim buf(r.DecompressedLength - 1)
                        .Read(buf, 0, buf.Length)
                        File.WriteAllBytes(fn, buf)
                    End If

                    pos = .Position
                    With lg
                        .Append(nam)
                        .Append(If(r.Compressed, "压缩的", "未压缩的"))
                        .Append(Int32MultiByteBitToString(r.FileType))
                        .Append("文件,提取后长度")
                        .Append(r.DecompressedLength)
                        .Append("位置")
                        .AppendLine(pos)
                    End With
                Next
                File.WriteAllText(Path.Combine(di, "提取记录.txt"), lg.ToString, Text.Encoding.UTF8)
            End With
        End Sub
    End Structure
    Private Shared Function Int32MultiByteBitToString(num As Integer) As String
        Dim sb As New Text.StringBuilder
        For Each b In BitConverter.GetBytes(num)
            sb.Append(Chr(b))
        Next
        Return sb.ToString
    End Function
End Class
