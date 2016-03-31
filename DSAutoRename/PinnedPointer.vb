Imports System.Runtime
Imports System.Runtime.InteropServices.Marshal
Imports System.Runtime.InteropServices

''' <summary>
''' 改良版的Pointer类。作用类似于c++的pin_ptr。能自由控制它的释放。
''' </summary>
''' <typeparam name="T"></typeparam>
''' <remarks></remarks>
<Security.SecurityCritical()>
Public Class PinnedPointer(Of T As Structure)
    Inherits CriticalHandle
    Implements IDisposable
    ''' <summary>
    ''' 当前的GCHandle。不要用它释放句柄。
    ''' </summary>
    ''' <remarks></remarks>
    Public HGc As GCHandle
    ''' <summary>
    ''' 目标的地址
    ''' </summary>
    Public Property Address As IntPtr
        Get
            Return handle
        End Get
        Set(value As IntPtr)
            handle = value
        End Set
    End Property
    ''' <summary>
    ''' 目标元素的大小
    ''' </summary>
    ''' <remarks></remarks>
    Public ObjSize As Integer
    Private Const ErrorTextCannotRead As String = "Void指针不支持此操作，请把ObjSize设置为大于0的值。"
    '''<summary> 读写目标内存 </summary>
    Public Property Target As T
        <TargetedPatchingOptOut("")>
        Get
            Return CType(TargetElement(0), T)
        End Get
        <TargetedPatchingOptOut("")>
        Set(Value As T)
            TargetElement(0) = Value
        End Set
    End Property
    Public Overrides ReadOnly Property IsInvalid As Boolean
        Get
            If IsDisposed Then
                Return False
            Else
                Return Address.ToInt64 > 0
            End If
        End Get
    End Property
    ''' <summary>获取或设置指向的目标(注意!如果要紧凑地序列化和反序列化结构体,则结构体应带有以下特性:&lt;System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack:=1)&gt;)</summary>
    Default Public Property TargetElement(index As Integer) As T
        <TargetedPatchingOptOut("")>
        Set(s As T)
            If ObjSize = 0 Then Throw New InvalidOperationException(ErrorTextCannotRead)
            StructureToPtr(s, Address + index * ObjSize, False)
        End Set
        <TargetedPatchingOptOut("")>
        Get
            If ObjSize = 0 Then Throw New InvalidOperationException(ErrorTextCannotRead)
            Return DirectCast(PtrToStructure(Address + ObjSize * index, GetType(T)), T) '拆箱
        End Get
    End Property

    ''' <summary>
    ''' 转换为IntPtr
    ''' </summary>
    ''' <param name="p">Pointer</param>
    <TargetedPatchingOptOut("")>
    Public Shared Narrowing Operator CType(p As PinnedPointer(Of T)) As IntPtr
        Return p.Address
    End Operator
    ''' <summary>c样式的指针加法(+=)，会自动计算元素的大小</summary>
    Public Sub Add(Value As Integer)
        Address += Value * ObjSize
    End Sub
    ''' <summary>把指针当作一般的数字增加，不会自动计算元素的大小</summary>
    Public Sub AddPosition(Value As Integer)
        Address += Value
    End Sub
    ''' <summary>c样式的指针减法(-=)，会自动计算元素的大小</summary>
    Public Sub Minus(Value As Integer)
        Address -= Value * ObjSize
    End Sub
    ''' <summary>把指针当作一般的数字减少，不会自动计算元素的大小</summary>
    Public Sub MinusPosition(Value As Integer)
        Address -= Value
    End Sub
    ''' <summary>c样式的指针加法，会自动计算元素的大小</summary>
    <TargetedPatchingOptOut("")>
    Public Shared Operator +(a As PinnedPointer(Of T), b As Integer) As PinnedPointer(Of T)
        Dim ptr = New PinnedPointer(Of T)(a.HGc)
        ptr.Address += b * a.ObjSize
        Return ptr
    End Operator

    ''' <summary>c样式的指针减法，会自动计算元素的大小</summary>
    <TargetedPatchingOptOut("")>
    Public Shared Operator -(a As PinnedPointer(Of T), b As Integer) As PinnedPointer(Of T)
        Dim ptr = New PinnedPointer(Of T)(a.HGc)
        ptr.Address -= b * a.ObjSize
        Return ptr
    End Operator
    <TargetedPatchingOptOut("")>
    Private Sub AddrInit()
        Address = HGc.AddrOfPinnedObject()
        ObjSize = SizeOf(GetType(T))
    End Sub
    '''<summary>为值类型分配指针</summary>
    <TargetedPatchingOptOut("")>
    Sub New(obj As T)
        MyBase.New(IntPtr.Zero)
        HGc = GCHandle.Alloc(obj, GCHandleType.Pinned)
        AddrInit()
    End Sub
    '''<summary>从数组获取指针</summary>
    <TargetedPatchingOptOut("")>
    Sub New(arr As T())
        MyBase.New(IntPtr.Zero)
        HGc = GCHandle.Alloc(arr, GCHandleType.Pinned)
        AddrInit()
    End Sub
    ''' <summary>将GCHandle包装进去,用于克隆此对象</summary>
    <TargetedPatchingOptOut("")>
    Sub New(GC_Handle As GCHandle)
        MyBase.New(IntPtr.Zero)
        HGc = GC_Handle
        AddrInit()
    End Sub
    ''' <summary>
    ''' 转换指针类型
    ''' </summary>
    ''' <typeparam name="NewPtr">新的指针类型</typeparam>
    <TargetedPatchingOptOut("")>
    Public Function SwitchType(Of NewPtr As Structure)() As PinnedPointer(Of NewPtr)
        Return New PinnedPointer(Of NewPtr)(HGc)
    End Function

    Protected Overrides Function ReleaseHandle() As Boolean
        If IsDisposed Then Throw New InvalidOperationException("不要重复释放PinnedPointer")
        HGc.Free()
        IsDisposed = True
        Return True
    End Function
    Dim IsDisposed As Boolean = False
    Protected Overrides Sub Finalize()
        If Not IsDisposed Then Dispose()
    End Sub
    ''' <summary>
    ''' 释放GCHandle
    ''' </summary>
    ''' <remarks></remarks>
    Public Overloads Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
    End Sub
    Protected Overloads Sub Dispose(disposing As Boolean)
        ReleaseHandle()
    End Sub
End Class
 