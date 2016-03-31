Option Strict On
Imports System.Runtime.InteropServices.Marshal
Imports System.Runtime.InteropServices
Imports System.Runtime
Imports System.Reflection
'Copyright Nukepayload2 2014
'''<summary>指向值类型的指针。使用时一定要小心，否则会导致不可预知的后果。注意这个类的性能不是特别好。 </summary>
'''<remarks>这些操作都是难以验证的，所以需要完全信任权限。</remarks>
<Security.SecurityCritical()>
Public Class Pointer(Of T As Structure)
    ''' <summary>
    ''' 指向的元素的地址
    ''' </summary>
    ''' <remarks></remarks>
    Public Address As IntPtr
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

    ''' <summary>获取或设置指向的目标(注意!如果要紧凑地序列化和反序列化结构体,则结构体应带有以下特性:&lt;System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack:=1)&gt;)</summary>
    Default Public Property TargetElement(index As Integer) As T
        <TargetedPatchingOptOut("")>
        Set(s As T) '稳定
            If ObjSize = 0 Then Throw New InvalidOperationException(ErrorTextCannotRead)
            StructureToPtr(s, Address + index * ObjSize, False)
        End Set
        <TargetedPatchingOptOut("")>
        Get '不稳定
            If ObjSize = 0 Then Throw New InvalidOperationException(ErrorTextCannotRead)
            Return DirectCast(PtrToStructure(Address + ObjSize * index, GetType(T)), T) '拆箱
        End Get
    End Property

    ''' <summary>
    ''' 转换为IntPtr
    ''' </summary>
    ''' <param name="p">Pointer</param>
    <TargetedPatchingOptOut("")>
    Public Shared Narrowing Operator CType(p As Pointer(Of T)) As IntPtr
        Return p.Address
    End Operator

    ''' <summary>
    ''' 从IntPtr新建指针
    ''' </summary>
    ''' <param name="p">地址</param>
    <TargetedPatchingOptOut("")>
    Public Shared Widening Operator CType(p As IntPtr) As Pointer(Of T)
        Return New Pointer(Of T)(p, SizeOf(GetType(T)))
    End Operator

    ''' <summary>c样式的指针加法，会自动计算元素的大小</summary>
    <TargetedPatchingOptOut("")>
    Public Shared Operator +(a As Pointer(Of T), b As Integer) As Pointer(Of T)
        Return New Pointer(Of T)(a.Address + b * a.ObjSize, a.ObjSize)
    End Operator

    ''' <summary>c样式的指针减法，会自动计算元素的大小</summary>
    <TargetedPatchingOptOut("")>
    Public Shared Operator -(a As Pointer(Of T), b As Integer) As Pointer(Of T)
        Return New Pointer(Of T)(a.Address - b * a.ObjSize, a.ObjSize)
    End Operator

    '''<summary>初始化一个用于操作值类型的指针。为了高效，结构体指针最好用结构体数组代替。文件指针请用对应的Stream代替。</summary>
    <TargetedPatchingOptOut("")>
    Sub New(Addr As IntPtr, UnitSize As Integer)
        Address = Addr
        ObjSize = UnitSize
    End Sub
    '''<summary>为值类型分配指针</summary>
    <TargetedPatchingOptOut("")>
    Sub New(obj As T)
        Address = VarPtr(obj)
        ObjSize = SizeOf(GetType(T))
    End Sub
    ''' <summary>
    ''' 获取地址
    ''' </summary>
    ''' <typeparam name="A"></typeparam>
    ''' <param name="ele">要获取地址的元素。如果是非基元结构则引发异常。</param>
    ''' <param name="Free">是否自动释放句柄。释放后的瞬间固定过的数据一般不会移动，但是时间长一点就很可能会移动。填写False会有降低应用程序性能和内存泄漏的风险。</param>
    <TargetedPatchingOptOut("")>
    Public Shared Function VarPtr(Of A As Structure)(ele As A, Optional Free As Boolean = True) As IntPtr
        Dim GC = GCHandle.Alloc(ele, GCHandleType.Pinned)
        Dim GC2 = GC.AddrOfPinnedObject
        If Free Then GC.Free()
        Return GC2
    End Function
    ''' <summary>
    ''' 获取数组地址
    ''' </summary>
    ''' <param name="arr">要获取地址的数组</param>
    <TargetedPatchingOptOut("")>
    Public Shared Function VarPtr(arr As Array) As IntPtr
        Return UnsafeAddrOfPinnedArrayElement(arr, 0)
    End Function

    ''' <summary>
    ''' 从数组新建指针
    ''' </summary>
    ''' <param name="arr">要转换成指针的数组</param>
    <TargetedPatchingOptOut("")>
    Public Shared Function FromArray(arr As Array) As Pointer(Of T)
        Return New Pointer(Of T)(UnsafeAddrOfPinnedArrayElement(arr, 0), SizeOf(GetType(T)))
    End Function

    ''' <summary>
    ''' 从值类型新建指针
    ''' </summary>
    ''' <param name="v">要获取地址的变量</param>
    ''' <param name="Free">是否自动释放句柄。释放后的瞬间固定过的数据一般不会移动，但是时间长一点就很可能会移动。填写False会有降低应用程序性能和内存泄漏的风险。</param>
    <TargetedPatchingOptOut("")>
    Public Shared Function FromValueType(v As ValueType, Optional Free As Boolean = True) As Pointer(Of T)
        Dim GC = GCHandle.Alloc(v, GCHandleType.Pinned)
        Dim res = New Pointer(Of T)(GC.AddrOfPinnedObject, SizeOf(v))
        If Free Then GC.Free()
        Return res
    End Function
    ''' <summary>
    ''' 从地址新建指针
    ''' </summary>
    ''' <param name="p">地址</param>
    <TargetedPatchingOptOut("")>
    Public Shared Function FromIntPtr(p As IntPtr) As Pointer(Of T)
        Return New Pointer(Of T)(p, SizeOf(GetType(T)))
    End Function
    ''' <summary>
    ''' 转换指针类型
    ''' </summary>
    ''' <typeparam name="NewPtr">新的指针类型</typeparam>
    <TargetedPatchingOptOut("")>
    Public Function SwitchType(Of NewPtr As Structure)() As Pointer(Of NewPtr)
        Return New Pointer(Of NewPtr)(CType(Address, IntPtr), SizeOf(GetType(NewPtr)))
    End Function
End Class