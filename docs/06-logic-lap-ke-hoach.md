# Logic Lập Kế Hoạch

Hệ thống lập kế hoạch theo cặp `BuildGroup + Gr.xxx`, không gom toàn bộ Gr.xxx của cùng BuildGroup thành một block nữa.

## Tạo Block

Nguồn ưu tiên:

1. MES/OpenMin nếu có `BuildGroup + Gr.xxx`.
2. Planning nếu MES thiếu cặp đó.

Mỗi block có:

- BuildGroup.
- Function.
- Gr.xxx.
- Tổng số phút cần làm.

## Deadline

Deadline bắt buộc theo từng `Gr.xxx`.

Nếu có block thuộc `Gr.xxx` chưa cấu hình deadline, hệ thống không auto schedule và yêu cầu user nhập deadline trước.

## Auto Schedule

Line được tạo theo Function. Vì nghiệp vụ quy định một BuildGroup chỉ thuộc một Function, line Function tương ứng với BuildGroup đó.

Quy tắc xếp:

- Đặt nguyên block vào ngày deadline của `Gr.xxx`.
- Nếu BuildGroup chỉ chọn ca A, đặt vào ca A.
- Nếu chỉ chọn ca B, đặt vào ca B.
- Nếu chọn A+B, ưu tiên ca A; nếu đặt vào A làm vượt capacity thì chuyển sang B; nếu B cũng vượt thì vẫn đặt vào B.
- Không tự chia block theo capacity trong bước auto schedule.

Capacity cell:

```text
capacity = workers * minutes * efficiency
```

Nếu tổng phút trong cell vượt capacity, cell và block trong cell hiển thị viền đỏ. User sẽ tự tăng người, tăng phút làm việc hoặc kéo cụm block sang cell khác.

## Kéo Thả

Khi một cell có nhiều block, kéo một block trong cell sẽ kéo nguyên cụm block của cell đó sang cell đích.
