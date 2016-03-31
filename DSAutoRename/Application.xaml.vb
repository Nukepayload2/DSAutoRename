Imports System.Windows.Threading

Class Application

    ' 应用程序级事件(例如 Startup、Exit 和 DispatcherUnhandledException)
    ' 可以在此文件中进行处理。
    Private Shared exitFrameCallback As New DispatcherOperationCallback(AddressOf ExitFrame)
    ''' <summary>
    ''' 刷新画面
    ''' </summary>
    ''' <remarks></remarks>
    Public Shared Sub DoEvents()
        Dim nestedFrame As New DispatcherFrame
        Dim exitOperation As DispatcherOperation = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, exitFrameCallback, nestedFrame)
        Dispatcher.PushFrame(nestedFrame)
        If exitOperation.Status <> DispatcherOperationStatus.Completed Then exitOperation.Abort()
    End Sub
    Private Shared Function ExitFrame(state As Object) As Object
        Dim frame As DispatcherFrame = TryCast(state, DispatcherFrame)
        frame.[Continue] = False
        Return Nothing
    End Function

End Class
