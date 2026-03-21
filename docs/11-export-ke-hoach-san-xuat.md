# 11. Export Kế Hoạch Sản Xuất Ra Excel

## Mục đích

Cho phép xuất file Excel kế hoạch sản xuất chi tiết cho từng Line, giúp nhân viên trên sàn biết chính xác:
- Hôm nay cần làm **sản phẩm gì**
- Mỗi sản phẩm cần làm **bao nhiêu cái**
- Thứ tự sản phẩm nào làm **trước/sau**

## Nguồn dữ liệu

| Nguồn | Dữ liệu lấy ra | File JSON |
|---|---|---|
| Marketing Excel | ProductId, GroupId, MinutesPerProduct | `products.json` |
| MES Excel | ProductId, OpenMinutes | `openMinutes.json` |
| Lưới kế hoạch (Grid) | Block nào ở Line nào, ngày nào, AllocatedMinutes | Runtime |

### Công thức tính số SP

```
Số sản phẩm = OpenMinutes(SP) ÷ MinutesPerProduct(SP)
```

> **Lưu ý:** Mỗi SP con trong cùng GroupId có `MinutesPerProduct` **khác nhau**, không dùng chung được.

## Chế độ tính phân bổ theo ngày

Người dùng chọn 1 trong 2 chế độ trước khi export:

### Chế độ 1: Tuần tự (Sequential)

Làm hết SP này → mới chuyển sang SP kia. Thứ tự do người dùng sắp xếp.

**Ví dụ:** Nhóm `BM8R030`, capacity 150 phút/ngày:

| SP | OpenMinutes | Min/SP | Tổng SP |
|---|---|---|---|
| BM8R030110 | 100' | 10'/sp | 10 sp |
| BM8R031110 | 200' | 20'/sp | 10 sp |

| | Ngày 1 (150') | Ngày 2 (150') |
|---|---|---|
| BM8R030110 | **10 sp** ✅ (hết 100') | — |
| BM8R031110 | **2 sp** (dùng 50' còn lại) | **8 sp** ✅ (hết 150') |
| **Tổng SP** | **12** | **8** |

### Chế độ 2: Tỷ lệ (Proportional)

Tất cả SP được chia đều theo % phút phân bổ trong ngày.

**Ví dụ:** Cùng dữ liệu trên:

| | Ngày 1 (150'/300' = 50%) | Ngày 2 (150'/300' = 50%) |
|---|---|---|
| BM8R030110 | 5 sp | 5 sp |
| BM8R031110 | 5 sp | 5 sp |
| **Tổng SP** | **10** | **10** |

## Popup sắp xếp thứ tự (áp dụng cho chế độ Tuần tự)

Khi bấm nút **"Export Kế Hoạch"**, popup hiện ra cho phép:

1. **Chọn chế độ tính:** Tuần tự hoặc Tỷ lệ
2. **Sắp xếp thứ tự SP trong từng Block (GroupId):**
   - Danh sách SP con hiển thị theo từng nhóm/block
   - Hỗ trợ kéo thả hoặc nút ↑↓ để đổi thứ tự
   - Thứ tự chỉ ảnh hưởng **trong nội bộ 1 block**, không ảnh hưởng thứ tự giữa các block trên line

> Nếu chọn chế độ **Tỷ lệ**, thứ tự chỉ ảnh hưởng cách hiển thị trên Excel (SP nào liệt kê trước).

## Cấu trúc file Excel đầu ra

### Mỗi Sheet = 1 Line

Tên sheet: `Line 1 - Ca A`, `Line 1 - Ca B`, `Line 2 - Ca A`...

### Bố cục bảng

| | Thứ 2 (10/3) | Thứ 3 (11/3) | ... | Thứ 7 (15/3) |
|---|---|---|---|---|
| **Nhóm** | BM8R030 | BM8R030 | ... | BM9A020 |
| **Phút phân bổ** | 150' | 150' | ... | 120' |
| **─ BM8R030110** (10'/sp) | 10 sp ✅ | — | ... | — |
| **─ BM8R031110** (20'/sp) | 2 sp | 8 sp ✅ | ... | — |
| **─ BM9A020305** (15'/sp) | — | — | ... | 8 sp |
| **Số người** | 5 | 5 | ... | 4 |
| **Tổng SP trong ngày** | **12** | **8** | ... | **8** |

### Phạm vi ngày

Export từ ngày hiện tại đến **cuối tuần** (Thứ 7), hoặc có thể mở rộng cho phép chọn khoảng ngày.

## Quy tắc xử lý

1. **1 ô ngày có 2 block khác nhau:** Hiển thị 2 nhóm riêng biệt, mỗi nhóm liệt kê SP con riêng
2. **SP có OpenMinutes = 0:** Bỏ qua, không hiển thị
3. **MinutesPerProduct chưa có:** Hiển thị gạch `—` thay vì số SP, kèm cảnh báo
4. **Ngày nghỉ (IsDayOff):** Hiển thị ô trống hoặc đánh dấu "NGHỈ"
5. **Làm tròn số SP:** Làm tròn lên (`Math.Ceiling`) vì không thể làm 0.5 sản phẩm
