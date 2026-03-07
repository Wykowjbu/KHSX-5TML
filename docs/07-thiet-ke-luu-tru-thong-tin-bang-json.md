# Thiết Kế Lưu Trữ Dữ Liệu JSON

## Hệ thống Lập Kế Hoạch Sản Xuất (WPF)

## 1. Mục tiêu

Hệ thống lập kế hoạch sản xuất được xây dựng bằng **WPF Desktop Application**.

Hệ thống **không sử dụng database**, toàn bộ dữ liệu được lưu bằng **file JSON**.

Ưu điểm:

* dễ backup
* dễ debug
* không cần cài đặt database server
* phù hợp với ứng dụng desktop nội bộ

---

# 2. Nguyên tắc hoạt động của hệ thống

Hệ thống nhận dữ liệu từ 2 nguồn:

### 1️⃣ File Marketing Release

Chứa:

* tên sản phẩm
* nhóm sản phẩm
* **số lượng sản phẩm** theo từng group (Gr.xxx) — cần nhân với `minutesPerProduct` để tính tổng phút
* Hệ thống lưu tổng cộng dồn tất cả Gr.xxx (không cần tách từng Gr.xxx)

---

### 2️⃣ File MES (Manufacturing Execution System)

Chứa:

* productId
* open minutes (số phút sản xuất còn lại)

---

# 3. Quy tắc quan trọng của hệ thống

`currentMESGroup` cho biết hệ thống MES **đang chạy đến Gr.xxx nào**. Tất cả sản phẩm trong cùng một ProductGroup sẽ có chung `currentMESGroup`.

Ví dụ:

MES hiện tại đã chạy đến:

```
Gr.285
```

Thì deadline check mặc định áp dụng **chung cho tất cả block** dựa trên Gr.xxx mà ProductGroup đó đã đến.

Tuy nhiên:
- Một ProductGroup khác có thể mới đến Gr.284 (deadline khác)
- User có thể bấm vào **từng block** để chỉnh deadline riêng (customDeadline)
- Mỗi ProductGroup gắn với một Gr.xxx duy nhất (Gr.xxx mà nhóm đó đã đến)

---

# 4. Cấu trúc thư mục dữ liệu

```text
Data/
│
├── products.json
├── productGroups.json
├── openMinutes.json
├── deadlines.json
│
├── blocks.json
├── schedule.json
│
├── factory/
│   ├── lines.json
│   ├── shifts.json
│   ├── workers.json
│   └── cellCapacity.json
│
└── settings.json
```

---

# 5. products.json

Lưu thông tin sản phẩm. `totalQuantity` là tổng số lượng cộng dồn tất cả Gr.xxx (không tách từng Gr).

```json
[
  {
    "productId": "BM8R030110",
    "groupId": "BM8R030",
    "productionGroup": "Gr.285",
    "function": "DRIVER DOOR PANEL",
    "minutesPerProduct": 18,
    "totalQuantity": 50
  }
]
```

---

# 6. productGroups.json

Danh sách nhóm sản phẩm. Trường **name** là **tên do người dùng đặt** (hiển thị cho nhóm); giao diện cần có phần cho phép người dùng đặt/sửa tên nhóm.

```json
[
  {
    "groupId": "BM8R030",
    "name": "DRIVER DOOR PANEL"
  }
]
```

---

# 7. openMinutes.json

Dữ liệu import từ hệ thống MES.

```json
[
  {
    "productId": "BM8R093105",
    "openMinutes": 142.92
  }
]
```

Sau khi import, hệ thống sẽ:

```
gom open minutes theo ProductGroup và gắn ProductionGroup (Gr.xxx mà nhóm đó đã đến) tương ứng cho block.
```

---

# 8. deadlines.json

Deadline chung được thiết lập theo production group (Gr.xxx). Trường `groupNumber` là mã Gr.xxx ("Gr.284", "Gr.285"...).

```json
[
  {
    "groupNumber": "Gr.284",
    "deadline": "2026-03-07"
  },
  {
    "groupNumber": "Gr.285",
    "deadline": "2026-03-09"
  }
]
```

---

# 9. settings.json và Cấu hình Factory

Hệ thống cần biết **MES đang chạy tới group nào** và cấu hình mặc định. (Lưu ý: Do yêu cầu hiệu suất của mỗi ca khác nhau, efficiency sẽ được chuyển xuống lưu từng ca trong Factory cấu hình, không còn dùng chung global).

```json
{
  "currentMESGroup": "Gr.285",
  "defaultShiftMinutes": 480
}
```

**factory/shifts.json** (Cấu hình mặc định theo line + shift)
```json
[
  {
    "line": "Line 1",
    "shift": "A",
    "efficiency": 1.15,
    "workers": 10
  }
]
```

**factory/cellCapacity.json** (Override số người / số phút theo từng ô: line + shift + ngày)

Khi user chỉnh sửa riêng cho một cell (double-click vào ô để đổi số người, số phút làm việc cho ngày đó + ca đó), hệ thống lưu vào đây. Ô nào không có trong file thì dùng mặc định từ `shifts.json`. Công suất ô = workers × minutes × efficiency (của ô đó hoặc mặc định ca).

```json
[
  {
    "lineId": "Line 1",
    "shift": "A",
    "date": "2026-03-07",
    "workers": 12,
    "minutes": 480
  }
]
```

---

# 10. blocks.json (Danh sách block — chờ gán hoặc đã gán một phần)

