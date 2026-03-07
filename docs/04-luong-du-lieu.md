# Luồng Dữ Liệu

Bước 1

Quản lý import file Excel từ Marketing.

Hệ thống lưu:

- sản phẩm (cột A)
- nhóm sản phẩm (cột K)
- tên function (cột M)
- **số lượng sản phẩm** theo từng Gr.xxx (các cột E–J: giá trị dưới header mỗi Gr là **số lượng**, không phải số phút)
- minutesPerProduct (nếu có) — dùng để tính: `tổng phút = số lượng × minutesPerProduct`

**Mục đích chính của import Marketing:** xác định sản phẩm nào thuộc nhóm sản phẩm nào, và sản phẩm đó ở Gr.xxx nào. Hệ thống lưu tổng số lượng cộng dồn tất cả Gr.xxx (không cần tách từng Gr.xxx).

**Khi import lại file Marketing:** dữ liệu cũ được cập nhật (merge), Gr.xxx mới được thêm vào. Sản phẩm đã tồn tại chỉ cập nhật số liệu mới, không xóa dữ liệu cũ nếu sản phẩm không có trong file mới.

---

Bước 2

Quản lý thiết lập deadline cho **từng** Production Group (Gr.xxx). Cấu hình **current group** dùng để hệ thống biết lấy Gr.xxx nào làm mốc deadline khi lập kế hoạch. Toàn bộ deadline được lưu trong file JSON.

---

Bước 3

Quản lý import file Excel từ MES.

Hệ thống đọc:

- sản phẩm (cột D từ hàng 20)
- số phút còn lại (cột I từ hàng 20)

---

Bước 4

Hệ thống **tự động** match productId (MES ↔ Marketing), sau đó gom theo ProductGroup để xác định sản phẩm thuộc nhóm nào và tổng open minutes theo nhóm. **Nếu sản phẩm trong MES không có trong dữ liệu Marketing** (không match được): hệ thống **bắt buộc cảnh báo** và cho phép **nhập tay** (gán thủ công) để xử lý.

---

Bước 5

Hệ thống gom dữ liệu theo ProductGroup để tạo block. Mỗi **ProductGroup** tương ứng **một block**; tổng độ dài block = tổng số phút còn lại (open minutes) của nhóm. Nếu sản phẩm trong nhóm nằm ở nhiều Gr.xxx thì vẫn chỉ quan tâm **tổng** phút của nhóm (lưu tổng cộng dồn, không tách từng Gr.xxx). Có thể click vào block để chỉnh deadline riêng (custom deadline) nếu khác deadline của Gr.xxx. Block gắn với ProductionGroup (Gr.xxx mà nhóm đó đã đến) để kiểm tra deadline.

---

Bước 6

Hệ thống tạo block sản xuất.

---

Bước 7

Quản lý kéo block vào line sản xuất.

- Khi kéo, **kéo toàn bộ block** (không kéo một nửa). Ngoại trừ: phần vượt deadline được tách ra để kéo riêng.
- Cho phép kéo block ngược lại phía mục sản phẩm chưa gán; hoặc khi xóa bên line thì nó trở về mục sản phẩm chưa gán.
- Khi xóa một hàng (line + shift) có block trên đó: hệ thống hiện **cảnh báo**, sau đó các block trở về danh sách chờ.