# Quy Trình Sử Dụng

**Bước 1.** Import file Marketing (thường chỉ import 1 lần khi nhận release từ Marketing; khi có thêm Gr.xxx mới thì import lại). Hệ thống đọc: tên sản phẩm, nhóm sản phẩm, Gr.xxx, số lượng × minutesPerProduct = tổng phút. Dữ liệu được merge (không overwrite): Gr.xxx mới thêm vào, sản phẩm đã có chỉ cập nhật số liệu mới.

**Bước 2.** Thiết lập deadline cho từng Production Group (Gr.xxx) và chọn current group để hệ thống biết lấy deadline nào làm mốc.

**Bước 3.** Import file MES.

**Bước 4.** Hệ thống tự động gán open minutes (match sản phẩm MES ↔ Marketing, gom theo ProductGroup). Nếu có sản phẩm không match thì cảnh báo và cho nhập tay.

**Bước 5.** Hệ thống tạo block (mỗi ProductGroup một block; độ dài = tổng open minutes của nhóm từ MES).

**Bước 6.** Kéo block vào line sản xuất (kéo thả toàn bộ block vào các ô line + ca + ngày). Block luôn tự đẩy lên ngày sớm nhất có capacity trống.

---

**Cập nhật hàng ngày (sau khi có file MES mới):**

- Cuối ngày MES xuất file Excel mới; đầu ngày sáng hôm sau quản lý import file đó để cập nhật số liệu (số phút còn lại, có thể thêm Gr mới).
- Block đã được kéo vào line **giữ nguyên vị trí**; hệ thống chỉ **cập nhật số phút**:
  - **Số phút giảm:** block tự co lại, các split block trên schedule co tương ứng.
  - **Số phút tăng:** block mở rộng, phần dư tự thêm vào schedule.
  - Tất cả sản phẩm đã lưu (theo productId) chỉ cập nhật số liệu mới, không quan tâm số cũ.
- Hệ thống tự **re-render** và đẩy block lên ngày sớm nhất.
- Nếu quá deadline: hiển thị **cảnh báo** cho người dùng.
