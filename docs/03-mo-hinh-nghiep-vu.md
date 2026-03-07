# Mô Hình Nghiệp Vụ

Các thực thể chính trong hệ thống:

Product
ProductGroup
ProductionGroup (Gr.xxx)
ProductionLine
Shift
Worker
ProductionBlock
Deadline
OpenMinutes

---

# Product

Một sản phẩm cụ thể.

Ví dụ:

BM8R030110

---

# ProductGroup

Mỗi sản phẩm thuộc một nhóm sản phẩm.

Ví dụ:

BM8R030

Việc lập kế hoạch chỉ dựa trên ProductGroup.

---

# ProductionGroup

Ví dụ:

Gr.284
Gr.285
Gr.286

Đây là các batch sản xuất được phát hành từ Marketing.

Mỗi group có deadline riêng. 
Chú ý quan trọng: deadline được set cho **Gr.xxx (ProductionGroup)** chứ không phải set cho ProductGroup.

## Quy tắc CurrentMESGroup

`currentMESGroup` cho biết hệ thống MES **đang chạy đến Gr.xxx nào**. Tất cả sản phẩm trong cùng một ProductGroup sẽ có chung `currentMESGroup`.

Ví dụ: nếu `currentMESGroup = Gr.285`:
- Sản phẩm BM8R030110 thuộc nhóm BM8R030, đã đến Gr.285 → deadline của block BM8R030 là deadline của Gr.285.
- Sản phẩm BM8R097113 thuộc nhóm BM8R097, mới đến Gr.284 → deadline của block BM8R097 là deadline của Gr.284.

## Quy tắc Deadline check

- **Mặc định:** deadline check áp dụng **chung cho tất cả block** dựa trên Gr.xxx mà ProductGroup đã đến (theo `currentMESGroup`).
- **Tùy chỉnh riêng:** user có thể bấm vào **từng block** để chỉnh deadline riêng (customDeadline), ghi đè deadline mặc định của Gr.xxx.
- Mỗi ProductGroup gắn với một Gr.xxx duy nhất (Gr.xxx mà nhóm đó đã đến), không cần track từng Gr.xxx riêng lẻ.