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
Imports System.IO
Imports System.Collections.Generic
Imports System.Runtime.InteropServices

Public Class SWAV

    Public Shared Function Read(path As String) As sSWAV
        '**Lectura del archivo SWAV**

        Dim fs As System.IO.FileStream = Nothing
        Dim br As System.IO.BinaryReader = Nothing

        Dim swav As New sSWAV()

        'Try
        fs = New System.IO.FileStream(path, System.IO.FileMode.Open)
        br = New System.IO.BinaryReader(fs)

        'Leer Header
        swav.header.type = Encoding.ASCII.GetChars(br.ReadBytes(4))
        swav.header.magic = br.ReadUInt32()
        swav.header.nFileSize = br.ReadUInt32()
        swav.header.nSize = br.ReadUInt16()
        swav.header.nBlock = br.ReadUInt16()

        'Leer Data
        swav.data.type = Encoding.ASCII.GetChars(br.ReadBytes(4))
        swav.data.nSize = br.ReadUInt32()

        'Leer Info
        swav.data.info.nWaveType = br.ReadByte()
        swav.data.info.bLoop = br.ReadByte()
        swav.data.info.nSampleRate = br.ReadUInt16()
        swav.data.info.nTime = br.ReadUInt16()
        swav.data.info.nLoopOffset = br.ReadUInt16()
        swav.data.info.nNonLoopLen = br.ReadUInt32()

        'Leer resto de Data
        Dim DataLen As UInteger = CUInt(br.BaseStream.Length - br.BaseStream.Position)
        swav.data.data = br.ReadBytes(CInt(DataLen))
        'Catch ex As Exception
        'System.Console.WriteLine(ex.Message.ToString())
        'Finally
        If fs IsNot Nothing Then
            fs.Close()
        End If
        If br IsNot Nothing Then
            br.Close()
        End If
        ' End Try

        Return swav
    End Function
    Public Shared Sub Write(swav As sSWAV, path As String)
        Dim fs As System.IO.FileStream = Nothing
        Dim bw As System.IO.BinaryWriter = Nothing

        fs = New System.IO.FileStream(path, System.IO.FileMode.Create)
        bw = New System.IO.BinaryWriter(fs)

        '文件头
        bw.Write(Encoding.ASCII.GetBytes(swav.header.type))
        bw.Write(swav.header.magic)
        bw.Write(swav.header.nFileSize)
        bw.Write(swav.header.nSize)
        bw.Write(swav.header.nBlock)

        'Data头
        bw.Write(Encoding.ASCII.GetBytes(swav.data.type))
        bw.Write(swav.data.nSize)

        'Info
        bw.Write(swav.data.info.nWaveType)
        bw.Write(swav.data.info.bLoop)
        bw.Write(swav.data.info.nSampleRate)
        bw.Write(swav.data.info.nTime)
        bw.Write(swav.data.info.nLoopOffset)
        bw.Write(swav.data.info.nNonLoopLen)

        'data
        bw.Write(swav.data.data)

        If fs IsNot Nothing Then
            fs.Close()
        End If
        If bw IsNot Nothing Then
            bw.Close()
        End If

    End Sub

    <StructLayout(LayoutKind.Sequential, Charset:=CharSet.Ansi, Pack:=1)>
    Public Structure sSWAV
        Public header As sHeader
        Public data As sData

        Public Structure sHeader
            <MarshalAs(UnmanagedType.ByValTStr, sizeconst:=4)> Public type As Char() ' "SWAV"
            Public magic As UInteger ' &H0100feff
            Public nFileSize As UInteger ' Size of this SWAV file
            Public nSize As UShort ' Size of this structure. Always = 16
            Public nBlock As UShort ' Number of Blocks. Mostly = 1
        End Structure
        Public Structure sData
            <MarshalAs(UnmanagedType.ByValTStr, sizeconst:=4)> Public type As Char() ' "DATA"
            Public nSize As UInteger' Size of this structure
            Public info As SWAVInfo' info about the sample
            Public data As Byte()' array of binary data
            ' info about the sample
            Public Structure SWAVInfo
                Public nWaveType As Byte' 0 = PCM8, 1 = PCM16, 2 = (IMA-)ADPCM
                Public bLoop As Byte' Loop flag = TRUE|FALSE
                Public nSampleRate As UShort' Sampling Rate
                Public nTime As UShort' (ARM7_CLOCK / nSampleRate) [ARM7_CLOCK: 33.513982MHz / 2 = 1.6756991 E +7]
                Public nLoopOffset As UShort' Loop Offset (expressed in words (32-bits))
                Public nNonLoopLen As UInteger' Non Loop Length (expressed in words (32-bits))
            End Structure
        End Structure
    End Structure

End Class