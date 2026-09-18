# Kế hoạch migration WinForms sang WinUI 3 + Windows App SDK + MVVM

## 1. Kết luận và quyết định đã khóa

- Giữ nguyên `PasswordTool.Core`, định dạng `.config`/`.storage`, mã hóa, backup và API; chỉ thay presentation client.
- Thay WinForms bằng WinUI trong một lần cutover. Không phát hành phiên bản hybrid; WinForms chỉ được giữ làm behavioral reference đến khi WinUI đạt parity rồi mới xóa.
- Dùng WinUI 3, Windows App SDK stable `2.4.0`, .NET 10, `CommunityToolkit.Mvvm 8.4.2` và `Microsoft.Extensions.DependencyInjection 10.0.12`. WinUI/Windows App SDK hỗ trợ từ Windows 10 1809; project target SDK mới nhưng khai báo minimum `10.0.17763.0`. Xem [Windows App SDK](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/) và [MVVM Toolkit](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/).
- Giữ kênh phát hành ZIP + Inno Setup: unpackaged, x64, self-contained, offline, single-file EXE có extraction lúc chạy lần đầu. Cấu hình bắt buộc gồm `WindowsPackageType=None`, `WindowsAppSDKSelfContained`, `SelfContained`, `EnableMsixTooling`, `IncludeAllContentForSelfExtract` và `PublishSingleFile`. Xem [hướng dẫn unpackaged WinUI](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/unpackage-winui-app).
- UI dùng single-window `NavigationView`, phong cách Bitwarden nhưng ít dày hơn, restrained blue, theo Light/Dark/High Contrast của hệ thống.
- Vault dùng Option A — List first: danh sách là nội dung chính, không có detail pane mặc định; editor và secret viewer là flow riêng.
- Baseline hiện tại hợp lệ: solution build không warning/error và 78/78 Core tests pass. Máy hiện chưa cài WinUI `dotnet new` template, nên setup tooling là prerequisite đầu tiên.

## 2. Kiến trúc và contract mới

### Projects

- Thêm `PasswordTool.Presentation` target `net10.0`:
  - Chứa ViewModels, UI-facing models, navigation/app-flow state và các interface cho platform services.
  - Tham chiếu `PasswordTool.Core` và `CommunityToolkit.Mvvm`.
  - Không tham chiếu `Microsoft.UI.Xaml`, HWND hoặc WinRT UI types.
- Thêm `PasswordTool.WinUI` target `net10.0-windows10.0.26100.0`, minimum Windows `10.0.17763.0`:
  - Chứa XAML views, resources, WinUI adapters, composition root và executable.
  - Tham chiếu Presentation, Windows App SDK `2.4.0`, DI `10.0.12` và QRCoder `1.8.0`.
- Thêm `PasswordTool.Presentation.Tests`, dùng cùng xUnit stack với Core tests.
- Khi đạt cutover gate: xóa `PasswordTool.WinForms`, project preview WinForms và mọi solution/release reference tới executable cũ.

### MVVM boundary

- ViewModels kế thừa `ObservableObject`, dùng `[ObservableProperty]`, `[RelayCommand]` và `AsyncRelayCommand`.
- `VaultService` là singleton của session và được inject trực tiếp; không tạo một interface khổng lồ sao chép hơn 40 method của service.
- Mọi call tới `VaultService` được serialize qua một application-level operation runner và chạy ngoài UI thread. Không chạy đồng thời nhiều mutation trên service stateful.
- View code-behind chỉ được phép xử lý XAML-specific concerns: focus, `XamlRoot`, `WindowId`, responsive visual state và chuyển giá trị từ `PasswordBox` trực tiếp vào command. Không bind Master Password vào observable property và không đặt business/security logic trong code-behind.
- Không dùng Prism, ReactiveUI, generic repository hay event bus toàn cục.

### Public interfaces/types trong Presentation

