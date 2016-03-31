Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Runtime.InteropServices.Marshal

<Obsolete>
Public Class CastleVeniaImg
     
    Public Structure P_DatFile
        Dim Flag As UInteger
        Dim pData1 As UInteger
        Dim pData2 As UInteger
        Dim pData3 As UInteger
        Dim pData4 As UInteger
        Dim pData5 As UInteger
        Dim Padding1 As Long
        Dim pChecksum As UInteger
        Dim SubImageCount As UInteger
        Dim ActionCount As Integer
        Dim CurrentFileLength As UInteger
        Dim Padding2 As Decimal
        Dim Data1s As sImageSplitter()
        Dim Data2s As sBodyAttackExtraHittest()
        Dim Data3s As sFrameInfo()
        Dim Data4s As sSquence()
        Dim Data5s As sActionWrapper()
        Dim chkSubImageCount As UShort
        Dim chkData5Count As UShort
        Dim chkCurrentFileLength As UInteger
        Dim chkchkCurrentFileLength As UInteger
        <StructLayout(LayoutKind.Sequential, size:=16)>
        Structure sImageSplitter
            Dim DestOffsetX As Short '在目标位置的水平偏移
            Dim DestOffsetY As Short '在目标位置的竖直偏移
            Dim SourceX As UShort '原图起始点的X
            Dim SourceY As UShort '原图起始点的Y
            Dim Width As UShort '宽
            Dim Height As UShort '高
            Dim FileID As Byte '文件ID，就是sc文件夹里的 s_soma\d\d\.dat 的\d\d部分
            Dim FlipMode As FlipModes
            Dim PalatteAdjust As Byte
            Dim Preserved As Byte '总是0
            Enum FlipModes As Byte
                None
                X
                Y
                Both
            End Enum
        End Structure
        Structure sBodyAttackExtraHittest '这是攻击敌人时身体的Hittest，与武器无关，一般用于滑铲
            Dim X As Short
            Dim Y As Short
            Dim Width As UShort
            Dim Height As UShort
        End Structure
        Structure sFrameInfo
            Dim Padding1 As Short
            Dim Operation As FrameOperations
            Dim SplittedImageCount As Byte '当前结构关联多少个ImageSplitter
            Dim pFrameParam As UInteger '指向一个结构体，此地址是与当前结构在此结构组的索引*8对应的，没有与Splitter对应，且8字节对齐（与下一帧的Squence有关？）
            Dim SplitterOffset As UInteger '相对于Splitter起始地址
            Enum FrameOperations As Byte
                DrawOnly
                PlayAnimation
                LoopAnimation
            End Enum
        End Structure
        Structure sSquence
            Dim FrameID As UShort '表示这关联第几组FrameInfo
            Dim LoopCount As UShort
            Dim Padding As Integer
        End Structure
        Structure sActionWrapper '封装了动作的Squence
            Dim SquenceCount As UInteger
            Dim SquenceOffset As UInteger
        End Structure
    End Structure
    Public Shared Function LoadFromArray(Of t)(Data() As Byte, StructSize As Integer) As List(Of t)
        Dim pStruct = UnsafeAddrOfPinnedArrayElement(Data, 0)
        Dim ls As New List(Of t)
        For i As Integer = 0 To Data.Length - 1 Step StructSize
            Dim tmp As t
            tmp = PtrToStructure(pStruct + i, GetType(t))
            ls.Add(tmp)
        Next
        Return ls
    End Function
    Private Shared Function AppendZero(s As String) As String
        If s.Length = 1 Then
            Return "0" + s
        Else
            Return s
        End If
    End Function
    
End Class
