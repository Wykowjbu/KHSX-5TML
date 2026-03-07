# Development Workflow — KHSX

Quy trình phát triển tính năng mới cho hệ thống KHSX, sử dụng multi-agent collaboration.

## Agent Pipeline

```
User Request
    │
    ▼
┌─────────────────────┐
│ Requirement Analyst  │  Phân tích yêu cầu → output: requirement spec
└─────────┬───────────┘
          ▼
┌─────────────────────┐
│ System Architect     │  Thiết kế kiến trúc → output: architecture spec
└─────────┬───────────┘
          ▼
┌─────────────────────┐
│ Task Planner         │  Phân rã task → output: task breakdown
└─────────┬───────────┘
          ▼
    ┌─────┴─────┐
    ▼           ▼
┌──────────┐ ┌──────────────┐
│ Backend  │ │ Frontend     │  Implement song song
│ Developer│ │ Developer    │
└────┬─────┘ └──────┬───────┘
     └───────┬──────┘
             ▼
┌─────────────────────┐
│ Code Reviewer        │  Review code → output: review report
└─────────┬───────────┘
          ▼
┌─────────────────────┐
│ QA Tester            │  Kiểm thử → output: test report
└─────────┬───────────┘
          ▼
┌─────────────────────┐
│ Documentation Writer │  Cập nhật tài liệu trong docs/
└─────────────────────┘
```

## Chi Tiết Từng Bước

### Bước 1: Phân Tích Yêu Cầu (Requirement Analyst)

**Trigger:** User mô tả tính năng mới hoặc thay đổi

**Input:**
- Mô tả từ user
- Tài liệu nghiệp vụ hiện tại: `docs/02-bai-toan-nghiep-vu.md`, `docs/03-mo-hinh-nghiep-vu.md`

**Output:**
- Danh sách actors
- Functional & non-functional requirements
- Business rules mới/thay đổi
- Acceptance criteria

**Ví dụ prompt:**
```
@requirement-analyst Phân tích yêu cầu: Thêm tính năng xuất kế hoạch sản xuất ra file PDF
```

### Bước 2: Thiết Kế Kiến Trúc (System Architect)

**Trigger:** Requirement analysis hoàn thành

**Input:**
- Output từ Requirement Analyst
- Kiến trúc hiện tại: `Models/`, `Services/`, `ViewModels/`
- JSON schema: `docs/07-thiet-ke-luu-tru-thong-tin-bang-json.md`

**Output:**
- Data model changes
- JSON schema updates
- Service interface design
- MVVM binding specs

**Ví dụ prompt:**
```
@system-architect Thiết kế kiến trúc cho tính năng xuất PDF dựa trên requirement analysis sau: [paste output]
```

### Bước 3: Phân Rã Task (Task Planner)

**Trigger:** Architecture design hoàn thành

**Input:**
- Requirements + Architecture specs

**Output:**
- Task list với dependencies
- Assignment cho Backend/Frontend Developer
- Complexity estimates
- Acceptance criteria per task

**Ví dụ prompt:**
```
@task-planner Tạo task breakdown cho feature xuất PDF: [paste requirement + architecture]
```

### Bước 4: Implementation (Backend + Frontend Developer)

**Trigger:** Task breakdown hoàn thành

**Backend Developer:**
- Implement Models → Services → ViewModel integration
- Tham chiếu: `docs/06-logic-lap-ke-hoach.md`, `docs/05-dac-ta-import-excel.md`

**Frontend Developer:**
- Implement ViewModel properties → XAML layout → Interactions
- Tham chiếu: `docs/09-thiet-ke-ui-ux.md`

**Ví dụ prompt:**
```
@backend-developer Implement task #1: Tạo PdfExportService theo spec sau: [paste task]
@frontend-developer Implement task #3: Thêm nút Export PDF vào toolbar: [paste task]
```

### Bước 5: Code Review (Code Reviewer)

**Trigger:** Implementation hoàn thành

**Input:**
- Code changes từ developers

**Checklist:**
- MVVM compliance
- Business logic correctness
- Security (input validation)
- Performance (async I/O, UI thread)

**Ví dụ prompt:**
```
@code-reviewer Review code trong Services/PdfExportService.cs và ViewModels/MainViewModel.cs
```

### Bước 6: Testing (QA Tester)

**Trigger:** Code review passed

**Input:**
- Acceptance criteria từ Task Planner
- Code changes đã reviewed

**Scope:**
- Happy path tests
- Edge cases từ `docs/15-cac-truong-hop-dac-biet.md`
- Regression tests

**Ví dụ prompt:**
```
@qa-tester Tạo test cases cho feature xuất PDF với acceptance criteria: [paste criteria]
```

### Bước 7: Documentation (Documentation Writer)

**Trigger:** Testing passed

**Input:**
- Feature description
- Test results

**Output:**
- Cập nhật docs liên quan
- Thêm user guide nếu cần
- Update `docs/README.md`

**Ví dụ prompt:**
```
@documentation-writer Cập nhật tài liệu cho feature xuất PDF đã hoàn thành
```

## Quy Tắc Chung

1. **Không bỏ bước** — mỗi agent đóng vai trò kiểm soát chất lượng
2. **Output rõ ràng** — mỗi agent phải output theo format chuẩn
3. **Tham chiếu docs/** — luôn reference tài liệu hiện có
4. **Rollback** — nếu review hoặc test fail, quay lại bước implementation

## Hotfix Workflow (Sửa Bug Nhanh)

Với bug nhỏ, có thể rút gọn pipeline:

```
Bug Report → Backend/Frontend Developer → Code Reviewer → QA Tester → Done
```
