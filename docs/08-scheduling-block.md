# Scheduling Block

Mỗi ProductGroup sẽ trở thành một block sản xuất.

Độ dài block = tổng số phút cần sản xuất (open minutes từ MES, gom theo ProductGroup).

Block có thể kéo thả vào các line sản xuất.

Hệ thống sẽ kiểm tra:

- công suất line (workers × minutes × efficiency)
- deadline của group (theo Gr.xxx mà ProductGroup đã đến)

Nếu không đủ công suất trên line/ca hiện tại thì **người dùng quan sát và tự kéo block sang line khác** (thao tác thủ công); hệ thống không tự động chuyển line.

## Quy tắc kéo thả

- **Kéo toàn bộ block** — không hỗ trợ kéo một nửa. Ngoại trừ: phần vượt deadline được tách ra để kéo riêng.
- Block luôn **tự đẩy lên ngày sớm nhất** có capacity còn trống (auto-push). Mỗi khi re-render, hệ thống tự cập nhật vị trí block.
- Kéo block ngược về danh sách chờ: block trở lại trạng thái Unassigned.
- Khi xóa một hàng (line + shift) có block trên đó: **hiện cảnh báo**, các block trở về danh sách chờ.

## Cập nhật MES

- Block đã gán trên line giữ nguyên vị trí. Chỉ cập nhật số phút (tự co lại nếu giảm, tự mở rộng nếu tăng).