# Cấu Hình Hệ Thống

Quản lý có thể điều chỉnh:

- số công nhân (theo ca mặc định trong `factory/shifts.json`, hoặc riêng từng ô trong `factory/cellCapacity.json`)
- số phút làm việc (mặc định 480; có thể override theo từng ô)
- hiệu suất (theo từng line + shift trong factory, không dùng global)
- ngày nghỉ / ngày làm việc (toggle từng cell)
- tăng ca

Cấu hình mặc định:

- Workers: thay đổi được (theo ca hoặc theo từng ngày)
- Minutes: 480
- Efficiency: 115% (theo từng ca trong factory/shifts.json)
- Sunday: nghỉ (mặc định; có thể click toggle thành ngày làm việc)
- Ngày lễ: có thể click toggle bất kỳ ngày nào thành ngày nghỉ (ví dụ thứ 2 trúng lễ)