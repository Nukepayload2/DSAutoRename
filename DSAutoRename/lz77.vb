Imports System.IO

Public MustInherit Class LZBase

    Protected sourceFilePath As String
    Sub New(Filepath As String)
        sourceFilePath = Filepath
    End Sub
    Public Sub unzipData(targetFilePath As String)
        Dim zipedData As New MemoryStream(File.ReadAllBytes(sourceFilePath))
        Dim data As New MemoryStream()
        Try
            Decompress(zipedData, data)
            File.WriteAllBytes(targetFilePath, data.ToArray)
            zipedData.Close()
            data.Close()
        Catch ex As FormatException
            zipedData.Close()
            data.Close()
            Throw
        End Try
    End Sub

    MustOverride Function Decompress(instream As Stream, outstream As Stream) As Long
End Class

Public Class lz77
    Inherits LZBase
    Sub New(Filepath As String)
        MyBase.New(Filepath)
    End Sub
#Region "压缩数据"


    Public Sub zipData(targetFilePath As String)
        Dim data As New FileStream(sourceFilePath, FileMode.Open)
        Dim zipedData As New FileStream(targetFilePath, FileMode.Create)
        Dim p, wPos, wLen, tPos, tInt, tLen As Integer
        Dim dataLength As Integer = CInt(data.Length)
        Dim bufferLength As Integer = 0
        Dim buffer(4095) As Byte
        Dim buffer_zip(8091) As Byte
        Dim nextSamePoint(4095) As Integer
        Dim bufferDP As Integer
        Dim f As Boolean, ff As Boolean
        Dim tByteLen As Byte = 0
        While (Assign(bufferLength, data.Read(buffer, 0, 4096))) > 0
            ' 如果buffer长度太短直接保存
            If bufferLength < 6 Then
                For i As Integer = 0 To Math.Min(3, bufferLength) - 1
                    tInt = (tInt << 8) + buffer(i)
                    zipedData.WriteByte(CByte(tInt >> tByteLen))
                    tInt = getRightByte(tInt, tByteLen)
                Next
                For i As Integer = 3 To bufferLength - 1
                    tInt = (tInt << 9) + buffer(i)
                    tByteLen = CByte(tByteLen + 9)
                    While tByteLen >= 8
                        zipedData.WriteByte(CByte(tInt >> (tByteLen - 8) << 24 >> 24))
                        tByteLen = CByte(tByteLen - 8)
                    End While
                    ' 记录剩余信息
                    tInt = getRightByte(tInt, tByteLen)
                Next
                Exit While
            End If
            ' 提前计算出下一个相同字节的位置
            For i As Integer = 0 To bufferLength - 1
                For j As Integer = i + 1 To bufferLength - 1
                    nextSamePoint(i) = 4096
                    If buffer(i) = buffer(j) Then
                        nextSamePoint(i) = j
                        Exit For
                    End If
                Next
            Next
            ' 初始化字典和滑块
            tInt = (tInt << 8) + buffer(0)
            buffer_zip(0) = CByte(tInt >> tByteLen)
            tInt = getRightByte(tInt, tByteLen)
            tInt = (tInt << 8) + buffer(1)
            buffer_zip(1) = CByte(tInt >> tByteLen)
            tInt = getRightByte(tInt, tByteLen)
            tInt = (tInt << 8) + buffer(2)
            buffer_zip(2) = CByte(tInt >> tByteLen)
            tInt = getRightByte(tInt, tByteLen)
            p = 3
            wPos = 0
            wLen = 3
            tPos = -1
            tLen = 0
            bufferDP = 2
            While bufferLength - 3 >= p
                While wPos + wLen <= p AndAlso p + wLen <= bufferLength
                    ' 判断滑块是否匹配
                    f = True
                    If buffer(wPos) = buffer(p) Then
                        ff = True
                        For i As Integer = 1 To wLen - 1
                            If buffer(wPos + i) <> buffer(p + i) Then
                                f = False
                                Exit For
                            End If
                        Next
                    Else
                        f = False
                        ff = False
                    End If
                    ' 匹配则增加滑块长度，否则移动
                    If Not f Then
                        ' 滑块移动
                        If ff Then
                            wPos = nextSamePoint(wPos)
                        Else
                            wPos += 1
                        End If
                    Else
                        ' 增加滑块长度
                        While wPos + wLen <> p AndAlso p + wLen <> bufferLength AndAlso wLen < 1024
                            If buffer(wPos + wLen) = buffer(p + wLen) Then
                                wLen += 1
                            Else
                                Exit While
                            End If
                        End While
                        tPos = wPos
                        tLen = wLen
                        ' 滑块移动并增加1长度
                        wPos = nextSamePoint(wPos)
                        wLen += 1
                    End If
                End While
                If tPos = -1 Then
                    ' 单个字节
                    tInt = (tInt << 9) + buffer(p)
                    tByteLen = CByte(tByteLen + 9)
                    p += 1
                Else
                    ' 匹配的字节串
                    tInt = (((tInt << 1) + 1 << 12) + tPos << 11) + tLen
                    tByteLen = CByte(tByteLen + 24)
                    p += tLen
                End If
                While tByteLen >= 8
                    buffer_zip(System.Threading.Interlocked.Increment(bufferDP)) = CByte(tInt >> (tByteLen - 8) << 24 >> 24)
                    tByteLen = CByte(tByteLen - 8)
                End While
                tInt = getRightByte(tInt, tByteLen)

                wPos = 0
                wLen = 3
                tPos = -1
                tLen = 0
            End While
            ' 写入剩余字节
            For i As Integer = p To bufferLength - 1
                tInt = (tInt << 9) + buffer(i)
                tByteLen = CByte(tByteLen + 9)
                buffer_zip(System.Threading.Interlocked.Increment(bufferDP)) = CByte(tInt >> (tByteLen - 8))
                tByteLen = CByte(tByteLen - 8)
                tInt = getRightByte(tInt, tByteLen)
            Next
            ' 写入Stream
            zipedData.Write(buffer_zip, 0, bufferDP + 1)
        End While
        If tByteLen <> 0 Then
            zipedData.WriteByte(CByte(tInt << 8 - tByteLen))
        End If
        ' 写入剩余信息
        data.Close()
        zipedData.Close()
    End Sub
