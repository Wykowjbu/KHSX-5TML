# Mô Hình Nghiệp Vụ

## Function

Tên line sản xuất. Function lấy từ cột A của `module_list.xlsx`.

## BuildGroup

Nhóm sản xuất chính. BuildGroup lấy từ cột B của `module_list.xlsx`.

Quy tắc:

- Một BuildGroup chỉ thuộc một Function.
- Một BuildGroup có thể có nhiều FP.

## FP

FP là mã 7 ký tự đầu dùng để match sản phẩm từ planning/MES về BuildGroup.

Ví dụ `BM8R221112` có FP `BM8R221`. Nếu module list map FP này về BuildGroup `BM8R220`, block thuộc `BM8R220`.

## Production Group (Gr.xxx)

`Gr.xxx` là nhóm deadline. Block được tạo theo `BuildGroup + Gr.xxx`.

## Deadline

Deadline bắt buộc theo từng `Gr.xxx`.

## Block

Block đại diện cho tổng số phút cần làm của một `BuildGroup + Gr.xxx`. Block được auto schedule vào ngày deadline của `Gr.xxx` trên line Function.
