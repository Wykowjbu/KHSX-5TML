# Thiết Kế Lưu Trữ JSON

Hệ thống vẫn dùng file JSON trong thư mục `Data`, không dùng database.

## moduleMappings.json

Lưu mapping FP sang BuildGroup và Function.

```json
[
  {
    "fp": "BM8R221",
    "buildGroup": "BM8R220",
    "functionName": "REAR DOOR",
    "isManual": false
  }
]
```

`isManual = true` khi mapping do user nhập trong popup vì module list thiếu FP.

## planningBlocks.json

Lưu planned minutes từ file planning, gom theo `BuildGroup + Gr.xxx`.

```json
[
  {
    "buildGroup": "BM8R220",
    "productionGroup": "Gr.285",
    "functionName": "REAR DOOR",
    "plannedMinutes": 1200
  }
]
```

## openMinutes.json

Lưu open minutes từ MES/OpenMin, gom theo `BuildGroup + Gr.xxx`.

```json
[
  {
    "buildGroup": "BM8R220",
    "productionGroup": "Gr.285",
    "functionName": "REAR DOOR",
    "openMinutes": 900
  }
]
```

## buildGroupSettings.json

Lưu ca được phép làm và số người từng ca cho từng BuildGroup.

```json
[
  {
    "buildGroup": "BM8R220",
    "functionName": "REAR DOOR",
    "useShiftA": true,
    "useShiftB": true,
    "workersA": 3,
    "workersB": 2
  }
]
```

## deadlines.json

Deadline bắt buộc theo từng `Gr.xxx`.

```json
[
  {
    "groupNumber": "Gr.285",
    "deadline": "2026-05-12"
  }
]
```

## factory/lines.json, schedule.json, blocks.json

Tiếp tục lưu cấu hình line, ngày làm việc và block trên grid như trước, nhưng line sau auto schedule là tên Function.
