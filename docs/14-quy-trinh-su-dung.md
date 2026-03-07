# Quy Trình Sử Dụng

**Bước 1.** Import file Marketing (thường chỉ import 1 lần khi nhận release từ Marketing; khi có thêm Gr.xxx mới thì import lại nếu cần).

**Bước 2.** Thiết lập deadline cho từng Production Group (Gr.xxx) và chọn current group để hệ thống biết lấy deadline nào làm mốc.

**Bước 3.** Import file MES.

**Bước 4.** Hệ thống tự động gán open minutes (match sản phẩm MES ↔ Marketing, gom theo ProductGroup). Nếu có sản phẩm không match thì cảnh báo và cho nhập tay.

**Bước 5.** Hệ thống tạo block (mỗi ProductGroup một block; độ dài = tổng số phút còn lại).

**Bước 6.** Kéo block vào line sản xuất (kéo thả vào các ô line + ca + ngày).

---

**Cập nhật hàng ngày (sau khi có file MES mới):**

- Cuối ngày MES xuất file Excel mới; đầu ngày sáng hôm sau quản lý import file đó để cập nhật số liệu (số phút còn lại, có thể thêm Gr mới).
- Block đã được kéo vào line **không cần kéo thả lại**; hệ thống chỉ **cập nhật số** (ví dụ block còn 1000 phút, đã làm 500, thêm Gr +200 → sáng hôm sau số phút còn lại 700; vị trí block trên bảng giữ nguyên).
- Nếu quá deadline: thực hiện theo quy trình xử lý trễ deadline và hiển thị **cảnh báo** cho người dùng.
