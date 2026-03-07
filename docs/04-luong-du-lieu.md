# Luồng Dữ Liệu

Bước 1

Quản lý import file Excel từ Marketing.

Hệ thống lưu:

- sản phẩm (cột A)
- nhóm sản phẩm (cột K)
- tên function (cột M)
- số phút cần sản xuất theo từng Gr.xxx (các cột E–J: giá trị dưới header mỗi Gr là số phút cần sản xuất của sản phẩm đó cho Gr đó)

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

Hệ thống gom dữ liệu theo ProductGroup để tạo block. Mỗi **ProductGroup** tương ứng **một block**; tổng độ dài block = tổng số phút còn lại (open minutes) của nhóm. Nếu sản phẩm trong nhóm nằm ở nhiều Gr.xxx thì vẫn chỉ quan tâm **tổng** phút của nhóm. Có thể click vào block để chỉnh deadline riêng (custom deadline) nếu khác deadline của Gr.xxx. Block gắn với ProductionGroup (Gr.xxx) để kiểm tra deadline (dựa vào current group).

---

Bước 6

Hệ thống tạo block sản xuất.

---

Bước 7

Quản lý kéo block vào line sản xuất.

Cho phép kéo block ngược lại phía mục sản phẩm chưa gán. hoặc khi xoá bên line thì nó có bên phía mục sản phẩm chưa gán