using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Morfologia;

internal sealed class MainForm : Form
{
    private readonly PictureBox _originalPicture = CreatePictureBox();
    private readonly PictureBox _resultPicture = CreatePictureBox();
    private readonly Label _originalTitle = CreateImageTitle("Imagem original");
    private readonly Label _resultTitle = CreateImageTitle("Resultado");
    private readonly ToolStripMenuItem _filtersMenu = new("Filtros");
    private readonly Button[] _filterButtons;
    private Bitmap? _sourceImage;
    private Bitmap? _resultImage;

    public MainForm()
    {
        Text = "Morfologia - Processamento de Imagens";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(800, 520);
        Size = new Size(1100, 720);

        var openItem = new ToolStripMenuItem("Abrir imagem...");
        openItem.Click += (_, _) => OpenImage();

        var exitItem = new ToolStripMenuItem("Sair");
        exitItem.Click += (_, _) => Close();

        var fileMenu = new ToolStripMenuItem("Arquivo");
        fileMenu.DropDownItems.Add(openItem);
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(exitItem);

        _filtersMenu.Enabled = false;
        AddFilterOption("Dilatação (DMA)", Filters.dilatacao_DMA);
        AddFilterOption("Dilatação (sem DMA)", Filters.dilatacao_semDMA);
        AddFilterOption("Erosão (DMA)", Filters.erosao_DMA);
        AddFilterOption("Erosão (sem DMA)", Filters.erosao_semDMA);

        var menu = new MenuStrip();
        menu.Items.Add(fileMenu);
        menu.Items.Add(_filtersMenu);
        MainMenuStrip = menu;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(8),
            WrapContents = true
        };
        toolbar.Controls.Add(CreateButton("Abrir imagem", (_, _) => OpenImage()));
        _filterButtons =
        [
            CreateFilterButton("Dilatação (DMA)", Filters.dilatacao_DMA),
            CreateFilterButton("Dilatação (sem DMA)", Filters.dilatacao_semDMA),
            CreateFilterButton("Erosão (DMA)", Filters.erosao_DMA),
            CreateFilterButton("Erosão (sem DMA)", Filters.erosao_semDMA)
        ];
        toolbar.Controls.AddRange(_filterButtons);
        SetFilterButtonsEnabled(false);

        var imageLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(10)
        };
        imageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        imageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        imageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        imageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        imageLayout.Controls.Add(_originalTitle, 0, 0);
        imageLayout.Controls.Add(_resultTitle, 1, 0);
        imageLayout.Controls.Add(_originalPicture, 0, 1);
        imageLayout.Controls.Add(_resultPicture, 1, 1);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(menu, 0, 0);
        layout.Controls.Add(toolbar, 0, 1);
        layout.Controls.Add(imageLayout, 0, 2);
        Controls.Add(layout);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _sourceImage?.Dispose();
        _resultImage?.Dispose();
        base.OnFormClosed(e);
    }

    private void AddFilterOption(string title, Action<Bitmap, Bitmap> filter)
    {
        var item = new ToolStripMenuItem(title);
        item.Click += (_, _) => ApplyFilter(filter, title);
        _filtersMenu.DropDownItems.Add(item);
    }

    private void OpenImage()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Abrir imagem",
            Filter = "Imagens|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff|Todos os arquivos|*.*"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            using var loadedImage = Image.FromFile(dialog.FileName);
            var newSource = ConvertTo24Bpp(loadedImage);
            var oldSource = _sourceImage;
            _sourceImage = newSource;
            _originalPicture.Image = newSource;
            oldSource?.Dispose();

            ClearResult();
            _filtersMenu.Enabled = true;
            SetFilterButtonsEnabled(true);
            Text = $"Morfologia - {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception exception) when (
            exception is ArgumentException or ExternalException or IOException or OutOfMemoryException)
        {
            MessageBox.Show(
                this,
                $"Não foi possível abrir a imagem.\n{exception.Message}",
                "Erro ao abrir imagem",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ApplyFilter(Action<Bitmap, Bitmap> filter, string filterName)
    {
        if (_sourceImage is null)
            return;

        var output = new Bitmap(_sourceImage.Width, _sourceImage.Height, PixelFormat.Format24bppRgb);
        try
        {
            filter(_sourceImage, output);
            var previousResult = _resultImage;
            _resultImage = output;
            _resultPicture.Image = output;
            previousResult?.Dispose();
            _resultTitle.Text = $"Resultado - {filterName}";
        }
        catch (Exception exception)
        {
            output.Dispose();
            MessageBox.Show(
                this,
                $"Não foi possível aplicar o filtro.\n{exception.Message}",
                "Erro ao aplicar filtro",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ClearResult()
    {
        _resultPicture.Image = null;
        _resultImage?.Dispose();
        _resultImage = null;
        _resultTitle.Text = "Resultado";
    }

    private void SetFilterButtonsEnabled(bool enabled)
    {
        foreach (var button in _filterButtons)
            button.Enabled = enabled;
    }

    private static Bitmap ConvertTo24Bpp(Image image)
    {
        var bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        graphics.DrawImageUnscaled(image, 0, 0);
        return bitmap;
    }

    private static PictureBox CreatePictureBox() => new()
    {
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.FixedSingle,
        SizeMode = PictureBoxSizeMode.Zoom,
        BackColor = Color.FromArgb(245, 245, 245)
    };

    private static Label CreateImageTitle(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
    };

    private static Button CreateButton(string text, EventHandler onClick)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 34,
            Margin = new Padding(4)
        };
        button.Click += onClick;
        return button;
    }

    private Button CreateFilterButton(string text, Action<Bitmap, Bitmap> filter)
    {
        var button = CreateButton(text, (_, _) => ApplyFilter(filter, text));
        button.Enabled = false;
        return button;
    }
}
