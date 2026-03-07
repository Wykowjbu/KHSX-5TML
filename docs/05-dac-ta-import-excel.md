# Đặc Tả Import Excel

Hệ thống sử dụng 2 nguồn Excel.

---

# File Marketing Release

Chứa thông tin sản xuất.

Các cột:

- Cột A: tên sản phẩm
- Cột E1, F1, G1, H1, I1, J1: header lần lượt là tên các Gr.xxx
- Giá trị cột E–J (từ hàng 2): **số lượng sản phẩm** (KHÔNG phải số phút). Cần nhân với `minutesPerProduct` để tính tổng phút.
- Cột K (từ hàng 2): tên nhóm sản phẩm (ProductGroup)
- Cột M: tên function

Ví dụ:

BM8R030110
Group: BM8R030
Quantity tại Gr.285: 5
minutesPerProduct: 18
→ Tổng phút cho Gr.285 = 5 × 18 = 90 phút

**Hệ thống lưu tổng cộng dồn số lượng tất cả Gr.xxx** (không cần tách từng Gr.xxx).

**Khi import lại file Marketing:**
- Gr.xxx mới sẽ được thêm vào.
- Sản phẩm đã tồn tại: cập nhật số liệu mới (merge), không xóa.
- Sản phẩm không có trong file mới: giữ nguyên dữ liệu cũ.

---

# File MES

Chứa số phút sản xuất còn lại (open minutes).

Cột D (từ hàng 20): tên sản phẩm
Cột I (từ hàng 20): số phút còn lại (open minutes)

Ví dụ:

BM8R093105
Open Minutes: 142.92

**Đây là nguồn dữ liệu chính** để xác định tổng số phút cần sản xuất cho mỗi ProductGroup khi lập kế hoạch.