#End Region
#Region "解压数据"
     

    ''' <summary>
    ''' 解压LZ77,算法来自DSDecmp
    ''' </summary>
    ''' <param name="instream">压缩的流</param>
    ''' <param name="outstream">输出的流</param>
    Public Overrides Function Decompress(instream As Stream, outstream As Stream) As Long
        If instream.Length < 5 Then Throw New FormatException("文件太小")
        Dim readBytes As Long = 0
        Dim inLength As Long = instream.Length
        Dim type As Byte = CByte(instream.ReadByte())
        If type <> &H10 Then Throw New FormatException("不是lz77文件")
        Dim sizeBytes(3) As Byte
        instream.Read(sizeBytes, 0, 3)
        Dim decompressedSize As Integer = BitConverter.ToInt32(sizeBytes, 0) ' ToNDSu24(sizeBytes, 0)
        readBytes += 4
        If decompressedSize = 0 Then
            instream.Read(sizeBytes, 0, 4)
            decompressedSize = BitConverter.ToInt32(sizeBytes, 0)
            readBytes += 4
        End If
        Dim bufferLength As Integer = &H1000
        Dim buffer(bufferLength - 1) As Byte
        Dim bufferOffset As Integer = 0
        Dim currentOutSize As Integer = 0
        Dim flags As Integer = 0, mask As Integer = 1
        Do While currentOutSize < decompressedSize
            If mask = 1 Then
                If readBytes >= inLength Then
                    Throw New FormatException("文件太短")
                End If
                flags = instream.ReadByte()
                readBytes += 1
                If flags < 0 Then
                    Throw New FormatException("文件太短")
                End If
                mask = &H80
            Else
                mask >>= 1
            End If
            If (flags And mask) > 0 Then
                If readBytes + 1 >= inLength Then
                    If readBytes < inLength Then
                        instream.ReadByte()
                        readBytes += 1
                    End If
                    Throw New FormatException("数据不足")
                End If
                Dim byte1 As Integer = instream.ReadByte()
                readBytes += 1
                Dim byte2 As Integer = instream.ReadByte()
                readBytes += 1
                If byte2 < 0 Then
                    Throw New FormatException("文件太短")
                End If
                Dim length As Integer = byte1 >> 4
                length += 3
                Dim disp As Integer = ((byte1 And &HF) << 8) Or byte2
                disp += 1
                If disp > currentOutSize Then
                    Throw New FormatException("文件破损")
                End If
                Dim bufIdx As Integer = bufferOffset + bufferLength - disp
                For i As Integer = 0 To length - 1
                    Dim nxt As Byte = buffer(bufIdx Mod bufferLength)
                    bufIdx += 1
                    outstream.WriteByte(nxt)
                    buffer(bufferOffset) = nxt
                    bufferOffset = (bufferOffset + 1) Mod bufferLength
                Next
                currentOutSize += length
            Else
                If readBytes >= inLength Then
                    Throw New FormatException("数据不足")
                End If
                Dim nxt As Integer = instream.ReadByte()
                readBytes += 1
                currentOutSize += 1
                outstream.WriteByte(CByte(nxt))
                buffer(bufferOffset) = CByte(nxt)
                bufferOffset = (bufferOffset + 1) Mod bufferLength
            End If
            outstream.Flush()
        Loop
        If readBytes < inLength Then
            If (readBytes Xor (readBytes And 3)) + 4 < inLength Then
                Throw New FormatException("文件长度与数据不相符")
            End If
        End If

        Return decompressedSize
    End Function

#End Region
#Region "private item"
    Private Shared Function getLeftByte(dataLine As Integer, s As Integer) As Integer
        Return dataLine << 32 - s >> 24
    End Function
    Private Shared Function getLeftByte(dataLine As UInteger, s As Integer) As UInteger
        Return dataLine << 32 - s >> 24
    End Function
    Private Shared Function getRightByte(dataLine As Integer, l As Integer) As Integer
        If l = 0 Then
            Return 0
        Else
            Return dataLine << (32 - l) >> (32 - l)
        End If
    End Function
    Private Shared Function getRightByte(dataLine As UInteger, l As Integer) As UInteger
        If l = 0 Then
            Return 0
        Else
            Return dataLine << (32 - l) >> (32 - l)
        End If
    End Function
    Private Shared Function Assign(Of T)(ByRef target As T, value As T) As T
        target = value
        Return value
    End Function
#End Region

End Class

Public Class LZ77_11
    Inherits LZBase
    Const MagicByte As Integer = &H11
    Private Shared m_lookAhead As Boolean = False
    ''' <summary>
    ''' Sets the flag that determines if 'look-ahead'/DP should be used when compressing
    ''' with the LZ-11 format. The default is false, which is what is used in the original
    ''' implementation.
    ''' </summary>
    Public Shared WriteOnly Property LookAhead() As Boolean
        Set(value As Boolean)
            m_lookAhead = value
        End Set
    End Property

    Sub New(Filepath As String)
        MyBase.New(Filepath)
    End Sub
   
