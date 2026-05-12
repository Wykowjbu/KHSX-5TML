# Export Kế Hoạch Sản Xuất

Export cần phản ánh dữ liệu mới:

- Function / line.
- Ca A/B.
- BuildGroup.
- `Gr.xxx`.
- Số phút được xếp.
- Trạng thái vượt capacity hoặc vượt deadline.

Nguồn dữ liệu chính khi export là lịch đang nằm trên grid (`schedule.json`) và các block hiện tại. Các schema planning/open minutes mới không còn dựa trên ProductGroup cũ.