- `AppRoute`: `Vault`, `SecurityCheck`, `Backup`, `HashTool`, `Settings`, `ItemEditor`.
- `AppFlowState`: `Loading`, `FirstLaunch`, `Recover`, `CreateMasterPassword`, `SetupAuthenticator`, `Unlock`, `Unlocked`.
- `VaultItemListItem`: model secret-free cho list; không có password, recovery codes hoặc TOTP secret.
- `INavigationService`: điều hướng route, back navigation và reset stack khi lock.
- `IUserDialogService`: confirm, error, sensitive TOTP prompt, backup passphrase và secret viewer.
- `IFilePickerService`: open/save path bằng `Microsoft.Windows.Storage.Pickers` gắn với `AppWindow.Id`.
- `ISensitiveClipboardService`: copy giá trị, clear sau 30 giây chỉ khi clipboard vẫn chứa chính giá trị đó, và clear khi session/window đóng.
- `ISystemLockMonitor`: phát `LockRequired` khi idle timeout, Windows lock/disconnect, suspend hoặc resume.
- Không thay đổi public Core models, persisted JSON schema, cryptographic parameters hoặc API contracts.

## 3. UX và hành vi bắt buộc

- Root window hiển thị startup/unlock flow trước; `NavigationView` chỉ xuất hiện sau khi vault mở.
- Top-level navigation: Vault, Security Check, Backup, Hash Tool, Settings; Lock được pin cuối navigation.
- First-launch flow trong cùng cửa sổ:
  - Chọn tạo vault hoặc recovery.
  - Recovery: chọn file → nhập backup passphrase → xem inspection summary → tạo Master Password mới → setup Authenticator mới.
  - Cancel trước khi hoàn tất sẽ thoát app, giống hành vi hiện tại.
- Vault page:
  - Search, type/folder filters, sort, Add Item và contextual actions cho selection.
  - `ListView` virtualized với header tương ứng; không dùng card grid hoặc dependency DataGrid ngoài.
  - Từ 1200 logical px: hiện đầy đủ Title, Username, Type, Folder, TOTP, URL, Updated.
  - Từ 900–1199 px: NavigationView compact, ẩn URL/TOTP khỏi cột chính và đưa vào overflow/secondary text.
  - Minimum window `900×600`; không hỗ trợ mobile layout.
  - `Ctrl+F`, `Ctrl+N`, Enter-to-edit, visible focus và screen-reader names phải hoạt động.
- Add/Edit Item là page riêng; Password Generator là `ContentDialog`; Trash là sub-view/filter trong Vault.
- View password/recovery codes/TOTP dùng dialog ngắn, chỉ lấy secret sau khi sensitive authorization pass; secret không được giữ trong list model hoặc long-lived ViewModel state.
- Security Check, Backup/Snapshots và Settings là page, không phải chuỗi nested modal.
- Hash Tool dùng ba tab Generate, Verify, Inspect để giảm mật độ.
- Settings nhóm login mode, inactivity timeout, sensitive-action timeout, Master Password, Authenticator và KDF upgrade.
- Dùng system font/WinUI controls, spacing vừa phải, một blue accent, status luôn kèm text/icon. Hỗ trợ system Light/Dark/High Contrast; Mica chỉ bật khi OS hỗ trợ và luôn có solid fallback.
- Global clipboard guard phải chặn copy/cut shortcuts và context actions trên input thông thường; chỉ các command copy được kiểm soát mới được đưa secret vào clipboard.
- Giữ single-instance behavior. Instance thứ hai không mở vault khác và hiển thị thông báo native an toàn.
- Lock phải clear vault key, decrypted items, Authenticator secret, sensitive authorization, clipboard-owned secret và navigation history trước khi quay về Unlock.

## 4. Các phase triển khai

### Phase 1 — Tooling và baseline

