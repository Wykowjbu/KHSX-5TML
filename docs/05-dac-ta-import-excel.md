# Đặc Tả Import Excel

Hệ thống sử dụng 2 nguồn Excel.

---

# File Marketing Release

Chứa thông tin sản xuất.

Các cột:

- **Cột A:** tên sản phẩm (productId)
- **Cột E–J, hàng 1:** header là tên các Gr.xxx (vd: Gr.284, Gr.285, …; nhà máy làm theo thứ tự và Gr tăng dần 289, 290, …)
- **Cột E–J, từ hàng 2:** **số lượng sản phẩm** theo từng Gr (không phải số phút). Tổng phút = số lượng × minutesPerProduct.
- **Cột K:** tên nhóm sản phẩm (ProductGroup)
- **Cột L:** minutesPerProduct (số phút/sản phẩm)
- **Cột M:** tên function

Ví dụ:

- BM8R030110, nhóm BM8R030, Gr.285: 5 cái, minutesPerProduct: 18 → tổng phút Gr.285 = 5 × 18 = 90 phút.

**Hệ thống lưu:** số lượng **theo từng Gr.xxx** và **tổng số lượng** cộng dồn tất cả Gr.

**Khi import lại file Marketing (merge):**
- **Cập nhật cả hai:** số lượng theo từng Gr.xxx (chỉ những Gr có trong file mới) và tổng.
- Gr.xxx **có trong file mới:** cập nhật số lượng theo Gr đó.
- Gr.xxx **không có trong file mới:** giữ nguyên (vd: sản phẩm có Gr.284 và Gr.285, file mới chỉ có Gr.285 → Gr.284 giữ nguyên).
- Sản phẩm **không có trong file mới:** giữ nguyên dữ liệu cũ.

---

# File MES

Chứa số phút sản xuất còn lại (open minutes).

- **Cột D** (từ hàng 20): tên sản phẩm
- **Cột I** (từ hàng 20): số phút còn lại (open minutes)

**Lưu ý:** Hàng 1–19 bỏ qua; chỉ đọc dữ liệu từ hàng 20 trở đi.

Ví dụ:

BM8R093105
Open Minutes: 142.92

**Đây là nguồn dữ liệu chính** để xác định tổng số phút cần sản xuất cho mỗi ProductGroup khi lập kế hoạch.
