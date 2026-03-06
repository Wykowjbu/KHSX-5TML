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



Khi kéo block sản phẩm vào cell, nếu cell đó đã có block sản phẩm, thì block sản phẩm mới sẽ được thêm vào sau block sản phẩm cũ. Block sản phẩm lun đc đẩy lên để tối ưu ví dụ hôm nay là 29/5 thì kéo vào ô 30/5 mà 29/5 đang trống thì nó tự chuyển qua 29/5 để tối ưu.

Hoặc 29/5 tổng capa là 1000 mà mới dùng hết 500 thì sẽ có 500 của block đó lấp vào