#Region "Decompression method"
    Public Overrides Function Decompress(instream As Stream, outstream As Stream) As Long
        '#Region "Format definition in NDSTEK style"
        '  Data header (32bit)
        '                  Bit 0-3   Reserved
        '                  Bit 4-7   Compressed type (must be 1 for LZ77)
        '                  Bit 8-31  Size of decompressed data. if 0, the next 4 bytes are decompressed length
        '                Repeat below. Each Flag Byte followed by eight Blocks.
        '                Flag data (8bit)
        '                  Bit 0-7   Type Flags for next 8 Blocks, MSB first
        '                Block Type 0 - Uncompressed - Copy 1 Byte from Source to Dest
        '                  Bit 0-7   One data byte to be copied to dest
        '                Block Type 1 - Compressed - Copy LEN Bytes from Dest-Disp-1 to Dest
        '                    If Reserved is 0: - Default
        '                      Bit 0-3   Disp MSBs
        '                      Bit 4-7   LEN - 3
        '                      Bit 8-15  Disp LSBs
        '                    If Reserved is 1: - Higher compression rates for files with (lots of) long repetitions
        '                      Bit 4-7   Indicator
        '                        If Indicator > 1:
        '                            Bit 0-3    Disp MSBs
        '                            Bit 4-7    LEN - 1 (same bits as Indicator)
        '                            Bit 8-15   Disp LSBs
        '                        If Indicator is 1: A(B CD E)(F GH)
        '                            Bit 0-3     (LEN - 0x111) MSBs
        '                            Bit 4-7     Indicator; unused
        '                            Bit 8-15    (LEN- 0x111) 'middle'-SBs
        '                            Bit 16-19   Disp MSBs
        '                            Bit 20-23   (LEN - 0x111) LSBs
        '                            Bit 24-31   Disp LSBs
        '                        If Indicator is 0:
        '                            Bit 0-3     (LEN - 0x11) MSBs
        '                            Bit 4-7     Indicator; unused
        '                            Bit 8-11    Disp MSBs
        '                            Bit 12-15   (LEN - 0x11) LSBs
        '                            Bit 16-23   Disp LSBs
        '             

        '#End Region
        Dim inLength As Long = instream.Length
        Dim readBytes As Long = 0

        Dim type As Byte = CByte(instream.ReadByte())
        If type <> MagicByte Then
            Throw New InvalidDataException([String].Format(Get_Traduction("S03"), type.ToString("X")))
        End If
        Dim sizeBytes As Byte() = New Byte(2) {}
        instream.Read(sizeBytes, 0, 3)
        Dim decompressedSize As Integer = ToNDSu24(sizeBytes, 0)
        readBytes += 4
        If decompressedSize = 0 Then
            sizeBytes = New Byte(3) {}
            instream.Read(sizeBytes, 0, 4)
            decompressedSize = ToNDSs32(sizeBytes, 0)
            readBytes += 4
        End If

        ' the maximum 'DISP-1' is still 0xFFF.
        Dim bufferLength As Integer = &H1000
        Dim buffer As Byte() = New Byte(bufferLength - 1) {}
        Dim bufferOffset As Integer = 0

        Dim currentOutSize As Integer = 0
        Dim flags As Integer = 0, mask As Integer = 1
        While currentOutSize < decompressedSize
            ' (throws when requested new flags byte is not available)
            '#Region "Update the mask. If all flag bits have been read, get a new set."
            ' the current mask is the mask used in the previous run. So if it masks the
            ' last flag bit, get a new flags byte.
            If mask = 1 Then
                If readBytes >= inLength Then
                    Throw New NotEnoughDataException(currentOutSize, decompressedSize)
                End If
                flags = instream.ReadByte()
                readBytes += 1
                If flags < 0 Then
                    Throw New StreamTooShortException()
                End If
                mask = &H80
            Else
                mask >>= 1
            End If
            '#End Region

            ' bit = 1 <=> compressed.
            If (flags And mask) > 0 Then
                ' (throws when not enough bytes are available)
                '#Region "Get length and displacement('disp') values from next 2, 3 or 4 bytes"

                ' read the first byte first, which also signals the size of the compressed block
                If readBytes >= inLength Then
                    Throw New NotEnoughDataException(currentOutSize, decompressedSize)
                End If
                Dim byte1 As Integer = instream.ReadByte()
                readBytes += 1
                If byte1 < 0 Then
                    Throw New StreamTooShortException()
                End If

                Dim length As Integer = byte1 >> 4
                Dim disp As Integer = -1
                If length = 0 Then
                    '#Region "case 0; 0(B C)(D EF) + (0x11)(0x1) = (LEN)(DISP)"

                    ' case 0:
                    ' data = AB CD EF (with A=0)
                    ' LEN = ABC + 0x11 == BC + 0x11
                    ' DISP = DEF + 1

                    ' we need two more bytes available
                    If readBytes + 1 >= inLength Then
                        Throw New NotEnoughDataException(currentOutSize, decompressedSize)
                    End If
                    Dim byte2 As Integer = instream.ReadByte()
                    readBytes += 1
                    Dim byte3 As Integer = instream.ReadByte()
                    readBytes += 1
                    If byte3 < 0 Then
                        Throw New StreamTooShortException()
                    End If

                    length = (((byte1 And &HF) << 4) Or (byte2 >> 4)) + &H11

                    '#End Region
                    disp = (((byte2 And &HF) << 8) Or byte3) + &H1
                ElseIf length = 1 Then
                    '#Region "case 1: 1(B CD E)(F GH) + (0x111)(0x1) = (LEN)(DISP)"

                    ' case 1:
                    ' data = AB CD EF GH (with A=1)
                    ' LEN = BCDE + 0x111
                    ' DISP = FGH + 1

                    ' we need three more bytes available
                    If readBytes + 2 >= inLength Then
                        Throw New NotEnoughDataException(currentOutSize, decompressedSize)
                    End If
                    Dim byte2 As Integer = instream.ReadByte()
                    readBytes += 1
                    Dim byte3 As Integer = instream.ReadByte()
                    readBytes += 1
                    Dim byte4 As Integer = instream.ReadByte()
                    readBytes += 1
                    If byte4 < 0 Then
                        Throw New StreamTooShortException()
                    End If

                    length = (((byte1 And &HF) << 12) Or (byte2 << 4) Or (byte3 >> 4)) + &H111

                    '#End Region
                    disp = (((byte3 And &HF) << 8) Or byte4) + &H1
                Else
                    '#Region "case > 1: (A)(B CD) + (0x1)(0x1) = (LEN)(DISP)"

                    ' case other:
                    ' data = AB CD
                    ' LEN = A + 1
                    ' DISP = BCD + 1

                    ' we need only one more byte available
                    If readBytes >= inLength Then
                        Throw New NotEnoughDataException(currentOutSize, decompressedSize)
                    End If
                    Dim byte2 As Integer = instream.ReadByte()
                    readBytes += 1
                    If byte2 < 0 Then
                        Throw New StreamTooShortException()
                    End If

                    length = ((byte1 And &HF0) >> 4) + &H1

                    '#End Region
                    disp = (((byte1 And &HF) << 8) Or byte2) + &H1
                End If

                If disp > currentOutSize Then
                    Throw New InvalidDataException([String].Format(Get_Traduction("S04"), disp, currentOutSize.ToString("X"), instream.Position.ToString("X"), (byte1 >> 4).ToString("X")))
                End If
                '#End Region

                Dim bufIdx As Integer = bufferOffset + bufferLength - disp
                For i As Integer = 0 To length - 1
                    Dim [next] As Byte = buffer(bufIdx Mod bufferLength)
                    bufIdx += 1
                    outstream.WriteByte([next])
                    buffer(bufferOffset) = [next]
                    bufferOffset = (bufferOffset + 1) Mod bufferLength
                Next
                currentOutSize += length
            Else
                If readBytes >= inLength Then
                    Throw New NotEnoughDataException(currentOutSize, decompressedSize)
                End If
                Dim [next] As Integer = instream.ReadByte()
                readBytes += 1
                If [next] < 0 Then
                    Throw New StreamTooShortException()
                End If

                outstream.WriteByte(CByte([next]))
                currentOutSize += 1
                buffer(bufferOffset) = CByte([next])
                bufferOffset = (bufferOffset + 1) Mod bufferLength
            End If
        End While

        If readBytes < inLength Then
            ' the input may be 4-byte aligned.
            If (readBytes Xor (readBytes And 3)) + 4 < inLength Then
                Throw New IOException(readBytes & inLength)
            End If
        End If

        Return decompressedSize
    End Function
