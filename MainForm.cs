using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DepthLayerStudio;

public sealed class MainForm : Form
{
    private readonly ComboBox materialBox = new();
    private readonly NumericUpDown maxEdgeBox = new();
    private readonly PictureBox sourcePreview = new();
    private readonly FlowLayoutPanel maskPanel = new();
    private readonly DataGridView layerGrid = new();
    private readonly TextBox notesBox = new();
    private readonly Label statusLabel = new();
    private readonly Button sliceButton = new();
    private readonly Button exportButton = new();
    private readonly Button sendButton = new();
    private readonly Button openFolderButton = new();

    private string currentMaterial = "Baltic birch";
    private string? photoPath;
    private string? exportFolder;
    private List<LayerSpec> layerSpecs = MaterialPresets.Create("Baltic birch").Select(layer => layer.Clone()).ToList();
    private SliceResult? currentResult;
    private Image? sourcePreviewImage;
    private bool slicesDirty;

    public MainForm()
    {
        Text = "DepthLayer Studio for LightBurn";
        MinimumSize = new Size(1180, 760);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        BackColor = Color.FromArgb(12, 18, 28);

        BuildLayout();
        LoadLayerGrid();
        UpdateNotes();
        SetStatus("Choose a photo to begin.");
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        sourcePreviewImage?.Dispose();

        foreach (var layer in layerSpecs)
        {
            layer.Mask?.Dispose();
        }

        if (currentResult is not null)
        {
            foreach (var layer in currentResult.Layers)
            {
                layer.Mask?.Dispose();
            }
        }

        base.OnFormClosed(e);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            BackColor = BackColor,
            Padding = new Padding(14),
        };

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        root.Controls.Add(BuildToolbar(), 0, 0);
        root.Controls.Add(BuildMainArea(), 0, 1);

        statusLabel.Dock = DockStyle.Fill;
        statusLabel.ForeColor = Color.FromArgb(188, 202, 219);
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        root.Controls.Add(statusLabel, 0, 2);

