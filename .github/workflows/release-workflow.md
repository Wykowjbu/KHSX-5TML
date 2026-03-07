# Release Workflow — KHSX

Quy trình release version mới cho hệ thống KHSX.

## Release Checklist

### Pre-Release

```
┌─────────────────────┐
│ Tất cả tasks Done    │  Task Planner confirm
└─────────┬───────────┘
          ▼
┌─────────────────────┐
│ Code Review Passed   │  Code Reviewer confirm
└─────────┬───────────┘
          ▼
┌─────────────────────┐
│ QA Tests Passed      │  QA Tester confirm
└─────────┬───────────┘
          ▼
┌─────────────────────┐
│ Docs Updated         │  Documentation Writer confirm
└─────────┬───────────┘
          ▼
┌─────────────────────┐
│ Build Successful     │  No compile errors
└─────────┬───────────┘
          ▼
      Ready to Release
```

### 1. Feature Freeze
- [ ] Tất cả features trong scope đã implement xong
- [ ] Không có 🔴 Critical issues open
- [ ] Task Planner đã đánh dấu tất cả tasks ✅

### 2. Quality Gate
- [ ] Code Reviewer đã approve tất cả changes
- [ ] QA Tester báo cáo: happy path ✅, edge cases ✅, regression ✅
- [ ] Không có bug severity Critical hoặc Major chưa fix

### 3. Documentation
- [ ] `docs/` đã cập nhật cho features mới
- [ ] `docs/README.md` index đã cập nhật
- [ ] Release notes đã viết

### 4. Build & Verify
- [ ] `dotnet build` không có errors
- [ ] App chạy được trên máy clean
- [ ] JSON data files tương thích (không breaking change schema)

## Release Notes Template

```markdown
# KHSX v[X.Y.Z] — [Ngày phát hành]

## Tính năng mới
- [Feature 1]: Mô tả ngắn
- [Feature 2]: Mô tả ngắn

## Sửa lỗi
- Fix [bug description]
- Fix [bug description]

## Thay đổi
- [Change description]

## Breaking Changes
- [Nếu có thay đổi JSON schema, ghi rõ migration steps]

## Tài liệu liên quan
- docs/[relevant-doc].md
```

## Data Migration (Khi JSON Schema Thay Đổi)

Vì KHSX dùng JSON files, cần chú ý khi schema thay đổi:

1. **Backward compatible change** (thêm field mới với default value):
   - Không cần migration
   - Code handle missing fields gracefully

2. **Breaking change** (rename/remove field, restructure):
   - Tạo migration logic trong `Services/`
   - Backup data files trước khi migrate
   - Test migration với data thực tế
   - Document trong release notes

## Version Numbering

```
v[Major].[Minor].[Patch]

Major: Breaking changes, major feature
Minor: New features, non-breaking
Patch: Bug fixes, minor improvements
```

## Ví Dụ Prompt

```
@qa-tester Chạy full regression test trước release v1.2.0
@documentation-writer Viết release notes cho v1.2.0 với features: [list]
```

## Post-Release

- [ ] Tag version trong source control
- [ ] Archive build output
- [ ] Thông báo users về version mới
- [ ] Monitor feedback và log bugs mới vào backlog
