Imports System.Text
Imports System.IO
 
Public Class NARC
     Private narc As ARC
     

#Region "Unpack methods"
    Public Function Unpack(stream As Stream, Path As String) As sFolder
        Dim file As sFile
        file.path = Path
        file.name = IO.Path.GetFileName(Path)
        Dim arc As New ARC()
        Dim br As New BinaryReader(stream)

        ' Nitro generic header
        arc.id = br.ReadChars(4)
        arc.id_endian = br.ReadUInt16()
        If arc.id_endian = &HFFFE Then
            arc.id.Reverse()
        End If
        arc.constant = br.ReadUInt16()
        arc.file_size = br.ReadUInt32()
        arc.header_size = br.ReadUInt16()
        arc.nSections = br.ReadUInt16()

        ' BTAF (File Allocation TaBle)
        arc.btaf.id = br.ReadChars(4)
        arc.btaf.section_size = br.ReadUInt32()
        arc.btaf.nFiles = br.ReadUInt32()
        arc.btaf.entries = New BTAF_Entry(arc.btaf.nFiles - 1) {}
        For i As Integer = 0 To arc.btaf.nFiles - 1
            arc.btaf.entries(i).start_offset = br.ReadUInt32()
            arc.btaf.entries(i).end_offset = br.ReadUInt32()
        Next

        ' BTNF (File Name TaBle)
        arc.btnf.id = br.ReadChars(4)
        arc.btnf.section_size = br.ReadUInt32()
        arc.btnf.entries = New List(Of BTNF_MainEntry)()

        Dim mainTables_offset As Long = br.BaseStream.Position
        Dim gmif_offset As UInteger = CUInt(br.BaseStream.Position) + arc.btnf.section_size

        '#Region "Get root folder"

        br.BaseStream.Position += 6
        Dim num_mains As UShort = br.ReadUInt16()
        br.BaseStream.Position -= 8

        For m As Integer = 0 To num_mains - 1
            Dim main As New BTNF_MainEntry()
            main.offset = br.ReadUInt32()
            main.first_pos = br.ReadUInt16()
            main.parent = br.ReadUInt16()
            Dim idFile As UInteger = main.first_pos

            If main.offset < &H8 Then
                ' There aren't names (in Pokemon games)
                For i As Integer = 0 To arc.btaf.nFiles - 1
                    Dim currFile As New sFile()
                    currFile.name = IO.Path.GetFileNameWithoutExtension(file.name) & "_"c & idFile.ToString()
                    currFile.id = CUShort(idFile)
                    idFile += 1
                    ' FAT data
                    currFile.path = file.path
                    currFile.offset = arc.btaf.entries(currFile.id).start_offset + gmif_offset
                    currFile.size = (arc.btaf.entries(currFile.id).end_offset - arc.btaf.entries(currFile.id).start_offset)

                    ' Get the extension
                    Dim currPos As Long = br.BaseStream.Position
                    br.BaseStream.Position = currFile.offset
                    Dim ext As Char()
                    If currFile.size < 4 Then
                        ext = Encoding.ASCII.GetChars(br.ReadBytes(CInt(currFile.size)))
                    Else
                        ext = Encoding.ASCII.GetChars(br.ReadBytes(4).Reverse.ToArray)
                    End If

                    Dim extS As String = "."
                    For s As Integer = 0 To ext.Length - 1
                        If Char.IsLetterOrDigit(ext(s)) OrElse ext(s) = Chr(&H20) Then
                            extS += ext(s)
                        End If
                    Next

                    If extS <> "." AndAlso extS.Length = 5 AndAlso currFile.size >= 4 Then
                        currFile.name += extS
                    Else
                        currFile.name += ".bin"
                    End If
                    br.BaseStream.Position = currPos


                    If Not (TypeOf main.files Is List(Of sFile)) Then
                        main.files = New List(Of sFile)()
                    End If
                    main.files.Add(currFile)
                Next

                arc.btnf.entries.Add(main)
                Continue For
            End If

            Dim posmain As Long = br.BaseStream.Position
            br.BaseStream.Position = main.offset + mainTables_offset

            Dim id As Integer = br.ReadByte()
            While id <> &H0
                ' Indicate the end of the subtable
                If (id And &H80) = 0 Then
                    ' File
                    Dim currFile As New sFile()
                    currFile.id = CUShort(idFile)
                    idFile += 1
                    currFile.name = New String(br.ReadChars(id))

                    ' FAT data
                    currFile.path = file.path
                    currFile.offset = arc.btaf.entries(currFile.id).start_offset + gmif_offset
                    currFile.size = (arc.btaf.entries(currFile.id).end_offset - arc.btaf.entries(currFile.id).start_offset)

                    If Not (TypeOf main.files Is List(Of sFile)) Then
                        main.files = New List(Of sFile)()
                    End If
                    main.files.Add(currFile)
                Else
                    ' Directory
                    Dim currFolder As New sFolder()
                    currFolder.name = New String(br.ReadChars(id - &H80))
                    currFolder.id = br.ReadUInt16()

                    If Not (TypeOf main.folders Is List(Of sFolder)) Then
                        main.folders = New List(Of sFolder)()
                    End If
                    main.folders.Add(currFolder)
                End If

                id = br.ReadByte()
            End While
            arc.btnf.entries.Add(main)
            br.BaseStream.Position = posmain
        Next

        Dim root As sFolder = Create_TreeFolders(arc.btnf.entries, &HF000, "root")
        '#End Region

        ' GMIF (File IMaGe)
        br.BaseStream.Position = gmif_offset - 8
        arc.gmif.id = br.ReadChars(4)
        arc.gmif.section_size = br.ReadUInt32()
        ' Files data

        'br.Close()
        narc = arc
        Return root
    End Function
    Private Function Create_TreeFolders(entries As List(Of BTNF_MainEntry), idFolder As Integer, nameFolder As String) As sFolder
        Dim currFolder As New sFolder()

        currFolder.name = nameFolder
        currFolder.id = CUShort(idFolder)
        currFolder.files = entries(idFolder And &HFFF).files

        If TypeOf entries(idFolder And &HFFF).folders Is List(Of sFolder) Then
            ' If there are folders
            currFolder.folders = New List(Of sFolder)()

            For Each subFolder As sFolder In entries(idFolder And &HFFF).folders
                currFolder.folders.Add(Create_TreeFolders(entries, subFolder.id, subFolder.name))
            Next
        End If

        Return currFolder
    End Function
