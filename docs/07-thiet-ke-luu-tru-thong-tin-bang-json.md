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
* số lượng theo từng group (Gr.xxx)

---

### 2️⃣ File MES (Manufacturing Execution System)

Chứa:

* productId
* open minutes (số phút sản xuất còn lại)

---

# 3. Quy tắc quan trọng của hệ thống

MES luôn hoạt động theo **group mới nhất**.

Ví dụ:

MES hiện tại đã chạy đến:

```
Gr.285
```

Thì hệ thống lập kế hoạch **chỉ quan tâm deadline của Gr.285**.

Không cần quan tâm:

```
Gr.284
```

dù vẫn còn open minutes.

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
│   └── workers.json
│
└── settings.json
```

---

# 5. products.json

Lưu thông tin sản phẩm.

```json
[
  {
    "productId": "BM8R030110",
    "groupId": "BM8R030",
    "function": "DRIVER DOOR PANEL",
    "minutesPerProduct": 18
  }
]
```

---

# 6. productGroups.json

Danh sách nhóm sản phẩm.

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
gom open minutes theo ProductGroup
```

---

# 8. deadlines.json

Deadline được thiết lập theo group marketing.

```json
[
  {
    "groupNumber": 284,
    "deadline": "2026-03-07"
  },
  {
    "groupNumber": 285,
    "deadline": "2026-03-09"
  }
]
```

---

# 9. settings.json

Hệ thống cần biết **MES đang chạy tới group nào**.

```json
{
  "currentMESGroup": 285,
  "efficiency": 1.15,
  "defaultShiftMinutes": 480
}
```

Deadline của block sẽ được lấy từ:

```
deadline[currentMESGroup]
```

---

# 10. blocks.json

Block đại diện cho **ProductGroup**.

Block length = tổng số phút sản xuất cần thiết.

```json
[
  {
    "blockId": "BLOCK_1",
    "productGroup": "BM8R030",
    "totalMinutes": 1200,
    "scheduledMinutes": 0,
    "deadline": "2026-03-09"
  }
]
```

---

# 11. schedule.json

File này lưu **vị trí block trên planning board**.

```json
[
  {
    "blockId": "BLOCK_1",
    "lineId": "LINE1",
    "shift": "A",
    "date": "2026-03-06",
    "startMinute": 0,
    "duration": 600
  }
]
```

Một block có thể chia thành nhiều phần nếu kéo qua nhiều ngày.

Ví dụ:

```
BLOCK_1 = 1200 phút
```

Có thể được schedule:

```
6/3 → 600 phút
7/3 → 600 phút
```

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

Cell **không phải entity dữ liệu**.

Cell chỉ là **UI representation**.

Dữ liệu thực nằm trong:

```
schedule.json
```

UI sẽ đọc `schedule.json` và render block lên grid.

---

# 14. Cấu trúc Model trong WPF

Ví dụ model Block:

```csharp
public class Block
{
    public string BlockId { get; set; }

    public string ProductGroup { get; set; }

    public double TotalMinutes { get; set; }

    public double ScheduledMinutes { get; set; }

    public DateTime Deadline { get; set; }
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

```
```
