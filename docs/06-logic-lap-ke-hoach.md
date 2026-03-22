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

- Khi kéo block vào cell đã có block: block mới được **thêm vào sau** block cũ (nối tiếp).
- **Thứ tự trong cell:** Theo thứ tự user đã kéo — block kéo trước nằm trước, block kéo sau nối tiếp vào. Khi re-render/auto-push, hệ thống tôn trọng thứ tự này.
- **Kéo toàn bộ block** — không kéo một nửa. Riêng **phần vượt deadline** tách ra: kéo phần sau deadline thì chỉ phần đó di chuyển, không kéo phần trước deadline theo.
- **Block luôn được đẩy lên ngày sớm nhất** (auto-push): ví dụ kéo vào ô 30/5 mà 29/5 còn trống thì block tự chuyển sang 29/5; trong mỗi cell, block lấp đầy capacity theo thứ tự đã kéo.
- **Re-render:** Mỗi khi có thay đổi (thêm/xóa block, cập nhật số phút), hệ thống tự đẩy block lên ngày sớm nhất có thể, giữ thứ tự block trong từng cell.

# Cập Nhật Khi Import MES Mới

- **Số phút giảm** (ví dụ 1000 → 700): hệ thống sẽ **xoá sạch** toàn bộ các phân mảnh (split) hiện tại của block đó trên mọi line. Sau đó gom tổng 700 phút thành 1 block nguyên bản và ném thả toàn bộ vào ngày đầu tiên ở **Line đầu tiên** mà block từng xuất hiện. Nếu tổng khối lượng mới tràn qua deadline, hệ thống sẽ báo viền đỏ cảnh báo.
- **Số phút tăng** (ví dụ 1000 → 1300 do thêm Gr mới): hệ thống **cập nhật số phút mới** cho các splits cũ.
- **Nguyên tắc:** tất cả sản phẩm đã lưu (theo productId) chỉ cập nhật số liệu mới, không quan tâm số cũ.

# Cập Nhật Khi Thay Đổi Cấu Hình (Auto-Repack)
- Cơ chế **Repack Blocks** được kích hoạt tự động mỗi khi có thay đổi sức chứa của bất kì Line nào.
- Nếu bạn đổi số công nhân, đổi số phút, mức hiệu suất, hoặc set "Ngày nghỉ hàng loạt": Hệ thống tự động quét Line, tháo toàn bộ block ra khỏi Line và thả nối đuôi nhau lại từ đầu. Giúp lịch linh hoạt co/trượt block ra những ngày làm việc thực tế cực chuẩn.