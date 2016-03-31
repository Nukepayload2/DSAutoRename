Imports System.IO
Imports DSAutoRename.SDAT

Module FileExportHelper
    Public Sub UnpackFiles(strm As Stream, files As IList(Of Sound), dir As String)
        If files Is Nothing Then Return
        For Each snd In files
            Dim buf(snd.size - 1) As Byte
            strm.Position = snd.offset
            strm.Read(buf, 0, snd.size)
            IO.File.WriteAllBytes(dir + snd.name, buf)
        Next
    End Sub
    Public Sub UnpackFolders(strm As Stream, folders As IList(Of Folder), dir As String)
        If folders Is Nothing Then Return
        For Each fi In folders
            UnpackFiles(strm, fi.files, dir)
        Next
        For Each fo In folders
            UnpackFolders(strm, fo.folders, dir)
        Next
    End Sub

    Public Sub Write_sFolder(strm As Stream, Folder As sFolder, Dire As String)
        Dim CurrentFolder = Dire + "\"
        Dim UnpackFi As New Action(Of IEnumerable(Of sFile))(
            Sub(fil As IEnumerable(Of sFile))
                If fil Is Nothing Then Return
                If Not Directory.Exists(CurrentFolder) Then Directory.CreateDirectory(CurrentFolder)
                For Each f In fil
                    Dim buf(f.size - 1) As Byte
                    strm.Position = f.offset
                    strm.Read(buf, 0, f.size)
                    Dim tatg = Path.Combine(CurrentFolder, f.name)
                    Debug.WriteLine(tatg)
                    File.WriteAllBytes(tatg, buf)
                Next
            End Sub)
        Dim UnpackDir As Action(Of IEnumerable(Of sFolder))
        UnpackDir = New Action(Of IEnumerable(Of sFolder))(
            Sub(dirs As IEnumerable(Of sFolder))
                If dirs Is Nothing Then Return
                Dim OldDire = CurrentFolder
                For Each d In dirs
                    CurrentFolder = Path.Combine(CurrentFolder, d.name)
                    UnpackFi.Invoke(d.files)
                    UnpackDir.Invoke(d.folders)
                Next
                CurrentFolder = OldDire
            End Sub)
        UnpackFi.Invoke(Folder.files)
        UnpackDir.Invoke(Folder.folders)
    End Sub
End Module 