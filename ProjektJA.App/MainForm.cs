using System.Drawing;
using System.Windows.Forms;

namespace ProjektJA.App;

/// <summary>
/// Główne okno aplikacji z elementami wspólnymi dla obu bibliotek.
/// </summary>
internal sealed class MainForm : Form
{
    private static readonly Color PageColor = Color.FromArgb(244, 247, 251);
    private static readonly Color InkColor = Color.FromArgb(30, 41, 59);
    private static readonly Color MutedColor = Color.FromArgb(100, 116, 139);
    private static readonly Color AccentColor = Color.FromArgb(37, 99, 235);

    private readonly RadioButton _highLevelOption = new() { Text = "C++ — język wysokiego poziomu", AutoSize = true };
    private readonly RadioButton _assemblyOption = new() { Text = "ASM x64 — biblioteka asemblerowa", AutoSize = true };
    private readonly TrackBar _threadSlider = new() { Minimum = 1, Maximum = 64, TickFrequency = 4, SmallChange = 1, LargeChange = 8, Height = 44 };
    private readonly NumericUpDown _threadInput = new() { Minimum = 1, Maximum = 64, Width = 96, TextAlign = HorizontalAlignment.Center };
    private readonly ComboBox _maskSizeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
    private readonly Label _threadSummary = new() { AutoSize = true, ForeColor = MutedColor };
    private readonly Label _selectedBackend = new() { AutoSize = true, ForeColor = AccentColor, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
    private readonly Label _timeValue = new() { Text = "—", AutoSize = true, ForeColor = InkColor, Font = new Font("Segoe UI", 27F, FontStyle.Bold) };
    private readonly Label _statusPill = new() { AutoSize = true, Padding = new Padding(10, 7, 10, 7), BackColor = Color.FromArgb(226, 232, 240), ForeColor = InkColor, Text = "Inicjalizacja" };

    private readonly Button _loadImageButton = new() { Text = "Wczytaj obraz", Width = 280, Height = 42 };
    private readonly Button _runButton = new() { Text = "Uruchom algorytm", Width = 280, Height = 42, Enabled = false };
    private readonly Button _saveButton = new() { Text = "Zapisz wynik", Width = 280, Height = 42, Enabled = false };
    private readonly Button _benchmarkButton = new() { Text = "Benchmark (CSV)", Width = 280, Height = 42, Enabled = false };

    private readonly PictureBox _sourcePreview = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
    private readonly PictureBox _resultPreview = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
    private readonly Label _sourcePlaceholder = new() { Text = "Wczytaj własny obraz, aby zobaczyć podgląd.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = MutedColor, Padding = new Padding(24) };
    private readonly Label _resultPlaceholder = new() { Text = "Wynik pojawi się po uruchomieniu algorytmu.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = MutedColor, Padding = new Padding(24) };

    private NativeBackend? _highLevelBackend;
    private NativeBackend? _assemblyBackend;
    private Bitmap? _inputBitmap;
    private bool _syncingThreadControls;

    public MainForm()
    {
        Text = "Projekt JA — Filtr rozmycia Gaussa";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(1100, 720);
        ClientSize = new Size(1480, 900);
        BackColor = PageColor;
        ForeColor = InkColor;
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildLayout();
        InitializeThreadCount();
        LoadBackends();
        UpdateSelectedBackend();

        _loadImageButton.Click += (_, _) => LoadImage();
        _runButton.Click += (_, _) => RunAlgorithm();
        _saveButton.Click += (_, _) => SaveImage();
        _benchmarkButton.Click += (_, _) => RunBenchmark();
        _highLevelOption.CheckedChanged += (_, _) => UpdateSelectedBackend();
        _assemblyOption.CheckedChanged += (_, _) => UpdateSelectedBackend();
        _threadSlider.ValueChanged += (_, _) => SynchronizeThreadCount(_threadSlider.Value);
        _threadInput.ValueChanged += (_, _) => SynchronizeThreadCount((int)_threadInput.Value);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PageColor,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 2
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var header = BuildHeader();
        root.Controls.Add(header, 0, 0);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = PageColor,
            Padding = new Padding(0, 8, 0, 0)
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 326));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(body, 0, 1);

        body.Controls.Add(BuildControlCard(), 0, 0);
        body.Controls.Add(BuildPreviewArea(), 1, 0);
    }

    private Control BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Fill, BackColor = PageColor };
        var title = new Label
        {
            Text = "Projekt JA",
            AutoSize = true,
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            ForeColor = InkColor,
            Location = new Point(2, 2)
        };
        var subtitle = new Label
        {
            Text = "Filtr rozmycia Gaussa (RGB)",
            AutoSize = true,
            Font = new Font("Segoe UI", 10F),
            ForeColor = MutedColor,
            Location = new Point(4, 48)
        };
        _statusPill.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        header.Controls.Add(_statusPill);
        header.Resize += (_, _) => _statusPill.Location = new Point(header.ClientSize.Width - _statusPill.Width - 4, 18);
        return header;
    }

    private Control BuildControlCard()
    {
        var card = CreateCard();
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.White,
            Padding = new Padding(0, 2, 0, 8)
        };
        card.Controls.Add(flow);

        flow.Controls.Add(SectionTitle("Biblioteka"));
        var backendChoice = new FlowLayoutPanel
        {
            Width = 280,
            Height = 72,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 8, 0, 0),
            BackColor = Color.White
        };
        _highLevelOption.Checked = true;
        _highLevelOption.Margin = new Padding(2, 4, 2, 2);
        _assemblyOption.Margin = new Padding(2, 5, 2, 2);
        backendChoice.Controls.Add(_highLevelOption);
        backendChoice.Controls.Add(_assemblyOption);
        flow.Controls.Add(backendChoice);
        flow.Controls.Add(_selectedBackend);

        flow.Controls.Add(SectionTitle("Rozmiar maski Gaussa"));
        _maskSizeCombo.Items.AddRange(["3x3 (Szybka — promień 1 px)", "5x5 (Średnia — promień 2 px)", "7x7 (Głęboka — promień 3 px)"]);
        _maskSizeCombo.SelectedIndex = 0;
        _maskSizeCombo.Margin = new Padding(0, 6, 0, 4);
        flow.Controls.Add(_maskSizeCombo);

        flow.Controls.Add(SectionTitle("Wątki robocze"));
        var processorCount = Math.Clamp(Environment.ProcessorCount, 1, 64);
        _threadSlider.Value = processorCount;
        _threadInput.Value = processorCount;
        _threadSlider.Width = 280;
        _threadSlider.Margin = new Padding(0, 4, 0, 0);
        flow.Controls.Add(_threadSlider);

        var threadRow = new TableLayoutPanel
        {
            Width = 280,
            Height = 38,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 2)
        };
        threadRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        threadRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var threadLabel = new Label
        {
            Text = "Wybrana liczba wątków",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = InkColor,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _threadInput.Anchor = AnchorStyles.Right;
        threadRow.Controls.Add(threadLabel, 0, 0);
        threadRow.Controls.Add(_threadInput, 1, 0);
        flow.Controls.Add(threadRow);
        _threadSummary.Text = $"Wykryte procesory logiczne: {Environment.ProcessorCount}";
        flow.Controls.Add(_threadSummary);

        flow.Controls.Add(SectionTitle("Czas funkcji"));
        var timerBox = new Panel
        {
            Width = 280,
            Height = 68,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(12),
            Margin = new Padding(0, 8, 0, 4)
        };
        _timeValue.Location = new Point(12, 8);
        timerBox.Controls.Add(_timeValue);
        flow.Controls.Add(timerBox);

        flow.Controls.Add(SectionTitle("Dane wejściowe"));
        _loadImageButton.FlatStyle = FlatStyle.Flat;
        _loadImageButton.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _loadImageButton.BackColor = Color.White;
        _loadImageButton.ForeColor = InkColor;
        _loadImageButton.Margin = new Padding(0, 8, 0, 0);
        flow.Controls.Add(_loadImageButton);

        _runButton.FlatStyle = FlatStyle.Flat;
        _runButton.FlatAppearance.BorderSize = 0;
        _runButton.BackColor = Color.FromArgb(203, 213, 225);
        _runButton.ForeColor = Color.FromArgb(100, 116, 139);
        _runButton.Margin = new Padding(0, 8, 0, 0);
        flow.Controls.Add(_runButton);

        _saveButton.FlatStyle = FlatStyle.Flat;
        _saveButton.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _saveButton.FlatAppearance.BorderSize = 1;
        _saveButton.BackColor = Color.FromArgb(226, 232, 240);
        _saveButton.ForeColor = MutedColor;
        _saveButton.Margin = new Padding(0, 8, 0, 0);
        flow.Controls.Add(_saveButton);

        _benchmarkButton.FlatStyle = FlatStyle.Flat;
        _benchmarkButton.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _benchmarkButton.FlatAppearance.BorderSize = 1;
        _benchmarkButton.BackColor = Color.FromArgb(226, 232, 240);
        _benchmarkButton.ForeColor = MutedColor;
        _benchmarkButton.Margin = new Padding(0, 8, 0, 0);
        flow.Controls.Add(_benchmarkButton);

        return card;
    }

    private Control BuildPreviewArea()
    {
        var area = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = PageColor,
            Padding = new Padding(12, 0, 0, 0)
        };
        area.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        area.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        area.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        area.Controls.Add(BuildImageCard("OBRAZ WEJŚCIOWY", _sourcePreview, _sourcePlaceholder), 0, 0);
        area.Controls.Add(BuildImageCard("WYNIK", _resultPreview, _resultPlaceholder), 1, 0);

        return area;
    }

    private static Control BuildImageCard(string title, PictureBox picture, Label placeholder)
    {
        var card = CreateCard();
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.White
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var heading = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = MutedColor
        };
        var previewFrame = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White
        };
        picture.Dock = DockStyle.Fill;
        placeholder.Dock = DockStyle.Fill;
        previewFrame.Controls.Add(picture);
        previewFrame.Controls.Add(placeholder);
        placeholder.BringToFront();

        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(previewFrame, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private static Panel CreateCard()
    {
        return new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(18),
            Margin = new Padding(0),
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    private static Label SectionTitle(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = InkColor,
            Margin = new Padding(0, 15, 0, 1)
        };
    }

    private void InitializeThreadCount()
    {
        var detected = Math.Clamp(Environment.ProcessorCount, 1, 64);
        _threadSlider.Value = detected;
        _threadInput.Value = detected;
    }

    private void LoadBackends()
    {
        try
        {
            _highLevelBackend = NativeBackend.Load("ProjektJA.HighLevel.dll");
        }
        catch (Exception ex)
        {
            _selectedBackend.Text = $"C++: błąd ({ex.Message})";
        }

        try
        {
            _assemblyBackend = NativeBackend.Load("ProjektJA.Assembly.dll");
        }
        catch (Exception ex)
        {
            if (_highLevelBackend is null)
            {
                _selectedBackend.Text = $"ASM: błąd ({ex.Message})";
            }
        }

        var bothLoaded = _highLevelBackend is not null && _assemblyBackend is not null;
        _statusPill.Text = bothLoaded ? "Obie DLL wczytane" : "Brak biblioteki DLL";
        _statusPill.BackColor = bothLoaded
            ? Color.FromArgb(220, 252, 231)
            : Color.FromArgb(254, 226, 226);
        _statusPill.ForeColor = bothLoaded
            ? Color.FromArgb(22, 101, 52)
            : Color.FromArgb(153, 27, 27);
    }

    private void UpdateSelectedBackend()
    {
        var backend = _assemblyOption.Checked ? _assemblyBackend : _highLevelBackend;
        var label = _assemblyOption.Checked ? "ASM" : "C++";
        _selectedBackend.Text = backend is null
            ? $"{label}: biblioteka niedostępna"
            : $"Aktywna biblioteka: {backend.Name}";
    }

    private void SynchronizeThreadCount(int value)
    {
        if (_syncingThreadControls) return;

        _syncingThreadControls = true;
        _threadSlider.Value = Math.Clamp(value, _threadSlider.Minimum, _threadSlider.Maximum);
        _threadInput.Value = Math.Clamp(value, (int)_threadInput.Minimum, (int)_threadInput.Maximum);
        _syncingThreadControls = false;
    }

    private void LoadImage()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Wybierz obraz do podglądu",
            Filter = "Pliki obrazów|*.bmp;*.png;*.jpg;*.jpeg;*.tif;*.tiff|Wszystkie pliki|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        string extension = Path.GetExtension(dialog.FileName).ToLower();
        if (extension != ".jpg" && extension != ".jpeg" && extension != ".png" && extension != ".bmp")
        {
            MessageBox.Show(this, "Ten format pliku nie jest wspierany przez wbudowany silnik. Użyj standardowego JPG, PNG lub BMP.",
                "Brak wsparcia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            using var loaded = Image.FromFile(dialog.FileName);
            var detachedBitmap = new Bitmap(loaded);
            var previousBitmap = _inputBitmap;
            _inputBitmap = detachedBitmap;
            _sourcePreview.Image = detachedBitmap;
            _sourcePlaceholder.Visible = false;
            _timeValue.Text = "—";
            _runButton.Enabled = true;
            _runButton.BackColor = AccentColor;
            _runButton.ForeColor = Color.White;

            if (_highLevelBackend is not null && _assemblyBackend is not null)
            {
                _benchmarkButton.Enabled = true;
                _benchmarkButton.BackColor = Color.White;
                _benchmarkButton.ForeColor = InkColor;
            }

            var previousResult = _resultPreview.Image;
            _resultPreview.Image = null;
            _resultPlaceholder.Visible = true;
            previousResult?.Dispose();

            _saveButton.Enabled = false;
            _saveButton.BackColor = Color.FromArgb(226, 232, 240);
            _saveButton.ForeColor = MutedColor;

            previousBitmap?.Dispose();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Nie udało się wczytać obrazu.\n\n{ex.Message}",
                "Błąd wczytywania",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _inputBitmap?.Dispose();
        _resultPreview.Image?.Dispose();
        _highLevelBackend?.Dispose();
        _assemblyBackend?.Dispose();
        base.OnFormClosed(e);
    }

    private int GetSelectedMaskSize() => _maskSizeCombo.SelectedIndex switch
    {
        1 => 5,
        2 => 7,
        _ => 3
    };

    private async void RunAlgorithm()
    {
        if (_inputBitmap is null) return;

        NativeBackend? selectedBackend = _highLevelOption.Checked ? _highLevelBackend : _assemblyBackend;
        if (selectedBackend is null)
        {
            MessageBox.Show(this, "Wybrana biblioteka DLL nie została poprawnie załadowana.", "Błąd backendu", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        int maskSize = GetSelectedMaskSize();
        int radius = maskSize / 2;

        if (_inputBitmap.Width <= maskSize || _inputBitmap.Height <= maskSize)
        {
            MessageBox.Show(this, $"Obraz jest zbyt mały dla maski {maskSize}x{maskSize}.", "Zbyt mały obraz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        int threads = (int)_threadInput.Value;

        _runButton.Enabled = false;
        _loadImageButton.Enabled = false;
        _benchmarkButton.Enabled = false;

        using Bitmap normalizedInput = _inputBitmap.Clone(
            new Rectangle(0, 0, _inputBitmap.Width, _inputBitmap.Height),
            System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        Bitmap outputBitmap = new Bitmap(normalizedInput.Width, normalizedInput.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        var rect = new Rectangle(0, 0, normalizedInput.Width, normalizedInput.Height);
        var inData = normalizedInput.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, normalizedInput.PixelFormat);
        var outData = outputBitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, outputBitmap.PixelFormat);

        try
        {
            int width = inData.Width;
            int height = inData.Height;
            int stride = inData.Stride;
            nint inScan0 = inData.Scan0;
            nint outScan0 = outData.Scan0;

            int usableHeight = height - 2 * radius;
            int rowsPerThread = usableHeight / threads;
            int remainder = usableHeight % threads;

            Task[] tasks = new Task[threads];
            int currentY = radius;

            var sw = System.Diagnostics.Stopwatch.StartNew();

            for (int t = 0; t < threads; t++)
            {
                int startY = currentY;
                int endY = startY + rowsPerThread + (t < remainder ? 1 : 0);
                currentY = endY;

                tasks[t] = Task.Run(() =>
                {
                    if (startY < endY)
                    {
                        int status = selectedBackend.Process(inScan0, outScan0, width, stride, startY, endY, maskSize);
                        if (status != 0)
                        {
                            throw new Exception($"Biblioteka natywna zwróciła błąd (Kod: {status}).");
                        }
                    }
                });
            }

            await Task.WhenAll(tasks);
            sw.Stop();

            normalizedInput.UnlockBits(inData);
            outputBitmap.UnlockBits(outData);

            var oldResult = _resultPreview.Image;
            _resultPreview.Image = outputBitmap;
            _resultPlaceholder.Visible = false;
            oldResult?.Dispose();

            _saveButton.Enabled = true;
            _saveButton.BackColor = Color.White;
            _saveButton.ForeColor = InkColor;

            _timeValue.Text = $"{sw.Elapsed.TotalMilliseconds:F2} ms";
        }
        catch (Exception ex)
        {
            normalizedInput.UnlockBits(inData);
            outputBitmap.UnlockBits(outData);
            outputBitmap.Dispose();

            _timeValue.Text = "Błąd";

            MessageBox.Show(
                this,
                $"Wystąpił błąd podczas przetwarzania:\n\n{ex.InnerException?.Message ?? ex.Message}",
                "Błąd wykonania",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _runButton.Enabled = true;
            _loadImageButton.Enabled = true;
            if (_highLevelBackend is not null && _assemblyBackend is not null)
            {
                _benchmarkButton.Enabled = true;
            }
        }
    }

    private void SaveImage()
    {
        if (_resultPreview.Image is null) return;

        using var dialog = new SaveFileDialog
        {
            Title = "Zapisz przetworzony obraz",
            Filter = "Obraz PNG (*.png)|*.png|Mapa bitowa BMP (*.bmp)|*.bmp|Obraz JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg",
            DefaultExt = "png",
            FileName = "wynik_gauss"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            var format = ext switch
            {
                ".bmp" => System.Drawing.Imaging.ImageFormat.Bmp,
                ".jpg" or ".jpeg" => System.Drawing.Imaging.ImageFormat.Jpeg,
                _ => System.Drawing.Imaging.ImageFormat.Png
            };

            _resultPreview.Image.Save(dialog.FileName, format);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Nie udało się zapisać obrazu.\n\n{ex.Message}",
                "Błąd zapisu",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async void RunBenchmark()
    {
        if (_inputBitmap is null || _highLevelBackend is null || _assemblyBackend is null) return;

        int maskSize = GetSelectedMaskSize();

        if (_inputBitmap.Width <= maskSize || _inputBitmap.Height <= maskSize)
        {
            MessageBox.Show(this, $"Obraz jest zbyt mały dla maski {maskSize}x{maskSize}.", "Zbyt mały obraz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Zapisz wyniki benchmarku do CSV",
            Filter = "Plik CSV (*.csv)|*.csv",
            DefaultExt = "csv",
            FileName = $"benchmark_{_inputBitmap.Width}x{_inputBitmap.Height}_maska{maskSize}x{maskSize}.csv"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        int maxThreads = (int)_threadInput.Maximum;
        const int iterations = 10;

        _runButton.Enabled = false;
        _loadImageButton.Enabled = false;
        _benchmarkButton.Enabled = false;

        using Bitmap normalizedInput = _inputBitmap.Clone(
            new Rectangle(0, 0, _inputBitmap.Width, _inputBitmap.Height),
            System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        using Bitmap outputBitmap = new Bitmap(normalizedInput.Width, normalizedInput.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        var rect = new Rectangle(0, 0, normalizedInput.Width, normalizedInput.Height);
        var inData = normalizedInput.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, normalizedInput.PixelFormat);
        var outData = outputBitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, outputBitmap.PixelFormat);

        var csvBuilder = new System.Text.StringBuilder();
        csvBuilder.AppendLine("Liczba watkow;Sredni czas C++ [ms];Sredni czas ASM [ms];Przyspieszenie (C++/ASM)");

        var plCulture = System.Globalization.CultureInfo.GetCultureInfo("pl-PL");

        try
        {
            for (int threads = 1; threads <= maxThreads; threads++)
            {
                double cppAvg = await MeasureBackendAsync(_highLevelBackend, inData, outData, threads, iterations, maskSize);
                double asmAvg = await MeasureBackendAsync(_assemblyBackend, inData, outData, threads, iterations, maskSize);

                double speedup = asmAvg > 0 ? cppAvg / asmAvg : 0;

                csvBuilder.AppendLine(string.Format(plCulture, "{0};{1:F4};{2:F4};{3:F2}", threads, cppAvg, asmAvg, speedup));
            }

            File.WriteAllText(dialog.FileName, csvBuilder.ToString(), System.Text.Encoding.UTF8);
            MessageBox.Show(this, $"Zakończono pełny test (1–{maxThreads} wątków, maska {maskSize}x{maskSize})!\nWyniki zapisano w:\n{dialog.FileName}", "Benchmark gotowy", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Błąd podczas benchmarku:\n\n{ex.InnerException?.Message ?? ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            normalizedInput.UnlockBits(inData);
            outputBitmap.UnlockBits(outData);

            _runButton.Enabled = true;
            _loadImageButton.Enabled = true;
            _benchmarkButton.Enabled = true;
        }
    }

    private async Task<double> MeasureBackendAsync(
        NativeBackend backend,
        System.Drawing.Imaging.BitmapData inData,
        System.Drawing.Imaging.BitmapData outData,
        int threads,
        int iterations,
        int maskSize)
    {
        int radius = maskSize / 2;
        int usableHeight = inData.Height - 2 * radius;
        int rowsPerThread = usableHeight / threads;
        int remainder = usableHeight % threads;

        Task RunSinglePassAsync()
        {
            Task[] tasks = new Task[threads];
            int currentY = radius;

            for (int t = 0; t < threads; t++)
            {
                int startY = currentY;
                int endY = startY + rowsPerThread + (t < remainder ? 1 : 0);
                currentY = endY;

                tasks[t] = Task.Run(() =>
                {
                    if (startY < endY)
                    {
                        int status = backend.Process(inData.Scan0, outData.Scan0, inData.Width, inData.Stride, startY, endY, maskSize);
                        if (status != 0) throw new Exception($"Kod błędu DLL: {status}");
                    }
                });
            }
            return Task.WhenAll(tasks);
        }

        await RunSinglePassAsync();

        double totalMs = 0;
        for (int i = 1; i <= iterations; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await RunSinglePassAsync();
            sw.Stop();
            totalMs += sw.Elapsed.TotalMilliseconds;
        }

        double avgMs = totalMs / iterations;
        _timeValue.Text = $"{avgMs:F2} ms";
        return avgMs;
    }
}