'
' * Copyright (C) 2011  rafael1193
' *
' *   This program is free software: you can redistribute it and/or modify
' *   it under the terms of the GNU General Public License as published by
' *   the Free Software Foundation, either version 3 of the License, or
' *   (at your option) any later version.
' *
' *   This program is distributed in the hope that it will be useful,
' *   but WITHOUT ANY WARRANTY; without even the implied warranty of
' *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
' *   GNU General Public License for more details.
' *
' *   You should have received a copy of the GNU General Public License
' *   along with this program.  If not, see <http://www.gnu.org/licenses/>. 
' *
' * Programador: rafael1193
' * 
' 


Imports System
Imports System.Text
Imports System.Collections.Generic
Imports System.IO
Imports DSAutoRename.SWAV

''' <summary>
''' Operations with SWAR files
''' </summary>
Class SWAR
    ''' <summary>
    ''' Read a SWAR file and return a SWAR structure
    ''' </summary>
    ''' <param name="path">File to read</param>
    ''' <returns>Structure of the file</returns>
    Public Shared Function Read(path As String) As sSWAR
        Dim fs As System.IO.FileStream = Nothing
        Dim br As System.IO.BinaryReader = Nothing

        Dim swar As New sSWAR()


        fs = New System.IO.FileStream(path, System.IO.FileMode.Open)
        br = New System.IO.BinaryReader(fs)

        ' Common header
        swar.header.type = Encoding.ASCII.GetChars(br.ReadBytes(4))
        If swar.header.type <> "SWAR" Then Throw New FileFormatException("不是SWAR文件")
        swar.header.magic = br.ReadUInt32()
        swar.header.nFileSize = br.ReadUInt32()
        swar.header.nSize = br.ReadUInt16()
        swar.header.nBlock = br.ReadUInt16()

        ' DATA section
        swar.data.type = Encoding.ASCII.GetChars(br.ReadBytes(4))
        swar.data.nSize = br.ReadUInt32()
        swar.data.reserved = New UInteger(7) {}
        For i As Integer = 0 To 7
            swar.data.reserved(i) = br.ReadUInt32()
        Next
        swar.data.nSample = br.ReadUInt32()

        swar.data.nOffset = New UInteger(swar.data.nSample - 1) {}
        For i As Integer = 0 To swar.data.nSample - 1
            swar.data.nOffset(i) = br.ReadUInt32()
        Next

        swar.data.samples = New sSWAR.sData.Sample(swar.data.nSample - 1) {}
        If swar.data.nSample = 0 Then Throw New TaskCanceledException("警告：SWAR文件是空壳，取消处理此文件。")
        For i As UInteger = 0 To swar.data.nSample - 1
            ' INFO structure
            swar.data.samples(i).info.nWaveType = br.ReadByte()
            swar.data.samples(i).info.bLoop = br.ReadByte()
            swar.data.samples(i).info.nSampleRate = br.ReadUInt16()
            swar.data.samples(i).info.nTime = br.ReadUInt16()
            swar.data.samples(i).info.nLoopOffset = br.ReadUInt16()
            swar.data.samples(i).info.nNonLoopLen = br.ReadUInt32()

            ' Calculation of data size
            If i < swar.data.nOffset.Length - 1 Then
                'SWAVInfo size ->
                swar.data.samples(i).data = New Byte(swar.data.nOffset(i + 1) - swar.data.nOffset(i) - 13) {}
            Else
                'SWAVInfo size ->
                swar.data.samples(i).data = New Byte(br.BaseStream.Length - swar.data.nOffset(i) - 13) {}
            End If

            ' Read DATA
            For j As UInteger = 0 To swar.data.samples(i).data.Length - 1
                swar.data.samples(i).data(j) = br.ReadByte()

            Next
        Next

        If fs IsNot Nothing Then
            fs.Close()
        End If
        If br IsNot Nothing Then
            br.Close()
        End If

        Return swar
    End Function
    ''' <summary>
    ''' 拆包SWAR文件。这通常可以帮助dls文件生成。
    ''' </summary>
    ''' <param name="sounds"></param>
    ''' <param name="fn"></param>
    ''' <remarks>Tinke项目把这个写错了</remarks>
    Public Shared Sub Write(sounds As sSWAV(), fn As String)
        For i As Integer = 0 To sounds.Length - 1
            Dim fileout As String = IO.Path.GetDirectoryName(fn) + "\" + IO.Path.GetFileName(fn) + "_" + i.ToString + ".SWAV"
            SWAV.Write(sounds(i), fileout)
        Next
    End Sub


    ''' <summary>
    ''' Decompress the SWAR file in SWAV files.
    ''' </summary>
    ''' <param name="swar">SWAR structure to decompress</param>
    ''' <returns>All the SWAV that are in it</returns>
    Public Shared Function ConvertToSWAV(swar As sSWAR) As sSWAV()
        Dim swav As sSWAV() = New sSWAV(swar.data.samples.Length - 1) {}

        For i As Integer = 0 To swav.Length - 1
            swav(i) = New sSWAV()

            swav(i).data.data = swar.data.samples(i).data
            swav(i).data.info = swar.data.samples(i).info
            swav(i).data.type = New Char() {"D"c, "A"c, "T"c, "A"c}
            swav(i).data.nSize = CUInt(swav(i).data.data.Length) + 1 * 4 + 4 * 1 + (2 * 1 + 3 * 2 + 4)

            swav(i).header.type = New Char() {"S"c, "W"c, "A"c, "V"c}
            swav(i).header.magic = &H100FEFF
            swav(i).header.nSize = 16
            swav(i).header.nBlock = 1
            swav(i).header.nFileSize = 16 + (CUInt(swav(i).data.data.Length) + 1 * 4 + 4 * 1 + (2 * 1 + 3 * 2 + 4))
        Next

        Return swav
    End Function
End Class

''' <summary>
''' Structure of a SWAR file
''' </summary>
Public Structure sSWAR
    Public header As sHeader
    Public data As sData
    Public Structure sHeader
        Public type As Char()
        ' 'SWAR'
        Public magic As UInteger
        ' 0x0100feff
        Public nFileSize As UInteger
        ' Size of this SWAR file
        Public nSize As UShort
        ' Size of this structure = 16
        Public nBlock As UShort
        ' Number of Blocks = 1
    End Structure
    Public Structure sData
        Public type As Char()
        ' 'DATA'
        Public nSize As UInteger
        ' Size of this structure
        Public reserved As UInteger()
        ' 8 reserved 0s, for use in runtime
        Public nSample As UInteger
        ' Number of Samples
        Public nOffset As UInteger()
        ' array of offsets of samples
        Public samples As Sample()

        Public Structure Sample
            Public info As SWAV.sSWAV.sData.SWAVInfo
            Public data As Byte()
        End Structure
    End Structure
End Structure

