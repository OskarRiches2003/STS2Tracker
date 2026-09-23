namespace RunParser;

internal sealed class MainForm : Form
{
    private readonly TextBox _folderPath = new();
    private readonly Button _browseButton = new();
    private readonly Button _parseButton = new();
    private readonly TextBox _results = new();
    private readonly Label _status = new();

    public MainForm()
    {
        Text = "Slay the Spire 2 Run Parser";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 520);
        Size = new Size(920, 680);
        Font = new Font("Segoe UI", 9F);

        var instructions = new Label
        {
            Text = "Choose the folder containing your .run files, then select Parse runs.",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };

        _folderPath.Dock = DockStyle.Fill;
        _folderPath.ReadOnly = true;
        _folderPath.PlaceholderText = "Select your run-history folder";
        _folderPath.Margin = new Padding(0, 0, 8, 0);

        _browseButton.Text = "Choose folder…";
        _browseButton.AutoSize = true;
        _browseButton.Click += ChooseFolder;

        _parseButton.Text = "Parse runs";
        _parseButton.AutoSize = true;
        _parseButton.Enabled = false;
        _parseButton.Click += ParseRuns;

        var folderRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 8)
        };
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        folderRow.Controls.Add(_folderPath, 0, 0);
        folderRow.Controls.Add(_browseButton, 1, 0);
        folderRow.Controls.Add(_parseButton, 2, 0);

        _results.Dock = DockStyle.Fill;
        _results.ReadOnly = true;
        _results.Multiline = true;
        _results.ScrollBars = ScrollBars.Both;
        _results.WordWrap = false;
        _results.Font = new Font("Consolas", 9F);
        _results.Text = "Your run summary will appear here.";

        _status.Dock = DockStyle.Bottom;
        _status.AutoSize = true;
        _status.Padding = new Padding(0, 8, 0, 0);
        _status.Text = "Choose a folder to get started.";

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 1,
            RowCount = 4
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(instructions, 0, 0);
        layout.Controls.Add(folderRow, 0, 1);
        layout.Controls.Add(_results, 0, 2);
        layout.Controls.Add(_status, 0, 3);
        Controls.Add(layout);
    }

    private void ChooseFolder(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose the folder where the game stores your .run files",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (Directory.Exists(_folderPath.Text))
            dialog.InitialDirectory = _folderPath.Text;

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _folderPath.Text = dialog.SelectedPath;
        _parseButton.Enabled = true;
        _status.Text = "Ready to parse .run files in the selected folder.";
    }

    private async void ParseRuns(object? sender, EventArgs e)
    {
        var folder = _folderPath.Text;
        if (!Directory.Exists(folder))
        {
            MessageBox.Show(this, "Please choose a folder that exists.", "Folder not found", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _parseButton.Enabled = false;
        _browseButton.Enabled = false;
        _status.Text = "Reading run files…";
        _results.Clear();

        try
        {
            var result = await Task.Run(() => RunParserService.ParseFolder(folder));
            _results.Text = result.Report;
            _status.Text = result.FileCount == 0
                ? "No .run files were found. Backup files ending in .run.backup are ignored."
                : $"Finished. Parsed {result.ParsedCount} of {result.FileCount} .run files.";
        }
        catch (Exception exception)
        {
            _status.Text = "Could not read that folder.";
            MessageBox.Show(this, exception.Message, "Could not parse runs", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _browseButton.Enabled = true;
            _parseButton.Enabled = Directory.Exists(_folderPath.Text);
        }
    }
}
