# Dịch màn hình

Ứng dụng Windows thử nghiệm để chọn một vùng trên màn hình, nhận diện vị trí từng dòng bằng OCR có sẵn của Windows và phủ bản dịch lên dòng gốc. Chữ dịch nằm trong cửa sổ riêng, không nhận click hay lấy focus; một thanh nhỏ riêng dùng để làm mới, bật/tắt tự quét hoặc đóng lớp phủ.

Ứng dụng chỉ OCR ảnh màn hình qua `Graphics.CopyFromScreen` và OCR của Windows; không có chế độ HOOK, không sửa tệp game, chèn DLL, đọc bộ nhớ hay gửi thao tác bàn phím/chuột tới game. Dù vậy, một số game/anti-cheat có thể chặn hoặc không cho phép mọi loại overlay; không thể bảo đảm điều này sẽ không ảnh hưởng tới tài khoản.

## Chạy bản dựng sẵn sau khi clone

Repo có kèm bản Windows x64 self-contained trong `publish/`. Sau khi clone, mở PowerShell tại thư mục repo và chạy:

```powershell
.\publish\DichManHinh.exe
```

Không cần cài .NET SDK để chạy bản này. Giữ nguyên toàn bộ thư mục `publish/` vì EXE cần các DLL và tệp đi kèm. Nếu Windows SmartScreen hiện cảnh báo vì ứng dụng cá nhân chưa được ký, chỉ tiếp tục nếu bạn tin tưởng repo này.

## Dùng ứng dụng

1. Chọn ngôn ngữ OCR đã cài trong Windows.
2. Nhấn **Chọn vùng để dịch**, kéo quanh chữ trong game; nhấn Esc để hủy.
3. Bản dịch sẽ che dòng gốc; tự quét được bật sẵn với chu kỳ 2 giây. Chọn chu kỳ 1, 2, 3 hoặc 5 giây trong màn hình chính hoặc thanh điều khiển. Tắt **Tự quét** nếu muốn chỉ dịch thủ công; nút làm mới sẽ dịch ngay.

Cửa sổ chính hỗ trợ thu nhỏ, phóng to và thay đổi kích thước. Khi cửa sổ thấp, có thể cuộn để xem các thiết lập bên dưới; khi phóng to, nội dung được căn giữa để dễ đọc.

Khi quét lại, bản dịch cũ được giữ trong lúc OCR và dịch ảnh mới. Ứng dụng cập nhật các nhãn hiện có thay vì dựng lại toàn bộ lớp phủ; nếu Windows không thể loại overlay khỏi ảnh chụp, ứng dụng dùng chế độ tương thích có thể nháy nhẹ trong lúc chụp. Nếu nội dung không đổi, bản dịch đã lưu được dùng lại thay vì gửi lại yêu cầu dịch.

Chế độ cửa sổ không viền (borderless) thường tương thích với overlay tốt hơn chế độ toàn màn hình độc quyền.

## Ngôn ngữ và quyền riêng tư

- OCR chạy cục bộ bằng bộ nhận diện ngôn ngữ của Windows. Chỉ các ngôn ngữ có gói OCR đã cài mới xuất hiện trong danh sách; cần cài thêm tính năng ngôn ngữ/OCR của Windows để nhận diện ngôn ngữ khác.
- Ảnh chụp vùng chọn không được tải lên. Từng dòng chữ OCR trích xuất được gửi qua Internet tới `translate.googleapis.com` để dịch tự động sang tiếng Việt; không cần API key. Không dùng ứng dụng nếu không muốn gửi phần chữ đó tới dịch vụ bên ngoài.
- Đây là bản thử nghiệm dùng endpoint dịch không chính thức; dịch vụ có thể giới hạn hoặc thay đổi. OCR cũng có thể sai với chữ nhỏ, font cách điệu hoặc hiệu ứng trong game.
- Các dòng được dịch riêng để đặt đúng vị trí; ngữ cảnh giữa nhiều dòng có thể không đầy đủ. Chu kỳ quét là khoảng tối thiểu mục tiêu; kết quả mới có thể chậm hơn nếu OCR hoặc mạng mất thêm thời gian.
- Ứng dụng chỉ quét vùng đã chọn. Một số game toàn màn hình/anti-cheat có thể chặn chụp ảnh hoặc overlay.

## Chạy hoặc tự publish từ mã nguồn

Cần .NET SDK 11 và Windows 10 phiên bản 2004 trở lên:

```powershell
dotnet run
```

Tạo lại bản chạy độc lập:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -o publish
```

Sau khi cập nhật mã nguồn, hãy publish lại và commit các thay đổi trong `publish/` để người clone repo nhận được bản chạy mới nhất.
