Imports System.Collections.ObjectModel

Class MainWindow

    Private Sub TextBlock_Drop(sender As Object, e As DragEventArgs)
        Dim f = CType(e.Data.GetData(DataFormats.FileDrop), String())
        RenameFiles(f)
    End Sub

    Private Sub RenameFiles(f As String())
        errlist.ItemsSource = New ObservableCollection(Of Object) From {New With {.ex = "请稍候", .fn = "............."}}
        Application.DoEvents()
        Dim ren As New RawFileRenamer
        Dim Errs As New ObservableCollection(Of ErrInfo)
        System.Threading.Tasks.Parallel.ForEach(f, Sub(fn As String)
                                                       sam.Wait()
                                                       Try
                                                           ren.auto_rename(fn)
                                                       Catch ex As Exception
                                                           Errs.Add(New ErrInfo(ex, fn))
                                                       Finally
                                                           sam.Release()
                                                       End Try
                                                   End Sub)
        If Errs.Count = 0 Then
            errlist.ItemsSource = New ObservableCollection(Of Object) From {New With {.ex = "无错误", .fn = "............."}}
        Else
            errlist.ItemsSource = Errs
        End If
    End Sub

    Private Sub TextBlock_Drop_1(sender As Object, e As DragEventArgs)
        Dim f = CType(e.Data.GetData(DataFormats.FileDrop), String())
        DecompressFiles(f)
    End Sub

    Private Function DecompressFiles(f As String(), Optional Is11 As Boolean = False) As List(Of String)
        errlist.ItemsSource = New ObservableCollection(Of Object) From {New With {.ex = "请稍候", .fn = "............."}}
        Application.DoEvents()
        Dim Errs As New ObservableCollection(Of ErrInfo)
        Dim er As New List(Of String)
        System.Threading.Tasks.Parallel.ForEach(f, Sub(fn As String)
                                                       sam.Wait()
                                                       Try
                                                           Dim z As LZBase
                                                           If Is11 Then
                                                               z = New LZ77_11(fn)
                                                           Else
                                                               z = New lz77(fn)
                                                           End If
                                                           z.unzipData(IO.Path.GetDirectoryName(fn) + "\" + IO.Path.GetFileNameWithoutExtension(fn) + ".bin")
                                                       Catch ex As Exception
                                                           Errs.Add(New ErrInfo(ex, fn))
                                                           er.Add(fn)
                                                       Finally
                                                           sam.Release()
                                                       End Try
                                                   End Sub)
        If Errs.Count = 0 Then
            errlist.ItemsSource = New ObservableCollection(Of Object) From {New With {.ex = "无错误", .fn = "............."}}
        Else
            errlist.ItemsSource = Errs
        End If
        Return er
    End Function

    Structure ErrInfo
        Public Property ex As Exception
        Public Property fn As String
        Sub New(er As Exception, f As String)
            ex = er
            fn = f
        End Sub
    End Structure

    Private Sub TextBlock_Drop_2(sender As Object, e As DragEventArgs)
        Dim f = CType(e.Data.GetData(DataFormats.FileDrop), String())
        RenameFiles(IO.Directory.GetFiles(IO.Path.GetDirectoryName(f(0))))
        Dim lz As New List(Of String)
        For Each fn In IO.Directory.GetFiles(IO.Path.GetDirectoryName(f(0)))
            Select Case IO.Path.GetExtension(fn).ToLowerInvariant
                Case ".empty"
                    IO.File.Delete(fn)
                Case ".lz77"
                    lz.Add(fn)
            End Select
        Next
        Dim er = DecompressFiles(lz.ToArray)
        Dim src = errlist.ItemsSource
        RenameFiles(IO.Directory.GetFiles(IO.Path.GetDirectoryName(f(0))))
        errlist.ItemsSource = src
        For Each fn In IO.Directory.GetFiles(IO.Path.GetDirectoryName(f(0)))
            Select Case IO.Path.GetExtension(fn).ToLowerInvariant
                Case ".empty", ".mix3d"
                    IO.File.Delete(fn)
                Case ".lz77"
                    If Not er.Contains(fn) Then IO.File.Delete(fn)
            End Select
        Next
    End Sub
    Private Delegate Sub FileProc(fn As String)
    Private Delegate Sub FileProcWarning(text As String)
    Dim sam As New System.Threading.SemaphoreSlim(1)
    Private Sub ProcessFiles(f() As String, Proc As FileProc)
        errlist.ItemsSource = New ObservableCollection(Of Object) From {New With {.ex = "请稍候", .fn = "............."}}
        Application.DoEvents()
        Dim Errs As New ObservableCollection(Of ErrInfo)
        Dim er As New List(Of String)
        System.Threading.Tasks.Parallel.ForEach(f, Sub(fn As String)
                                                       sam.Wait()
                                                       Try
                                                           Proc(fn)
                                                       Catch ex As Exception
                                                           Errs.Add(New ErrInfo(ex, fn))
                                                           er.Add(fn)
                                                       Finally
                                                           sam.Release()
                                                       End Try
                                                   End Sub)
        If Errs.Count = 0 Then
            errlist.ItemsSource = New ObservableCollection(Of Object) From {New With {.ex = "无错误", .fn = "............."}}
        Else
            errlist.ItemsSource = Errs
        End If
    End Sub
    Private Sub TextBlock_Drop_3(sender As Object, e As DragEventArgs)
        Dim f = CType(e.Data.GetData(DataFormats.FileDrop), String())
        ProcessFiles(f, Sub(fn As String)
                            Dim z As New TxrcSbinDecryptor(fn)
                            z.Decrypt(IO.Path.GetDirectoryName(fn) + "\" + IO.Path.GetFileNameWithoutExtension(fn) + ".bin")
                        End Sub)
   End Sub

    Private Sub TextBlock_Drop_4(sender As Object, e As DragEventArgs)
        Dim f = DirectCast(e.Data.GetData(DataFormats.FileDrop), String())
        ProcessFiles(f, Sub(fn As String)
                            Call New SDAT().UnpackSDAT(f)
                        End Sub)
    End Sub

    Private Sub tblDecompLz11_Drop(sender As Object, e As DragEventArgs) Handles tblDecompLz11.Drop
        Dim f = CType(e.Data.GetData(DataFormats.FileDrop), String())
        DecompressFiles(f, True)
    End Sub

    Private Sub tblSWAR_Drop(sender As Object, e As DragEventArgs) Handles tblSWAR.Drop
        Dim f = DirectCast(e.Data.GetData(DataFormats.FileDrop), String())
        ProcessFiles(f, Sub(fn As String)
                            SWAR.Write(SWAR.ConvertToSWAV(SWAR.Read(fn)), fn)
                        End Sub)
    End Sub

    Private Sub tblNARC_Drop(sender As Object, e As DragEventArgs) Handles tblNARC.Drop
        Dim f = DirectCast(e.Data.GetData(DataFormats.FileDrop), String())
        ProcessFiles(f, Sub(fn As String)
                            Dim strm = IO.File.OpenRead(fn)
                            Write_sFolder(strm, New NARC().Unpack(strm, fn), IO.Path.GetDirectoryName(fn))
                            strm.Close()
                        End Sub)
    End Sub

    Private Sub TextBlock_Drop_5(sender As Object, e As DragEventArgs)
        Dim f = DirectCast(e.Data.GetData(DataFormats.FileDrop), String())
        ProcessFiles(f, Sub(fn As String)
                            Dim simg As New Pack.Img(fn)
                            simg.DumpPacks(IO.Path.GetDirectoryName(fn) + "\img_bin")
                        End Sub)
    End Sub

    Private Sub tblPack_Drop(sender As Object, e As DragEventArgs) Handles tblPack.Drop
        Dim f = DirectCast(e.Data.GetData(DataFormats.FileDrop), String())
        ProcessFiles(f, Sub(fn As String)
                            Dim pak As New Pack.sPack(fn)
                            pak.DecomressAllFiles()
                        End Sub)
    End Sub

    Private Sub lblsbin2vb_Drop(sender As Object, e As DragEventArgs) Handles lblsbin2vb.Drop

    End Sub
End Class

<ValueConversion(GetType(Object), GetType(String))>
Class ErrConv
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As Globalization.CultureInfo) As Object Implements IValueConverter.Convert
        If IsNothing(value) Then Return ""
        If Not TypeOf value Is Exception Then Return value.ToString
        Dim ex As Exception = value
        Return ex.Message + vbCrLf + "类型:" + ex.GetType.ToString
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As Globalization.CultureInfo) As Object Implements IValueConverter.ConvertBack
        Return New Exception(value.ToString.Substring(0, value.ToString.IndexOf(vbCrLf)))
    End Function
End Class