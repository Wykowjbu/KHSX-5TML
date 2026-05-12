# Cấu Hình Hệ Thống

## Deadline

Deadline cấu hình theo từng `Gr.xxx` và là bắt buộc trước khi auto schedule.

## BuildGroup/Ca

Mỗi BuildGroup cấu hình:

- Function.
- Danh sách FP.
- Ca A bật/tắt.
- Số người ca A.
- Ca B bật/tắt.
- Số người ca B.

Nếu chọn A+B, auto schedule ưu tiên A; nếu A vượt capacity thì chuyển sang B.

## Capacity

Capacity của một cell:

```text
workers * minutes * efficiency
```

Nếu tổng phút block trong cell vượt capacity, cell hiển thị viền đỏ.
