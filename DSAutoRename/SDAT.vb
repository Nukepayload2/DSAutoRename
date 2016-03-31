Imports System.IO
Imports System.Text

Public Class SDAT


    Public Structure sFile
        Public offset As UInt32
        ' Offset where the files inside of the file in path
        Public size As UInt32
        ' Length of the file
        Public name As String
        ' File name
        Public id As UInt16
        ' Internal id
        Public path As String
        ' Path where the file is
        Public format As Format
        ' Format file 
        Public tag As Object
        ' Extra information
    End Structure
    Public Structure sFolder
        Public files As List(Of sFile)
        ' List of files
        Public folders As List(Of sFolder)
        ' List of folders
        Public name As String
        ' Folder name
        Public id As UInt16
        ' Internal id
        Public tag As Object
        ' Extra information
    End Structure


    Public Structure Folder
        Public name As String
        Public id As UInteger
        Public files As List(Of Sound)
        Public folders As List(Of Folder)
        Public tag As Object
    End Structure
    Public Structure Sound
        Public name As String
        Public size As UInteger
        Public offset As UInteger
        Public id As UInteger
        Public type As FormatSound
        Public internalID As UInteger
        Public path As String
        Public tag As Object
    End Structure
    Public Enum FormatSound
        SSEQ
        SSAR
        SBNK
        SWAV
        SWAR
        STRM
    End Enum

    Public Structure NitroHeader
        ' Generic Header in Nitro formats
        Public id As Char()
        Public endianess As UInt16
        ' 0xFFFE -> little endian
        Public constant As UInt16
        ' Always 0x0100
        Public file_size As UInt32
        Public header_size As UInt16
        ' Always 0x10
        Public nSection As UInt16
        ' Number of sections
    End Structure

    Public Structure sSDAT
        Public archivo As String
        Public id As Integer
        Public generico As NitroHeader
        Public cabecera As Cabecera
        Public symbol As Symbol
        Public info As Info
        Public fat As FAT
        Public files As FileBlock

    End Structure
    Public Structure Cabecera
        Public symbOffset As UInt32
        Public symbSize As UInt32
        Public infoOffset As UInt32
        Public infoSize As UInt32
        Public fatOffset As UInt32
        Public fatSize As UInt32
        Public fileOffset As UInt32
        Public fileSize As UInt32
        Public reserved As Byte()
    End Structure
#Region "SYMBOL"
    Public Structure Symbol
        Public id As Char()
        Public size As UInt32
        Public offsetSeq As UInt32
        Public offsetSeqArc As UInt32
        Public offsetBank As UInt32
        Public offsetWaveArch As UInt32
        Public offsetPlayer As UInt32
        Public offsetGroup As UInt32
        Public offsetPlayer2 As UInt32
        Public offsetStream As UInt32
        Public reserved As Byte()
        Public records As Record() 'Filenames inside
        Public record2 As Record2 'Filenames inside

    End Structure
    Public Structure Record
        Public nEntries As UInt32
        Public entriesOffset As UInt32()
        Public entries As String() '关键.文件名
    End Structure
    Public Structure Record2
        Public nEntries As UInt32
        Public group As Group()
    End Structure
    Public Structure Group
        Public groupOffset As UInt32
        Public groupName As String '关键.组名
        Public subRecOffset As UInt32
        Public subRecord As Record
    End Structure