#End Region

#Region "Pack methods"
    Public Function Pack(Data As Stream, Path As String, ByRef unpacked As sFolder, TempOutPath As String) As String
        Unpack(Data, Path)
        Save_NARC(TempOutPath, unpacked, Path)
        Return TempOutPath
    End Function
    Private Sub Save_NARC(fileout As String, ByRef decompressed As sFolder, orifile As String)
        ' Structure of the file
        '             * 
        '             * Common header
        '             * 
        '             * BTAF section
        '             * |_ Start offset
        '             * |_ End offset
        '             * 
        '             * BTNF section
        '             * 
        '             * GMIF section
        '             * |_ Files
        '             * 
        '             


        Dim bw As New BinaryWriter(File.OpenWrite(fileout))
        Dim br As New BinaryReader(File.OpenRead(orifile))

        ' Write the BTAF section
        Dim btafTmp As String = Path.GetTempFileName()
        Write_BTAF(btafTmp, &H10 + narc.btaf.section_size + narc.btnf.section_size + &H8, decompressed)

        ' Write the BTNF section
        Dim btnfTmp As String = Path.GetTempFileName()
        br.BaseStream.Position = &H10 + narc.btaf.section_size
        File.WriteAllBytes(btnfTmp, br.ReadBytes(CInt(narc.btnf.section_size)))

        ' Write the GMIF section
        Dim gmifTmp As String = Path.GetTempFileName()
        Write_GMIF(gmifTmp, decompressed)

        ' Write the NARC file
        Dim file_size As Integer = CInt(narc.header_size + narc.btaf.section_size + narc.btnf.section_size + narc.gmif.section_size)

        ' Common header
        bw.Write(narc.id)
        bw.Write(narc.id_endian)
        bw.Write(narc.constant)
        bw.Write(file_size)
        bw.Write(narc.header_size)
        bw.Write(narc.nSections)
        ' Write the sections
        bw.Write(File.ReadAllBytes(btafTmp))
        bw.Write(File.ReadAllBytes(btnfTmp))
        bw.Write(narc.gmif.id)
        bw.Write(narc.gmif.section_size)
        bw.Write(File.ReadAllBytes(gmifTmp))

        bw.Flush()
        bw.Close()
        br.Close()

        File.Delete(btafTmp)
        File.Delete(btnfTmp)
        File.Delete(gmifTmp)
    End Sub
    Private Sub Write_BTAF(fileout As String, startOffset As UInteger, ByRef decompressed As sFolder)
        Dim bw As New BinaryWriter(File.OpenWrite(fileout))
        Dim offset As UInteger = 0

        bw.Write(narc.btaf.id)
        bw.Write(narc.btaf.section_size)
        bw.Write(narc.btaf.nFiles)

        For i As Integer = 0 To narc.btaf.nFiles - 1
            Dim currFile As sFile = Search_File(i + decompressed.id, decompressed)
            currFile.offset = offset

            bw.Write(offset)
            offset += currFile.size
            bw.Write(offset)
        Next

        bw.Flush()
        bw.Close()
    End Sub
    Private Sub Write_GMIF(fileout As String, decompressed As sFolder)
        Dim bw As New BinaryWriter(File.OpenWrite(fileout))

        For i As Integer = 0 To narc.btaf.nFiles - 1
            Dim currFile As sFile = Search_File(i + decompressed.id, decompressed)
            Dim br As New BinaryReader(File.OpenRead(currFile.path))
            br.BaseStream.Position = currFile.offset

            bw.Write(br.ReadBytes(CInt(currFile.size)))
            br.Close()
            bw.Flush()
        Next

        While bw.BaseStream.Position Mod 4 <> 0
            bw.Write(CByte(&HFF))
        End While

        bw.Flush()
        bw.Close()
        narc.gmif.section_size = CUInt(New FileInfo(fileout).Length) + &H8
    End Sub
    Private Function Search_File(id As Integer, currFolder As sFolder) As sFile
        If TypeOf currFolder.files Is List(Of sFile) Then
            For Each archivo As sFile In currFolder.files
                If archivo.id = id Then
                    Return archivo
                End If
            Next
        End If


        If TypeOf currFolder.folders Is List(Of sFolder) Then
            For Each subFolder As sFolder In currFolder.folders
                Dim currFile As sFile = Search_File(id, subFolder)
                If TypeOf currFile.name Is String Then
                    Return currFile
                End If
            Next
        End If

        Return New sFile()
    End Function
