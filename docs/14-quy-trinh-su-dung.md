# Quy Trình Sử Dụng

**Bước 1.** Import file Marketing. Hệ thống đọc: sản phẩm, nhóm (cột K), minutesPerProduct (cột L), số lượng theo từng Gr.xxx (cột E–J). Merge: cập nhật số lượng theo từng Gr có trong file mới; Gr không có trong file mới giữ nguyên; cập nhật tổng.

**Bước 2.** Thiết lập deadline cho từng Production Group (Gr.xxx). Gán Gr.xxx cho từng ProductGroup (thủ công hoặc dùng mặc định Gr.xxx lớn nhất của nhóm) để hệ thống biết deadline nào áp dụng cho block.

**Bước 3.** Import file MES.

**Bước 4.** Hệ thống tự động gán open minutes (match sản phẩm MES ↔ Marketing, gom theo ProductGroup). Nếu có sản phẩm không match thì cảnh báo và cho nhập tay.

**Bước 5.** Hệ thống tạo block (mỗi ProductGroup một block; độ dài = tổng open minutes của nhóm từ MES).

**Bước 6.** Kéo block vào line sản xuất (kéo toàn bộ block vào các ô line + ca + ngày). Block tự đẩy lên ngày sớm nhất có capacity. Thứ tự block trong mỗi cell theo thứ tự user đã kéo. Phần vượt deadline có thể kéo riêng (chỉ phần đó di chuyển).

---

**Cập nhật hàng ngày (sau khi có file MES mới):**

- Cuối ngày MES xuất file Excel mới; đầu ngày sáng hôm sau quản lý import file đó để cập nhật số liệu (số phút còn lại, có thể thêm Gr mới).
- Block đã được kéo vào line **giữ nguyên vị trí**; hệ thống chỉ **cập nhật số phút**:
  - **Số phút giảm:** block tự co lại, các split block trên schedule co tương ứng.
  - **Số phút tăng:** block mở rộng, phần dư tự thêm vào schedule.
  - Tất cả sản phẩm đã lưu (theo productId) chỉ cập nhật số liệu mới, không quan tâm số cũ.
- Hệ thống tự **re-render** và đẩy block lên ngày sớm nhất.
- Nếu quá deadline: hiển thị **cảnh báo** cho người dùng.
