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

Chứa: tên sản phẩm, nhóm (cột K), minutesPerProduct (cột L), **số lượng** theo từng Gr.xxx (cột E–J). Hệ thống lưu **số lượng theo từng Gr.xxx** và **tổng số lượng** (products.json: quantitiesByGroup, totalQuantity). Merge khi import lại: cập nhật từng Gr có trong file; Gr không có giữ nguyên.

---

### 2️⃣ File MES (Manufacturing Execution System)

Chứa:

* productId
* open minutes (số phút sản xuất còn lại)

---

# 3. ProductionGroup (Gr.xxx) cho từng ProductGroup

Mỗi **ProductGroup** cần một **Gr.xxx** để tra deadline. Giá trị này lưu **theo từng nhóm** trong `productGroups.json` (trường `productionGroup`):

- **User chọn thủ công:** Trong giao diện, user có thể gán/sửa Gr.xxx cho từng nhóm.
- **Mặc định:** Nếu chưa chọn, hệ thống dùng **Gr.xxx lớn nhất** mà nhóm có trong dữ liệu (từ số lượng theo Gr trong Marketing).

Khi tạo block, hệ thống lấy `productionGroup` của ProductGroup đó để gắn vào block và tra deadline trong `deadlines.json`. User có thể ghi đè deadline cho từng block bằng `customDeadline`.

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
│   ├── lines.json      # Danh sách line (Line 1, Line 2, ...)
│   ├── shifts.json     # Mặc định workers, efficiency theo line + shift
│   └── cellCapacity.json   # Override workers, minutes theo (line, shift, ngày)
│
└── settings.json
```

---

# 5. products.json

Lưu thông tin sản phẩm từ Marketing: nhóm, số lượng theo từng Gr.xxx và tổng. Dùng để merge khi import lại và để suy ra Gr.xxx mặc định cho ProductGroup (Gr lớn nhất).

```json
[
  {
    "productId": "BM8R030110",
    "groupId": "BM8R030",
    "function": "DRIVER DOOR PANEL",
    "minutesPerProduct": 18,
    "quantitiesByGroup": {
      "Gr.284": 20,
      "Gr.285": 30
    },
    "totalQuantity": 50
  }
]
```

- **quantitiesByGroup:** số lượng theo từng Gr.xxx (chỉ những Gr có dữ liệu).
- **totalQuantity:** tổng cộng dồn tất cả Gr.
- Khi import lại: cập nhật từng Gr có trong file mới; Gr không có trong file mới giữ nguyên; sau đó tính lại totalQuantity.

---

# 6. productGroups.json

Danh sách nhóm sản phẩm. Mỗi nhóm có **tên hiển thị** (user đặt/sửa) và **Gr.xxx dùng để tra deadline** (productionGroup).

```json
[
  {
    "groupId": "BM8R030",
    "name": "DRIVER DOOR PANEL",
    "productionGroup": "Gr.285"
  }
]
```

- **name:** tên hiển thị do user đặt; giao diện cho phép sửa.
- **productionGroup:** Gr.xxx áp dụng cho nhóm này để lấy deadline. User chọn thủ công; nếu trống, hệ thống dùng Gr.xxx **lớn nhất** có trong `products` của nhóm (từ quantitiesByGroup).
- Khi tạo block cho ProductGroup, hệ thống lấy `productionGroup` từ đây (hoặc tính mặc định) và gắn vào block.

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

Sau khi import, hệ thống gom open minutes theo ProductGroup. Khi tạo block, ProductionGroup (Gr.xxx) của block lấy từ `productGroups[].productionGroup` (hoặc mặc định Gr.xxx lớn nhất của nhóm).

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

**settings.json** — cấu hình chung (vd số phút mặc định một ca).

```json
{
  "defaultShiftMinutes": 480
}
```

**factory/lines.json** — danh sách line (để biết có những line nào, tạo hàng trên bảng).

```json
["Line 1", "Line 2"]
```

**factory/shifts.json** — Cấu hình mặc định theo (line + shift): số công nhân, hiệu suất. Số phút mặc định lấy từ settings hoặc cùng file.

```json
[
  {
    "lineId": "Line 1",
    "shift": "A",
    "efficiency": 1.15,
    "workers": 13
  },
  {
    "lineId": "Line 1",
    "shift": "B",
    "efficiency": 1.15,
    "workers": 13
  }
]
```

**factory/cellCapacity.json** — Override cho từng ô (line + shift + ngày). Double-click vào **cell** để chỉnh số người, số phút cho **đúng ngày đó** (vd tăng ca = tăng số phút trong cell). Ô không có trong file dùng mặc định từ `shifts.json`. Công suất ô = workers × minutes × efficiency.

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

- **Một ProductGroup = một block.** Độ dài block = tổng open minutes của nhóm (từ MES). **productionGroup** lấy từ `productGroups[].productionGroup` (hoặc mặc định Gr.xxx lớn nhất) để tra deadline.
- **allocatedMinutes:** số phút đã đặt vào schedule. Block chưa gán: allocatedMinutes = 0; gán hết: status = FullyAssigned.
- **status:** `Unassigned` | `PartiallyAssigned` (đã gán một phần, block trải nhiều cell) | `FullyAssigned`. Kéo block từ line về danh sách chờ → cập nhật lại block (allocatedMinutes = 0, status = Unassigned).
- **Kéo toàn bộ block** (không kéo một nửa). Riêng **phần vượt deadline** tách ra: kéo phần sau deadline thì chỉ phần đó di chuyển.
- **customDeadline:** user ghi đè deadline riêng cho block thì lưu tại đây.

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
- **Công suất từng ô:** mặc định từ `factory/shifts.json` (theo line + shift). User double-click **hàng** để sửa mặc định cả ca; double-click **cell** để sửa riêng ngày đó (vd tăng ca = tăng số phút) — override lưu vào `factory/cellCapacity.json`.

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
