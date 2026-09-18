using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32;
using PasswordTool.Core.Models;
using PasswordTool.Core.Services;

namespace PasswordTool.WinForms;

public sealed class VaultForm : Form
{
    private readonly VaultService vaultService;
    private readonly SensitiveClipboardService clipboardService = new();
    private readonly DataGridView itemsGrid = new();
    private readonly TextBox searchTextBox = new();
    private readonly ComboBox viewFilterComboBox = new();
    private readonly ComboBox folderFilterComboBox = new();
    private readonly Label sensitiveSessionLabel = new();
    private readonly Label sensitiveSessionDescriptionLabel = new();
    private readonly Panel sensitiveSessionPanel = new();
    private readonly Label emptyStateLabel = new();
    private readonly Label selectionStatusLabel = new();
    private readonly Label itemCountLabel = new();
    private readonly Button addItemButton = new() { Text = "+  New item", Width = 126, Height = 38 };
    private readonly Button viewSecretButton = new() { Text = "View", Width = 118, Height = 38 };
    private readonly Button copyUsernameButton = new() { Text = "Copy username", Width = 142, Height = 38 };
    private readonly Button copyPasswordButton = new() { Text = "Copy password", Width = 142, Height = 38 };
    private readonly Button moreButton = new() { Text = "More...", Width = 92, Height = 38 };
    private readonly Button toolsButton = new() { Text = "Tools", Width = 96, Height = 38 };
    private readonly ContextMenuStrip moreMenu = new();
    private readonly ContextMenuStrip toolsMenu = new();
    private readonly ToolStripMenuItem editMenuItem = new("Edit item");
    private readonly ToolStripMenuItem copyTotpMenuItem = new("Copy TOTP");
    private readonly ToolStripMenuItem historyMenuItem = new("Password history");
    private readonly ToolStripMenuItem deleteMenuItem = new("Move to trash");
    private readonly System.Windows.Forms.Timer sessionTimer = new() { Interval = 1_000 };
    private IReadOnlyList<VaultItem> allItems = [];
    private TimeSpan inactivityLockTimeout;
    private bool lifecycleEventsSubscribed;
    private bool refreshingFolderFilter;
    private VaultFilterKind currentFilter = VaultFilterKind.All;
    private string sortColumnName = "Title";
    private ListSortDirection sortDirection = ListSortDirection.Ascending;

    public bool LockRequested { get; private set; }

    public VaultForm(VaultService vaultService)
    {
        this.vaultService = vaultService;
        RefreshSecurityTimeouts();
        BuildInterface();
        FormIconService.Apply(this);
        UiTheme.Apply(this);
        UiTheme.StylePrimaryButton(addItemButton);
        UiTheme.StyleMenu(moreMenu);
        UiTheme.StyleMenu(toolsMenu);
        LoadItems();
        sessionTimer.Tick += SessionTimer_Tick;
        sessionTimer.Start();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (lifecycleEventsSubscribed) return;
        SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
        SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
        lifecycleEventsSubscribed = true;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (lifecycleEventsSubscribed)
        {
            SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
            SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
            lifecycleEventsSubscribed = false;
        }
        sessionTimer.Dispose();
        clipboardService.Dispose();
        vaultService.ClearSession();
        base.OnFormClosed(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.F))
        {
            searchTextBox.Focus();
            searchTextBox.SelectAll();
            return true;
        }
        if (keyData == (Keys.Control | Keys.N))
        {
            AddItem();
            return true;
        }
        if (keyData == Keys.Enter && itemsGrid.ContainsFocus && TryGetSelectedItem(out _))
        {
            EditSelectedItem();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void BuildInterface()
    {
        Text = "Password Vault";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1280, 720);
        MinimumSize = new Size(1024, 640);
        Padding = new Padding(16);
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.Dpi;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = UiTheme.WindowBackground
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = UiTheme.Surface,
            Padding = new Padding(16, 8, 12, 8),
            Margin = new Padding(0, 0, 0, 8)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
        var titleBlock = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = UiTheme.Surface,
            Margin = Padding.Empty
        };
        titleBlock.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        titleBlock.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var title = new Label
        {
            Text = "Password Vault",
            Dock = DockStyle.Fill,
            Font = new Font(UiTheme.DefaultFont.FontFamily, 16F, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomLeft,
            AccessibleName = "Password Vault"
        };
        var subtitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Your encrypted credentials, organized and ready when you need them.",
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = UiTheme.TextSecondary,
            AutoEllipsis = true
        };
        titleBlock.Controls.Add(title, 0, 0);
        titleBlock.Controls.Add(subtitle, 0, 1);

