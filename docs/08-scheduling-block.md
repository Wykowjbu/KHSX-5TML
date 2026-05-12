# Scheduling Block

Mỗi block đại diện cho một cặp `BuildGroup + Gr.xxx`.

Ví dụ:

- `BM8R220 / Gr.285`
- `BM8R220 / Gr.286`

Hai block này tách riêng dù cùng BuildGroup.

## Nguồn Số Phút

- Có MES/OpenMin: dùng open minutes từ MES.
- Không có MES nhưng planning có: dùng planned minutes từ planning.
- MES có nhưng planning không có: vẫn tạo block, đồng thời cảnh báo.

## Vị Trí Tự Động

Block được đặt vào cell ngày deadline của `Gr.xxx` trên line Function của BuildGroup.

Nếu cell vượt capacity, hệ thống không tự chia nhỏ block mà hiển thị viền đỏ.
