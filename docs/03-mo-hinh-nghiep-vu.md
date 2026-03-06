# Mô Hình Nghiệp Vụ

Các thực thể chính trong hệ thống:

Product
ProductGroup
ProductionGroup
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
Chú ý quan trong việc settdealine là set cho Gr.xx(ProductionGroup) chứ ko phải setdealine cho ProductGroup