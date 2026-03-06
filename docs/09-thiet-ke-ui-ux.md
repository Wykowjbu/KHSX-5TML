# Thiết Kế UI

Giao diện chính là bảng lập kế hoạch sản xuất.

Bao gồm:

Production Lines ( mỗi ca là một hàng luôn xếp block sản phẩm vào ca A không đủ thì có thể kéo sang ca B hoặc ca của line khác)
Timeline theo ngày
Production Blocks

Block có thể kéo thả.

Chiều dài block tương ứng với số phút sản xuất.


Thiết kế theo kiểu data grid
Cột là ngày ( ngyaf đầu tiên là hôm nay)
Từng hàng là ca
Ví dụ:

Line1: Ca A
Line1: Ca B
Line2: Ca A
Line2: Ca B
...

Có chổ để xoá cái hàng đó, có nút thêm hàng,
double click vào hàng để mở popup để chỉnh sửa số người trong ca và mặc định bao nhiêu phút

Mỗi cell sẽ chứa số người trong ca và số phút ( double click vào cell để chỉnh sửa riêng cho ngày đó ca đó, chỉnh sô người, số phút làm , khi đc chỉnh thì nền của cell sẽ khác các cell khác)