        Controls.Add(root);
    }

    private Control BuildToolbar()
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(16, 27, 42),
            Padding = new Padding(12),
            WrapContents = false,
            AutoScroll = true,
        };

        var openButton = CreateButton("Open photo", Color.FromArgb(182, 240, 156), Color.FromArgb(8, 17, 31));
        openButton.Click += (_, _) => OpenPhoto();

        sliceButton.Text = "Slice photo";
        StyleButton(sliceButton, Color.FromArgb(182, 240, 156), Color.FromArgb(8, 17, 31));
        sliceButton.Click += (_, _) => RunSlice(true);

        exportButton.Text = "Export project";
        StyleButton(exportButton, Color.FromArgb(110, 168, 254), Color.White);
        exportButton.Click += (_, _) => ExportMasks();

        sendButton.Text = "Send to LightBurn";
        StyleButton(sendButton, Color.FromArgb(255, 214, 107), Color.FromArgb(8, 17, 31));
        sendButton.Click += (_, _) => SendToLightBurn();

        openFolderButton.Text = "Open export folder";
        StyleButton(openFolderButton, Color.FromArgb(40, 54, 74), Color.White);
        openFolderButton.Click += (_, _) => OpenExportFolder();

        materialBox.DropDownStyle = ComboBoxStyle.DropDownList;
        materialBox.Width = 170;
        materialBox.Items.AddRange(new object[] { "Baltic birch", "Opaque acrylic", "Coated slate" });
        materialBox.SelectedItem = currentMaterial;
        materialBox.SelectedIndexChanged += (_, _) => ChangeMaterial();

        maxEdgeBox.Minimum = 300;
        maxEdgeBox.Maximum = 2200;
        maxEdgeBox.Increment = 100;
        maxEdgeBox.Value = 900;
        maxEdgeBox.Width = 92;

        toolbar.Controls.Add(openButton);
        toolbar.Controls.Add(sliceButton);
        toolbar.Controls.Add(exportButton);
        toolbar.Controls.Add(sendButton);
        toolbar.Controls.Add(openFolderButton);
        toolbar.Controls.Add(CreateToolbarLabel("Material"));
        toolbar.Controls.Add(materialBox);
        toolbar.Controls.Add(CreateToolbarLabel("Mask px"));
        toolbar.Controls.Add(maxEdgeBox);

        return toolbar;
    }

    private Control BuildMainArea()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 510,
            BackColor = BackColor,
        };

        split.Panel1.Controls.Add(BuildPreviewPanel());
        split.Panel2.Controls.Add(BuildSettingsPanel());

        return split;
    }

    private Control BuildPreviewPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            BackColor = BackColor,
            Padding = new Padding(0, 0, 12, 0),
        };

        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 48));

        panel.Controls.Add(CreateSectionLabel("Photo preview"), 0, 0);

        sourcePreview.Dock = DockStyle.Fill;
        sourcePreview.BackColor = Color.FromArgb(20, 30, 44);
        sourcePreview.SizeMode = PictureBoxSizeMode.Zoom;
        panel.Controls.Add(WrapCard(sourcePreview), 0, 1);

        panel.Controls.Add(CreateSectionLabel("Layer masks"), 0, 2);

        maskPanel.Dock = DockStyle.Fill;
        maskPanel.BackColor = Color.FromArgb(20, 30, 44);
        maskPanel.AutoScroll = true;
        maskPanel.Padding = new Padding(10);
        panel.Controls.Add(WrapCard(maskPanel), 0, 3);

        return panel;
    }

    private Control BuildSettingsPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            BackColor = BackColor,
        };

        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 285));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        panel.Controls.Add(CreateSectionLabel("Layer settings"), 0, 0);

        ConfigureLayerGrid();
        panel.Controls.Add(WrapCard(layerGrid), 0, 1);

        panel.Controls.Add(CreateSectionLabel("LightBurn handoff notes"), 0, 2);

        notesBox.Dock = DockStyle.Fill;
        notesBox.Multiline = true;
        notesBox.ReadOnly = true;
        notesBox.ScrollBars = ScrollBars.Vertical;
        notesBox.BackColor = Color.FromArgb(8, 17, 31);
        notesBox.ForeColor = Color.FromArgb(220, 230, 240);
        notesBox.BorderStyle = BorderStyle.None;
        notesBox.Font = new Font("Consolas", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        panel.Controls.Add(WrapCard(notesBox), 0, 3);

        return panel;
    }

    private void ConfigureLayerGrid()
    {
        layerGrid.Dock = DockStyle.Fill;
        layerGrid.AllowUserToAddRows = false;
        layerGrid.AllowUserToDeleteRows = false;
        layerGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        layerGrid.BackgroundColor = Color.FromArgb(20, 30, 44);
        layerGrid.BorderStyle = BorderStyle.None;
        layerGrid.GridColor = Color.FromArgb(54, 68, 88);
        layerGrid.RowHeadersVisible = false;
        layerGrid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        layerGrid.EnableHeadersVisualStyles = false;
        layerGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 43, 61);
        layerGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        layerGrid.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
        layerGrid.DefaultCellStyle.BackColor = Color.FromArgb(20, 30, 44);
        layerGrid.DefaultCellStyle.ForeColor = Color.FromArgb(235, 241, 248);
        layerGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(46, 75, 111);
        layerGrid.DefaultCellStyle.SelectionForeColor = Color.White;

        layerGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "On", FillWeight = 42 });
        layerGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Label", HeaderText = "Layer", ReadOnly = true, FillWeight = 110 });
        layerGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Cutoff", HeaderText = "Cutoff %", FillWeight = 72 });
        layerGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Depth", HeaderText = "Depth mm", FillWeight = 78 });
        layerGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Passes", HeaderText = "Passes", FillWeight = 65 });
        layerGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Power", HeaderText = "Power %", FillWeight = 70 });
        layerGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Speed", HeaderText = "Speed", FillWeight = 76 });
        layerGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Coverage", HeaderText = "Coverage", ReadOnly = true, FillWeight = 80 });

        layerGrid.CellEndEdit += (_, _) => MarkSlicesOutOfDate();
        layerGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (layerGrid.IsCurrentCellDirty)
            {
                layerGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                MarkSlicesOutOfDate();
            }
        };
    }

    private static Control WrapCard(Control child)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            BackColor = Color.FromArgb(20, 30, 44),
        };

        child.Dock = DockStyle.Fill;
        panel.Controls.Add(child);
        return panel;
    }

    private static Label CreateSectionLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleLeft,
        };
    }

    private static Label CreateToolbarLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = false,
            Width = 70,
            Height = 38,
            ForeColor = Color.FromArgb(188, 202, 219),
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(16, 0, 4, 0),
        };
    }

    private static Button CreateButton(string text, Color backColor, Color foreColor)
    {
        var button = new Button { Text = text };
        StyleButton(button, backColor, foreColor);
        return button;
    }

    private static void StyleButton(Button button, Color backColor, Color foreColor)
    {
        button.AutoSize = false;
        button.Width = 140;
        button.Height = 38;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = backColor;
        button.ForeColor = foreColor;
        button.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point);
        button.Margin = new Padding(0, 0, 10, 0);
    }

    private void OpenPhoto()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose a photo",
            Filter = "Image files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff|All files|*.*",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        photoPath = dialog.FileName;
        DisposeCurrentResult();
        currentResult = null;
        exportFolder = null;
        slicesDirty = false;
        ClearSavedPaths();
        sourcePreviewImage?.Dispose();
        sourcePreviewImage = ImageSlicer.CreatePreviewImage(photoPath, 900, 700);
        sourcePreview.Image = sourcePreviewImage;
        maskPanel.Controls.Clear();
        UpdateNotes();
        SetStatus("Photo loaded. Slice it to generate LightBurn masks.");
    }

    private void ChangeMaterial()
    {
        if (materialBox.SelectedItem is not string material)
        {
            return;
        }

        currentMaterial = material;
        DisposeLayerMasks();
        DisposeCurrentResult();
        layerSpecs = MaterialPresets.Create(currentMaterial).Select(layer => layer.Clone()).ToList();
        currentResult = null;
        exportFolder = null;
        slicesDirty = false;
        LoadLayerGrid();
        maskPanel.Controls.Clear();
        UpdateNotes();
        SetStatus($"Material preset changed to {currentMaterial}. Slice again when ready.");
    }

    private void RunSlice(bool showMessages)
    {
        if (string.IsNullOrWhiteSpace(photoPath) || !File.Exists(photoPath))
        {
            MessageBox.Show(this, "Choose a photo first.", "DepthLayer Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var selectedPhotoPath = photoPath!;

        try
        {
            ReadLayerGrid();
            UseWaitCursor = true;
            ToggleWorkButtons(false);
            SetStatus("Slicing photo into depth masks...");
            Refresh();

            DisposeCurrentResult();
            currentResult = ImageSlicer.Slice(selectedPhotoPath, layerSpecs, (int)maxEdgeBox.Value);
            DisposeLayerMasks();
            layerSpecs = currentResult.Layers.Select(layer => layer.Clone()).ToList();
            exportFolder = null;
            slicesDirty = false;

            LoadLayerGrid();
            RenderMasks();
            UpdateNotes();
            SetStatus($"Generated {currentResult.Layers.Count(layer => layer.Enabled)} mask(s). Review, then export.");
        }
        catch (Exception exception)
        {
            if (showMessages)
            {
                MessageBox.Show(this, exception.Message, "Could not slice photo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            SetStatus("The photo could not be sliced.");
        }
        finally
        {
            ToggleWorkButtons(true);
            UseWaitCursor = false;
        }
    }

    private void ExportMasks()
    {
        if (currentResult is null || slicesDirty)
        {
            RunSlice(false);
        }

        if (currentResult is null)
        {
            return;
        }

        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose where to save the LightBurn mask set",
            SelectedPath = exportFolder ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "DepthLayer Exports"),
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            ReadLayerGrid();
            ApplyGridSettingsToResult();
            exportFolder = dialog.SelectedPath;
            var notes = ImageSlicer.BuildNotes(currentResult, currentMaterial, exportFolder);
            var saved = ImageSlicer.Export(currentResult, exportFolder, notes);
            notes = ImageSlicer.BuildNotes(currentResult, currentMaterial, exportFolder);
            File.WriteAllText(saved[0], notes);
            UpdateNotes();
            var maskCount = currentResult.Layers.Count(layer => layer.Enabled && !string.IsNullOrWhiteSpace(layer.SavedPath));
            SetStatus($"Exported {maskCount} PNG mask(s), a LightBurn project, and notes to {exportFolder}.");
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("Export failed.");
        }
    }

    private void SendToLightBurn()
    {
        if (currentResult is null)
        {
            MessageBox.Show(this, "Slice and export the LightBurn project first.", "DepthLayer Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (slicesDirty)
        {
            MessageBox.Show(this, "Slice and export again so LightBurn receives the latest masks and settings.", "DepthLayer Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var missingExport = currentResult.Layers
            .Where(layer => layer.Enabled)
            .Any(layer => string.IsNullOrWhiteSpace(layer.SavedPath) || !File.Exists(layer.SavedPath));

        var missingProject = string.IsNullOrWhiteSpace(currentResult.ProjectPath) || !File.Exists(currentResult.ProjectPath);

        if (missingExport || missingProject)
        {
            MessageBox.Show(this, "Export the LightBurn project first so the layer settings can be sent.", "DepthLayer Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            SetStatus("Opening LightBurn project...");
            Refresh();

            var result = LightBurnBridge.OpenProject(currentResult.ProjectPath!);

            MessageBox.Show(
                this,
                result.Message,
                "LightBurn import",
                MessageBoxButtons.OK,
                result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

            SetStatus(result.Message);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "LightBurn import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("LightBurn import failed.");
        }
    }

    private void OpenExportFolder()
    {
        if (string.IsNullOrWhiteSpace(exportFolder) || !Directory.Exists(exportFolder))
        {
            MessageBox.Show(this, "Export a mask set first.", "DepthLayer Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = exportFolder,
            UseShellExecute = true,
        });
    }

    private void LoadLayerGrid()
    {
        layerGrid.Rows.Clear();

        foreach (var layer in layerSpecs)
        {
            layerGrid.Rows.Add(
                layer.Enabled,
                layer.Label,
                Math.Round(layer.Threshold * 100),
                layer.DepthMm.ToString("0.0", CultureInfo.InvariantCulture),
                layer.Passes,
                layer.Power,
                layer.Speed,
                layer.Mask is null ? "-" : $"{layer.Coverage:0.0}%");
        }
    }

    private void ReadLayerGrid()
    {
        for (var index = 0; index < layerGrid.Rows.Count && index < layerSpecs.Count; index += 1)
        {
            var row = layerGrid.Rows[index];
            var layer = layerSpecs[index];

            layer.Enabled = ReadBool(row.Cells["Enabled"].Value);
            layer.Threshold = Clamp(ReadDouble(row.Cells["Cutoff"].Value, layer.Threshold * 100) / 100.0, 0.02, 0.98);
            layer.DepthMm = Clamp(ReadDouble(row.Cells["Depth"].Value, layer.DepthMm), 0.3, 4);
            layer.Passes = (int)Clamp(ReadDouble(row.Cells["Passes"].Value, layer.Passes), 1, 3);
            layer.Power = (int)Clamp(ReadDouble(row.Cells["Power"].Value, layer.Power), 1, 100);
            layer.Speed = (int)Clamp(ReadDouble(row.Cells["Speed"].Value, layer.Speed), 100, 10000);
        }
    }

    private void ApplyGridSettingsToResult()
    {
        if (currentResult is null)
        {
            return;
        }

        foreach (var resultLayer in currentResult.Layers)
        {
            var edited = layerSpecs.FirstOrDefault(layer => layer.Key == resultLayer.Key);

            if (edited is null)
            {
                continue;
            }

            resultLayer.Enabled = edited.Enabled;
            resultLayer.Threshold = edited.Threshold;
            resultLayer.DepthMm = edited.DepthMm;
            resultLayer.Passes = edited.Passes;
            resultLayer.Power = edited.Power;
            resultLayer.Speed = edited.Speed;
        }
    }

    private void RenderMasks()
    {
        maskPanel.Controls.Clear();

        foreach (var layer in layerSpecs.Where(layer => layer.Mask is not null))
        {
            var card = new Panel
            {
                Width = 210,
                Height = 178,
                BackColor = Color.FromArgb(28, 40, 58),
                Padding = new Padding(8),
                Margin = new Padding(6),
            };

            var title = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                Text = $"{layer.Label}  {layer.Coverage:0.0}%",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point),
            };

            var image = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Image = layer.Mask is null ? null : new Bitmap(layer.Mask),
                SizeMode = PictureBoxSizeMode.Zoom,
            };

            image.Disposed += (_, _) => image.Image?.Dispose();
            card.Controls.Add(image);
            card.Controls.Add(title);
            maskPanel.Controls.Add(card);
        }
    }

    private void UpdateNotes()
    {
        if (currentResult is null)
        {
        notesBox.Text =
                "DepthLayer Studio will create a LightBurn project, numbered PNG masks, and a notes file.\r\n\r\n" +
                "1. Open a photo.\r\n" +
                "2. Pick a material preset.\r\n" +
                "3. Slice the photo.\r\n" +
                "4. Export the project.\r\n" +
                "5. Send to LightBurn and run a scrap-material test.";
            return;
        }

        notesBox.Text = ImageSlicer.BuildNotes(currentResult, currentMaterial, exportFolder).Replace("\n", "\r\n");
    }

    private void MarkSlicesOutOfDate()
    {
        slicesDirty = true;
        exportFolder = null;
        ClearSavedPaths();

        if (currentResult is not null)
        {
            SetStatus("Layer settings changed. Slice again before exporting for updated masks.");
        }
    }

    private void DisposeLayerMasks()
    {
        foreach (var layer in layerSpecs)
        {
            layer.Mask?.Dispose();
            layer.Mask = null;
        }
    }

    private void DisposeCurrentResult()
    {
        if (currentResult is null)
        {
            return;
        }

        foreach (var layer in currentResult.Layers)
        {
            layer.Mask?.Dispose();
            layer.Mask = null;
        }
    }

    private void ClearSavedPaths()
    {
        foreach (var layer in layerSpecs)
        {
            layer.SavedPath = null;
        }

        if (currentResult is not null)
        {
            currentResult.ProjectPath = null;

            foreach (var layer in currentResult.Layers)
            {
                layer.SavedPath = null;
            }
        }
    }

    private void ToggleWorkButtons(bool enabled)
    {
        sliceButton.Enabled = enabled;
        exportButton.Enabled = enabled;
        sendButton.Enabled = enabled;
        openFolderButton.Enabled = enabled;
    }

    private void SetStatus(string message)
    {
        statusLabel.Text = message;
    }

    private static bool ReadBool(object? value)
    {
        return value is bool boolean && boolean;
    }

    private static double ReadDouble(object? value, double fallback)
    {
        if (value is null)
        {
            return fallback;
        }

        return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }
}
