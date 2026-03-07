# Luồng Dữ Liệu

Bước 1

Quản lý import file Excel từ Marketing.

Hệ thống lưu:

- sản phẩm (cột A)
- nhóm sản phẩm (cột K)
- minutesPerProduct (cột L)
- tên function (cột M)
- **số lượng sản phẩm** theo từng Gr.xxx (cột E–J: giá trị dưới header mỗi Gr là **số lượng**)
- **tổng số lượng** cộng dồn tất cả Gr.xxx

**Mục đích:** Xác định sản phẩm thuộc nhóm nào, số lượng theo từng Gr.xxx và tổng, phục vụ tính toán và gán Gr.xxx mặc định cho ProductGroup (Gr.xxx lớn nhất).

**Khi import lại file Marketing (merge):**
- Gr.xxx **có trong file mới:** cập nhật số lượng theo Gr.xxx đó; đồng thời cập nhật lại tổng.
- Gr.xxx **không có trong file mới:** giữ nguyên số lượng cũ (vd: sản phẩm có Gr.284 và Gr.285, file mới chỉ có Gr.285 → Gr.284 giữ nguyên).
- Sản phẩm **không có trong file mới:** giữ nguyên dữ liệu cũ (không xóa).

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

Hệ thống gom dữ liệu theo ProductGroup để tạo block. Mỗi **ProductGroup** tương ứng **một block**; độ dài block = tổng **open minutes** của nhóm (từ MES). Block gắn với **ProductionGroup** (Gr.xxx của nhóm, xem mô hình nghiệp vụ) để kiểm tra deadline. User có thể click vào block để chỉnh deadline riêng (custom deadline).

**Sản phẩm có trong Marketing nhưng không có trong MES:** Vẫn tạo block cho nhóm đó (tổng open minutes có thể bằng 0). Hệ thống hiển thị block để user có thể thiết lập Gr.xxx (production group), deadline và xếp lịch khi có dữ liệu MES sau.

---

Bước 6

Hệ thống tạo block sản xuất.

---

Bước 7

Quản lý kéo block vào line sản xuất.

- **Kéo toàn bộ block** (không kéo một nửa). Riêng **phần vượt deadline** được tách ra: khi user click và kéo phần sau deadline thì chỉ phần đó di chuyển, không kéo phần phía trước deadline đi theo.
- Cho phép kéo block ngược về danh sách chưa gán; khi xóa khỏi line thì block trở về danh sách chờ.
- Khi xóa một hàng (line + shift) có block: hệ thống hiện **cảnh báo**, sau đó các block trên hàng đó trở về danh sách chờ.