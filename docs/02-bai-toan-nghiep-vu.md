# Bài Toán Nghiệp Vụ

Nhà máy cần lập lịch cho các BuildGroup theo từng `Gr.xxx`.

Các yêu cầu chính:

- Sản phẩm đầy đủ phải map về BuildGroup qua FP trong module list.
- Chỉ lấy dữ liệu `Sektor = U4`.
- Mỗi `BuildGroup + Gr.xxx` là một block riêng.
- Deadline bắt buộc theo `Gr.xxx`.
- BuildGroup được cấu hình ca A/B và số người từng ca.
- Block được đặt vào ngày deadline, nếu vượt capacity thì hiển thị đỏ để user xử lý.
