# Đặc Tả Import Excel

Hệ thống dùng 3 nguồn Excel theo thứ tự.

## 1. Module List

File `module_list.xlsx` là nguồn mapping chính.

- Cột A `Part name`: tên Function.
- Cột B `Buildgroup`: BuildGroup.
- Cột C `FP`: mã 7 ký tự đầu dùng để match sản phẩm.

Một BuildGroup chỉ thuộc một Function, nhưng một BuildGroup có thể có nhiều FP.

Nếu sản phẩm có FP chưa tồn tại trong module list, hệ thống mở popup để user nhập tay `FP -> BuildGroup -> Function` và lưu lại cho lần sau.

## 2. Planning

File planning mới là `planning (1).xlsb`, sheet `Serienplaning`.

- Cột A: `Sektor`, chỉ lấy `U4`.
- Cột C: mã sản phẩm đầy đủ.
- Cột H-L: quantity theo từng `Gr.xxx`; header là tên `Gr.xxx`.
- Cột M: `Total`, chỉ lấy dòng `Total > 0`.
- Cột U: phút/sản phẩm, chia `1000`.

Cách tính:

```text
FP = 7 ký tự đầu của mã sản phẩm
BuildGroup/Function = tra FP trong module list
plannedMinutes = quantity từng Gr.xxx * minutesPerProduct
```

Dữ liệu được gom theo `BuildGroup + Gr.xxx` và lưu vào `planningBlocks.json`.

## 3. MES / OpenMin

File MES/OpenMin là file import ở bước `Import MES -> Auto Schedule`.

- Cột B: `Sektor`, chỉ lấy `U4`.
- Cột C: `Gr.xxx`.
- Cột D: mã sản phẩm đầy đủ.
- Cột I: open minutes, đã là tổng phút.

Dữ liệu được map qua FP giống planning và gom theo `BuildGroup + Gr.xxx`, lưu vào `openMinutes.json`.

Nếu MES có block không có trong planning, hệ thống vẫn tạo block và cảnh báo. Nếu planning có block mà MES thiếu, hệ thống dùng planned minutes làm fallback.
