# Mô Hình Nghiệp Vụ

Các thực thể chính trong hệ thống:

Product
ProductGroup
ProductionGroup (Gr.xxx)
ProductionLine
Shift
Worker
ProductionBlock
Deadline
OpenMinutes

---

# Product

Một sản phẩm cụ thể.

Ví dụ:

BM8R030110

---

# ProductGroup

Mỗi sản phẩm thuộc một nhóm sản phẩm.

Ví dụ:

BM8R030

Việc lập kế hoạch chỉ dựa trên ProductGroup.

---

# ProductionGroup

Ví dụ:

Gr.284
Gr.285
Gr.286

Đây là các batch sản xuất được phát hành từ Marketing.

Mỗi group có deadline riêng.

Chú ý: deadline được thiết lập cho **từng Gr.xxx (ProductionGroup)**. Khi kiểm tra deadline cho block, hệ thống dùng Gr.xxx gắn với **ProductGroup** của block đó (xem dưới).

---

## ProductionGroup (Gr.xxx) gắn với từng ProductGroup

Mỗi **ProductGroup** cần biết đang ở **Gr.xxx nào** để lấy đúng deadline. Cách xác định:

- **User chọn thủ công:** Trong giao diện, user có thể chọn/sửa Gr.xxx áp dụng cho từng nhóm sản phẩm (vd: BM8R030 → Gr.285, BM8R097 → Gr.284).
- **Mặc định (tự động):** Nếu user chưa chọn, hệ thống dùng **Gr.xxx lớn nhất** mà nhóm đó có trong dữ liệu Marketing (sản phẩm trong nhóm có số lượng ở Gr nào thì coi nhóm “đã đến” Gr lớn nhất trong số đó).

Ví dụ:
- Nhóm BM8R030 có sản phẩm với số lượng ở Gr.284 và Gr.285 → mặc định dùng Gr.285 làm mốc deadline.
- Nhóm BM8R097 chỉ có ở Gr.284 → mặc định dùng Gr.284.

Giá trị này được lưu **theo từng ProductGroup** (vd trong `productGroups.json`, trường `productionGroup`), không dùng một giá trị chung cho toàn hệ thống.

---

## Quy tắc kiểm tra deadline

- **Mặc định:** Với mỗi block, deadline = deadline của **Gr.xxx** gắn với ProductGroup của block (theo `productionGroup` của nhóm đó).
- **Tùy chỉnh theo block:** User có thể bấm vào **từng block** để đặt deadline riêng (customDeadline), ghi đè deadline mặc định của Gr.xxx.