# Tổng Quan Hệ Thống

Hệ thống lập kế hoạch sản xuất cho nhà máy dựa trên Module List, Planning và MES/OpenMin.

Nguồn dữ liệu:

- Module List: map FP, BuildGroup và Function.
- Planning: kế hoạch theo sản phẩm, `Gr.xxx` và số phút/sản phẩm.
- MES/OpenMin: số phút còn lại thực tế.

Kết quả là các block sản xuất theo `BuildGroup + Gr.xxx`, được auto schedule vào line Function theo deadline của `Gr.xxx`.
