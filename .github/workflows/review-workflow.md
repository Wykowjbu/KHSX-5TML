# Review Workflow — KHSX

Quy trình review code đảm bảo chất lượng cho hệ thống KHSX.

## Khi Nào Cần Review

| Loại thay đổi | Bắt buộc review? | Agent review |
|----------------|-------------------|--------------|
| Model mới/sửa | ✅ Có | Code Reviewer |
| Service logic | ✅ Có | Code Reviewer |
| ViewModel binding | ✅ Có | Code Reviewer |
| XAML layout | ✅ Có | Code Reviewer |
| JSON schema change | ✅ Có | Code Reviewer + System Architect |
| Config change | ⚠️ Tùy | Code Reviewer |
| Doc update | ❌ Không | — |

## Quy Trình Review

```
Developer hoàn thành code
        │
        ▼
┌──────────────────────┐
│ Self-check Checklist  │  Developer tự review trước
└──────────┬───────────┘
           ▼
┌──────────────────────┐
│ Code Reviewer         │  Review theo checklist chuẩn
└──────────┬───────────┘
           │
     ┌─────┴─────┐
     ▼           ▼
  ✅ Pass     ❌ Fail
     │           │
     ▼           ▼
  QA Test    Developer fix
             rồi re-submit
```

## Self-Check Trước Khi Submit (Cho Developer)

### Backend Developer
- [ ] Business logic đúng theo `docs/06-logic-lap-ke-hoach.md`
- [ ] Capacity formula: workers × minutes × efficiency
- [ ] JSON serialization/deserialization hoạt động
- [ ] Excel import validate input data
- [ ] Async/await cho file I/O
- [ ] Không có hardcoded values

### Frontend Developer
- [ ] Data binding hoạt động 2-way khi cần
- [ ] INotifyPropertyChanged cho tất cả bound properties
- [ ] Không có business logic trong code-behind
- [ ] UI responsive (heavy work off UI thread)
- [ ] Drag-drop handlers không throw exception

## Code Reviewer Checklist

### 1. MVVM Compliance
- Business logic chỉ trong `Services/`
- ViewModel không reference View trực tiếp
- Code-behind chỉ chứa UI logic (drag-drop, focus, etc.)

### 2. Data Integrity
- JSON read/write atomic (không corrupt khi app crash)
- Excel import validate headers và data types
- Block minutes tính toán đúng sau MES update

### 3. Security
- File path validation cho JSON/Excel I/O
- Không log sensitive data
- Input sanitization cho user-entered values

### 4. Performance
- ObservableCollection không rebuild toàn bộ khi update 1 item
- Planning grid render hiệu quả với nhiều blocks
- File I/O sử dụng async

### 5. Edge Cases (tham chiếu docs/15)
- ProductGroup không có open minutes
- Block vượt deadline
- Sunday/holiday toggle
- Unmatched MES products

## Severity Levels

| Level | Ký hiệu | Mô tả | Action |
|-------|----------|--------|--------|
| Critical | 🔴 | Bug logic, data corruption, security | Phải fix trước khi merge |
| Warning | 🟡 | Performance, code smell, missing validation | Nên fix |
| Info | 🟢 | Style, naming, suggestion | Optional |

## Ví Dụ Prompt

```
@code-reviewer Review toàn bộ changes trong:
- Models/ProductBlock.cs (thêm SplitBlock method)
- Services/SchedulingService.cs (sửa auto-push logic)
- ViewModels/MainViewModel.cs (thêm SplitCommand)
Focus vào: business logic correctness và MVVM compliance
```
