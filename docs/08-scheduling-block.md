# Scheduling Block

Mỗi ProductGroup sẽ trở thành một block sản xuất.

Độ dài block = tổng số phút cần sản xuất.

Block có thể kéo thả vào các line sản xuất.

Hệ thống sẽ kiểm tra:

- công suất line
- deadline của group

Nếu không đủ công suất trên line/ca hiện tại thì **người dùng quan sát và tự kéo block sang line khác** (thao tác thủ công); hệ thống không tự động chuyển line.