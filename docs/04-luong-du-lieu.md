# Luồng Dữ Liệu

## 1. Module List

Import `module_list.xlsx` để tạo `moduleMappings.json` và cấu hình BuildGroup mặc định.

## 2. Planning

Import planning sheet `Serienplaning`, lọc `Sektor = U4`, map sản phẩm qua FP và gom planned minutes theo `BuildGroup + Gr.xxx`.

## 3. Deadline

Hệ thống lấy toàn bộ `Gr.xxx` từ planning/MES và bắt buộc user cấu hình deadline.

## 4. BuildGroup/Ca

User chọn BuildGroup làm ca A, B hoặc cả A+B, đồng thời nhập số người từng ca.

## 5. MES/OpenMin

Import MES/OpenMin, lọc `Sektor = U4`, gom open minutes theo `BuildGroup + Gr.xxx`.

## 6. Tạo Block Và Auto Schedule

Hệ thống tạo block cuối cùng:

- Ưu tiên phút từ MES.
- Fallback sang planning nếu MES thiếu.
- Vẫn tạo block MES nếu planning không có và cảnh báo.

Sau đó auto schedule block vào line Function tại ngày deadline của `Gr.xxx`.