#End Region

#Region "Original compression method"
    Public Function Compress(instream As Stream, inLength As Long, outstream As Stream) As Integer
        ' make sure the decompressed size fits in 3 bytes.
        ' There should be room for four bytes, however I'm not 100% sure if that can be used
        ' in every game, as it may not be a built-in function.
        If inLength > &HFFFFFF Then
            Throw New IOException
        End If

        ' use the other method if lookahead is enabled
        If m_lookAhead Then
            Return CompressWithLA(instream, inLength, outstream)
        End If

        ' save the input data in an array to prevent having to go back and forth in a file
        Dim indata As Byte() = New Byte(inLength - 1) {}
        Dim numReadBytes As Integer = instream.Read(indata, 0, CInt(inLength))
        If numReadBytes <> inLength Then
            Throw New StreamTooShortException()
        End If

        ' write the compression header first
        outstream.WriteByte(LZ77_11.MagicByte)
        outstream.WriteByte(CByte(inLength And &HFF))
        outstream.WriteByte(CByte((inLength >> 8) And &HFF))
        outstream.WriteByte(CByte((inLength >> 16) And &HFF))

        Dim compressedLength As Integer = 4
        Dim instart = Pointer(Of Byte).FromArray(indata)
        ' we do need to buffer the output, as the first byte indicates which blocks are compressed.
        ' this version does not use a look-ahead, so we do not need to buffer more than 8 blocks at a time.
        ' (a block is at most 4 bytes long)
        Dim outbuffer As Byte() = New Byte(8 * 4) {}
        outbuffer(0) = 0
        Dim bufferlength As Integer = 1, bufferedBlocks As Integer = 0
        Dim readBytes As Integer = 0
        While readBytes < inLength
            '#Region "If 8 blocks are bufferd, write them and reset the buffer"
            ' we can only buffer 8 blocks at a time.
            If bufferedBlocks = 8 Then
                outstream.Write(outbuffer, 0, bufferlength)
                compressedLength += bufferlength
                ' reset the buffer
                outbuffer(0) = 0
                bufferlength = 1
                bufferedBlocks = 0
            End If
            '#End Region

            ' determine if we're dealing with a compressed or raw block.
            ' it is a compressed block when the next 3 or more bytes can be copied from
            ' somewhere in the set of already compressed bytes.
            Dim disp As Integer
            Dim oldLength As Integer = Math.Min(readBytes, &H1000)
            Dim length As Integer = GetOccurrenceLength(instart + readBytes, CInt(Math.Min(inLength - readBytes, &H10110)), instart + readBytes - oldLength, oldLength, disp)

            ' length not 3 or more? next byte is raw data
            If length < 3 Then
                outbuffer(System.Math.Max(System.Threading.Interlocked.Increment(bufferlength), bufferlength - 1)) = (instart + (System.Math.Max(System.Threading.Interlocked.Increment(readBytes), readBytes - 1))).Target
            Else
                ' 3 or more bytes can be copied? next (length) bytes will be compressed into 2 bytes
                readBytes += length

                ' mark the next block as compressed
                outbuffer(0) = outbuffer(0) Or CByte(1 << (7 - bufferedBlocks))

                If length > &H110 Then
                    ' case 1: 1(B CD E)(F GH) + (0x111)(0x1) = (LEN)(DISP)
                    outbuffer(bufferlength) = &H10
                    outbuffer(bufferlength) = outbuffer(bufferlength) Or CByte(((length - &H111) >> 12) And &HF)
                    bufferlength += 1
                    outbuffer(bufferlength) = CByte(((length - &H111) >> 4) And &HFF)
                    bufferlength += 1
                    outbuffer(bufferlength) = CByte(((length - &H111) << 4) And &HF0)
                ElseIf length > &H10 Then
                    ' case 0; 0(B C)(D EF) + (0x11)(0x1) = (LEN)(DISP)
                    outbuffer(bufferlength) = &H0
                    outbuffer(bufferlength) = outbuffer(bufferlength) Or CByte(((length - &H111) >> 4) And &HF)
                    bufferlength += 1
                    outbuffer(bufferlength) = CByte(((length - &H111) << 4) And &HF0)
                Else
                    ' case > 1: (A)(B CD) + (0x1)(0x1) = (LEN)(DISP)
                    outbuffer(bufferlength) = CByte(((length - 1) << 4) And &HF0)
                End If
                ' the last 1.5 bytes are always the disp
                outbuffer(bufferlength) = outbuffer(bufferlength) Or CByte(((disp - 1) >> 8) And &HF)
                bufferlength += 1
                outbuffer(bufferlength) = CByte((disp - 1) And &HFF)
                bufferlength += 1
            End If
            bufferedBlocks += 1
        End While

        ' copy the remaining blocks to the output
        If bufferedBlocks > 0 Then
            outstream.Write(outbuffer, 0, bufferlength)
            '/ make the compressed file 4-byte aligned.
            '                    while ((compressedLength % 4) != 0)
            '                    {
            '                        outstream.WriteByte(0);
            '                        compressedLength++;
            '                    }/*

            compressedLength += bufferlength
        End If


        Return compressedLength
    End Function
