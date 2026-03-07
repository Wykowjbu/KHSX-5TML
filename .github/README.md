# KHSX Multi-Agent Workspace

Hệ thống AI agents hỗ trợ phát triển **KHSX — Hệ Thống Lập Kế Hoạch Sản Xuất**.

## Cấu Trúc Workspace

```
.github/
├── agents/                          # AI Agent definitions
│   ├── requirement-analyst.agent.md # Phân tích yêu cầu nghiệp vụ
│   ├── system-architect.agent.md    # Thiết kế kiến trúc & data model
│   ├── task-planner.agent.md        # Phân rã task & lập kế hoạch
│   ├── backend-developer.agent.md   # Implement C#/Services/Models
│   ├── frontend-developer.agent.md  # Implement WPF/XAML/ViewModel
│   ├── code-reviewer.agent.md       # Review code quality & MVVM
│   ├── qa-tester.agent.md           # Kiểm thử & báo cáo bug
│   ├── feature-researcher.agent.md  # Nghiên cứu library & giải pháp
│   └── documentation-writer.agent.md# Viết & cập nhật tài liệu
│
├── workflows/                       # Quy trình làm việc
│   ├── development-workflow.md      # Pipeline phát triển tính năng
│   ├── review-workflow.md           # Quy trình review code
│   └── release-workflow.md          # Quy trình release version
│
docs/                                # Tài liệu dự án (đã có sẵn)
│   ├── 01-tong-quan-he-thong.md
│   ├── 02-bai-toan-nghiep-vu.md
│   ├── 03-mo-hinh-nghiep-vu.md
│   ├── 04-luong-du-lieu.md
│   ├── 05-dac-ta-import-excel.md
│   ├── 06-logic-lap-ke-hoach.md
│   ├── 07-thiet-ke-luu-tru-thong-tin-bang-json.md
│   ├── 08-scheduling-block.md
│   ├── 09-thiet-ke-ui-ux.md
│   ├── 10-cau-hinh-he-thong.md
│   ├── 14-quy-trinh-su-dung.md
│   └── 15-cac-truong-hop-dac-biet.md
```

## Agent Pipeline
Always follow this workflow:

```
User Request
    │
    ▼
Requirement Analyst  →  Phân tích yêu cầu
    │
    ▼
System Architect     →  Thiết kế kiến trúc & data model
    │
    ▼
Task Planner         →  Phân rã thành tasks cụ thể
    │
    ├──────────────────┐
    ▼                  ▼
Backend Developer   Frontend Developer  →  Implement song song
    │                  │
    └────────┬─────────┘
             ▼
Code Reviewer        →  Review chất lượng code
             │
             ▼
QA Tester            →  Kiểm thử & regression
             │
             ▼
Documentation Writer →  Cập nhật tài liệu
```

## Cách Sử Dụng

### Gọi Agent Trực Tiếp

Trong VS Code Copilot Chat, gõ `@` + tên agent:

```
@requirement-analyst Phân tích yêu cầu: thêm tính năng drag-drop block giữa các line
@system-architect Thiết kế data model cho block splitting
@task-planner Phân rã tasks cho feature export PDF
@backend-developer Implement SchedulingService.AutoPushBlocks()
@frontend-developer Thêm drag-drop handler cho planning grid
@code-reviewer Review Services/SchedulingService.cs
@qa-tester Tạo test cases cho capacity calculation
@feature-researcher So sánh EPPlus vs ClosedXML cho Excel import
@documentation-writer Cập nhật docs cho feature mới hoàn thành
```

### Workflow Đầy Đủ (Feature Mới)

Xem chi tiết: `.github/workflows/development-workflow.md`

### Hotfix (Sửa Bug Nhanh)

```
@backend-developer Fix bug: capacity tính sai khi efficiency = 0
@code-reviewer Review fix trong Services/CapacityService.cs
@qa-tester Verify fix capacity calculation edge case
```

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Frontend | WPF (XAML) |
| Language | C# |
| Pattern | MVVM |
| Storage | JSON files |
| Import | Excel (Marketing + MES) |
| IDE | Visual Studio / VS Code |

## Tài Liệu Tham Khảo

- Nghiệp vụ: `docs/02-bai-toan-nghiep-vu.md`, `docs/03-mo-hinh-nghiep-vu.md`
- Logic lập KH: `docs/06-logic-lap-ke-hoach.md`
- Import Excel: `docs/05-dac-ta-import-excel.md`
- JSON storage: `docs/07-thiet-ke-luu-tru-thong-tin-bang-json.md`
- UI/UX: `docs/09-thiet-ke-ui-ux.md`
- Edge cases: `docs/15-cac-truong-hop-dac-biet.md`
