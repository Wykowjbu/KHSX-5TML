# Các Trường Hợp Đặc Biệt

- **Sản phẩm có trong Marketing nhưng không có trong MES:** Vẫn tạo block cho nhóm (tổng open minutes có thể bằng 0). Hiển thị để user thiết lập production group (Gr.xxx), deadline và xếp lịch khi có dữ liệu MES sau.

- **MES thêm group mới:** Có thể có Gr.xxx mới trong file MES ngày hôm sau. Sau khi import file MES mới, hệ thống cập nhật dữ liệu; block đã nằm trên line chỉ cập nhật số phút còn lại, không cần kéo thả lại.

- **Open minutes thay đổi:** Mỗi ngày import file MES mới để cập nhật số phút còn lại.
  - **Giảm:** block tự co lại, giữ nguyên vị trí trên line. Các split block co tương ứng.
  - **Tăng:** block mở rộng, phần dư tự thêm vào schedule.
  - Tất cả sản phẩm đã lưu (theo productId) chỉ cập nhật số liệu mới, không quan tâm số cũ.

- **Số công nhân / số phút / hiệu suất thay đổi:** Quản lý có thể chỉnh trong cấu hình (shifts.json hoặc cellCapacity.json cho từng ô). Công suất ô được tính lại theo workers × minutes × efficiency (efficiency theo từng line + shift).

- **Tăng ca:** Double-click vào **cell** (line + shift + ngày) để tăng số phút làm việc cho đúng ngày đó (vd mặc định 480 phút → 600 phút). Công suất ô tính lại theo workers × minutes × efficiency.

- **Quá deadline:** Hệ thống hiển thị **cảnh báo**. Phần vượt deadline được tách ra: khi user click và kéo phần sau deadline thì chỉ phần đó di chuyển, không kéo phần trước deadline theo.

- **Ngày đầu tiên trên bảng:** Luôn là **ngày hôm nay**; không cần quan tâm ngày quá khứ.

- **Xóa hàng (line + shift):** Hệ thống hiện **cảnh báo**; tất cả block trên hàng đó trở về danh sách chờ.

- **Import lại file Marketing:** Merge: cập nhật số lượng theo từng Gr.xxx có trong file mới; Gr không có trong file mới giữ nguyên (vd sản phẩm có Gr.284 và Gr.285, file mới chỉ có Gr.285 → Gr.284 giữ nguyên). Cập nhật cả tổng. Sản phẩm không có trong file mới giữ nguyên.

- **Ngày nghỉ / ngày lễ:** Chủ nhật mặc định nghỉ. Cho phép click toggle bất kỳ cell nào thành ngày nghỉ (ví dụ thứ 2 trúng lễ) hoặc ngày làm việc (ví dụ Chủ nhật tăng ca). Hệ thống bỏ qua cell ngày nghỉ khi tính toán schedule.

- **Kéo block:** Luôn kéo toàn bộ block, không kéo một nửa. Ngoại trừ phần vượt deadline được tách riêng.

- **Auto-push (đẩy block lên):** Hệ thống re-render và đẩy block lên ngày sớm nhất có capacity. Thứ tự block trong cell theo thứ tự user đã kéo (block kéo trước nằm trước, block sau nối tiếp).