- Cài WinUI template/workload và bật Developer Mode theo [WinUI quick start](https://learn.microsoft.com/en-us/windows/apps/get-started/start-here).
- Ghi nhận screenshots, flow inventory và dirty WinForms changes hiện tại; không xóa hoặc format lại chúng.
- Scaffold hai project mới, pin stable dependencies và xác nhận blank unpackaged app build/run trên Windows 10.

### Phase 2 — Foundation

- Tạo DI composition root, App flow coordinator, navigation, dialog/file-picker adapters, theme resources và error mapping.
- Triển khai single-instance, operation serialization, async busy state và top-level exception handling không log secrets.

### Phase 3 — Authentication và lifecycle

- Port first launch, recovery, Authenticator setup và unlock.
- Port inactivity polling, Windows session/power events, explicit lock và session cleanup.
- Chứng minh WinUI mở được vault hiện có do WinForms tạo mà không migration dữ liệu.

### Phase 4 — Vault workspace

- Port list/filter/search/sort/favorites/folders/tags, add/edit/delete/trash/history và website TOTP.
- Port protected reveal/copy flows, clipboard expiry và CSV import review.
- Hoàn thiện responsive states, keyboard navigation, accessibility và empty/error/busy states.

### Phase 5 — Secondary surfaces

- Port Security Check, Backup & Recovery, snapshots, Settings, Master Password/Auth reset/KDF upgrade và Hash Tool.
- Mọi feature hiện có phải được map vào WinUI; không bỏ tính năng vì thiếu control tương đương.

### Phase 6 — Release cutover

- Đổi publish target và expected executable thành `PasswordTool.WinUI.exe`.
- Sửa Inno shortcuts, signing, checksum/manifest và qualification scripts; installer vẫn không xóa `%LocalAppData%\PasswordTool`.
- Xóa WinForms project sau khi toàn bộ acceptance gate pass.
- Cập nhật README, architecture, frontend structure và release operations theo cấu trúc/XAML/deployment mới.

## 5. Verification, acceptance và assumptions

### Automated verification

- `dotnet build PasswordTool.slnx` không warning/error.
- Tất cả Core tests hiện có vẫn pass.
- Presentation tests cover app-flow transitions, command enablement, filters/sorting, cancellation/busy behavior, error mapping và lock cleanup.
- Release pipeline/qualification tests dùng đúng WinUI executable và từ chối payload không self-contained hoặc chứa source/vault/secret files.

### Security compatibility

- Vault WinForms hiện có unlock được bằng WinUI và mutation vẫn đọc lại được.
- New vault, legacy PBKDF2 upgrade, Master Password change, TOTP trusted unlock, backup export/import/recovery, CSV import và snapshot restore giữ nguyên semantics.
- Clipboard tự clear sau 30 giây khi unchanged; không clear dữ liệu clipboard mới của người dùng.
- Session lock/disconnect/suspend/resume/idle đều đưa app về Unlock và clear session.

### UI acceptance

- Test `900×600` trở lên ở 100%, 125%, 150% và 200% scaling.
- Test Light, Dark, High Contrast, keyboard-only và Narrator/automation names.
- Test empty vault, một item, hàng trăm items, long title/URL/tags và mọi validation/error/cancel state.

### Release acceptance

- Smoke test ZIP và Inno trên Windows 10 x64 và Windows 11 x64 sạch, offline, không cài sẵn .NET hoặc Windows App SDK runtime.
- Verify install, launch, upgrade, uninstall và xác nhận vault data không bị tạo/xóa/migrate ngoài contract.

### Assumptions

- Chỉ hỗ trợ Windows x64; không thêm ARM64 trong migration này.
- Không thay đổi product scope: không cloud sync, telemetry, auto-update, account, browser extension hoặc API vault.
- Không ship từng phần; các phase chỉ là implementation gates trong cùng migration.
- Các WinForms changes chưa commit là behavioral baseline và phải được bảo toàn cho tới cutover.