#End Region

#Region "Dynamic Programming compression method"
    ''' <summary>
    ''' Variation of the original compression method, making use of Dynamic Programming to 'look ahead'
    ''' and determine the optimal 'length' values for the compressed blocks. Is not 100% optimal,
    ''' as the flag-bytes are not taken into account.
    ''' </summary>
    Private Function CompressWithLA(instream As Stream, inLength As Long, outstream As Stream) As Integer
        ' save the input data in an array to prevent having to go back and forth in a file
        Dim indata As Byte() = New Byte(inLength - 1) {}
        Dim numReadBytes As Integer = instream.Read(indata, 0, CInt(inLength))
        If numReadBytes <> inLength Then
            Throw New StreamTooShortException()
        End If

        ' write the compression header first
        outstream.WriteByte(LZ77_11.MagicByte)
        outstream.WriteByte(CByte(inLength And &HFF))
        outstream.WriteByte(CByte((inLength >> 8) And &HFF))
        outstream.WriteByte(CByte((inLength >> 16) And &HFF))

        Dim compressedLength As Integer = 4

        ' we do need to buffer the output, as the first byte indicates which blocks are compressed.
        ' this version does not use a look-ahead, so we do not need to buffer more than 8 blocks at a time.
        ' blocks are at most 4 bytes long.
        Dim outbuffer As Byte() = New Byte(8 * 4) {}
        outbuffer(0) = 0
        Dim bufferlength As Integer = 1, bufferedBlocks As Integer = 0
        Dim readBytes As Integer = 0
        Dim instart = Pointer(Of Byte).FromArray(indata)
        ' get the optimal choices for len and disp
        Dim lengths As Integer(), disps As Integer()
        Me.GetOptimalCompressionLengths(instart, indata.Length, lengths, disps)
        While readBytes < inLength
            ' we can only buffer 8 blocks at a time.
            If bufferedBlocks = 8 Then
                outstream.Write(outbuffer, 0, bufferlength)
                compressedLength += bufferlength
                ' reset the buffer
                outbuffer(0) = 0
                bufferlength = 1
                bufferedBlocks = 0
            End If


            If lengths(readBytes) = 1 Then
                outbuffer(System.Math.Max(System.Threading.Interlocked.Increment(bufferlength), bufferlength - 1)) = (instart + (System.Math.Max(System.Threading.Interlocked.Increment(readBytes), readBytes - 1))).Target
            Else
                ' mark the next block as compressed
                outbuffer(0) = outbuffer(0) Or CByte(1 << (7 - bufferedBlocks))

                If lengths(readBytes) > &H110 Then
                    ' case 1: 1(B CD E)(F GH) + (0x111)(0x1) = (LEN)(DISP)
                    outbuffer(bufferlength) = &H10
                    outbuffer(bufferlength) = outbuffer(bufferlength) Or CByte(((lengths(readBytes) - &H111) >> 12) And &HF)
                    bufferlength += 1
                    outbuffer(bufferlength) = CByte(((lengths(readBytes) - &H111) >> 4) And &HFF)
                    bufferlength += 1
                    outbuffer(bufferlength) = CByte(((lengths(readBytes) - &H111) << 4) And &HF0)
                ElseIf lengths(readBytes) > &H10 Then
                    ' case 0; 0(B C)(D EF) + (0x11)(0x1) = (LEN)(DISP)
                    outbuffer(bufferlength) = &H0
                    outbuffer(bufferlength) = outbuffer(bufferlength) Or CByte(((lengths(readBytes) - &H111) >> 4) And &HF)
                    bufferlength += 1
                    outbuffer(bufferlength) = CByte(((lengths(readBytes) - &H111) << 4) And &HF0)
                Else
                    ' case > 1: (A)(B CD) + (0x1)(0x1) = (LEN)(DISP)
                    outbuffer(bufferlength) = CByte(((lengths(readBytes) - 1) << 4) And &HF0)
                End If
                ' the last 1.5 bytes are always the disp
                outbuffer(bufferlength) = outbuffer(bufferlength) Or CByte(((disps(readBytes) - 1) >> 8) And &HF)
                bufferlength += 1
                outbuffer(bufferlength) = CByte((disps(readBytes) - 1) And &HFF)
                bufferlength += 1

                readBytes += lengths(readBytes)
            End If


            bufferedBlocks += 1
        End While

        ' copy the remaining blocks to the output
        If bufferedBlocks > 0 Then
            outstream.Write(outbuffer, 0, bufferlength)
            '/ make the compressed file 4-byte aligned.
            '                    while ((compressedLength % 4) != 0)
            '                    {
            '                        outstream.WriteByte(0);
            '                        compressedLength++;
            '                    }/*

            compressedLength += bufferlength
        End If


        Return compressedLength
    End Function
#End Region

#Region "DP compression helper method; GetOptimalCompressionLengths"
    ''' <summary>
    ''' Gets the optimal compression lengths for each start of a compressed block using Dynamic Programming.
    ''' This takes O(n^2) time, although in practice it will often be O(n^3) since one of the constants is 0x10110
    ''' (the maximum length of a compressed block)
    ''' </summary>
    ''' <param name="indata">The data to compress.</param>
    ''' <param name="inLength">The length of the data to compress.</param>
    ''' <param name="lengths">The optimal 'length' of the compressed blocks. For each byte in the input data,
    ''' this value is the optimal 'length' value. If it is 1, the block should not be compressed.</param>
    ''' <param name="disps">The 'disp' values of the compressed blocks. May be 0, in which case the
    ''' corresponding length will never be anything other than 1.</param>
    Private Sub GetOptimalCompressionLengths(indata As Pointer(Of Byte), inLength As Integer, ByRef lengths As Integer(), ByRef disps As Integer())
        lengths = New Integer(inLength - 1) {}
        disps = New Integer(inLength - 1) {}
        Dim minLengths As Integer() = New Integer(inLength - 1) {}

        For i As Integer = inLength - 1 To 0 Step -1
            ' first get the compression length when the next byte is not compressed
            minLengths(i) = Integer.MaxValue
            lengths(i) = 1
            If i + 1 >= inLength Then
                minLengths(i) = 1
            Else
                minLengths(i) = 1 + minLengths(i + 1)
            End If
            ' then the optimal compressed length
            Dim oldLength As Integer = Math.Min(&H1000, i)
            ' get the appropriate disp while at it. Takes at most O(n) time if oldLength is considered O(n) and 0x10110 constant.
            ' however since a lot of files will not be larger than 0x10110, this will often take ~O(n^2) time.
            ' be sure to bound the input length with 0x10110, as that's the maximum length for LZ-11 compressed blocks.
            Dim maxLen As Integer = GetOccurrenceLength(indata + i, Math.Min(inLength - i, &H10110), indata + i - oldLength, oldLength, disps(i))
            If disps(i) > i Then
                Throw New Exception(Get_Traduction("S02"))
            End If
            For j As Integer = 3 To maxLen
                Dim blocklen As Integer
                If j > &H110 Then
                    blocklen = 4
                ElseIf j > &H10 Then
                    blocklen = 3
                Else
                    blocklen = 2
                End If
                Dim newCompLen As Integer
                If i + j >= inLength Then
                    newCompLen = blocklen
                Else
                    newCompLen = blocklen + minLengths(i + j)
                End If
                If newCompLen < minLengths(i) Then
                    lengths(i) = j
                    minLengths(i) = newCompLen
                End If
            Next
        Next

        ' we could optimize this further to also optimize it with regard to the flag-bytes, but that would require 8 times
        ' more space and time (one for each position in the block) for only a potentially tiny increase in compression ratio.
    End Sub