- **Một ProductGroup = một block.** Nếu sản phẩm trong nhóm nằm ở nhiều Gr.xxx thì vẫn chỉ lưu **một block** với tổng số phút (totalMinutes). Block gắn với ProductionGroup (Gr.xxx mà nhóm đó đã đến) để kiểm tra deadline.
- Block length = **tổng số phút** còn lại cần sản xuất (open minutes của nhóm).
- Hệ thống lưu tổng cộng dồn tất cả Gr.xxx (không cần tách từng Gr.xxx).
- **allocatedMinutes:** số phút **đã được đặt vào schedule** (đã kéo vào line). Block chưa gán thì allocatedMinutes = 0; khi gán hết thì có thể xóa khỏi blocks.json hoặc đánh dấu trạng thái.
- **status** (gợi ý): `Unassigned` (chưa gán), `FullyAssigned` (đã gán hết). Khi user kéo block từ line về lại danh sách chờ: cập nhật lại block trong blocks.json (allocatedMinutes = 0, status = Unassigned).
- **Kéo block = kéo toàn bộ** (không có kéo một nửa). Ngoại trừ: phần vượt deadline được tách ra để kéo riêng.
- **customDeadline:** nếu user ghi đè deadline riêng cho block thì lưu tại đây.

```json
[
  {
    "blockId": "BLOCK_1",
    "productGroup": "BM8R030",
    "productionGroup": "Gr.285",
    "totalMinutes": 1200,
    "allocatedMinutes": 0,
    "status": "Unassigned",
    "customDeadline": "2026-03-10"
  }
]
```

---

# 11. schedule.json

File này lưu **vị trí block trên planning board** (Các block đã gán).

```json
[
  {
    "blockId": "BLOCK_1_SPLIT_1",
    "parentId": "BLOCK_1",
    "productGroup": "BM8R030",
    "productionGroup": "Gr.285",
    "lineId": "Line 1",
    "shift": "A",
    "date": "2026-03-06",
    "allocatedMinutes": 600,
    "customDeadline": "2026-03-10" 
  }
]
```

Một block có thể chia thành nhiều phần nếu kéo qua nhiều ngày. Khóa `parentId` cho phép gom chúng lại. Nếu người dùng **Kéo thả block ngược về danh sách chờ / Xóa khỏi line**, hệ thống sẽ:
1. Xóa các phân mảnh (split block) trong `schedule.json`.
2. Tính tổng số phút `allocatedMinutes` của các mảnh đó.
3. Sinh lại một Block nguyên vẹn vào `blocks.json` để chờ xếp lịch tiếp.

**Khi import MES mới (cập nhật open minutes):**
- Block đã nằm trên line **giữ nguyên vị trí**, chỉ cập nhật số phút.
- **Số phút giảm:** block tự co lại. Các split block trên schedule co lại tương ứng.
- **Số phút tăng:** block tự mở rộng. Phần dư thêm vào trên schedule.
- Mỗi khi có thay đổi, hệ thống **tự re-render** và đẩy block lên ngày sớm nhất có thể.

---

# 12. Planning Board UI

Giao diện planning board có cấu trúc:

```
Cột = ngày
Hàng = line + shift
```

Ví dụ:

| Line/Shift | 6/3   | 7/3   | 8/3   | 9/3 |
| ---------- | ----- | ----- | ----- | --- |
| Line1-A    | block | block |       |     |
| Line1-B    |       | block | block |     |
| Line2-A    |       |       |       |     |
| Line2-B    |       |       |       |     |

---

# 13. Cell có lưu dữ liệu không?

- **Vị trí block trong ô:** không lưu theo từng cell; dữ liệu nằm trong `schedule.json`. UI đọc `schedule.json` và render block lên grid.
- **Công suất từng ô (số người, số phút):** mặc định từ `factory/shifts.json`; nếu user chỉnh riêng cho (line, shift, ngày) thì lưu vào `factory/cellCapacity.json`.

---

# 14. Cấu trúc Model trong WPF

Ví dụ model BlockData (JSON mapping):

```csharp
public class BlockData
{
    public Guid BlockId { get; set; }
    public Guid? ParentId { get; set; } // Liên kết các block bị xé lẻ
    public string GroupId { get; set; } // ProductGroup vd: BM8R030
    public string ProductionGroup { get; set; } // Gr.xxx
    
    public double TotalMinutesRequired { get; set; }
    public double AllocatedMinutes { get; set; } // Đã đặt vào schedule
    
    public string Status { get; set; } // Unassigned | PartiallyAssigned | FullyAssigned
    
    public DateTime? CustomDeadline { get; set; } // Ghi đè deadline
}
```

---

# 15. Service đọc ghi JSON

```csharp
public static class JsonStorage
{
    public static T Load<T>(string path)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json);
    }

    public static void Save<T>(string path, T data)
    {
        string json = JsonSerializer.Serialize(data,
        new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(path, json);
    }
}
```

---

# 16. Cấu trúc project WPF

```text
ProductionPlanning/
│
├── Models
├── Services
├── ViewModels
├── Views
│
├── Data
│
└── Utils
```

Áp dụng kiến trúc **MVVM**.

---

# 17. Lợi ích của thiết kế này

* Không cần database
* Dễ backup dữ liệu
* Dễ debug
* Phù hợp với ứng dụng desktop
* dễ mở rộng sau này
