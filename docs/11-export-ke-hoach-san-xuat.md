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

## Chế độ phân bổ: Tuần tự (Sequential)

Hệ thống chỉ hỗ trợ **một chế độ duy nhất: Tuần tự** — làm hết SP này → mới chuyển sang SP kia. Thứ tự do người dùng sắp xếp.

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

## Cơ chế sắp xếp thứ tự

Chia thành **2 tầng**, xử lý ở 2 thời điểm khác nhau:

### Tầng 1 — Setting: Thứ tự SP trong cùng Block (cài đặt trước)

Người dùng cài đặt **trước** khi export, thứ tự được **lưu lại** để dùng lâu dài.

- Nằm ở trang cài đặt hoặc panel riêng
- Hiển thị danh sách SP con theo từng block (GroupId)
- Hỗ trợ kéo thả hoặc nút ↑↓ để đổi thứ tự
- **Thứ tự mặc định:** theo ProductId tăng dần (ví dụ: BM8R0110 → BM8R1110)

```
⚙️ Cài đặt thứ tự SP

Block: BM8R030
  ↕ BM8R030110   (10'/sp)    ← làm trước
  ↕ BM8R031110   (20'/sp)    ← làm sau
```

> Thứ tự SP **ít thay đổi**, nên phù hợp để lưu sẵn trong setting.

---

### Tầng 2 — Popup lúc Export: Thứ tự Block trên Line

Khi nhấn **"Export Kế Hoạch"**, hệ thống kiểm tra từng Line:

#### Line có đúng 1 Block → Export liền

Không cần popup, vì thứ tự SP đã được set sẵn ở tầng 1.

#### Line có nhiều hơn 1 Block → Popup chọn thứ tự block

Popup hiển thị **tất cả line cùng lúc** (mỗi line 1 nhóm), cho phép sắp xếp block nào làm trước:

```
[ Sắp xếp thứ tự block ]

Line 1:
  ↕ Block: BM8R030     ← làm trước
  ↕ Block: BM9A020     ← làm sau

Line 3:
  ↕ Block: BM5C010     ← làm trước
  ↕ Block: BM7D020     ← làm sau

         [Xác nhận & Export]
```

- Hệ thống xử lý tuần tự: làm hết SP của block 1 trước, rồi mới chuyển qua block 2
- Thứ tự SP trong mỗi block lấy từ setting (tầng 1)

> Thứ tự chỉ ảnh hưởng trong phạm vi **1 line**. Các line độc lập nhau.

## Xử lý SP không thuộc block nào

Nếu phát hiện SP có trên grid nhưng **không thuộc block nào** (không tìm thấy trong `products.json`):

1. Hệ thống **dừng lại** và hiển thị danh sách SP chưa có block
2. Cho phép người dùng:
   - **Chọn block hiện có** để gán SP vào
   - **Tạo block mới** và gán SP vào
3. Sau khi gán xong, quay lại popup sắp xếp thứ tự để người dùng sắp xếp
4. Nhấn **"Xác nhận & Export"** để tiến hành export

> Không bỏ qua SP, luôn yêu cầu người dùng xử lý trước khi export.

## Lưu file Excel

| Hạng mục | Chi tiết |
|---|---|
| **Hộp thoại lưu** | Hệ thống mở **Save As dialog** để người dùng chọn thư mục lưu |
| **Tên file mặc định** | `KHSX_DD-MM-YYYY.xlsx` (ngày export) — Ví dụ: `KHSX_24-03-2026.xlsx` |
| **File trùng tên** | Do dùng Save As dialog, người dùng tự chọn ghi đè hoặc đổi tên |

## Cấu trúc file Excel đầu ra

File Excel gồm **1 sheet tổng quan** + **nhiều sheet chi tiết** (mỗi line 1 sheet).

### Sheet "Tổng Quan" (sheet đầu tiên)

Hiển thị tổng hợp tất cả line, có link trỏ đến sheet chi tiết:

| Line | Block | Tổng SP | Link |
|---|---|---|---|
| Line 1 | BM8R030, BM9A020 | 28 sp | 👉 [Chi tiết](#Line1) |
| Line 2 | BM5C010 | 10 sp | 👉 [Chi tiết](#Line2) |

> Sử dụng hàm `HYPERLINK()` trong Excel để tạo link giữa các sheet.

### Sheet chi tiết (mỗi Line = 1 Sheet)

Tên sheet: `Line 1`, `Line 2`...

**Bố cục bảng** (cột = ngày, hàng = block/SP):

| | Thứ 2 (10/3) | Thứ 3 (11/3) | CN (16/3 - NGHỈ) | ... |
|---|---|---|---|---|
| **Nhóm** | BM8R030 | BM8R030 | — | ... |
| **Phút phân bổ** | 150' | 150' | 0 | ... |
| **─ BM8R030110** (10'/sp) | 10 sp ✅ | — | 0 | ... |
| **─ BM8R031110** (20'/sp) | 2 sp | 8 sp ✅ | 0 | ... |
| **Số người** | 5 | 5 | 0 | ... |
| **Tổng SP trong ngày** | **12** | **8** | **0** | ... |

### Phạm vi ngày

Cột ngày luôn bắt đầu từ **ngày bắt đầu** đến **ngày deadline** của kế hoạch.

### Ngày nghỉ (IsDayOff)

- **Vẫn hiển thị** cột ngày nghỉ trong bảng (không bỏ cột)
- Tất cả giá trị hiển thị **số 0** (0 sp, 0 phút, 0 người)

## Quy tắc xử lý

1. **1 ô ngày có 2 block khác nhau:** Hiển thị 2 nhóm riêng biệt, mỗi nhóm liệt kê SP con riêng
2. **SP có OpenMinutes = 0:** Bỏ qua, không hiển thị
3. **MinutesPerProduct chưa có:** Hiển thị gạch `—` thay vì số SP, kèm cảnh báo
4. **SP không thuộc block nào:** Dừng export, yêu cầu gán block trước (xem mục trên)
5. **Line không có block nào:** Bỏ qua, không tạo sheet
6. **Block có tất cả SP đều OpenMinutes = 0:** Bỏ qua block đó
7. **Làm tròn số SP:** Làm tròn lên (`Math.Ceiling`) vì không thể làm 0.5 sản phẩm
