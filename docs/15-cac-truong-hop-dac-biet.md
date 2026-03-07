# Các Trường Hợp Đặc Biệt

- **MES thêm group mới:** Có thể có Gr.xxx mới trong file MES ngày hôm sau. Sau khi import file MES mới, hệ thống cập nhật dữ liệu; block đã nằm trên line chỉ cập nhật số phút còn lại, không cần kéo thả lại.

- **Open minutes thay đổi:** Mỗi ngày import file MES mới để cập nhật số phút còn lại (có thể tăng hoặc giảm). Block trên bảng giữ nguyên vị trí, chỉ cập nhật số.

- **Số công nhân / số phút / hiệu suất thay đổi:** Quản lý có thể chỉnh trong cấu hình (shifts.json hoặc cellCapacity.json cho từng ô). Công suất ô được tính lại theo workers × minutes × efficiency.

- **Tăng ca:** Có thể thêm hàng (line + ca) mới hoặc điều chỉnh số phút làm việc trong ca.

- **Quá deadline:** Hệ thống hiển thị **cảnh báo**; quản lý xử lý theo quy trình (ví dụ ưu tiên đẩy block đó, hoặc điều chỉnh kế hoạch).

- **Ngày đầu tiên trên bảng:** Luôn là **ngày hôm nay**; không cần quan tâm ngày quá khứ.
