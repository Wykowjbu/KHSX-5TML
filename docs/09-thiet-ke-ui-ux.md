# Thiết Kế UI/UX

Toolbar chính đi theo luồng:

1. Import Module List.
2. Import Planning.
3. Thiết Lập Deadline.
4. Cấu Hình BuildGroup/Ca.
5. Import MES -> Auto Schedule.

## Dialog Chính

- Missing FP Mapping: nhập BuildGroup và Function cho FP chưa có trong module list.
- Deadline: nhập bắt buộc deadline cho từng `Gr.xxx`.
- BuildGroup/Ca: chọn ca A/B và số người cho từng ca.

## Grid

Line hiển thị theo Function. Cell vượt capacity có viền đỏ. Nếu cell có nhiều block, thao tác kéo sẽ di chuyển nguyên cụm block trong cell.
