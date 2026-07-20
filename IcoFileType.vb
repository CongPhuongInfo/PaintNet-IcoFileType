Option Strict On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms
Imports PaintDotNet

''' <summary>
''' IcoFileType - Paint.NET FileType plugin, KHONG PHAI Effect.
''' Them dinh dang .ico vao menu File > Open va File > Save As cua Paint.NET.
'''
''' KHAC BIET QUAN TRONG SO VOI CAC EFFECT TRONG BO:
''' - FileType xu ly VIEC MO/LUU FILE, khong xu ly pixel tren canvas dang mo.
''' - Class ke thua tu PaintDotNet.FileType (khong phai Effect/PropertyBasedEffect).
''' - Can them mot IFileTypeFactory de Paint.NET biet cach khoi tao plugin.
''' - Sau khi build, DLL phai duoc dat vao thu muc "FileTypes\" cua Paint.NET,
'''   KHONG PHAI thu muc "Effects\" (2 thu muc load plugin rieng biet).
'''
''' DINH DANG ICO XUAT RA:
''' Dung "PNG-frame ICO" (chuan tu Windows Vista tro di): moi kich thuoc trong
''' file .ico la 1 anh PNG nen day du (ho tro alpha/trong suot muot ma), thay
''' vi BMP+AND-mask kieu cu. Windows hien dai (Explorer, taskbar...) doc dinh
''' dang nay binh thuong; chi cac ung dung rat cu (truoc Vista) doi hoi BMP
''' frame moi khong tuong thich - truong hop nay rat hiem gap trong thuc te.
''' </summary>
Public NotInheritable Class IcoFileType
    Inherits FileType

    Public Sub New()
        MyBase.New("Icon",
                   New FileTypeOptions() With {
                       .LoadExtensions = New String() {".ico"},
                       .SaveExtensions = New String() {".ico"},
                       .SupportsLayers = False,
                       .SupportsCancellation = True
                   })
    End Sub

    ' =========================================================
    ' MO FILE (.ico -> Document)
    ' =========================================================
    Protected Overrides Function OnLoad(input As Stream) As Document
        ' Yeu cau frame lon nhat co san (256x256 neu file co); Windows se tu
        ' chon frame gan nhat neu khong co dung 256x256.
        Using icon As New Icon(input, New Size(256, 256))
            Using rawBmp As Bitmap = icon.ToBitmap()
                ' Dam bao dung dinh dang 32bppArgb de giu alpha khi chuyen sang Surface.
                Using bmp As New Bitmap(rawBmp.Width, rawBmp.Height, PixelFormat.Format32bppArgb)
                    Using g As Graphics = Graphics.FromImage(bmp)
                        g.DrawImage(rawBmp, 0, 0, rawBmp.Width, rawBmp.Height)
                    End Using

                    Dim surface As Surface = Surface.CopyFromBitmap(bmp)
                    Dim doc As New Document(surface.Width, surface.Height)
                    Dim layer As New BitmapLayer(surface)
                    layer.Name = "Background"
                    doc.Layers.Add(layer)
                    Return doc
                End Using
            End Using
        End Using
    End Function

    ' =========================================================
    ' LUU FILE (Document -> .ico)
    ' =========================================================
    Protected Overrides Sub OnSave(input As Document, output As Stream, token As SaveConfigToken, scratchSurface As Surface, callback As ProgressEventHandler)
        Dim icoToken As IcoSaveConfigToken = TryCast(token, IcoSaveConfigToken)
        Dim sizes As Integer() = If(icoToken IsNot Nothing, icoToken.Sizes, IcoSaveConfigToken.DefaultSizes)

        If sizes Is Nothing OrElse sizes.Length = 0 Then
            sizes = IcoSaveConfigToken.DefaultSizes
        End If

        Using sourceBmp As Bitmap = scratchSurface.CreateAliasedBitmap()
            WriteIco(output, sourceBmp, sizes, callback)
        End Using
    End Sub

    Protected Overrides Function OnCreateDefaultSaveConfigToken() As SaveConfigToken
        Return New IcoSaveConfigToken(IcoSaveConfigToken.DefaultSizes)
    End Function

    Public Overrides Function CreateSaveConfigWidget() As SaveConfigWidget
        Return New IcoSaveConfigWidget()
    End Function

    ''' <summary>
    ''' Ghi ra dinh dang .ico hoan chinh (ICONDIR + N ICONDIRENTRY + N anh PNG),
    ''' moi kich thuoc trong "sizes" duoc resize (giu ty le, can giua, nen trong
    ''' suot xung quanh neu anh goc khong vuong) roi nen thanh PNG.
    ''' </summary>
    Private Shared Sub WriteIco(output As Stream, sourceBmp As Bitmap, sizes As Integer(), callback As ProgressEventHandler)
        Dim distinctSizes As Integer() = sizes.Distinct().OrderBy(Function(s) s).ToArray()
        Dim frames As New List(Of Byte())()

        For i As Integer = 0 To distinctSizes.Length - 1
            Dim size As Integer = distinctSizes(i)
            Using framedBmp As Bitmap = FitToSquare(sourceBmp, size)
                Using ms As New MemoryStream()
                    framedBmp.Save(ms, ImageFormat.Png)
                    frames.Add(ms.ToArray())
                End Using
            End Using

            If callback IsNot Nothing Then
                Dim percent As Double = CDbl(i + 1) / distinctSizes.Length * 100.0
                callback(Nothing, New ProgressEventArgs(percent))
            End If
        Next

        Using bw As New BinaryWriter(output)
            ' --- ICONDIR (6 bytes) ---
            bw.Write(CUShort(0))                       ' Reserved, phai la 0
            bw.Write(CUShort(1))                       ' Type = 1 (icon, 2 = cursor)
            bw.Write(CUShort(distinctSizes.Length))     ' So luong anh trong file

            ' --- ICONDIRENTRY (16 bytes/anh) ---
            Dim dataOffset As UInteger = CUInt(6 + 16 * distinctSizes.Length)
            For i As Integer = 0 To distinctSizes.Length - 1
                Dim size As Integer = distinctSizes(i)
                Dim byteLen As Integer = frames(i).Length

                ' Width/Height: gia tri 0 nghia la 256 theo chuan ICO.
                bw.Write(CByte(If(size >= 256, 0, size)))
                bw.Write(CByte(If(size >= 256, 0, size)))
                bw.Write(CByte(0))                      ' So mau trong bang mau (0 = khong dung palette)
                bw.Write(CByte(0))                      ' Reserved
                bw.Write(CUShort(1))                    ' Color planes
                bw.Write(CUShort(32))                   ' Bits per pixel (32 = RGBA)
                bw.Write(CUInt(byteLen))                ' Kich thuoc du lieu anh (bytes)
                bw.Write(dataOffset)                     ' Vi tri anh trong file

                dataOffset += CUInt(byteLen)
            Next

            ' --- Du lieu PNG cua tung anh, theo dung thu tu offset da ghi ---
            For Each frameBytes As Byte() In frames
                bw.Write(frameBytes)
            Next

            bw.Flush()
        End Using
    End Sub

    ''' <summary>
    ''' Resize anh goc giu nguyen ty le vao trong 1 khung vuong targetSize x
    ''' targetSize, can giua, phan con lai trong suot (khong keo dan bien dang
    ''' anh khi anh goc khong vuong).
    ''' </summary>
    Private Shared Function FitToSquare(source As Bitmap, targetSize As Integer) As Bitmap
        Dim result As New Bitmap(targetSize, targetSize, PixelFormat.Format32bppArgb)

        Dim scale As Double = Math.Min(targetSize / CDbl(source.Width), targetSize / CDbl(source.Height))
        Dim drawWidth As Integer = Math.Max(1, CInt(Math.Round(source.Width * scale)))
        Dim drawHeight As Integer = Math.Max(1, CInt(Math.Round(source.Height * scale)))
        Dim offsetX As Integer = (targetSize - drawWidth) \ 2
        Dim offsetY As Integer = (targetSize - drawHeight) \ 2

        Using g As Graphics = Graphics.FromImage(result)
            g.CompositingMode = CompositingMode.SourceOver
            g.CompositingQuality = CompositingQuality.HighQuality
            g.InterpolationMode = InterpolationMode.HighQualityBicubic
            g.SmoothingMode = SmoothingMode.HighQuality
            g.PixelOffsetMode = PixelOffsetMode.HighQuality
            g.Clear(Color.Transparent)
            g.DrawImage(source, New Rectangle(offsetX, offsetY, drawWidth, drawHeight))
        End Using

        Return result
    End Function
End Class

''' <summary>
''' Cac tuy chon khi luu .ico: danh sach kich thuoc (px) se duoc dua vao file.
''' </summary>
Public NotInheritable Class IcoSaveConfigToken
    Inherits SaveConfigToken

    Public Shared ReadOnly DefaultSizes As Integer() = New Integer() {16, 32, 48, 256}
    Public Shared ReadOnly AllAvailableSizes As Integer() = New Integer() {16, 24, 32, 48, 64, 128, 256}

    Public Property Sizes As Integer()

    Public Sub New(sizes As Integer())
        Me.Sizes = sizes
    End Sub

    Public Overrides Function Clone() As Object
        Return New IcoSaveConfigToken(CType(Sizes.Clone(), Integer()))
    End Function
End Class

''' <summary>
''' UI trong hop thoai Save As: checkbox chon cac kich thuoc muon dua vao .ico.
''' LUU Y VE PHIEN BAN SDK: lop co so SaveConfigWidget va ten cac ham can
''' override (thuong la InitTokenFromWidget / InitWidgetFromToken) co the
''' khac nhau chut it giua cac ban Paint.NET SDK. Neu build bao loi o cac ham
''' nay, mo Object Browser/IntelliSense tren PaintDotNet.Base.dll dang cai de
''' doi chieu dung ten/chu ky ham.
''' </summary>
Public NotInheritable Class IcoSaveConfigWidget
    Inherits SaveConfigWidget

    Private ReadOnly checkBoxes As New Dictionary(Of Integer, CheckBox)()

    Public Sub New()
        Me.SuspendLayout()

        Dim y As Integer = 8
        For Each size As Integer In IcoSaveConfigToken.AllAvailableSizes
            Dim cb As New CheckBox() With {
                .Text = $"{size} x {size} px",
                .Location = New Point(8, y),
                .AutoSize = True
            }
            AddHandler cb.CheckedChanged, AddressOf OnCheckedChanged
            Me.Controls.Add(cb)
            checkBoxes(size) = cb
            y += 24
        Next

        Me.Size = New Size(160, y + 8)
        Me.ResumeLayout(False)
    End Sub

    Private Sub OnCheckedChanged(sender As Object, e As EventArgs)
        Me.InitTokenFromWidget()
    End Sub

    Protected Overrides Sub InitTokenFromWidget()
        Dim selected As New List(Of Integer)()
        For Each kv As KeyValuePair(Of Integer, CheckBox) In checkBoxes
            If kv.Value.Checked Then selected.Add(kv.Key)
        Next

        If selected.Count = 0 Then
            selected.AddRange(IcoSaveConfigToken.DefaultSizes)
        End If

        Dim tok As New IcoSaveConfigToken(selected.ToArray())
        Me.Token = tok
    End Sub

    Protected Overrides Sub InitWidgetFromToken(sourceToken As SaveConfigToken)
        Dim icoToken As IcoSaveConfigToken = TryCast(sourceToken, IcoSaveConfigToken)
        Dim sizes As Integer() = If(icoToken IsNot Nothing, icoToken.Sizes, IcoSaveConfigToken.DefaultSizes)

        For Each kv As KeyValuePair(Of Integer, CheckBox) In checkBoxes
            kv.Value.Checked = sizes.Contains(kv.Key)
        Next
    End Sub
End Class

''' <summary>
''' Factory de Paint.NET tim thay va khoi tao IcoFileType. Day la diem "vao"
''' bat buoc phai co trong moi FileType plugin - Paint.NET quet DLL trong
''' thu muc FileTypes\ de tim class implement IFileTypeFactory.
''' </summary>
Public NotInheritable Class IcoFileTypeFactory
    Implements IFileTypeFactory

    Public Function GetFileTypeInstances() As FileType() Implements IFileTypeFactory.GetFileTypeInstances
        Return New FileType() {New IcoFileType()}
    End Function
End Class
