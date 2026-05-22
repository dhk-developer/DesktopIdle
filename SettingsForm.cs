namespace DesktopIdle;

internal sealed class SettingsForm : Form
{
    private readonly NumericUpDown _ambientDelay;
    private readonly ListBox _activityList;
    private readonly ListBox _windowList;
    private readonly Button _startupButton;
    private readonly Label _startupStatus;
    private readonly Func<string> _getActiveProcessName;

    public AppSettings Settings { get; private set; }

    public SettingsForm(AppSettings settings, Func<string> getActiveProcessName)
    {
        Settings = CloneSettings(settings);
        _getActiveProcessName = getActiveProcessName;

        Text = "Desktop Idle Settings";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(680, 660);
        Size = new Size(760, 720);
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 1,
            RowCount = 9
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "Ambient mode starts after this many seconds without keyboard or mouse input:",
            AutoSize = true,
            Dock = DockStyle.Fill
        });

        var delayPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        _ambientDelay = new NumericUpDown
        {
            Minimum = 5,
            Maximum = 86400,
            Value = Math.Max(5, Settings.AmbientDelaySeconds),
            Width = 100
        };
        delayPanel.Controls.Add(_ambientDelay);
        delayPanel.Controls.Add(new Label { Text = "seconds", AutoSize = true, Padding = new Padding(8, 5, 0, 0) });
        root.Controls.Add(delayPanel);

        root.Controls.Add(BuildListSection(
            "Activity exclusions",
            "Ambient mode will not activate while one of these apps is the active window.",
            out _activityList,
            AddProcessFromFile,
            AddActiveProcess,
            RemoveSelected));

        root.Controls.Add(new Label
        {
            Text = "Window excluded processes",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 10, 0, 0),
            Font = new Font(Font, FontStyle.Bold)
        });

        root.Controls.Add(BuildListSection(
            string.Empty,
            "These apps will not be minimised when ambient mode shows the desktop.",
            out _windowList,
            AddProcessFromFile,
            AddActiveProcess,
            RemoveSelected));

        var options = new Label
        {
            Text = "Ambient mode is automatic only. Use the tray icon to open settings or exit the app.",
            AutoSize = true,
            Padding = new Padding(0, 10, 0, 0)
        };
        root.Controls.Add(options);

        var startupPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 4, 0, 4)
        };
        _startupButton = new Button { Width = 190 };
        _startupStatus = new Label { AutoSize = true, Padding = new Padding(8, 6, 0, 0) };
        _startupButton.Click += (_, _) => ToggleStartup();
        startupPanel.Controls.Add(_startupButton);
        startupPanel.Controls.Add(_startupStatus);
        root.Controls.Add(startupPanel);
        RefreshStartupButton();

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        var save = new Button { Text = "Save", Width = 100, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Width = 100, DialogResult = DialogResult.Cancel };
        var defaults = new Button { Text = "Restore defaults", Width = 130 };
        save.Click += (_, _) => SaveFromUi();
        defaults.Click += (_, _) => RestoreDefaults();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(defaults);
        root.Controls.Add(buttons);

        AcceptButton = save;
        CancelButton = cancel;

        LoadLists();
    }

    private Control BuildListSection(string title, string description, out ListBox listBox, Action<ListBox> addFile, Action<ListBox> addActive, Action<ListBox> remove)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = string.IsNullOrEmpty(title) ? 3 : 4,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };

        if (!string.IsNullOrEmpty(title))
        {
            panel.Controls.Add(new Label
            {
                Text = title,
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 10, 0, 0)
            });
        }

        panel.Controls.Add(new Label { Text = description, AutoSize = true, Dock = DockStyle.Fill });

        listBox = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            HorizontalScrollbar = true
        };
        panel.Controls.Add(listBox);

        // Do not capture the out parameter directly in lambdas.
        // C# forbids capturing ref/out/in parameters, so copy it to a normal local first.
        var targetListBox = listBox;

        var buttonRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        var addFileButton = new Button { Text = "Add app from file...", Width = 150 };
        var addActiveButton = new Button { Text = "Add active app", Width = 125 };
        var removeButton = new Button { Text = "Remove selected", Width = 130 };
        addFileButton.Click += (_, _) => addFile(targetListBox);
        addActiveButton.Click += (_, _) => addActive(targetListBox);
        removeButton.Click += (_, _) => remove(targetListBox);
        buttonRow.Controls.Add(addFileButton);
        buttonRow.Controls.Add(addActiveButton);
        buttonRow.Controls.Add(removeButton);
        panel.Controls.Add(buttonRow);

        return panel;
    }

    private void LoadLists()
    {
        FillList(_activityList, Settings.ExcludedActivityProcesses);
        FillList(_windowList, Settings.WindowExcludedProcesses);
    }

    private static void FillList(ListBox listBox, IEnumerable<string> values)
    {
        listBox.Items.Clear();
        foreach (var item in AppSettings.NormaliseDistinct(values)) listBox.Items.Add(item);
    }

    private void AddProcessFromFile(ListBox listBox)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose an executable to exclude",
            Filter = "Applications (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            AddProcessName(listBox, dialog.FileName);
        }
    }

    private void AddActiveProcess(ListBox listBox)
    {
        var active = _getActiveProcessName();
        if (string.IsNullOrWhiteSpace(active))
        {
            MessageBox.Show(this, "Could not read the active process.", "Desktop Idle Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        AddProcessName(listBox, active);
    }

    private void AddProcessName(ListBox listBox, string processName)
    {
        processName = AppSettings.NormaliseProcessName(processName);
        if (string.IsNullOrWhiteSpace(processName)) return;

        foreach (var existing in listBox.Items.Cast<string>())
        {
            if (existing.Equals(processName, StringComparison.OrdinalIgnoreCase)) return;
        }

        listBox.Items.Add(processName);
    }

    private void RemoveSelected(ListBox listBox)
    {
        if (listBox.SelectedIndex < 0)
        {
            MessageBox.Show(this, "Select a process first.", "Desktop Idle Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        listBox.Items.RemoveAt(listBox.SelectedIndex);
    }

    private void ToggleStartup()
    {
        try
        {
            StartupManager.SetEnabled(!StartupManager.IsEnabled());
            RefreshStartupButton();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Could not update the Windows startup setting.\n\n" + ex.Message,
                "Desktop Idle Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void RefreshStartupButton()
    {
        var enabled = StartupManager.IsEnabled();
        _startupButton.Text = enabled ? "Disable start with Windows" : "Enable start with Windows";
        _startupStatus.Text = enabled
            ? "DesktopIdle will start when you sign in."
            : "DesktopIdle will not start automatically.";
    }

    private void SaveFromUi()
    {
        Settings.AmbientDelaySeconds = (int)_ambientDelay.Value;
        Settings.ExcludedActivityProcesses = _activityList.Items.Cast<string>().ToList();
        Settings.WindowExcludedProcesses = _windowList.Items.Cast<string>().ToList();
        Settings.Sanitise();
    }

    private void RestoreDefaults()
    {
        Settings.AmbientDelaySeconds = 120;
        Settings.ExcludedActivityProcesses = AppSettings.DefaultActivityExclusions();
        Settings.WindowExcludedProcesses = AppSettings.DefaultWindowExclusions();
        Settings.Sanitise();
        _ambientDelay.Value = Settings.AmbientDelaySeconds;
        LoadLists();
    }

    private static AppSettings CloneSettings(AppSettings source)
    {
        return new AppSettings
        {
            AmbientDelaySeconds = source.AmbientDelaySeconds,
            InputFreshThresholdMs = source.InputFreshThresholdMs,
            AmbientInputGraceMs = source.AmbientInputGraceMs,
            EnableTaskbarAutoHideInAmbient = source.EnableTaskbarAutoHideInAmbient,
            SuppressWhenFullscreen = source.SuppressWhenFullscreen,
            RestoreMaxWindows = source.RestoreMaxWindows,
            YouTubeOnlySuppressWhenPlaying = source.YouTubeOnlySuppressWhenPlaying,
            YouTubeFallbackAnyBrowserMedia = source.YouTubeFallbackAnyBrowserMedia,
            MediaSessionCheckIntervalMs = source.MediaSessionCheckIntervalMs,
            BrowserProcesses = source.BrowserProcesses.ToList(),
            ExcludedActivityProcesses = source.ExcludedActivityProcesses.ToList(),
            VideoPlayerProcesses = source.VideoPlayerProcesses.ToList(),
            VideoTitlePatterns = source.VideoTitlePatterns.ToList(),
            VideoTitleExceptions = source.VideoTitleExceptions.ToList(),
            WindowExcludedProcesses = source.WindowExcludedProcesses.ToList()
        };
    }
}
