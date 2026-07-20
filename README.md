# IcoFileType — Paint.NET FileType Plugin

Plugin **FileType** (không phải Effect) cho Paint.NET, thêm định dạng `.ico`
vào menu **File > Open** và **File > Save As**.

## Điểm khác biệt so với Effect

- FileType xử lý việc **mở/lưu file**, không xử lý pixel trên canvas đang mở.
- Class kế thừa từ `PaintDotNet.FileType` (không phải `Effect`/`PropertyBasedEffect`).
- Cần thêm một `IFileTypeFactory` để Paint.NET biết cách khởi tạo plugin.
- Sau khi build, DLL phải đặt vào thư mục **`FileTypes\`** của Paint.NET,
  **không phải** thư mục `Effects\` (hai thư mục load plugin riêng biệt).

## Định dạng ICO xuất ra

Dùng "PNG-frame ICO" (chuẩn từ Windows Vista trở đi): mỗi kích thước trong
file `.ico` là một ảnh PNG nén đầy đủ (hỗ trợ alpha/trong suốt mượt mà),
thay vì BMP+AND-mask kiểu cũ.

## Yêu cầu

- **.NET 9 SDK** — https://dotnet.microsoft.com/download
- **Paint.NET** đã cài trên máy (cần các DLL: `PaintDotNet.Base.dll`,
  `PaintDotNet.Effects.Core.dll`, và tùy bản còn cần
  `PaintDotNet.Primitives.dll`)

## Build

Chạy `build_IcoFileType.bat`. Script sẽ:

1. Kiểm tra `dotnet` đã cài chưa.
2. Tự dò thư mục cài Paint.NET (`Program Files\paint.net` hoặc
   `Program Files (x86)\paint.net`).
3. Build ra `bin\IcoFileType.dll`.
4. Hỏi có muốn copy DLL vào `<thư mục Paint.NET>\Effects\` không.

> ⚠️ **Lưu ý:** đây là FileType, không phải Effect — DLL cần nằm ở thư mục
> `FileTypes\`, không phải `Effects\`. Nếu batch script copy vào `Effects\`,
> tự copy thủ công `bin\IcoFileType.dll` vào `<thư mục Paint.NET>\FileTypes\`
> rồi khởi động lại Paint.NET.

Build thủ công nếu cần chỉ định đường dẫn Paint.NET khác:

```
dotnet build "IcoFileType.vbproj" -c Release -p:PdnDir="D:\duong\dan\paint.net" -o bin
```

## Cấu trúc code

- `IcoFileType` — class chính, override `OnLoad`/`OnSave` để đọc/ghi `.ico`.
- `IcoSaveConfigToken` — lưu danh sách kích thước (px) sẽ đưa vào file khi save.
- `IcoSaveConfigWidget` — UI checkbox trong hộp thoại Save As để chọn kích thước.
- `IcoFileTypeFactory` — điểm vào bắt buộc, implement `IFileTypeFactory`.

## Lưu ý về phiên bản Paint.NET SDK

API của lớp `FileType` (tên hàm cần override, có/không override được, kiểu
tham số constructor...) **khác nhau giữa các bản Paint.NET SDK**. Project
này đã từng gặp và phải sửa:

- `CreateDefaultSaveConfigToken()` → không `Overridable` trực tiếp, phải
  override qua `Protected Overrides Function OnCreateDefaultSaveConfigToken()`.
- `CreateSaveConfigWidget()` → ngược lại, vẫn override trực tiếp bằng
  `Public Overrides Function CreateSaveConfigWidget()` (không có bản `On...`).
- Constructor `FileType.New(name, FileTypeFlags, extensions)` đã bị đánh dấu
  obsolete ở bản SDK mới, phải dùng overload nhận `FileTypeOptions`.
- Có thể cần thêm reference `PaintDotNet.Primitives.dll` (chứa kiểu
  `SizeInt32`) tùy bản Paint.NET.

Nếu build báo lỗi tương tự (`BC31086` không override được, hoặc thành phần
obsolete/không tồn tại), mở **Visual Studio → Object Browser**, trỏ vào
`PaintDotNet.Effects.Core.dll` / `PaintDotNet.Base.dll` đang cài trên máy,
tra đúng tên và chữ ký hàm/thuộc tính của bản đang dùng.

## Cài đặt

1. Build xong, copy `bin\IcoFileType.dll` vào `<thư mục Paint.NET>\FileTypes\`.
2. Khởi động lại Paint.NET.
3. `.ico` sẽ xuất hiện trong danh sách định dạng ở **File > Open** và
   **File > Save As**.
