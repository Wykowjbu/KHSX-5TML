# Hệ thống Lập Kế Hoạch Sản Xuất

Hệ thống lập kế hoạch sản xuất dành cho quản lý nhà máy.

Hệ thống giúp lập lịch sản xuất dựa trên:

- File release từ bộ phận Marketing
- File open minutes từ hệ thống MES

Sau khi xử lý dữ liệu, hệ thống tạo ra các **block sản xuất** và cho phép quản lý kéo thả block vào các line sản xuất.

---

# Chức năng chính

- Import file Excel từ Marketing
- Import file Excel từ MES
- Thiết lập deadline cho từng Production Group (Gr.xxx); chọn current group làm mốc
- Tự động gán số phút còn lại (từ MES) theo nhóm sản phẩm; cảnh báo và nhập tay nếu không match
- Gom dữ liệu theo nhóm sản phẩm
- Hiển thị block sản xuất
- Kéo thả block vào line sản xuất
- Tính toán công suất theo line và shift
- Cho phép điều chỉnh hiệu suất, số người, tăng ca

---

# Khái niệm chính

Hệ thống lập kế hoạch theo **nhóm sản phẩm (Product Group)**.

Không lập kế hoạch theo từng sản phẩm riêng lẻ.

Mỗi nhóm sản phẩm sẽ được biểu diễn bằng một **block sản xuất**.

Độ dài block tương ứng với **tổng số phút cần sản xuất**.

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