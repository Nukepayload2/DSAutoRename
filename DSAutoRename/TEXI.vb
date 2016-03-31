Public Class TEXI
    Structure sTexI
        Dim Head As Header
        Dim Flag As Integer '&H49584554 TEXI
        Dim Records As Record()
        Structure Header
            Dim FileType As Integer '&H49524553 SERI
            Dim unk1 As Integer
            Dim count As Short
            Dim unk2 As Short
            Dim padding1 As Short
            Dim byteperpixel As Short
            Dim unk3 As Decimal
            Dim unk4 As Long
            Dim unk5 As Short
            Dim _68s As Long
            Dim MapWidth1 As Integer '?
            Dim MapHeight1 As Integer '?
            Dim padding2 As Integer
            Dim PixelFormat As Integer 'shopfiles d,128kb c,768kb b 24bit
            Dim GroupPixelWidth As Integer
        End Structure
        Structure Record
            Dim Width As Integer
            Dim Height As Integer
        End Structure
    End Structure

End Class
