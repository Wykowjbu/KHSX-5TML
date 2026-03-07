# Thiết Kế UI

Giao diện chính là bảng lập kế hoạch sản xuất.

Bao gồm:

Production Lines (mỗi ca là một hàng; xếp block sản phẩm vào ca A, không đủ thì có thể kéo sang ca B hoặc ca của line khác)
Timeline theo ngày
Production Blocks

Block có thể kéo thả.

Chiều dài block tương ứng với số phút sản xuất.


Thiết kế theo kiểu data grid
Cột là ngày (ngày đầu tiên là hôm nay)
Từng hàng là ca
Ví dụ:

Line1: Ca A
Line1: Ca B
Line2: Ca A
Line2: Ca B
...

Có chỗ để xóa cái hàng đó, có nút thêm hàng.
- **Khi xóa hàng có block:** hiện cảnh báo, các block trở về danh sách chờ.

Double click vào hàng để mở popup để chỉnh sửa số người trong ca và mặc định bao nhiêu phút.

Mỗi cell sẽ chứa số người trong ca và số phút (double click vào cell để chỉnh sửa riêng cho ngày đó ca đó, chỉnh số người, số phút làm; khi được chỉnh thì nền của cell sẽ khác các cell khác).
Không cần quan tâm tổng capacity theo ngày; chỉ quan tâm từng ca (ca A ngày đó hoặc ca B ngày đó). Mỗi ca được coi như một hàng riêng biệt.

## Ngày nghỉ / ngày lễ

- **Chủ nhật mặc định nghỉ** — cell hiển thị "Nghỉ", block bỏ qua ngày này.
- **Cho phép click vào bất kỳ cell nào** để toggle nghỉ/làm việc:
  - Thứ 2 trúng lễ → click chọn là ngày nghỉ → hệ thống sẽ bỏ qua ngày đó.
  - Chủ nhật tăng ca → click chọn là ngày làm việc → hệ thống sẽ tính capacity cho ngày đó.

**Tên nhóm sản phẩm:** Giao diện cần có phần cho phép người dùng **đặt tên** (hoặc sửa tên) cho từng nhóm sản phẩm (ProductGroup).