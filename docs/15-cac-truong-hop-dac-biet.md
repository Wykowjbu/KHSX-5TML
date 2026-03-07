# Các Trường Hợp Đặc Biệt

- **MES thêm group mới:** Có thể có Gr.xxx mới trong file MES ngày hôm sau. Sau khi import file MES mới, hệ thống cập nhật dữ liệu; block đã nằm trên line chỉ cập nhật số phút còn lại, không cần kéo thả lại.

- **Open minutes thay đổi:** Mỗi ngày import file MES mới để cập nhật số phút còn lại.
  - **Giảm:** block tự co lại, giữ nguyên vị trí trên line. Các split block co tương ứng.
  - **Tăng:** block mở rộng, phần dư tự thêm vào schedule.
  - Tất cả sản phẩm đã lưu (theo productId) chỉ cập nhật số liệu mới, không quan tâm số cũ.

- **Số công nhân / số phút / hiệu suất thay đổi:** Quản lý có thể chỉnh trong cấu hình (shifts.json hoặc cellCapacity.json cho từng ô). Công suất ô được tính lại theo workers × minutes × efficiency (efficiency theo từng line + shift).

- **Tăng ca:** Có thể thêm hàng (line + ca) mới hoặc điều chỉnh số phút làm việc trong ca.

- **Quá deadline:** Hệ thống hiển thị **cảnh báo**; phần vượt deadline được tách ra để user kéo riêng sang line khác.

- **Ngày đầu tiên trên bảng:** Luôn là **ngày hôm nay**; không cần quan tâm ngày quá khứ.

- **Xóa hàng (line + shift):** Hệ thống hiện **cảnh báo**; tất cả block trên hàng đó trở về danh sách chờ.

- **Import lại file Marketing:** Dữ liệu merge (không overwrite). Gr.xxx mới được thêm; sản phẩm đã có chỉ cập nhật số liệu (ví dụ Gr.284 từ 500 → 450). Sản phẩm không có trong file mới giữ nguyên.

- **Ngày nghỉ / ngày lễ:** Chủ nhật mặc định nghỉ. Cho phép click toggle bất kỳ cell nào thành ngày nghỉ (ví dụ thứ 2 trúng lễ) hoặc ngày làm việc (ví dụ Chủ nhật tăng ca). Hệ thống bỏ qua cell ngày nghỉ khi tính toán schedule.

- **Kéo block:** Luôn kéo toàn bộ block, không kéo một nửa. Ngoại trừ phần vượt deadline được tách riêng.

- **Auto-push (đẩy block lên):** Hệ thống luôn re-render và tự đẩy block lên ngày sớm nhất có capacity trống. Tôn trọng thứ tự các block đã có trong cell.
