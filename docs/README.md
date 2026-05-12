# Hệ thống Lập Kế Hoạch Sản Xuất

Hệ thống lập kế hoạch sản xuất dành cho quản lý nhà máy.

Hệ thống giúp lập lịch sản xuất dựa trên:

- File module list để map FP, BuildGroup và Function
- File planning để lấy kế hoạch theo `BuildGroup + Gr.xxx`
- File open minutes từ hệ thống MES

Sau khi xử lý dữ liệu, hệ thống tạo ra các **block sản xuất** và cho phép quản lý kéo thả block vào các line sản xuất.

---

# Chức năng chính

- Import file Module List
- Import file Planning
- Import file Excel từ MES
- Thiết lập deadline bắt buộc cho từng Production Group (Gr.xxx)
- Cấu hình BuildGroup theo ca A/B và số người từng ca
- Tự động gán số phút còn lại (từ MES) theo `BuildGroup + Gr.xxx`; cảnh báo và nhập tay nếu FP không match module list
- Gom dữ liệu theo `BuildGroup + Gr.xxx`
- Hiển thị block sản xuất
- Tự động xếp block vào line theo Function và ngày deadline của Gr.xxx
- Kéo thả nguyên cụm block trong cell
- Tính toán công suất theo line và shift
- Cho phép điều chỉnh hiệu suất, số người; tăng ca bằng cách chỉnh số phút trong từng cell (line + shift + ngày)

---

# Khái niệm chính

Hệ thống lập kế hoạch theo **BuildGroup + Production Group (Gr.xxx)**.

Không lập kế hoạch theo từng sản phẩm riêng lẻ.

Mỗi cặp `BuildGroup + Gr.xxx` sẽ được biểu diễn bằng một **block sản xuất**.

Độ dài block tương ứng với **tổng số phút cần sản xuất**.

Thiết kế nghiệp vụ mới được ghi trong [2026-05-11-module-planning-openmin-redesign.md](superpowers/specs/2026-05-11-module-planning-openmin-redesign.md).

---

# Đối tượng sử dụng

Quản lý sản xuất tại nhà máy.

---

# Công nghệ có thể sử dụng

Frontend:

WPF

Backend:

WPF

dùng file json de luu data ( khong can DB)