#End Region

    ''' <summary>
    ''' Determine the maximum size of a LZ-compressed block starting at newPtr, using the already compressed data
    ''' starting at oldPtr. Takes O(inLength * oldLength) = O(n^2) time.
    ''' </summary>
    ''' <param name="newPtr">The start of the data that needs to be compressed.</param>
    ''' <param name="newLength">The number of bytes that still need to be compressed.</param>
    ''' <param name="oldPtr">The start of the raw file.</param>
    ''' <param name="oldLength">The number of bytes already compressed.</param>
    ''' <param name="disp">The offset of the start of the longest block to refer to.</param>
    ''' <returns>The length of the longest sequence of bytes that can be copied from the already decompressed data.</returns>
    Friend Shared Function GetOccurrenceLength(newPtr As Pointer(Of Byte), newLength As Integer, oldPtr As Pointer(Of Byte), oldLength As Integer, ByRef disp As Integer) As Integer
        disp = 0
        If newLength = 0 Then
            Return 0
        End If
        Dim maxLength As Integer = 0
        ' try every possible 'disp' value (disp = oldLength - i)
        For i As Integer = 0 To oldLength - 2
            ' work from the start of the old data to the end, to mimic the original implementation's behaviour
            ' (and going from start to end or from end to start does not influence the compression ratio anyway)
            Dim currentOldStart As Pointer(Of Byte) = oldPtr + i
            Dim currentLength As Integer = 0
            ' determine the length we can copy if we go back (oldLength - i) bytes
            ' always check the next 'newLength' bytes, and not just the available 'old' bytes,
            ' as the copied data can also originate from what we're currently trying to compress.
            For j As Integer = 0 To newLength - 1
                ' stop when the bytes are no longer the same
                If (currentOldStart + j).Target <> (newPtr + j).Target Then
                    Exit For
                End If
                currentLength += 1
            Next

            ' update the optimal value
            If currentLength > maxLength Then
                maxLength = currentLength
                disp = oldLength - i
            End If
        Next
        Return maxLength
    End Function

    Shared Function Get_Traduction(code As String) As String
        Dim message As [String] = ""
        Try
            Dim xml As XElement = XElement.Load(My.Application.Info.DirectoryPath + Path.DirectorySeparatorChar & "Tinke.xml")
            Dim idioma As String = xml.Element("Options").Element("Language").Value
            xml = Nothing

            For Each langFile As String In Directory.GetFiles(My.Application.Info.DirectoryPath + Path.DirectorySeparatorChar & "langs")
                If Not langFile.EndsWith(".xml") Then
                    Continue For
                End If

                xml = XElement.Load(langFile)
                If xml.Attribute("name").Value = idioma Then
                    Exit For
                End If
            Next

            message = xml.Element("DSDecmp").Element(code).Value
        Catch
            Throw New Exception("There was an error in the XML file of language.")
        End Try

        Return message
    End Function

    ''' <summary>
    ''' Returns a 4-byte unsigned integer as used on the NDS converted from four bytes
    ''' at a specified position in a byte array.
    ''' </summary>
    ''' <param name="buffer">The source of the data.</param>
    ''' <param name="offset">The location of the data in the source.</param>
    ''' <returns>The indicated 4 bytes converted to uint</returns>
    Public Shared Function ToNDSu32(buffer As Byte(), offset As Integer) As UInteger
        Return CUInt(CInt(buffer(offset)) Or (CInt(buffer(offset + 1)) << 8) Or (CInt(buffer(offset + 2)) << 16) Or (CInt(buffer(offset + 3)) << 24))
    End Function

    ''' <summary>
    ''' Returns a 4-byte signed integer as used on the NDS converted from four bytes
    ''' at a specified position in a byte array.
    ''' </summary>
    ''' <param name="buffer">The source of the data.</param>
    ''' <param name="offset">The location of the data in the source.</param>
    ''' <returns>The indicated 4 bytes converted to int</returns>
    Public Shared Function ToNDSs32(buffer As Byte(), offset As Integer) As Integer
        Return CInt(CInt(buffer(offset)) Or (CInt(buffer(offset + 1)) << 8) Or (CInt(buffer(offset + 2)) << 16) Or (CInt(buffer(offset + 3)) << 24))
    End Function

    ''' <summary>
    ''' Converts a u32 value into a sequence of bytes that would make ToNDSu32 return
    ''' the given input value.
    ''' </summary>
    Public Shared Function FromNDSu32(value As UInteger) As Byte()
        Return New Byte() {CByte(value And &HFF), CByte((value >> 8) And &HFF), CByte((value >> 16) And &HFF), CByte((value >> 24) And &HFF)}
    End Function

    ''' <summary>
    ''' Returns a 3-byte integer as used in the built-in compression
    ''' formats in the DS, convrted from three bytes at a specified position in a byte array,
    ''' </summary>
    ''' <param name="buffer">The source of the data.</param>
    ''' <param name="offset">The location of the data in the source.</param>
    ''' <returns>The indicated 3 bytes converted to an integer.</returns>
    Public Shared Function ToNDSu24(buffer As Byte(), offset As Integer) As Integer
        Return CInt(CInt(buffer(offset)) Or (CInt(buffer(offset + 1)) << 8) Or (CInt(buffer(offset + 2)) << 16))
    End Function
End Class

Public Class NotEnoughDataException
    Inherits Exception
    Sub New(a As String, b As String)
        MyBase.New(a + vbCrLf + b)
    End Sub
End Class

Public Class StreamTooShortException
    Inherits Exception

End Class