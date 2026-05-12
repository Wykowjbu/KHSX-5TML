# Quy Trình Sử Dụng

1. Import Module List.
2. Import Planning.
3. Thiết lập deadline cho tất cả `Gr.xxx`.
4. Cấu hình BuildGroup/Ca.
5. Import MES/OpenMin để auto schedule.
6. Kiểm tra cell viền đỏ và điều chỉnh người/phút/ca nếu cần.
7. Xuất kế hoạch.

## Lưu Ý

- Module List phải import trước planning và MES.
- Deadline của mọi `Gr.xxx` là bắt buộc.
- Nếu có FP thiếu mapping, nhập tay trong popup; mapping được lưu lại dùng lâu dài.
- Auto schedule đặt nguyên block vào ngày deadline, không tự dàn đều theo capacity.
