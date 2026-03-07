# Bài Toán Nghiệp Vụ

Nhà máy nhận yêu cầu sản xuất từ bộ phận Marketing.

Các yêu cầu này được gửi dưới dạng file Excel.

File này chứa:

- tên sản phẩm (cột A)
- Gr.xxx (header cột E–J)
- **số lượng sản phẩm** theo từng Gr.xxx (giá trị trong cột E–J). Lưu ý: đây là số lượng, **không phải số phút**. Cần nhân với `minutesPerProduct` để ra tổng số phút cần sản xuất.
- tên nhóm sản phẩm (cột K) — ví dụ BM8R030110, BM8R031110, BM8R040110 đều có chung nhóm BM8R030
- minutesPerProduct (cột L) — số phút/sản phẩm, dùng để tính tổng phút từ số lượng
- function (cột M)

Vấn đề là việc lập kế hoạch sản xuất hiện đang thực hiện thủ công.

Các khó khăn gồm:

- nhiều line sản xuất
- số người mỗi ca thay đổi, khác hiệu suất
- deadline khác nhau
- dữ liệu MES thay đổi mỗi ngày

Điều này dẫn đến:

- lập kế hoạch không tối ưu
- dễ trễ deadline
- không tận dụng hết công suất nhà máy