# Logic Lập Kế Hoạch

Hệ thống lập kế hoạch dựa trên ProductGroup.

Một ProductGroup có thể chứa nhiều sản phẩm.

Hệ thống sẽ cộng tổng số phút open minutes của tất cả sản phẩm trong nhóm.

Ví dụ:

Product A = 50 phút
Product B = 70 phút

Tổng nhóm = 120 phút

Hệ thống tạo một block sản xuất với độ dài 120 phút.

# Tính Toán Công Suất

Công suất sản xuất phụ thuộc vào:

- số công nhân
- số phút làm việc
- hiệu suất

Công thức:

Capacity = workers × minutes × efficiency

Ví dụ:

Workers = 5
Minutes = 480
Efficiency = 115%

Capacity = 5 × 480 × 1.15

= 2760 phút