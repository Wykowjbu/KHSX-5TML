# Đặc Tả Import Excel

Hệ thống sử dụng 2 nguồn Excel.

---

# File Marketing Release

Chứa thông tin sản xuất.

Các cột:

Tên sản phẩm
Sales Order
Packing Size
Gr.xxx
Nhóm sản phẩm
Số phút / sản phẩm
Function

Ví dụ:

BM8R030110
Group: BM8R030
Minutes: 18

---
Cột A là tên sản phẩm
Ô E1,F1,G1,H1,I1,J1 lần lương là tên các Gr.xxx
Cột K bắt đau từ hàng 2 là tên nhóm sản phẩm
cột M là tên function

# File MES

Chứa số phút sản xuất còn lại.

Các cột:

Product
Open Minutes

Ví dụ:

BM8R093105

Open Minutes: 142.92

--
Cột D20 xuống là tên sản phẩm, I20 xuống là số phút còn lại