#End Region

End Class

Public Class ARCH
    Private Const Padding As Integer = &H10
    Private Const MagicStamp As String = "ARCH"
    Private Shared ReadOnly DefaultEncoding As Encoding = Encoding.ASCII

    Public Function Get_Format(file As sFile, magic As Byte()) As Format
        If Encoding.ASCII.GetString(magic) = MagicStamp Then
            Return Format.Pack
        End If

        Return Format.Unknown
    End Function

    Public Function Unpack(file__1 As sFile) As sFolder
        Dim strIn As Stream = File.OpenRead(file__1.path)
        Dim br As New BinaryReader(strIn)
        Dim unpacked As New sFolder()
        unpacked.files = New List(Of sFile)()
        unpacked.folders = New List(Of sFolder)()

        ' Read header
        Dim magicStamp As New String(br.ReadChars(4))
        Dim numFiles As UInteger = br.ReadUInt32()
        Dim fntOffset As UInteger = br.ReadUInt32()
        Dim fatOffset As UInteger = br.ReadUInt32()
        Dim natOffset As UInteger = br.ReadUInt32()
        Dim filesOffset As UInteger = br.ReadUInt32()

        ' Extract files
        For i As Integer = 0 To numFiles - 1
            strIn.Position = natOffset
            SetNameOffset(strIn, i)
            Dim nameOffset As UShort = br.ReadUInt16()

            strIn.Position = fntOffset + nameOffset
            Dim filename As String = ReadString(strIn)

            strIn.Position = fatOffset + (&H10 * i)
            Dim encodedSize As Integer = br.ReadInt32()
            Dim decodedSize As Integer = br.ReadInt32()
            Dim fileOffset As UInteger = br.ReadUInt32() + filesOffset
            Dim nameOffset2 As UShort = br.ReadUInt16()
            Dim isEncoded As Boolean = br.ReadUInt16() = 1

            Dim result As Boolean
            Console.Write("{0} file {1}... ", If(isEncoded, "Decoding", "Saving"), filename)
            Dim newFile As New sFile()
            If isEncoded Then
                Dim decodedPath As String = IO.Path.GetTempFileName
                newFile.offset = 0
                newFile.size = CUInt(decodedSize)
                newFile.path = decodedPath

                Dim dec As New Decoder(strIn, fileOffset, encodedSize, decodedSize)
                result = dec.Decode(decodedPath)
            Else
                newFile.offset = fileOffset
                newFile.path = file__1.path
                newFile.size = CUInt(encodedSize)

                result = True
            End If

            Console.Write("{0}", If(result, "Ok", "Fail"))
            AddFile(unpacked, newFile, filename)
        Next

        br.Close()
        br = Nothing
        Return unpacked
    End Function

    Public Function Pack(ByRef unpacked As sFolder, file__1 As sFile) As String
        Dim files As sFile() = GetFiles(unpacked)
        Dim numFiles As Integer = files.Length

        ' Write the sections       
        Dim bw As BinaryWriter

        ' A) Fnt
        Dim fntStr As New MemoryStream()
        bw = New BinaryWriter(fntStr)
        Dim namesOffsets As UShort() = New UShort(numFiles - 1) {}

        bw.Write(&H0)
        ' I'll write later the section size
        bw.Write(numFiles)
        For i As Integer = 0 To numFiles - 1
            namesOffsets(i) = CUShort(fntStr.Position)
            WriteString(fntStr, files(i).name)
        Next

        WritePadding(fntStr)

        ' Now write section size
        fntStr.Position = 0
        bw.Write(CUInt(fntStr.Length))
        bw.Flush()
        bw = Nothing

        ' B) Fat
        Dim fatStr As New MemoryStream()
        bw = New BinaryWriter(fatStr)

        Dim offset As UInteger = &H0
        For i As Integer = 0 To numFiles - 1
            bw.Write(files(i).size)
            bw.Write(&H0)
            bw.Write(offset)
            bw.Write(namesOffsets(i))
            bw.Write(CUShort(&H0))
            ' No encoding
            offset = AddPadding(offset + files(i).size)
        Next

        bw.Flush()
        bw = Nothing

        ' C) Nat
        Dim natStr As New MemoryStream()
        bw = New BinaryWriter(natStr)

        For i As Integer = 0 To numFiles - 1
            bw.Write(CUShort(i))
            bw.Write(namesOffsets(i))
        Next

        WritePadding(natStr)
        bw.Flush()
        bw = Nothing

        ' D) Write file
        Dim outFile As String = IO.Path.GetTempFileName
        Dim strOut As Stream = File.OpenWrite(outFile)
        bw = New BinaryWriter(strOut)

        ' Calculate section offsets
        Dim fntOffset As UInteger = &H20
        ' After header
        Dim fatOffset As UInteger = fntOffset + CUInt(fntStr.Length)
        ' After Fnt
        Dim natOffset As UInteger = fatOffset + CUInt(fatStr.Length)
        ' After Fat
        Dim filesOffset As UInteger = natOffset + CUInt(natStr.Length)
        ' After Nat
        ' Write header
        bw.Write(Encoding.ASCII.GetBytes(MagicStamp))
        bw.Write(numFiles)
        bw.Write(fntOffset)
        bw.Write(fatOffset)
        bw.Write(natOffset)
        bw.Write(filesOffset)
        WritePadding(strOut)
        bw.Flush()

        ' Write sections
        fntStr.WriteTo(strOut)
        fatStr.WriteTo(strOut)
        natStr.WriteTo(strOut)
        bw.Flush()

        ' Write files
        Dim br As BinaryReader
        For i As Integer = 0 To numFiles - 1
            br = New BinaryReader(File.OpenRead(files(i).path))
            br.BaseStream.Position = files(i).offset
            bw.Write(br.ReadBytes(CInt(files(i).size)))
            br.Close()
            br = Nothing

            WritePadding(strOut)
            bw.Flush()
        Next

        bw.Flush()
        bw.Close()
        bw = Nothing
        Return outFile
    End Function

    ' ------------------------------------------------------------------//

    Private Shared Sub AddFile(folder As sFolder, file As sFile, filePath As String)
        If filePath.Contains("\") Then
            Dim folderName As String = filePath.Substring(0, filePath.IndexOf("\"c))
            Dim subfolder As New sFolder()
            For Each f As sFolder In folder.folders
                If f.name = folderName Then
                    subfolder = f
                End If
            Next

            If String.IsNullOrEmpty(subfolder.name) Then
                subfolder.name = folderName
                subfolder.folders = New List(Of sFolder)()
                subfolder.files = New List(Of sFile)()
                folder.folders.Add(subfolder)
            End If

            AddFile(subfolder, file, filePath.Substring(filePath.IndexOf("\"c) + 1))
        Else
            file.name = filePath
            folder.files.Add(file)
        End If
    End Sub

    Private Shared Function GetFiles(folder As sFolder) As sFile()
        Dim files As New List(Of sFile)()
        Dim queue As New Queue(Of sFolder)()
        folder.name = String.Empty
        queue.Enqueue(folder)

        Do
            Dim currentFolder As sFolder = queue.Dequeue()
            For Each f As sFolder In currentFolder.folders
                Dim subfolder As sFolder = f
                If Not String.IsNullOrEmpty(currentFolder.name) Then
                    subfolder.name = currentFolder.name + "\"c + subfolder.name
                End If

                queue.Enqueue(subfolder)
            Next

            For Each f As sFile In currentFolder.files
                Dim file As sFile = f
                If Not String.IsNullOrEmpty(currentFolder.name) Then
                    file.name = currentFolder.name + "\"c + file.name
                End If
                files.Add(file)
            Next
        Loop While queue.Count <> 0

        Return files.ToArray()
    End Function

    Private Shared Sub WritePadding(str As Stream)
        While str.Position Mod Padding <> 0
            str.WriteByte(&H0)
        End While
    End Sub

    Private Shared Function AddPadding(val As UInteger) As UInteger
        If val Mod Padding <> 0 Then
            val += Padding - (val Mod Padding)
        End If

        Return val
    End Function

    Private Shared Function ReadString(str As Stream) As String
        Dim s As String = String.Empty
        Dim data As New List(Of Byte)()

        While str.ReadByte() <> 0
            str.Position -= 1
            data.Add(CByte(str.ReadByte()))
        End While

        s = DefaultEncoding.GetString(data.ToArray())
        data.Clear()
        data = Nothing

        Return s
    End Function

    Private Shared Sub WriteString(str As Stream, s As String)
        Dim data As Byte() = DefaultEncoding.GetBytes(s & ControlChars.NullChar)
        str.Write(data, 0, data.Length)
    End Sub

    Private Shared Sub SetNameOffset(str As Stream, fileId As Integer)
        Dim br As New BinaryReader(str)
        While br.ReadUInt16() <> fileId
            br.ReadUInt16()
        End While

        br = Nothing
    End Sub
End Class

''' <summary>
''' Decode Arch files.
''' </summary>
Public Class Decoder
    Private nextSamples As New Stack(Of Byte)(&H80)
    Private buffer1 As Byte() = New Byte(255) {}
    Private buffer2 As Byte() = New Byte(255) {}

    Private str As Stream
    Private encodedSize As Integer
    Private decodedSize As Integer

    ''' <summary>
    ''' Initializes a new instance of the <see cref="Decoder" /> class.
    ''' </summary>
    ''' <param name="file1">File to decode.</param>
    Public Sub New(file1 As String)
        Me.New(File.OpenRead(file1), -1)
    End Sub

    ''' <summary>
    ''' Initializes a new instance of the <see cref="Decoder" /> class.
    ''' </summary>
    ''' <param name="str">Stream with the data encoded.</param>
    ''' <param name="decodedSize">Size of the decoded file.</param>
    Public Sub New(str As Stream, decodedSize As Integer)
        Me.New(str, 0, CInt(str.Length), decodedSize)
    End Sub

    ''' <summary>
    ''' Initializes a new instance of the <see cref="Decoder" /> class.
    ''' </summary>
    ''' <param name="str">Stream with the data encoded</param>
    ''' <param name="offset">Offset to the data encoded.</param>
    ''' <param name="encodedSize">Size of the encoded file.</param>
    ''' <param name="decodedSize">Size of the decoded file.</param>
    Public Sub New(str As Stream, offset As UInteger, encodedSize As Integer, decodedSize As Integer)
        str.Position = offset
        Me.str = str
        Me.encodedSize = encodedSize
        Me.decodedSize = decodedSize
    End Sub

    ''' <summary>
    ''' Decode the data.
    ''' </summary>
    ''' <param name="fileOut">Path to the output file.</param>
    ''' <returns>A value indicating whether the operation was successfully.</returns>
    Public Function Decode(fileOut As String) As Boolean
        If File.Exists(fileOut) Then
            File.Delete(fileOut)
        End If

        Dim fs As New FileStream(fileOut, FileMode.Create, FileAccess.Write)

        Dim result As Boolean = Me.Decode(fs)

        fs.Flush()
        fs.Close()
        fs.Dispose()
        fs = Nothing

        Return result
    End Function

    ''' <summary>
    ''' Decode the data.
    ''' </summary>
    ''' <param name="strOut">Stream to the output file.</param>
    ''' <returns>A value indicating whether the operation was successfully.</returns>
    Public Function Decode(strOut As Stream) As Boolean
        Dim startReading As Long = Me.str.Position
        Dim startWriting As Long = strOut.Position

        While Me.str.Position - startReading < Me.encodedSize
            InitBuffer(Me.buffer2)
            Me.FillBuffer()

            Me.Process(strOut)
        End While

        If Me.decodedSize <> -1 Then
            Return (strOut.Position - startWriting) = Me.decodedSize
        Else
            Return True
        End If
    End Function

    Private Shared Sub InitBuffer(buffer As Byte())
        If buffer.Length > &H100 Then
            Throw New ArgumentException("Invalid buffer length", "buffer")
        End If

        For i As Integer = 0 To buffer.Length - 1
            buffer(i) = CByte(i)
        Next
    End Sub

    Private Sub FillBuffer()
        Dim index As Integer = 0

        While index <> &H100
            Dim id As Integer = Me.str.ReadByte()
            Dim numLoops As Integer = id

            If id > &H7F Then
                numLoops = 0
                Dim skipPositions As Integer = id - &H7F
                index += skipPositions
            End If

            If index = &H100 Then
                Exit While
            End If

            ' It's in the ARM code but... really?
            If numLoops < 0 Then
                Continue While
            End If

            For i As Integer = 0 To numLoops
                Dim b As Byte = CByte(Me.str.ReadByte())
                Me.buffer2(index) = b

                ' It'll write
                If b <> index Then
                    Me.buffer1(index) = CByte(Me.str.ReadByte())
                End If

                index += 1
            Next
        End While
    End Sub

    Private Sub Process(strOut As Stream)
        Dim numLoops As Integer = (Me.str.ReadByte() << 8) + Me.str.ReadByte()
        Me.nextSamples.Clear()
        Dim index As Integer

        While True
            If Me.nextSamples.Count = 0 Then
                If numLoops = 0 Then
                    Return
                End If

                numLoops -= 1
                index = Me.str.ReadByte()
            Else
                index = Me.nextSamples.Pop()
            End If

            If Me.buffer2(index) = index Then
                strOut.WriteByte(CByte(index))
            Else
                Me.nextSamples.Push(Me.buffer1(index))
                Me.nextSamples.Push(Me.buffer2(index))
                index = Me.nextSamples.Count
            End If
        End While
    End Sub
End Class

Public Class Utility
     

#Region "Unpack methods"
    Public Function Unpack(ArcPath As String, ArcStrm As Stream) As sFolder
        Dim arc As New ARC()

        Dim br As New BinaryReader(ArcStrm)

        Dim fntOffset As UInteger = br.ReadUInt32()
        Dim fntSize As UInteger = br.ReadUInt32()
        Dim fatOffset As UInteger = br.ReadUInt32()
        Dim fatSize As UInteger = br.ReadUInt32()

        ' FAT (File Allocation TaBle)
        br.BaseStream.Position = fatOffset

        arc.btaf.nFiles = fatSize \ &H8
        arc.btaf.entries = New BTAF_Entry(arc.btaf.nFiles - 1) {}
        For i As Integer = 0 To arc.btaf.nFiles - 1
            arc.btaf.entries(i).start_offset = br.ReadUInt32()
            arc.btaf.entries(i).end_offset = br.ReadUInt32()
        Next

        ' FNT (File Name TaBle)
        br.BaseStream.Position = fntOffset
        arc.btnf.entries = New List(Of BTNF_MainEntry)()

        '#Region "Get root directory"
        Do
            Dim main As New BTNF_MainEntry()
            main.offset = br.ReadUInt32()
            main.first_pos = br.ReadUInt16()
            main.parent = br.ReadUInt16()
            Dim idFile As UInteger = main.first_pos

            Dim currOffset As Long = br.BaseStream.Position
            br.BaseStream.Position = main.offset + fntOffset
            Dim id As Integer = br.ReadByte()

            While id <> &H0
                ' End of subtable
                If (id And &H80) = 0 Then
                    ' File
                    Dim currFile As New sFile()
                    currFile.id = CUShort(idFile)
                    idFile += 1
                    currFile.name = New String(br.ReadChars(id))

                    ' Add the fat data
                    currFile.path = ArcPath
                    currFile.offset = arc.btaf.entries(currFile.id).start_offset
                    currFile.size = (arc.btaf.entries(currFile.id).end_offset - currFile.offset)

                    If Not (TypeOf main.files Is List(Of sFile)) Then
                        main.files = New List(Of sFile)()
                    End If
                    main.files.Add(currFile)
                Else
                    ' Directory
                    Dim currFolder As New sFolder()
                    currFolder.name = New String(br.ReadChars(id - &H80))
                    currFolder.id = br.ReadUInt16()
                    If Not (TypeOf main.folders Is List(Of sFolder)) Then
                        main.folders = New List(Of sFolder)()
                    End If
                    main.folders.Add(currFolder)
                End If

                id = br.ReadByte()
            End While
            arc.btnf.entries.Add(main)

            br.BaseStream.Position = currOffset
        Loop While fntOffset + arc.btnf.entries(0).offset <> br.BaseStream.Position

        Dim root As sFolder = Create_TreeFolders(arc.btnf.entries, &HF000, "root")
        '#End Region

        br.Close()
        Return root
    End Function
    Public Function Create_TreeFolders(entries As List(Of BTNF_MainEntry), idFolder As Integer, nameFolder As String) As sFolder
        Dim currFolder As New sFolder()

        currFolder.name = nameFolder
        currFolder.id = CUShort(idFolder)
        currFolder.files = entries(idFolder And &HFFF).files

        If TypeOf entries(idFolder And &HFFF).folders Is List(Of sFolder) Then
            ' If there is folders inside.
            currFolder.folders = New List(Of sFolder)()

            For Each subFolder As sFolder In entries(idFolder And &HFFF).folders
                currFolder.folders.Add(Create_TreeFolders(entries, subFolder.id, subFolder.name))
            Next
        End If

        Return currFolder
    End Function
#End Region

    Public Function Pack(fileIn As String, ByRef unpacked As sFolder) As String
        Dim fileOut As String = IO.Path.GetTempPath + Path.DirectorySeparatorChar + "newUtility_" & Path.GetRandomFileName()

        Dim br As New BinaryReader(File.OpenRead(fileIn))
        ' Old pack file
        Dim bw As New BinaryWriter(File.OpenWrite(fileOut))
        ' New pack file
        Dim buffer As New List(Of Byte)()
        ' Buffer with file data
        ' By the moment, as we can not add files, the FNT section won't change, only repointing

        Dim fntOffset As UInteger = br.ReadUInt32()
        Dim fntSize As UInteger = br.ReadUInt32()
        Dim fatOffset As UInteger = br.ReadUInt32()
        Dim fatSize As UInteger = br.ReadUInt32()

        bw.Write(fntOffset)
        bw.Write(fntSize)
        bw.Write(fatOffset)
        bw.Write(fatSize)

        ' Write FNT section
        br.BaseStream.Position = fntOffset
        bw.Write(br.ReadBytes(CInt(fntSize)))
        bw.Write(CULng(&H0))
        ' Padding
        bw.Flush()
        br.Close()

        ' Write FAT section
        Dim offset As UInteger = fatOffset + fatSize + &H10
        For i As Integer = 0 To fatSize \ 8 - 1
            bw.Write(offset)
            Dim currFile As sFile = Get_File(i + unpacked.id, offset, fileOut, unpacked)
            offset += currFile.size
            bw.Write(offset)

            ' Write the file to the buffer
            br = New BinaryReader(File.OpenRead(currFile.path))
            br.BaseStream.Position = currFile.offset
            buffer.AddRange(br.ReadBytes(CInt(currFile.size)))
            br.Close()

            ' Padding
            If offset Mod 4 <> 0 Then
                For r As Integer = 0 To 4 - (offset Mod 4) - 1
                    buffer.Add(&HFF)
                Next

                offset += 4 - (offset Mod 4)
            End If
        Next
        bw.Write(CULng(&H0))
        bw.Write(CULng(&H0))

        ' Write files
        bw.Write(buffer.ToArray())
        bw.Flush()
        bw.Close()

        buffer.Clear()

        Return fileOut
    End Function
    Private Function Get_File(id As Integer, newOffset As UInteger, path As String, currFolder As sFolder) As sFile
        If TypeOf currFolder.files Is List(Of sFile) Then
            For i As Integer = 0 To currFolder.files.Count - 1
                If currFolder.files(i).id = id Then
                    Dim original As sFile = currFolder.files(i)
                    Dim newFile As sFile = currFolder.files(i)
                    newFile.offset = newOffset
                    newFile.path = path
                    currFolder.files(i) = newFile

                    Return original
                End If
            Next
        End If


        If TypeOf currFolder.folders Is List(Of sFolder) Then
            For Each subFolder As sFolder In currFolder.folders
                Dim currFile As sFile = Get_File(id, newOffset, path, subFolder)
                If TypeOf currFile.name Is String Then
                    Return currFile
                End If
            Next
        End If

        Return New sFile()

    End Function
End Class

Public Module Formats

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


    Public Enum Format
        Palette
        Tile
        Map
        Cell
        Animation
        FullImage
        Text
        Video
        Sound
        Font
        Compressed
        Unknown
        System
        Script
        Pack
        Model3D
        Texture
    End Enum
    Public Enum FormatCompress
        ' From DSDecmp
        LZOVL
        ' keep this as the first one, as only the end of a file may be LZ-ovl-compressed (and overlay files are oftenly double-compressed)
        LZ10
        LZ11
        HUFF4
        HUFF8
        RLE
        HUFF
        NDS
        GBA
        Invalid
    End Enum

    Public Structure ARC
        Public id As Char()
        ' Always NARC = 0x4E415243
        Public id_endian As UInt16
        ' Si 0xFFFE hay que darle la vuelta al id
        Public constant As UInt16
        ' Always 0x0100
        Public file_size As UInt32
        Public header_size As UInt16
        ' Siempre 0x0010
        Public nSections As UInt16
        ' En este caso siempre 0x0003
        Public btaf As BTAF
        Public btnf As BTNF
        Public gmif As GMIF
    End Structure
    Public Structure BTAF
        Public id As Char()
        Public section_size As UInt32
        Public nFiles As UInt32
        Public entries As BTAF_Entry()
    End Structure
    Public Structure BTAF_Entry
        ' Ambas son relativas a la sección GMIF
        Public start_offset As UInt32
        Public end_offset As UInt32
    End Structure
    Public Structure BTNF
        Public id As Char()
        Public section_size As UInt32
        Public entries As List(Of BTNF_MainEntry)
    End Structure
    Public Structure BTNF_MainEntry
        Public offset As UInt32
        ' Relativo a la primera entrada
        Public first_pos As UInt32
        ' ID del primer archivo.
        Public parent As UInt32
        ' En el caso de root, número de carpetas;
        Public files As List(Of sFile)
        Public folders As List(Of sFolder)
    End Structure
    Public Structure GMIF
        Public id As Char()
        Public section_size As UInt32
        ' Datos de los archivos....
    End Structure

End Module