        var headerActions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = UiTheme.Surface,
            Margin = Padding.Empty
        };
        headerActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        var lockButton = new Button
        {
            Text = "Lock",
            Dock = DockStyle.Top,
            Height = 38,
            Margin = Padding.Empty
        };
        var lockHost = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(12, 11, 0, 11),
            BackColor = UiTheme.Surface
        };
        lockHost.Controls.Add(lockButton);
        lockButton.Click += (_, _) => LockVault();
        var vaultStatus = new Label
        {
            Text = "Vault unlocked\r\nItems available; secrets remain protected",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = UiTheme.TextSecondary,
            AutoEllipsis = true,
            AccessibleName = "Vault unlocked. Secrets remain protected."
        };
        headerActions.Controls.Add(vaultStatus, 0, 0);
        headerActions.Controls.Add(lockHost, 1, 0);
        header.Controls.Add(titleBlock, 0, 0);
        header.Controls.Add(headerActions, 1, 0);

        var commandBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = UiTheme.Surface,
            Padding = new Padding(10, 8, 10, 8),
            Margin = Padding.Empty
        };
        commandBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        commandBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        var selectionActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            BackColor = UiTheme.Surface,
            Margin = Padding.Empty
        };
        addItemButton.Margin = new Padding(0, 0, 8, 0);
        copyUsernameButton.Margin = new Padding(0, 0, 8, 0);
        copyPasswordButton.Margin = new Padding(0, 0, 8, 0);
        viewSecretButton.Margin = new Padding(0, 0, 8, 0);
        moreButton.Margin = Padding.Empty;
        toolsButton.Dock = DockStyle.Top;
        toolsButton.Height = 38;
        toolsButton.Margin = Padding.Empty;
        var toolsHost = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(14, 4, 0, 4),
            BackColor = UiTheme.Surface
        };
        toolsHost.Controls.Add(toolsButton);
        addItemButton.Click += (_, _) => AddItem();
        viewSecretButton.Click += (_, _) => ViewSelectedSecret();
        copyUsernameButton.Click += (_, _) => CopySelectedUsername();
        copyPasswordButton.Click += (_, _) => CopySelectedPassword();
        moreButton.Click += (_, _) => moreMenu.Show(moreButton, new Point(0, moreButton.Height));
        toolsButton.Click += (_, _) => toolsMenu.Show(toolsButton, new Point(0, toolsButton.Height));
        selectionActions.Controls.AddRange([addItemButton, copyUsernameButton, copyPasswordButton, viewSecretButton, moreButton]);
        commandBar.Controls.Add(selectionActions, 0, 0);
        commandBar.Controls.Add(toolsHost, 1, 0);

        editMenuItem.Click += (_, _) => EditSelectedItem();
        copyTotpMenuItem.Click += (_, _) => CopySelectedTotp();
        historyMenuItem.Click += (_, _) => ViewPasswordHistory();
        deleteMenuItem.Click += (_, _) => DeleteSelectedItem();
        deleteMenuItem.ForeColor = UiTheme.Danger;
        moreMenu.Items.AddRange([editMenuItem, copyTotpMenuItem, historyMenuItem, new ToolStripSeparator(), deleteMenuItem]);
        toolsMenu.Items.Add("Security Check", null, (_, _) => OpenSecurityCheck());
        toolsMenu.Items.Add("Backup & Recovery", null, (_, _) => OpenBackupRecoveryCenter());
        toolsMenu.Items.Add("Import CSV", null, (_, _) => ImportCsv());
        toolsMenu.Items.Add(new ToolStripSeparator());
        toolsMenu.Items.Add("Trash", null, (_, _) => OpenTrash());
        toolsMenu.Items.Add("Settings", null, (_, _) => OpenSettings());
        toolsMenu.Items.Add("Hash Tool", null, (_, _) => OpenHashTool());

        var filterRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = UiTheme.WindowBackground,
            Padding = new Padding(0, 8, 0, 8),
            Margin = Padding.Empty
        };
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
        var filters = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = UiTheme.WindowBackground,
            Margin = Padding.Empty
        };
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 164));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 184));
        filters.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        searchTextBox.AutoSize = true;
        searchTextBox.Dock = DockStyle.Top;
        searchTextBox.PlaceholderText = "Search title, username, URL, folder, or tag";
        searchTextBox.AccessibleName = "Search vault";
        searchTextBox.Margin = Padding.Empty;
        searchTextBox.TextChanged += (_, _) => ApplyFilters();

        viewFilterComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        viewFilterComboBox.DisplayMember = nameof(ViewFilterOption.DisplayName);
        viewFilterComboBox.Dock = DockStyle.Top;
        viewFilterComboBox.Margin = Padding.Empty;
        viewFilterComboBox.AccessibleName = "Item type filter";
        viewFilterComboBox.Items.AddRange(
        [
            new ViewFilterOption("All items", VaultFilterKind.All),
            new ViewFilterOption("Favorites", VaultFilterKind.Favorites),
            new ViewFilterOption("Passwords", VaultFilterKind.Passwords),
            new ViewFilterOption("Recovery codes", VaultFilterKind.RecoveryCodes)
        ]);
        viewFilterComboBox.SelectedIndex = 0;
        viewFilterComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (viewFilterComboBox.SelectedItem is not ViewFilterOption option) return;
            currentFilter = option.Filter;
            ApplyFilters();
        };

        folderFilterComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        folderFilterComboBox.DisplayMember = nameof(FolderFilterOption.DisplayName);
        folderFilterComboBox.Dock = DockStyle.Top;
        folderFilterComboBox.Margin = Padding.Empty;
        folderFilterComboBox.AccessibleName = "Folder filter";
        folderFilterComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (!refreshingFolderFilter) ApplyFilters();
        };
        filters.Controls.Add(CreateFilterHost(searchTextBox, rightPadding: 12), 0, 0);
        filters.Controls.Add(CreateFilterHost(viewFilterComboBox, rightPadding: 12), 1, 0);
        filters.Controls.Add(CreateFilterHost(folderFilterComboBox, rightPadding: 0), 2, 0);

        sensitiveSessionPanel.Dock = DockStyle.Fill;
        sensitiveSessionPanel.Padding = new Padding(14, 8, 14, 6);
        sensitiveSessionPanel.Margin = new Padding(12, 0, 0, 0);
        sensitiveSessionPanel.BorderStyle = BorderStyle.FixedSingle;
        var sensitiveLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        sensitiveLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        sensitiveLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sensitiveSessionLabel.Dock = DockStyle.Fill;
        sensitiveSessionLabel.Font = new Font(UiTheme.DefaultFont, FontStyle.Bold);
        sensitiveSessionDescriptionLabel.Dock = DockStyle.Fill;
        sensitiveSessionDescriptionLabel.AutoEllipsis = true;
        sensitiveLayout.Controls.Add(sensitiveSessionLabel, 0, 0);
        sensitiveLayout.Controls.Add(sensitiveSessionDescriptionLabel, 0, 1);
        sensitiveSessionPanel.Controls.Add(sensitiveLayout);
        filterRow.Controls.Add(filters, 0, 0);
        filterRow.Controls.Add(sensitiveSessionPanel, 1, 0);

        ConfigureGrid();
        var gridHost = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface, Padding = new Padding(1) };
        emptyStateLabel.Dock = DockStyle.Fill;
        emptyStateLabel.TextAlign = ContentAlignment.MiddleCenter;
        emptyStateLabel.Font = new Font(UiTheme.DefaultFont.FontFamily, 11F);
        emptyStateLabel.ForeColor = UiTheme.TextSecondary;
        emptyStateLabel.BackColor = UiTheme.Surface;
        emptyStateLabel.Visible = false;
        gridHost.Controls.Add(itemsGrid);
        gridHost.Controls.Add(emptyStateLabel);

        var statusBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = UiTheme.WindowBackground,
            Padding = new Padding(4, 6, 4, 0)
        };
        statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 390));
        selectionStatusLabel.Dock = DockStyle.Fill;
        selectionStatusLabel.ForeColor = UiTheme.TextSecondary;
        itemCountLabel.Dock = DockStyle.Fill;
        itemCountLabel.ForeColor = UiTheme.TextSecondary;
        itemCountLabel.TextAlign = ContentAlignment.TopRight;
        statusBar.Controls.Add(selectionStatusLabel, 0, 0);
        statusBar.Controls.Add(itemCountLabel, 1, 0);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(commandBar, 0, 1);
        root.Controls.Add(filterRow, 0, 2);
        root.Controls.Add(gridHost, 0, 3);
        root.Controls.Add(statusBar, 0, 4);
        Controls.Add(root);
        UpdateSensitiveSessionLabel();
    }

    private void ConfigureGrid()
    {
        itemsGrid.Dock = DockStyle.Fill;
        itemsGrid.AllowUserToAddRows = false;
        itemsGrid.AllowUserToDeleteRows = false;
        itemsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        itemsGrid.BackgroundColor = SystemColors.Window;
        itemsGrid.ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable;
        itemsGrid.MultiSelect = false;
        itemsGrid.ReadOnly = true;
        itemsGrid.RowHeadersVisible = false;
        itemsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        itemsGrid.SelectionChanged += (_, _) => UpdateButtonState();
        itemsGrid.CellDoubleClick += (_, eventArgs) =>
        {
            if (eventArgs.RowIndex >= 0) EditSelectedItem();
        };
        itemsGrid.ColumnHeaderMouseClick += ItemsGrid_ColumnHeaderMouseClick;
        AddSortableColumn("Favorite", "\u2605", 6);
        AddSortableColumn("Title", "Title", 21);
        AddSortableColumn("Username", "Username", 20);
        AddSortableColumn("Type", "Type", 12);
        AddSortableColumn("Folder", "Folder", 12);
        AddSortableColumn("Totp", "TOTP", 11);
        AddSortableColumn("Url", "URL", 20);
        AddSortableColumn("UpdatedAt", "Updated", 14, typeof(DateTime), "g");
        UiTheme.StyleGrid(itemsGrid);
    }

    private static Panel CreateFilterHost(Control control, int rightPadding)
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(0, 16, rightPadding, 0),
            BackColor = UiTheme.WindowBackground
        };
        host.Controls.Add(control);
        return host;
    }

    private void LoadItems()
    {
        var selectedId = TryGetSelectedItem(out var selected) ? selected.Id : (Guid?)null;
        allItems = vaultService.GetItems();
        RefreshFolderFilter();
        ApplyFilters(selectedId);
    }

    private void ApplyFilters(Guid? preferredSelectionId = null)
    {
        preferredSelectionId ??= TryGetSelectedItem(out var selected) ? selected.Id : null;
        var search = searchTextBox.Text.Trim();
        var folder = folderFilterComboBox.SelectedItem as FolderFilterOption;
        var filtered = allItems
            .Where(MatchesActiveFilter)
            .Where(item => folder is null || folder.IsAll
                || folder.IsNoFolder && string.IsNullOrWhiteSpace(item.Folder)
                || folder.Folder is not null && string.Equals(item.Folder, folder.Folder, StringComparison.CurrentCultureIgnoreCase))
            .Where(item => search.Length == 0 || MatchesSearch(item, search))
            .ToList();
        filtered.Sort(CompareItems);

        itemsGrid.Rows.Clear();
        foreach (var item in filtered)
        {
            var rowIndex = itemsGrid.Rows.Add(
                item.IsFavorite ? "\u2605" : string.Empty,
                item.Title,
                item.Username,
                GetTypeDisplayName(item),
                item.Folder,
                item.HasTotp ? "Configured" : "\u2014",
                item.HideUrl ? "Hidden" : item.Url,
                item.UpdatedAt.ToLocalTime().DateTime);
            itemsGrid.Rows[rowIndex].Tag = new VaultItemRowTag(item.Id, item.Type, item.HasTotp);
        }

        itemsGrid.ClearSelection();
        var rowToSelect = itemsGrid.Rows.Cast<DataGridViewRow>()
            .FirstOrDefault(row => row.Tag is VaultItemRowTag tag && tag.Id == preferredSelectionId)
            ?? itemsGrid.Rows.Cast<DataGridViewRow>().FirstOrDefault();
        if (rowToSelect is not null)
        {
            rowToSelect.Selected = true;
            itemsGrid.CurrentCell = rowToSelect.Cells["Title"];
        }

        var hasRows = filtered.Count > 0;
        itemsGrid.Visible = hasRows;
        emptyStateLabel.Text = allItems.Count == 0
            ? "No vault items yet.\r\nSelect New item to add your first credential."
            : "No items match your current search and filters.";
        emptyStateLabel.Visible = !hasRows;
        if (!hasRows) emptyStateLabel.BringToFront();
        itemCountLabel.Text = $"Clipboard clears after 30 seconds     {filtered.Count} of {allItems.Count} items";
        UpdateSortGlyph();
        UpdateButtonState();
    }

    private bool MatchesActiveFilter(VaultItem item) => currentFilter switch
    {
        VaultFilterKind.Favorites => item.IsFavorite,
        VaultFilterKind.Passwords => item.Type == VaultItemType.Password,
        VaultFilterKind.RecoveryCodes => item.Type == VaultItemType.RecoveryCodes,
        _ => true
    };

    private int CompareItems(VaultItem left, VaultItem right)
    {
        var comparison = sortColumnName switch
        {
            "Favorite" => left.IsFavorite.CompareTo(right.IsFavorite),
            "Username" => CompareText(left.Username, right.Username),
            "Type" => CompareText(GetTypeDisplayName(left), GetTypeDisplayName(right)),
            "Folder" => CompareText(left.Folder, right.Folder),
            "Totp" => left.HasTotp.CompareTo(right.HasTotp),
            "Url" => CompareText(left.HideUrl ? "Hidden" : left.Url, right.HideUrl ? "Hidden" : right.Url),
            "UpdatedAt" => left.UpdatedAt.CompareTo(right.UpdatedAt),
            _ => CompareText(left.Title, right.Title)
        };
        return sortDirection == ListSortDirection.Ascending ? comparison : -comparison;
    }

    private static int CompareText(string? left, string? right) =>
        StringComparer.CurrentCultureIgnoreCase.Compare(left ?? string.Empty, right ?? string.Empty);

    private void ItemsGrid_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        var column = itemsGrid.Columns[e.ColumnIndex];
        if (sortColumnName == column.Name)
        {
            sortDirection = sortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }
        else
        {
            sortColumnName = column.Name;
            sortDirection = ListSortDirection.Ascending;
        }
        ApplyFilters();
    }

    private void UpdateSortGlyph()
    {
        foreach (DataGridViewColumn column in itemsGrid.Columns)
        {
            column.HeaderCell.SortGlyphDirection = column.Name == sortColumnName
                ? sortDirection == ListSortDirection.Ascending ? SortOrder.Ascending : SortOrder.Descending
                : SortOrder.None;
        }
    }

    private void AddSortableColumn(string name, string header, float fillWeight, Type? valueType = null, string? format = null)
    {
        var column = new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = header,
            FillWeight = fillWeight,
            SortMode = DataGridViewColumnSortMode.Programmatic,
            ValueType = valueType
        };
        if (format is not null) column.DefaultCellStyle.Format = format;
        itemsGrid.Columns.Add(column);
    }

    private void RefreshFolderFilter()
    {
        var current = folderFilterComboBox.SelectedItem as FolderFilterOption;
        var options = new List<FolderFilterOption>
        {
            new("All folders", null, IsAll: true),
            new("No folder", null, IsNoFolder: true)
        };
        options.AddRange(allItems
            .Select(item => item.Folder?.Trim())
            .Where(folder => !string.IsNullOrWhiteSpace(folder))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(folder => folder, StringComparer.CurrentCultureIgnoreCase)
            .Select(folder => new FolderFilterOption(folder!, folder)));

        refreshingFolderFilter = true;
        folderFilterComboBox.Items.Clear();
        folderFilterComboBox.Items.AddRange([.. options]);
        var preserved = options.FirstOrDefault(option =>
            option.IsAll == current?.IsAll
            && option.IsNoFolder == current?.IsNoFolder
            && string.Equals(option.Folder, current?.Folder, StringComparison.CurrentCultureIgnoreCase));
        folderFilterComboBox.SelectedItem = preserved ?? options[0];
        refreshingFolderFilter = false;
    }

    private static bool MatchesSearch(VaultItem item, string search) =>
        Contains(item.Title, search) || Contains(item.Username, search) || Contains(item.Url, search)
        || Contains(item.Folder, search) || item.Tags.Any(tag => Contains(tag, search));

    private static bool Contains(string? value, string search) => value?.Contains(search, StringComparison.CurrentCultureIgnoreCase) == true;

    private static string GetTypeDisplayName(VaultItem item) =>
        item.Type == VaultItemType.Password ? "Password" : "Recovery codes";

    private void AddItem()
    {
        using var editor = new VaultItemEditorForm();
        if (editor.ShowDialog(this) != DialogResult.OK || editor.DeleteRequested) return;
        RunVaultAction(() => { vaultService.AddItem(editor.Item); LoadItems(); });
    }

    private void EditSelectedItem()
    {
        if (TryGetSelectedItem(out var selected)) EditItem(selected.Id);
    }

    private bool EditItem(Guid id)
    {
        var code = RequestSensitiveAuthorization("Edit Vault Item", "Confirm with the PasswordTool Authenticator code before editing this item.");
        if (code is null) return false;
        var completed = false;
        RunVaultAction(() =>
        {
            var item = vaultService.GetItemForEditing(id, code);
            using var editor = new VaultItemEditorForm(item);
            if (editor.ShowDialog(this) != DialogResult.OK) return;
            if (editor.DeleteRequested) vaultService.DeleteItem(id);
            else vaultService.UpdateItem(editor.Item);
            LoadItems();
            completed = true;
        });
        return completed;
    }

    private void DeleteSelectedItem()
    {
        if (!TryGetSelectedItem(out var selected)) return;
        if (MessageBox.Show("Delete the selected vault item?", "PasswordTool", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        RunVaultAction(() => { vaultService.DeleteItem(selected.Id); LoadItems(); });
    }

    private void ViewSelectedSecret()
    {
        if (!TryGetSelectedItem(out var selected)) return;
        var code = RequestSensitiveAuthorization("View Secret", "Confirm with the PasswordTool Authenticator code before viewing this secret.");
        if (code is null) return;
        RunVaultAction(() =>
        {
            if (selected.Type == VaultItemType.Password) ShowTextDialog("Password", vaultService.GetPassword(selected.Id, code));
            else ShowTextDialog("Recovery Codes", string.Join(Environment.NewLine, vaultService.GetRecoveryCodes(selected.Id, code)), multiline: true);
        });
    }

    private void CopySelectedUsername()
    {
        if (!TryGetSelectedItem(out var selected)) return;
        RunVaultAction(() => CopyWithNotice(vaultService.GetUsername(selected.Id), "Username"));
    }

    private void CopySelectedPassword()
    {
        if (!TryGetSelectedItem(out var selected) || selected.Type != VaultItemType.Password) return;
        var code = RequestSensitiveAuthorization("Copy Password", "Confirm with the PasswordTool Authenticator code before copying this password.");
        if (code is null) return;
        RunVaultAction(() => CopyWithNotice(vaultService.GetPassword(selected.Id, code), "Password"));
    }

    private void CopySelectedTotp()
    {
        if (!TryGetSelectedItem(out var selected) || !selected.HasTotp) return;
        var code = RequestSensitiveAuthorization("Copy Website TOTP", "Confirm with the PasswordTool Authenticator code before copying this website code.");
        if (code is null) return;
        RunVaultAction(() =>
        {
            var result = vaultService.GetWebsiteTotpCode(selected.Id, code);
            CopyWithNotice(result.Code, $"Website TOTP code ({result.SecondsRemaining}s remaining)");
        });
    }

    private void CopyWithNotice(string value, string label)
    {
        clipboardService.Copy(value);
        MessageBox.Show($"{label} copied. PasswordTool will clear it after 30 seconds if the clipboard is unchanged.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ImportCsv()
    {
        if (MessageBox.Show("CSV exports contain plaintext passwords. Import only a file you trust, then securely remove it when finished.", "Import CSV", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
        using var dialog = new OpenFileDialog { Title = "Import Password CSV", Filter = "CSV files (*.csv)|*.csv", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string csv;
        try
        {
            var info = new FileInfo(dialog.FileName);
            if (info.Length > VaultCsvImportService.MaxCsvCharacters) throw new InvalidDataException("The selected CSV exceeds the 10 MB limit.");
            csv = File.ReadAllText(dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ShowError(ex.Message);
            return;
        }
        var code = RequestSensitiveAuthorization("Import CSV", "Confirm with the PasswordTool Authenticator code before reviewing plaintext imported passwords.");
        if (code is null) return;
        RunVaultAction(() =>
        {
            var plan = vaultService.PreviewCsvImport(csv, code);
            using var review = new VaultCsvImportReviewForm(plan);
            if (review.ShowDialog(this) != DialogResult.OK) return;
            var count = vaultService.ImportCsv(csv, string.Empty);
            LoadItems();
            MessageBox.Show($"Imported {count} new account(s). Existing matching accounts were not overwritten.", "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private string? RequestSensitiveAuthorization(string title, string prompt)
    {
        if (!vaultService.IsGoogleAuthenticatorConfigured || vaultService.IsSensitiveSessionActive) return string.Empty;
        using var form = new VerifyTotpForm(title, prompt, vaultService.VerifyTotpForSensitiveAction);
        return form.ShowDialog(this) == DialogResult.OK ? form.Code : null;
    }

    private void UpdateSensitiveSessionLabel()
    {
        if (vaultService.SensitiveSessionExpiresAt is not { } expiresAt)
        {
            sensitiveSessionLabel.Text = "Sensitive actions locked";
            sensitiveSessionDescriptionLabel.Text = "Copy passwords, view, or edit asks for Authenticator.";
            sensitiveSessionPanel.BackColor = UiTheme.WarningBackground;
            sensitiveSessionLabel.ForeColor = UiTheme.WarningText;
            sensitiveSessionDescriptionLabel.ForeColor = UiTheme.WarningText;
            return;
        }
        var remaining = expiresAt - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            sensitiveSessionLabel.Text = "Sensitive actions locked";
            sensitiveSessionDescriptionLabel.Text = "Copy passwords, view, or edit asks for Authenticator.";
            sensitiveSessionPanel.BackColor = UiTheme.WarningBackground;
            sensitiveSessionLabel.ForeColor = UiTheme.WarningText;
            sensitiveSessionDescriptionLabel.ForeColor = UiTheme.WarningText;
            return;
        }
        sensitiveSessionLabel.Text = $"Sensitive actions unlocked  {remaining.Minutes}:{remaining.Seconds:D2}";
        sensitiveSessionDescriptionLabel.Text = "Secret actions are available until the timer expires.";
        sensitiveSessionPanel.BackColor = UiTheme.SuccessBackground;
        sensitiveSessionLabel.ForeColor = UiTheme.SuccessText;
        sensitiveSessionDescriptionLabel.ForeColor = UiTheme.SuccessText;
    }

    private void OpenSettings()
    {
        using var settings = new VaultSettingsForm(vaultService);
        settings.ShowDialog(this);
        if (settings.RequiresVaultLock)
        {
            LockVault();
            return;
        }
        RefreshSecurityTimeouts();
    }

    private void OpenBackupRecoveryCenter()
    {
        using var center = new BackupRecoveryCenterForm(vaultService, LoadItems);
        center.ShowDialog(this);
        if (center.RequiresVaultLock) LockVault();
    }

    private void ViewPasswordHistory()
    {
        if (!TryGetSelectedItem(out var selected) || selected.Type != VaultItemType.Password) return;
        var code = RequestSensitiveAuthorization("Password History", "Confirm before viewing previous passwords.");
        if (code is null) return;
        RunVaultAction(() =>
        {
            var history = vaultService.GetPasswordHistory(selected.Id, code);
            var text = history.Count == 0
                ? "No previous passwords are stored for this item."
                : string.Join(Environment.NewLine + Environment.NewLine,
                    history.Select(entry => $"{entry.ChangedAt.ToLocalTime():g}{Environment.NewLine}{entry.Password}"));
            ShowTextDialog("Password History (latest 10)", text, multiline: true);
        });
    }

    private void OpenSecurityCheck()
    {
        var code = RequestSensitiveAuthorization("Local Security Check", "Confirm before checking encrypted passwords locally.");
        if (code is null) return;
        RunVaultAction(() =>
        {
            while (true)
            {
                using var form = new VaultSecurityCheckForm(vaultService.GetSecurityFindings(code));
                if (form.ShowDialog(this) != DialogResult.OK || form.SelectedItemId is not { } itemId) return;
                if (!EditItem(itemId)) return;
                code = RequestSensitiveAuthorization("Local Security Check", "Confirm before refreshing the local Security Check.");
                if (code is null) return;
            }
        });
    }

    private void OpenTrash()
    {
        using var dialog = new Form
        {
            Text = "Trash - automatically removed after 30 days",
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(700, 420),
            Padding = new Padding(16)
        };
        FormIconService.Apply(dialog);
        var list = new ListBox { Dock = DockStyle.Fill };
        var restore = new Button { Text = "Restore", Width = 100 };
        var permanentDelete = new Button { Text = "Delete permanently", Width = 150 };
        var close = new Button { Text = "Close", Width = 100, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(close);
        buttons.Controls.Add(permanentDelete);
        buttons.Controls.Add(restore);
        dialog.Controls.Add(list);
        dialog.Controls.Add(buttons);
        UiTheme.Apply(dialog);
        UiTheme.StylePrimaryButton(restore);
        UiTheme.StyleDangerButton(permanentDelete);

        void Reload()
        {
            list.Items.Clear();
            foreach (var item in vaultService.GetDeletedItems()) list.Items.Add(new TrashRow(item.Id, item.Title, item.DeletedAt));
        }
        restore.Click += (_, _) =>
        {
            if (list.SelectedItem is not TrashRow row) return;
            RunVaultAction(() => { vaultService.RestoreDeletedItem(row.Id); Reload(); LoadItems(); });
        };
        permanentDelete.Click += (_, _) =>
        {
            if (list.SelectedItem is not TrashRow row) return;
            if (MessageBox.Show("This cannot be undone. Delete permanently?", "PasswordTool", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            var code = RequestSensitiveAuthorization("Delete Permanently", "Confirm before permanently deleting this item.");
            if (code is null) return;
            RunVaultAction(() => { vaultService.PermanentlyDeleteItem(row.Id, code); Reload(); });
        };
        Reload();
        dialog.ShowDialog(this);
    }

    private void SessionTimer_Tick(object? sender, EventArgs e)
    {
        UpdateSensitiveSessionLabel();
        if (GetSystemIdleTime() >= inactivityLockTimeout) LockVault();
    }

    private void RefreshSecurityTimeouts()
    {
        inactivityLockTimeout = TimeSpan.FromMinutes(vaultService.SecuritySettings.InactivityLockTimeoutMinutes);
    }

    private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock
            or SessionSwitchReason.ConsoleDisconnect
            or SessionSwitchReason.RemoteDisconnect)
        {
            RequestLifecycleLock();
        }
    }

    private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode is PowerModes.Suspend or PowerModes.Resume)
        {
            RequestLifecycleLock();
        }
    }

    private void RequestLifecycleLock()
    {
        if (IsDisposed || Disposing || LockRequested) return;
        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action(RequestLifecycleLock));
            }
            catch (InvalidOperationException)
            {
            }
            return;
        }

        LockVault();
    }

    private static TimeSpan GetSystemIdleTime()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;
        var elapsed = unchecked((uint)Environment.TickCount - info.TickCount);
        return TimeSpan.FromMilliseconds(elapsed);
    }

    private void OpenHashTool()
    {
        using var form = new PasswordHashToolForm();
        form.ShowDialog(this);
    }

    private void LockVault()
    {
        if (LockRequested) return;
        LockRequested = true;
        Close();
    }

    private void ShowTextDialog(string title, string value, bool multiline = false)
    {
        using var dialog = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ClientSize = multiline ? new Size(580, 420) : new Size(540, 150),
            Padding = new Padding(16)
        };
        FormIconService.Apply(dialog);
        var textBox = new TextBox { Text = value, Dock = DockStyle.Fill, ReadOnly = true, Multiline = multiline, ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None, ShortcutsEnabled = false };
        var close = new Button { Text = "Close", Dock = DockStyle.Bottom, Height = 34, DialogResult = DialogResult.OK };
        dialog.Controls.Add(textBox);
        dialog.Controls.Add(close);
        UiTheme.Apply(dialog);
        UiTheme.StylePrimaryButton(close);
        dialog.AcceptButton = close;
        dialog.ShowDialog(this);
    }

    private bool TryGetSelectedItem(out VaultItemRowTag item)
    {
        item = null!;
        if (itemsGrid.CurrentRow?.Tag is not VaultItemRowTag selected) return false;
        item = selected;
        return true;
    }

    private void UpdateButtonState()
    {
        var selected = TryGetSelectedItem(out var item);
        viewSecretButton.Enabled = selected;
        copyUsernameButton.Enabled = selected;
        copyPasswordButton.Enabled = selected && item.Type == VaultItemType.Password;
        moreButton.Enabled = selected;
        editMenuItem.Enabled = selected;
        deleteMenuItem.Enabled = selected;
        copyTotpMenuItem.Enabled = selected && item.HasTotp;
        historyMenuItem.Enabled = selected && item.Type == VaultItemType.Password;
        viewSecretButton.Text = selected && item.Type == VaultItemType.RecoveryCodes ? "View Codes" : "View Password";
        selectionStatusLabel.Text = selected
            ? "1 item selected  -  Press Enter to edit"
            : "No item selected";
    }

    private static void RunVaultAction(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException
            or IOException or InvalidDataException or CryptographicException or ExternalException)
        {
            ShowError(ex.Message);
        }
    }

    private static void ShowError(string message) => MessageBox.Show(message, "PasswordTool", MessageBoxButtons.OK, MessageBoxIcon.Error);

    private sealed record VaultItemRowTag(Guid Id, VaultItemType Type, bool HasTotp);

    private sealed record FolderFilterOption(string DisplayName, string? Folder, bool IsAll = false, bool IsNoFolder = false);

    private sealed record ViewFilterOption(string DisplayName, VaultFilterKind Filter);

    private enum VaultFilterKind
    {
        All,
        Favorites,
        Passwords,
        RecoveryCodes
    }

    private sealed record TrashRow(Guid Id, string Title, DateTimeOffset? DeletedAt)
    {
        public override string ToString() => $"{Title} — deleted {DeletedAt?.ToLocalTime():g}";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint TickCount;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);
}
