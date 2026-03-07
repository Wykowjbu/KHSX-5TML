# Logic Lập Kế Hoạch

Hệ thống lập kế hoạch dựa trên ProductGroup.

Một ProductGroup có thể chứa nhiều sản phẩm.

Hệ thống sẽ cộng tổng số phút open minutes (từ MES) của tất cả sản phẩm trong nhóm.

Ví dụ:

Product A = 50 phút (open minutes từ MES)
Product B = 70 phút (open minutes từ MES)

Tổng nhóm = 120 phút

Hệ thống tạo một block sản xuất với độ dài 120 phút.

**Lưu ý:** Open minutes từ MES là nguồn dữ liệu chính để tính độ dài block. Dữ liệu từ file Marketing (số lượng × minutesPerProduct) chỉ dùng để tham khảo tổng nhu cầu ban đầu.

# Tính Toán Công Suất

Công suất sản xuất phụ thuộc vào:

- số công nhân
- số phút làm việc
- hiệu suất (theo từng line + shift)

Công thức:

Capacity = workers × minutes × efficiency

Ví dụ:

Workers = 5
Minutes = 480
Efficiency = 115%

Capacity = 5 × 480 × 1.15

= 2760 phút

# Quy Tắc Kéo Thả Block

- Khi kéo block sản phẩm vào cell, nếu cell đó đã có block thì block mới được thêm vào sau block cũ.
- **Kéo toàn bộ block** — không có việc kéo một nửa. Ngoại trừ: phần vượt deadline được tách ra để kéo riêng.
- **Block luôn được đẩy lên ngày sớm nhất** (auto-push): ví dụ hôm nay là 29/5 thì kéo vào ô 30/5 mà 29/5 đang trống thì nó tự chuyển qua 29/5.
- Nếu 29/5 tổng capa là 1000 mà mới dùng hết 500 thì sẽ có 500 của block đó lấp vào.
- **Luôn re-render**: mỗi khi có thay đổi (thêm block, xóa block, cập nhật số phút), hệ thống tự đẩy lại block lên ngày sớm nhất có thể.

# Cập Nhật Khi Import MES Mới

- **Số phút giảm** (ví dụ 1000 → 700): hệ thống tự **co lại** block, giữ nguyên vị trí trên line. Các split block trên schedule co lại tương ứng.
- **Số phút tăng** (ví dụ 1000 → 1300 do thêm Gr mới): hệ thống **cập nhật số phút mới** cho block. Phần dư (300 phút) được thêm vào totalMinutes; block giữ vị trí cũ, phần dư tự lấp thêm vào schedule.
- **Nguyên tắc:** tất cả sản phẩm đã lưu (theo productId) chỉ cập nhật số liệu mới, không quan tâm số cũ.