#End Region
    Public Structure Info
        Public header As sHeader
        Public block As sBlock()

        Public Structure sHeader
            Public id As Char()
            Public size As UInteger
            Public offsetRecords As UInteger()
            Public reserved As Byte()
        End Structure
        Public Structure sBlock
            Public nEntries As UInteger
            Public offsetEntries As UInteger()
            Public entries As Object()
        End Structure
        Public Structure SEQ
            Public fileID As UShort
            Public unknown As UShort
            Public bnk As UShort
            Public vol As Byte
            Public cpr As Byte
            Public ppr As Byte
            Public ply As Byte
            Public unknown2 As Byte()
        End Structure
        Public Structure SEQARC
            Public fileID As UShort
            Public unknown As UShort
        End Structure
        Public Structure BANK
            Public fileID As UShort
            Public unknown As UShort
            Public wa As UShort()
        End Structure
        Public Structure WAVEARC
            Public fileID As UShort
            Public unknown As UShort
        End Structure
        Public Structure PLAYER
            Public unknown As Byte
            Public padding As Byte()
            Public unknown2 As UInteger
        End Structure
        Public Structure GROUP
            Public nCount As UInteger
            Public subgroup As sSubgroup()

            Public Structure sSubgroup
                Public type As UInteger
                ' 0x0700 => SEQ; 0x0803 => SEQARC; 0x0601 => BANK; 0x0402 => WAVEARC
                Public nEntry As UInteger
            End Structure
        End Structure
        Public Structure PLAYER2
            Public nCount As Byte
            Public v As Byte()
            Public reserved As Byte()
        End Structure
        Public Structure STRM
            Public fileID As UShort
            Public unknown As UShort
            Public vol As Byte
            Public pri As Byte
            Public ply As Byte
            Public reserved As Byte()
        End Structure
    End Structure
    Public Structure FAT
        Public header As sHeader
        Public records As Record()

        Public Structure sHeader
            Public id As Char()
            Public size As UInteger
            Public nRecords As UInteger
        End Structure
        Public Structure Record
            Public offset As UInteger
            Public size As UInteger
            Public reserved As Byte()
        End Structure
    End Structure
    Public Structure FileBlock
        Public header As sHeader
        Public root As Folder

        Public Structure sHeader
            Public id As Char()
            Public size As UInteger
            Public nSounds As UInteger
            Public reserved As UInteger
        End Structure
    End Structure
   
    Public Sub UnpackSDAT(SdatFiles As String())
        For Each fn In SdatFiles
            Dim dir As String = IO.Path.GetDirectoryName(fn) + "\"
            Dim sd As New SDAT
            Dim strm As New IO.FileStream(fn, IO.FileMode.Open)
            With sd.Read_SDAT(fn, strm)
                UnpackFiles(strm, .files.root.files, dir)
                UnpackFolders(strm, .files.root.folders, dir)
            End With
            strm.Close()
        Next
    End Sub
    'Structure EntryInfo
    '    Dim EntryType As EntryTypes
    '    Dim Name As String
    '    Sub New(typ As EntryTypes, Nam As String)
    '        EntryType = typ
    '        Name = Nam
    '    End Sub
    'End Structure
    Enum EntryTypes
        SSEQ
        SSAR
        SBNK
        SWAR
        Player
        Group
        Player2
        STRM
        SSEQ_IN_SSAR
    End Enum
    Private FileNameCache(8) As List(Of String)
    Private Sub FileCacheInit()
        For i As Integer = 0 To 8
            FileNameCache(i) = New List(Of String)
        Next
    End Sub
    Private Function Read_SDAT(Path As String, DataStream As Stream) As sSDAT
        FileCacheInit()
        Dim file As sFile
        With file
            .id = 1
            .format = Format.Pack
            .name = IO.Path.GetFileName(Path)
            .offset = DataStream.Position
            .path = Path
            .size = DataStream.Length
        End With
        Dim sdat As New sSDAT()
        sdat.id = file.id
        sdat.archivo = file.path
        Dim br As New BinaryReader(DataStream)
        Dim h As String = New String(br.ReadChars(4))

        br.BaseStream.Position = &H0

        '#Region "标头"
        sdat.generico.id = br.ReadChars(4)
        If sdat.generico.id <> "SDAT" Then Throw New FileFormatException("不是sdat文件")
        sdat.generico.endianess = br.ReadUInt16()
        sdat.generico.constant = br.ReadUInt16()
        sdat.generico.file_size = br.ReadUInt32()
        sdat.generico.header_size = br.ReadUInt16()
        sdat.generico.nSection = br.ReadUInt16()
        '#End Region
        '#Region "文件表头"
        sdat.cabecera.symbOffset = br.ReadUInt32()
        sdat.cabecera.symbSize = br.ReadUInt32()
        sdat.cabecera.infoOffset = br.ReadUInt32()
        sdat.cabecera.infoSize = br.ReadUInt32()
        sdat.cabecera.fatOffset = br.ReadUInt32()
        sdat.cabecera.fatSize = br.ReadUInt32()
        sdat.cabecera.fileOffset = br.ReadUInt32()
        sdat.cabecera.fileSize = br.ReadUInt32()
        sdat.cabecera.reserved = br.ReadBytes(16)
        '#End Region
        '#Region "符号"
        If sdat.cabecera.symbSize <> &H0 Then


            br.BaseStream.Position = sdat.cabecera.symbOffset
            sdat.symbol.id = br.ReadChars(4)
            sdat.symbol.size = br.ReadUInt32()
            sdat.symbol.offsetSeq = br.ReadUInt32()
            sdat.symbol.offsetSeqArc = br.ReadUInt32()
            sdat.symbol.offsetBank = br.ReadUInt32()
            sdat.symbol.offsetWaveArch = br.ReadUInt32()
            sdat.symbol.offsetPlayer = br.ReadUInt32()
            sdat.symbol.offsetGroup = br.ReadUInt32()
            sdat.symbol.offsetPlayer2 = br.ReadUInt32()
            sdat.symbol.offsetStream = br.ReadUInt32()
            sdat.symbol.reserved = br.ReadBytes(24)

            ' 主文件记录
            sdat.symbol.records = New Record(6) {}
            Dim offsets As UInteger() = New UInteger(6) {sdat.symbol.offsetSeq, sdat.symbol.offsetBank, sdat.symbol.offsetWaveArch, sdat.symbol.offsetPlayer, sdat.symbol.offsetGroup, sdat.symbol.offsetPlayer2, _
                sdat.symbol.offsetStream}

            '#Region "记录"
            For i As Integer = 0 To offsets.Length - 1
                br.BaseStream.Position = &H40 + offsets(i)
                sdat.symbol.records(i) = New Record()
                sdat.symbol.records(i).nEntries = br.ReadUInt32()
                sdat.symbol.records(i).entriesOffset = New UInteger(sdat.symbol.records(i).nEntries - 1) {}
                sdat.symbol.records(i).entries = New String(sdat.symbol.records(i).nEntries - 1) {}
                For j As Integer = 0 To sdat.symbol.records(i).nEntries - 1
                    sdat.symbol.records(i).entriesOffset(j) = br.ReadUInt32()
                Next

                For k As Integer = 0 To sdat.symbol.records(i).nEntries - 1
                    If sdat.symbol.records(i).entriesOffset(k) = &H0 Then
                        Continue For
                    End If
                    br.BaseStream.Position = &H40 + sdat.symbol.records(i).entriesOffset(k)
                    '解析记录文件名
                    Dim c As Char = ControlChars.NullChar
                    Do
                        c = CChar(ChrW(br.ReadByte()))
                        If c = ControlChars.NullChar Then Exit Do
                        sdat.symbol.records(i).entries(k) += c
                    Loop
                    FileNameCache(i).Add(sdat.symbol.records(i).entries(k))
                Next

            Next
            '#End Region
            '#Region "记录2:一般是seq包文件的记录"
            br.BaseStream.Position = &H40 + sdat.symbol.offsetSeqArc
            sdat.symbol.record2 = New Record2()
            sdat.symbol.record2.nEntries = br.ReadUInt32()
            sdat.symbol.record2.group = New Group(sdat.symbol.record2.nEntries - 1) {}
            ' 填充偏移
            For i As Integer = 0 To sdat.symbol.record2.nEntries - 1
                sdat.symbol.record2.group(i).groupOffset = br.ReadUInt32()
                sdat.symbol.record2.group(i).subRecOffset = br.ReadUInt32()
            Next
            ' 解析seq包ssar文件名
            For i As Integer = 0 To sdat.symbol.record2.nEntries - 1
                Dim c As Char = ControlChars.NullChar

                If sdat.symbol.record2.group(i).groupOffset = &H0 Then
                    ' 区别对待seq包的文件名
                    sdat.symbol.record2.group(i).groupName = "SSAR_" & i.ToString()
                Else
                    ' 解析记录2文件名
                    br.BaseStream.Position = &H40 + sdat.symbol.record2.group(i).groupOffset
                    c = ControlChars.NullChar
                    Do
                        c = CChar(ChrW(br.ReadByte()))
                        If c = ControlChars.NullChar Then Exit Do
                        sdat.symbol.record2.group(i).groupName += c
                    Loop
                    FileNameCache(EntryTypes.Group).Add(sdat.symbol.record2.group(i).groupName)
                End If

                ' 子记录处理
                If sdat.symbol.record2.group(i).subRecOffset = &H0 Then
                    ' 子记录是空的
                    sdat.symbol.record2.group(i).subRecord = New Record()
                    Continue For
                End If

                br.BaseStream.Position = &H40 + sdat.symbol.record2.group(i).subRecOffset
                Dim subRecord As New Record()
                subRecord.nEntries = br.ReadUInt32()
                subRecord.entriesOffset = New UInteger(subRecord.nEntries - 1) {}
                subRecord.entries = New String(subRecord.nEntries - 1) {}
                For j As Integer = 0 To subRecord.nEntries - 1
                    subRecord.entriesOffset(j) = br.ReadUInt32()
                Next
                ' 分析子记录
                For j As Integer = 0 To subRecord.nEntries - 1
                    If subRecord.entriesOffset(j) = &H0 Then
                        Continue For
                    End If
                    br.BaseStream.Position = &H40 + subRecord.entriesOffset(j)
                    c = ControlChars.NullChar
                    Do '子记录的文件名
                        c = CChar(ChrW(br.ReadByte()))
                        If c = ControlChars.NullChar Then Exit Do
                        subRecord.entries(j) += c
                    Loop
                    FileNameCache(EntryTypes.SSEQ_IN_SSAR).Add(subRecord.entries(j))
                Next
                sdat.symbol.record2.group(i).subRecord = subRecord
            Next
        End If
        '#End Region
        '#End Region
        '#Region "曲目信息"

        br.BaseStream.Position = sdat.cabecera.infoOffset
        Dim info As New Info()
        ' 标头
        info.header.id = br.ReadChars(4)
        info.header.size = br.ReadUInt32()
        info.header.offsetRecords = New UInteger(7) {}
        For i As Integer = 0 To 7
            info.header.offsetRecords(i) = br.ReadUInt32()
        Next
        info.header.reserved = br.ReadBytes(24)

        ' 区块
        info.block = New Info.sBlock(7) {}
        For i As Integer = 0 To 7
            br.BaseStream.Position = sdat.cabecera.infoOffset + info.header.offsetRecords(i)
            info.block(i).nEntries = br.ReadUInt32()
            info.block(i).offsetEntries = New UInteger(info.block(i).nEntries - 1) {}
            info.block(i).entries = New Object(info.block(i).nEntries - 1) {}
            For j As Integer = 0 To info.block(i).nEntries - 1
                info.block(i).offsetEntries(j) = br.ReadUInt32()
            Next
        Next

        ' 文件入口信息
        ' SSEQ文件
        For i As Integer = 0 To info.block(EntryTypes.SSEQ).nEntries - 1
            br.BaseStream.Position = sdat.cabecera.infoOffset + info.block(0).offsetEntries(i)

            Dim seq As New Info.SEQ()
            seq.fileID = br.ReadUInt16()
            seq.unknown = br.ReadUInt16()
            seq.bnk = br.ReadUInt16()
            seq.vol = br.ReadByte()
            seq.cpr = br.ReadByte()
            seq.ppr = br.ReadByte()
            seq.ply = br.ReadByte()
            seq.unknown2 = br.ReadBytes(2)
            info.block(EntryTypes.SSEQ).entries(i) = seq
        Next
        ' SEQ包文件SSAR
        For i As Integer = 0 To info.block(EntryTypes.SSAR).nEntries - 1
            br.BaseStream.Position = sdat.cabecera.infoOffset + info.block(1).offsetEntries(i)

            Dim seq As New Info.SEQARC()
            seq.fileID = br.ReadUInt16()
            seq.unknown = br.ReadUInt16()
            info.block(EntryTypes.SSAR).entries(i) = seq
        Next
        ' SBNK
        For i As Integer = 0 To info.block(EntryTypes.SBNK).nEntries - 1
            br.BaseStream.Position = sdat.cabecera.infoOffset + info.block(2).offsetEntries(i)

            Dim bank As New Info.BANK()
            bank.fileID = br.ReadUInt16()
            bank.unknown = br.ReadUInt16()
            bank.wa = New UShort(3) {}
            For j As Integer = 0 To 3
                bank.wa(j) = br.ReadUInt16()
            Next
            info.block(EntryTypes.SBNK).entries(i) = bank
        Next
        ' SWAR
        For i As Integer = 0 To info.block(EntryTypes.SWAR).nEntries - 1
            br.BaseStream.Position = sdat.cabecera.infoOffset + info.block(3).offsetEntries(i)

            Dim wave As New Info.WAVEARC()
            wave.fileID = br.ReadUInt16()
            wave.unknown = br.ReadUInt16()
            info.block(EntryTypes.SWAR).entries(i) = wave
        Next
        ' PLAYER
        For i As Integer = 0 To info.block(EntryTypes.Player).nEntries - 1
            br.BaseStream.Position = sdat.cabecera.infoOffset + info.block(4).offsetEntries(i)

            Dim player As New Info.PLAYER()
            player.unknown = br.ReadByte()
            player.padding = br.ReadBytes(3)
            player.unknown2 = br.ReadUInt32()
            info.block(EntryTypes.Player).entries(i) = player
        Next
        ' GROUP
        For i As Integer = 0 To info.block(EntryTypes.Group).nEntries - 1
            If info.block(5).offsetEntries(i) = &H0 Then
                info.block(5).entries(i) = New Info.GROUP()
                Continue For
            End If

            br.BaseStream.Position = sdat.cabecera.infoOffset + info.block(5).offsetEntries(i)

            Dim group As New Info.GROUP()
            group.nCount = br.ReadUInt32()
            group.subgroup = New Info.GROUP.sSubgroup(group.nCount - 1) {}
            For j As Integer = 0 To group.nCount - 1
                group.subgroup(j).type = br.ReadUInt32()
                group.subgroup(j).nEntry = br.ReadUInt32()
            Next
            info.block(EntryTypes.Group).entries(i) = group
        Next
        ' PLAYER2
        For i As Integer = 0 To info.block(EntryTypes.Player2).nEntries - 1
            br.BaseStream.Position = sdat.cabecera.infoOffset + info.block(6).offsetEntries(i)

            Dim player As New Info.PLAYER2()
            player.nCount = br.ReadByte()
            player.v = br.ReadBytes(16)
            player.reserved = br.ReadBytes(7)
            info.block(EntryTypes.Player2).entries(i) = player
        Next
        ' STRM
        For i As Integer = 0 To info.block(EntryTypes.STRM).nEntries - 1
            br.BaseStream.Position = sdat.cabecera.infoOffset + info.block(7).offsetEntries(i)

            Dim strm As New Info.STRM()
            strm.fileID = br.ReadUInt16()
            strm.unknown = br.ReadUInt16()
            strm.vol = br.ReadByte()
            strm.pri = br.ReadByte()
            strm.ply = br.ReadByte()
            strm.reserved = br.ReadBytes(5)
            info.block(EntryTypes.STRM).entries(i) = strm
        Next
        sdat.info = info
        '#End Region
        '#Region "Bloque FAT"
        br.BaseStream.Position = sdat.cabecera.fatOffset
        Dim fat As New FAT()

        ' Header
        fat.header.id = br.ReadChars(4)
        fat.header.size = br.ReadUInt32()
        fat.header.nRecords = br.ReadUInt32()

        ' Records
        fat.records = New FAT.Record(fat.header.nRecords - 1) {}
        For i As Integer = 0 To fat.header.nRecords - 1
            fat.records(i).offset = br.ReadUInt32()
            fat.records(i).size = br.ReadUInt32()
            fat.records(i).reserved = br.ReadBytes(8)
        Next
        sdat.fat = fat
        '#End Region
        '#Region "Bloque File"
        br.BaseStream.Position = sdat.cabecera.fileOffset

        ' Header
        sdat.files.header.id = br.ReadChars(4)
        sdat.files.header.size = br.ReadUInt32()
        sdat.files.header.nSounds = br.ReadUInt32()
        sdat.files.header.reserved = br.ReadUInt32()
        '#End Region

        BuildFileTable(sdat, DataStream)
        Return sdat

    End Function

    Private Sub BuildFileTable(ByRef sdat As sSDAT, file As Stream)
        '#Region "目录信息"
        Dim root As New Folder()
        root.name = "SDAT"
        root.id = &HF000
        root.folders = New List(Of Folder)()

        Dim sseq As Folder, ssar As Folder, sbnk As Folder, swar As Folder, strm As Folder
        sseq = New Folder()
        sseq.files = New List(Of Sound)()
        sseq.name = "SSEQ"
        sseq.id = &HF001

        ssar = New Folder()
        ssar.files = New List(Of Sound)()
        ssar.name = "SSAR"
        ssar.id = &HF002

        sbnk = New Folder()
        sbnk.files = New List(Of Sound)()
        sbnk.name = "SBNK"
        sbnk.id = &HF003

        swar = New Folder()
        swar.files = New List(Of Sound)()
        swar.name = "SWAR"
        swar.id = &HF005

        strm = New Folder()
        strm.files = New List(Of Sound)()
        strm.name = "STRM"
        strm.id = &HF006
        '#End Region

        Dim br As New BinaryReader(file)
        Dim j As Integer = 0
        Dim LastMagic As Integer = -1

        For i As Integer = 0 To sdat.fat.header.nRecords - 1
            br.BaseStream.Position = sdat.fat.records(i).offset

            Dim sound As New Sound()
            sound.offset = sdat.fat.records(i).offset
            sound.size = sdat.fat.records(i).size
            sound.internalID = CUInt(i)

            'Tinke Plugin SDAT 名称处理方式错误,应该与CrystleTile2保持一致，从Symbol里提取。
            Dim magic As String = New String(Encoding.ASCII.GetChars(br.ReadBytes(4)))

            Dim tp As Integer = 0

            Select Case magic
                Case "SSEQ"
                    tp = 0
                Case "SSAR"
                    tp = 5
                Case "SBNK"
                    tp = 1
                Case "SWAR"
                    tp = 2
                Case "STRM"
                    tp = 6
            End Select
            If LastMagic = tp Then
                j += 1
            Else
                j = 0
            End If
            sound.name = FileNameCache(tp)(j) & "." & magic
            LastMagic = tp
            Select Case magic
                Case "SSEQ"
                    sound.type = FormatSound.SSEQ
                    sseq.files.Add(sound)
                Case "SSAR"
                    sound.type = FormatSound.SSAR
                    ssar.files.Add(sound)
                Case "SBNK"
                    sound.type = FormatSound.SBNK
                    sbnk.files.Add(sound)
                Case "SWAR"
                    sound.type = FormatSound.SWAR
                    swar.files.Add(sound)
                Case "STRM"
                    sound.type = FormatSound.STRM
                    strm.files.Add(sound)
            End Select
        Next
        'br.Close()

        root.folders.Add(sseq)
        root.folders.Add(ssar)
        root.folders.Add(sbnk)
        root.folders.Add(swar)
        root.folders.Add(strm)

        sdat.files.root = root
    End Sub
     

End Class
