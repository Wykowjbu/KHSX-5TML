# Scheduling Block

Mỗi ProductGroup sẽ trở thành một block sản xuất.

Độ dài block = tổng số phút cần sản xuất (open minutes từ MES, gom theo ProductGroup).

Block có thể kéo thả vào các line sản xuất.

Hệ thống sẽ kiểm tra:

- công suất line (workers × minutes × efficiency)
- deadline của group (theo Gr.xxx mà ProductGroup đã đến)

Nếu không đủ công suất trên line/ca hiện tại thì **người dùng quan sát và tự kéo block sang line khác** (thao tác thủ công); hệ thống không tự động chuyển line.

## Quy tắc kéo thả

- **Kéo toàn bộ block** — không kéo một nửa. **Phần vượt deadline:** thiết kế sao cho khi user **click và kéo phần sau deadline** thì chỉ phần đó di chuyển, **không** kéo phần phía trước deadline đi theo (phần trước deadline giữ nguyên vị trí).
- **Thứ tự trong cell:** Theo thứ tự user đã kéo — block kéo trước nằm trước, block kéo sau nối tiếp. Auto-push tôn trọng thứ tự này.
- Block luôn **tự đẩy lên ngày sớm nhất** có capacity (auto-push). Re-render khi có thay đổi.
- Kéo block ngược về danh sách chờ: block trở lại Unassigned.
- Xóa hàng (line + shift) có block: **cảnh báo**, các block trên hàng đó trở về danh sách chờ.

## Cập nhật MES

- Block đã gán trên line giữ nguyên vị trí. Chỉ cập nhật số phút (tự co lại nếu giảm, tự mở rộng nếu tăng).