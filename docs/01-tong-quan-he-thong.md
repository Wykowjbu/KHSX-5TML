# Tổng Quan Hệ Thống

Hệ thống lập kế hoạch sản xuất giúp quản lý nhà máy sắp xếp sản xuất hiệu quả.

Hệ thống sử dụng dữ liệu từ:

- Marketing (file release sản xuất)
- MES (file số phút sản xuất còn lại)

Dựa trên các dữ liệu này hệ thống sẽ:

- Tính toán thời gian sản xuất còn lại
- Gom sản phẩm theo nhóm
- Tạo block sản xuất
- Cho phép kéo block vào line sản xuất

---

# Cấu trúc nhà máy

Nhà máy gồm nhiều **Production Line**

Mỗi line có:

- Shift A
- Shift B

Mỗi shift có:

- số lượng công nhân
- hiệu suất

Mỗi công nhân có số phút làm việc mỗi ca.