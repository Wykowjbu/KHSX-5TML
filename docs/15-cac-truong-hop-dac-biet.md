# Các Trường Hợp Đặc Biệt

## FP Không Có Trong Module List

Hệ thống mở popup cho user nhập BuildGroup và Function. Mapping được lưu lại vào `moduleMappings.json`.

## MES Có Block Không Có Trong Planning

Vẫn tạo block theo MES và hiển thị cảnh báo.

## Planning Có Block Nhưng MES Thiếu

Dùng planned minutes từ planning để tạo block.

## Thiếu Deadline

Không auto schedule. User phải cấu hình đủ deadline cho mọi `Gr.xxx`.

## Cell Vượt Capacity

Block vẫn nằm nguyên trong cell deadline. Cell và block hiển thị viền đỏ để user tăng người, tăng phút làm việc hoặc kéo cụm block.

## Kéo Cell Có Nhiều Block

Kéo một block trong cell có nhiều block sẽ kéo nguyên cụm block của cell đó sang cell đích.
